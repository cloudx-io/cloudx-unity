using System;
using System.Collections;
using System.Threading;
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
     * The case worth catching is the zeroed one. An opted-out device still returns an ID,
     * but it returns ZeroedId -- the same all-zeros UUID on every opted-out device. It is
     * a well-formed UUID, so it pastes into the dashboard without complaint and then
     * matches nothing, which presents as ordinary no-fill and sends people to debug their
     * integration instead of their consent flow.
     *
     * There is no one call that covers both platforms. Unity documents
     * Application.RequestAdvertisingIdentifierAsync as "an advertising ID for iOS and
     * UWP" -- it dropped the Android implementation years ago, and on Android the call
     * returns false rather than the GAID. So only iOS uses that API, and Android asks
     * Google Play services for AdvertisingIdClient directly. Keeping the two apart is
     * also what lets each explain a zeroed ID in its own terms, which are not the same
     * terms.
     *
     * Like DemoAppTrackingiOS, this is demo scaffolding and must never move into the
     * CloudXSdk package.
     */
    public static class DemoAdvertisingId
    {
        /* What both platforms report when the device is opted out. */
        public const string ZeroedId = "00000000-0000-0000-0000-000000000000";

        /*
         * Written by the resolver -- on Android from a worker thread, on iOS from a
         * callback whose thread Unity does not document -- and read from the coroutine,
         * i.e. on the Unity thread. _resolved is volatile and is written last, so a
         * reader that sees it true also sees the three values behind it.
         */
        private static volatile bool _resolved;
        private static string _id;
        private static bool _trackingEnabled;
        private static string _error;

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        /*
         * Whether the platform call has been issued. Only ever touched on the Unity
         * thread, and it is what keeps a second caller -- or a first caller retrying
         * after a timeout -- from starting a second worker. GeneralScene and
         * FirstLookScene are separate scenes today so only one ever runs, but the ATT
         * gate needs the same guard and this one costs nothing.
         */
        private static bool _started;
#endif

        public static string Id => _id;
        public static bool TrackingEnabled => _trackingEnabled;
        public static string Error => _error;
        public static bool Resolved => _resolved;

        /* A real ID that is worth registering on the dashboard. */
        public static bool IsUsable =>
            _resolved && string.IsNullOrEmpty(_error) && !string.IsNullOrEmpty(_id) && _id != ZeroedId;

        /*
         * Yields until the ID lands or the timeout expires, so a platform that never
         * answers cannot hang the demo's startup. On timeout the state is deliberately
         * left unresolved rather than marked failed: the answer usually arrives a moment
         * later, and ShortStatus picks it up when initialization reports back.
         */
        public static IEnumerator Resolve(float timeoutSeconds = 3f)
        {
            if (_resolved)
            {
                yield break;
            }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (!_started)
            {
                _started = true;
                StartPlatformResolve();
            }

            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!_resolved && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
#else
            /* Assigned explicitly so the Editor build does not warn them unassigned. */
            _id = null;
            _trackingEnabled = false;
            _error = "no advertising ID in the Editor";
            _resolved = true;
            yield break;
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        /*
         * iOS only. This call is what the Android path exists to replace: Unity documents
         * it as "an advertising ID for iOS and UWP" and returns false for it on Android,
         * which reads as "no advertising ID on this device" and is not what is happening.
         */
        private static void StartPlatformResolve()
        {
            var started = Application.RequestAdvertisingIdentifierAsync(
                (id, trackingEnabled, error) =>
                {
                    _id = id;
                    _trackingEnabled = trackingEnabled;
                    _error = error;
                    _resolved = true;
                });

            if (!started)
            {
                _error = "the platform declined to report one";
                _resolved = true;
            }
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        /*
         * AdvertisingIdClient.getAdvertisingIdInfo binds to Google Play services and
         * throws IllegalStateException if it is called on the main thread, so it runs on a
         * worker. The thread has to be attached to the JVM by hand for JNI to work, and
         * every AndroidJavaObject it creates is disposed on that same thread -- only
         * plain strings and bools cross back.
         *
         * Background so a Play services bind that never returns cannot hold the process
         * open, and fire-and-forget because the coroutine above is what waits.
         */
        private static void StartPlatformResolve()
        {
            var worker = new Thread(() =>
            {
                AndroidJNI.AttachCurrentThread();
                try
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    using var client =
                        new AndroidJavaClass("com.google.android.gms.ads.identifier.AdvertisingIdClient");
                    using var info = client.CallStatic<AndroidJavaObject>("getAdvertisingIdInfo", activity);

                    _id = info.Call<string>("getId");
                    /*
                     * Limit ad tracking is the pre-Android-12 form of opting out: the ID
                     * stays real but must not be used for ads. From Android 12 the ID
                     * itself zeroes instead, so on a modern device this is normally false
                     * whenever _id is usable.
                     */
                    _trackingEnabled = !info.Call<bool>("isLimitAdTrackingEnabled");
                }
                catch (Exception e)
                {
                    _error = DescribeAndroidFailure(e);
                }
                finally
                {
                    /* Last, and volatile, so the reader sees the values above it. */
                    _resolved = true;
                    AndroidJNI.DetachCurrentThread();
                }
            })
            {
                IsBackground = true,
                Name = "CloudXUnityDemoAdvertisingId"
            };

            worker.Start();
        }

        /*
         * Unity surfaces a Java throwable as AndroidJavaException whose message opens with
         * the Java class name, so match on that. Each of these is a different thing to go
         * and fix, which "unavailable" on its own would not tell anyone.
         */
        private static string DescribeAndroidFailure(Exception e)
        {
            var message = e.Message ?? string.Empty;

            if (message.Contains("GooglePlayServicesNotAvailableException"))
            {
                return "no Google Play services on this device";
            }

            if (message.Contains("GooglePlayServicesRepairableException"))
            {
                return "Google Play services needs updating";
            }

            if (message.Contains("ClassNotFoundException") || message.Contains("NoClassDefFoundError"))
            {
                return "play-services-ads-identifier is not on the classpath";
            }

            if (message.Contains("IOException"))
            {
                return "could not reach Google Play services";
            }

            return message.Length == 0 ? e.GetType().Name : message;
        }
#endif

        /*
         * One line for the log: the full ID, which is the value to paste into the
         * dashboard, plus what to do about it.
         */
        public static string Describe()
        {
            if (!_resolved)
            {
                return "not resolved yet";
            }

            if (!string.IsNullOrEmpty(_error))
            {
                return $"unavailable ({_error})";
            }

            if (string.IsNullOrEmpty(_id))
            {
                return "unavailable (no ID reported)";
            }

            if (_id == ZeroedId)
            {
                return $"{_id} - ZEROED, cannot be whitelisted ({ZeroedCause()})";
            }

            if (!_trackingEnabled)
            {
                /*
                 * A real ID the device has told us not to use for ads -- the
                 * pre-Android-12 form of opting out. Registering it works, so this is not
                 * the zeroed case, but fill will still look broken for a reason that has
                 * nothing to do with the dashboard.
                 */
                return $"{_id} - registerable, but this device is opted out of tracking, " +
                       "so bid requests go out as do-not-track and will not fill";
            }

            return $"{_id} - register this on the CloudX dashboard to serve test ads";
        }

        /*
         * The two platforms zero the ID for different reasons and are fixed in different
         * places, so say which one applies rather than the union of both.
         */
        private static string ZeroedCause()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return "turn on ad personalization in Settings > Google > Ads, and check the app declares " +
                   "com.google.android.gms.permission.AD_ID";
#elif UNITY_IOS && !UNITY_EDITOR
            return "App Tracking Transparency was not authorized; reinstall to be asked again";
#else
            return "the device is opted out of tracking";
#endif
        }

        /* Short enough for the on-screen status line, which is narrow in landscape. */
        public static string ShortStatus()
        {
            if (!_resolved)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(_error) || string.IsNullOrEmpty(_id))
            {
                return "Ad ID unavailable";
            }

            if (_id == ZeroedId)
            {
                return "Ad ID zeroed - see log";
            }

            return _trackingEnabled ? "Ad ID ok" : "Ad ID no-track - see log";
        }
    }
}
