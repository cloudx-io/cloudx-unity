#nullable enable

using System;
using UnityEngine;

namespace CloudX
{
    /// <summary>
    /// Callbacks for SDK initialization lifecycle events.
    /// Subscribe to these events before calling CloudXSdk.Initialize().
    /// </summary>
    public static class CloudXInitializationCallbacks
    {
        /// <summary>
        /// Fired when the SDK has been successfully initialized.
        /// </summary>
        public static event Action<CloudXSdkConfiguration>? OnSdkInitializedEvent;

        /// <summary>
        /// Fired when SDK initialization has failed.
        /// </summary>
        public static event Action<CloudXError>? OnSdkInitializationFailedEvent;

        /// <summary>
        /// Internal method to trigger the OnSdkInitializedEvent.
        /// </summary>
        internal static void OnSdkInitializedInternal(CloudXSdkConfiguration configuration)
        {
            OnSdkInitializedEvent?.Invoke(configuration);
        }

        /// <summary>
        /// Internal method to trigger the OnSdkInitializationFailedEvent.
        /// </summary>
        internal static void OnSdkInitializationFailedInternal(CloudXError error)
        {
            OnSdkInitializationFailedEvent?.Invoke(error);
        }

        /// <summary>
        /// Reset all static events to null at the start of each Play Mode session.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents()
        {
            OnSdkInitializedEvent = null;
            OnSdkInitializationFailedEvent = null;
        }
    }
}
