using System.Collections;
using CloudX;
using CloudX.Demo;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;

/*
 * Arbiter/TPA demo entry point and integration template: CloudX and AdMob load
 * in parallel for every placement and Trusted Arbiter (CloudXSdk.Arbiter) picks
 * which one is shown. Interstitial and rewarded prepare the winner ahead of the
 * placement; banner and MREC arbitrate and then render on a refresh cycle with
 * auto-refresh off. The flow lives entirely in this folder (screen + a shared
 * controller base + one controller per format) so it can be copied into a
 * publisher app as-is; AdScreenUi is demo-only layout and is kept out on purpose.
 *
 * The status lines make the arbitration visible: which sides loaded, what the
 * arbiter returned and over how many bids, and which platform is showing.
 */
[RequireComponent(typeof(AdScreenUi))]
public class ArbiterScreen : MonoBehaviour
{
    private const string TAG = "CloudXUnityDemo";
    private const float InitializationUiTimeoutSeconds = 15f;

    /*
     * Retry policy after a load or show failure while no winner is prepared or
     * shown: 2s, 4s, 8s ... capped, and reset once a load succeeds. Once a
     * winner exists, the controllers re-request the missing network themselves
     * on the next cycle, so a sustained no-fill never turns into a tight loop.
     */
    private const float RetryBaseDelaySeconds = 2f;
    private const float RetryMaxDelaySeconds = 60f;

