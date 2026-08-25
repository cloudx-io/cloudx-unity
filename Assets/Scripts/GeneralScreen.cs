using System.Collections;
using CloudX;
using CloudX.Demo;
using UnityEngine;

/*
 * CloudX Unity integration sample.
 *
 * Replace DemoConfig IDs with values from your CloudX dashboard.
 * This class is the SDK call sequence. AdScreenUi is demo-only layout and is
 * not part of the integration.
 *
 * Flow: request iOS ATT -> optional privacy / user data -> subscribe to init
 * callbacks -> CloudXSdk.Initialize -> create or load ads only after
 * OnSdkInitialized.
 */
[RequireComponent(typeof(AdScreenUi))]
public class GeneralScreen : MonoBehaviour
{
    private const string TAG = "CloudXUnityDemo";

    /* Demo-only retry delay after a fullscreen load or show failure. */
    private const float FullscreenRetryDelaySeconds = 2f;
    /*
     * Backstop for re-enabling the UI. Both init callbacks originate in native
     * code, so neither is guaranteed to arrive; without this a hung Initialize
     * would leave the demo permanently untappable.
     */
    private const float InitializationUiTimeoutSeconds = 15f;

    private static readonly string AppKey = DemoConfig.AppKey;
    private static readonly string BannerAdUnitId = DemoConfig.BannerAdUnitId;
    private static readonly string MrecAdUnitId = DemoConfig.MrecAdUnitId;
    private static readonly string InterstitialAdUnitId = DemoConfig.InterstitialAdUnitId;
    private static readonly string RewardedAdUnitId = DemoConfig.RewardedAdUnitId;

    private static void Log(string message) => Debug.Log($"[{TAG}] {message}");

    private AdScreenUi _ui;
    private bool _initAnswered;
    private bool isBannerShown;
    private bool _bannerCreated;
    private bool _bannerIsVertical;
    private CloudXAdViewConfiguration.AdViewPosition _horizontalBannerPosition =
        CloudXAdViewConfiguration.AdViewPosition.TopCenter;
    private CloudXAdViewConfiguration.AdViewVerticalPosition _verticalBannerPosition =
        CloudXAdViewConfiguration.AdViewVerticalPosition.Left;
    private bool _mrecCreated;
    private bool isMrecShowing;

    void Awake()
    {
        _ui = GetComponent<AdScreenUi>();
    }

    IEnumerator Start()
    {
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        /*
         * Bind first so the layout is built behind the modal ATT alert, but
         * leave the ad actions inert until the SDK answers. A load issued
         * before ATT resolves goes out as do-not-track and never fills, even
         * after the user grants permission.
         */
        _ui.Bind(CreateUiActions());
        _ui.SetActionsInteractable(false);
#if UNITY_IOS && !UNITY_EDITOR
        /* Only iOS ever presents a tracking prompt; elsewhere the gate is a no-op. */
        _ui.SetInitializationStatus("Status: Requesting tracking permission");
#endif
        /*
         * Resolve ATT before Initialize. The SDK never prompts and treats an
         * undetermined status as opted out (no IDFA, dnt = 1), which suppresses
         * fill on physical devices.
         */
        yield return DemoAppTrackingiOS.EnsureRequested();

        /*
         * Read the advertising ID once tracking has been answered, and log it in full.
         * This is the value that has to be on the dashboard's test-device list for the
         * device to serve test ads, and it is worth logging even when tracking was
         * declined: a zeroed ID is the specific symptom that looks like a wrong
         * dashboard entry rather than a consent problem.
         */
        yield return DemoAdvertisingId.Resolve();
        Log($"Advertising ID: {DemoAdvertisingId.Describe()}");

        if (!DemoAppTrackingiOS.IsUsable(DemoAppTrackingiOS.Status))
        {
            /*
             * The user declined tracking, so every auction request goes out
             * opted out and no ad ever fills. Leave the actions inert and say
             * why, instead of offering loads that silently return no fill.
             * iOS only re-prompts after a reinstall.
             */
            Log($"Tracking not authorized ({DemoAppTrackingiOS.Status}), leaving the UI disabled");
            _ui.SetInitializationStatus(TrackingBlockedStatus(DemoAppTrackingiOS.Status));
            yield break;
        }

        /* Initialize early, before any Create* / Load* call. */
        InitializeCloudX();
        StartCoroutine(ReleaseActionsIfInitStalls());
    }

