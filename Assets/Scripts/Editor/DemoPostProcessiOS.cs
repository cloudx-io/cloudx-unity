#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace CloudX.Demo.Editor
{
    /// <summary>
    /// Demo-app-only iOS post-processing: the Google Mobile Ads application id
    /// and the AppTrackingTransparency link. Both are demo-specific and neither
    /// belongs in the CloudXSdk package post-process.
    /// </summary>
    /*
     * 1. Google Mobile Ads asserts a GADApplicationIdentifier at process start
     *    (GADInvalidInitializationException otherwise). The demo only serves
     *    test AdMob inventory, so it ships Google's public TEST application id.
     *    Publisher apps set their own id.
     * 2. CLXDemoAttPlugin.m compiles into the UnityFramework target and calls
     *    ATTrackingManager, so AppTrackingTransparency.framework must be linked
     *    there. The SDK package never prompts for ATT and so never links it.
     */
    public static class DemoPostProcessiOS
    {
        private const string GoogleTestAppId = "ca-app-pub-3940256099942544~1458002511";
        private const string AppTrackingTransparencyFramework = "AppTrackingTransparency.framework";

        [PostProcessBuild(46)]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            LinkAppTrackingTransparency(buildPath);

            var plistPath = Path.Combine(buildPath, "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning($"[CloudXDemo] Info.plist not found at path: {plistPath}");
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            if (plist.root["GADApplicationIdentifier"] == null)
            {
                plist.root.SetString("GADApplicationIdentifier", GoogleTestAppId);
                plist.WriteToFile(plistPath);
                Debug.Log("[CloudXDemo] Added GADApplicationIdentifier (Google test app id) to Info.plist");
            }
        }

        /*
         * Linked against UnityFramework, not Unity-iPhone: Unity compiles
         * Assets/**\/Plugins/iOS sources into the UnityFramework target, so that
         * is where CLXDemoAttPlugin.m's ATTrackingManager symbols must resolve.
         * Linking the main target instead leaves UnityFramework with an
         * undefined symbol.
         *
         * Weak-linked because ATT is iOS 14+ while the project's pods declare a
         * 13.0 minimum; CLXDemoAttPlugin.m guards every call with
         * @available(iOS 14, *) to match.
         *
         * This runs at priority 46, before the CloudXSdk package post-process
         * (90), which re-reads the project from disk - so this edit survives.
         */
        private static void LinkAppTrackingTransparency(string buildPath)
        {
            var projectPath = PBXProject.GetPBXProjectPath(buildPath);
            if (!File.Exists(projectPath))
            {
                Debug.LogWarning($"[CloudXDemo] Xcode project not found at path: {projectPath}");
                return;
            }

            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();
            if (project.ContainsFramework(frameworkTargetGuid, AppTrackingTransparencyFramework))
            {
                return;
            }

            /* Third argument is weak: true. */
            project.AddFrameworkToProject(frameworkTargetGuid, AppTrackingTransparencyFramework, true);
            project.WriteToFile(projectPath);
            Debug.Log($"[CloudXDemo] Weak-linked {AppTrackingTransparencyFramework} into UnityFramework for the ATT prompt");
        }
    }
}
#endif
