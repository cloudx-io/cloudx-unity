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
#else
    public const string AdMobInterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";
    public const string AdMobRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
#endif

    /*
     * Flip to true to exercise the AdMob fallback path: CloudX is asked to fill
     * an unknown ad unit, fails to load, and the controllers fall back to AdMob.
     */
    public const bool ForceCloudXNoFill = false;

    private const string InvalidCloudXAdUnitId = "first-look-invalid-unit";

    public static string CloudXAdUnitOrInvalid(string realAdUnitId) =>
        ForceCloudXNoFill ? InvalidCloudXAdUnitId : realAdUnitId;
}