    /*
     * Releases the UI if Initialize never reports back. Ads still will not load
     * in that case, but a stuck "Status: Initializing" with nothing tappable is
     * worse than a tester being able to poke the not-ready paths.
     */
    private IEnumerator ReleaseActionsIfInitStalls()
    {
        yield return new WaitForSecondsRealtime(InitializationUiTimeoutSeconds);

        if (_initAnswered)
        {
            yield break;
        }

        Log($"No initialization result within {InitializationUiTimeoutSeconds}s, re-enabling the UI");
        _ui.SetActionsInteractable(true);
    }

    /*
     * iOS only presents the tracking prompt once per install, so a refusal
     * needs a reinstall to undo. An unanswered prompt is asked again on the
     * next launch, and Restricted is set by device policy the user cannot
     * change from here.
     */
    private static string TrackingBlockedStatus(DemoAppTrackingiOS.AttStatus status)
    {
        switch (status)
        {
            case DemoAppTrackingiOS.AttStatus.Denied:
                return "Status: Tracking denied - reinstall to enable ads";
            case DemoAppTrackingiOS.AttStatus.Restricted:
                return "Status: Tracking restricted - ads cannot load";
            default:
                return "Status: Tracking unanswered - relaunch the app";
        }
    }

    private AdScreenUi.Actions CreateUiActions()
    {
        return new AdScreenUi.Actions
        {
            ShowBanner = CycleBanner,
            ToggleMrec = ToggleMrecVisibility,
            ShowInterstitial = ShowInterstitial,
            ShowRewarded = ShowRewardedAd,
            OnOrientationChanged = OnBannerOrientationChanged,
        };
    }

    /*
     * Required setup. Call once early (Start or Awake), before any ad load.
     * Optional user / privacy setters go before Initialize. Subscribe to the
     * init callbacks before Initialize so you do not miss the result.
     */
    private void InitializeCloudX()
    {
        if (_ui == null)
            _ui = GetComponent<AdScreenUi>();

        _ui.SetInitializationStatus("Status: Initializing");

        /* Verbose logging is for development. Turn it down in production. */
        CloudXSdk.SetMinLogLevel(CloudXLogLevel.Verbose);

        CloudXSdk.SetHashedUserId("test-hashed-user-id-12345");
        CloudXSdk.SetUserKeyValue("user_level", "premium");
        CloudXSdk.SetAppKeyValue("app_version", "demo-1337");
        /*
         * Manual GDPR / CCPA overrides when you are not using a CMP. The demo
         * passes literals so it always serves; do not copy that. Pass what the
         * user actually chose in your consent flow, because asserting consent
         * the user never gave is a compliance problem, not a default.
         */
        CloudXSdk.SetHasUserConsent(true);
        CloudXSdk.SetDoNotSell(false);

        /*
         * Callback threading. The demo keeps the default, so this stays commented out.
         * Set it before Initialize so it also covers the initialization callbacks.
         *
         * Left unset (default): every callback runs on the Unity main thread, except
         * OnAdRevenuePaid for interstitial, app open and rewarded ads. Those arrive on a
         * background thread, because the game is paused or covered while a fullscreen ad
         * is showing and a main-thread callback may only run after the ad closes.
         * Banner and MREC revenue runs on the main thread like everything else.
         *
         * true: every callback, revenue included, runs on the Unity main thread. Fullscreen
         * revenue may then reach you after the ad closes rather than at impression time.
         *
         * false: every callback runs on a background thread. Fastest delivery, but your
         * handlers must not touch Unity APIs and must marshal to the main thread themselves.
         */
        // CloudXSdk.InvokeEventsOnUnityMainThread = true;

        CloudXInitializationCallbacks.OnSdkInitializedEvent += OnSdkInitialized;
        CloudXInitializationCallbacks.OnSdkInitializationFailedEvent += OnSdkInitializationFailed;

        var config = CloudXInitializationConfiguration.Create(AppKey).Build();
        CloudXSdk.Initialize(config);
    }

