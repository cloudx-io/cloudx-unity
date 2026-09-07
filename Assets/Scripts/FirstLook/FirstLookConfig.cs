/*
 * The AdMob ad unit ids the First Look demo falls back to. The CloudX ad unit
 * ids come from DemoConfig; these are Google's official AdMob TEST ad unit ids.
 * Replace them with your own AdMob ad units in a real integration.
 *
 * When you do, set Automatic refresh to Disabled on the banner unit in the
 * AdMob console. The Unity plugin cannot control it, and a refreshing AdMob
 * banner would replace the ad that won the First Look pass.
 *
 * The banner pass cooldown is not here; it lives in FirstLookBannerCycle,
 * which paces it.
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
}
