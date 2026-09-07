using System;
using CloudX;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

/*
 * First Look banner: CloudX gets the first chance to fill, AdMob loads lazily
 * as the fallback only after CloudX fails.
 *
 * This file decides which SDK fills a pass. It does not decide when the next
 * pass starts, because it is a plain class with no clock. Copy three files:
 * this one, FirstLookBannerCycle.cs for the timing, and FirstLookSource.cs for
 * the enum every event reports. Taking this file alone leaves nothing driving
 * the cycle, and the banner stops after its first pass.
 *
 * Reading order: state, the Load/Show/Hide entry points, the pass cycle, then
 * each SDK's callbacks.
 *
 * A banner is not the interstitial with different method names. A fullscreen ad
 * is consumed by being shown, so the SDKs' own readiness answers go false and
 * the next Load() starts at CloudX again. An inline ad is never consumed -
 * CloudX banners report load and click, with no show or close callback - so
 * this controller tracks a loaded flag per source and spends them when an ad
 * goes on screen. Without that, the first fill owns the placement until the
 * scene is destroyed and one CloudX no-fill hands the slot to the fallback for
 * the rest of the session.
 *
 * Three things the host has to do, or the cycle stalls or loops.
 * FirstLookBannerCycle does all three; they are written out here for anyone
 * driving this controller from their own component instead:
 *
 *   1. Start the next pass on PassSpent, after a cooldown of your choosing.
 *      Reloading immediately is a request loop, because the new fill renders
 *      into the visible view and spends the next pass at once.
 *   2. Cancel that pending pass when it calls Hide(), or a hidden slot keeps
 *      requesting. Show() starts the cycle again.
 *   3. Do not call Load() while the slot is hidden. Load() means the slot is
 *      wanted, so it lifts the cancellation below - including on a pass still
 *      out on the network - and a load asked for on a dismissed slot puts the
 *      requests back with nothing to stop them.
 *
 * Hide also ends the pass already running, and that part is this controller's
 * job rather than the host's: a CloudX load still in flight will not hand over
 * to the fallback, and a later Show does not revive it. Only the next Load
 * does - including one the in-flight guard drops, so a host tick that produces
 * no callback cannot leave the cycle with nothing left to schedule from.
 *
 * Set Automatic refresh to Disabled on the AdMob ad unit you use as the
 * fallback. The Google Mobile Ads Unity plugin has no refresh API, so that
 * console setting is the only thing controlling it, and a refreshing BannerView
 * swaps creatives outside this cycle.
 *
 * Background and the reasoning behind each rule:
 * https://docs.cloudx.io/en/unity/integrations/first-look
 */
public sealed class FirstLookBannerController : IDisposable
{
    private const CloudXAdViewConfiguration.AdViewPosition CloudXPosition =
        CloudXAdViewConfiguration.AdViewPosition.TopCenter;

    public event Action<FirstLookSource> AdLoaded;
    public event Action<FirstLookSource, string> AdLoadFailed;
    public event Action<FirstLookSource> AdShown;
    public event Action<FirstLookSource> AdClicked;

    /*
     * Raised when a display spent a First Look pass, i.e. it showed a fill this
     * controller asked for. The host uses it to time the next pass. It is
     * deliberately not raised for an ad AdMob refreshed on its own schedule:
     * that is outside the cycle, and letting it re-arm the cooldown would push
     * CloudX's next first look back every time - forever, if AdMob's refresh
     * interval is shorter than the cooldown.
     */
    public event Action PassSpent;

    private readonly string _cloudXAdUnitId;
    private readonly string _adMobAdUnitId;

    /*
     * When CloudX initialization failed, its load callbacks may never fire, so
     * the controller skips the CloudX leg and goes straight to the fallback.
     */
    private readonly bool _cloudXAvailable;

    private BannerView _adMobBanner;

    /* An unspent fill, per source. Cleared when one goes on screen. */
    private bool _cloudXLoaded;
    private bool _adMobLoaded;

    /* Whether each native view exists yet: first pass creates, later ones reload. */
    private bool _cloudXCreated;
    private bool _adMobCreated;

    private bool _isLoadingCloudX;
    private bool _isLoadingAdMob;
    private bool _wantShown;
    private bool _isShown;