    /* Create or load ads only after this callback. */
    private void OnSdkInitialized(CloudXSdkConfiguration config)
    {
        Log("CloudX SDK initialized successfully");
        _initAnswered = true;
        /*
         * Carry the advertising-ID verdict on the status line. A zeroed ID does not stop
         * initialization -- it stops the device from ever matching the dashboard's
         * test-device list -- so this is the only place the demo can flag it before the
         * tester concludes their ad units are wrong.
         */
        var adId = DemoAdvertisingId.ShortStatus();
        _ui.SetInitializationStatus(
            string.IsNullOrEmpty(adId) ? "Status: Initialized" : $"Status: Initialized - {adId}");
        _ui.SetActionsInteractable(true);

        InitializeBannerAds();
        InitializeMrecAds();
        InitializeInterstitialAds();
        InitializeRewardedAds();
    }

    /* Init failed. Do not create or load ads until Initialize succeeds. */
    private void OnSdkInitializationFailed(CloudXError error)
    {
        Log($"CloudX initialization failed: {error}");
        _initAnswered = true;
        _ui.SetInitializationStatus($"Status: Failed - {error.ErrorCodeName}");
        /*
         * Re-enabled on purpose. Init failed, so no ad will load, but a tester
         * still needs the buttons to exercise the not-ready paths. The status
         * text above is what explains the state.
         */
        _ui.SetActionsInteractable(true);
    }

    #region Banner Ad Methods

    /*
     * Subscribe first, then create the banner. Portrait uses a horizontal
     * banner (top / bottom). Landscape uses a vertical banner (left / right).
     */
    private void InitializeBannerAds()
    {
        CloudXAdsCallbacks.Banner.OnAdLoadSuccess += OnBannerAdLoadedEvent;
        CloudXAdsCallbacks.Banner.OnAdLoadFailed += OnBannerAdLoadFailedEvent;
        CloudXAdsCallbacks.Banner.OnAdClicked += OnBannerAdClickedEvent;
        CloudXAdsCallbacks.Banner.OnAdRevenuePaid += OnBannerAdRevenuePaidEvent;

        _bannerIsVertical = Screen.width > Screen.height;
        ApplyBannerRequestParameters();
        CreateBannerForCurrentKind();
        _bannerCreated = true;
        UpdateBannerButtonLabel();
    }

    /*
     * Placement and custom data must be set before CreateBanner so they are
     * on the first request. Set them again after DestroyBanner, before the
     * next CreateBanner.
     */
    private void ApplyBannerRequestParameters()
    {
        CloudXSdk.SetBannerPlacement(BannerAdUnitId, "home_screen");
        CloudXSdk.SetBannerCustomData(BannerAdUnitId, "home_banner_data");
    }

    /*
     * First tap shows the banner for the current orientation. Later taps move it
     * to the opposite edge (top <-> bottom, or left <-> right) by Destroy + Create.
     */
    private void CycleBanner()
    {
        if (!_bannerCreated)
        {
            /*
             * CreateBanner runs in InitializeBannerAds, off OnSdkInitialized.
             * Showing before that would flip isBannerShown on a banner that
             * does not exist, and OnApplicationFocus would then re-show the
             * missing banner on every focus change, permanently. Reachable
             * because ReleaseActionsIfInitStalls re-enables this button when
             * Initialize never answers.
             */
            Log("Banner not created yet, ignoring show");
            return;
        }

        if (isBannerShown)
        {
            FlipCurrentBannerEdge();
            RecreateBanner();
        }

        isBannerShown = true;
        CloudXSdk.ShowBanner(BannerAdUnitId);
        UpdateBannerButtonLabel();
    }

