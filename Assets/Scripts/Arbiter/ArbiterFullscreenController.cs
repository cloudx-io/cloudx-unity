using System;
using System.Collections.Generic;
using CloudX;

/*
 * Fullscreen Arbiter/TPA controllers (interstitial, rewarded): the
 * prepare-ahead rule from docs.cloudx.io -> Trusted Arbiter. Both networks load
 * in parallel; once both have settled (loaded or failed) the loaded ones are
 * submitted to CloudXSdk.Arbiter and the result is stored. Show() then shows the
 * stored winner immediately - no arbiter call and no network call on the show
 * path - and returns false when no winner is prepared, in which case the caller
 * carries on with the game. The cycle restarts (reload what is missing,
 * re-arbitrate) after the ad closes or fails to show.
 *
 * The CloudX callback handlers below filter by ad unit id and drive the shared
 * state; the concrete subclass only routes the right CloudXAdsCallbacks group
 * into them (SubscribeCloudXCallbacks) and supplies the format's SDK calls.
 */
public abstract class ArbiterFullscreenController : ArbiterAdController
{
    public event Action<CloudXArbiterPlatform> AdShown;
    public event Action<CloudXArbiterPlatform, string> AdShowFailed;
    public event Action<CloudXArbiterPlatform> AdClosed;

    private bool _cloudXSettled;
    private bool _adMobSettled;
    private CloudXArbiterResult _nextWinner;
    /* A show call was made and neither close nor show-failure has arrived yet. */
    private bool _isShowing;

    protected ArbiterFullscreenController(
        string cloudXAdUnitId,
        string adMobAdUnitId,
        bool cloudXAvailable)
        : base(cloudXAdUnitId, adMobAdUnitId, cloudXAvailable)
    {
        /* No CloudX leg to wait for. */
        _cloudXSettled = !cloudXAvailable;
    }

    /* The platform Show() would use right now; null when no winner is stored. */
    public CloudXArbiterPlatform? PreparedWinner =>
        _nextWinner == null || _nextWinner.Platform == CloudXArbiterPlatform.None
            ? null
            : _nextWinner.Platform;

    /* True between a show call and its close or show-failure callback. */
    public bool IsShowing => _isShowing;

    /*
     * True when nothing is loaded, loading, decided or showing, so only a fresh
     * Load() can move things forward. The screen's backoff retry keys off this:
     * while a candidate, a load, a round or a show is pending, the controller
     * gets there itself.
     */
    public bool NeedsRetry =>
        !IsDisposed && !ArbiterInFlight && !_isShowing && PreparedWinner == null
        && !IsLoadingCloudX && !IsLoadingAdMob && !CloudXHasBid && !AdMobCanShow();

    private bool CloudXHasBid => LoadedCloudXAd != null && CloudXIsReady();

    /*
     * Loads whichever network does not hold a fill. After one side failed, a
     * retry reloads only that side and re-arbitrates when it settles. While an
     * ad is showing nothing is loaded: the shown AdMob object must not be
     * destroyed under the user, and the close callback restarts the cycle.
     */
    public override void Load()
    {
        if (IsDisposed || ArbiterInFlight || _isShowing)
        {
            return;
        }

        if (CloudXAvailable && !CloudXHasBid && !IsLoadingCloudX)
        {
            _cloudXSettled = false;
            LoadedCloudXAd = null;
            IsLoadingCloudX = true;
            CloudXLoad();
        }

        if (!AdMobCanShow() && !IsLoadingAdMob)
        {
            _adMobSettled = false;
            IsLoadingAdMob = true;
            DestroyAdMobAd();
            AdMobLoad();
        }

        /*
         * Both sides may already hold a fill with no winner stored - after a
         * None result, or after a stale winner was dropped - in which case there
         * is nothing to load and the round has to be run again from here.
         */
        if (_nextWinner == null)
        {
            MaybePrepareWinner();
        }
    }

    /*
     * Shows the stored winner. A stale winner (its ad expired or was consumed)
     * is dropped and false is returned; the caller reloads. While a show is in
     * progress a second call is ignored and returns false; check IsShowing to
     * tell the two apart.
     */
    public bool Show()
    {
        if (IsDisposed || _isShowing || _nextWinner == null)
        {
            return false;
        }

        var winner = _nextWinner.Platform;
        _nextWinner = null;

        switch (winner)
        {
            case CloudXArbiterPlatform.CloudX when CloudXHasBid:
                _isShowing = true;
                CloudXShow();
                return true;
            case CloudXArbiterPlatform.CloudX:
                LoadedCloudXAd = null;
                return false;
            case CloudXArbiterPlatform.AdMob when AdMobCanShow():
                _isShowing = true;
                AdMobShow();
                return true;
            case CloudXArbiterPlatform.AdMob:
                DestroyAdMobAd();
                return false;
            default:
                return false;
        }
    }

