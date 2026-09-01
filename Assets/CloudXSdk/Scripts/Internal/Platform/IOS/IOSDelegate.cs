//
//  IOSDelegate.cs
//  CloudX Unity Plugin - iOS Platform Delegate
//
//  Copyright (c) 2024 CloudX. All rights reserved.
//
//  https://stackoverflow.com/questions/55492214/the-annotation-for-nullable-reference-types-should-only-be-used-in-code-within-a

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;
using CloudX;
using CloudX.Internal.Threading;

namespace CloudX.IOS
{
    /// <summary>
    /// iOS platform delegate that bridges C# to native Objective-C via P/Invoke.
    /// </summary>
    internal class IOSDelegate : PlatformDelegate
    {
        // Callback delegate type matching native CLXUnityBackgroundCallback
        private delegate void CLXUnityBackgroundCallback(string args);

        // Initialization callbacks
        private static Action<CloudXSdkConfiguration>? _initializationOnSuccess;
        private static Action<CloudXError>? _initializationOnFailure;

        /*
         * Routes async arbiter completions back to per-call C# callbacks.
         *
         * iOS funnels every native callback through one static BackgroundCallback(), so
         * concurrent arbiter calls would otherwise collide. The native side echoes a
         * caller-generated GUID (callId) in the OnArbiterCompletedEvent envelope; we
         * remove + invoke the matching Action here.
         *
         * Invariant: the native side fires exactly one completion per call on every
         * branch (success, parse failure, empty bid list, zero parsed bids). If that
         * invariant ever breaks the pending Action leaks — see runArbiterWithCallId:
         * in CLXUnityAdManager.m.
         */
        private static readonly ConcurrentDictionary<string, Action<CloudXArbiterResult>> _arbiterPending = new();
        
        // Events for banner ad lifecycle callbacks
        public event Action<CloudXAd>? BannerAdLoadSuccess;
        public event Action<string, CloudXError>? BannerAdLoadFailed;
        public event Action<CloudXAd>? BannerAdClicked;
        public event Action<CloudXAd>? BannerAdRevenuePaid;

        // Events for MREC ad lifecycle callbacks
        public event Action<CloudXAd>? MrecAdLoadSuccess;
        public event Action<string, CloudXError>? MrecAdLoadFailed;
        public event Action<CloudXAd>? MrecAdClicked;
        public event Action<CloudXAd>? MrecAdRevenuePaid;

        // Events for interstitial ad lifecycle callbacks
        public event Action<CloudXAd>? InterstitialAdLoadSuccess;
        public event Action<string, CloudXError>? InterstitialAdLoadFailed;
        public event Action<CloudXAd>? InterstitialAdShowSuccess;
        public event Action<CloudXAd, CloudXError>? InterstitialAdShowFailed;
        public event Action<CloudXAd>? InterstitialAdHidden;
        public event Action<CloudXAd>? InterstitialAdClicked;
        public event Action<CloudXAd>? InterstitialAdRevenuePaid;

        // Events for app open ad lifecycle callbacks
        public event Action<CloudXAd>? AppOpenAdLoadSuccess;
        public event Action<string, CloudXError>? AppOpenAdLoadFailed;
        public event Action<CloudXAd>? AppOpenAdShowSuccess;
        public event Action<CloudXAd, CloudXError>? AppOpenAdShowFailed;
        public event Action<CloudXAd>? AppOpenAdHidden;
        public event Action<CloudXAd>? AppOpenAdClicked;
        public event Action<CloudXAd>? AppOpenAdRevenuePaid;

        // Events for rewarded ad lifecycle callbacks
        public event Action<CloudXAd>? RewardedAdLoadSuccess;
        public event Action<string, CloudXError>? RewardedAdLoadFailed;
        public event Action<CloudXAd>? RewardedAdShowSuccess;
        public event Action<CloudXAd, CloudXError>? RewardedAdShowFailed;
        public event Action<CloudXAd>? RewardedAdHidden;
        public event Action<CloudXAd>? RewardedAdClicked;
        public event Action<CloudXAd, CloudXReward>? RewardedAdRewarded;
        public event Action<CloudXAd>? RewardedAdRevenuePaid;

        // Singleton instance for callback routing
        private static IOSDelegate? _instance;

#if UNITY_IOS && !UNITY_EDITOR

        #region Native Method Declarations

        [DllImport("__Internal")]
        private static extern void _CLXSetBackgroundCallback(CLXUnityBackgroundCallback callback);

        [DllImport("__Internal")]
        private static extern void _CLXInitialize(string appKey, string pluginVersion);

