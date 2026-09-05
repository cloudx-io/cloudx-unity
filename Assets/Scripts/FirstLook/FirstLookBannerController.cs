using System;
using CloudX;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

/*
 * First Look banner: CloudX gets the first chance to fill, AdMob loads lazily
 * as the fallback only after CloudX fails. Copy this file and FirstLookSource.cs
 * into your project; it is the whole flow, top to bottom, with no base class to
 * bring along. Reading order: state, the Load/Show/Hide entry points, the pass
 * cycle, then each SDK's callbacks.
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
 * Two things the host has to do, or the cycle stalls:
 *
 *   1. Start the next pass on PassSpent, after a cooldown of your choosing.
 *      Reloading immediately is a request loop, because the new fill renders
 *      into the visible view and spends the next pass at once.
 *   2. Cancel that pending pass when it calls Hide(), or a hidden slot keeps
 *      requesting. Show() starts the cycle again.
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

    /* Returns whether the fill went on screen. */
    private bool ShowIfWanted(FirstLookSource source, bool spendsPass)
    {
        if (!_wantShown)
        {
            return false;
        }

        /*
         * A fill from a pass legitimately replaces the ad the previous pass put
         * up, so there is no _isShown check. A fill that is not part of a pass
         * is different: letting AdMob's own refresh take the slot from CloudX
         * would undo the source decision this pass made, so it only re-shows the
         * source that is already up.
         */
        if (!spendsPass && _shownSource != null && _shownSource != source)
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
    private static bool KeepsUnspentFill(bool spendsPass, bool wentOnScreen) =>
        spendsPass && !wentOnScreen;

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
        var spendsPass = _isLoadingCloudX;

        _isLoadingCloudX = false;
        _cloudXLoaded = true;
        AdLoaded?.Invoke(FirstLookSource.CloudX);

        var wentOnScreen = ShowIfWanted(FirstLookSource.CloudX, spendsPass);
        _cloudXLoaded = KeepsUnspentFill(spendsPass, wentOnScreen);
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

            if (!_isDisposed)
            {
                AdLoadFailed?.Invoke(FirstLookSource.AdMob, error.GetMessage());
            }
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
        var spendsPass = _isLoadingAdMob;

        _isLoadingAdMob = false;

        if (_isDisposed)
        {
            DestroyAdMobAd();
            return;
        }

        _adMobLoaded = true;
        AdLoaded?.Invoke(FirstLookSource.AdMob);

        var wentOnScreen = ShowIfWanted(FirstLookSource.AdMob, spendsPass);
        _adMobLoaded = KeepsUnspentFill(spendsPass, wentOnScreen);
    }

    private void DestroyAdMobAd()
    {
        _adMobBanner?.Destroy();
        _adMobBanner = null;
    }
}
