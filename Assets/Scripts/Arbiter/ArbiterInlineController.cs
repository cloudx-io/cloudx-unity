using System;
using System.Collections.Generic;
using CloudX;
using GoogleMobileAds.Api;

/*
 * Inline Arbiter/TPA controllers (banner, MREC): the arbitrate-then-render rule
 * from docs.cloudx.io -> Trusted Arbiter -> Banner and MREC arbitration. Both
 * networks load in parallel and stay loaded; when both have settled the loaded
 * ones are submitted to CloudXSdk.Arbiter and the winner's view is shown from
 * the completion callback. The loser's view is never shown - it would render
 * and fire an impression for a bid the arbiter did not select - it just keeps
 * its fill for the next round.
 *
 * Auto-refresh is OFF on both sides and this class drives the cycle itself:
 *  1. The shown winner's impression marks its fill as consumed; the loser keeps
 *     its fill.
 *  2. Rounds repeat on RefreshIntervalSeconds (docs: 20-30 s). When the
 *     interval elapses the shown view is hidden, the consumed winner and any
 *     network without a fill are reloaded, and the round runs once those loads
 *     settle. Settled loads never start a round on their own once something is
 *     on screen, so there is exactly one arbiter call per interval and never
 *     one over a half-loaded candidate set.
 *  3. Only the new winner's view is shown; the other is hidden.
 *
 * One deliberate deviation from the docs' cycle, which reloads the winner the
 * moment its impression fires: both Unity plugins reload into the existing
 * view, so a reload of the view on screen replaced the visible creative within
 * a second of showing it and fired a second AdMob paid event (verified on
 * device) - an impression for an ad no round had selected. Here the shown
 * creative stays up for the whole interval, is hidden before its reload, and
 * the next round decides what appears; the cost is the load latency during
 * which the slot is empty.
 *
 * CloudX inline auto-refresh is opt-out: the concrete's CloudXCreateAndLoad calls
 * Stop*AutoRefresh before create and nothing here calls Start*AutoRefresh. AdMob
 * has no refresh API at all: the ad unit's Automatic refresh MUST be Disabled in
 * the AdMob console, or AdMob swaps the creative behind the arbiter's back.
 *
 * The refresh clock is fed by the owning MonoBehaviour through Tick(), so this
 * stays a plain class like the fullscreen controllers.
 */
public abstract class ArbiterInlineController : ArbiterAdController
{
    /*
     * The platform whose view is on screen after a round; None when the round
     * produced no winner and both views were hidden.
     */
    public event Action<CloudXArbiterPlatform> WinnerShown;

    private readonly float _refreshIntervalSeconds;

    private bool _cloudXCreated;
    private bool _cloudXLoaded;
    private bool _adMobCreated;
    private bool _adMobLoaded;
    private string _adMobAdSourceName;

    /* User intent from the toggle button; the clock runs while this is set. */
    private bool _wantShown;
    private CloudXArbiterPlatform? _shownPlatform;
    private float _refreshTimer;
    private int _roundId;
    private int _impressionHandledRound = -1;
    /* The shown winner's impression fired; its fill is reloaded when the interval elapses. */
    private bool _winnerConsumed;

    protected ArbiterInlineController(
        string cloudXAdUnitId,
        string adMobAdUnitId,
        bool cloudXAvailable,
        float refreshIntervalSeconds)
        : base(cloudXAdUnitId, adMobAdUnitId, cloudXAvailable)
    {
        _refreshIntervalSeconds = refreshIntervalSeconds;
    }

    public bool IsShown => _wantShown;

    /* What is actually on screen; null while nothing is. */
    public CloudXArbiterPlatform? ShownPlatform => _shownPlatform;

    /*
     * True when nothing is loaded, loading or on screen, so only a fresh Load()
     * can move things forward. The screen's backoff retry keys off this; in
     * every other state the cycle re-requests fills itself.
     */
    public bool NeedsRetry =>
        !IsDisposed && !ArbiterInFlight && _shownPlatform == null && !AnyLoaded && !AnyLoading;

    private bool AnyLoaded => _cloudXLoaded || _adMobLoaded;
    private bool AnyLoading => IsLoadingCloudX || IsLoadingAdMob;

    /*
     * Loads whichever network does not hold a fill. The first CloudX load comes
     * from create (Destroy -> Stop*AutoRefresh -> Create); later ones reuse the
     * view, which is allowed once refresh is stopped. AdMob's view is created
     * hidden once and reloaded in place.
     */
    public override void Load()
    {
        if (IsDisposed)
        {
            return;
        }

        if (CloudXAvailable && !_cloudXLoaded && !IsLoadingCloudX)
        {
            IsLoadingCloudX = true;

            if (_cloudXCreated)
            {
                CloudXLoad();
            }
            else
            {
                _cloudXCreated = true;
                CloudXCreateAndLoad();
            }
        }

        if (!_adMobLoaded && !IsLoadingAdMob)
        {
            IsLoadingAdMob = true;

            if (!_adMobCreated)
            {
                _adMobCreated = true;
                AdMobCreateHidden();
            }

            AdMobLoad();
        }
    }