        [DllImport("__Internal")]
        private static extern bool _CLXIsInitialized();

        [DllImport("__Internal")]
        private static extern void _CLXSetHashedUserId(string hashedUserId);

        [DllImport("__Internal")]
        private static extern void _CLXSetUserKeyValue(string key, string value);

        [DllImport("__Internal")]
        private static extern void _CLXSetAppKeyValue(string key, string value);

        [DllImport("__Internal")]
        private static extern void _CLXClearAllKeyValues();

        [DllImport("__Internal")]
        private static extern bool _CLXReportRevenueData(string json);

        [DllImport("__Internal")]
        private static extern void _CLXSetMinLogLevel(string level);

        [DllImport("__Internal")]
        private static extern void _CLXSetHasUserConsent(int hasValue, int hasUserConsent);

        [DllImport("__Internal")]
        private static extern void _CLXSetDoNotSell(int hasValue, int doNotSell);

        // Banner
        [DllImport("__Internal")]
        private static extern void _CLXCreateBanner(string adUnitId, string position);

        [DllImport("__Internal")]
        private static extern void _CLXCreateVerticalBanner(string adUnitId, string verticalPosition);

        [DllImport("__Internal")]
        private static extern void _CLXShowBanner(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXHideBanner(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXLoadBanner(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXStartBannerAutoRefresh(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXStopBannerAutoRefresh(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXDestroyBanner(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXSetBannerPlacement(string adUnitId, string? placement);

        [DllImport("__Internal")]
        private static extern void _CLXSetBannerCustomData(string adUnitId, string? customData);

        [DllImport("__Internal")]
        private static extern void _CLXSetBannerExtraParameterJson(string adUnitId, string key, string? json);

        // MREC
        [DllImport("__Internal")]
        private static extern void _CLXCreateMrec(string adUnitId, string position);

        [DllImport("__Internal")]
        private static extern void _CLXShowMrec(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXHideMrec(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXLoadMrec(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXStartMrecAutoRefresh(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXStopMrecAutoRefresh(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXDestroyMrec(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXSetMrecPlacement(string adUnitId, string? placement);

        [DllImport("__Internal")]
        private static extern void _CLXSetMrecCustomData(string adUnitId, string? customData);

        [DllImport("__Internal")]
        private static extern void _CLXSetMrecExtraParameterJson(string adUnitId, string key, string? json);

        // Interstitial
        [DllImport("__Internal")]
        private static extern void _CLXLoadInterstitial(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXShowInterstitial(string adUnitId, string? placement, string? customData);

        [DllImport("__Internal")]
        private static extern void _CLXSetInterstitialExtraParameterJson(string adUnitId, string key, string? json);

        [DllImport("__Internal")]
        private static extern bool _CLXIsInterstitialReady(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXDestroyInterstitial(string adUnitId);

        // App Open
        [DllImport("__Internal")]
        private static extern void _CLXLoadAppOpen(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXShowAppOpen(string adUnitId, string? placement, string? customData);

        [DllImport("__Internal")]
        private static extern void _CLXSetAppOpenExtraParameterJson(string adUnitId, string key, string? json);

        [DllImport("__Internal")]
        private static extern bool _CLXIsAppOpenReady(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXDestroyAppOpen(string adUnitId);

        // Rewarded
        [DllImport("__Internal")]
        private static extern void _CLXLoadRewarded(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXShowRewarded(string adUnitId, string? placement, string? customData);

        [DllImport("__Internal")]
        private static extern void _CLXSetRewardedExtraParameterJson(string adUnitId, string key, string? json);

        [DllImport("__Internal")]
        private static extern bool _CLXIsRewardedReady(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _CLXDestroyRewarded(string adUnitId);

        // Utilities
        [DllImport("__Internal")]
        private static extern string _CLXGetSdkVersion();

        // Visual Debugging
        [DllImport("__Internal")]
        private static extern void _CLXSetVisualDebuggingEnabled(bool enabled);

        [DllImport("__Internal")]
        private static extern bool _CLXIsVisualDebuggingEnabled();

        // Arbiter
        [DllImport("__Internal")]
        private static extern void _CLXArbiter(string callId, string bidsJson);

        #endregion

#endif

        public IOSDelegate()
        {
#if UNITY_IOS && !UNITY_EDITOR
            _instance = this;

            // Register the callback with native code
            _CLXSetBackgroundCallback(BackgroundCallback);
            CloudXSdk.Log.LogDebug(() => "IOSDelegate initialized - background callback registered");
#else
            /*
             * Single enforcement point for "only iOS code runs in the iOS delegate".
             * CloudXSdk builds this delegate only for an iOS player, so constructing it
             * anywhere else means the platform selection was bypassed. Failing here
             * makes every off-iOS body in this file unreachable, which is why the void
             * methods can stay silent no-ops.
             */
            throw NotOnIOS("constructor");
#endif
        }

        #region Callback Handler

        /// <summary>
        /// Static callback invoked by native code on a serial background queue thread. Must be static
        /// with MonoPInvokeCallback. Parses JSON and routes to appropriate event handlers; everything it
        /// touches (statics, ConcurrentDictionary, UnityMainThreadDispatcher.Enqueue, Debug.Log) is thread-safe.
        /// </summary>
        [MonoPInvokeCallback(typeof(CLXUnityBackgroundCallback))]
        private static void BackgroundCallback(string propsStr)
        {
            if (string.IsNullOrEmpty(propsStr) || _instance == null)
            {
                return;
            }

            string? eventName = null;
            try
            {
                var props = Json.Parse(propsStr) as Dictionary<string, object>;
                if (props == null || !props.ContainsKey("name"))
                {
                    CloudXSdk.Log.LogError(() => $"Invalid callback JSON: {propsStr}");
                    return;
                }

                eventName = props["name"] as string;
                if (eventName == null)
                {
                    CloudXSdk.Log.LogError(() => $"Invalid callback JSON (name is not a string): {propsStr}");
                    return;
                }
                CloudXSdk.Log.LogDebug(() => $"iOS callback received: {eventName}");

                // Route to appropriate handler
                HandleEvent(eventName, props);
            }
            catch (Exception e)
            {
                // Publisher handler exceptions are caught closer to the handler (CallbackInvoker /
                // CallbackDispatcher); this guards JSON parsing and routing at the P/Invoke boundary.
                CloudXSdk.Log.LogError(() => $"Error handling iOS callback {eventName ?? "<unparsed>"}: {e.GetType().Name}: {e.Message}", e);
            }
        }

        /*
         * Events whose per-event default is the native callback thread when
         * CloudXSdk.InvokeEventsOnUnityMainThread is unset: revenue for the fullscreen formats
         * only. Names must match the HandleEvent switch cases verbatim; internal so tests can
         * assert the exact membership.
         */
        internal static readonly HashSet<string> BackgroundEventNames = new HashSet<string>
        {
            "OnInterstitialAdRevenuePaidEvent",
            "OnAppOpenAdRevenuePaidEvent",
            "OnRewardedAdRevenuePaidEvent",
        };

        private static void HandleEvent(string eventName, Dictionary<string, object> props)
        {
            // Extract common fields
            var adUnitId = GetString(props, "adUnitId");
            var ad = CreateCloudXAd(props);
            var error = CreateCloudXError(props);

            /*
             * Fullscreen revenue events default to the native callback thread: the Unity player is
             * paused or covered while the ad shows, so main-thread delivery would wait until the ad
             * closes. Banner/MREC revenue and every other event default to the Unity main thread.
             * CloudXSdk.InvokeEventsOnUnityMainThread overrides both. CLXUnityAdManager forwards
             * every event on a serial background queue, so this method runs off the Unity main
             * thread and must only touch thread-safe state; "inline" below means that background
             * thread. The set is an explicit name list (not a suffix match) so renaming an event
             * string cannot silently change its thread.
             */
            var keepInBackground = BackgroundEventNames.Contains(eventName);
            CallbackDispatcher.Dispatch(eventName, keepInBackground, () =>
            {
                switch (eventName)
                {
                    // SDK Initialization
                    case "OnSdkInitializedEvent":
                        var success = GetBool(props, "success");
                        if (success)
                        {
                            _initializationOnSuccess?.Invoke(new CloudXSdkConfiguration());
                        }
                        else
                        {
                            _initializationOnFailure?.Invoke(error ?? CreateDefaultError("Unknown initialization error"));
                        }
                        break;

                    // Banner Events
                    case "OnBannerAdLoadedEvent":
                        _instance?.BannerAdLoadSuccess?.Invoke(ad);
                        break;
                    case "OnBannerAdLoadFailedEvent":
                        _instance?.BannerAdLoadFailed?.Invoke(adUnitId ?? "", error ?? CreateDefaultError("Unknown error"));
                        break;
                    case "OnBannerAdClickedEvent":
                        _instance?.BannerAdClicked?.Invoke(ad);
                        break;
                    case "OnBannerAdRevenuePaidEvent":
                        _instance?.BannerAdRevenuePaid?.Invoke(ad);
                        break;
                    /*
                     * Expand/collapse are legitimate native events with no Unity API surface yet
                     * (Android's listener methods are equally empty); handled as no-ops so they do
                     * not trip the unhandled-event error below.
                     */
                    case "OnBannerAdExpandedEvent":
                    case "OnBannerAdCollapsedEvent":
                    case "OnMrecAdExpandedEvent":
                    case "OnMrecAdCollapsedEvent":
                        break;

                    // MREC Events
                    case "OnMrecAdLoadedEvent":
                        _instance?.MrecAdLoadSuccess?.Invoke(ad);
                        break;
                    case "OnMrecAdLoadFailedEvent":
                        _instance?.MrecAdLoadFailed?.Invoke(adUnitId ?? "", error ?? CreateDefaultError("Unknown error"));
                        break;
                    case "OnMrecAdClickedEvent":
                        _instance?.MrecAdClicked?.Invoke(ad);
                        break;
                    case "OnMrecAdRevenuePaidEvent":
                        _instance?.MrecAdRevenuePaid?.Invoke(ad);
                        break;

                    // Interstitial Events
                    case "OnInterstitialAdLoadedEvent":
                        CloudXSdk.Log.LogDebug(() => $"[IOSDelegate] Dispatching OnInterstitialAdLoadedEvent to main thread. {ad}");
                        _instance?.InterstitialAdLoadSuccess?.Invoke(ad);
                        break;
                    case "OnInterstitialAdLoadFailedEvent":
                        CloudXSdk.Log.LogDebug(() => $"[IOSDelegate] Dispatching OnInterstitialAdLoadFailedEvent. Error: {error}");
                        _instance?.InterstitialAdLoadFailed?.Invoke(adUnitId ?? "", error ?? CreateDefaultError("Unknown error"));
                        break;
                    case "OnInterstitialAdDisplayedEvent":
                        _instance?.InterstitialAdShowSuccess?.Invoke(ad);
                        break;
                    case "OnInterstitialAdDisplayFailedEvent":
                        _instance?.InterstitialAdShowFailed?.Invoke(ad, error ?? CreateDefaultError("Unknown error"));
                        break;
                    case "OnInterstitialAdHiddenEvent":
                        _instance?.InterstitialAdHidden?.Invoke(ad);
                        break;
                    case "OnInterstitialAdClickedEvent":
                        _instance?.InterstitialAdClicked?.Invoke(ad);
                        break;
                    case "OnInterstitialAdRevenuePaidEvent":
                        _instance?.InterstitialAdRevenuePaid?.Invoke(ad);
                        break;

                    // App Open Events
                    case "OnAppOpenAdLoadedEvent":
                        _instance?.AppOpenAdLoadSuccess?.Invoke(ad);
                        break;
                    case "OnAppOpenAdLoadFailedEvent":
                        _instance?.AppOpenAdLoadFailed?.Invoke(adUnitId ?? "", error ?? CreateDefaultError("Unknown error"));
                        break;
                    case "OnAppOpenAdDisplayedEvent":
                        _instance?.AppOpenAdShowSuccess?.Invoke(ad);
                        break;
                    case "OnAppOpenAdDisplayFailedEvent":
                        _instance?.AppOpenAdShowFailed?.Invoke(ad, error ?? CreateDefaultError("Unknown error"));
                        break;
                    case "OnAppOpenAdHiddenEvent":
                        _instance?.AppOpenAdHidden?.Invoke(ad);
                        break;
                    case "OnAppOpenAdClickedEvent":
                        _instance?.AppOpenAdClicked?.Invoke(ad);
                        break;
                    case "OnAppOpenAdRevenuePaidEvent":
                        _instance?.AppOpenAdRevenuePaid?.Invoke(ad);
                        break;

                    // Rewarded Events
                    case "OnRewardedAdLoadedEvent":
                        _instance?.RewardedAdLoadSuccess?.Invoke(ad);
                        break;
                    case "OnRewardedAdLoadFailedEvent":
                        _instance?.RewardedAdLoadFailed?.Invoke(adUnitId ?? "", error ?? CreateDefaultError("Unknown error"));
                        break;
                    case "OnRewardedAdDisplayedEvent":
                        _instance?.RewardedAdShowSuccess?.Invoke(ad);
                        break;
                    case "OnRewardedAdDisplayFailedEvent":
                        _instance?.RewardedAdShowFailed?.Invoke(ad, error ?? CreateDefaultError("Unknown error"));
                        break;
                    case "OnRewardedAdHiddenEvent":
                        _instance?.RewardedAdHidden?.Invoke(ad);
                        break;
                    case "OnRewardedAdClickedEvent":
                        _instance?.RewardedAdClicked?.Invoke(ad);
                        break;
                    case "OnRewardedAdRewardedEvent":
                        var reward = CreateCloudXReward(props);
                        _instance?.RewardedAdRewarded?.Invoke(ad, reward);
                        break;
                    case "OnRewardedAdRevenuePaidEvent":
                        _instance?.RewardedAdRevenuePaid?.Invoke(ad);
                        break;

                    case "OnArbiterCompletedEvent":
                        var arbCallId = GetString(props, "callId") ?? "";
                        if (_arbiterPending.TryRemove(arbCallId, out var arbPending))
                            arbPending(CreateArbiterResult(props));
                        break;

                    default:
                        // Error so it prints at the default log level: an unhandled name is a defect, never expected traffic.
                        CloudXSdk.Log.LogError(() => $"Unhandled iOS event: {eventName} (adUnitId={adUnitId})");
                        break;
                }
            });
        }

        #endregion

        #region PlatformDelegate Implementation

        public string GetVersion()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return _CLXGetSdkVersion();
#else
            throw NotOnIOS(nameof(GetVersion));
#endif
        }

        public void Initialize(
            string appKey,
            string pluginVersion,
            Action<CloudXSdkConfiguration> onSuccess,
            Action<CloudXError> onFailure
        )
        {
#if UNITY_IOS && !UNITY_EDITOR
            _initializationOnSuccess = onSuccess;
            _initializationOnFailure = onFailure;
            CloudXSdk.Log.LogDebug(() => $"Initialize called - appKey: {appKey}, pluginVersion: {pluginVersion}");
            _CLXInitialize(appKey, pluginVersion);
#else
            throw NotOnIOS(nameof(Initialize));
#endif
        }

        public bool IsInitialized()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return _CLXIsInitialized();
#else
            throw NotOnIOS(nameof(IsInitialized));
#endif
        }

        public void SetHashedUserId(string hashedUserId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"SetHashedUserId: {hashedUserId}");
            _CLXSetHashedUserId(hashedUserId);
#endif
        }

        public void SetUserKeyValue(string key, string value)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"SetUserKeyValue: {key}={value}");
            _CLXSetUserKeyValue(key, value);
#endif
        }

        public void SetAppKeyValue(string key, string value)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"SetAppKeyValue: {key}={value}");
            _CLXSetAppKeyValue(key, value);
#endif
        }

        public void ClearAllKeyValues()
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => "ClearAllKeyValues");
            _CLXClearAllKeyValues();
#endif
        }

        public bool ReportRevenueData(string revenueDataJson)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"ReportRevenueData: {revenueDataJson}");
            return _CLXReportRevenueData(revenueDataJson);
#else
            throw NotOnIOS(nameof(ReportRevenueData));
#endif
        }

        public void SetMinLogLevel(CloudXLogLevel level)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"SetMinLogLevel: {level}");
            _CLXSetMinLogLevel(level.ToString());
#endif
        }

        public void SetHasUserConsent(bool? hasUserConsent)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"SetHasUserConsent: {hasUserConsent}");
            _CLXSetHasUserConsent(hasUserConsent.HasValue ? 1 : 0, hasUserConsent.GetValueOrDefault() ? 1 : 0);
#endif
        }

        public void SetDoNotSell(bool? doNotSell)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"SetDoNotSell: {doNotSell}");
            _CLXSetDoNotSell(doNotSell.HasValue ? 1 : 0, doNotSell.GetValueOrDefault() ? 1 : 0);
#endif
        }

        #region Banner Methods

        public void CreateHorizontalBanner(string adUnitId, CloudXAdViewConfiguration.AdViewPosition position)
        {
#if UNITY_IOS && !UNITY_EDITOR
            var positionName = position.ToString();
            CloudXSdk.Log.LogDebug(() => $"CreateHorizontalBanner: {adUnitId}, position: {positionName}");
            _CLXCreateBanner(adUnitId, positionName);
#endif
        }

        public void CreateVerticalBanner(string adUnitId, CloudXAdViewConfiguration.AdViewVerticalPosition verticalPosition)
        {
#if UNITY_IOS && !UNITY_EDITOR
            var positionName = verticalPosition.ToString();
            CloudXSdk.Log.LogDebug(() => $"CreateVerticalBanner: {adUnitId}, verticalPosition: {positionName}");
            _CLXCreateVerticalBanner(adUnitId, positionName);
#endif
        }

        public void ShowBanner(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"ShowBanner: {adUnitId}");
            _CLXShowBanner(adUnitId);
#endif
        }

