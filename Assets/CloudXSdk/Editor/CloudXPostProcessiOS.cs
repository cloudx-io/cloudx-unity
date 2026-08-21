//
//  CloudXPostProcessiOS.cs
//  CloudX Unity SDK
//
//  iOS post-processing for CloudX SDK integration.
//  Handles CocoaPods installation and dynamic framework embedding.
//

#if UNITY_IOS || UNITY_IPHONE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;

namespace CloudX.Editor
{
    public class CloudXPostProcessiOS
    {
        // Priority 90: Run after EDM4U (60) but before Firebase (100)
        private const int EmbedFrameworksPriority = 90;
        private const string UserTrackingUsageDescriptionKey = "NSUserTrackingUsageDescription";
        private const string DefaultUserTrackingUsageDescription = "This uses device info for more personalized ads and content";
        private const string SkAdNetworkItemsKey = "SKAdNetworkItems";
        private const string SkAdNetworkIdentifierKey = "SKAdNetworkIdentifier";

        private static readonly string[] SkAdNetworkIds = {
            "cstr6suwn9.skadnetwork",
            "4fzdc2evr5.skadnetwork",
            "2fnua5tdw4.skadnetwork",
            "ydx93a7ass.skadnetwork",
            "p78axxw29g.skadnetwork",
            "v72qych5uu.skadnetwork",
            "ludvb6z3bs.skadnetwork",
            "cp8zw746q7.skadnetwork",
            "3sh42y64q3.skadnetwork",
            "c6k4g5qg8m.skadnetwork",
            "s39g8k73mm.skadnetwork",
            "3qy4746246.skadnetwork",
            "f38h382jlk.skadnetwork",
            "hs6bdukanm.skadnetwork",
            "mlmmfzh3r3.skadnetwork",
            "v4nxqhlyqp.skadnetwork",
            "wzmmz9fp6w.skadnetwork",
            "su67r6k2v3.skadnetwork",
            "yclnxrl5pm.skadnetwork",
            "t38b2kh725.skadnetwork",
            "7ug5zh24hu.skadnetwork",
            "gta9lk7p23.skadnetwork",
            "vutu7akeur.skadnetwork",
            "y5ghdn5j9k.skadnetwork",
            "v9wttpbfk9.skadnetwork",
            "n38lu8286q.skadnetwork",
            "47vhws6wlr.skadnetwork",
            "kbd757ywx3.skadnetwork",
            "9t245vhmpl.skadnetwork",
            "a2p9lx4jpn.skadnetwork",
            "22mmun2rn5.skadnetwork",
            "44jx6755aq.skadnetwork",
            "k674qkevps.skadnetwork",
            "4468km3ulz.skadnetwork",
            "2u9pt9hc89.skadnetwork",
            "8s468mfl3y.skadnetwork",
            "klf5c3l5u5.skadnetwork",
            "ppxm28t8ap.skadnetwork",
            "kbmxgpxpgc.skadnetwork",
            "uw77j35x4d.skadnetwork",
            "578prtvx9j.skadnetwork",
            "4dzt52r2t5.skadnetwork",
            "tl55sbb4fm.skadnetwork",
            "c3frkrj4fj.skadnetwork",
            "e5fvkxwrpn.skadnetwork",
            "8c4e2ghe7u.skadnetwork",
            "3rd42ekr43.skadnetwork",
            "97r2b46745.skadnetwork",
            "3qcr597p9d.skadnetwork",
            "v79kvwwj4g.skadnetwork",
            "wg4vff78zm.skadnetwork",
            "737z793b9f.skadnetwork",
            "9rd848q2bz.skadnetwork",
            "n6fk4nfna4.skadnetwork",
            "m8dbw4sv7c.skadnetwork",
            "av6w8kgt66.skadnetwork",
            "424m5254lk.skadnetwork",
            "4pfyvq9l8r.skadnetwork",
            "zmvfpc5aq8.skadnetwork",
            "ggvn48r87g.skadnetwork",
            "5lm9lj6jb7.skadnetwork",
            "f73kdq92p3.skadnetwork",
            "w9q455wk68.skadnetwork",
            "prcb7njmu6.skadnetwork",
            "5l3tpt7t6e.skadnetwork",
            "7rz58n8ntl.skadnetwork",
            "cg4yq2srnc.skadnetwork",
            "5a6flpkh64.skadnetwork",
            "pwa73g5rt2.skadnetwork",
            "9nlqeag3gk.skadnetwork",
            "cj5566h2ga.skadnetwork",
            "mtkv5xtk9e.skadnetwork",
            "g28c52eehv.skadnetwork",
            "glqzh8vgby.skadnetwork",
            "b9bk5wbcq9.skadnetwork",
            "294l99pt4k.skadnetwork",
            "523jb4fst2.skadnetwork",
            "mls7yz5dvl.skadnetwork",
            "74b6s63p6l.skadnetwork",
            "52fl2v3hgk.skadnetwork",
            "6g9af3uyq4.skadnetwork",
            "x5l83yy675.skadnetwork",
            "mqn7fxpca7.skadnetwork",
            "g6gcrrvk4p.skadnetwork",
            "r45fhb6rf7.skadnetwork",
            "ejvt5qm6ak.skadnetwork",
            "488r3q3dtq.skadnetwork",
            "55644vm79v.skadnetwork",
            "5tjdwbrq8w.skadnetwork",
            "nzq8sh4pbs.skadnetwork",
            "7fmhfwg9en.skadnetwork",
            "6v7lgmsu45.skadnetwork",
            "6yxyv74ff7.skadnetwork",
            "55y65gfgn7.skadnetwork",
            "qqp299437r.skadnetwork",
            "32z4fx6l9h.skadnetwork",
            "feyaarzu9v.skadnetwork",
            "252b5q8x7y.skadnetwork",
            "qu637u8glc.skadnetwork",
            "vhf287vqwu.skadnetwork",
            "a8cz6cu7e5.skadnetwork",
            "cwn433xbcr.skadnetwork",
            "fq6vru337s.skadnetwork",
            "87u5trcl3r.skadnetwork",
            "mj797d8u6f.skadnetwork",
            "dbu4b84rxf.skadnetwork",
            "238da6jt44.skadnetwork",
            "t6d3zquu66.skadnetwork",
            "zq492l623r.skadnetwork",
            "xga6mpmplv.skadnetwork",
            "tmhh9296z4.skadnetwork",
            "6xzpu9s2p8.skadnetwork",
            "fz2k2k5tej.skadnetwork",
            "g2y4y55b64.skadnetwork",
            "275upjj5gd.skadnetwork",
            "24zw6aqk47.skadnetwork",
            "44n7hlldy6.skadnetwork",
            "ecpz2srf59.skadnetwork",
            "bvpn9ufa9b.skadnetwork",
        };

