using System;
using CloudX;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

/*
 * First Look banner: CloudX gets the first chance to fill, AdMob loads lazily
 * as the fallback only after CloudX fails. Same rule as
 * FirstLookInterstitialController, but a banner stays on screen instead of
 * being shown once, which changes two things.
 *
 * This file is the whole flow, top to bottom, so it can be copied into an app
 * on its own (plus FirstLookSource.cs for the enum). Reading order: state, the
 * Load/Show/Hide entry points, the pass cycle, then each SDK's callbacks.
 *
 * 1. THE PASS CYCLE. A fullscreen ad is consumed by being shown, so the SDKs'
 *    own "is an ad ready" answers go false and the next Load() naturally starts
 *    at CloudX again. Inline ads have no such event - CloudX banners report
 *    only load and click, no show or close - so this controller tracks a loaded
 *    flag per source, and something has to clear them or the first fill wins the
 *    placement forever.
 *
 *    One pass = one ad opportunity: CloudX asked first, AdMob only if CloudX
 *    fails, winner displayed. Putting the winner on screen spends the pass,
 *    because a load into an already-visible view renders immediately - so "on
 *    screen" is the one moment this code can treat as "this fill has been
 *    used". ShowSource therefore clears both flags and raises PassSpent, and the
 *    host schedules the next Load() one cooldown later
 *    (FirstLookConfig.PassCooldownSeconds), which starts at CloudX again. An
 *    immediate reload would be a request loop, since the new fill would render
 *    and spend the pass at once.
 *
 *    The host owns the other half of that contract: it must cancel the pending
 *    pass when it calls Hide(), or a hidden slot keeps requesting. See
 *    FirstLookScreen.ToggleBanner and ScheduleNextPass.
 *
 * 2. AUTO-REFRESH STAYS OFF. CloudX banner auto-refresh is opt-out: showing a
 *    banner starts it automatically unless the ad unit was first passed to
 *    StopBannerAutoRefresh, which also gates LoadBanner. CloudXCreateAndLoad
 *    below therefore calls it before create and nothing here ever calls
 *    StartBannerAutoRefresh - the pass cycle owns reloading, so an SDK refresh
 *    timer would compete with it and could swap the ad out from under the First
 *    Look source decision. (GeneralScreen restarts refresh on focus; First Look
 *    deliberately does not.)
 *
 *    AdMob is the half this code cannot control: the Google Mobile Ads Unity
 *    plugin has no refresh API at all. Whether a BannerView refreshes is decided
 *    solely by the ad unit's Automatic refresh setting in the AdMob console, and
 *    publishers MUST set that to Disabled on every unit used as a First Look
 *    fallback. Google's test units do refresh, so this controller ignores a fill
 *    it did not ask for when counting passes - see OnAdMobLoaded.
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

    private void ShowIfWanted(FirstLookSource source, bool spendsPass)
    {
        if (!_wantShown)
        {
            return;
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
            return;
        }

        ShowSource(source, spendsPass);
    }

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
        ShowIfWanted(FirstLookSource.CloudX, spendsPass);
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
        ShowIfWanted(FirstLookSource.AdMob, spendsPass);
    }

    private void DestroyAdMobAd()
    {
        _adMobBanner?.Destroy();
        _adMobBanner = null;
    }
}
