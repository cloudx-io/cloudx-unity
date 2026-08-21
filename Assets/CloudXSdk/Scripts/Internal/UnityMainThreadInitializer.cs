using UnityEngine;
using CloudX.Internal.Threading;

namespace CloudX
{
    /// <summary>
    /// Ensures UnityMainThreadDispatcher is initialized before any JNI callbacks fire.
    /// This guarantees that all Android native callbacks are dispatched to Unity's main thread.
    /// Initializes automatically before any scene loads - no manual setup required.
    /// </summary>
    internal class UnityMainThreadInitializer : MonoBehaviour
    {
        private static UnityMainThreadInitializer instance;
        private static UnityMainThreadDispatcher dispatcher;

        /// <summary>
        /// Gets the UnityMainThreadDispatcher instance for dispatching callbacks to the main thread
        /// </summary>
        public static UnityMainThreadDispatcher Dispatcher => dispatcher;

        /// <summary>
        /// Unity automatically calls this method when the game starts, before any scene loads.
        /// No manual setup required by SDK consumers.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (instance != null) return;

            // First ensure dispatcher exists
            if (!UnityMainThreadDispatcher.Exists())
            {
                GameObject dispatcherGo = new GameObject("UnityMainThreadDispatcher");
                dispatcher = dispatcherGo.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(dispatcherGo);

                CloudXSdk.Log.LogDebug(() => "[UnityMainThreadInitializer] Created UnityMainThreadDispatcher");
            }
            else
            {
                dispatcher = UnityMainThreadDispatcher.Instance();
                CloudXSdk.Log.LogDebug(() => "[UnityMainThreadInitializer] Found existing UnityMainThreadDispatcher");
            }

            // Then create our initializer
            GameObject go = new GameObject("UnityMainThreadInitializer");
            instance = go.AddComponent<UnityMainThreadInitializer>();
            DontDestroyOnLoad(go);

            CloudXSdk.Log.LogDebug(() => "[UnityMainThreadInitializer] Auto-initialized");
        }
    }
}