    /*
     * Horizontal and vertical banners use different CloudXAdViewConfiguration
     * constructors. Rotate: DestroyBanner, then CreateBanner with the other
     * position type.
     */
    private void OnBannerOrientationChanged(bool landscape)
    {
        if (_bannerIsVertical == landscape)
            return;

        _bannerIsVertical = landscape;
        if (_bannerCreated)
        {
            RecreateBanner();
            if (isBannerShown)
                CloudXSdk.ShowBanner(BannerAdUnitId);
        }

        UpdateBannerButtonLabel();
    }

    /* Destroy the current banner, re-apply placement / custom data, then create the new one. */
    private void RecreateBanner()
    {
        CloudXSdk.DestroyBanner(BannerAdUnitId);
        ApplyBannerRequestParameters();
        CreateBannerForCurrentKind();
    }

    /*
     * AdViewPosition = horizontal banner. AdViewVerticalPosition = vertical banner
     * pinned to the left or right edge.
     */
    private void CreateBannerForCurrentKind()
    {
        var configuration = _bannerIsVertical
            ? new CloudXAdViewConfiguration(_verticalBannerPosition)
            : new CloudXAdViewConfiguration(_horizontalBannerPosition);
        CloudXSdk.CreateBanner(BannerAdUnitId, configuration);
    }

    /* Demo: next CreateBanner uses the opposite edge of the current kind. */
    private void FlipCurrentBannerEdge()
    {
        if (_bannerIsVertical)
        {
            _verticalBannerPosition = OppositeOf(_verticalBannerPosition);
            return;
        }

        _horizontalBannerPosition = OppositeOf(_horizontalBannerPosition);
    }

    private void UpdateBannerButtonLabel()
    {
        if (!isBannerShown)
        {
            _ui.SetBannerButtonLabel(_bannerIsVertical ? "Show Left Banner" : "Show Top Banner");
            return;
        }

        _ui.SetBannerButtonLabel($"Show {CurrentOppositeEdgeName()} Banner");
    }

    private string CurrentOppositeEdgeName()
    {
        if (_bannerIsVertical)
            return OppositeOf(_verticalBannerPosition).ToString();

        return OppositeOf(_horizontalBannerPosition) == CloudXAdViewConfiguration.AdViewPosition.TopCenter
            ? "Top"
            : "Bottom";
    }

    private static CloudXAdViewConfiguration.AdViewPosition OppositeOf(
        CloudXAdViewConfiguration.AdViewPosition position)
    {
        return position == CloudXAdViewConfiguration.AdViewPosition.TopCenter
            ? CloudXAdViewConfiguration.AdViewPosition.BottomCenter
            : CloudXAdViewConfiguration.AdViewPosition.TopCenter;
    }

    private static CloudXAdViewConfiguration.AdViewVerticalPosition OppositeOf(
        CloudXAdViewConfiguration.AdViewVerticalPosition position)
    {
        return position == CloudXAdViewConfiguration.AdViewVerticalPosition.Left
            ? CloudXAdViewConfiguration.AdViewVerticalPosition.Right
            : CloudXAdViewConfiguration.AdViewVerticalPosition.Left;
    }

    private void OnBannerAdLoadedEvent(CloudXAd cloudXAd)
    {
        Log($"Banner loaded: {cloudXAd}");
    }

    private void OnBannerAdLoadFailedEvent(string adUnitId, CloudXError cloudXError)
    {
        Log($"Banner failed to load: {adUnitId} - {cloudXError}");
    }

    private void OnBannerAdClickedEvent(CloudXAd cloudXAd)
    {
        Log($"Banner clicked: {cloudXAd}");
    }

    /* Impression revenue for this banner. Forward to your analytics if needed. */
    private void OnBannerAdRevenuePaidEvent(CloudXAd cloudXAd)
    {
        Log($"Banner revenue paid - {cloudXAd}");
    }

    #endregion

    #region MREC Ad Methods

