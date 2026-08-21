using System;
using UnityEngine;

namespace CloudX
{
public static class CloudXAdsCallbacks
{
    public static class Banner
    {
        /// <summary>
        /// Fired when a banner ad finishes loading successfully.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdLoadSuccess;
        /// <summary>
        /// Fired when a banner ad fails to load.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<string, CloudXError> OnAdLoadFailed;
        /// <summary>
        /// Fired when a banner ad is clicked.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdClicked;
        /// <summary>
        /// Fired as soon as impression revenue is available.
        /// On Android and iOS this callback may run off the Unity main thread, so handlers must not
        /// touch Unity APIs directly without switching threads first.
        /// </summary>
        public static event Action<CloudXAd> OnAdRevenuePaid;

        // Internal methods to trigger the events
        internal static void OnAdLoadSuccessInternal(CloudXAd cloudXAd)
        {
            OnAdLoadSuccess?.Invoke(cloudXAd);
        }

        internal static void OnAdLoadFailedInternal(string adUnitId, CloudXError cloudXError)
        {
            OnAdLoadFailed?.Invoke(adUnitId, cloudXError);
        }

        internal static void OnAdClickedInternal(CloudXAd cloudXAd)
        {
            OnAdClicked?.Invoke(cloudXAd);
        }

        internal static void OnAdRevenuePaidInternal(CloudXAd cloudXAd)
        {
            OnAdRevenuePaid?.Invoke(cloudXAd);
        }

        // Add a method to clear all events
        internal static void ResetEvents()
        {
            OnAdLoadSuccess = null;
            OnAdLoadFailed = null;
            OnAdClicked = null;
            OnAdRevenuePaid = null;
        }
    }

    public static class Mrec
    {
        /// <summary>
        /// Fired when an MREC ad finishes loading successfully.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdLoadSuccess;
        /// <summary>
        /// Fired when an MREC ad fails to load.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<string, CloudXError> OnAdLoadFailed;
        /// <summary>
        /// Fired when an MREC ad is clicked.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdClicked;
        /// <summary>
        /// Fired as soon as impression revenue is available.
        /// On Android and iOS this callback may run off the Unity main thread, so handlers must not
        /// touch Unity APIs directly without switching threads first.
        /// </summary>
        public static event Action<CloudXAd> OnAdRevenuePaid;

        // Internal methods to trigger the events
        internal static void OnAdLoadSuccessInternal(CloudXAd cloudXAd)
        {
            OnAdLoadSuccess?.Invoke(cloudXAd);
        }

        internal static void OnAdLoadFailedInternal(string adUnitId, CloudXError cloudXError)
        {
            OnAdLoadFailed?.Invoke(adUnitId, cloudXError);
        }

        internal static void OnAdClickedInternal(CloudXAd cloudXAd)
        {
            OnAdClicked?.Invoke(cloudXAd);
        }

        internal static void OnAdRevenuePaidInternal(CloudXAd cloudXAd)
        {
            OnAdRevenuePaid?.Invoke(cloudXAd);
        }

        // Add a method to clear all events
        internal static void ResetEvents()
        {
            OnAdLoadSuccess = null;
            OnAdLoadFailed = null;
            OnAdClicked = null;
            OnAdRevenuePaid = null;
        }
    }

    public static class Interstitial
    {
        /// <summary>
        /// Fired when an interstitial ad finishes loading successfully.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdLoadSuccess;
        /// <summary>
        /// Fired when an interstitial ad fails to load.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<string, CloudXError> OnAdLoadFailed;
        /// <summary>
        /// Fired when an interstitial ad is displayed.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdShowSuccess;
        /// <summary>
        /// Fired when an interstitial ad fails to display.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd, CloudXError> OnAdShowFailed;
        /// <summary>
        /// Fired when an interstitial ad is hidden after being shown.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdHidden;
        /// <summary>
        /// Fired when an interstitial ad is clicked.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdClicked;
        /// <summary>
        /// Fired as soon as impression revenue is available.
        /// On Android and iOS this callback may run off the Unity main thread, so handlers must not
        /// touch Unity APIs directly without switching threads first.
        /// </summary>
        public static event Action<CloudXAd> OnAdRevenuePaid;

        // Internal methods to trigger the events
        internal static void OnAdLoadSuccessInternal(CloudXAd cloudXAd)
        {
            OnAdLoadSuccess?.Invoke(cloudXAd);
        }

        internal static void OnAdLoadFailedInternal(string adUnitId, CloudXError cloudXError)
        {
            OnAdLoadFailed?.Invoke(adUnitId, cloudXError);
        }

        internal static void OnAdShowSuccessInternal(CloudXAd cloudXAd)
        {
            OnAdShowSuccess?.Invoke(cloudXAd);
        }

        internal static void OnAdShowFailedInternal(CloudXAd cloudXAd, CloudXError cloudXError)
        {
            OnAdShowFailed?.Invoke(cloudXAd, cloudXError);
        }

        internal static void OnAdHiddenInternal(CloudXAd cloudXAd)
        {
            OnAdHidden?.Invoke(cloudXAd);
        }

        internal static void OnAdClickedInternal(CloudXAd cloudXAd)
        {
            OnAdClicked?.Invoke(cloudXAd);
        }

        internal static void OnAdRevenuePaidInternal(CloudXAd cloudXAd)
        {
            OnAdRevenuePaid?.Invoke(cloudXAd);
        }

        // Add a method to clear all events
        internal static void ResetEvents()
        {
            OnAdLoadSuccess = null;
            OnAdLoadFailed = null;
            OnAdShowSuccess = null;
            OnAdShowFailed = null;
            OnAdHidden = null;
            OnAdClicked = null;
            OnAdRevenuePaid = null;
        }
    }

