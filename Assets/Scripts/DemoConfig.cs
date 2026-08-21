/*
 * Demo dashboard IDs so this sample runs without a CloudX account.
 * In your game, replace these with the app key and ad unit IDs from your
 * CloudX dashboard. Use one app key per process.
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
#else
    public const string AppKey = "0qE4q2MoJzoOkFQQKAtkt";
    public const string BannerAdUnitId = "guDml31r4Ys6O6HroPJia";
    public const string MrecAdUnitId = "TL6HTNWj7kkRUcodwGKSY";
    public const string InterstitialAdUnitId = "PwIOPhOD0KMCB_aqz8c89";
    public const string AppOpenAdUnitId = "BI0Whd5_o8ZIxkdHBS7X_";
    public const string RewardedAdUnitId = "LZrqb2oz47LMG_TaaVtaR";
#endif
}