    /*
     * MREC is a 300x250 banner-like view. Same callback and placement pattern
     * as banner. MREC is horizontal only — do not pass a vertical position.
     */
    private void InitializeMrecAds()
    {
        CloudXAdsCallbacks.Mrec.OnAdLoadSuccess += OnMrecAdLoadedEvent;
        CloudXAdsCallbacks.Mrec.OnAdLoadFailed += OnMrecAdLoadFailedEvent;
        CloudXAdsCallbacks.Mrec.OnAdClicked += OnMrecAdClickedEvent;
        CloudXAdsCallbacks.Mrec.OnAdRevenuePaid += OnMrecAdRevenuePaidEvent;

        var configuration = new CloudXAdViewConfiguration(CloudXAdViewConfiguration.AdViewPosition.BottomCenter);
        CloudXSdk.SetMRecPlacement(MrecAdUnitId, "home_screen");
        CloudXSdk.SetMRecCustomData(MrecAdUnitId, "home_mrec_data");
        CloudXSdk.CreateMrec(MrecAdUnitId, configuration);
        _mrecCreated = true;
    }

    /* Load + Show to display. Hide to take it off screen without destroying it. */
    private void ToggleMrecVisibility()
    {
        if (!_mrecCreated)
        {
            /* Same as CycleBanner: no MREC exists until OnSdkInitialized. */
            Log("MREC not created yet, ignoring toggle");
            return;
        }

        if (!isMrecShowing)
        {
            CloudXSdk.LoadMrec(MrecAdUnitId);
            CloudXSdk.ShowMrec(MrecAdUnitId);
            _ui.SetMrecButtonLabel("Hide MREC");
        }
        else
        {
            CloudXSdk.HideMrec(MrecAdUnitId);
            _ui.SetMrecButtonLabel("Show MREC");
        }

        isMrecShowing = !isMrecShowing;
    }

    private void OnMrecAdLoadedEvent(CloudXAd cloudXAd)
    {
        Log($"MREC loaded: {cloudXAd}");
    }

    private void OnMrecAdLoadFailedEvent(string adUnitId, CloudXError cloudXError)
    {
        Log($"MREC failed to load: {adUnitId} - {cloudXError}");
    }

    private void OnMrecAdClickedEvent(CloudXAd cloudXAd)
    {
        Log($"MREC clicked: {cloudXAd}");
    }

    /* Impression revenue for this MREC. Forward to your analytics if needed. */
    private void OnMrecAdRevenuePaidEvent(CloudXAd cloudXAd)
    {
        Log($"MREC revenue paid - {cloudXAd}");
    }

    #endregion

    #region Interstitial Ad Methods

    /* Subscribe, then Load. Show only after OnAdLoadSuccess / IsInterstitialReady. */
    private void InitializeInterstitialAds()
    {
        CloudXAdsCallbacks.Interstitial.OnAdLoadSuccess += OnInterstitialLoadedEvent;
        CloudXAdsCallbacks.Interstitial.OnAdLoadFailed += OnInterstitialFailedEvent;
        CloudXAdsCallbacks.Interstitial.OnAdShowSuccess += OnInterstitialDisplayedEvent;
        CloudXAdsCallbacks.Interstitial.OnAdShowFailed += InterstitialFailedToDisplayEvent;
        CloudXAdsCallbacks.Interstitial.OnAdClicked += OnInterstitialClickedEvent;
        CloudXAdsCallbacks.Interstitial.OnAdHidden += OnInterstitialDismissedEvent;
        CloudXAdsCallbacks.Interstitial.OnAdRevenuePaid += OnInterstitialRevenuePaidEvent;

        LoadInterstitial();
    }

    /* Request a fullscreen interstitial. */
    void LoadInterstitial()
    {
        _ui.SetInterstitialStatus("Loading...");
        CloudXSdk.LoadInterstitial(InterstitialAdUnitId);
    }

    /* Check IsInterstitialReady before Show. If it is not ready, load again. */
    void ShowInterstitial()
    {
        if (CloudXSdk.IsInterstitialReady(InterstitialAdUnitId))
        {
            CloudXSdk.ShowInterstitial(InterstitialAdUnitId, placement: "home_inter", customData: "home_inter_data");
        }
        else
        {
            _ui.SetInterstitialStatus("Ad not ready; reloading");
            LoadInterstitial();
        }
    }