    /*
     * Asks for the ad to be on screen. A round runs as soon as the candidates
     * have settled (immediately, if they already have) and its winner is shown
     * from the arbiter callback; WinnerShown reports which.
     */
    public void Show()
    {
        if (IsDisposed || _wantShown)
        {
            return;
        }

        _wantShown = true;
        _refreshTimer = 0f;

        if (!AnyLoaded && !AnyLoading)
        {
            Load();
        }

        TryRunRound();
    }

    /*
     * Hides both views and stops the clock. Fills are kept, so the next Show()
     * arbitrates over them without a reload. A round in flight is reported but
     * shows nothing.
     */
    public void Hide()
    {
        if (IsDisposed || !_wantShown)
        {
            return;
        }

        _wantShown = false;
        _refreshTimer = 0f;
        InvalidateInFlightArbiter();

        /* A consumed fill must not be re-shown later; the reload path hides the views itself. */
        if (!ReloadConsumedWinner())
        {
            HideBothViews();
        }

        _shownPlatform = null;
    }

    /* Advances the refresh clock; called every frame by the owner. */
    public void Tick(float deltaSeconds)
    {
        if (IsDisposed || !_wantShown)
        {
            return;
        }

        _refreshTimer += deltaSeconds;

        if (_refreshTimer >= _refreshIntervalSeconds)
        {
            /*
             * Interval elapsed: take the consumed winner down and request every
             * missing fill (docs step 3: re-request only the networks that did
             * not fill). Load() is a no-op for a side that is loaded or loading,
             * so a round follows as soon as anything outstanding settles.
             */
            ReloadConsumedWinner();
            Load();
            TryRunRound();
        }
    }

    /*
     * Drops the fill of the winner whose impression fired and hides its view, so
     * the reload cannot render a creative no round has selected. Runs when the
     * interval elapses (or the ad is hidden), never right after the impression
     * - see the class note. Nothing is on screen afterwards, so the next round
     * is due as soon as the loads settle. Returns whether there was a consumed
     * winner to take down.
     */
    private bool ReloadConsumedWinner()
    {
        if (!_winnerConsumed)
        {
            return false;
        }

        _winnerConsumed = false;

        if (_shownPlatform == CloudXArbiterPlatform.CloudX)
        {
            _cloudXLoaded = false;
        }
        else if (_shownPlatform == CloudXArbiterPlatform.AdMob)
        {
            _adMobLoaded = false;
        }

        _shownPlatform = null;
        HideBothViews();
        Load();
        return true;
    }

    /*
     * The one gate for arbitration: the ad is wanted, no round is in flight, no
     * load is outstanding, and a round is due - nothing is on screen yet, or the
     * interval has elapsed. Load settlement, the clock and Show() all end up
     * here, so whichever condition becomes true last starts the round, once.
     */
    private void TryRunRound()
    {
        if (IsDisposed || !_wantShown || ArbiterInFlight || AnyLoading)
        {
            return;
        }

        var due = _shownPlatform == null || _refreshTimer >= _refreshIntervalSeconds;
        if (!due)
        {
            return;
        }

        var bids = new List<CloudXArbiterBid>();

        if (CloudXAvailable && _cloudXLoaded)
        {
            bids.Add(new CloudXArbiterBid.CloudX(LoadedCloudXAd));
        }

        if (_adMobLoaded)
        {
            bids.Add(AdMobBid(_adMobAdSourceName));
        }

        if (bids.Count == 0)
        {
            /*
             * Nothing to arbitrate: both loads failed. The screen's backoff retry
             * owns the reload (NeedsRetry is true here).
             */
            return;
        }

        _refreshTimer = 0f;
        RunArbiter(bids, OnRoundDecided);
    }

    private void OnRoundDecided(CloudXArbiterResult result)
    {
        switch (result.Platform)
        {
            case CloudXArbiterPlatform.CloudX when _cloudXLoaded:
                AdMobHide();
                CloudXShow();
                ShowingWinner(CloudXArbiterPlatform.CloudX);
                break;
            case CloudXArbiterPlatform.AdMob when _adMobLoaded:
                if (CloudXAvailable)
                {
                    CloudXHide();
                }

                AdMobShow();
                ShowingWinner(CloudXArbiterPlatform.AdMob);
                break;
            default:
                /*
                 * None (nothing locally priceable during fallback) or a winner
                 * whose fill is gone: show nobody. The clock keeps running, so
                 * the next interval retries over the retained fills.
                 */
                _shownPlatform = null;
                HideBothViews();
                WinnerShown?.Invoke(CloudXArbiterPlatform.None);
                break;
        }
    }

