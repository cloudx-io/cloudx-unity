#nullable enable

using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace CloudX
{
    public static class CloudXSdk
    {
        private static readonly PlatformDelegate PlatformDelegate;
        internal static readonly Logger Log = new Logger("CloudXUnityPlugin");
        internal const string PluginVersion = "unity-4.6.1";

        static CloudXSdk()
        {
#if UNITY_EDITOR
            // Check for Unity Editor first since the editor also responds to the currently selected platform.
            PlatformDelegate = GetUnityPlayerDelegate();
#elif UNITY_ANDROID
            PlatformDelegate = new Android.AndroidDelegate(AndroidJniBridge.JniBridgeClass);
#elif UNITY_IPHONE || UNITY_IOS
            PlatformDelegate = new IOS.IOSDelegate();
#else
            PlatformDelegate = GetUnityPlayerDelegate();
#endif
            // Banner ad callbacks
            PlatformDelegate.BannerAdLoadSuccess += CloudXAdsCallbacks.Banner.OnAdLoadSuccessInternal;
            PlatformDelegate.BannerAdLoadFailed += CloudXAdsCallbacks.Banner.OnAdLoadFailedInternal;
            PlatformDelegate.BannerAdClicked += CloudXAdsCallbacks.Banner.OnAdClickedInternal;
            PlatformDelegate.BannerAdRevenuePaid += CloudXAdsCallbacks.Banner.OnAdRevenuePaidInternal;

            // MREC ad callbacks
            PlatformDelegate.MrecAdLoadSuccess += CloudXAdsCallbacks.Mrec.OnAdLoadSuccessInternal;
            PlatformDelegate.MrecAdLoadFailed += CloudXAdsCallbacks.Mrec.OnAdLoadFailedInternal;
            PlatformDelegate.MrecAdClicked += CloudXAdsCallbacks.Mrec.OnAdClickedInternal;
            PlatformDelegate.MrecAdRevenuePaid += CloudXAdsCallbacks.Mrec.OnAdRevenuePaidInternal;

            // Interstitial ad callbacks
            PlatformDelegate.InterstitialAdLoadSuccess += CloudXAdsCallbacks.Interstitial.OnAdLoadSuccessInternal;
            PlatformDelegate.InterstitialAdLoadFailed += CloudXAdsCallbacks.Interstitial.OnAdLoadFailedInternal;
            PlatformDelegate.InterstitialAdShowSuccess += CloudXAdsCallbacks.Interstitial.OnAdShowSuccessInternal;
            PlatformDelegate.InterstitialAdShowFailed += CloudXAdsCallbacks.Interstitial.OnAdShowFailedInternal;
            PlatformDelegate.InterstitialAdHidden += CloudXAdsCallbacks.Interstitial.OnAdHiddenInternal;
            PlatformDelegate.InterstitialAdClicked += CloudXAdsCallbacks.Interstitial.OnAdClickedInternal;
            PlatformDelegate.InterstitialAdRevenuePaid += CloudXAdsCallbacks.Interstitial.OnAdRevenuePaidInternal;

            // App Open ad callbacks
            PlatformDelegate.AppOpenAdLoadSuccess += CloudXAdsCallbacks.AppOpen.OnAdLoadSuccessInternal;
            PlatformDelegate.AppOpenAdLoadFailed += CloudXAdsCallbacks.AppOpen.OnAdLoadFailedInternal;
            PlatformDelegate.AppOpenAdShowSuccess += CloudXAdsCallbacks.AppOpen.OnAdShowSuccessInternal;
            PlatformDelegate.AppOpenAdShowFailed += CloudXAdsCallbacks.AppOpen.OnAdShowFailedInternal;
            PlatformDelegate.AppOpenAdHidden += CloudXAdsCallbacks.AppOpen.OnAdHiddenInternal;
            PlatformDelegate.AppOpenAdClicked += CloudXAdsCallbacks.AppOpen.OnAdClickedInternal;
            PlatformDelegate.AppOpenAdRevenuePaid += CloudXAdsCallbacks.AppOpen.OnAdRevenuePaidInternal;

            // Rewarded ad callbacks
            PlatformDelegate.RewardedAdLoadSuccess += CloudXAdsCallbacks.Rewarded.OnAdLoadSuccessInternal;
            PlatformDelegate.RewardedAdLoadFailed += CloudXAdsCallbacks.Rewarded.OnAdLoadFailedInternal;
            PlatformDelegate.RewardedAdShowSuccess += CloudXAdsCallbacks.Rewarded.OnAdShowSuccessInternal;
            PlatformDelegate.RewardedAdShowFailed += CloudXAdsCallbacks.Rewarded.OnAdShowFailedInternal;
            PlatformDelegate.RewardedAdHidden += CloudXAdsCallbacks.Rewarded.OnAdHiddenInternal;
            PlatformDelegate.RewardedAdClicked += CloudXAdsCallbacks.Rewarded.OnAdClickedInternal;
            PlatformDelegate.RewardedAdRewarded += CloudXAdsCallbacks.Rewarded.OnAdRewardedInternal;
            PlatformDelegate.RewardedAdRevenuePaid += CloudXAdsCallbacks.Rewarded.OnAdRevenuePaidInternal;
        }

        /// <summary>
        /// Controls which thread CloudX callbacks are invoked on.
        /// <list type="bullet">
        /// <item><c>null</c> (default): ad lifecycle, initialization, Trusted Arbiter and banner/MREC
        /// <c>OnAdRevenuePaid</c> callbacks run on the Unity main thread. <c>OnAdRevenuePaid</c> for the
        /// fullscreen formats (interstitial, app open, rewarded) is delivered immediately on the native
        /// callback thread, so it arrives while the ad is showing instead of after it closes.</item>
        /// <item><c>true</c>: every callback, including <c>OnAdRevenuePaid</c>, runs on the Unity main thread
        /// (adds up to one frame of latency). On Android the Unity player is paused while a fullscreen ad
        /// Activity is in front, so callbacks raised during the ad (show, revenue) are delivered when the
        /// ad closes.</item>
        /// <item><c>false</c>: every callback runs inline on the native callback thread; handlers must not
        /// use Unity APIs.</item>
        /// </list>
        /// Read on each callback, so it can be set at any time; set it before <see cref="Initialize"/> to
        /// cover initialization callbacks. A handler that throws is logged at ERROR with its type and method
        /// and does not affect other handlers.
        /// </summary>
        public static bool? InvokeEventsOnUnityMainThread { get; set; }

        public static string GetVersion()
        {
            return PlatformDelegate.GetVersion();
        }

        /// <summary>
        /// Initialize the CloudX SDK with the provided configuration.
        /// Subscribe to CloudXInitializationCallbacks events before calling this method.
        /// </summary>
        /// <param name="configuration">The initialization configuration built using CloudXInitializationConfiguration.Create().</param>
        public static void Initialize(CloudXInitializationConfiguration configuration)
        {
            PlatformDelegate.Initialize(
                configuration.AppKey,
                PluginVersion,
                CloudXInitializationCallbacks.OnSdkInitializedInternal,
                CloudXInitializationCallbacks.OnSdkInitializationFailedInternal
            );
        }

        /// <summary>
        /// Check if the SDK has been initialized.
        /// </summary>
        /// <returns>True if the SDK is initialized, false otherwise.</returns>
        public static bool IsInitialized()
        {
            return PlatformDelegate.IsInitialized();
        }

        public static void SetMinLogLevel(CloudXLogLevel level)
        {
            Log.CurrentLogLevel = level;
            PlatformDelegate.SetMinLogLevel(level);
        }

        /// <summary>
        /// Sets the GDPR user consent override for publishers not using a CMP.
        /// IAB consent signals take precedence over this manual value.
        /// Pass null to clear the override and defer back to CMP or IAB signals.
        /// </summary>
        public static void SetHasUserConsent(bool? hasUserConsent)
        {
            PlatformDelegate.SetHasUserConsent(hasUserConsent);
        }

        /// <summary>
        /// Sets the CCPA do-not-sell override for publishers not using a CMP.
        /// IAB privacy signals take precedence over this manual value.
        /// Pass null to clear the override and defer back to CMP or IAB signals.
        /// </summary>
        public static void SetDoNotSell(bool? doNotSell)
        {
            PlatformDelegate.SetDoNotSell(doNotSell);
        }

        public static void SetHashedUserId(string hashedUserId)
        {
            PlatformDelegate.SetHashedUserId(hashedUserId);
        }

        public static void SetUserKeyValue(string key, string value)
        {
            PlatformDelegate.SetUserKeyValue(key, value);
        }

        public static void SetAppKeyValue(string key, string value)
        {
            PlatformDelegate.SetAppKeyValue(key, value);
        }

        public static void ClearAllKeyValues()
        {
            PlatformDelegate.ClearAllKeyValues();
        }

        /// <summary>
        /// Reports publisher-observed impression-level ad revenue, such as an AdMob paid event.
        /// Returns false when the payload is invalid or the native SDK rejects the event.
        /// An invalid payload logs which field was wrong at <see cref="CloudXLogLevel.Warn"/>;
        /// call <see cref="SetMinLogLevel"/> with that level or lower any time before this
        /// call to see it.
        /// </summary>
        public static bool ReportRevenueData(CloudXRevenueData data)
        {
            if (!RevenueDataJsonWriter.TryWrite(data, out var json)) return false;
            return PlatformDelegate.ReportRevenueData(json!);
        }

        /// <summary>
        /// Creates a banner ad view for the given ad unit. The configuration selects the mode:
        /// a horizontal position places the banner at one of the nine screen anchors, while a
        /// vertical position rotates it 90 degrees and pins it flush against the left or right
        /// screen edge, inset past any display cutout (notch, camera hole, Dynamic Island) on
        /// that edge so the creative is never obscured.
        /// </summary>
        public static void CreateBanner(string adUnitId, CloudXAdViewConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            if (configuration.HorizontalPosition != null)
            {
                PlatformDelegate.CreateHorizontalBanner(adUnitId, configuration.HorizontalPosition.Value);
            }
            else if (configuration.VerticalPosition != null)
            {
                PlatformDelegate.CreateVerticalBanner(adUnitId, configuration.VerticalPosition.Value);
            }
            else
            {
                /*
                 * Unreachable today: the CloudXAdViewConfiguration constructors guarantee
                 * exactly one position is set. Reaching this means a new position kind was
                 * added without extending this dispatch - fail loudly instead of silently
                 * creating nothing.
                 */
                throw new NotImplementedException(
                    "Unhandled CloudXAdViewConfiguration position kind. " +
                    "Extend CloudXSdk.CreateBanner when adding new position kinds.");
            }
        }

        // Banner ad methods
        public static void ShowBanner(string adUnitId)
        {
            PlatformDelegate.ShowBanner(adUnitId);
        }

        public static void HideBanner(string adUnitId)
        {
            PlatformDelegate.HideBanner(adUnitId);
        }

        public static void LoadBanner(string adUnitId)
        {
            PlatformDelegate.LoadBanner(adUnitId);
        }

        public static void StartBannerAutoRefresh(string adUnitId)
        {
            PlatformDelegate.StartBannerAutoRefresh(adUnitId);
        }

        public static void StopBannerAutoRefresh(string adUnitId)
        {
            PlatformDelegate.StopBannerAutoRefresh(adUnitId);
        }

        public static void SetBannerPlacement(string adUnitId, string? placement)
        {
            PlatformDelegate.SetBannerPlacement(adUnitId, placement);
        }

        public static void SetBannerCustomData(string adUnitId, string? customData)
        {
            PlatformDelegate.SetBannerCustomData(adUnitId, customData);
        }

        /// <summary>
        /// Sets or clears a per-request bid-request extra parameter for the banner. The value is
        /// attached under imp.ext.cx.local_extra_parameters and interpreted by the server.
        /// Reserved floor keys: "minFloor" (single USD CPM), "minFloors" (per-round list),
        /// "minFloorsByPriority" (map of priority string to USD CPM). Pass null to clear the key.
        /// Accepts bool, int, long, float, double, decimal, string, or an IList/IDictionary. The
        /// top-level type is checked client-side (unsupported types, and non-finite numbers, are
        /// ignored and logged); values nested inside a list/dictionary are validated server-side,
        /// where unsupported or invalid entries may be dropped (lists all-or-nothing, maps drop
        /// invalid entries).
        /// </summary>
        public static void SetBannerExtraParameter(string adUnitId, string key, object? value)
        {
            PlatformDelegate.SetBannerExtraParameter(adUnitId, key, value);
        }

        public static void DestroyBanner(string adUnitId)
        {
            PlatformDelegate.DestroyBanner(adUnitId);
        }

        // MREC ad methods

        /// <summary>
        /// Creates an MREC ad view for the given ad unit. MREC ads accept only horizontal
        /// configurations; passing a vertical configuration throws
        /// <see cref="ArgumentException"/>.
        /// </summary>
        public static void CreateMrec(string adUnitId, CloudXAdViewConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (configuration.HorizontalPosition == null)
            {
                throw new ArgumentException(
                    "MREC ads support only horizontal positions; vertical positions are banner-only.",
                    nameof(configuration));
            }

            PlatformDelegate.CreateMrec(adUnitId, configuration.HorizontalPosition.Value);
        }

        public static void ShowMrec(string adUnitId)
        {
            PlatformDelegate.ShowMrec(adUnitId);
        }

        public static void HideMrec(string adUnitId)
        {
            PlatformDelegate.HideMrec(adUnitId);
        }

        public static void LoadMrec(string adUnitId)
        {
            PlatformDelegate.LoadMrec(adUnitId);
        }

        public static void StartMrecAutoRefresh(string adUnitId)
        {
            PlatformDelegate.StartMrecAutoRefresh(adUnitId);
        }

        public static void SetMRecPlacement(string adUnitId, string? placement)
        {
            PlatformDelegate.SetMrecPlacement(adUnitId, placement);
        }

        public static void SetMRecCustomData(string adUnitId, string? customData)
        {
            PlatformDelegate.SetMrecCustomData(adUnitId, customData);
        }

        /// <summary>
        /// Sets or clears a per-request bid-request extra parameter for the MREC. See
        /// <see cref="SetBannerExtraParameter"/> for the reserved floor keys and accepted value types.
        /// </summary>
        public static void SetMRecExtraParameter(string adUnitId, string key, object? value)
        {
            PlatformDelegate.SetMrecExtraParameter(adUnitId, key, value);
        }

        public static void StopMrecAutoRefresh(string adUnitId)
        {
            PlatformDelegate.StopMrecAutoRefresh(adUnitId);
        }

        public static void DestroyMrec(string adUnitId)
        {
            PlatformDelegate.DestroyMrec(adUnitId);
        }

        // Interstitial ad methods
        public static void LoadInterstitial(string adUnitId)
        {
            PlatformDelegate.LoadInterstitial(adUnitId);
        }
        public static void ShowInterstitial(string adUnitId, string? placement = null, string? customData = null)
        {
            PlatformDelegate.ShowInterstitial(adUnitId, placement, customData);
        }

        /// <summary>
        /// Sets or clears a per-request bid-request extra parameter for the interstitial. Values are
        /// read at load time. See <see cref="SetBannerExtraParameter"/> for reserved floor keys and
        /// accepted value types.
        /// </summary>
        public static void SetInterstitialExtraParameter(string adUnitId, string key, object? value)
        {
            PlatformDelegate.SetInterstitialExtraParameter(adUnitId, key, value);
        }

        public static bool IsInterstitialReady(string adUnitId)
        {
            return PlatformDelegate.IsInterstitialReady(adUnitId);
        }

        public static void DestroyInterstitial(string adUnitId)
        {
            PlatformDelegate.DestroyInterstitial(adUnitId);
        }

        // App Open ad methods
        public static void LoadAppOpen(string adUnitId)
        {
            PlatformDelegate.LoadAppOpen(adUnitId);
        }
        public static void ShowAppOpen(string adUnitId, string? placement = null, string? customData = null)
        {
            PlatformDelegate.ShowAppOpen(adUnitId, placement, customData);
        }

        /// <summary>
        /// Sets or clears a per-request bid-request extra parameter for the app open ad. Values are
        /// read at load time. See <see cref="SetBannerExtraParameter"/> for reserved floor keys and
        /// accepted value types.
        /// </summary>
        public static void SetAppOpenExtraParameter(string adUnitId, string key, object? value)
        {
            PlatformDelegate.SetAppOpenExtraParameter(adUnitId, key, value);
        }

        public static bool IsAppOpenReady(string adUnitId)
        {
            return PlatformDelegate.IsAppOpenReady(adUnitId);
        }

        public static void DestroyAppOpen(string adUnitId)
        {
            PlatformDelegate.DestroyAppOpen(adUnitId);
        }

        // Rewarded ad methods
        public static void LoadRewarded(string adUnitId)
        {
            PlatformDelegate.LoadRewarded(adUnitId);
        }
        public static void ShowRewarded(string adUnitId, string? placement = null, string? customData = null)
        {
            PlatformDelegate.ShowRewarded(adUnitId, placement, customData);
        }

        /// <summary>
        /// Sets or clears a per-request bid-request extra parameter for the rewarded ad. Values are
        /// read at load time. See <see cref="SetBannerExtraParameter"/> for reserved floor keys and
        /// accepted value types.
        /// </summary>
        public static void SetRewardedExtraParameter(string adUnitId, string key, object? value)
        {
            PlatformDelegate.SetRewardedExtraParameter(adUnitId, key, value);
        }

        public static bool IsRewardedReady(string adUnitId)
        {
            return PlatformDelegate.IsRewardedReady(adUnitId);
        }

        public static void DestroyRewarded(string adUnitId)
        {
            PlatformDelegate.DestroyRewarded(adUnitId);
        }

        /// <summary>
        /// Run a one-shot trusted-arbiter auction over the provided bids.
        /// onCompleted is invoked exactly once with the winning bid, or a NONE-platform
        /// result if no bid was selected.
        /// </summary>
        public static void Arbiter(IReadOnlyList<CloudXArbiterBid> bids,
            Action<CloudXArbiterResult> onCompleted)
        {
            var bidsJson = ArbiterBidJsonWriter.Write(bids);
            PlatformDelegate.Arbiter(bidsJson, onCompleted);
        }

        /* TODO: Uncomment when visual debugging is supported on all platforms
         * Currently iOS-only. Re-enable when Android SDK adds support.
         *
         * /// <summary>
         * /// Enable or disable visual debugging overlay on ads.
         * /// When enabled, shows bidder info, placement details, and revenue data on ads.
         * /// NOTE: Currently only available on iOS. No-op on Android.
         * /// </summary>
         * /// <param name="enabled">True to enable visual debugging, false to disable.</param>
         * public static void SetVisualDebuggingEnabled(bool enabled)
         * {
         *     PlatformDelegate.SetVisualDebuggingEnabled(enabled);
         * }
         *
         * /// <summary>
         * /// Check if visual debugging is currently enabled.
         * /// NOTE: Currently only available on iOS. Always returns false on Android.
         * /// </summary>
         * /// <returns>True if visual debugging is enabled, false otherwise.</returns>
         * public static bool IsVisualDebuggingEnabled()
         * {
         *     return PlatformDelegate.IsVisualDebuggingEnabled();
         * }
         */
        
        private static UnityPlayer.UnityPlayerDelegate GetUnityPlayerDelegate()
        {
            // IMPORTANT: DO NOT IMPORT THE QUALIFIER  `UnityPlayer.`. 
            // Otherwise, Jetbrains Rider could remove unused qualifiers when commiting.
            return new UnityPlayer.UnityPlayerDelegate();
        }
    }
}
