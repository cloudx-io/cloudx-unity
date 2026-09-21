using System.Collections;
using CloudX;
using CloudX.Demo;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;

/*
 * Demo-only host for the Trusted Arbiter interstitial controller. Nothing in
 * this file is meant to be copied into a publisher integration. It brings both
 * SDKs up, turns controller events into status text, and retries terminal
 * failures with backoff. Every ad SDK call lives in
 * ArbiterInterstitialController.cs.
 */
[RequireComponent(typeof(AdScreenUi))]
public class ArbiterScreen : MonoBehaviour
{
    private const string TAG = "CloudXUnityDemo";
    private const float InitializationUiTimeoutSeconds = 15f;
    private const float RetryBaseDelaySeconds = 2f;
    private const float RetryMaxDelaySeconds = 60f;

    private AdScreenUi _ui;
    private ArbiterInterstitialController _interstitial;
    private bool _cloudXInitAnswered;
    private int _interstitialRetries;
    private string _cloudXStatus = "CloudX: Initializing";
    private string _adMobStatus = "AdMob: Initializing";

    private static void Log(string message) => Debug.Log($"[{TAG}][Arbiter] {message}");

    void Awake()
    {
        _ui = GetComponent<AdScreenUi>();
    }

    IEnumerator Start()
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        _ui.Bind(new AdScreenUi.Actions
        {
            ShowBanner = () => { },
            ToggleMrec = () => { },
            ShowInterstitial = ShowInterstitial,
            ShowRewarded = () => { },
            OnOrientationChanged = _ => { },
        });
        _ui.SetButtonVisible(_ui.showBannerButton, false);
        _ui.SetButtonVisible(_ui.showMrecButton, false);
        _ui.SetButtonVisible(_ui.showRewardedButton, false);
        _ui.SetRewardedStatus(string.Empty);
        _ui.SetActionsInteractable(false);
#if UNITY_IOS && !UNITY_EDITOR
        PublishInitializationStatus("Requesting tracking permission");
#endif
        /*
         * Resolve ATT before either SDK initializes. CloudX never prompts and
         * treats an undetermined status as opted out, which suppresses fill on
         * physical devices.
         */
        yield return DemoAppTrackingiOS.EnsureRequested();

        if (!DemoAppTrackingiOS.IsUsable(DemoAppTrackingiOS.Status))
        {
            Log($"Tracking not authorized ({DemoAppTrackingiOS.Status}), leaving the UI disabled");
            _ui.SetInitializationStatus("Status: Tracking not authorized - ads cannot load");
            yield break;
        }

