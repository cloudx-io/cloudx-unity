/*
 * Fallback-side configuration for the First Look demo. The CloudX ad unit ids
 * come from DemoConfig; these are Google's official AdMob TEST ad unit ids.
 * Replace them with your own AdMob ad units in a real integration.
 *
 * When you do, set Automatic refresh to Disabled on the banner unit in the
 * AdMob console. The Unity plugin cannot control it, and a refreshing AdMob
 * banner would replace the ad that won the First Look pass.
 *
 * https://docs.cloudx.io/en/unity/integrations/first-look
 */
public static class FirstLookConfig
{
#if UNITY_IOS
    public const string AdMobInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";
    public const string AdMobBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
#else
    public const string AdMobInterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";
    public const string AdMobBannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";
#endif

    /*
     * How long a displayed banner stays up before the next First Look pass
     * starts. Displaying an ad spends the pass (see FirstLookBannerController),
     * and a fill into a visible view renders immediately, so reloading without
     * a cooldown would be a request loop. Treat it like a banner refresh
     * interval - 30s matches the usual default; anything very short both burns
     * requests and hurts CPM.
     */
    public const float PassCooldownSeconds = 30f;

    /*
     * Flip to true to exercise the AdMob fallback path: CloudX is asked to fill
     * an unknown ad unit, fails to load, and the controllers fall back to AdMob.
     */
    public const bool ForceCloudXNoFill = false;

    private const string InvalidCloudXAdUnitId = "first-look-invalid-unit";

    public static string CloudXAdUnitOrInvalid(string realAdUnitId) =>
        ForceCloudXNoFill ? InvalidCloudXAdUnitId : realAdUnitId;
}