    /*
     * Hide() invalidated a round while it was in flight and Show() may have been
     * tapped again since; that Show() found the arbiter busy, so the round it
     * asked for starts here.
     */
    protected override void OnArbiterResultInvalidated() => TryRunRound();

    private void ShowingWinner(CloudXArbiterPlatform platform)
    {
        _shownPlatform = platform;
        _roundId++;
        _winnerConsumed = false;
        WinnerShown?.Invoke(platform);
    }

    private void HideBothViews()
    {
        if (CloudXAvailable)
        {
            CloudXHide();
        }

        AdMobHide();
    }

    /*
     * The shown winner's impression: its fill is consumed and gets reloaded when
     * the interval elapses (the loser keeps its fill). Counted once per round.
     */
    private void OnImpression(CloudXArbiterPlatform platform)
    {
        if (IsDisposed || !_wantShown || platform != _shownPlatform || _impressionHandledRound == _roundId)
        {
            return;
        }

        _impressionHandledRound = _roundId;
        _winnerConsumed = true;
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
        LoadedCloudXAd = ad;
        RaiseAdLoaded(CloudXArbiterPlatform.CloudX);
        TryRunRound();
    }

    protected void CloudXOnLoadFailed(string adUnitId, CloudXError error)
    {
        if (adUnitId != CloudXAdUnitId)
        {
            return;
        }

        IsLoadingCloudX = false;
        _cloudXLoaded = false;
        RaiseAdLoadFailed(CloudXArbiterPlatform.CloudX, error.Message);
        /* The other network may still win a round on its own. */
        TryRunRound();
    }

    protected void CloudXOnClicked(CloudXAd ad)
    {
        if (ad.AdUnitId == CloudXAdUnitId)
        {
            RaiseAdClicked(CloudXArbiterPlatform.CloudX);
        }
    }

    protected void CloudXOnRevenuePaid(CloudXAd ad)
    {
        if (ad.AdUnitId == CloudXAdUnitId)
        {
            OnImpression(CloudXArbiterPlatform.CloudX);
        }
    }

    /*
     * AdMob results, reported by the concrete on the Unity main thread. They can
     * arrive after Dispose (the callback was already queued), so they check
     * IsDisposed first and never raise into a screen that is gone.
     */
    protected void OnAdMobLoaded()
    {
        IsLoadingAdMob = false;

        if (IsDisposed)
        {
            return;
        }

        _adMobAdSourceName = AdSourceName(AdMobResponseInfo());

        /* A load must never reveal the loser's view. */
        if (_shownPlatform != CloudXArbiterPlatform.AdMob)
        {
            AdMobHide();
        }

        _adMobLoaded = true;
        RaiseAdLoaded(CloudXArbiterPlatform.AdMob);
        TryRunRound();
    }

    protected void OnAdMobLoadFailed(string message)
    {
        IsLoadingAdMob = false;

        if (IsDisposed)
        {
            return;
        }

        _adMobLoaded = false;
        RaiseAdLoadFailed(CloudXArbiterPlatform.AdMob, message);
        TryRunRound();
    }

    protected void OnAdMobClicked()
    {
        if (!IsDisposed)
        {
            RaiseAdClicked(CloudXArbiterPlatform.AdMob);
        }
    }

    protected void OnAdMobImpression() => OnImpression(CloudXArbiterPlatform.AdMob);

    /*
     * Required AdMob paid-event forwarding, then the impression handling (which
     * is idempotent with OnAdMobImpression, in case only one of the two fires).
     */
    protected void OnAdMobPaid(AdValue adValue)
    {
        if (IsDisposed)
        {
            return;
        }

        ReportAdMobPaidEvent(adValue, AdFormatName, _adMobAdSourceName);
        OnImpression(CloudXArbiterPlatform.AdMob);
    }

    /* "banner" or "mrec", the AdFormat reported with AdMob revenue. */
    protected abstract string AdFormatName { get; }

    /*
     * Format-specific SDK calls. CloudXCreateAndLoad must Stop*AutoRefresh, set
     * placement/custom data, then create the view (which issues the first load)
     * - it must not Start*AutoRefresh. CloudXLoad reloads the existing view.
     * AdMobCreateHidden creates the BannerView, wires its events through
     * ExecuteInUpdate and hides it before any load.
     */
    protected abstract void CloudXCreateAndLoad();
    protected abstract void CloudXLoad();
    protected abstract void CloudXShow();
    protected abstract void CloudXHide();
    protected abstract void AdMobCreateHidden();
    protected abstract void AdMobLoad();
    protected abstract void AdMobShow();
    protected abstract void AdMobHide();
    protected abstract ResponseInfo AdMobResponseInfo();
}