        public void HideBanner(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"HideBanner: {adUnitId}");
            _CLXHideBanner(adUnitId);
#endif
        }

        public void LoadBanner(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"LoadBanner: {adUnitId}");
            _CLXLoadBanner(adUnitId);
#endif
        }

        public void StartBannerAutoRefresh(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"StartBannerAutoRefresh: {adUnitId}");
            _CLXStartBannerAutoRefresh(adUnitId);
#endif
        }

        public void StopBannerAutoRefresh(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"StopBannerAutoRefresh: {adUnitId}");
            _CLXStopBannerAutoRefresh(adUnitId);
#endif
        }

        public void SetBannerPlacement(string adUnitId, string? placement)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"SetBannerPlacement: {adUnitId}, placement: {placement}");
            _CLXSetBannerPlacement(adUnitId, placement);
#endif
        }

        public void SetBannerCustomData(string adUnitId, string? customData)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"SetBannerCustomData: {adUnitId}, customData: {customData}");
            _CLXSetBannerCustomData(adUnitId, customData);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        /*
         * Serializes the value to an enveloped JSON string for the native ...Json bridge. Returns
         * false when the value type is unsupported (caller skips the call so prior state is kept);
         * a null value yields json=null (clear the key).
         */
        private static bool TryBuildExtraParameterJson(object? value, out string? json)
        {
            json = value == null ? null : ExtraParameterCodec.SerializeEnvelope(value);
            return value == null || json != null;
        }