        InitializeAdMob();
        InitializeCloudX();
        StartCoroutine(ReleaseActionsIfInitStalls());
    }

    void OnDestroy()
    {
        CloudXInitializationCallbacks.OnSdkInitializedEvent -= OnCloudXInitialized;
        CloudXInitializationCallbacks.OnSdkInitializationFailedEvent -= OnCloudXInitializationFailed;

        _interstitial?.Dispose();
        _interstitial = null;
    }

    private void InitializeAdMob()
    {
        /* Google callbacks that touch UI are moved onto the Unity main thread. */
        MobileAds.Initialize(_ => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            _adMobStatus = "AdMob: Ready";
            PublishInitializationStatus();
        }));
    }

    private void InitializeCloudX()
    {
        PublishInitializationStatus();

        if (CloudXSdk.IsInitialized())
        {
            OnCloudXInitialized(new CloudXSdkConfiguration());
            return;
        }

        CloudXSdk.SetMinLogLevel(CloudXLogLevel.Verbose);
        CloudXSdk.SetHasUserConsent(true);
        CloudXSdk.SetDoNotSell(false);

        CloudXInitializationCallbacks.OnSdkInitializedEvent += OnCloudXInitialized;
        CloudXInitializationCallbacks.OnSdkInitializationFailedEvent += OnCloudXInitializationFailed;

        CloudXSdk.Initialize(CloudXInitializationConfiguration.Create(DemoConfig.AppKey).Build());
    }

    private void OnCloudXInitialized(CloudXSdkConfiguration _)
    {
        _cloudXInitAnswered = true;

        if (_interstitial != null)
        {
            /*
             * The watchdog already built an AdMob-only controller. Swapping it
             * now could destroy an ad that is on screen, so keep this session.
             */
            Log("CloudX initialized after the watchdog; this session stays AdMob-only");
            _cloudXStatus = "CloudX: Initialized late - AdMob only this session";
            PublishInitializationStatus();
            return;
        }

        Log("CloudX initialized");
        _cloudXStatus = "CloudX: Initialized";
        PublishInitializationStatus();
        CreateController(cloudXAvailable: true);
    }

    private void OnCloudXInitializationFailed(CloudXError error)
    {
        Log($"CloudX initialization failed: {error}");
        _cloudXInitAnswered = true;
        _cloudXStatus = $"CloudX: Failed ({error.ErrorCodeName})";
        PublishInitializationStatus();

        /* AdMob is the only candidate and wins each round locally. */
        CreateController(cloudXAvailable: false);
    }

    private IEnumerator ReleaseActionsIfInitStalls()
    {
        yield return new WaitForSecondsRealtime(InitializationUiTimeoutSeconds);

        if (_cloudXInitAnswered)
        {
            yield break;
        }

        Log($"No CloudX initialization result within {InitializationUiTimeoutSeconds}s, continuing with AdMob only");
        _cloudXStatus = "CloudX: No response";
        PublishInitializationStatus();
        CreateController(cloudXAvailable: false);
    }

    private void CreateController(bool cloudXAvailable)
    {
        if (_interstitial != null)
        {
            return;
        }

        _interstitial = new ArbiterInterstitialController(
            DemoConfig.InterstitialAdUnitId,
            DemoConfig.AdMobInterstitialAdUnitId,
            cloudXAvailable);
        _interstitial.AdLoaded += platform =>
        {
            _interstitialRetries = 0;
            SetInterstitialStatus($"{platform} loaded");
        };
        _interstitial.AdLoadFailed += (platform, message) =>
        {
            if (!_interstitial.NeedsRetry)
            {
                SetInterstitialStatus($"Load failed ({platform}): {message}");
                return;
            }

            var delay = NextRetryDelay(ref _interstitialRetries);
            SetInterstitialStatus($"Load failed ({platform}): {message}\nRetrying in {delay:0}s...");
            Invoke(nameof(LoadInterstitial), delay);
        };
        _interstitial.ArbiterCompleted += (result, bidCount) =>
            SetInterstitialStatus(ArbiterSummary(result, bidCount));
        _interstitial.AdShown += platform => SetInterstitialStatus($"Showing ({platform})");
        _interstitial.AdShowFailed += (platform, message) =>
        {
            var delay = NextRetryDelay(ref _interstitialRetries);
            SetInterstitialStatus($"Show failed ({platform}): {message}\nRetrying in {delay:0}s...");
            Invoke(nameof(LoadInterstitial), delay);
        };
        _interstitial.AdClosed += platform =>
        {
            SetInterstitialStatus($"Closed ({platform})");
            LoadInterstitial();
        };
        _interstitial.AdClicked += platform => Log($"Interstitial clicked ({platform})");
        _interstitial.RevenueReported += (data, accepted) =>
            SetInterstitialStatus(
                $"Revenue -> CloudX: {data.Revenue:0.000000} {data.CurrencyCode} " +
                $"(accepted={accepted})");

        LoadInterstitial();
        _ui.SetActionsInteractable(true);
    }

    private void ShowInterstitial()
    {
        var winner = _interstitial.PreparedWinner;

        if (_interstitial.Show())
        {
            Log($"Showing the interstitial ({winner})");
            return;
        }

        if (_interstitial.IsShowing)
        {
            return;
        }

        /*
         * A real app continues the game when no winner is prepared. The demo
         * reloads as well so another round becomes visible without a restart.
         */
        SetInterstitialStatus("No winner prepared; reloading");
        LoadInterstitial();
    }

    private static float NextRetryDelay(ref int retries)
    {
        var delay = Mathf.Min(RetryBaseDelaySeconds * Mathf.Pow(2f, retries), RetryMaxDelaySeconds);
        retries++;
        return delay;
    }

    private static string Bids(int bidCount) => bidCount == 1 ? "1 bid" : $"{bidCount} bids";

    private static string ArbiterSummary(CloudXArbiterResult result, int bidCount) =>
        result.Platform == CloudXArbiterPlatform.None
            ? $"Arbiter: no winner ({Bids(bidCount)})"
            : $"Arbiter: {result.Platform} ({Bids(bidCount)})";

    private void LoadInterstitial()
    {
        _interstitial?.Load();
    }

    private void PublishInitializationStatus(string overrideText = null)
    {
        _ui.SetInitializationStatus(overrideText ?? $"{_cloudXStatus} | {_adMobStatus}");
    }

    private void SetInterstitialStatus(string text)
    {
        Log($"Interstitial: {text.Replace('\n', ' ')}");
        _ui.SetInterstitialStatus($"Inter: {text}");
    }
}
