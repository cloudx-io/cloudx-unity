using System.Collections;
using UnityEngine;

namespace CloudX.Demo
{
    /*
     * Demo-only: reads this device's advertising ID so the demo can say whether the
     * device can be registered as a CloudX test device.
     *
     * Test mode is server-controlled -- a device serves test ads because its advertising
     * ID is on the dashboard's test-device list, not because of anything in this build.
     * Nothing in the SDK surfaces that ID, so without this the only way to find it is to
     * already know where to look.
     *
     * The case worth catching is the zeroed one. With tracking unauthorized, both
     * platforms still return an ID, but they return ZeroedId -- the same all-zeros UUID
     * on every opted-out device. It is a well-formed UUID, so it pastes into the
     * dashboard without complaint and then matches nothing, which presents as ordinary
     * no-fill and sends people to debug their integration instead of their consent flow.
     *
     * Like DemoAppTrackingiOS, this is demo scaffolding and must never move into the
     * CloudXSdk package.
     */
    public static class DemoAdvertisingId
    {
        /* What both platforms report when tracking is not authorized. */
        public const string ZeroedId = "00000000-0000-0000-0000-000000000000";

        public static string Id { get; private set; }
        public static bool TrackingEnabled { get; private set; }
        public static string Error { get; private set; }
        public static bool Resolved { get; private set; }

        /* A real ID that is worth registering on the dashboard. */
        public static bool IsUsable =>
            Resolved && string.IsNullOrEmpty(Error) && !string.IsNullOrEmpty(Id) && Id != ZeroedId;

        /*
         * Unity does not document which thread the callback arrives on, so it only
         * stores values; every caller reads them from the coroutine, i.e. on the Unity
         * thread. Yields until the value lands or the timeout expires, so a platform
         * that never calls back cannot hang the demo's startup.
         */
        public static IEnumerator Resolve(float timeoutSeconds = 3f)
        {
            if (Resolved)
            {
                yield break;
            }

            var started = Application.RequestAdvertisingIdentifierAsync(
                (id, trackingEnabled, error) =>
                {
                    Id = id;
                    TrackingEnabled = trackingEnabled;
                    Error = error;
                    Resolved = true;
                });

            if (!started)
            {
                /* No advertising ID on this platform -- the Editor, most notably. */
                Error = "not available on this platform";
                Resolved = true;
                yield break;
            }

            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!Resolved && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!Resolved)
            {
                Error = "timed out";
                Resolved = true;
            }
        }

        /* One line for the log: the full ID, which is the value to paste into the dashboard. */
        public static string Describe()
        {
            if (!Resolved)
            {
                return "not resolved yet";
            }

            if (!string.IsNullOrEmpty(Error))
            {
                return $"unavailable ({Error})";
            }

            if (!IsUsable)
            {
                return $"{Id} - ZEROED, cannot be whitelisted (tracking not authorized)";
            }

            return $"{Id} - register this on the CloudX dashboard to serve test ads";
        }

        /* Short enough for the on-screen status line, which is narrow in landscape. */
        public static string ShortStatus()
        {
            if (!Resolved)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(Error))
            {
                return "Ad ID unavailable";
            }

            return IsUsable ? "Ad ID ok" : "Ad ID zeroed - see log";
        }
    }
}