    private void OnInterstitialLoadedEvent(CloudXAd cloudXAd)
    {
        _ui.SetInterstitialStatus($"Loaded {cloudXAd.AdUnitId}");
        Log($"Interstitial loaded: {cloudXAd}");
    }

    /* Demo retries after a short delay. Use your own backoff in production. */
    private void OnInterstitialFailedEvent(string adUnitId, CloudXError cloudXError)
    {
        _ui.SetInterstitialStatus($"Load failed: {cloudXError.Message}\nRetrying in {FullscreenRetryDelaySeconds}s...");
        Log($"Interstitial failed to load: {adUnitId} - {cloudXError}");

        Invoke("LoadInterstitial", FullscreenRetryDelaySeconds);
    }

    private void OnInterstitialDisplayedEvent(CloudXAd cloudXAd)
    {
        Log($"Interstitial shown: {cloudXAd}");
    }

    /* Show failed. Reload so a later Show can succeed. */
    private void InterstitialFailedToDisplayEvent(CloudXAd cloudXAd, CloudXError cloudXError)
    {
        _ui.SetInterstitialStatus($"Show failed: {cloudXError.Message}\nRetrying in {FullscreenRetryDelaySeconds}s...");
        Log($"Interstitial failed to display - {cloudXAd} - Error: {cloudXError}");
        Invoke("LoadInterstitial", FullscreenRetryDelaySeconds);
    }

    private void OnInterstitialClickedEvent(CloudXAd cloudXAd)
    {
        Log($"Interstitial clicked: {cloudXAd}");
    }

    /* Reload after dismiss so the next Show has an ad ready. */
    private void OnInterstitialDismissedEvent(CloudXAd cloudXAd)
    {
        Log($"Interstitial dismissed: {cloudXAd}");
        LoadInterstitial();
    }

    /* Impression revenue for this interstitial. Forward to your analytics if needed. */
    private void OnInterstitialRevenuePaidEvent(CloudXAd cloudXAd)
    {
        Log($"Interstitial revenue paid - {cloudXAd}");
    }

    #endregion

    #region Rewarded Ad Methods

    /* Same load / ready / show pattern as interstitial, plus OnAdRewarded. */
    private void InitializeRewardedAds()
    {
        CloudXAdsCallbacks.Rewarded.OnAdLoadSuccess += OnRewardedAdLoadedEvent;
        CloudXAdsCallbacks.Rewarded.OnAdLoadFailed += OnRewardedAdFailedEvent;
        CloudXAdsCallbacks.Rewarded.OnAdShowSuccess += OnRewardedAdDisplayedEvent;
        CloudXAdsCallbacks.Rewarded.OnAdShowFailed += OnRewardedAdFailedToDisplayEvent;
        CloudXAdsCallbacks.Rewarded.OnAdClicked += OnRewardedAdClickedEvent;
        CloudXAdsCallbacks.Rewarded.OnAdHidden += OnRewardedAdDismissedEvent;
        CloudXAdsCallbacks.Rewarded.OnAdRewarded += OnRewardedAdReceivedRewardEvent;
        CloudXAdsCallbacks.Rewarded.OnAdRevenuePaid += OnRewardedAdRevenuePaidEvent;

        LoadRewardedAd();
    }

    /* Request a rewarded ad. */
    private void LoadRewardedAd()
    {
        _ui.SetRewardedStatus("Loading...");
        CloudXSdk.LoadRewarded(RewardedAdUnitId);
    }

    /* Check IsRewardedReady before Show. If it is not ready, load again. */
    private void ShowRewardedAd()
    {
        if (CloudXSdk.IsRewardedReady(RewardedAdUnitId))
        {
            _ui.SetRewardedStatus("Showing");
            CloudXSdk.ShowRewarded(RewardedAdUnitId, placement: "home_rewarded", customData: "home_rewarded_data");
        }
        else
        {
            _ui.SetRewardedStatus("Ad not ready; reloading");
            LoadRewardedAd();
        }
    }

