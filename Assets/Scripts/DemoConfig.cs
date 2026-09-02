/*
 * Demo dashboard IDs so this sample runs without a CloudX account.
 * In your game, replace these with the app key and ad unit IDs from your
 * CloudX dashboard. Use one app key per process.
 *
 * The AdMob ids are Google's official TEST ad units, shared by the First Look
 * and Arbiter/TPA demos. Replace them with your own AdMob ad units in a real
 * integration, and set Automatic refresh to Disabled on the banner and MREC
 * units in the AdMob console: the Unity plugin cannot control it, and a
 * refreshing AdMob banner would replace the ad the demo flow decided to show.
 */
public static class DemoConfig
{
#if UNITY_IOS
    public const string AppKey = "CmuKsWum6hx3yZK5SY_V_";
    public const string BannerAdUnitId = "8H3K7_7aSdkNHgYHe10aB";
    public const string MrecAdUnitId = "6V_LoFhGlpRxQW-6gf9Cy";
    public const string InterstitialAdUnitId = "9SizbPM3Dctz71WM2BKpi";
    public const string AppOpenAdUnitId = "3evNMg9P4E1pgRPyAYk9O";
    public const string RewardedAdUnitId = "7T2i4VWjsG2I4PM5vircU";

    public const string AdMobInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";
    public const string AdMobRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";
    public const string AdMobBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
#else
    public const string AppKey = "0qE4q2MoJzoOkFQQKAtkt";
    public const string BannerAdUnitId = "guDml31r4Ys6O6HroPJia";
    public const string MrecAdUnitId = "TL6HTNWj7kkRUcodwGKSY";
    public const string InterstitialAdUnitId = "PwIOPhOD0KMCB_aqz8c89";
    public const string AppOpenAdUnitId = "BI0Whd5_o8ZIxkdHBS7X_";
    public const string RewardedAdUnitId = "LZrqb2oz47LMG_TaaVtaR";

    public const string AdMobInterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";
    public const string AdMobRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
    public const string AdMobBannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";
#endif

    /*
     * Google has no dedicated MREC test unit; its banner test unit returns a
     * test ad at whatever AdSize is requested, so it serves the 300x250 MREC too.
     */
    public const string AdMobMrecAdUnitId = AdMobBannerAdUnitId;
}
