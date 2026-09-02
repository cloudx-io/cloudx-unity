using System;
using CloudX;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

/*
 * First Look rewarded. Same as FirstLookInterstitialController plus the reward
 * callback of both SDKs surfaced through RewardEarned.
 */
public sealed class FirstLookRewardedController : FirstLookFullscreenController
{
    public event Action<FirstLookSource, string> RewardEarned;

    private RewardedAd _adMobRewarded;

    public FirstLookRewardedController(
        string cloudXAdUnitId,
        string adMobAdUnitId,
        bool cloudXAvailable)
        : base(cloudXAdUnitId, adMobAdUnitId, cloudXAvailable)
    {
        SubscribeCloudXCallbacks();
    }

    protected override void SubscribeCloudXCallbacks()
    {
        CloudXAdsCallbacks.Rewarded.OnAdLoadSuccess += CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Rewarded.OnAdLoadFailed += CloudXOnLoadFailed;
        CloudXAdsCallbacks.Rewarded.OnAdShowSuccess += CloudXOnShowSuccess;
        CloudXAdsCallbacks.Rewarded.OnAdShowFailed += CloudXOnShowFailed;
        CloudXAdsCallbacks.Rewarded.OnAdHidden += CloudXOnHidden;
        CloudXAdsCallbacks.Rewarded.OnAdClicked += CloudXOnClicked;
        CloudXAdsCallbacks.Rewarded.OnAdRewarded += CloudXOnRewarded;
    }

    protected override void UnsubscribeCloudXCallbacks()
    {
        CloudXAdsCallbacks.Rewarded.OnAdLoadSuccess -= CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Rewarded.OnAdLoadFailed -= CloudXOnLoadFailed;
        CloudXAdsCallbacks.Rewarded.OnAdShowSuccess -= CloudXOnShowSuccess;
        CloudXAdsCallbacks.Rewarded.OnAdShowFailed -= CloudXOnShowFailed;
        CloudXAdsCallbacks.Rewarded.OnAdHidden -= CloudXOnHidden;
        CloudXAdsCallbacks.Rewarded.OnAdClicked -= CloudXOnClicked;
        CloudXAdsCallbacks.Rewarded.OnAdRewarded -= CloudXOnRewarded;
    }

    protected override bool CloudXIsReady() => CloudXSdk.IsRewardedReady(CloudXAdUnitId);
    protected override void CloudXLoad() => CloudXSdk.LoadRewarded(CloudXAdUnitId);
    protected override void CloudXShow() => CloudXSdk.ShowRewarded(CloudXAdUnitId);
    protected override void DestroyCloudXAd() => CloudXSdk.DestroyRewarded(CloudXAdUnitId);

    protected override bool AdMobCanShow() => _adMobRewarded != null && _adMobRewarded.CanShowAd();

    protected override void AdMobLoad()
    {
        /*
         * Google Mobile Ads raises its callbacks off the Unity main thread.
         * ExecuteInUpdate moves the whole body onto it, so controller state and
         * the events subscribers use for UI both stay on one thread, like the
         * CloudX callbacks.
         */
        RewardedAd.Load(
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

                _adMobRewarded = ad;
                RegisterAdMobEvents(ad);
                RaiseAdLoaded(FirstLookSource.AdMob);
            }));
    }

    protected override void AdMobShow()
    {
        _adMobRewarded.Show(reward => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            RewardEarned?.Invoke(FirstLookSource.AdMob, $"{reward.Amount} {reward.Type}")));
    }

    protected override void DestroyAdMobAd()
    {
        _adMobRewarded?.Destroy();
        _adMobRewarded = null;
    }

    private void CloudXOnRewarded(CloudXAd ad, CloudXReward reward)
    {
        if (ad.AdUnitId == CloudXAdUnitId)
        {
            RewardEarned?.Invoke(FirstLookSource.CloudX, $"{reward.Amount} {reward.Label}");
        }
    }

    private void RegisterAdMobEvents(RewardedAd ad)
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
