using CloudX;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

/*
 * First Look banner. Shared flow lives in FirstLookInlineController; this class
 * only supplies the banner SDK calls. Top banner on both SDKs (see
 * FirstLookScreen). Auto-refresh is kept off - see the FirstLookInlineController
 * class note; the crucial call is StopBannerAutoRefresh before create.
 */
public sealed class FirstLookBannerController : FirstLookInlineController
{
    private const CloudXAdViewConfiguration.AdViewPosition CloudXPosition =
        CloudXAdViewConfiguration.AdViewPosition.TopCenter;

    private BannerView _adMobBanner;

    public FirstLookBannerController(
        string cloudXAdUnitId,
        string adMobAdUnitId,
        bool cloudXAvailable)
        : base(cloudXAdUnitId, adMobAdUnitId, cloudXAvailable)
    {
        SubscribeCloudXCallbacks();
    }

    protected override void SubscribeCloudXCallbacks()
    {
        CloudXAdsCallbacks.Banner.OnAdLoadSuccess += CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Banner.OnAdLoadFailed += CloudXOnLoadFailed;
        CloudXAdsCallbacks.Banner.OnAdClicked += CloudXOnClicked;
    }

    protected override void UnsubscribeCloudXCallbacks()
    {
        CloudXAdsCallbacks.Banner.OnAdLoadSuccess -= CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Banner.OnAdLoadFailed -= CloudXOnLoadFailed;
        CloudXAdsCallbacks.Banner.OnAdClicked -= CloudXOnClicked;
    }

    protected override void CloudXCreateAndLoad()
    {
        CloudXSdk.DestroyBanner(CloudXAdUnitId);

        /*
         * Required, not optional: CloudX banner auto-refresh is opt-out, so
         * without this the first ShowBanner would start a background reload that
         * could swap the ad out from under the First Look source decision. It
         * goes before CreateBanner: the native layer registers the ad unit as
         * refresh-disabled even with no view yet, then creates the view with
         * refresh already off, so no timer ever runs. (Destroy clears that
         * registration, hence this order.)
         */
        CloudXSdk.StopBannerAutoRefresh(CloudXAdUnitId);

        /*
         * Placement and custom data must be set before CreateBanner so they are
         * on the first request. CreateBanner also issues the first load, so the
         * OnAdLoadSuccess / OnAdLoadFailed callbacks that drive the source and
         * the fallback come from here - no separate LoadBanner call.
         */
        CloudXSdk.SetBannerPlacement(CloudXAdUnitId, "first_look_screen");
        CloudXSdk.SetBannerCustomData(CloudXAdUnitId, "first_look_banner_data");
        CloudXSdk.CreateBanner(CloudXAdUnitId, new CloudXAdViewConfiguration(CloudXPosition));
    }

    protected override void CloudXShow() => CloudXSdk.ShowBanner(CloudXAdUnitId);
    protected override void CloudXHide() => CloudXSdk.HideBanner(CloudXAdUnitId);
    protected override void DestroyCloudXAd() => CloudXSdk.DestroyBanner(CloudXAdUnitId);

    protected override void AdMobCreateAndLoad()
    {
        DestroyAdMobAd();

        /*
         * A BannerView loads once; there is no refresh API to turn off here. Its
         * refresh is the ad unit's Automatic refresh setting in the AdMob console,
         * which MUST be Disabled for this unit - otherwise AdMob replaces the ad
         * on its own schedule behind First Look's back. Google Mobile Ads raises
         * its callbacks off the Unity main thread; ExecuteInUpdate moves them
         * back on.
         */
        _adMobBanner = new BannerView(AdMobAdUnitId, AdSize.Banner, AdPosition.Top);

        _adMobBanner.OnBannerAdLoaded += () => MobileAdsEventExecutor.ExecuteInUpdate(OnAdMobLoaded);

        _adMobBanner.OnBannerAdLoadFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            OnAdMobLoadFailed(error.GetMessage()));

        _adMobBanner.OnAdClicked += () => MobileAdsEventExecutor.ExecuteInUpdate(OnAdMobClicked);

        /* Created hidden; Show()/Hide() drive visibility. */
        _adMobBanner.Hide();
        _adMobBanner.LoadAd(new AdRequest());
    }

    protected override void AdMobShow() => _adMobBanner.Show();
    protected override void AdMobHide() => _adMobBanner?.Hide();

    protected override void DestroyAdMobAd()
    {
        _adMobBanner?.Destroy();
        _adMobBanner = null;
    }
}
