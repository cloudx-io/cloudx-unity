using CloudX;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

/*
 * First Look MREC (300x250). Same as FirstLookBannerController with the MREC SDK
 * calls: bottom-center on both SDKs, MREC size on AdMob. MREC takes an
 * AdViewPosition only (a vertical config throws). Auto-refresh is
 * kept off - see the FirstLookInlineController class note; the crucial call is
 * StopMrecAutoRefresh before create.
 */
public sealed class FirstLookMrecController : FirstLookInlineController
{
    private const CloudXAdViewConfiguration.AdViewPosition CloudXPosition =
        CloudXAdViewConfiguration.AdViewPosition.BottomCenter;

    private BannerView _adMobMrec;

    public FirstLookMrecController(
        string cloudXAdUnitId,
        string adMobAdUnitId,
        bool cloudXAvailable)
        : base(cloudXAdUnitId, adMobAdUnitId, cloudXAvailable)
    {
        SubscribeCloudXCallbacks();
    }

    protected override void SubscribeCloudXCallbacks()
    {
        CloudXAdsCallbacks.Mrec.OnAdLoadSuccess += CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Mrec.OnAdLoadFailed += CloudXOnLoadFailed;
        CloudXAdsCallbacks.Mrec.OnAdClicked += CloudXOnClicked;
    }

    protected override void UnsubscribeCloudXCallbacks()
    {
        CloudXAdsCallbacks.Mrec.OnAdLoadSuccess -= CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Mrec.OnAdLoadFailed -= CloudXOnLoadFailed;
        CloudXAdsCallbacks.Mrec.OnAdClicked -= CloudXOnClicked;
    }

    protected override void CloudXCreateAndLoad()
    {
        CloudXSdk.DestroyMrec(CloudXAdUnitId);

        /*
         * Required, not optional: CloudX MREC auto-refresh is opt-out, so without
         * this the first ShowMrec would start a background reload that could swap
         * the ad out from under the First Look source decision. It goes before
         * CreateMrec: the native layer registers the ad unit as refresh-disabled
         * even with no view yet, then creates the view with refresh already off,
         * so no timer ever runs. (Destroy clears that registration, hence this
         * order.)
         */
        CloudXSdk.StopMrecAutoRefresh(CloudXAdUnitId);

        /*
         * Placement and custom data must be set before CreateMrec so they are on
         * the first request. CreateMrec also issues the first load, so the
         * OnAdLoadSuccess / OnAdLoadFailed callbacks that drive the source and
         * the fallback come from here - no separate LoadMrec call. Note the
         * capital-R setter names against the lowercase-r lifecycle methods.
         */
        CloudXSdk.SetMRecPlacement(CloudXAdUnitId, "first_look_screen");
        CloudXSdk.SetMRecCustomData(CloudXAdUnitId, "first_look_mrec_data");
        CloudXSdk.CreateMrec(CloudXAdUnitId, new CloudXAdViewConfiguration(CloudXPosition));
    }

    protected override void CloudXShow() => CloudXSdk.ShowMrec(CloudXAdUnitId);
    protected override void CloudXHide() => CloudXSdk.HideMrec(CloudXAdUnitId);
    protected override void DestroyCloudXAd() => CloudXSdk.DestroyMrec(CloudXAdUnitId);

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
        _adMobMrec = new BannerView(AdMobAdUnitId, AdSize.MediumRectangle, AdPosition.Bottom);

        _adMobMrec.OnBannerAdLoaded += () => MobileAdsEventExecutor.ExecuteInUpdate(OnAdMobLoaded);

        _adMobMrec.OnBannerAdLoadFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            OnAdMobLoadFailed(error.GetMessage()));

        _adMobMrec.OnAdClicked += () => MobileAdsEventExecutor.ExecuteInUpdate(OnAdMobClicked);

        /* Created hidden; Show()/Hide() drive visibility. */
        _adMobMrec.Hide();
        _adMobMrec.LoadAd(new AdRequest());
    }

    protected override void AdMobShow() => _adMobMrec.Show();
    protected override void AdMobHide() => _adMobMrec?.Hide();

    protected override void DestroyAdMobAd()
    {
        _adMobMrec?.Destroy();
        _adMobMrec = null;
    }
}
