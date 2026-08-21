#nullable enable

using System;
using System.Threading.Tasks;
using CloudX;

namespace CloudX.UnityPlayer
{
    internal class UnityPlayerDelegate : PlatformDelegate
    {
        // Mock events - these won't actually fire in editor
        public event Action<CloudXAd> BannerAdLoadSuccess;
        public event Action<string, CloudXError> BannerAdLoadFailed;
        public event Action<CloudXAd> BannerAdClicked;
        public event Action<CloudXAd> BannerAdRevenuePaid;

        public event Action<CloudXAd> MrecAdLoadSuccess;
        public event Action<string, CloudXError> MrecAdLoadFailed;
        public event Action<CloudXAd> MrecAdClicked;
        public event Action<CloudXAd> MrecAdRevenuePaid;

        public event Action<CloudXAd> InterstitialAdLoadSuccess;
        public event Action<string, CloudXError> InterstitialAdLoadFailed;
        public event Action<CloudXAd> InterstitialAdShowSuccess;
        public event Action<CloudXAd, CloudXError> InterstitialAdShowFailed;
        public event Action<CloudXAd> InterstitialAdHidden;
        public event Action<CloudXAd> InterstitialAdClicked;
        public event Action<CloudXAd> InterstitialAdRevenuePaid;

        public event Action<CloudXAd> AppOpenAdLoadSuccess;
        public event Action<string, CloudXError> AppOpenAdLoadFailed;
        public event Action<CloudXAd> AppOpenAdShowSuccess;
        public event Action<CloudXAd, CloudXError> AppOpenAdShowFailed;
        public event Action<CloudXAd> AppOpenAdHidden;
        public event Action<CloudXAd> AppOpenAdClicked;
        public event Action<CloudXAd> AppOpenAdRevenuePaid;

        public event Action<CloudXAd> RewardedAdLoadSuccess;
        public event Action<string, CloudXError> RewardedAdLoadFailed;
        public event Action<CloudXAd> RewardedAdShowSuccess;
        public event Action<CloudXAd, CloudXError> RewardedAdShowFailed;
        public event Action<CloudXAd> RewardedAdHidden;
        public event Action<CloudXAd> RewardedAdClicked;
        public event Action<CloudXAd, CloudXReward> RewardedAdRewarded;
        public event Action<CloudXAd> RewardedAdRevenuePaid;

        private bool _isInitialized = false;

        public string GetVersion()
        {
            return CloudXSdk.PluginVersion;
        }

        public void Initialize(
            string appKey,
            string pluginVersion,
            Action<CloudXSdkConfiguration> onSuccess,
            Action<CloudXError> onFailure
        )
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock initialization - appKey={appKey}, pluginVersion={pluginVersion}");
            _isInitialized = true;
            // Mock always returns success with delay to mimic async
            InvokeWithDelay(() => onSuccess(new CloudXSdkConfiguration()));
        }

        public bool IsInitialized()
        {
            return _isInitialized;
        }