#endif

        public void SetBannerExtraParameter(string adUnitId, string key, object? value)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!TryBuildExtraParameterJson(value, out var json)) return;
            CloudXSdk.Log.LogDebug(() => $"SetBannerExtraParameter: {adUnitId}, key: {key}, json: {json}");
            _CLXSetBannerExtraParameterJson(adUnitId, key, json);
#endif
        }

        public void DestroyBanner(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"DestroyBanner: {adUnitId}");
            _CLXDestroyBanner(adUnitId);
#endif
        }

        #endregion

        #region MREC Methods

        public void CreateMrec(string adUnitId, CloudXAdViewConfiguration.AdViewPosition position)
        {
#if UNITY_IOS && !UNITY_EDITOR
            var positionName = position.ToString();
            CloudXSdk.Log.LogDebug(() => $"CreateMrec: {adUnitId}, position: {positionName}");
            _CLXCreateMrec(adUnitId, positionName);
#endif
        }

        public void ShowMrec(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"ShowMrec: {adUnitId}");
            _CLXShowMrec(adUnitId);
#endif
        }

        public void HideMrec(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"HideMrec: {adUnitId}");
            _CLXHideMrec(adUnitId);
#endif
        }

        public void LoadMrec(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"LoadMrec: {adUnitId}");
            _CLXLoadMrec(adUnitId);
