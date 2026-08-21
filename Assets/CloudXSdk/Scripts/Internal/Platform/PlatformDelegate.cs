#nullable enable

using System;

namespace CloudX
{
internal interface PlatformDelegate
{
    // Events for banner ad lifecycle callbacks
    public event Action<CloudXAd> BannerAdLoadSuccess;
    public event Action<string, CloudXError> BannerAdLoadFailed;
    public event Action<CloudXAd> BannerAdClicked;
    public event Action<CloudXAd> BannerAdRevenuePaid;

    // Events for MREC ad lifecycle callbacks
    public event Action<CloudXAd> MrecAdLoadSuccess;
    public event Action<string, CloudXError> MrecAdLoadFailed;
    public event Action<CloudXAd> MrecAdClicked;
    public event Action<CloudXAd> MrecAdRevenuePaid;

    // Events for interstitial ad lifecycle callbacks
    public event Action<CloudXAd> InterstitialAdLoadSuccess;
    public event Action<string, CloudXError> InterstitialAdLoadFailed;
    public event Action<CloudXAd> InterstitialAdShowSuccess;
    public event Action<CloudXAd, CloudXError> InterstitialAdShowFailed;
    public event Action<CloudXAd> InterstitialAdHidden;
    public event Action<CloudXAd> InterstitialAdClicked;
    public event Action<CloudXAd> InterstitialAdRevenuePaid;

    // Events for app open ad lifecycle callbacks
    public event Action<CloudXAd> AppOpenAdLoadSuccess;
    public event Action<string, CloudXError> AppOpenAdLoadFailed;
    public event Action<CloudXAd> AppOpenAdShowSuccess;
    public event Action<CloudXAd, CloudXError> AppOpenAdShowFailed;
    public event Action<CloudXAd> AppOpenAdHidden;
    public event Action<CloudXAd> AppOpenAdClicked;
    public event Action<CloudXAd> AppOpenAdRevenuePaid;

    // Events for rewarded ad lifecycle callbacks
    public event Action<CloudXAd> RewardedAdLoadSuccess;
    public event Action<string, CloudXError> RewardedAdLoadFailed;
    public event Action<CloudXAd> RewardedAdShowSuccess;
    public event Action<CloudXAd, CloudXError> RewardedAdShowFailed;
    public event Action<CloudXAd> RewardedAdHidden;
    public event Action<CloudXAd> RewardedAdClicked;
    public event Action<CloudXAd, CloudXReward> RewardedAdRewarded;
    public event Action<CloudXAd> RewardedAdRevenuePaid;

    string GetVersion();
    void Initialize(
        string appKey,
        string pluginVersion,
        Action<CloudXSdkConfiguration> onSuccess,
        Action<CloudXError> onFailure
    );
    bool IsInitialized();
    void SetMinLogLevel(CloudXLogLevel level);
    void SetHasUserConsent(bool? hasUserConsent);
    void SetDoNotSell(bool? doNotSell);
    void SetHashedUserId(string hashedUserId);
    void SetUserKeyValue(string key, string value);
    void SetAppKeyValue(string key, string value);
    void ClearAllKeyValues();
    bool ReportRevenueData(string revenueDataJson);

    // Banner methods
    void CreateHorizontalBanner(string adUnitId, CloudXAdViewConfiguration.AdViewPosition position);
    void CreateVerticalBanner(string adUnitId, CloudXAdViewConfiguration.AdViewVerticalPosition verticalPosition);
    void ShowBanner(string adUnitId);
    void HideBanner(string adUnitId);
    void LoadBanner(string adUnitId);
    void StartBannerAutoRefresh(string adUnitId);
    void StopBannerAutoRefresh(string adUnitId);
    void SetBannerPlacement(string adUnitId, string? placement);
    void SetBannerCustomData(string adUnitId, string? customData);
    void SetBannerExtraParameter(string adUnitId, string key, object? value);
    void DestroyBanner(string adUnitId);

    // MREC methods
    void CreateMrec(string adUnitId, CloudXAdViewConfiguration.AdViewPosition position);
    void ShowMrec(string adUnitId);
    void HideMrec(string adUnitId);
    void LoadMrec(string adUnitId);
    void StartMrecAutoRefresh(string adUnitId);
    void StopMrecAutoRefresh(string adUnitId);
    void SetMrecPlacement(string adUnitId, string? placement);
    void SetMrecCustomData(string adUnitId, string? customData);
    void SetMrecExtraParameter(string adUnitId, string key, object? value);
    void DestroyMrec(string adUnitId);

    // Interstitial methods
    void LoadInterstitial(string adUnitId);
    void ShowInterstitial(string adUnitId, string? placement, string? customData);
    void SetInterstitialExtraParameter(string adUnitId, string key, object? value);
    bool IsInterstitialReady(string adUnitId);
    void DestroyInterstitial(string adUnitId);
    void DestroyAllInterstitials();

    // App Open methods
    void LoadAppOpen(string adUnitId);
    void ShowAppOpen(string adUnitId, string? placement, string? customData);
    void SetAppOpenExtraParameter(string adUnitId, string key, object? value);
    bool IsAppOpenReady(string adUnitId);
    void DestroyAppOpen(string adUnitId);
    void DestroyAllAppOpens();

    // Rewarded methods
    void LoadRewarded(string adUnitId);
    void ShowRewarded(string adUnitId, string? placement, string? customData);
    void SetRewardedExtraParameter(string adUnitId, string key, object? value);
    bool IsRewardedReady(string adUnitId);
    void DestroyRewarded(string adUnitId);
    void DestroyAllRewarded();

    // Visual debugging (iOS only - no-op on other platforms)
    void SetVisualDebuggingEnabled(bool enabled);
    bool IsVisualDebuggingEnabled();

    // Arbiter
    void Arbiter(string bidsJson, Action<CloudXArbiterResult> onCompleted);
}
}
