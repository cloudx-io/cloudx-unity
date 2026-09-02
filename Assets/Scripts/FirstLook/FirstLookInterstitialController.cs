using CloudX;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

/*
 * First Look interstitial. Shared flow lives in FirstLookFullscreenController;
 * this class only supplies the interstitial SDK calls for each side.
 */
public sealed class FirstLookInterstitialController : FirstLookFullscreenController
{
    private InterstitialAd _adMobInterstitial;

    public FirstLookInterstitialController(
        string cloudXAdUnitId,
        string adMobAdUnitId,
        bool cloudXAvailable)
        : base(cloudXAdUnitId, adMobAdUnitId, cloudXAvailable)
    {
        SubscribeCloudXCallbacks();
    }

    protected override void SubscribeCloudXCallbacks()
    {
        CloudXAdsCallbacks.Interstitial.OnAdLoadSuccess += CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Interstitial.OnAdLoadFailed += CloudXOnLoadFailed;
        CloudXAdsCallbacks.Interstitial.OnAdShowSuccess += CloudXOnShowSuccess;
        CloudXAdsCallbacks.Interstitial.OnAdShowFailed += CloudXOnShowFailed;
        CloudXAdsCallbacks.Interstitial.OnAdHidden += CloudXOnHidden;
        CloudXAdsCallbacks.Interstitial.OnAdClicked += CloudXOnClicked;
    }

    protected override void UnsubscribeCloudXCallbacks()
    {
        CloudXAdsCallbacks.Interstitial.OnAdLoadSuccess -= CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Interstitial.OnAdLoadFailed -= CloudXOnLoadFailed;
        CloudXAdsCallbacks.Interstitial.OnAdShowSuccess -= CloudXOnShowSuccess;
        CloudXAdsCallbacks.Interstitial.OnAdShowFailed -= CloudXOnShowFailed;
        CloudXAdsCallbacks.Interstitial.OnAdHidden -= CloudXOnHidden;
        CloudXAdsCallbacks.Interstitial.OnAdClicked -= CloudXOnClicked;
    }

    protected override bool CloudXIsReady() => CloudXSdk.IsInterstitialReady(CloudXAdUnitId);
    protected override void CloudXLoad() => CloudXSdk.LoadInterstitial(CloudXAdUnitId);
    protected override void CloudXShow() => CloudXSdk.ShowInterstitial(CloudXAdUnitId);
    protected override void DestroyCloudXAd() => CloudXSdk.DestroyInterstitial(CloudXAdUnitId);

    protected override bool AdMobCanShow() => _adMobInterstitial != null && _adMobInterstitial.CanShowAd();

    protected override void AdMobLoad()
    {
        /*
         * Google Mobile Ads raises its callbacks off the Unity main thread.
         * ExecuteInUpdate moves the whole body onto it, so controller state and
         * the events subscribers use for UI both stay on one thread, like the
         * CloudX callbacks.
         */
        InterstitialAd.Load(
            AdMobAdUnitId,
            new AdRequest(),
            (ad, error) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                IsLoadingAdMob = false;

                if (IsDisposed)
                {
                    ad?.Destroy();
                    return;
                }

                if (error != null || ad == null)
                {
                    RaiseAdLoadFailed(
                        FirstLookSource.AdMob,
                        error?.GetMessage() ?? "AdMob returned no ad");
                    return;
                }

                _adMobInterstitial = ad;
                RegisterAdMobEvents(ad);
                RaiseAdLoaded(FirstLookSource.AdMob);
            }));
    }

    protected override void AdMobShow() => _adMobInterstitial.Show();

    protected override void DestroyAdMobAd()
    {
        _adMobInterstitial?.Destroy();
        _adMobInterstitial = null;
    }

    private void RegisterAdMobEvents(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentOpened += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            RaiseAdShown(FirstLookSource.AdMob));

        ad.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            DestroyAdMobAd();
            RaiseAdClosed(FirstLookSource.AdMob);
        });

        ad.OnAdFullScreenContentFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            DestroyAdMobAd();
            RaiseAdShowFailed(FirstLookSource.AdMob, error.GetMessage());
        });

        ad.OnAdClicked += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            RaiseAdClicked(FirstLookSource.AdMob));
    }
}