#endif
        }

        public void StartMrecAutoRefresh(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"StartMrecAutoRefresh: {adUnitId}");
            _CLXStartMrecAutoRefresh(adUnitId);
#endif
        }

        public void StopMrecAutoRefresh(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"StopMrecAutoRefresh: {adUnitId}");
            _CLXStopMrecAutoRefresh(adUnitId);
#endif
        }

        public void SetMrecPlacement(string adUnitId, string? placement)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"SetMrecPlacement: {adUnitId}, placement: {placement}");
            _CLXSetMrecPlacement(adUnitId, placement);
#endif
        }

        public void SetMrecCustomData(string adUnitId, string? customData)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"SetMrecCustomData: {adUnitId}, customData: {customData}");
            _CLXSetMrecCustomData(adUnitId, customData);
#endif
        }

        public void SetMrecExtraParameter(string adUnitId, string key, object? value)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!TryBuildExtraParameterJson(value, out var json)) return;
            CloudXSdk.Log.LogDebug(() => $"SetMrecExtraParameter: {adUnitId}, key: {key}, json: {json}");
            _CLXSetMrecExtraParameterJson(adUnitId, key, json);
#endif
        }

        public void DestroyMrec(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"DestroyMrec: {adUnitId}");
            _CLXDestroyMrec(adUnitId);
