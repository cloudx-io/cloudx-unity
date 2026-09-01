using System;
using CloudX;
using GoogleMobileAds.Api;

/*
 * First Look rewarded: CloudX gets the first chance to fill; AdMob loads
 * lazily as the fallback only after CloudX fails to load. Show() shows CloudX
 * if it is ready, otherwise AdMob, and returns false when neither source has
 * an ad. Same pattern as FirstLookInterstitialController plus the reward
 * callback of both SDKs surfaced through RewardEarned.
 */
public sealed class FirstLookRewardedController : IDisposable
{
    public enum Source
    {
        CloudX,
        AdMob,
    }

    public event Action<Source> AdLoaded;
    public event Action<Source, string> AdLoadFailed;
    public event Action<Source> AdShown;
    public event Action<Source, string> AdShowFailed;
    public event Action<Source> AdClosed;
    public event Action<Source> AdClicked;
    public event Action<Source, string> RewardEarned;

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
        bool cloudXAvailable = true)
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
    public Source? ReadySource
    {
        get
        {
            if (isDisposed)
            {
                return null;
            }

            if (cloudXAvailable && CloudXSdk.IsRewardedReady(cloudXAdUnitId))
            {
                return Source.CloudX;
            }

            if (adMobRewarded != null && adMobRewarded.CanShowAd())
            {
                return Source.AdMob;
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
        AdLoaded?.Invoke(Source.CloudX);
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
            AdShown?.Invoke(Source.CloudX);
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
            AdShowFailed?.Invoke(Source.CloudX, error.Message);
        }
    }

    private void OnCloudXClosed(CloudXAd ad)
    {
        if (ad.AdUnitId == cloudXAdUnitId)
        {
            AdClosed?.Invoke(Source.CloudX);
        }
    }

    private void OnCloudXClicked(CloudXAd ad)
    {
        if (ad.AdUnitId == cloudXAdUnitId)
        {
            AdClicked?.Invoke(Source.CloudX);
        }
    }

    private void OnCloudXRewarded(CloudXAd ad, CloudXReward reward)
    {
        if (ad.AdUnitId == cloudXAdUnitId)
        {
            RewardEarned?.Invoke(Source.CloudX, $"{reward.Amount} {reward.Label}");
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

        RewardedAd.Load(
            adMobAdUnitId,
            new AdRequest(),
            (ad, error) =>
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
                        Source.AdMob,
                        error?.GetMessage() ?? "AdMob returned no ad");
                    return;
                }

                adMobRewarded = ad;
                RegisterAdMobEvents(ad);
                AdLoaded?.Invoke(Source.AdMob);
            });
    }

    private bool ShowAdMobFallback()
    {
        if (adMobRewarded == null || !adMobRewarded.CanShowAd())
        {
            return false;
        }

        adMobRewarded.Show(reward =>
            RewardEarned?.Invoke(Source.AdMob, $"{reward.Amount} {reward.Type}"));
        return true;
    }

    private void RegisterAdMobEvents(RewardedAd ad)
    {
        ad.OnAdFullScreenContentOpened += () =>
            AdShown?.Invoke(Source.AdMob);

        ad.OnAdFullScreenContentClosed += () =>
        {
            DestroyAdMobRewarded();
            AdClosed?.Invoke(Source.AdMob);
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            DestroyAdMobRewarded();
            AdShowFailed?.Invoke(Source.AdMob, error.GetMessage());
        };

        ad.OnAdClicked += () =>
            AdClicked?.Invoke(Source.AdMob);
    }

    private void DestroyAdMobRewarded()
    {
        adMobRewarded?.Destroy();
        adMobRewarded = null;
    }
}
