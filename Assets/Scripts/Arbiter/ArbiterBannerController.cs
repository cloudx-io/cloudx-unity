using CloudX;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

/*
 * Arbiter/TPA banner. Shared flow lives in ArbiterInlineController; this class
 * only supplies the banner SDK calls. Top banner on both SDKs. Auto-refresh is
 * kept off on both sides - see the ArbiterInlineController class note; the
 * crucial CloudX call is StopBannerAutoRefresh before create.
 */
public sealed class ArbiterBannerController : ArbiterInlineController
{
    private const CloudXAdViewConfiguration.AdViewPosition CloudXPosition =
        CloudXAdViewConfiguration.AdViewPosition.TopCenter;

    private BannerView _adMobBanner;

    public ArbiterBannerController(
        string cloudXAdUnitId,
        string adMobAdUnitId,
        bool cloudXAvailable,
        float refreshIntervalSeconds)
        : base(cloudXAdUnitId, adMobAdUnitId, cloudXAvailable, refreshIntervalSeconds)
    {
        SubscribeCloudXCallbacks();
    }

    protected override string AdFormatName => "banner";

    protected override void SubscribeCloudXCallbacks()
    {
        CloudXAdsCallbacks.Banner.OnAdLoadSuccess += CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Banner.OnAdLoadFailed += CloudXOnLoadFailed;
        CloudXAdsCallbacks.Banner.OnAdClicked += CloudXOnClicked;
        CloudXAdsCallbacks.Banner.OnAdRevenuePaid += CloudXOnRevenuePaid;
    }

    protected override void UnsubscribeCloudXCallbacks()
    {
        CloudXAdsCallbacks.Banner.OnAdLoadSuccess -= CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Banner.OnAdLoadFailed -= CloudXOnLoadFailed;
        CloudXAdsCallbacks.Banner.OnAdClicked -= CloudXOnClicked;
        CloudXAdsCallbacks.Banner.OnAdRevenuePaid -= CloudXOnRevenuePaid;
    }

    protected override void CloudXCreateAndLoad()
    {
        CloudXSdk.DestroyBanner(CloudXAdUnitId);

        /*
         * Required, not optional: CloudX banner auto-refresh is opt-out, so
         * without this the first ShowBanner would start a background reload that
         * races the arbiter. It goes before CreateBanner: the native layer
         * registers the ad unit as refresh-disabled even with no view yet, then
         * creates the view with refresh already off, so no timer ever runs.
         * (Destroy clears that registration, hence this order.) It also unlocks
         * the explicit LoadBanner used for later rounds.
         */
        CloudXSdk.StopBannerAutoRefresh(CloudXAdUnitId);

        /*
         * Placement and custom data must be set before CreateBanner so they are
         * on the first request. CreateBanner also issues the first load.
         */
        CloudXSdk.SetBannerPlacement(CloudXAdUnitId, "arbiter_screen");
        CloudXSdk.SetBannerCustomData(CloudXAdUnitId, "arbiter_banner_data");
        CloudXSdk.CreateBanner(CloudXAdUnitId, new CloudXAdViewConfiguration(CloudXPosition));
    }

    protected override void CloudXLoad() => CloudXSdk.LoadBanner(CloudXAdUnitId);
    protected override void CloudXShow() => CloudXSdk.ShowBanner(CloudXAdUnitId);
    protected override void CloudXHide() => CloudXSdk.HideBanner(CloudXAdUnitId);
    protected override void DestroyCloudXAd() => CloudXSdk.DestroyBanner(CloudXAdUnitId);

    protected override void AdMobCreateHidden()
    {
        DestroyAdMobAd();

        /*
         * A BannerView loads once per LoadAd; there is no refresh API to turn off
         * here. Its refresh is the ad unit's Automatic refresh setting in the
         * AdMob console, which MUST be Disabled for this unit. Google Mobile Ads
         * raises its callbacks off the Unity main thread; ExecuteInUpdate moves
         * them back on.
         */
        _adMobBanner = new BannerView(AdMobAdUnitId, AdSize.Banner, AdPosition.Top);

        _adMobBanner.OnBannerAdLoaded += () => MobileAdsEventExecutor.ExecuteInUpdate(OnAdMobLoaded);

        _adMobBanner.OnBannerAdLoadFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            OnAdMobLoadFailed(error.GetMessage()));

        _adMobBanner.OnAdClicked += () => MobileAdsEventExecutor.ExecuteInUpdate(OnAdMobClicked);
        _adMobBanner.OnAdImpressionRecorded += () => MobileAdsEventExecutor.ExecuteInUpdate(OnAdMobImpression);

        /* Required: this is how CloudX learns what the AdMob bid was worth. */
        _adMobBanner.OnAdPaid += adValue => MobileAdsEventExecutor.ExecuteInUpdate(() => OnAdMobPaid(adValue));

        /* Created hidden: a load must never render a view the arbiter did not pick. */
        _adMobBanner.Hide();
    }

    protected override void AdMobLoad() => _adMobBanner.LoadAd(new AdRequest());
    protected override void AdMobShow() => _adMobBanner.Show();
    protected override void AdMobHide() => _adMobBanner?.Hide();
    protected override ResponseInfo AdMobResponseInfo() => _adMobBanner?.GetResponseInfo();

    protected override void DestroyAdMobAd()
    {
        _adMobBanner?.Destroy();
        _adMobBanner = null;
    }
}