#endif
        }

        #endregion

        #region Interstitial Methods

        public void LoadInterstitial(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"LoadInterstitial: {adUnitId}");
            _CLXLoadInterstitial(adUnitId);
#endif
        }

        public void ShowInterstitial(string adUnitId, string? placement, string? customData)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"ShowInterstitial: {adUnitId}, placement: {placement}, customData: {customData}");
            _CLXShowInterstitial(adUnitId, placement, customData);
#endif
        }

        public void SetInterstitialExtraParameter(string adUnitId, string key, object? value)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!TryBuildExtraParameterJson(value, out var json)) return;
            CloudXSdk.Log.LogDebug(() => $"SetInterstitialExtraParameter: {adUnitId}, key: {key}, json: {json}");
            _CLXSetInterstitialExtraParameterJson(adUnitId, key, json);
#endif
        }

        public bool IsInterstitialReady(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return _CLXIsInterstitialReady(adUnitId);
#else
            throw NotOnIOS(nameof(IsInterstitialReady));
#endif
        }

        public void DestroyInterstitial(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"DestroyInterstitial: {adUnitId}");
            _CLXDestroyInterstitial(adUnitId);
#endif
        }

        public void DestroyAllInterstitials()
        {
            CloudXSdk.Log.LogDebug(() => $"DestroyAllInterstitials - No-op on iOS");
        }

        #endregion

        #region App Open Methods

        public void LoadAppOpen(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"LoadAppOpen: {adUnitId}");
            _CLXLoadAppOpen(adUnitId);
