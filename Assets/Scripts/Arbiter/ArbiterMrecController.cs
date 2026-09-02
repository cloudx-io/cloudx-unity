using CloudX;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

/*
 * Arbiter/TPA MREC (300x250). Shared flow lives in ArbiterInlineController;
 * this class only supplies the MREC SDK calls: bottom-center on both SDKs, MREC
 * size on AdMob. MREC takes an AdViewPosition only (a vertical config throws).
 * Auto-refresh is kept off on both sides - see the ArbiterInlineController class
 * note; the crucial CloudX call is StopMrecAutoRefresh before create. Note the
 * capital-R setter names against the lowercase-r lifecycle methods.
 */
public sealed class ArbiterMrecController : ArbiterInlineController
{
    private const CloudXAdViewConfiguration.AdViewPosition CloudXPosition =
        CloudXAdViewConfiguration.AdViewPosition.BottomCenter;

    private BannerView _adMobMrec;

    public ArbiterMrecController(
        string cloudXAdUnitId,
        string adMobAdUnitId,
        bool cloudXAvailable,
        float refreshIntervalSeconds)
        : base(cloudXAdUnitId, adMobAdUnitId, cloudXAvailable, refreshIntervalSeconds)
    {
        SubscribeCloudXCallbacks();
    }

    protected override string AdFormatName => "mrec";

    protected override void SubscribeCloudXCallbacks()
    {
        CloudXAdsCallbacks.Mrec.OnAdLoadSuccess += CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Mrec.OnAdLoadFailed += CloudXOnLoadFailed;
        CloudXAdsCallbacks.Mrec.OnAdClicked += CloudXOnClicked;
        CloudXAdsCallbacks.Mrec.OnAdRevenuePaid += CloudXOnRevenuePaid;
    }

    protected override void UnsubscribeCloudXCallbacks()
    {
        CloudXAdsCallbacks.Mrec.OnAdLoadSuccess -= CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Mrec.OnAdLoadFailed -= CloudXOnLoadFailed;
        CloudXAdsCallbacks.Mrec.OnAdClicked -= CloudXOnClicked;
        CloudXAdsCallbacks.Mrec.OnAdRevenuePaid -= CloudXOnRevenuePaid;
    }

    protected override void CloudXCreateAndLoad()
    {
        CloudXSdk.DestroyMrec(CloudXAdUnitId);

        /*
         * Required, not optional: CloudX MREC auto-refresh is opt-out, so
         * without this the first ShowMrec would start a background reload that
         * races the arbiter. It goes before CreateMrec: the native layer
         * registers the ad unit as refresh-disabled even with no view yet, then
         * creates the view with refresh already off, so no timer ever runs.
         * (Destroy clears that registration, hence this order.) It also unlocks
         * the explicit LoadMrec used for later rounds.
         */
        CloudXSdk.StopMrecAutoRefresh(CloudXAdUnitId);

        /*
         * Placement and custom data must be set before CreateMrec so they are
         * on the first request. CreateMrec also issues the first load.
         */
        CloudXSdk.SetMRecPlacement(CloudXAdUnitId, "arbiter_screen");
        CloudXSdk.SetMRecCustomData(CloudXAdUnitId, "arbiter_mrec_data");
        CloudXSdk.CreateMrec(CloudXAdUnitId, new CloudXAdViewConfiguration(CloudXPosition));
    }

    protected override void CloudXLoad() => CloudXSdk.LoadMrec(CloudXAdUnitId);
    protected override void CloudXShow() => CloudXSdk.ShowMrec(CloudXAdUnitId);
    protected override void CloudXHide() => CloudXSdk.HideMrec(CloudXAdUnitId);
    protected override void DestroyCloudXAd() => CloudXSdk.DestroyMrec(CloudXAdUnitId);

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
        _adMobMrec = new BannerView(AdMobAdUnitId, AdSize.MediumRectangle, AdPosition.Bottom);

        _adMobMrec.OnBannerAdLoaded += () => MobileAdsEventExecutor.ExecuteInUpdate(OnAdMobLoaded);

        _adMobMrec.OnBannerAdLoadFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            OnAdMobLoadFailed(error.GetMessage()));

        _adMobMrec.OnAdClicked += () => MobileAdsEventExecutor.ExecuteInUpdate(OnAdMobClicked);
        _adMobMrec.OnAdImpressionRecorded += () => MobileAdsEventExecutor.ExecuteInUpdate(OnAdMobImpression);

        /* Required: this is how CloudX learns what the AdMob bid was worth. */
        _adMobMrec.OnAdPaid += adValue => MobileAdsEventExecutor.ExecuteInUpdate(() => OnAdMobPaid(adValue));

        /* Created hidden: a load must never render a view the arbiter did not pick. */
        _adMobMrec.Hide();
    }

    protected override void AdMobLoad() => _adMobMrec.LoadAd(new AdRequest());
    protected override void AdMobShow() => _adMobMrec.Show();
    protected override void AdMobHide() => _adMobMrec?.Hide();
    protected override ResponseInfo AdMobResponseInfo() => _adMobMrec?.GetResponseInfo();

    protected override void DestroyAdMobAd()
    {
        _adMobMrec?.Destroy();
        _adMobMrec = null;
    }
}
