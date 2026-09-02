#nullable enable

using System;

namespace CloudX.Internal.Threading
{
    /// <summary>
    /// Invokes every subscriber of a public CloudX event in isolation: one throwing handler is
    /// logged at ERROR with its declaring type and method, and the remaining handlers still run.
    /// </summary>
    internal static class CallbackInvoker
    {
        public static void Invoke<T>(Action<T>? handlers, T arg, string eventName)
        {
            if (handlers == null) return;
            foreach (var handler in handlers.GetInvocationList())
            {
                Guarded(eventName, handler, () => ((Action<T>)handler)(arg));
            }
        }

        public static void Invoke<T1, T2>(Action<T1, T2>? handlers, T1 arg1, T2 arg2, string eventName)
        {
            if (handlers == null) return;
            foreach (var handler in handlers.GetInvocationList())
            {
                Guarded(eventName, handler, () => ((Action<T1, T2>)handler)(arg1, arg2));
            }
        }

        private static void Guarded(string eventName, Delegate handler, Action invoke)
        {
            try
            {
                invoke();
            }
            catch (Exception e)
            {
                var method = handler.Method;
                CloudXSdk.Log.LogError(() => CallbackDispatcher.FormatPublisherEventFailure(
                    eventName, $"{method.DeclaringType?.FullName}.{method.Name}", e), e);
            }
        }
    }
}
