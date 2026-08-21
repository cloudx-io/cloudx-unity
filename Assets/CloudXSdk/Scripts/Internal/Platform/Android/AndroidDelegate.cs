//  https://stackoverflow.com/questions/55492214/the-annotation-for-nullable-reference-types-should-only-be-used-in-code-within-a

#nullable enable

using System;
using CloudX;
using UnityEngine;

namespace CloudX.Android
{
internal class AndroidDelegate : PlatformDelegate
{
    public AndroidDelegate(AndroidJavaClass jniBridgeClass)
    {
        _jniBridgeClass = jniBridgeClass;
    }

    private readonly AndroidJavaClass _jniBridgeClass;

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

    public string GetVersion()
    {
        return _jniBridgeClass.CallStatic<string>("getVersion");
    }

    public void SetHashedUserId(string hashedUserId)
    {
        CloudXSdk.Log.LogDebug(() => $"SetHashedUserId called with: {hashedUserId}");

        _jniBridgeClass.CallStatic("setHashedUserId", hashedUserId);
    }

    public void SetUserKeyValue(string key, string value)
    {
        CloudXSdk.Log.LogDebug(() => $"SetUserKeyValue called with key={key}, value={value}");

        _jniBridgeClass.CallStatic("setUserKeyValue", key, value);
    }

    public void SetAppKeyValue(string key, string value)
    {
        CloudXSdk.Log.LogDebug(() => $"SetAppKeyValue called with key={key}, value={value}");

        _jniBridgeClass.CallStatic("setAppKeyValue", key, value);
    }

    public void ClearAllKeyValues()
    {
        CloudXSdk.Log.LogDebug(() => $"ClearAllKeyValues called");

        _jniBridgeClass.CallStatic("clearAllKeyValues");
    }

    public bool ReportRevenueData(string revenueDataJson)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android reportRevenueData method");
        var result = _jniBridgeClass.CallStatic<bool>("reportRevenueData", revenueDataJson);
        CloudXSdk.Log.LogDebug(() => $"Android reportRevenueData method returned: {result}");
        return result;
    }

    public void SetMinLogLevel(CloudXLogLevel level)
    {
        CloudXSdk.Log.LogDebug(() => $"SetMinLogLevel called with: {level}");

        _jniBridgeClass.CallStatic("setMinLogLevel", level.ToString());
    }

    public void SetHasUserConsent(bool? hasUserConsent)
    {
        CloudXSdk.Log.LogDebug(() => $"SetHasUserConsent called with: {hasUserConsent}");

        _jniBridgeClass.CallStatic("setHasUserConsentState", ToPrivacyState(hasUserConsent));
    }

    public void SetDoNotSell(bool? doNotSell)
    {
        CloudXSdk.Log.LogDebug(() => $"SetDoNotSell called with: {doNotSell}");

        _jniBridgeClass.CallStatic("setDoNotSellState", ToPrivacyState(doNotSell));
    }

    public void Initialize(
        string appKey,
        string pluginVersion,
        Action<CloudXSdkConfiguration> onSuccess,
        Action<CloudXError> onFailure
    )
    {
        var callback = new InitializationListenerProxy(onSuccess, onFailure);

        _jniBridgeClass.CallStatic("initialize", appKey, pluginVersion, callback);
    }

    public bool IsInitialized()
    {
        return _jniBridgeClass.CallStatic<bool>("isInitialized");
    }

    // Banner methods
    public void CreateHorizontalBanner(string adUnitId, CloudXAdViewConfiguration.AdViewPosition position)
    {
        // Pass position enum name as string to Android
        var positionName = position.ToString();

        CloudXSdk.Log.LogDebug(() => $"About to call Android createHorizontalBanner method with position: {positionName}");
        CallCreateBanner("createHorizontalBanner", adUnitId, positionName);
        CloudXSdk.Log.LogDebug(() => $"Android createHorizontalBanner method called");
    }

    public void CreateVerticalBanner(string adUnitId, CloudXAdViewConfiguration.AdViewVerticalPosition verticalPosition)
    {
        // Pass vertical position enum name as string to Android
        var positionName = verticalPosition.ToString();

        CloudXSdk.Log.LogDebug(() => $"About to call Android createVerticalBanner method with verticalPosition: {positionName}");
        CallCreateBanner("createVerticalBanner", adUnitId, positionName);
        CloudXSdk.Log.LogDebug(() => $"Android createVerticalBanner method called");
    }

    private void CallCreateBanner(string methodName, string adUnitId, string positionArgument)
    {
        var callback = new BannerListenerProxy();
        var revenueCallback = new RevenueListenerProxy();

        // Wire up lifecycle events
        callback.AdLoaded += BannerAdLoadSuccess;
        callback.AdLoadFailed += BannerAdLoadFailed;
        callback.AdClicked += BannerAdClicked;

        // Wire up revenue event
        revenueCallback.AdRevenuePaid += BannerAdRevenuePaid;

        _jniBridgeClass.CallStatic(methodName, adUnitId, positionArgument, callback, revenueCallback);
    }