    /*
     * Whether the pass currently in flight was cancelled by a Hide. It tracks
     * the pass, not the slot: clearing it on Show would revive a pass the
     * player just cancelled, so only Load clears it. A hide-then-quick-show
     * otherwise lets the old request's terminal callback land after the show
     * and start the fallback at once, skipping the cooldown that show just
     * restarted.
     *
     * _wantShown cannot do this job either, because it is also false during
     * the preload before the first Show, and the preload has to be allowed to
     * reach the fallback.
     */
    private bool _passCancelled;
    private bool _isDisposed;

    /*
     * The source whose native view currently holds a creative. Unlike the loaded
     * flags it survives a show, so Hide() followed by Show() puts the same ad
     * back up instead of leaving the slot blank until the next pass fills.
     */
    private FirstLookSource? _shownSource;

    public FirstLookBannerController(
        string cloudXAdUnitId,
        string adMobAdUnitId,
        bool cloudXAvailable)
    {
        _cloudXAdUnitId = cloudXAdUnitId;
        _adMobAdUnitId = adMobAdUnitId;
        _cloudXAvailable = cloudXAvailable;

        CloudXAdsCallbacks.Banner.OnAdLoadSuccess += CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Banner.OnAdLoadFailed += CloudXOnLoadFailed;
        CloudXAdsCallbacks.Banner.OnAdClicked += CloudXOnClicked;
    }

    public bool IsShown => _isShown;