#endif
        }

        public void ShowAppOpen(string adUnitId, string? placement, string? customData)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"ShowAppOpen: {adUnitId}, placement: {placement}, customData: {customData}");
            _CLXShowAppOpen(adUnitId, placement, customData);
#endif
        }

        public void SetAppOpenExtraParameter(string adUnitId, string key, object? value)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!TryBuildExtraParameterJson(value, out var json)) return;
            CloudXSdk.Log.LogDebug(() => $"SetAppOpenExtraParameter: {adUnitId}, key: {key}, json: {json}");
            _CLXSetAppOpenExtraParameterJson(adUnitId, key, json);
#endif
        }

        public bool IsAppOpenReady(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return _CLXIsAppOpenReady(adUnitId);
#else
            throw NotOnIOS(nameof(IsAppOpenReady));
#endif
        }

        public void DestroyAppOpen(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"DestroyAppOpen: {adUnitId}");
            _CLXDestroyAppOpen(adUnitId);
#endif
        }

        public void DestroyAllAppOpens()
        {
            CloudXSdk.Log.LogDebug(() => $"DestroyAllAppOpens - No-op on iOS");
        }

        #endregion

        #region Rewarded Methods

        public void LoadRewarded(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"LoadRewarded: {adUnitId}");
            _CLXLoadRewarded(adUnitId);
#endif
        }

        public void ShowRewarded(string adUnitId, string? placement, string? customData)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"ShowRewarded: {adUnitId}, placement: {placement}, customData: {customData}");
            _CLXShowRewarded(adUnitId, placement, customData);
#endif
        }

        public void SetRewardedExtraParameter(string adUnitId, string key, object? value)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!TryBuildExtraParameterJson(value, out var json)) return;
            CloudXSdk.Log.LogDebug(() => $"SetRewardedExtraParameter: {adUnitId}, key: {key}, json: {json}");
            _CLXSetRewardedExtraParameterJson(adUnitId, key, json);
#endif
        }

        public bool IsRewardedReady(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return _CLXIsRewardedReady(adUnitId);
#else
            throw NotOnIOS(nameof(IsRewardedReady));
#endif
        }

        public void DestroyRewarded(string adUnitId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"DestroyRewarded: {adUnitId}");
            _CLXDestroyRewarded(adUnitId);
#endif
        }

        public void DestroyAllRewarded()
        {
            CloudXSdk.Log.LogDebug(() => $"DestroyAllRewarded - No-op on iOS");
        }

        #endregion

        #region Arbiter

        public void Arbiter(string bidsJson, Action<CloudXArbiterResult> onCompleted)
        {
#if UNITY_IOS && !UNITY_EDITOR
            var callId = System.Guid.NewGuid().ToString();
            _arbiterPending[callId] = onCompleted;
            _CLXArbiter(callId, bidsJson);
#else
            throw NotOnIOS(nameof(Arbiter));
#endif
        }

        #endregion

        #region Visual Debugging Methods

        public void SetVisualDebuggingEnabled(bool enabled)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CloudXSdk.Log.LogDebug(() => $"SetVisualDebuggingEnabled: {enabled}");
            _CLXSetVisualDebuggingEnabled(enabled);
#endif
        }

        public bool IsVisualDebuggingEnabled()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return _CLXIsVisualDebuggingEnabled();
#else
            throw NotOnIOS(nameof(IsVisualDebuggingEnabled));
