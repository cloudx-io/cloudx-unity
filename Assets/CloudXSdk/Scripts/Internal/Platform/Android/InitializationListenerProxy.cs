#nullable enable

using System;
using UnityEngine;
using CloudX.Internal.Threading;
// ReSharper disable InconsistentNaming

namespace CloudX
{
public class InitializationListenerProxy : AndroidJavaProxy
{
    private readonly Action<CloudXSdkConfiguration> _onSuccess;
    private readonly Action<CloudXError> _onFailure;

    public InitializationListenerProxy(
        Action<CloudXSdkConfiguration> onSuccess,
        Action<CloudXError> onFailure
    )
        : base("io.cloudx.sdk.CloudXInitializationListener")
    {
        CloudXSdk.Log.LogDebug(() => $"InitializationListenerProxy created");
        _onSuccess = onSuccess;
        _onFailure = onFailure;
    }

    // Called from Android when initialization succeeds (Android thread)
    public void onInitialized(AndroidJavaObject configuration)
    {
        // Convert on Android thread to minimize main thread work
        var sdkConfiguration = configuration.ToCloudXSdkConfiguration();
        CloudXSdk.Log.LogDebug(() => $"onInitialized callback received, configuration={sdkConfiguration}");

        // Dispatch to Unity main thread
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            _onSuccess(sdkConfiguration);
        });
    }

    // Called from Android when initialization fails (Android thread)
    public void onInitializationFailed(AndroidJavaObject cloudXErrorObject)
    {
        // Convert on Android thread to minimize main thread work
        var cloudXError = cloudXErrorObject.ToCloudXError();
        CloudXSdk.Log.LogDebug(() => $"onInitializationFailed callback received, error={cloudXError}");

        // Dispatch to Unity main thread
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            _onFailure(cloudXError);
        });
    }
}
}