    /* The source of an unspent fill; null once the current pass was displayed. */
    public FirstLookSource? ReadySource
    {
        get
        {
            if (_isDisposed)
            {
                return null;
            }

            if (_cloudXAvailable && _cloudXLoaded)
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

    /*
     * Starts a pass by asking CloudX, or resumes the one already running. The
     * AdMob fallback is loaded only if this CloudX load fails, from
     * CloudXOnLoadFailed. The first CloudX load has to create the view; later
     * passes reload the existing one, which swaps the creative in place with no
     * gap under a visible banner.
     */
    public void Load()
    {
        if (_isDisposed)
        {
            return;
        }

        /*
         * Whatever a Hide cancelled is history. This has to happen before the
         * guard below rather than after it. A load is only ever asked for on a
         * slot that is wanted, so it supersedes the cancellation even when the
         * cancelled pass is still out on the network and the guard drops this
         * call: leaving the flag set there would suppress that pass's terminal
         * callback as well, and the host would get neither the PassSpent nor
         * the failure it needs to schedule anything after it.
         */
        _passCancelled = false;

        if (_isLoadingCloudX || _isLoadingAdMob || ReadySource != null)
        {
            return;
        }

        if (!_cloudXAvailable)
        {
            LoadAdMobFallback();
            return;
        }

        _isLoadingCloudX = true;

        if (_cloudXCreated)
        {
            /* Permitted because StopBannerAutoRefresh already ran for this unit. */
            CloudXSdk.LoadBanner(_cloudXAdUnitId);
            return;
        }

        _cloudXCreated = true;
        CloudXCreateAndLoad();
    }

    /*
     * Shows the ready source now, or remembers the intent so the next load to
     * complete shows itself. Returns whether an ad was on screen immediately.
     */
    public bool Show()
    {
        if (_isDisposed)
        {
            return false;
        }

        _wantShown = true;

        /*
         * An unspent fill wins; otherwise re-show whatever is already in a
         * native view (the Hide-then-Show case).
         */
        var source = ReadySource ?? _shownSource;
        if (source == null)
        {
            return false;
        }

        ShowSource(source.Value, spendsPass: true);
        return true;
    }

    public void Hide()
    {
        if (_isDisposed)
        {
            return;
        }

        _wantShown = false;
        _isShown = false;
        _passCancelled = true;

        HideCloudX();
        HideAdMob();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        CloudXAdsCallbacks.Banner.OnAdLoadSuccess -= CloudXOnLoadSuccess;
        CloudXAdsCallbacks.Banner.OnAdLoadFailed -= CloudXOnLoadFailed;
        CloudXAdsCallbacks.Banner.OnAdClicked -= CloudXOnClicked;

        /* Leave the CloudX SDK alone when its init failed. */
        if (_cloudXAvailable)
        {
            CloudXSdk.DestroyBanner(_cloudXAdUnitId);
        }

        DestroyAdMobAd();
    }

    /*
     * The pass cycle
     */

    private void ShowSource(FirstLookSource source, bool spendsPass)
    {
        if (source == FirstLookSource.CloudX)
        {
            CloudXSdk.ShowBanner(_cloudXAdUnitId);
            HideAdMob();
        }
        else
        {
            _adMobBanner.Show();
            HideCloudX();
        }

        _isShown = true;
        _shownSource = source;

        /*
         * The fill is on screen, so it is no longer available to show. Both
         * flags clear, not just the winner's: the loser's fill is from the pass
         * that just ended, and leaving it set would let it win the next Show()
         * without CloudX having been asked again.
         */
        _cloudXLoaded = false;
        _adMobLoaded = false;

        AdShown?.Invoke(source);

        if (spendsPass)
        {
            PassSpent?.Invoke();
        }
    }

    /*
     * Returns whether the fill went on screen. Two flags, because they answer
     * different questions: "ours" is whether this controller asked for the
     * load, and "spendsPass" is whether it should re-time the cycle. They part
     * company for a pass a Hide cancelled - the fill is still ours to bank and
     * show, but the cooldown belongs to whatever the host has scheduled since.
     */
    private bool ShowIfWanted(FirstLookSource source, bool ours, bool spendsPass)
    {
        if (!_wantShown)
        {
            return false;
        }

        /*
         * A fill we asked for legitimately replaces the ad the previous pass
         * put up, so there is no _isShown check. A fill nobody asked for is
         * different: letting AdMob's own refresh take the slot from CloudX
         * would undo the source decision this pass made, so it only re-shows the
         * source that is already up.
         */
        if (!ours && _shownSource != null && _shownSource != source)
        {
            return false;
        }

        ShowSource(source, spendsPass);
        return true;
    }

    /*
     * A fill from a pass may sit here unspent until the slot is shown - that is
     * what banks an ad for the first tap. A fill nobody asked for may not: if it
     * is not on screen it has to be forgotten, because ReadySource would
     * otherwise report it, Load() would skip the next pass, and CloudX would
     * never be asked again - the very latch this cycle exists to prevent.
     * Nothing is lost by forgetting it; the native view keeps the creative and
     * the next pass reloads that side anyway.
     */
    private static bool KeepsUnspentFill(bool ours, bool wentOnScreen) =>
        ours && !wentOnScreen;

    private void HideCloudX()
    {
        if (_cloudXAvailable && _cloudXCreated)
        {
            CloudXSdk.HideBanner(_cloudXAdUnitId);
        }
    }

    private void HideAdMob()
    {
        if (_adMobCreated)
        {
            _adMobBanner?.Hide();
        }
    }

    /*
     * CloudX side
     */

    private void CloudXCreateAndLoad()
    {
        CloudXSdk.DestroyBanner(_cloudXAdUnitId);

        /*
         * Required, not optional, and it must come before CreateBanner: the
         * native layer registers the ad unit as refresh-disabled even with no
         * view yet, then creates the view with refresh already off, so no timer
         * ever runs. (Destroy clears that registration, hence this order.)
         */
        CloudXSdk.StopBannerAutoRefresh(_cloudXAdUnitId);

        /*
         * Placement and custom data must be set before CreateBanner so they are
         * on the first request. CreateBanner also issues the first load, so the
         * OnAdLoadSuccess / OnAdLoadFailed callbacks that drive the source and
         * the fallback come from here - no separate LoadBanner call.
         *
         * Both strings are this demo's. Replace them with your own placement
         * name, and with whatever custom data you report - or drop the custom
         * data line if you report none.
         */
        CloudXSdk.SetBannerPlacement(_cloudXAdUnitId, "first_look_screen");
        CloudXSdk.SetBannerCustomData(_cloudXAdUnitId, "first_look_banner_data");
        CloudXSdk.CreateBanner(_cloudXAdUnitId, new CloudXAdViewConfiguration(CloudXPosition));
    }

    private void CloudXOnLoadSuccess(CloudXAd ad)
    {
        if (ad.AdUnitId != _cloudXAdUnitId)
        {
            return;
        }

        /*
         * Only a load this controller issued sets _isLoadingCloudX, so it tells
         * a pass result apart from an unsolicited reload. CloudX auto-refresh is
         * off, so in practice this is always true; the check keeps the two
         * sources reading the same way.
         */
        var ours = _isLoadingCloudX;

        /*
         * A fill for a pass a Hide cancelled is still worth banking and showing
         * - it is an ad we paid a request for - but it must not raise PassSpent
         * and reset the cooldown, which by now belongs to the show that came
         * after the hide.
         */
        var spendsPass = ours && !_passCancelled;

        _isLoadingCloudX = false;
        _cloudXLoaded = true;
        AdLoaded?.Invoke(FirstLookSource.CloudX);

        var wentOnScreen = ShowIfWanted(FirstLookSource.CloudX, ours, spendsPass);
        _cloudXLoaded = KeepsUnspentFill(ours, wentOnScreen);
    }

    private void CloudXOnLoadFailed(string adUnitId, CloudXError _)
    {
        if (adUnitId != _cloudXAdUnitId)
        {
            return;
        }

        _isLoadingCloudX = false;

        /*
         * A Hide cancelled this pass while the load was still running. Do not
         * hand over to the fallback: the request would land on a slot the
         * player dismissed, and if they have since shown it again, it would
         * also jump the cooldown that show restarted. The next pass begins at
         * CloudX, as every pass does.
         */
        if (_passCancelled)
        {
            return;
        }

        /* The one place the fallback is triggered: CloudX had its first look. */
        LoadAdMobFallback();
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

    private void LoadAdMobFallback()
    {
        if (_isDisposed || _isLoadingAdMob || _adMobLoaded)
        {
            return;
        }

        _isLoadingAdMob = true;

        if (_adMobCreated)
        {
            _adMobBanner.LoadAd(new AdRequest());
            return;
        }

        _adMobCreated = true;
        AdMobCreateAndLoad();
    }

    private void AdMobCreateAndLoad()
    {
        DestroyAdMobAd();

        _adMobBanner = new BannerView(_adMobAdUnitId, AdSize.Banner, AdPosition.Top);

        _adMobBanner.OnBannerAdLoaded += () => MobileAdsEventExecutor.ExecuteInUpdate(OnAdMobLoaded);

        _adMobBanner.OnBannerAdLoadFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            _isLoadingAdMob = false;

            /*
             * Same reason as the CloudX leg: a cancelled pass must not reach
             * the host, or its retry would run against a dismissed slot - or
             * jump the cooldown, if the player has shown the banner again.
             */
            if (_isDisposed || _passCancelled)
            {
                return;
            }

            AdLoadFailed?.Invoke(FirstLookSource.AdMob, error.GetMessage());
        });

        _adMobBanner.OnAdClicked += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
        {
            if (!_isDisposed)
            {
                AdClicked?.Invoke(FirstLookSource.AdMob);
            }
        });

        /* Created hidden; Show()/Hide() drive visibility. */
        _adMobBanner.Hide();
        _adMobBanner.LoadAd(new AdRequest());
    }

    private void OnAdMobLoaded()
    {
        /*
         * A load this controller issued sets _isLoadingAdMob first, so a fill
         * arriving without it is one the AdMob console's Automatic refresh
         * produced. It still goes on screen - AdMob has already rendered it -
         * but it does not count as a pass, so the pending pass keeps its
         * original schedule.
         */
        var ours = _isLoadingAdMob;

        /*
         * Same as the CloudX leg: a cancelled pass banks and shows, but does
         * not re-time the cycle.
         */
        var spendsPass = ours && !_passCancelled;

        _isLoadingAdMob = false;

        if (_isDisposed)
        {
            DestroyAdMobAd();
            return;
        }

        _adMobLoaded = true;
        AdLoaded?.Invoke(FirstLookSource.AdMob);

        var wentOnScreen = ShowIfWanted(FirstLookSource.AdMob, ours, spendsPass);
        _adMobLoaded = KeepsUnspentFill(ours, wentOnScreen);
    }

    private void DestroyAdMobAd()
    {
        _adMobBanner?.Destroy();
        _adMobBanner = null;
    }
}
