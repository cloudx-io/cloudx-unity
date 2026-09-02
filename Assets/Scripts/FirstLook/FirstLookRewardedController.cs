using System;
using CloudX;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

/*
 * First Look rewarded: CloudX gets the first chance to fill; AdMob loads
 * lazily as the fallback only after CloudX fails to load. Show() shows CloudX
 * if it is ready, otherwise AdMob, and returns false when neither source has
 * an ad. Same pattern as FirstLookInterstitialController plus the reward
 * callback of both SDKs surfaced through RewardEarned.
 */
public sealed class FirstLookRewardedController : IDisposable
{
    public event Action<FirstLookSource> AdLoaded;
    public event Action<FirstLookSource, string> AdLoadFailed;
    public event Action<FirstLookSource> AdShown;
    public event Action<FirstLookSource, string> AdShowFailed;
    public event Action<FirstLookSource> AdClosed;
    public event Action<FirstLookSource> AdClicked;
    public event Action<FirstLookSource, string> RewardEarned;

    private readonly string cloudXAdUnitId;
    private readonly string adMobAdUnitId;

    /*
     * When CloudX initialization failed, its load callbacks may never fire, so
     * the controller skips the CloudX leg and goes straight to the fallback.
     */
    private readonly bool cloudXAvailable;

    private RewardedAd adMobRewarded;
    private bool isLoadingCloudX;
    private bool isLoadingAdMob;
    private bool isDisposed;

    public FirstLookRewardedController(
        string cloudXAdUnitId,
        string adMobAdUnitId,
        bool cloudXAvailable)
    {
        this.cloudXAdUnitId = cloudXAdUnitId;
        this.adMobAdUnitId = adMobAdUnitId;
        this.cloudXAvailable = cloudXAvailable;

        CloudXAdsCallbacks.Rewarded.OnAdLoadSuccess += OnCloudXLoaded;
        CloudXAdsCallbacks.Rewarded.OnAdLoadFailed += OnCloudXLoadFailed;
        CloudXAdsCallbacks.Rewarded.OnAdShowSuccess += OnCloudXShown;
        CloudXAdsCallbacks.Rewarded.OnAdShowFailed += OnCloudXShowFailed;
        CloudXAdsCallbacks.Rewarded.OnAdHidden += OnCloudXClosed;
        CloudXAdsCallbacks.Rewarded.OnAdClicked += OnCloudXClicked;
        CloudXAdsCallbacks.Rewarded.OnAdRewarded += OnCloudXRewarded;
    }

    /* The source Show() would use right now; null when no ad is ready. */
    public FirstLookSource? ReadySource
    {
        get
        {
            if (isDisposed)
            {
                return null;
            }

            if (cloudXAvailable && CloudXSdk.IsRewardedReady(cloudXAdUnitId))
            {
                return FirstLookSource.CloudX;
            }

            if (adMobRewarded != null && adMobRewarded.CanShowAd())
            {
                return FirstLookSource.AdMob;
            }

            return null;
        }
    }

    public void Load()
    {
        if (isDisposed || isLoadingCloudX || isLoadingAdMob || ReadySource != null)
        {
            return;
        }

        if (!cloudXAvailable)
        {
            LoadAdMobFallback();
            return;
        }

        isLoadingCloudX = true;
        CloudXSdk.LoadRewarded(cloudXAdUnitId);
    }

    public bool Show()
    {
        if (isDisposed)
        {
            return false;
        }

        if (cloudXAvailable && CloudXSdk.IsRewardedReady(cloudXAdUnitId))
        {
            CloudXSdk.ShowRewarded(cloudXAdUnitId);
            return true;
        }

        return ShowAdMobFallback();
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        CloudXAdsCallbacks.Rewarded.OnAdLoadSuccess -= OnCloudXLoaded;
        CloudXAdsCallbacks.Rewarded.OnAdLoadFailed -= OnCloudXLoadFailed;
        CloudXAdsCallbacks.Rewarded.OnAdShowSuccess -= OnCloudXShown;
        CloudXAdsCallbacks.Rewarded.OnAdShowFailed -= OnCloudXShowFailed;
        CloudXAdsCallbacks.Rewarded.OnAdHidden -= OnCloudXClosed;
        CloudXAdsCallbacks.Rewarded.OnAdClicked -= OnCloudXClicked;
        CloudXAdsCallbacks.Rewarded.OnAdRewarded -= OnCloudXRewarded;

        if (cloudXAvailable)
        {
            CloudXSdk.DestroyRewarded(cloudXAdUnitId);
        }

        DestroyAdMobRewarded();
    }

