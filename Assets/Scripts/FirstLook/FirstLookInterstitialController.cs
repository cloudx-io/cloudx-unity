using System;
using CloudX;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

/*
 * First Look interstitial: CloudX gets the first chance to fill, AdMob loads
 * lazily as the fallback only after CloudX fails. Show() shows CloudX if it is
 * ready, otherwise AdMob, and returns false when neither has an ad - the caller
 * just carries on with the game. Copy this file and FirstLookSource.cs into
 * your project; it is the whole flow, top to bottom, with no base class to
 * bring along. Reading order: state, the Load/Show entry points, then each
 * SDK's callbacks.
 *
 * A fullscreen ad is consumed by being shown, so readiness is asked of the SDKs
 * directly (CloudXSdk.IsInterstitialReady / InterstitialAd.CanShowAd) rather
 * than cached. Showing makes both answers false on their own, so the next
 * Load() starts at CloudX again with nothing for this class to reset. Rewarded
 * works the same way; the banner does not, which is why
 * FirstLookBannerController carries an explicit pass cycle.
 *
 * Background and the reasoning behind each rule:
 * https://docs.cloudx.io/en/unity/integrations/first-look
 */
public sealed class FirstLookInterstitialController : IDisposable
{
    public event Action<FirstLookSource> AdLoaded;
    public event Action<FirstLookSource, string> AdLoadFailed;
    public event Action<FirstLookSource> AdShown;
    public event Action<FirstLookSource, string> AdShowFailed;
    public event Action<FirstLookSource> AdClosed;
    public event Action<FirstLookSource> AdClicked;

    private readonly string _cloudXAdUnitId;
    private readonly string _adMobAdUnitId;

    /*
     * When CloudX initialization failed, its load callbacks may never fire, so
     * the controller skips the CloudX leg and goes straight to the fallback.
     */
    private readonly bool _cloudXAvailable;

    private InterstitialAd _adMobInterstitial;
    private bool _isLoadingCloudX;
    private bool _isLoadingAdMob;
    private bool _isDisposed;

    public FirstLookInterstitialController(
        string cloudXAdUnitId,
        string adMobAdUnitId,
        bool cloudXAvailable)
    {
        _cloudXAdUnitId = cloudXAdUnitId;
        _adMobAdUnitId = adMobAdUnitId;
        _cloudXAvailable = cloudXAvailable;

        CloudXAdsCallbacks.Interstitial.OnAdLoadSuccess += CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Interstitial.OnAdLoadFailed += CloudXOnLoadFailed;
        CloudXAdsCallbacks.Interstitial.OnAdShowSuccess += CloudXOnShowSuccess;
        CloudXAdsCallbacks.Interstitial.OnAdShowFailed += CloudXOnShowFailed;
        CloudXAdsCallbacks.Interstitial.OnAdHidden += CloudXOnHidden;
        CloudXAdsCallbacks.Interstitial.OnAdClicked += CloudXOnClicked;
    }

    /* The source a show right now would use; null when no ad is ready. */
    public FirstLookSource? ReadySource
    {
        get
        {
            if (_isDisposed)
            {
                return null;
            }

            if (_cloudXAvailable && CloudXSdk.IsInterstitialReady(_cloudXAdUnitId))
            {
                return FirstLookSource.CloudX;
            }

            if (AdMobCanShow())
            {
                return FirstLookSource.AdMob;
            }

            return null;
        }
    }

    /*
     * Asks CloudX first. The AdMob fallback is not loaded here - it is loaded
     * only if this CloudX load fails, from CloudXOnLoadFailed, so it costs
     * nothing when CloudX fills.
     */
    public void Load()
    {
        if (_isDisposed || _isLoadingCloudX || _isLoadingAdMob || ReadySource != null)
        {
            return;
        }

        if (!_cloudXAvailable)
        {
            LoadAdMobFallback();
            return;
        }

        _isLoadingCloudX = true;
        CloudXSdk.LoadInterstitial(_cloudXAdUnitId);
    }

