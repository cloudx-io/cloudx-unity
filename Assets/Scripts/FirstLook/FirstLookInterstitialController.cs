using System;
using CloudX;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

/*
 * First Look interstitial: CloudX gets the first chance to fill; AdMob loads
 * lazily as the fallback only after CloudX fails to load. Show() shows CloudX
 * if it is ready, otherwise AdMob, and returns false when neither source has
 * an ad. Mirrors docs.cloudx.io -> Integrations -> First Look.
 */
public sealed class FirstLookInterstitialController : IDisposable
{
    public event Action<FirstLookSource> AdLoaded;
    public event Action<FirstLookSource, string> AdLoadFailed;
    public event Action<FirstLookSource> AdShown;
    public event Action<FirstLookSource, string> AdShowFailed;
    public event Action<FirstLookSource> AdClosed;
    public event Action<FirstLookSource> AdClicked;

    private readonly string cloudXAdUnitId;
    private readonly string adMobAdUnitId;

    /*
     * When CloudX initialization failed, its load callbacks may never fire, so
     * the controller skips the CloudX leg and goes straight to the fallback.
     */
    private readonly bool cloudXAvailable;

    private InterstitialAd adMobInterstitial;
    private bool isLoadingCloudX;
    private bool isLoadingAdMob;
    private bool isDisposed;

    public FirstLookInterstitialController(
        string cloudXAdUnitId,
        string adMobAdUnitId,
        bool cloudXAvailable)
    {
        this.cloudXAdUnitId = cloudXAdUnitId;
        this.adMobAdUnitId = adMobAdUnitId;
        this.cloudXAvailable = cloudXAvailable;

        CloudXAdsCallbacks.Interstitial.OnAdLoadSuccess += OnCloudXLoaded;
        CloudXAdsCallbacks.Interstitial.OnAdLoadFailed += OnCloudXLoadFailed;
        CloudXAdsCallbacks.Interstitial.OnAdShowSuccess += OnCloudXShown;
        CloudXAdsCallbacks.Interstitial.OnAdShowFailed += OnCloudXShowFailed;
        CloudXAdsCallbacks.Interstitial.OnAdHidden += OnCloudXClosed;
        CloudXAdsCallbacks.Interstitial.OnAdClicked += OnCloudXClicked;
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

            if (cloudXAvailable && CloudXSdk.IsInterstitialReady(cloudXAdUnitId))
            {
                return FirstLookSource.CloudX;
            }

            if (adMobInterstitial != null && adMobInterstitial.CanShowAd())
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
        CloudXSdk.LoadInterstitial(cloudXAdUnitId);
    }

    public bool Show()
    {
        if (isDisposed)
        {
            return false;
        }

        if (cloudXAvailable && CloudXSdk.IsInterstitialReady(cloudXAdUnitId))
        {
            CloudXSdk.ShowInterstitial(cloudXAdUnitId);
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

        CloudXAdsCallbacks.Interstitial.OnAdLoadSuccess -= OnCloudXLoaded;
        CloudXAdsCallbacks.Interstitial.OnAdLoadFailed -= OnCloudXLoadFailed;
        CloudXAdsCallbacks.Interstitial.OnAdShowSuccess -= OnCloudXShown;
        CloudXAdsCallbacks.Interstitial.OnAdShowFailed -= OnCloudXShowFailed;
        CloudXAdsCallbacks.Interstitial.OnAdHidden -= OnCloudXClosed;
        CloudXAdsCallbacks.Interstitial.OnAdClicked -= OnCloudXClicked;

        if (cloudXAvailable)
        {
            CloudXSdk.DestroyInterstitial(cloudXAdUnitId);
        }

        DestroyAdMobInterstitial();
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

    private void LoadAdMobFallback()
    {
        if (isDisposed ||
            isLoadingAdMob ||
            (adMobInterstitial != null && adMobInterstitial.CanShowAd()))
        {
            return;
        }

        isLoadingAdMob = true;
        DestroyAdMobInterstitial();

        /*
         * Google Mobile Ads raises its callbacks off the Unity main thread.
         * ExecuteInUpdate moves the whole body onto it, so controller state and
         * the events subscribers use for UI both stay on one thread, like the
         * CloudX callbacks above.
         */
        InterstitialAd.Load(
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

                adMobInterstitial = ad;
                RegisterAdMobEvents(ad);
                AdLoaded?.Invoke(FirstLookSource.AdMob);
            }));
    }

    private bool ShowAdMobFallback()
    {
        if (adMobInterstitial == null || !adMobInterstitial.CanShowAd())
        {
            return false;
        }

        adMobInterstitial.Show();
        return true;
    }

    private void RegisterAdMobEvents(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentOpened += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            AdShown?.Invoke(FirstLookSource.AdMob));

        ad.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            DestroyAdMobInterstitial();
            AdClosed?.Invoke(FirstLookSource.AdMob);
        });

        ad.OnAdFullScreenContentFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            DestroyAdMobInterstitial();
            AdShowFailed?.Invoke(FirstLookSource.AdMob, error.GetMessage());
        });

        ad.OnAdClicked += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            AdClicked?.Invoke(FirstLookSource.AdMob));
    }

    private void DestroyAdMobInterstitial()
    {
        adMobInterstitial?.Destroy();
        adMobInterstitial = null;
    }
}