    public void ShowBanner(string adUnitId)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android showBanner method");
        _jniBridgeClass.CallStatic("showBanner", adUnitId);
        CloudXSdk.Log.LogDebug(() => $"Android showBanner method called");
    }

    public void HideBanner(string adUnitId)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android hideBanner method");
        _jniBridgeClass.CallStatic("hideBanner", adUnitId);
        CloudXSdk.Log.LogDebug(() => $"Android hideBanner method called");
    }

    public void LoadBanner(string adUnitId)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android loadBanner method");
        _jniBridgeClass.CallStatic("loadBanner", adUnitId);
        CloudXSdk.Log.LogDebug(() => $"Android loadBanner method called");
    }

    public void StartBannerAutoRefresh(string adUnitId)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android startBannerAutoRefresh method");
        _jniBridgeClass.CallStatic("startBannerAutoRefresh", adUnitId);
        CloudXSdk.Log.LogDebug(() => $"Android startBannerAutoRefresh method called");
    }

    public void StopBannerAutoRefresh(string adUnitId)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android stopBannerAutoRefresh method");
        _jniBridgeClass.CallStatic("stopBannerAutoRefresh", adUnitId);
        CloudXSdk.Log.LogDebug(() => $"Android stopBannerAutoRefresh method called");
    }

    public void SetBannerPlacement(string adUnitId, string? placement)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android setBannerPlacement method with placement: {placement}");
        _jniBridgeClass.CallStatic("setBannerPlacement", adUnitId, placement);
        CloudXSdk.Log.LogDebug(() => $"Android setBannerPlacement method called");
    }

    public void SetBannerCustomData(string adUnitId, string? customData)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android setBannerCustomData method with customData: {customData}");
        _jniBridgeClass.CallStatic("setBannerCustomData", adUnitId, customData);
        CloudXSdk.Log.LogDebug(() => $"Android setBannerCustomData method called");
    }

    public void SetBannerExtraParameter(string adUnitId, string key, object? value)
    {
        SetExtraParameterJson("setBannerExtraParameterJson", adUnitId, key, value);
    }

    /*
     * Serializes the value to an enveloped JSON string and forwards it to the JNI bridge's
     * ...Json entry point, which rebuilds a real Kotlin Map/List/scalar before calling native
     * setExtraParameter. A null value forwards null (clear the key). An unsupported value type
     * returns null from the codec (already logged) and is skipped so prior state is untouched.
     */
    private void SetExtraParameterJson(string jniMethod, string adUnitId, string key, object? value)
    {
        string? json = value == null ? null : ExtraParameterCodec.SerializeEnvelope(value);
        if (value != null && json == null) return;
        CloudXSdk.Log.LogDebug(() => $"About to call Android {jniMethod} with key: {key}, json: {json}");
        _jniBridgeClass.CallStatic(jniMethod, adUnitId, key, json);
        CloudXSdk.Log.LogDebug(() => $"Android {jniMethod} called");
    }

    public void DestroyBanner(string adUnitId)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android destroyBanner method");
        _jniBridgeClass.CallStatic("destroyBanner", adUnitId);
        CloudXSdk.Log.LogDebug(() => $"Android destroyBanner method called");
    }

    // MREC methods
    public void CreateMrec(string adUnitId, CloudXAdViewConfiguration.AdViewPosition position)
    {
        var callback = new BannerListenerProxy();
        var revenueCallback = new RevenueListenerProxy();

        callback.AdLoaded += MrecAdLoadSuccess;
        callback.AdLoadFailed += MrecAdLoadFailed;
        callback.AdClicked += MrecAdClicked;
        revenueCallback.AdRevenuePaid += MrecAdRevenuePaid;

        // Pass position enum name as string to Android (same as banner)
        var positionName = position.ToString();

        CloudXSdk.Log.LogDebug(() => $"About to call Android createMrec method with position: {positionName}");
        _jniBridgeClass.CallStatic("createMrec", adUnitId, positionName, callback, revenueCallback);
        CloudXSdk.Log.LogDebug(() => $"Android createMrec method called");
    }

    public void ShowMrec(string adUnitId)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android showMrec method");
        _jniBridgeClass.CallStatic("showMrec", adUnitId);
        CloudXSdk.Log.LogDebug(() => $"Android showMrec method called");
    }

    public void HideMrec(string adUnitId)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android hideMrec method");
        _jniBridgeClass.CallStatic("hideMrec", adUnitId);
        CloudXSdk.Log.LogDebug(() => $"Android hideMrec method called");
    }

    public void LoadMrec(string adUnitId)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android loadMrec method");
        _jniBridgeClass.CallStatic("loadMrec", adUnitId);
        CloudXSdk.Log.LogDebug(() => $"Android loadMrec method called");
    }

    public void StartMrecAutoRefresh(string adUnitId)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android startMrecAutoRefresh method");
        _jniBridgeClass.CallStatic("startMrecAutoRefresh", adUnitId);
        CloudXSdk.Log.LogDebug(() => $"Android startMrecAutoRefresh method called");
    }

    public void StopMrecAutoRefresh(string adUnitId)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android stopMrecAutoRefresh method");
        _jniBridgeClass.CallStatic("stopMrecAutoRefresh", adUnitId);
        CloudXSdk.Log.LogDebug(() => $"Android stopMrecAutoRefresh method called");
    }

    public void SetMrecPlacement(string adUnitId, string? placement)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android setMrecPlacement method with placement: {placement}");
        _jniBridgeClass.CallStatic("setMrecPlacement", adUnitId, placement);
        CloudXSdk.Log.LogDebug(() => $"Android setMrecPlacement method called");
    }

    public void SetMrecCustomData(string adUnitId, string? customData)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android setMrecCustomData method with customData: {customData}");
        _jniBridgeClass.CallStatic("setMrecCustomData", adUnitId, customData);
        CloudXSdk.Log.LogDebug(() => $"Android setMrecCustomData method called");
    }

    public void SetMrecExtraParameter(string adUnitId, string key, object? value)
    {
        SetExtraParameterJson("setMrecExtraParameterJson", adUnitId, key, value);
    }

    public void DestroyMrec(string adUnitId)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android destroyMrec method");
        _jniBridgeClass.CallStatic("destroyMrec", adUnitId);
        CloudXSdk.Log.LogDebug(() => $"Android destroyMrec method called");
    }

    // Interstitial methods
    public void LoadInterstitial(string adUnitId)
    {
        var callback = new InterstitialListenerProxy();
        var revenueCallback = new RevenueListenerProxy();

        // Wire up all lifecycle events
        callback.AdLoaded += InterstitialAdLoadSuccess;
        callback.AdLoadFailed += InterstitialAdLoadFailed;
        callback.AdDisplayed += InterstitialAdShowSuccess;
        callback.AdDisplayFailed += InterstitialAdShowFailed;
        callback.AdHidden += InterstitialAdHidden;
        callback.AdClicked += InterstitialAdClicked;

        // Wire up revenue event
        revenueCallback.AdRevenuePaid += InterstitialAdRevenuePaid;

        CloudXSdk.Log.LogDebug(() => $"About to call Android loadInterstitial method");
        _jniBridgeClass.CallStatic("loadInterstitial", adUnitId, callback, revenueCallback);
        CloudXSdk.Log.LogDebug(() => $"Android loadInterstitial method called");
    }

    public void ShowInterstitial(string adUnitId, string? placement, string? customData)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android showInterstitial method with placement: {placement}, customData: {customData}");
        _jniBridgeClass.CallStatic("showInterstitial", adUnitId, placement, customData);
        CloudXSdk.Log.LogDebug(() => $"Android showInterstitial method called");
    }

    public void SetInterstitialExtraParameter(string adUnitId, string key, object? value)
    {
        SetExtraParameterJson("setInterstitialExtraParameterJson", adUnitId, key, value);
    }

    public bool IsInterstitialReady(string adUnitId)
    {
        return _jniBridgeClass.CallStatic<bool>("isInterstitialReady", adUnitId);
    }

    public void DestroyInterstitial(string adUnitId)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android destroyInterstitial method");
        _jniBridgeClass.CallStatic("destroyInterstitial", adUnitId);
        CloudXSdk.Log.LogDebug(() => $"Android destroyInterstitial method called");
    }

    public void DestroyAllInterstitials()
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android destroyAllInterstitials method");
        _jniBridgeClass.CallStatic("destroyAllInterstitials");
        CloudXSdk.Log.LogDebug(() => $"Android destroyAllInterstitials method called");
    }

    // App Open methods
    public void LoadAppOpen(string adUnitId)
    {
        var callback = new AppOpenListenerProxy();
        var revenueCallback = new RevenueListenerProxy();

        callback.AdLoaded += AppOpenAdLoadSuccess;
        callback.AdLoadFailed += AppOpenAdLoadFailed;
        callback.AdDisplayed += AppOpenAdShowSuccess;
        callback.AdDisplayFailed += AppOpenAdShowFailed;
        callback.AdHidden += AppOpenAdHidden;
        callback.AdClicked += AppOpenAdClicked;

        revenueCallback.AdRevenuePaid += AppOpenAdRevenuePaid;

        CloudXSdk.Log.LogDebug(() => $"About to call Android loadAppOpen method");
        _jniBridgeClass.CallStatic("loadAppOpen", adUnitId, callback, revenueCallback);
        CloudXSdk.Log.LogDebug(() => $"Android loadAppOpen method called");
    }

    public void ShowAppOpen(string adUnitId, string? placement, string? customData)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android showAppOpen method with placement: {placement}, customData: {customData}");
        _jniBridgeClass.CallStatic("showAppOpen", adUnitId, placement, customData);
        CloudXSdk.Log.LogDebug(() => $"Android showAppOpen method called");
    }

    public void SetAppOpenExtraParameter(string adUnitId, string key, object? value)
    {
        SetExtraParameterJson("setAppOpenExtraParameterJson", adUnitId, key, value);
    }

    public bool IsAppOpenReady(string adUnitId)
    {
        return _jniBridgeClass.CallStatic<bool>("isAppOpenReady", adUnitId);
    }

    public void DestroyAppOpen(string adUnitId)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android destroyAppOpen method");
        _jniBridgeClass.CallStatic("destroyAppOpen", adUnitId);
        CloudXSdk.Log.LogDebug(() => $"Android destroyAppOpen method called");
    }

    public void DestroyAllAppOpens()
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android destroyAllAppOpens method");
        _jniBridgeClass.CallStatic("destroyAllAppOpens");
        CloudXSdk.Log.LogDebug(() => $"Android destroyAllAppOpens method called");
    }

    // Rewarded methods
    public void LoadRewarded(string adUnitId)
    {
        var callback = new RewardedListenerProxy();
        var revenueCallback = new RevenueListenerProxy();

        // Wire up all lifecycle events
        callback.AdLoaded += RewardedAdLoadSuccess;
        callback.AdLoadFailed += RewardedAdLoadFailed;
        callback.AdDisplayed += RewardedAdShowSuccess;
        callback.AdDisplayFailed += RewardedAdShowFailed;
        callback.AdHidden += RewardedAdHidden;
        callback.AdClicked += RewardedAdClicked;
        callback.AdRewarded += RewardedAdRewarded;

        // Wire up revenue event
        revenueCallback.AdRevenuePaid += RewardedAdRevenuePaid;

        CloudXSdk.Log.LogDebug(() => $"About to call Android loadRewarded method");
        _jniBridgeClass.CallStatic("loadRewarded", adUnitId, callback, revenueCallback);
        CloudXSdk.Log.LogDebug(() => $"Android loadRewarded method called");
    }

    public void ShowRewarded(string adUnitId, string? placement, string? customData)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android showRewarded method with placement: {placement}, customData: {customData}");
        _jniBridgeClass.CallStatic("showRewarded", adUnitId, placement, customData);
        CloudXSdk.Log.LogDebug(() => $"Android showRewarded method called");
    }

    public void SetRewardedExtraParameter(string adUnitId, string key, object? value)
    {
        SetExtraParameterJson("setRewardedExtraParameterJson", adUnitId, key, value);
    }

    public bool IsRewardedReady(string adUnitId)
    {
        return _jniBridgeClass.CallStatic<bool>("isRewardedReady", adUnitId);
    }

    public void DestroyRewarded(string adUnitId)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android destroyRewarded method");
        _jniBridgeClass.CallStatic("destroyRewarded", adUnitId);
        CloudXSdk.Log.LogDebug(() => $"Android destroyRewarded method called");
    }

    public void DestroyAllRewarded()
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android destroyAllRewarded method");
        _jniBridgeClass.CallStatic("destroyAllRewarded");
        CloudXSdk.Log.LogDebug(() => $"Android destroyAllRewarded method called");
    }

    // Arbiter
    public void Arbiter(string bidsJson, Action<CloudXArbiterResult> onCompleted)
    {
        CloudXSdk.Log.LogDebug(() => $"About to call Android arbiter method");
        var listener = new ArbiterListenerProxy(onCompleted);
        _jniBridgeClass.CallStatic("arbiter", bidsJson, listener);
        CloudXSdk.Log.LogDebug(() => $"Android arbiter method called");
    }

    // Visual Debugging - Not yet implemented on Android
    public void SetVisualDebuggingEnabled(bool enabled)
    {
        // No-op on Android - visual debugging not yet implemented
        CloudXSdk.Log.LogDebug(() => $"SetVisualDebuggingEnabled({enabled}) - Not available on Android");
    }

    public bool IsVisualDebuggingEnabled()
    {
        // Always returns false on Android - visual debugging not yet implemented
        return false;
    }

    private static int ToPrivacyState(bool? value)
    {
        if (!value.HasValue)
        {
            return -1;
        }

        return value.Value ? 1 : 0;
    }
}
}