#endif
        }

        #endregion

        #endregion

        #region Helper Methods

        private static CloudXAd CreateCloudXAd(Dictionary<string, object> props)
        {
            // Parse ad format from props if available, default to Interstitial
            var adFormatStr = GetString(props, "adFormat") ?? "INTERSTITIAL";
            var adFormat = ParseCloudXAdFormat(adFormatStr);

            return new CloudXAd(
                adFormat,
                GetString(props, "adUnitId"),
                GetString(props, "placement"),
                GetString(props, "networkName"),
                GetString(props, "networkPlacement"),
                GetDouble(props, "revenue"),
                GetStringDictionary(props, "adValues")
            );
        }

        private static CloudXAdFormat ParseCloudXAdFormat(string adFormatStr)
        {
            if (string.Equals(adFormatStr, "APP_OPEN", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(adFormatStr, "APPOPEN", System.StringComparison.OrdinalIgnoreCase))
            {
                return CloudXAdFormat.AppOpen;
            }

            return Enum.TryParse<CloudXAdFormat>(adFormatStr, ignoreCase: true, out var format)
                ? format
                : CloudXAdFormat.Interstitial;
        }

        private static CloudXArbiterResult CreateArbiterResult(Dictionary<string, object> props)
        {
            var id = GetString(props, "id") ?? string.Empty;
            var platformWireName = GetString(props, "platform");
            var platform = ArbiterPlatformParser.Parse(platformWireName);
            var platformName = GetString(props, "platformName");
            if (string.IsNullOrEmpty(platformName))
                platformName = string.IsNullOrEmpty(platformWireName) ? platform.ToWireString() : platformWireName;
            var bidId = GetString(props, "bidId");
            var extras = GetStringDictionary(props, "extras") ?? new Dictionary<string, string>();
            return new CloudXArbiterResult(id, platform, platformName, bidId, extras);
        }

        private static CloudXReward CreateCloudXReward(Dictionary<string, object> props)
        {
            return new CloudXReward(
                GetInt(props, "rewardAmount"),
                GetString(props, "rewardLabel") ?? ""
            );
        }

        private static CloudXError? CreateCloudXError(Dictionary<string, object> props)
        {
            if (!props.ContainsKey("errorCode")) return null;
            
            var code = GetInt(props, "errorCode");
            var message = GetString(props, "errorMessage") ?? "Unknown error";
            // CloudXError(errorCodeName, errorCodeValue, message)
            return new CloudXError("IOS_ERROR", code, message);
        }

        private static CloudXError CreateDefaultError(string message)
        {
            return new CloudXError("UNKNOWN_ERROR", -1, message);
        }

#if !UNITY_IOS || UNITY_EDITOR
        /*
         * Every member here works off iOS - through the delegate CloudXSdk picks for that
         * platform, never through this one. So the off-iOS branches report the wrong
         * delegate rather than returning a default that reads as a real answer: a
         * rejected payload, an empty auction, or version 0.0.0.
         */
        private static PlatformNotSupportedException NotOnIOS(string member)
        {
            return new PlatformNotSupportedException(
                $"IOSDelegate.{member} is iOS-only; use the delegate CloudXSdk selected for this platform.");
        }
#endif

        private static string? GetString(Dictionary<string, object> props, string key)
        {
            if (props.TryGetValue(key, out var value))
            {
                return value?.ToString();
            }
            return null;
        }

        private static bool GetBool(Dictionary<string, object> props, string key)
        {
            if (props.TryGetValue(key, out var value))
            {
                if (value is bool b) return b;
                if (bool.TryParse(value?.ToString(), out var result)) return result;
            }
            return false;
        }

        private static int GetInt(Dictionary<string, object> props, string key)
        {
            if (!props.TryGetValue(key, out var value)) return 0;
            switch (value)
            {
                case int i: return i;
                case long l: return (int)l;
                case double d: return (int)d;
            }
            return int.TryParse(value?.ToString(), out var result) ? result : 0;
        }

        private static double GetDouble(Dictionary<string, object> props, string key)
        {
            if (!props.TryGetValue(key, out var value)) return 0;
            switch (value)
            {
                case double d: return d;
                case float f: return f;
                case int i: return i;
            }
            return double.TryParse(value?.ToString(), out var result) ? result : 0;
        }

        private static IReadOnlyDictionary<string, string>? GetStringDictionary(Dictionary<string, object> props, string key)
        {
            if (!props.TryGetValue(key, out var raw) || raw is not Dictionary<string, object> map)
                return null;
            var result = new Dictionary<string, string>();
            foreach (var kvp in map)
            {
                if (string.IsNullOrEmpty(kvp.Key) || kvp.Value is not string strVal || string.IsNullOrEmpty(strVal))
                    continue;
                result[kvp.Key] = strVal;
            }
            return result;
        }

        #endregion
    }
}
