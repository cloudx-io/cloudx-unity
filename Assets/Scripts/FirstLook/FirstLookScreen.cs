using System.Collections;
using CloudX;
using CloudX.Demo;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;

/*
 * First Look demo entry point and integration template: CloudX gets the first
 * chance to fill each placement and AdMob is the lazy fallback.
 *
 * Two formats, deliberately: an interstitial and a banner. They are the two
 * shapes the rule has to handle - a fullscreen ad that is consumed by being
 * shown, and an inline ad that stays on screen and therefore needs an explicit
 * pass cycle. Rewarded follows the interstitial exactly and MREC follows the
 * banner exactly, so adding them here would only repeat a pattern; the General
 * screen already shows the SDK calls for all four formats.
 *
 * The flow lives entirely in this folder, and each controller is one
 * self-contained file, so integrating a format means copying two files: that
 * controller and FirstLookSource.cs. AdScreenUi is demo-only layout and is kept
 * out on purpose; this screen hides the two buttons it does not use.
 *
 * This screen is also the reference for the half of the banner contract the
 * controller cannot keep for you: ScheduleNextBannerPass starts the next pass a
 * cooldown after PassSpent, and ToggleBanner cancels it on hide.
 *
 * https://docs.cloudx.io/en/unity/integrations/first-look
 */
[RequireComponent(typeof(AdScreenUi))]
public class FirstLookScreen : MonoBehaviour
{
    private const string TAG = "CloudXUnityDemo";
    private const float InitializationUiTimeoutSeconds = 15f;

    /*
     * Retry policy after a load or show failure: 2s, 4s, 8s ... capped, and
     * reset once a load succeeds. A fixed short delay turns sustained no-fill
     * into a tight request loop against the fallback network, which ad
     * networks penalise.
     */
    private const float RetryBaseDelaySeconds = 2f;
    private const float RetryMaxDelaySeconds = 60f;

    private AdScreenUi _ui;
    private FirstLookInterstitialController _interstitial;
    private FirstLookBannerController _banner;
    private bool _cloudXInitAnswered;
    private int _interstitialRetries;
    private int _bannerRetries;
    private string _cloudXStatus = "CloudX: Initializing";
    private string _adMobStatus = "AdMob: Initializing";