    private void OnRewardedAdLoadedEvent(CloudXAd cloudXAd)
    {
        _ui.SetRewardedStatus($"Loaded {cloudXAd.AdUnitId}");
        Log($"Rewarded ad loaded: {cloudXAd}");
    }

    /* Demo retries after a short delay. Use your own backoff in production. */
    private void OnRewardedAdFailedEvent(string adUnitId, CloudXError cloudXError)
    {
        _ui.SetRewardedStatus($"Load failed: {cloudXError.Message}\nRetrying in {FullscreenRetryDelaySeconds}s...");
        Log($"Rewarded ad failed to load: {adUnitId} - {cloudXError}");

        Invoke("LoadRewardedAd", FullscreenRetryDelaySeconds);
    }

    private void OnRewardedAdDisplayedEvent(CloudXAd cloudXAd)
    {
        Log($"Rewarded ad displayed: {cloudXAd}");
    }

    /* Show failed. Reload so a later Show can succeed. */
    private void OnRewardedAdFailedToDisplayEvent(CloudXAd cloudXAd, CloudXError cloudXError)
    {
        _ui.SetRewardedStatus($"Show failed: {cloudXError.Message}\nRetrying in {FullscreenRetryDelaySeconds}s...");
        Log($"Rewarded ad failed to display - {cloudXAd} - Error: {cloudXError}");
        Invoke("LoadRewardedAd", FullscreenRetryDelaySeconds);
    }

    private void OnRewardedAdClickedEvent(CloudXAd cloudXAd)
    {
        Log($"Rewarded ad clicked: {cloudXAd}");
    }

    /* Reload after dismiss so the next Show has an ad ready. */
    private void OnRewardedAdDismissedEvent(CloudXAd cloudXAd)
    {
        Log($"Rewarded ad dismissed: {cloudXAd}");
        LoadRewardedAd();
    }

    /* Grant the reward here. This fires when the user earns it, not on dismiss. */
    private void OnRewardedAdReceivedRewardEvent(CloudXAd cloudXAd, CloudXReward cloudXReward)
    {
        Log($"Rewarded ad received reward: {cloudXReward.Amount} {cloudXReward.Label}");
    }

    /* Impression revenue for this rewarded ad. Forward to your analytics if needed. */
    private void OnRewardedAdRevenuePaidEvent(CloudXAd cloudXAd)
    {
        Log($"Rewarded revenue paid - {cloudXAd}");
    }

    #endregion

    /*
     * Hide banner / MREC when the app backgrounds, and show them again on focus.
     * Stop auto-refresh while hidden so you do not request ads off-screen.
     */
    private void OnApplicationFocus(bool hasFocus)
    {
        Log($"OnApplicationFocus: hasFocus={hasFocus}, isBannerShown={isBannerShown}, isMrecShowing={isMrecShowing}");

        if (hasFocus)
        {
            if (isBannerShown)
            {
                Log("Showing banner on focus");
                CloudXSdk.StartBannerAutoRefresh(BannerAdUnitId);
                CloudXSdk.ShowBanner(BannerAdUnitId);
            }

            if (isMrecShowing)
            {
                Log("Showing MREC on focus");
                CloudXSdk.StartMrecAutoRefresh(MrecAdUnitId);
                CloudXSdk.ShowMrec(MrecAdUnitId);
            }
        }
        else
        {
            if (isBannerShown)
            {
                Log("Hiding banner on focus loss");
                CloudXSdk.StopBannerAutoRefresh(BannerAdUnitId);
                CloudXSdk.HideBanner(BannerAdUnitId);
            }

            if (isMrecShowing)
            {
                Log("Hiding MREC on focus loss");
                CloudXSdk.StopMrecAutoRefresh(MrecAdUnitId);
                CloudXSdk.HideMrec(MrecAdUnitId);
            }
        }
    }
}