    /* Returns whether an ad was shown. False means the game just carries on. */
    public bool Show()
    {
        if (_isDisposed)
        {
            return false;
        }

        if (_cloudXAvailable && CloudXSdk.IsInterstitialReady(_cloudXAdUnitId))
        {
            CloudXSdk.ShowInterstitial(_cloudXAdUnitId);
            return true;
        }

        return ShowAdMobFallback();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        CloudXAdsCallbacks.Interstitial.OnAdLoadSuccess -= CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Interstitial.OnAdLoadFailed -= CloudXOnLoadFailed;
        CloudXAdsCallbacks.Interstitial.OnAdShowSuccess -= CloudXOnShowSuccess;
        CloudXAdsCallbacks.Interstitial.OnAdShowFailed -= CloudXOnShowFailed;
        CloudXAdsCallbacks.Interstitial.OnAdHidden -= CloudXOnHidden;
        CloudXAdsCallbacks.Interstitial.OnAdClicked -= CloudXOnClicked;

        /* Leave the CloudX SDK alone when its init failed. */
        if (_cloudXAvailable)
        {
            CloudXSdk.DestroyInterstitial(_cloudXAdUnitId);
        }

        DestroyAdMobAd();
    }

    /*
     * CloudX side
     */

    private void CloudXOnLoadSuccess(CloudXAd ad)
    {
        if (ad.AdUnitId != _cloudXAdUnitId)
        {
            return;
        }

        _isLoadingCloudX = false;
        AdLoaded?.Invoke(FirstLookSource.CloudX);
    }

    private void CloudXOnLoadFailed(string adUnitId, CloudXError _)
    {
        if (adUnitId != _cloudXAdUnitId)
        {
            return;
        }

        /* The one place the fallback is triggered: CloudX had its first look. */
        _isLoadingCloudX = false;
        LoadAdMobFallback();
    }

    private void CloudXOnShowSuccess(CloudXAd ad)
    {
        if (ad.AdUnitId == _cloudXAdUnitId)
        {
            AdShown?.Invoke(FirstLookSource.CloudX);
        }
    }

    private void CloudXOnShowFailed(CloudXAd ad, CloudXError error)
    {
        if (ad.AdUnitId != _cloudXAdUnitId)
        {
            return;
        }

        /* A CloudX ad that fails to show still leaves the placement to fill. */
        if (!ShowAdMobFallback())
        {
            AdShowFailed?.Invoke(FirstLookSource.CloudX, error.Message);
        }
    }

    private void CloudXOnHidden(CloudXAd ad)
    {
        if (ad.AdUnitId == _cloudXAdUnitId)
        {
            AdClosed?.Invoke(FirstLookSource.CloudX);
        }
    }

    private void CloudXOnClicked(CloudXAd ad)
    {
        if (ad.AdUnitId == _cloudXAdUnitId)
        {
            AdClicked?.Invoke(FirstLookSource.CloudX);
        }
    }

    /*
     * AdMob side. Google Mobile Ads raises its callbacks off the Unity main
     * thread, so every body goes through ExecuteInUpdate: controller state and
     * the events subscribers use for UI then both stay on one thread, like the
     * CloudX callbacks. Each body checks _isDisposed first, because a callback
     * queued before Dispose still arrives afterwards.
     */

    private bool AdMobCanShow() => _adMobInterstitial != null && _adMobInterstitial.CanShowAd();

    private void LoadAdMobFallback()
    {
        if (_isDisposed || _isLoadingAdMob || AdMobCanShow())
        {
            return;
        }

        _isLoadingAdMob = true;
        DestroyAdMobAd();

        InterstitialAd.Load(
            _adMobAdUnitId,
            new AdRequest(),
            (ad, error) => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                _isLoadingAdMob = false;

                if (_isDisposed)
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

                _adMobInterstitial = ad;
                RegisterAdMobEvents(ad);
                AdLoaded?.Invoke(FirstLookSource.AdMob);
            }));
    }

    private bool ShowAdMobFallback()
    {
        if (!AdMobCanShow())
        {
            return false;
        }

        _adMobInterstitial.Show();
        return true;
    }

    private void RegisterAdMobEvents(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentOpened += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            if (!_isDisposed)
            {
                AdShown?.Invoke(FirstLookSource.AdMob);
            }
        });

        ad.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            DestroyAdMobAd();

            if (!_isDisposed)
            {
                AdClosed?.Invoke(FirstLookSource.AdMob);
            }
        });

        ad.OnAdFullScreenContentFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            DestroyAdMobAd();

            if (!_isDisposed)
            {
                AdShowFailed?.Invoke(FirstLookSource.AdMob, error.GetMessage());
            }
        });

        ad.OnAdClicked += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            if (!_isDisposed)
            {
                AdClicked?.Invoke(FirstLookSource.AdMob);
            }
        });
    }

    private void DestroyAdMobAd()
    {
        _adMobInterstitial?.Destroy();
        _adMobInterstitial = null;
    }
}