    private AdScreenUi _ui;
    private ArbiterInterstitialController _interstitial;
    private ArbiterRewardedController _rewarded;
    private ArbiterBannerController _banner;
    private ArbiterMrecController _mrec;
    private bool _cloudXInitAnswered;
    private int _interstitialRetries;
    private int _rewardedRetries;
    private int _bannerRetries;
    private int _mrecRetries;
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
            ShowBanner = ToggleBanner,
            ToggleMrec = ToggleMrec,
            ShowInterstitial = ShowInterstitial,
            ShowRewarded = ShowRewarded,
            /* The banner stays at the top in both orientations, so nothing to reflow. */
            OnOrientationChanged = _ => { },
        });
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

    /* The inline controllers' refresh clock. */
    void Update()
    {
        _banner?.Tick(Time.unscaledDeltaTime);
        _mrec?.Tick(Time.unscaledDeltaTime);
    }

    void OnDestroy()
    {
        CloudXInitializationCallbacks.OnSdkInitializedEvent -= OnCloudXInitialized;
        CloudXInitializationCallbacks.OnSdkInitializationFailedEvent -= OnCloudXInitializationFailed;

        _interstitial?.Dispose();
        _interstitial = null;
        _rewarded?.Dispose();
        _rewarded = null;
        _banner?.Dispose();
        _banner = null;
        _mrec?.Dispose();
        _mrec = null;
    }

    /*
     * Initialization
     */

    private void InitializeAdMob()
    {
        /*
         * Google Mobile Ads queues loads issued before init completes, so the
         * parallel loads can start as soon as CloudX is ready. Its callbacks
         * arrive off the Unity main thread; anything that touches UI goes
         * through ExecuteInUpdate, and the controllers do the same.
         */
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
            /* Already initialized by another screen in this session. */
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

    /*
     * The arbiter is called only after initialization completed (docs). The
     * controllers are created here, so their first Arbiter call cannot precede
     * OnSdkInitialized.
     */
    private void OnCloudXInitialized(CloudXSdkConfiguration _)
    {
        _cloudXInitAnswered = true;

        if (_interstitial != null)
        {
            /*
             * The watchdog already gave up on CloudX and built AdMob-only
             * controllers. Swapping them now would destroy an AdMob ad that
             * may be on screen, so this session stays as it is and says so.
             */
            Log("CloudX initialized after the watchdog; this session stays AdMob-only");
            _cloudXStatus = "CloudX: Initialized late - AdMob only this session";
            PublishInitializationStatus();
            return;
        }

        Log("CloudX initialized");
        _cloudXStatus = "CloudX: Initialized";
        PublishInitializationStatus();
        CreateControllers(cloudXAvailable: true);
    }

    private void OnCloudXInitializationFailed(CloudXError error)
    {
        Log($"CloudX initialization failed: {error}");
        _cloudXInitAnswered = true;
        _cloudXStatus = $"CloudX: Failed ({error.ErrorCodeName})";
        PublishInitializationStatus();

        /*
         * The demo keeps working without CloudX: the controllers skip the CloudX
         * leg, AdMob is the only candidate and wins every round locally.
         */
        CreateControllers(cloudXAvailable: false);
    }

    /*
     * Releases the UI if CloudX Initialize never reports back. Ads may still
     * load through AdMob alone in that case.
     */
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
        CreateControllers(cloudXAvailable: false);
    }

    private void CreateControllers(bool cloudXAvailable)
    {
        if (_interstitial != null)
        {
            return;
        }

        _interstitial = new ArbiterInterstitialController(
            ArbiterConfig.CloudXAdUnitOrInvalid(DemoConfig.InterstitialAdUnitId),
            DemoConfig.AdMobInterstitialAdUnitId,
            cloudXAvailable);
        _interstitial.AdLoaded += platform =>
        {
            _interstitialRetries = 0;
            SetInterstitialStatus($"{platform} loaded");
        };
        _interstitial.AdLoadFailed += (platform, message) =>
        {
            /* With a candidate, a load or a round pending, the controller gets there itself. */
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
            /* Start the next cycle right away: reload what is missing, re-arbitrate. */
            LoadInterstitial();
        };
        _interstitial.AdClicked += platform => Log($"Interstitial clicked ({platform})");

        _rewarded = new ArbiterRewardedController(
            ArbiterConfig.CloudXAdUnitOrInvalid(DemoConfig.RewardedAdUnitId),
            DemoConfig.AdMobRewardedAdUnitId,
            cloudXAvailable);
        _rewarded.AdLoaded += platform =>
        {
            _rewardedRetries = 0;
            SetRewardedStatus($"{platform} loaded");
        };
        _rewarded.AdLoadFailed += (platform, message) =>
        {
            if (!_rewarded.NeedsRetry)
            {
                SetRewardedStatus($"Load failed ({platform}): {message}");
                return;
            }

            var delay = NextRetryDelay(ref _rewardedRetries);
            SetRewardedStatus($"Load failed ({platform}): {message}\nRetrying in {delay:0}s...");
            Invoke(nameof(LoadRewarded), delay);
        };
        _rewarded.ArbiterCompleted += (result, bidCount) =>
            SetRewardedStatus(ArbiterSummary(result, bidCount));
        _rewarded.AdShown += platform => SetRewardedStatus($"Showing ({platform})");
        _rewarded.AdShowFailed += (platform, message) =>
        {
            var delay = NextRetryDelay(ref _rewardedRetries);
            SetRewardedStatus($"Show failed ({platform}): {message}\nRetrying in {delay:0}s...");
            Invoke(nameof(LoadRewarded), delay);
        };
        _rewarded.AdClosed += platform =>
        {
            SetRewardedStatus($"Closed ({platform})");
            LoadRewarded();
        };
        _rewarded.AdClicked += platform => Log($"Rewarded clicked ({platform})");
        _rewarded.RewardEarned += (platform, reward) =>
        {
            Log($"Reward earned ({platform}): {reward}");
            SetRewardedStatus($"Reward: {reward} ({platform})");
        };

        _banner = new ArbiterBannerController(
            ArbiterConfig.CloudXAdUnitOrInvalid(DemoConfig.BannerAdUnitId),
            DemoConfig.AdMobBannerAdUnitId,
            cloudXAvailable,
            ArbiterConfig.InlineRefreshIntervalSeconds);
        _banner.AdLoaded += platform =>
        {
            _bannerRetries = 0;
            Log($"Banner: {platform} loaded");
        };
        _banner.AdLoadFailed += (platform, message) =>
        {
            /* With a candidate, a load, a round or a view pending, the cycle re-requests fills itself. */
            if (!_banner.NeedsRetry)
            {
                Log($"Banner: load failed ({platform}): {message}");
                return;
            }

            var delay = NextRetryDelay(ref _bannerRetries);
            Log($"Banner: load failed ({platform}): {message}; retrying in {delay:0}s");
            Invoke(nameof(LoadBanner), delay);
        };
        _banner.ArbiterCompleted += (result, bidCount) => Log($"Banner: {ArbiterSummary(result, bidCount)}");
        _banner.WinnerShown += platform => _ui.SetBannerButtonLabel(
            platform == CloudXArbiterPlatform.None ? "Banner: no winner" : $"Hide Banner ({platform})");
        _banner.AdClicked += platform => Log($"Banner clicked ({platform})");

        _mrec = new ArbiterMrecController(
            ArbiterConfig.CloudXAdUnitOrInvalid(DemoConfig.MrecAdUnitId),
            DemoConfig.AdMobMrecAdUnitId,
            cloudXAvailable,
            ArbiterConfig.InlineRefreshIntervalSeconds);
        _mrec.AdLoaded += platform =>
        {
            _mrecRetries = 0;
            Log($"MREC: {platform} loaded");
        };
        _mrec.AdLoadFailed += (platform, message) =>
        {
            if (!_mrec.NeedsRetry)
            {
                Log($"MREC: load failed ({platform}): {message}");
                return;
            }

            var delay = NextRetryDelay(ref _mrecRetries);
            Log($"MREC: load failed ({platform}): {message}; retrying in {delay:0}s");
            Invoke(nameof(LoadMrec), delay);
        };
        _mrec.ArbiterCompleted += (result, bidCount) => Log($"MREC: {ArbiterSummary(result, bidCount)}");
        _mrec.WinnerShown += platform => _ui.SetMrecButtonLabel(
            platform == CloudXArbiterPlatform.None ? "MREC: no winner" : $"Hide MREC ({platform})");
        _mrec.AdClicked += platform => Log($"MREC clicked ({platform})");

        /* Parallel loads for every format; each arbitrates once its candidates settle. */
        LoadInterstitial();
        LoadRewarded();
        LoadBanner();
        LoadMrec();
        _ui.SetActionsInteractable(true);
    }

    /*
     * Button handlers
     */

    private void ShowInterstitial()
    {
        var winner = _interstitial.PreparedWinner;

        if (_interstitial.Show())
        {
            Log($"Showing the interstitial ({winner})");
            return;
        }

        /*
         * No winner is prepared (or its ad is gone); in a real app the game flow
         * would simply continue here. The demo reloads and says so.
         */
        SetInterstitialStatus("No winner prepared; reloading");
        LoadInterstitial();
    }

    private void ShowRewarded()
    {
        var winner = _rewarded.PreparedWinner;

        if (_rewarded.Show())
        {
            Log($"Showing the rewarded ad ({winner})");
            return;
        }

        SetRewardedStatus("No winner prepared; reloading");
        LoadRewarded();
    }

    private void ToggleBanner()
    {
        if (_banner.IsShown)
        {
            _banner.Hide();
            _ui.SetBannerButtonLabel("Show Banner");
            return;
        }

        /* WinnerShown updates the label once the round has rendered. */
        _ui.SetBannerButtonLabel("Banner: arbitrating...");
        _banner.Show();
    }

    private void ToggleMrec()
    {
        if (_mrec.IsShown)
        {
            _mrec.Hide();
            _ui.SetMrecButtonLabel("Show MREC");
            return;
        }

        _ui.SetMrecButtonLabel("MREC: arbitrating...");
        _mrec.Show();
    }

    private static float NextRetryDelay(ref int retries)
    {
        var delay = Mathf.Min(RetryBaseDelaySeconds * Mathf.Pow(2f, retries), RetryMaxDelaySeconds);
        retries++;
        return delay;
    }

    private static string ArbiterSummary(CloudXArbiterResult result, int bidCount)
    {
        var bids = bidCount == 1 ? "1 bid" : $"{bidCount} bids";
        return result.Platform == CloudXArbiterPlatform.None
            ? $"Arbiter: no winner ({bids})"
            : $"Arbiter: {result.Platform} ({bids})";
    }

    /*
     * Named methods so terminal failures can retry via Invoke(nameof(...)).
     */

    private void LoadInterstitial()
    {
        _interstitial?.Load();
    }

    private void LoadRewarded()
    {
        _rewarded?.Load();
    }

    private void LoadBanner()
    {
        _banner?.Load();
    }

    private void LoadMrec()
    {
        _mrec?.Load();
    }

    /*
     * Status plumbing
     */

    private void PublishInitializationStatus(string overrideText = null)
    {
        _ui.SetInitializationStatus(overrideText ?? $"{_cloudXStatus} | {_adMobStatus}");
    }

    private void SetInterstitialStatus(string text)
    {
        Log($"Interstitial: {text.Replace('\n', ' ')}");
        _ui.SetInterstitialStatus($"Inter: {text}");
    }

    private void SetRewardedStatus(string text)
    {
        Log($"Rewarded: {text.Replace('\n', ' ')}");
        _ui.SetRewardedStatus($"Rewarded: {text}");
    }
}
