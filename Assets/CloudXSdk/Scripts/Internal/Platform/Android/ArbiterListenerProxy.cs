#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using CloudX.Internal.Threading;

namespace CloudX
{
internal sealed class ArbiterListenerProxy : AndroidJavaProxy
{
    private readonly Action<CloudXArbiterResult> _onCompleted;

    public ArbiterListenerProxy(Action<CloudXArbiterResult> onCompleted)
        : base("io.cloudx.sdk.CloudXArbiterListener")
    {
        _onCompleted = onCompleted;
    }

    public void onCompleted(AndroidJavaObject result) => JniCallbackGuard.Run(OnCompletedName, () =>
    {
        CloudXSdk.Log.LogDebug(() => "ArbiterListenerProxy.onCompleted received");
        var arbiterResult = result.ToCloudXArbiterResult();
        CallbackDispatcher.Dispatch(OnCompletedName, keepInBackground: false,
            () => _onCompleted(arbiterResult));
    });

    private const string OnCompletedName = "CloudXArbiterListener.onCompleted";
}
}
