using System.Collections;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace CloudX.DemoAdvanced
{
    /// <summary>
    /// Demo-app App Tracking Transparency gate, iOS only. Requests ATT once per
    /// process and resolves before the demo initializes the CloudX SDK.
    /// </summary>
    /*
     * Why the demo owns this: the CloudX SDK reads ATT status but never asks for
     * it, and treats notDetermined the same as denied (no IDFA, dnt = 1). An app
     * that never prompts therefore sends every bid request as do-not-track,
     * which suppresses fill on physical devices. Publishers must call ATT
     * themselves; this is the demo doing exactly that.
     *
     * Any entry point that calls CloudXSdk.Initialize must yield on
     * EnsureRequested first. Here that is HomeScreen.Start. The gate is
     * idempotent, so gating every entry point costs nothing.
     */
    public static class DemoAppTrackingiOS
    {
        private const string TAG = "CloudXUnityDemoATT";

        /*
         * Upper bounds, not expected waits. The app is normally active within a
         * frame or two of Start, and the alert is answered in seconds. These
         * exist so a wrong assumption degrades to "ads without IDFA" rather than
         * "no ads at all".
         */
        private const float ActiveWaitTimeoutSeconds = 10f;
        private const float ResponseWaitTimeoutSeconds = 60f;

        /*
         * Short settle after UIApplication reports active. The native ObjC demo
         * uses a flat 1s from launch for the same reason.
         */
        private const float ActiveSettleDelaySeconds = 0.5f;

        /*
         * Mirrors ATTrackingManagerAuthorizationStatus for the iOS 14+ values.
         * The two negative values are CloudX sentinels: Unavailable (-1) matches
         * the native side and means "no ATT on this OS", while NotQueried (-2)
         * means the status has not been read yet. Both are distinct from
         * NotDetermined ("could ask, has not been asked").
         */
        public enum AttStatus
        {
            NotQueried = -2,
            Unavailable = -1,
            NotDetermined = 0,
            Restricted = 1,
            Denied = 2,
            Authorized = 3
        }

        private static AttStatus _status = AttStatus.NotQueried;

        /// <summary>
        /// Last known tracking authorization status.
        /// </summary>
        public static AttStatus Status => _status;

        /// <summary>
        /// Whether the SDK can request a tracked auction. Platforms without ATT
        /// (Android, the Editor, pre-iOS 14) report Unavailable and are never
        /// opted out, so only an explicit refusal is unusable.
        /// </summary>
        public static bool IsUsable(AttStatus status) =>
            status == AttStatus.Authorized || status == AttStatus.Unavailable;

        private static void Log(string message) => Debug.Log($"[{TAG}] {message}");

#if UNITY_IOS && !UNITY_EDITOR
        private delegate void CLXDemoAttCallback(int status);

        [DllImport("__Internal")]
        private static extern bool _CLXDemoAttIsActive();

        [DllImport("__Internal")]
        private static extern int _CLXDemoAttStatus();

        [DllImport("__Internal")]
        private static extern void _CLXDemoAttRequest(CLXDemoAttCallback callback);

        private static bool _resolved;
        private static bool _callbackReceived;

        /*
         * True while a request is in flight. Without it two coroutines entering
         * together would both pass the _resolved check and both call
         * _CLXDemoAttRequest, and the second's _callbackReceived reset would
         * strand the first until its response timeout.
         */
        private static bool _inFlight;

        /*
         * Rooted for the process lifetime. Passing OnAttResolved directly would
         * create a temporary delegate that GC can collect while the user is still
         * reading the alert, before the native completion handler fires.
         */
        private static readonly CLXDemoAttCallback AttCallback = OnAttResolved;
#endif

        /// <summary>
        /// Resolves tracking authorization, prompting the user only when the
        /// status is still undetermined. Safe to yield on repeatedly; the prompt
        /// is shown at most once per install.
        /// </summary>
        public static IEnumerator EnsureRequested()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (_resolved)
            {
                yield break;
            }

            if (_inFlight)
            {
                /*
                 * Another caller is already asking; wait for its answer rather
                 * than issuing a second request. Bounded, because the coroutine
                 * that owns the request can be stopped with its GameObject and
                 * would then never clear the flag.
                 */
                var inFlightDeadline = Time.realtimeSinceStartup +
                    ActiveWaitTimeoutSeconds + ActiveSettleDelaySeconds + ResponseWaitTimeoutSeconds;
                while (_inFlight && Time.realtimeSinceStartup < inFlightDeadline)
                {
                    yield return null;
                }

                yield break;
            }

            _status = (AttStatus)_CLXDemoAttStatus();

            if (_status != AttStatus.NotDetermined)
            {
                /*
                 * Already answered on a previous launch, or ATT is unavailable.
                 * Re-requesting would not present the alert anyway.
                 */
                _resolved = true;
                Log($"ATT already resolved: {_status}");
                yield break;
            }

            /*
             * iOS silently declines to present the alert unless UIApplication is
             * in the foreground active state, returning the current status
             * instead - indistinguishable from never having asked. Poll the real
             * application state; Unity's Application.isFocused goes true while
             * UIApplication is still inactive during launch and is not a usable
             * proxy for this.
             *
             * The settle delay after active mirrors the native ObjC demo, which
             * waits a second past launch before asking.
             *
             * Both waits below are bounded. Never initializing the SDK is a
             * worse outcome than asking at a bad moment or proceeding without an
             * answer.
             */
            _inFlight = true;
            var activeDeadline = Time.realtimeSinceStartup + ActiveWaitTimeoutSeconds;
            bool isActive;
            while (!(isActive = _CLXDemoAttIsActive()) && Time.realtimeSinceStartup < activeDeadline)
            {
                yield return null;
            }

            if (!isActive)
            {
                Log($"App not active after {ActiveWaitTimeoutSeconds}s, requesting ATT anyway");
            }

            yield return new WaitForSecondsRealtime(ActiveSettleDelaySeconds);

            _callbackReceived = false;
            Log("Requesting ATT authorization");
            _CLXDemoAttRequest(AttCallback);

            var responseDeadline = Time.realtimeSinceStartup + ResponseWaitTimeoutSeconds;
            while (!_callbackReceived && Time.realtimeSinceStartup < responseDeadline)
            {
                yield return null;
            }

            _inFlight = false;

            if (!_callbackReceived)
            {
                /*
                 * Stop blocking initialization on an answer that is not coming.
                 * _resolved stays false on purpose: the status is still
                 * notDetermined, so a later caller - or the next process launch
                 * - asks again rather than inheriting a non-answer.
                 */
                Log($"No ATT response within {ResponseWaitTimeoutSeconds}s, continuing without it");
                yield break;
            }

            _resolved = true;
            Log($"ATT request completed: {_status}");
#else
            if (_status == AttStatus.NotQueried)
            {
                _status = AttStatus.Unavailable;
                Log("ATT not available on this platform, skipping request");
            }

            yield break;
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [MonoPInvokeCallback(typeof(CLXDemoAttCallback))]
        private static void OnAttResolved(int status)
        {
            _status = (AttStatus)status;
            _callbackReceived = true;
        }
#endif
    }
}
