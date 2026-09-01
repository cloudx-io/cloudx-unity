#nullable enable

using System;
using System.Threading;

namespace CloudX.Internal.Threading
{
    /// <summary>
    /// Single decision point for which thread a native SDK callback is delivered on, and the
    /// safety net that keeps a throwing publisher handler from escaping back into native code.
    /// Semantics follow <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/>.
    /// </summary>
    internal static class CallbackDispatcher
    {
        internal const string ThreadingHint =
            "Handlers that use Unity APIs must run on the Unity main thread; set CloudXSdk.InvokeEventsOnUnityMainThread = true or marshal manually.";

        // Interlocked int rather than bool: set from native callback threads, reset from the main thread.
        private static int _warnedMissingDispatcher;

        /*
         * Re-arms the once-only missing-dispatcher error. Called by
         * UnityMainThreadDispatcher.RegisterInstance (in practice once per process, since nothing
         * recreates a dispatcher after a mid-session destroy) and by tests to make the log
         * expectation deterministic.
         */
        internal static void ResetMissingDispatcherWarning()
        {
            Interlocked.Exchange(ref _warnedMissingDispatcher, 0);
        }

        /// <summary>
        /// Resolves the effective thread for one callback. <paramref name="keepInBackground"/> is
        /// the per-event default used when the publisher has not set
        /// <see cref="CloudXSdk.InvokeEventsOnUnityMainThread"/>.
        /// </summary>
        internal static bool ShouldRunOnUnityMainThread(bool keepInBackground)
        {
            var mode = CloudXSdk.InvokeEventsOnUnityMainThread;
            return mode == null ? !keepInBackground : mode.Value;
        }

        /// <summary>
        /// Invokes <paramref name="invoke"/> on the Unity main thread or inline on the calling
        /// (native callback) thread, depending on <see cref="ShouldRunOnUnityMainThread"/>.
        /// Never drops the callback: if the main thread is requested but the dispatcher does not
        /// exist, the callback runs inline and an error is logged once (re-armed if a dispatcher
        /// registers again).
        /// </summary>
        public static void Dispatch(string eventName, bool keepInBackground, Action invoke)
        {
            if (ShouldRunOnUnityMainThread(keepInBackground))
            {
                if (UnityMainThreadDispatcher.TryEnqueue(() => Guarded(eventName, invoke)))
                {
                    return;
                }

                if (Interlocked.CompareExchange(ref _warnedMissingDispatcher, 1, 0) == 0)
                {
                    /*
                     * Error, not warning: the dispatcher is created before any scene loads, so
                     * reaching this line means it was destroyed - a defect the publisher must see
                     * at the default (Error) log level.
                     */
                    CloudXSdk.Log.LogError(() =>
                        $"UnityMainThreadDispatcher does not exist; delivering {eventName} inline on the native callback thread.");
                }
            }

            Guarded(eventName, invoke);
        }

        private static void Guarded(string eventName, Action invoke)
        {
            try
            {
                invoke();
            }
            catch (Exception e)
            {
                CloudXSdk.Log.LogError(() => FormatPublisherEventFailure(eventName, null, e), e);
            }
        }

        /// <summary>
        /// One log line shape for every failed publisher callback, with or without a known handler.
        /// </summary>
        internal static string FormatPublisherEventFailure(string eventName, string? handler, Exception e)
        {
            var handlerPart = handler == null ? "" : $"handler {handler}, ";
            return $"Caught exception in publisher event: {eventName}, {handlerPart}{ThreadDescription()}. {ThreadingHint} exception: {e.GetType().Name}: {e.Message}";
        }

        /// <summary>
        /// Human-readable statement of the current thread relative to the Unity main thread.
        /// </summary>
        internal static string ThreadDescription()
        {
            var mainThreadId = UnityMainThreadDispatcher.MainThreadId;
            if (mainThreadId == null)
            {
                return $"delivered on managed thread {Thread.CurrentThread.ManagedThreadId} (Unity main thread unknown)";
            }

            return mainThreadId == Thread.CurrentThread.ManagedThreadId
                ? "delivered on the Unity main thread"
                : $"delivered OFF the Unity main thread (managed thread {Thread.CurrentThread.ManagedThreadId})";
        }
    }
}
