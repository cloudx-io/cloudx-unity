using System;
using CloudX;

/*
 * Fullscreen First Look controllers (interstitial, rewarded). CloudX gets the
 * first chance to fill; AdMob loads lazily as the fallback only after CloudX
 * fails to load. Show() shows CloudX if it is ready, otherwise AdMob, and
 * returns false when neither source has an ad - the caller just carries on with
 * the game. Mirrors docs.cloudx.io -> Integrations -> First Look.
 *
 * The CloudX callback handlers below filter by ad unit id and drive the shared
 * state; the concrete subclass only routes the right CloudXAdsCallbacks group
 * into them (SubscribeCloudXCallbacks) and supplies the format's SDK calls.
 */
public abstract class FirstLookFullscreenController : FirstLookAdController
{
    public event Action<FirstLookSource> AdShown;
    public event Action<FirstLookSource, string> AdShowFailed;
    public event Action<FirstLookSource> AdClosed;

    protected FirstLookFullscreenController(
        string cloudXAdUnitId,
        string adMobAdUnitId,
        bool cloudXAvailable)
        : base(cloudXAdUnitId, adMobAdUnitId, cloudXAvailable)
    {
    }

    public override FirstLookSource? ReadySource
    {
        get
        {
            if (IsDisposed)
            {
                return null;
            }

            if (CloudXAvailable && CloudXIsReady())
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

    public override void Load()
    {
        if (IsDisposed || IsLoadingCloudX || IsLoadingAdMob || ReadySource != null)
        {
            return;
        }

        if (!CloudXAvailable)
        {
            LoadAdMobFallback();
            return;
        }

        IsLoadingCloudX = true;
        CloudXLoad();
    }

    public bool Show()
    {
        if (IsDisposed)
        {
            return false;
        }

        if (CloudXAvailable && CloudXIsReady())
        {
            CloudXShow();
            return true;
        }

        return ShowAdMobFallback();
    }

    protected void LoadAdMobFallback()
    {
        if (IsDisposed || IsLoadingAdMob || AdMobCanShow())
        {
            return;
        }

        IsLoadingAdMob = true;
        DestroyAdMobAd();
        AdMobLoad();
    }

    protected bool ShowAdMobFallback()
    {
        if (!AdMobCanShow())
        {
            return false;
        }

        AdMobShow();
        return true;
    }

    protected void RaiseAdShown(FirstLookSource source) => AdShown?.Invoke(source);
    protected void RaiseAdShowFailed(FirstLookSource source, string message) => AdShowFailed?.Invoke(source, message);
    protected void RaiseAdClosed(FirstLookSource source) => AdClosed?.Invoke(source);

    /*
     * CloudX callback handlers, shared by both fullscreen formats. The concrete
     * subscribes the matching CloudXAdsCallbacks group to these.
     */
    protected void CloudXOnLoadSuccess(CloudXAd ad)
    {
        if (ad.AdUnitId != CloudXAdUnitId)
        {
            return;
        }

        IsLoadingCloudX = false;
        RaiseAdLoaded(FirstLookSource.CloudX);
    }

    protected void CloudXOnLoadFailed(string adUnitId, CloudXError _)
    {
        if (adUnitId != CloudXAdUnitId)
        {
            return;
        }

        IsLoadingCloudX = false;
        LoadAdMobFallback();
    }

    protected void CloudXOnShowSuccess(CloudXAd ad)
    {
        if (ad.AdUnitId == CloudXAdUnitId)
        {
            RaiseAdShown(FirstLookSource.CloudX);
        }
    }

    protected void CloudXOnShowFailed(CloudXAd ad, CloudXError error)
    {
        if (ad.AdUnitId != CloudXAdUnitId)
        {
            return;
        }

        if (!ShowAdMobFallback())
        {
            RaiseAdShowFailed(FirstLookSource.CloudX, error.Message);
        }
    }

    protected void CloudXOnHidden(CloudXAd ad)
    {
        if (ad.AdUnitId == CloudXAdUnitId)
        {
            RaiseAdClosed(FirstLookSource.CloudX);
        }
    }

    protected void CloudXOnClicked(CloudXAd ad)
    {
        if (ad.AdUnitId == CloudXAdUnitId)
        {
            RaiseAdClicked(FirstLookSource.CloudX);
        }
    }

    /* Format-specific SDK calls. */
    protected abstract bool CloudXIsReady();
    protected abstract void CloudXLoad();
    protected abstract void CloudXShow();
    protected abstract bool AdMobCanShow();
    protected abstract void AdMobLoad();
    protected abstract void AdMobShow();
}
