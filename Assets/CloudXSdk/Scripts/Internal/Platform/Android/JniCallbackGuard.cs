#nullable enable

using System;

namespace CloudX
{
    /// <summary>
    /// Wraps the body of every AndroidJavaProxy callback so no C# exception (DTO conversion,
    /// dispatch, or anything else) propagates back through JNI into the Java caller, where Unity
    /// would rethrow it as a Java throwable inside the native SDK. Publisher handler failures are
    /// still logged closer to the handler by CallbackInvoker; this is the outer safety net.
    /// </summary>
    internal static class JniCallbackGuard
    {
        public static void Run(string callbackName, Action body)
        {
            try
            {
                body();
            }
            catch (Exception e)
            {
                CloudXSdk.Log.LogError(() =>
                    $"Error handling Android callback {callbackName}: {e.GetType().Name}: {e.Message}", e);
            }
        }
    }
}
