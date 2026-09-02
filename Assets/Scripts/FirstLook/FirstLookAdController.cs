using System;

/*
 * Shared base for the First Look controllers. Every format follows the same
 * rule - CloudX gets the first chance to fill, AdMob is the lazy fallback that
 * loads only after CloudX fails - so the CloudX/AdMob bookkeeping, the load
 * events common to all formats, and the dispose sequence live here once. The
 * two families split below: fullscreen (interstitial, rewarded) in
 * FirstLookFullscreenController, inline (banner, MREC) in
 * FirstLookInlineController. Each concrete format is then a thin subclass that
 * only supplies the format-specific SDK calls.
 *
 * A publisher copying this into an app takes this base plus the one family base
 * and the one concrete they need.
 */
public abstract class FirstLookAdController : IDisposable
{
    public event Action<FirstLookSource> AdLoaded;
    public event Action<FirstLookSource, string> AdLoadFailed;
    public event Action<FirstLookSource> AdClicked;

    protected string CloudXAdUnitId { get; }
    protected string AdMobAdUnitId { get; }

    /*
     * When CloudX initialization failed, its load callbacks may never fire, so
     * the controller skips the CloudX leg and goes straight to the fallback.
     */
    protected bool CloudXAvailable { get; }

    protected bool IsDisposed { get; private set; }
    protected bool IsLoadingCloudX { get; set; }
    protected bool IsLoadingAdMob { get; set; }

    protected FirstLookAdController(string cloudXAdUnitId, string adMobAdUnitId, bool cloudXAvailable)
    {
        CloudXAdUnitId = cloudXAdUnitId;
        AdMobAdUnitId = adMobAdUnitId;
        CloudXAvailable = cloudXAvailable;
    }

    /* The source a show right now would use; null when no ad is ready. */
    public abstract FirstLookSource? ReadySource { get; }

    public abstract void Load();

    protected void RaiseAdLoaded(FirstLookSource source) => AdLoaded?.Invoke(source);
    protected void RaiseAdLoadFailed(FirstLookSource source, string message) => AdLoadFailed?.Invoke(source, message);
    protected void RaiseAdClicked(FirstLookSource source) => AdClicked?.Invoke(source);

    /*
     * Subscribe/unsubscribe the CloudX callbacks. Called from the concrete
     * constructor (not here) so the subclass is fully constructed first.
     */
    protected abstract void SubscribeCloudXCallbacks();
    protected abstract void UnsubscribeCloudXCallbacks();
    protected abstract void DestroyCloudXAd();
    protected abstract void DestroyAdMobAd();

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;

        UnsubscribeCloudXCallbacks();

        if (CloudXAvailable)
        {
            DestroyCloudXAd();
        }

        DestroyAdMobAd();
    }
}
