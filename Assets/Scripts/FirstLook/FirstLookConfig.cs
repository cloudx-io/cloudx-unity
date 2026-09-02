/*
 * Fallback-side configuration for the First Look demo. The CloudX ad unit ids
 * come from DemoConfig; these are Google's official AdMob TEST ad unit ids.
 * Replace them with your own AdMob ad units in a real integration.
 */
public static class FirstLookConfig
{
#if UNITY_IOS
    public const string AdMobInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";
    public const string AdMobRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";
    public const string AdMobBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
#else
    public const string AdMobInterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";
    public const string AdMobRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
    public const string AdMobBannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";
#endif

    /*
     * Google has no dedicated MREC test unit; its banner test unit returns a
     * test ad at whatever AdSize is requested, so it serves the 300x250 MREC too.
     */
    public const string AdMobMrecAdUnitId = AdMobBannerAdUnitId;

    /*
     * Flip to true to exercise the AdMob fallback path: CloudX is asked to fill
     * an unknown ad unit, fails to load, and the controllers fall back to AdMob.
     */
    public const bool ForceCloudXNoFill = false;

    private const string InvalidCloudXAdUnitId = "first-look-invalid-unit";

    public static string CloudXAdUnitOrInvalid(string realAdUnitId) =>
        ForceCloudXNoFill ? InvalidCloudXAdUnitId : realAdUnitId;
}