    private void OnCloudXLoaded(CloudXAd ad)
    {
        if (ad.AdUnitId != cloudXAdUnitId)
        {
            return;
        }

        isLoadingCloudX = false;
        AdLoaded?.Invoke(FirstLookSource.CloudX);
    }

    private void OnCloudXLoadFailed(string adUnitId, CloudXError _)
    {
        if (adUnitId != cloudXAdUnitId)
        {
            return;
        }

        isLoadingCloudX = false;
        LoadAdMobFallback();
    }

    private void OnCloudXShown(CloudXAd ad)
    {
        if (ad.AdUnitId == cloudXAdUnitId)
        {
            AdShown?.Invoke(FirstLookSource.CloudX);
        }
    }

    private void OnCloudXShowFailed(CloudXAd ad, CloudXError error)
    {
        if (ad.AdUnitId != cloudXAdUnitId)
        {
            return;
        }

        if (!ShowAdMobFallback())
        {
            AdShowFailed?.Invoke(FirstLookSource.CloudX, error.Message);
        }
    }

    private void OnCloudXClosed(CloudXAd ad)
    {
        if (ad.AdUnitId == cloudXAdUnitId)
        {
            AdClosed?.Invoke(FirstLookSource.CloudX);
        }
    }

    private void OnCloudXClicked(CloudXAd ad)
    {
        if (ad.AdUnitId == cloudXAdUnitId)
        {
            AdClicked?.Invoke(FirstLookSource.CloudX);
        }
    }

    private void OnCloudXRewarded(CloudXAd ad, CloudXReward reward)
    {
        if (ad.AdUnitId == cloudXAdUnitId)
        {
            RewardEarned?.Invoke(FirstLookSource.CloudX, $"{reward.Amount} {reward.Label}");
        }
    }

    private void LoadAdMobFallback()
    {
        if (isDisposed ||
            isLoadingAdMob ||
            (adMobRewarded != null && adMobRewarded.CanShowAd()))
        {
            return;
        }

        isLoadingAdMob = true;
        DestroyAdMobRewarded();

        /*
         * Google Mobile Ads raises its callbacks off the Unity main thread.
         * ExecuteInUpdate moves the whole body onto it, so controller state and
         * the events subscribers use for UI both stay on one thread, like the
         * CloudX callbacks above.
         */
        RewardedAd.Load(
            adMobAdUnitId,
            new AdRequest(),
            (ad, error) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                isLoadingAdMob = false;

                if (isDisposed)
                {
                    ad?.Destroy();
                    return;
                }

                if (error != null || ad == null)
                {
                    AdLoadFailed?.Invoke(
                        FirstLookSource.AdMob,
                        error?.GetMessage() ?? "AdMob returned no ad");
                    return;
                }

                adMobRewarded = ad;
                RegisterAdMobEvents(ad);
                AdLoaded?.Invoke(FirstLookSource.AdMob);
            }));
    }

    private bool ShowAdMobFallback()
    {
        if (adMobRewarded == null || !adMobRewarded.CanShowAd())
        {
            return false;
        }

        adMobRewarded.Show(reward => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            RewardEarned?.Invoke(FirstLookSource.AdMob, $"{reward.Amount} {reward.Type}")));
        return true;
    }

    private void RegisterAdMobEvents(RewardedAd ad)
    {
        ad.OnAdFullScreenContentOpened += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            AdShown?.Invoke(FirstLookSource.AdMob));

        ad.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            DestroyAdMobRewarded();
            AdClosed?.Invoke(FirstLookSource.AdMob);
        });

        ad.OnAdFullScreenContentFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            DestroyAdMobRewarded();
            AdShowFailed?.Invoke(FirstLookSource.AdMob, error.GetMessage());
        });

        ad.OnAdClicked += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            AdClicked?.Invoke(FirstLookSource.AdMob));
    }

    private void DestroyAdMobRewarded()
    {
        adMobRewarded?.Destroy();
        adMobRewarded = null;
    }
}
