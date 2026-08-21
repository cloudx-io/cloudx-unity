using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CloudX.Editor
{
    /// <summary>
    /// Provides command-line Android build functionality via Gradle export.
    /// Usage: Unity -executeMethod CloudX.Editor.AndroidBuilder.ExportDevelopment
    /// </summary>
    public static class AndroidBuilder
    {
        /// <summary>
        /// Exports a development Gradle project with incremental build support.
        /// Enables development build features (profiler, script debugging).
        /// </summary>
        public static void ExportDevelopment() => ExportGradleProject(development: true);

        /// <summary>
        /// Exports a release Gradle project with incremental build support.
        /// Optimized build without development features.
        /// </summary>
        public static void ExportRelease() => ExportGradleProject(development: false);

        private static void ExportGradleProject(bool development)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new System.Exception("Failed to resolve Unity project root");
            }

            var outputPath = Path.Combine(projectRoot, "build", "android-project");
            var previousDirectory = Directory.GetCurrentDirectory();

            // Configure Unity to export as Gradle project instead of APK/AAB
            EditorUserBuildSettings.exportAsGoogleAndroidProject = true;

            // Always use incremental build for faster subsequent builds
            var buildOptions = BuildOptions.AcceptExternalModificationsToPlayer;
            if (development) buildOptions |= BuildOptions.Development;

            try
            {
                Directory.SetCurrentDirectory(projectRoot);

                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes
                        .Where(s => s.enabled)
                        .Select(s => s.path)
                        .ToArray(),
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    options = buildOptions,
                };

                var report = BuildPipeline.BuildPlayer(options);

                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new System.Exception($"Build failed: {report.summary.result}");
                }
            }
            finally
            {
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

                try
                {
                    Directory.SetCurrentDirectory(previousDirectory);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning(
                        $"[CloudX] Failed to restore working directory to '{previousDirectory}': {e.Message}");
                }
            }
        }
    }
}
