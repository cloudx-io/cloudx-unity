using System;
using UnityEngine;

/*
 * The host half of the First Look banner contract, in one file.
 *
 * FirstLookBannerController decides which SDK fills a pass. It cannot decide
 * when the next pass starts, because it is a plain class with no clock - no
 * Update, no coroutine, no Invoke. That is what this MonoBehaviour adds, and
 * it is the whole of what a scene has to contribute:
 *
 *   1. Start the next pass on PassSpent, after a cooldown. Reloading
 *      immediately is a request loop, because the new fill renders into the
 *      visible view and spends the next pass at once.
 *   2. Cancel that pending pass on Hide, so a hidden slot stops requesting.
 *   3. Do not retry a failed load while the banner is hidden. Cancelling is
 *      not enough on its own: a load already out on the network when the
 *      player hides completes afterwards, long after CancelInvoke had
 *      anything to cancel. A fill is harmless - it is banked for the next
 *      Show. A failure is not, because its retry would start the requests
 *      up again.
 *
 * Copy this file together with FirstLookBannerController.cs and
 * FirstLookSource.cs. In your own project the ad unit ids would come from
 * wherever you keep them - a serialized field, your remote config - instead of
 * being passed to Begin by a demo screen.
 *
 * Background and the reasoning behind each rule:
 * https://docs.cloudx.io/en/unity/integrations/first-look
 */
public sealed class FirstLookBannerCycle : MonoBehaviour
{
    /*
     * How long a displayed banner stays up before the next First Look pass
     * starts. Displaying an ad spends the pass (see FirstLookBannerController),
     * and a fill into a visible view renders immediately, so reloading without
     * a cooldown would be a request loop. Treat it like a banner refresh
     * interval - 30s matches the usual default; anything very short both burns
     * requests and hurts CPM.
     */
    private const float PassCooldownSeconds = 30f;

    /*
     * Retry policy after a failed load: 2s, 4s, 8s ... capped, and reset once a
     * load succeeds. A fixed short delay turns sustained no-fill into a tight
     * request loop against the fallback network, which ad networks penalise.
     */
    private const float RetryBaseDelaySeconds = 2f;
    private const float RetryMaxDelaySeconds = 60f;

    public event Action<FirstLookSource> AdLoaded;
    public event Action<FirstLookSource, string> AdLoadFailed;
    public event Action<FirstLookSource> AdShown;
    public event Action<FirstLookSource> AdClicked;

    /* The banner left the screen because Toggle hid it. */
    public event Action AdHidden;

    /* Toggle wanted to show, but no source had an ad yet; a load is running. */
    public event Action ShowPending;

    private FirstLookBannerController _banner;
    private int _retries;

    /*
     * Whether the slot should hold an ad at all. IsShown is not enough: it is
     * also false during the preload before the first Toggle, when a retry is
     * still wanted.
     */
    private bool _wanted = true;

    public bool IsShown => _banner != null && _banner.IsShown;

    /*
     * Creates the controller and preloads one pass, so an ad is ready the first
     * time the player asks for one. Call once, after CloudX has answered -
     * initialized, failed, or past a timeout of your own - passing whether it
     * actually came up.
     *
     * Google Mobile Ads does not have to be ready. It queues loads issued while
     * it is still initializing, and the fallback is lazy in any case, so
     * waiting for it as well would only delay the first pass.
     */
    public void Begin(string cloudXAdUnitId, string adMobAdUnitId, bool cloudXAvailable)
    {
        if (_banner != null)
        {
            return;
        }

        _banner = new FirstLookBannerController(cloudXAdUnitId, adMobAdUnitId, cloudXAvailable);
        _banner.AdLoaded += OnAdLoaded;
        _banner.AdLoadFailed += OnAdLoadFailed;
        _banner.AdShown += source => AdShown?.Invoke(source);
        _banner.AdClicked += source => AdClicked?.Invoke(source);
        _banner.PassSpent += ScheduleNextPass;

        LoadBanner();
    }

    /* Shows the banner if it is hidden, hides it if it is up. */
    public void Toggle()
    {
        if (_banner == null)
        {
            return;
        }

        if (_banner.IsShown)
        {
            _wanted = false;
            _banner.Hide();
            /* Nothing on screen, so the pass cycle stops until the next show. */
            CancelInvoke(nameof(LoadBanner));
            AdHidden?.Invoke();
            return;
        }

        _wanted = true;

        /* AdShown fires once a source actually goes on screen. */
        if (!_banner.Show())
        {
            ShowPending?.Invoke();
            LoadBanner();
        }
    }

    private void OnAdLoaded(FirstLookSource source)
    {
        _retries = 0;
        AdLoaded?.Invoke(source);
    }

    private void OnAdLoadFailed(FirstLookSource source, string message)
    {
        AdLoadFailed?.Invoke(source, message);

        /*
         * Rule 3: a load that fails after the hide must not revive the slot.
         * FirstLookBannerController already drops the terminal failure of a
         * cancelled pass, so nothing reaches this in practice. It stays because
         * the rule is the host's to keep - drive that controller from your own
         * component and this is the line that keeps it true.
         */
        if (!_wanted)
        {
            return;
        }

        Invoke(nameof(LoadBanner), NextRetryDelay());
    }

    /*
     * Rule 1. Cancelling first collapses a pending backoff retry into this one -
     * both end up calling LoadBanner, and two pending invokes would arbitrate
     * the placement twice. Showing again after a hide raises PassSpent too,
     * which restarts the cooldown from that moment.
     */
    private void ScheduleNextPass()
    {
        CancelInvoke(nameof(LoadBanner));
        Invoke(nameof(LoadBanner), PassCooldownSeconds);
    }

    private float NextRetryDelay()
    {
        var delay = Mathf.Min(RetryBaseDelaySeconds * Mathf.Pow(2f, _retries), RetryMaxDelaySeconds);
        _retries++;
        return delay;
    }

    /* Named so Invoke(nameof(...)) can reach it. */
    private void LoadBanner()
    {
        _banner?.Load();
    }

    private void OnDestroy()
    {
        _banner?.Dispose();
        _banner = null;
    }
}