        internal static IReadOnlyList<string> RequiredSkAdNetworkIds => SkAdNetworkIds;
        
        // Podfile configuration constants
        private const string TargetUnityIphonePodfileLine = "target 'Unity-iPhone' do";
        private const string UseFrameworksStaticPodfileLine = "use_frameworks! :linkage => :static";
        private const string UseFrameworksDynamicPodfileLine = "use_frameworks! :linkage => :dynamic";
        private const string UseFrameworksPodfileLine = "use_frameworks!";
        
        // Dynamic frameworks are now detected automatically at build time.
        // The script scans all xcframeworks in Pods and embeds any that are dynamically linked.
        // This automatically handles CloudX frameworks and any third-party dynamic frameworks.
        
        /// <summary>
        /// Post-process the Xcode project to ensure pods are installed and embed dynamic frameworks.
        /// 
        /// Embedding decision follows industry standard:
        /// - If Unity-iPhone target is NOT in Podfile → embed frameworks
        /// - If use_frameworks! :linkage => :static → embed frameworks
        /// - Otherwise → let CocoaPods handle embedding
        /// </summary>
        [PostProcessBuild(EmbedFrameworksPriority)]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string buildPath)
        {
            if (buildTarget != BuildTarget.iOS) return;
            
            // Ensure CocoaPods dependencies are installed
            RunPodInstallIfNeeded(buildPath);

            UpdateInfoPlist(buildPath);
            
            var projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            
            var unityMainTargetGuid = project.GetUnityMainTargetGuid();
            
            // Conditionally embed dynamic frameworks based on Podfile configuration
            EmbedDynamicFrameworksIfNeeded(buildPath, project, unityMainTargetGuid);
            
            // Add Swift support (required by some dependencies)
            AddSwiftSupport(buildPath, project, project.GetUnityFrameworkTargetGuid(), unityMainTargetGuid);
            
            project.WriteToFile(projectPath);
        }