        public void SetHashedUserId(string hashedUserId)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetHashedUserId: {hashedUserId}");
        }

        public void SetUserKeyValue(string key, string value)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetUserKeyValue: key={key}, value={value}");
        }

        public void SetAppKeyValue(string key, string value)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetAppKeyValue: key={key}, value={value}");
        }

        public void ClearAllKeyValues()
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock ClearAllKeyValues called");
        }

        public bool ReportRevenueData(string revenueDataJson)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock ReportRevenueData - returning false: {revenueDataJson}");
            return false;
        }

        public void SetMinLogLevel(CloudXLogLevel level)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetMinLogLevel: {level}");
        }

        public void SetHasUserConsent(bool? hasUserConsent)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetHasUserConsent: {hasUserConsent}");
        }

        public void SetDoNotSell(bool? doNotSell)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetDoNotSell: {doNotSell}");
        }

        public void CreateHorizontalBanner(string adUnitId, CloudXAdViewConfiguration.AdViewPosition position)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock CreateHorizontalBanner called");
        }

        public void CreateVerticalBanner(string adUnitId, CloudXAdViewConfiguration.AdViewVerticalPosition verticalPosition)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock CreateVerticalBanner called");
        }

        public void ShowBanner(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock ShowBanner called");
        }

        public void HideBanner(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock HideBanner called");
        }

        public void LoadBanner(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock LoadBanner called");
            InvokeWithDelay(() => BannerAdLoadFailed?.Invoke(adUnitId, MockError("Banner not supported in Editor")));
        }

        public void StartBannerAutoRefresh(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock StartBannerAutoRefresh called");
        }

        public void StopBannerAutoRefresh(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock StopBannerAutoRefresh called");
        }

        public void SetBannerPlacement(string adUnitId, string? placement)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetBannerPlacement called with placement: {placement}");
        }

        public void SetBannerCustomData(string adUnitId, string? customData)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetBannerCustomData called with customData: {customData}");
        }

        public void SetBannerExtraParameter(string adUnitId, string key, object? value)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetBannerExtraParameter called with key={key}, value={value}");
        }

        public void DestroyBanner(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock DestroyBanner called");
        }

        public void CreateMrec(string adUnitId, CloudXAdViewConfiguration.AdViewPosition position)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock CreateMrec called");
        }

        public void ShowMrec(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock ShowMrec called");
        }

        public void HideMrec(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock HideMrec called");
        }

        public void LoadMrec(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock LoadMrec called");
            InvokeWithDelay(() => MrecAdLoadFailed?.Invoke(adUnitId, MockError("MREC not supported in Editor")));
        }

        public void StartMrecAutoRefresh(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock StartMrecAutoRefresh called");
        }

        public void StopMrecAutoRefresh(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock StopMrecAutoRefresh called");
        }

        public void SetMrecPlacement(string adUnitId, string? placement)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetMrecPlacement called with placement: {placement}");
        }

        public void SetMrecCustomData(string adUnitId, string? customData)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetMrecCustomData called with customData: {customData}");
        }

        public void SetMrecExtraParameter(string adUnitId, string key, object? value)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetMrecExtraParameter called with key={key}, value={value}");
        }

        public void DestroyMrec(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock DestroyMrec called");
        }

        public void LoadInterstitial(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock LoadInterstitial called");
            InvokeWithDelay(() => InterstitialAdLoadFailed?.Invoke(adUnitId, MockError("Interstitial not supported in Editor")));
        }

        public void ShowInterstitial(string adUnitId, string? placement, string? customData)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock ShowInterstitial called");
            InvokeWithDelay(() => InterstitialAdShowFailed?.Invoke(MockAd(adUnitId, CloudXAdFormat.Interstitial), MockError("Interstitial not supported in Editor")));
        }

        public void SetInterstitialExtraParameter(string adUnitId, string key, object? value)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetInterstitialExtraParameter called with key={key}, value={value}");
        }

        public bool IsInterstitialReady(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock IsInterstitialReady - always returns false");
            return false;
        }

        public void DestroyInterstitial(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock DestroyInterstitial called");
        }

        public void DestroyAllInterstitials()
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock DestroyAllInterstitials called");
        }

        public void LoadAppOpen(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock LoadAppOpen called");
            InvokeWithDelay(() => AppOpenAdLoadFailed?.Invoke(adUnitId, MockError("App Open not supported in Editor")));
        }

        public void ShowAppOpen(string adUnitId, string? placement, string? customData)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock ShowAppOpen called");
            InvokeWithDelay(() => AppOpenAdShowFailed?.Invoke(MockAd(adUnitId, CloudXAdFormat.AppOpen), MockError("App Open not supported in Editor")));
        }

        public void SetAppOpenExtraParameter(string adUnitId, string key, object? value)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetAppOpenExtraParameter called with key={key}, value={value}");
        }

        public bool IsAppOpenReady(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock IsAppOpenReady - always returns false");
            return false;
        }

        public void DestroyAppOpen(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock DestroyAppOpen called");
        }

        public void DestroyAllAppOpens()
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock DestroyAllAppOpens called");
        }

        public void LoadRewarded(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock LoadRewarded called");
            InvokeWithDelay(() => RewardedAdLoadFailed?.Invoke(adUnitId, MockError("Rewarded not supported in Editor")));
        }

        public void ShowRewarded(string adUnitId, string? placement, string? customData)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock ShowRewarded called");
            InvokeWithDelay(() => RewardedAdShowFailed?.Invoke(MockAd(adUnitId, CloudXAdFormat.Rewarded), MockError("Rewarded not supported in Editor")));
        }

        public void SetRewardedExtraParameter(string adUnitId, string key, object? value)
        {
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetRewardedExtraParameter called with key={key}, value={value}");
        }

        public bool IsRewardedReady(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock IsRewardedReady - always returns false");
            return false;
        }

        public void DestroyRewarded(string adUnitId)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock DestroyRewarded called");
        }

        public void DestroyAllRewarded()
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock DestroyAllRewarded called");
        }

        public void Arbiter(string bidsJson, Action<CloudXArbiterResult> onCompleted)
        {
            CloudXSdk.Log.LogDebug(() => "[CloudXAds Unity] Mock Arbiter - returning NONE result");
            onCompleted(new CloudXArbiterResult(string.Empty, CloudXArbiterPlatform.None,
                CloudXArbiterPlatform.None.ToWireString(), null,
                new System.Collections.Generic.Dictionary<string, string>()));
        }

        private bool _visualDebuggingEnabled = false;

        public void SetVisualDebuggingEnabled(bool enabled)
        {
            _visualDebuggingEnabled = enabled;
            CloudXSdk.Log.LogDebug(() => $"[CloudXAds Unity] Mock SetVisualDebuggingEnabled: {enabled}");
        }

        public bool IsVisualDebuggingEnabled()
        {
            return _visualDebuggingEnabled;
        }

        private static CloudXError MockError(string message) =>
            new CloudXError("EDITOR_NOT_SUPPORTED", -1, message);

        private static CloudXAd MockAd(string adUnitId, CloudXAdFormat adFormat = CloudXAdFormat.Interstitial) =>
            new CloudXAd(adFormat, adUnitId, null, "MockNetwork", null, 0.0);

        private const int MockDelayMs = 100;

        private static async void InvokeWithDelay(Action action)
        {
            await Task.Delay(MockDelayMs);
            action();
        }
    }
}
