using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CloudX.Editor
{
    /// <summary>
    /// Provides command-line iOS build functionality via Xcode project export.
    /// Usage: Unity -executeMethod CloudX.Editor.iOSBuilder.ExportDevelopment
    /// </summary>
    public static class iOSBuilder
    {
        /// <summary>
        /// Exports a development Xcode project.
        /// Enables development build features (profiler, script debugging).
        /// </summary>
        public static void ExportDevelopment() => ExportXcodeProject(development: true);

        /// <summary>
        /// Exports a release Xcode project.
        /// Optimized build without development features.
        /// </summary>
        public static void ExportRelease() => ExportXcodeProject(development: false);

        private static void ExportXcodeProject(bool development)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new System.Exception("Failed to resolve Unity project root");
            }

            var outputPath = Path.Combine(projectRoot, "build", "ios-project");
            var previousDirectory = Directory.GetCurrentDirectory();

            var buildOptions = BuildOptions.None;
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
                    target = BuildTarget.iOS,
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