    private static void Log(string message) => Debug.Log($"[{TAG}][FirstLook] {message}");

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
            ShowInterstitial = ShowInterstitial,
            /* This screen covers interstitial and banner only. */
            ToggleMrec = () => { },
            ShowRewarded = () => { },
            /* The banner stays at the top in both orientations, so nothing to reflow. */
            OnOrientationChanged = _ => { },
        });
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
        _banner?.Dispose();
        _banner = null;
    }

    /*
     * Initialization
     */

    private void InitializeAdMob()
    {
        /*
         * No need to wait for this before loading: the fallback is lazy, and
         * Google Mobile Ads queues loads issued before init completes.
         *
         * Google Mobile Ads raises its callbacks off the Unity main thread;
         * anything that touches UI goes through ExecuteInUpdate. The controllers
         * do the same for every ad event they forward.
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
         * First Look keeps working without CloudX: the controllers skip the
         * CloudX leg and serve the AdMob fallback directly.
         */
        CreateControllers(cloudXAvailable: false);
    }

    /*
     * Releases the UI if CloudX Initialize never reports back. Ads may still
     * load through the AdMob fallback path in that case.
     */
    private IEnumerator ReleaseActionsIfInitStalls()
    {
        yield return new WaitForSecondsRealtime(InitializationUiTimeoutSeconds);

        if (_cloudXInitAnswered)
        {
            yield break;
        }

        Log($"No CloudX initialization result within {InitializationUiTimeoutSeconds}s, continuing with the fallback only");
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

        _interstitial = new FirstLookInterstitialController(
            FirstLookConfig.CloudXAdUnitOrInvalid(DemoConfig.InterstitialAdUnitId),
            FirstLookConfig.AdMobInterstitialAdUnitId,
            cloudXAvailable);
        _interstitial.AdLoaded += source =>
        {
            _interstitialRetries = 0;
            SetInterstitialStatus($"Loaded ({source})");
        };
        _interstitial.AdLoadFailed += (source, message) =>
        {
            var delay = NextRetryDelay(ref _interstitialRetries);
            SetInterstitialStatus($"Load failed ({source}): {message}\nRetrying in {delay:0}s...");
            Invoke(nameof(LoadInterstitial), delay);
        };
        _interstitial.AdShown += source => SetInterstitialStatus($"Showing ({source})");
        _interstitial.AdShowFailed += (source, message) =>
        {
            var delay = NextRetryDelay(ref _interstitialRetries);
            SetInterstitialStatus($"Show failed ({source}): {message}\nRetrying in {delay:0}s...");
            Invoke(nameof(LoadInterstitial), delay);
        };
        _interstitial.AdClosed += source =>
        {
            SetInterstitialStatus($"Closed ({source})");
            /* Prepare the next placement opportunity right away. */
            LoadInterstitial();
        };
        _interstitial.AdClicked += source => Log($"Interstitial clicked ({source})");

        _banner = new FirstLookBannerController(
            FirstLookConfig.CloudXAdUnitOrInvalid(DemoConfig.BannerAdUnitId),
            FirstLookConfig.AdMobBannerAdUnitId,
            cloudXAvailable);
        _banner.AdLoaded += source =>
        {
            _bannerRetries = 0;
            Log($"Banner loaded ({source})");
            if (!_banner.IsShown)
            {
                _ui.SetBannerButtonLabel("Show Banner");
            }
        };
        _banner.AdLoadFailed += (source, message) =>
        {
            var delay = NextRetryDelay(ref _bannerRetries);
            Log($"Banner load failed ({source}): {message}; retrying in {delay:0}s");
            Invoke(nameof(LoadBanner), delay);
        };
        _banner.AdShown += source => _ui.SetBannerButtonLabel($"Hide Banner ({source})");
        _banner.PassSpent += ScheduleNextBannerPass;
        _banner.AdClicked += source => Log($"Banner clicked ({source})");

        LoadInterstitial();
        LoadBanner();
        _ui.SetActionsInteractable(true);
    }

    /*
     * Button handlers
     */

    private void ShowInterstitial()
    {
        var source = _interstitial.ReadySource;

        if (_interstitial.Show())
        {
            Log($"Showing the interstitial ({source})");
            return;
        }

        /*
         * Neither source has an ad; in a real app the game flow would simply
         * continue here. The demo reloads and says so.
         */
        SetInterstitialStatus("No ad ready; reloading");
        LoadInterstitial();
    }

    private void ToggleBanner()
    {
        if (_banner.IsShown)
        {
            _banner.Hide();
            /* Nothing on screen, so the pass cycle stops until the next Show. */
            CancelInvoke(nameof(LoadBanner));
            _ui.SetBannerButtonLabel("Show Banner");
            return;
        }

        /* AdShown updates the label once a source actually shows. */
        if (!_banner.Show())
        {
            _ui.SetBannerButtonLabel("Banner: loading...");
            LoadBanner();
        }
    }

    /*
     * Banner only: putting one on screen spends its First Look pass, so the
     * next pass is scheduled a cooldown later. Cancelling first collapses a
     * pending backoff retry into this one - both end up calling LoadBanner, and
     * two pending invokes would arbitrate the placement twice. Showing again
     * after a Hide raises PassSpent too, which restarts the cooldown from that
     * moment.
     */
    private void ScheduleNextBannerPass()
    {
        CancelInvoke(nameof(LoadBanner));
        Invoke(nameof(LoadBanner), FirstLookConfig.PassCooldownSeconds);
    }

    private static float NextRetryDelay(ref int retries)
    {
        var delay = Mathf.Min(RetryBaseDelaySeconds * Mathf.Pow(2f, retries), RetryMaxDelaySeconds);
        retries++;
        return delay;
    }

    /*
     * Named methods so terminal failures can retry via Invoke(nameof(...)).
     */

    private void LoadInterstitial()
    {
        _interstitial?.Load();
    }

    private void LoadBanner()
    {
        _banner?.Load();
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
}
