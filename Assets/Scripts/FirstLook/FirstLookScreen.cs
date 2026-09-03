using System.Collections;
using CloudX;
using CloudX.Demo;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;

/*
 * First Look demo entry point and integration template: CloudX gets the first
 * chance to fill each placement and AdMob is the lazy fallback. The flow lives
 * entirely in this folder (screen + a shared controller base + one controller
 * per format) so it can be copied into a publisher app as-is; AdScreenUi is
 * demo-only layout and is kept out on purpose. Covers all four formats -
 * interstitial, rewarded, banner and MREC. Banner and MREC keep CloudX
 * auto-refresh off (it is opt-out) so a background reload never overrides the
 * First Look source decision; GeneralScreen restarts refresh on focus, this
 * screen deliberately does not.
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
    private FirstLookRewardedController _rewarded;
    private FirstLookBannerController _banner;
    private FirstLookMrecController _mrec;
    private bool _cloudXInitAnswered;
    private int _interstitialRetries;
    private int _rewardedRetries;
    private int _bannerRetries;
    private int _mrecRetries;
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

        /*
         * Log the advertising ID once tracking has been answered. This screen has its
         * own CloudXSdk.Initialize, so it needs the same read the General screen does -
         * the CloudX leg no-fills the same way when the device is not whitelisted.
         * Resolve is idempotent, and the status line here is already the two-part
         * "CloudX | AdMob" summary, so the verdict stays in the log.
         */
        yield return DemoAdvertisingId.Resolve();
        Log($"Advertising ID: {DemoAdvertisingId.Describe()}");

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

        if (_interstitial != null || _rewarded != null)
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
        if (_interstitial != null || _rewarded != null)
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

        _rewarded = new FirstLookRewardedController(
            FirstLookConfig.CloudXAdUnitOrInvalid(DemoConfig.RewardedAdUnitId),
            FirstLookConfig.AdMobRewardedAdUnitId,
            cloudXAvailable);
        _rewarded.AdLoaded += source =>
        {
            _rewardedRetries = 0;
            SetRewardedStatus($"Loaded ({source})");
        };
        _rewarded.AdLoadFailed += (source, message) =>
        {
            var delay = NextRetryDelay(ref _rewardedRetries);
            SetRewardedStatus($"Load failed ({source}): {message}\nRetrying in {delay:0}s...");
            Invoke(nameof(LoadRewarded), delay);
        };
        _rewarded.AdShown += source => SetRewardedStatus($"Showing ({source})");
        _rewarded.AdShowFailed += (source, message) =>
        {
            var delay = NextRetryDelay(ref _rewardedRetries);
            SetRewardedStatus($"Show failed ({source}): {message}\nRetrying in {delay:0}s...");
            Invoke(nameof(LoadRewarded), delay);
        };
        _rewarded.AdClosed += source =>
        {
            SetRewardedStatus($"Closed ({source})");
            LoadRewarded();
        };
        _rewarded.AdClicked += source => Log($"Rewarded clicked ({source})");
        _rewarded.RewardEarned += (source, reward) =>
        {
            Log($"Reward earned ({source}): {reward}");
            SetRewardedStatus($"Reward: {reward} ({source})");
        };

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
        _banner.AdClicked += source => Log($"Banner clicked ({source})");

        _mrec = new FirstLookMrecController(
            FirstLookConfig.CloudXAdUnitOrInvalid(DemoConfig.MrecAdUnitId),
            FirstLookConfig.AdMobMrecAdUnitId,
            cloudXAvailable);
        _mrec.AdLoaded += source =>
        {
            _mrecRetries = 0;
            Log($"MREC loaded ({source})");
            if (!_mrec.IsShown)
            {
                _ui.SetMrecButtonLabel("Show MREC");
            }
        };
        _mrec.AdLoadFailed += (source, message) =>
        {
            var delay = NextRetryDelay(ref _mrecRetries);
            Log($"MREC load failed ({source}): {message}; retrying in {delay:0}s");
            Invoke(nameof(LoadMrec), delay);
        };
        _mrec.AdShown += source => _ui.SetMrecButtonLabel($"Hide MREC ({source})");
        _mrec.AdClicked += source => Log($"MREC clicked ({source})");

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

    private void ShowRewarded()
    {
        var source = _rewarded.ReadySource;

        if (_rewarded.Show())
        {
            Log($"Showing the rewarded ad ({source})");
            return;
        }

        SetRewardedStatus("No ad ready; reloading");
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

        /* AdShown updates the label once a source actually shows. */
        if (!_banner.Show())
        {
            _ui.SetBannerButtonLabel("Banner: loading...");
            LoadBanner();
        }
    }

    private void ToggleMrec()
    {
        if (_mrec.IsShown)
        {
            _mrec.Hide();
            _ui.SetMrecButtonLabel("Show MREC");
            return;
        }

        if (!_mrec.Show())
        {
            _ui.SetMrecButtonLabel("MREC: loading...");
            LoadMrec();
        }
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