        /// <summary>
        /// Adds required CloudX values to Info.plist without replacing publisher configuration.
        /// </summary>
        private static void UpdateInfoPlist(string buildPath)
        {
            var plistPath = Path.Combine(buildPath, "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning($"[CloudX] Info.plist not found at path: {plistPath}");
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var changed = false;

            if (plist.root[UserTrackingUsageDescriptionKey] == null)
            {
                plist.root.SetString(UserTrackingUsageDescriptionKey, DefaultUserTrackingUsageDescription);
                changed = true;
                Debug.Log("[CloudX] Added NSUserTrackingUsageDescription to Info.plist");
            }

            var addedSkAdNetworkIds = AddRequiredSkAdNetworkIds(plist);
            changed |= addedSkAdNetworkIds > 0;
            if (addedSkAdNetworkIds > 0)
            {
                Debug.Log($"[CloudX] Added {addedSkAdNetworkIds} SKAdNetwork identifiers to Info.plist");
            }

            if (changed)
            {
                plist.WriteToFile(plistPath);
            }
        }

        internal static int AddRequiredSkAdNetworkIds(PlistDocument plist)
        {
            if (plist.root.values.TryGetValue(SkAdNetworkItemsKey, out var existingElement) &&
                !(existingElement is PlistElementArray))
            {
                throw new BuildFailedException(
                    $"[CloudX] {SkAdNetworkItemsKey} in Info.plist must be an array");
            }

            var skAdNetworkItems = existingElement as PlistElementArray ??
                plist.root.CreateArray(SkAdNetworkItemsKey);
            var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var element in skAdNetworkItems.values)
            {
                if (!(element is PlistElementDict item) ||
                    !item.values.TryGetValue(SkAdNetworkIdentifierKey, out var identifier) ||
                    !(identifier is PlistElementString identifierString) ||
                    string.IsNullOrWhiteSpace(identifierString.AsString()))
                {
                    continue;
                }

                existingIds.Add(identifierString.AsString().Trim());
            }

            var addedCount = 0;
            foreach (var identifier in SkAdNetworkIds)
            {
                if (!existingIds.Add(identifier))
                {
                    continue;
                }

                skAdNetworkItems.AddDict().SetString(SkAdNetworkIdentifierKey, identifier);
                addedCount++;
            }

            return addedCount;
        }
        
        /// <summary>
        /// Runs 'pod install' if the Pods directory doesn't exist or is out of date.
        /// This ensures publishers don't need to run terminal commands manually.
        /// </summary>
        private static void RunPodInstallIfNeeded(string buildPath)
        {
            var podfilePath = Path.Combine(buildPath, "Podfile");
            var podsDirectory = Path.Combine(buildPath, "Pods");
            var podfileLockPath = Path.Combine(buildPath, "Podfile.lock");
            
            if (!File.Exists(podfilePath))
            {
                Debug.Log("[CloudX] No Podfile found, skipping pod install");
                return;
            }
            
            // Check if we need to run pod install
            bool needsPodInstall = !Directory.Exists(podsDirectory);
            
            if (!needsPodInstall && File.Exists(podfileLockPath))
            {
                // Check if Podfile is newer than Podfile.lock
                var podfileTime = File.GetLastWriteTime(podfilePath);
                var lockTime = File.GetLastWriteTime(podfileLockPath);
                needsPodInstall = podfileTime > lockTime;
            }
            
            if (!needsPodInstall)
            {
                Debug.Log("[CloudX] Pods already installed and up to date");
                return;
            }
            
            Debug.Log("[CloudX] Running pod install...");
            
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/zsh",
                    Arguments = $"-l -c \"cd '{buildPath}' && pod install --repo-update 2>&1\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(300000); // 5 minute timeout
            
            if (process.ExitCode == 0)
            {
                Debug.Log("[CloudX] pod install completed successfully");
            }
            else
            {
                Debug.LogError($"[CloudX] pod install failed with exit code {process.ExitCode}");
                Debug.LogError($"[CloudX] Output: {output}");
                Debug.LogError($"[CloudX] Error: {error}");
                Debug.LogError($"[CloudX] You may need to run 'pod install' manually in: {buildPath}");
            }
        }
        