    /*
     * Runs the arbiter once both networks have settled and stores the result.
     * With no loaded candidate there is nothing to arbitrate; the screen's
     * backoff retry calls Load() again.
     */
    private void MaybePrepareWinner()
    {
        if (IsDisposed || ArbiterInFlight || !_cloudXSettled || !_adMobSettled)
        {
            return;
        }

        var bids = new List<CloudXArbiterBid>();

        if (CloudXAvailable && CloudXHasBid)
        {
            bids.Add(new CloudXArbiterBid.CloudX(LoadedCloudXAd));
        }

        if (AdMobCanShow())
        {
            bids.Add(AdMobBid(AdMobAdSourceName()));
        }

        if (bids.Count == 0)
        {
            return;
        }

        RunArbiter(bids, result => _nextWinner = result);
    }

    protected void RaiseAdShown(CloudXArbiterPlatform platform) => AdShown?.Invoke(platform);

    /* Show-failure and close end the show; the flag clears before subscribers run so they may Load(). */
    protected void RaiseAdShowFailed(CloudXArbiterPlatform platform, string message)
    {
        _isShowing = false;
        AdShowFailed?.Invoke(platform, message);
    }

    protected void RaiseAdClosed(CloudXArbiterPlatform platform)
    {
        _isShowing = false;
        AdClosed?.Invoke(platform);
    }

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
        LoadedCloudXAd = ad;
        _cloudXSettled = true;
        RaiseAdLoaded(CloudXArbiterPlatform.CloudX);
        MaybePrepareWinner();
    }

    protected void CloudXOnLoadFailed(string adUnitId, CloudXError error)
    {
        if (adUnitId != CloudXAdUnitId)
        {
            return;
        }

        IsLoadingCloudX = false;
        LoadedCloudXAd = null;
        _cloudXSettled = true;
        RaiseAdLoadFailed(CloudXArbiterPlatform.CloudX, error.Message);
        MaybePrepareWinner();
    }

    protected void CloudXOnShowSuccess(CloudXAd ad)
    {
        if (ad.AdUnitId == CloudXAdUnitId)
        {
            RaiseAdShown(CloudXArbiterPlatform.CloudX);
        }
    }

    protected void CloudXOnShowFailed(CloudXAd ad, CloudXError error)
    {
        if (ad.AdUnitId != CloudXAdUnitId)
        {
            return;
        }

        LoadedCloudXAd = null;
        RaiseAdShowFailed(CloudXArbiterPlatform.CloudX, error.Message);
    }

    protected void CloudXOnHidden(CloudXAd ad)
    {
        if (ad.AdUnitId != CloudXAdUnitId)
        {
            return;
        }

        /* The fill is consumed; the next Load() requests a new one. */
        LoadedCloudXAd = null;
        RaiseAdClosed(CloudXArbiterPlatform.CloudX);
    }

    protected void CloudXOnClicked(CloudXAd ad)
    {
        if (ad.AdUnitId == CloudXAdUnitId)
        {
            RaiseAdClicked(CloudXArbiterPlatform.CloudX);
        }
    }

    /*
     * AdMob load results, reported by the concrete on the Unity main thread
     * after it stored (or destroyed) the ad object.
     */
    protected void OnAdMobLoaded()
    {
        IsLoadingAdMob = false;
        _adMobSettled = true;
        RaiseAdLoaded(CloudXArbiterPlatform.AdMob);
        MaybePrepareWinner();
    }

    protected void OnAdMobLoadFailed(string message)
    {
        IsLoadingAdMob = false;
        _adMobSettled = true;
        RaiseAdLoadFailed(CloudXArbiterPlatform.AdMob, message);
        MaybePrepareWinner();
    }

    /* Format-specific SDK calls. */
    protected abstract bool CloudXIsReady();
    protected abstract void CloudXLoad();
    protected abstract void CloudXShow();
    protected abstract bool AdMobCanShow();
    protected abstract void AdMobLoad();
    protected abstract void AdMobShow();
    protected abstract string AdMobAdSourceName();
}