    public static class AppOpen
    {
        /// <summary>
        /// Fired when an app open ad finishes loading successfully.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdLoadSuccess;
        /// <summary>
        /// Fired when an app open ad fails to load.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<string, CloudXError> OnAdLoadFailed;
        /// <summary>
        /// Fired when an app open ad is displayed.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdShowSuccess;
        /// <summary>
        /// Fired when an app open ad fails to display.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd, CloudXError> OnAdShowFailed;
        /// <summary>
        /// Fired when an app open ad is hidden after being shown.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdHidden;
        /// <summary>
        /// Fired when an app open ad is clicked.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdClicked;
        /// <summary>
        /// Fired as soon as impression revenue is available.
        /// On Android and iOS this callback may run off the Unity main thread, so handlers must not
        /// touch Unity APIs directly without switching threads first.
        /// </summary>
        public static event Action<CloudXAd> OnAdRevenuePaid;

        internal static void OnAdLoadSuccessInternal(CloudXAd cloudXAd)
        {
            OnAdLoadSuccess?.Invoke(cloudXAd);
        }

        internal static void OnAdLoadFailedInternal(string adUnitId, CloudXError cloudXError)
        {
            OnAdLoadFailed?.Invoke(adUnitId, cloudXError);
        }

        internal static void OnAdShowSuccessInternal(CloudXAd cloudXAd)
        {
            OnAdShowSuccess?.Invoke(cloudXAd);
        }

        internal static void OnAdShowFailedInternal(CloudXAd cloudXAd, CloudXError cloudXError)
        {
            OnAdShowFailed?.Invoke(cloudXAd, cloudXError);
        }

        internal static void OnAdHiddenInternal(CloudXAd cloudXAd)
        {
            OnAdHidden?.Invoke(cloudXAd);
        }

        internal static void OnAdClickedInternal(CloudXAd cloudXAd)
        {
            OnAdClicked?.Invoke(cloudXAd);
        }

        internal static void OnAdRevenuePaidInternal(CloudXAd cloudXAd)
        {
            OnAdRevenuePaid?.Invoke(cloudXAd);
        }

        internal static void ResetEvents()
        {
            OnAdLoadSuccess = null;
            OnAdLoadFailed = null;
            OnAdShowSuccess = null;
            OnAdShowFailed = null;
            OnAdHidden = null;
            OnAdClicked = null;
            OnAdRevenuePaid = null;
        }
    }

    public static class Rewarded
    {
        /// <summary>
        /// Fired when a rewarded ad finishes loading successfully.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdLoadSuccess;
        /// <summary>
        /// Fired when a rewarded ad fails to load.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<string, CloudXError> OnAdLoadFailed;
        /// <summary>
        /// Fired when a rewarded ad is displayed.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdShowSuccess;
        /// <summary>
        /// Fired when a rewarded ad fails to display.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd, CloudXError> OnAdShowFailed;
        /// <summary>
        /// Fired when a rewarded ad is hidden after being shown.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdHidden;
        /// <summary>
        /// Fired when a rewarded ad is clicked.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd> OnAdClicked;
        /// <summary>
        /// Fired when the user earns the rewarded ad payout.
        /// This callback runs on the Unity main thread.
        /// </summary>
        public static event Action<CloudXAd, CloudXReward> OnAdRewarded;
        /// <summary>
        /// Fired as soon as impression revenue is available.
        /// On Android and iOS this callback may run off the Unity main thread, so handlers must not
        /// touch Unity APIs directly without switching threads first.
        /// </summary>
        public static event Action<CloudXAd> OnAdRevenuePaid;

        // Internal methods to trigger the events
        internal static void OnAdLoadSuccessInternal(CloudXAd cloudXAd)
        {
            OnAdLoadSuccess?.Invoke(cloudXAd);
        }

        internal static void OnAdLoadFailedInternal(string adUnitId, CloudXError cloudXError)
        {
            OnAdLoadFailed?.Invoke(adUnitId, cloudXError);
        }

        internal static void OnAdShowSuccessInternal(CloudXAd cloudXAd)
        {
            OnAdShowSuccess?.Invoke(cloudXAd);
        }

        internal static void OnAdShowFailedInternal(CloudXAd cloudXAd, CloudXError cloudXError)
        {
            OnAdShowFailed?.Invoke(cloudXAd, cloudXError);
        }

        internal static void OnAdHiddenInternal(CloudXAd cloudXAd)
        {
            OnAdHidden?.Invoke(cloudXAd);
        }

        internal static void OnAdClickedInternal(CloudXAd cloudXAd)
        {
            OnAdClicked?.Invoke(cloudXAd);
        }

        internal static void OnAdRewardedInternal(CloudXAd cloudXAd, CloudXReward cloudXReward)
        {
            OnAdRewarded?.Invoke(cloudXAd, cloudXReward);
        }

        internal static void OnAdRevenuePaidInternal(CloudXAd cloudXAd)
        {
            OnAdRevenuePaid?.Invoke(cloudXAd);
        }

        // Add a method to clear all events
        internal static void ResetEvents()
        {
            OnAdLoadSuccess = null;
            OnAdLoadFailed = null;
            OnAdShowSuccess = null;
            OnAdShowFailed = null;
            OnAdHidden = null;
            OnAdClicked = null;
            OnAdRewarded = null;
            OnAdRevenuePaid = null;
        }
    }

    // Reset all static events to null at the start of each Play Mode session
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents()
    {
        Banner.ResetEvents();
        Mrec.ResetEvents();
        Interstitial.ResetEvents();
        AppOpen.ResetEvents();
        Rewarded.ResetEvents();
    }
}
}