        /// <summary>
        /// Determines whether dynamic frameworks should be embedded based on Podfile configuration.
        /// 
        /// Decision matrix:
        /// |--------------------------------------------------------------------------------------|
        /// |                      | use_frameworks! :dynamic | use_frameworks! :static | no line |
        /// |----------------------|--------------------------|-------------------------|---------|
        /// | Unity-iPhone present | Don't embed              | Embed                   | Don't   |
        /// | Unity-iPhone absent  | Embed                    | Embed                   | Embed   |
        /// |--------------------------------------------------------------------------------------|
        /// </summary>
        private static bool ShouldEmbedDynamicFrameworks(string buildPath)
        {
            var podfilePath = Path.Combine(buildPath, "Podfile");
            if (!File.Exists(podfilePath)) return false;
            
            var lines = File.ReadAllLines(podfilePath);
            
            // If Podfile doesn't have Unity-iPhone target, we should embed
            var containsUnityIphoneTarget = lines.Any(line => line.Contains(TargetUnityIphonePodfileLine));
            if (!containsUnityIphoneTarget) return true;
            
            // If static linkage is specified, we should embed
            var hasStaticLinkage = Array.FindIndex(lines, line => line.Contains(UseFrameworksStaticPodfileLine));
            if (hasStaticLinkage == -1) return false;
            
            // Check if static linkage is the last (active) use_frameworks line
            var dynamicIndex = Array.FindIndex(lines, line => line.Contains(UseFrameworksDynamicPodfileLine));
            var plainIndex = Array.FindIndex(lines, line => line.Trim() == UseFrameworksPodfileLine);
            
            return plainIndex < hasStaticLinkage && dynamicIndex < hasStaticLinkage;
        }
        
        /// <summary>
        /// Embeds all dynamic frameworks from Pods if needed based on Podfile configuration.
        /// Dynamically detects which frameworks are dynamic (vs static) and embeds them.
        /// This handles CloudX frameworks and any third-party dynamic frameworks from adapter dependencies.
        /// </summary>
        private static void EmbedDynamicFrameworksIfNeeded(string buildPath, PBXProject project, string targetGuid)
        {
            var podsDirectory = Path.Combine(buildPath, "Pods");
            if (!Directory.Exists(podsDirectory)) return;
            
            if (!ShouldEmbedDynamicFrameworks(buildPath))
            {
                Debug.Log("[CloudX] Skipping framework embedding - CocoaPods will handle it");
                return;
            }
            
            Debug.Log("[CloudX] Finding and embedding dynamic frameworks...");
            
            // Find all xcframeworks in Pods
            var xcframeworks = Directory.GetDirectories(podsDirectory, "*.xcframework", SearchOption.AllDirectories);
            
            foreach (var xcframeworkPath in xcframeworks)
            {
                var frameworkName = Path.GetFileNameWithoutExtension(xcframeworkPath);

                if (!IsValidXcframeworkBundle(xcframeworkPath))
                {
                    Debug.Log($"[CloudX] Skipping {frameworkName} (invalid xcframework: missing Info.plist)");
                    continue;
                }
                
                // Check if this is a dynamic framework
                if (!IsDynamicFramework(xcframeworkPath))
                {
                    Debug.Log($"[CloudX] Skipping {frameworkName} (static library)");
                    continue;
                }
                
                var relativePath = xcframeworkPath.Substring(buildPath.Length + 1);
                
                // Try to find existing file reference first (CocoaPods may have created one)
                var existingGuid = project.FindFileGuidByProjectPath(relativePath);
                
                if (!string.IsNullOrEmpty(existingGuid))
                {
                    // Reuse existing file reference
                    project.AddFileToEmbedFrameworks(targetGuid, existingGuid);
                    Debug.Log($"[CloudX] Embedded {frameworkName} (reused existing reference)");
                }
                else
                {
                    // Create new file reference
                    var fileGuid = project.AddFile(relativePath, relativePath);
                    project.AddFileToEmbedFrameworks(targetGuid, fileGuid);
                    Debug.Log($"[CloudX] Embedded {frameworkName} (created new reference)");
                }
            }
        }

        /// <summary>Checks that a discovered xcframework directory has a root Info.plist manifest.</summary>
        private static bool IsValidXcframeworkBundle(string xcframeworkPath)
        {
            return File.Exists(Path.Combine(xcframeworkPath, "Info.plist"));
        }
        
