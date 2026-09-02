using System;
using CloudX;

/*
 * Inline First Look controllers (banner, MREC). Same rule as the fullscreen
 * ones - CloudX first, AdMob as the lazy fallback - but inline ads stay on
 * screen instead of being shown once, so this base exposes Show()/Hide() and
 * tracks which source is up. CloudX inline ads report only load and click (no
 * show/close callbacks), so readiness is tracked with a loaded flag per source.
 *
 * Auto-refresh is deliberately kept OFF. CloudX banner/MREC auto-refresh is
 * opt-out: showing an inline ad starts it automatically unless the ad unit was
 * first passed to Stop*AutoRefresh, which also gates Load*. The concrete's
 * CloudXCreateAndLoad therefore calls Stop*AutoRefresh before create, and
 * nothing here ever calls Start*AutoRefresh - so a background reload never
 * overrides the First Look source decision. (GeneralScreen restarts refresh on
 * focus; First Look intentionally does not.)
 *
 * AdMob is the half this code cannot handle: the Google Mobile Ads Unity plugin
 * has no refresh API at all. A BannerView loads once, and whether it refreshes
 * afterwards is decided solely by the ad unit's Automatic refresh setting in
 * the AdMob console. Publishers MUST set that to Disabled on every banner and
 * MREC unit used as a First Look fallback; otherwise AdMob swaps the creative
 * on its own schedule and silently replaces the ad that won the First Look
 * pass.
 */
public abstract class FirstLookInlineController : FirstLookAdController
{
    public event Action<FirstLookSource> AdShown;

    private bool _cloudXLoaded;
    private bool _adMobLoaded;
    private bool _wantShown;
    private bool _isShown;

    protected FirstLookInlineController(
        string cloudXAdUnitId,
        string adMobAdUnitId,
        bool cloudXAvailable)
        : base(cloudXAdUnitId, adMobAdUnitId, cloudXAvailable)
    {
    }

    public bool IsShown => _isShown;

    public override FirstLookSource? ReadySource
    {
        get
        {
            if (IsDisposed)
            {
                return null;
            }

            if (CloudXAvailable && _cloudXLoaded)
            {
                return FirstLookSource.CloudX;
            }

            if (_adMobLoaded)
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
        CloudXCreateAndLoad();
    }

    /*
     * Shows the ready source now, or remembers the intent so the next load to
     * complete shows itself. Returns whether an ad was on screen immediately.
     */
    public bool Show()
    {
        if (IsDisposed)
        {
            return false;
        }

        _wantShown = true;

        var source = ReadySource;
        if (source == null)
        {
            return false;
        }

        ShowSource(source.Value);
        return true;
    }

    public void Hide()
    {
        if (IsDisposed)
        {
            return;
        }

        _wantShown = false;
        _isShown = false;

        /* Like Dispose: leave the CloudX SDK alone when its init failed. */
        if (CloudXAvailable)
        {
            CloudXHide();
        }

        AdMobHide();
    }

    protected void LoadAdMobFallback()
    {
        if (IsDisposed || IsLoadingAdMob || _adMobLoaded)
        {
            return;
        }

        IsLoadingAdMob = true;
        AdMobCreateAndLoad();
    }

    private void ShowSource(FirstLookSource source)
    {
        if (source == FirstLookSource.CloudX)
        {
            CloudXShow();
        }
        else
        {
            AdMobShow();
        }

        _isShown = true;
        AdShown?.Invoke(source);
    }

    private void ShowIfWanted(FirstLookSource source)
    {
        if (_wantShown && !_isShown)
        {
            ShowSource(source);
        }
    }

    /*
     * CloudX callback handlers, shared by both inline formats. The concrete
     * subscribes the matching CloudXAdsCallbacks group to these.
     */
    protected void CloudXOnLoadSuccess(CloudXAd ad)
    {
        if (ad.AdUnitId != CloudXAdUnitId)
        {
            return;
        }

        IsLoadingCloudX = false;
        _cloudXLoaded = true;
        RaiseAdLoaded(FirstLookSource.CloudX);
        ShowIfWanted(FirstLookSource.CloudX);
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

    protected void CloudXOnClicked(CloudXAd ad)
    {
        if (ad.AdUnitId == CloudXAdUnitId)
        {
            RaiseAdClicked(FirstLookSource.CloudX);
        }
    }

    /*
     * AdMob results, reported by the concrete on the Unity main thread. These
     * can arrive after Dispose (the callback was already queued), so they check
     * IsDisposed first and never raise into a screen that is gone - the same
     * order the fullscreen AdMob load callbacks use.
     */
    protected void OnAdMobLoaded()
    {
        IsLoadingAdMob = false;

        if (IsDisposed)
        {
            DestroyAdMobAd();
            return;
        }

        _adMobLoaded = true;
        RaiseAdLoaded(FirstLookSource.AdMob);
        ShowIfWanted(FirstLookSource.AdMob);
    }

    protected void OnAdMobLoadFailed(string message)
    {
        IsLoadingAdMob = false;

        if (IsDisposed)
        {
            return;
        }

        RaiseAdLoadFailed(FirstLookSource.AdMob, message);
    }

    protected void OnAdMobClicked()
    {
        if (!IsDisposed)
        {
            RaiseAdClicked(FirstLookSource.AdMob);
        }
    }

    /*
     * Format-specific SDK calls. CloudXCreateAndLoad must Stop*AutoRefresh (see
     * the class note), set placement/custom data, then create the view - it must
     * not Start*AutoRefresh. Recreate cleanly so a retry after a failure does
     * not leave a stale native view.
     */
    protected abstract void CloudXCreateAndLoad();
    protected abstract void CloudXShow();
    protected abstract void CloudXHide();
    protected abstract void AdMobCreateAndLoad();
    protected abstract void AdMobShow();
    protected abstract void AdMobHide();
}