        /// <summary>
        /// Checks if an xcframework contains a dynamic library (vs static archive).
        /// Dynamic frameworks need to be embedded, static ones don't.
        /// </summary>
        private static bool IsDynamicFramework(string xcframeworkPath)
        {
            // Look for a .framework inside the xcframework
            var innerFrameworks = Directory.GetDirectories(xcframeworkPath, "*.framework", SearchOption.AllDirectories);
            if (innerFrameworks.Length == 0) return false;
            
            // Check the binary inside the first framework found
            var framework = innerFrameworks[0];
            var binaryName = Path.GetFileNameWithoutExtension(framework);
            var binaryPath = Path.Combine(framework, binaryName);
            
            if (!File.Exists(binaryPath)) return false;
            
            try
            {
                // Read first few bytes to check Mach-O header
                using (var fs = new FileStream(binaryPath, FileMode.Open, FileAccess.Read))
                {
                    // Check for FAT binary (universal) or regular Mach-O
                    var header = new byte[8];
                    if (fs.Read(header, 0, 8) < 8) return false;
                    
                    // FAT magic: 0xCAFEBABE or 0xBEBAFECA (big/little endian)
                    // For FAT binaries, we need to look deeper, so we'll use a simpler heuristic:
                    // Check file size - dynamic libraries are usually larger than static archives
                    // But more reliable: check for LC_ID_DYLIB load command presence
                    
                    // Simpler approach: read the file as text and look for dynamic markers
                    // Actually, let's just check if the file size suggests a dynamic lib
                    // Static archives have "!<arch>" magic at the start
                    
                    fs.Position = 0;
                    var magicBytes = new byte[8];
                    fs.Read(magicBytes, 0, 8);
                    
                    // Static archive starts with "!<arch>\n"
                    if (magicBytes[0] == '!' && magicBytes[1] == '<' && magicBytes[2] == 'a' && 
                        magicBytes[3] == 'r' && magicBytes[4] == 'c' && magicBytes[5] == 'h')
                    {
                        return false; // Static archive
                    }
                    
                    // If not a static archive, assume dynamic (Mach-O)
                    return true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CloudX] Could not determine if {Path.GetFileName(xcframeworkPath)} is dynamic: {e.Message}");
                // Default to trying to embed - worst case it fails silently
                return true;
            }
        }
        
        /// <summary>
        /// Finds a framework (.xcframework or .framework) in the Pods directory.
        /// </summary>
        private static string FindFramework(string podsDirectory, string frameworkName)
        {
            // Try xcframework first
            var xcframeworks = Directory.GetDirectories(podsDirectory, $"{frameworkName}.xcframework", SearchOption.AllDirectories);
            if (xcframeworks.Length > 0) return xcframeworks[0];
            
            // Fall back to regular framework
            var frameworks = Directory.GetDirectories(podsDirectory, $"{frameworkName}.framework", SearchOption.AllDirectories);
            if (frameworks.Length > 0) return frameworks[0];
            
            return null;
        }
        
        /// <summary>
        /// Adds Swift support to the project (required by some dependencies).
        /// </summary>
        private static void AddSwiftSupport(string buildPath, PBXProject project, string frameworkTargetGuid, string mainTargetGuid)
        {
            var swiftFilePath = Path.Combine(buildPath, "Classes/CloudXSwiftSupport.swift");
            
            if (!File.Exists(swiftFilePath))
            {
                var swiftContent = @"//
//  CloudXSwiftSupport.swift
//  CloudX Unity SDK
//
//  This file ensures Swift support is enabled in the project.
//  It is automatically generated by CloudXPostProcessiOS.
//

import Foundation
";
                File.WriteAllText(swiftFilePath, swiftContent);
            }
            
            var swiftFileRelativePath = "Classes/CloudXSwiftSupport.swift";
            var swiftFileGuid = project.AddFile(swiftFileRelativePath, swiftFileRelativePath);
            project.AddFileToBuild(frameworkTargetGuid, swiftFileGuid);
            
            // Set Swift version if not already set
            var swiftVersion = project.GetBuildPropertyForAnyConfig(frameworkTargetGuid, "SWIFT_VERSION");
            if (string.IsNullOrEmpty(swiftVersion))
            {
                project.SetBuildProperty(frameworkTargetGuid, "SWIFT_VERSION", "5.0");
            }
            
            // Enable modules
            var enableModules = project.GetBuildPropertyForAnyConfig(frameworkTargetGuid, "CLANG_ENABLE_MODULES");
            if (string.IsNullOrEmpty(enableModules))
            {
                project.SetBuildProperty(frameworkTargetGuid, "CLANG_ENABLE_MODULES", "YES");
            }
            
            // Embed Swift standard libraries
            var alwaysEmbed = project.GetBuildPropertyForAnyConfig(mainTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES");
            if (string.IsNullOrEmpty(alwaysEmbed))
            {
                project.SetBuildProperty(mainTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            }
        }
    }
}
#endif
