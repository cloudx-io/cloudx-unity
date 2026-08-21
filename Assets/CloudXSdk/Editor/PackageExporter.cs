using System.IO;
using UnityEditor;

namespace CloudX.Editor
{
    /// <summary>
    /// Exports CloudXSdk and ExternalDependencyManager as a Unity package.
    /// Used by both CI (GameCI unity-builder) and local export scripts.
    /// </summary>
    public static class PackageExporter
    {
        private const string PackageJsonPath = "Assets/CloudXSdk/package.json";
        private const string OutputDirectory = "build";

        private static readonly string[] ExportPaths = {
            "Assets/CloudXSdk",
            "Assets/ExternalDependencyManager",
        };

        /// <summary>
        /// Entry point for CI builds via GameCI unity-builder.
        /// Reads version from package.json and exports to build/ directory.
        /// </summary>
        public static void Export()
        {
            var version = GetVersionFromPackageJson();
            var projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath) ?? "";
            var outputPath = Path.Combine(
                projectRoot,
                OutputDirectory,
                $"CloudXSdk-{version}.unitypackage"
            );

            ExportPackage(outputPath);

            UnityEngine.Debug.Log($"[PackageExporter] Successfully exported: {outputPath}");
        }

        /// <summary>
        /// Exports the package to the specified path.
        /// </summary>
        public static void ExportPackage(string outputPath)
        {
            // Ensure output directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AssetDatabase.ExportPackage(
                ExportPaths,
                outputPath,
                ExportPackageOptions.Recurse
            );
        }

        private static string GetVersionFromPackageJson()
        {
            var packageJsonFullPath = Path.Combine(
                Path.GetDirectoryName(UnityEngine.Application.dataPath) ?? "",
                PackageJsonPath
            );

            if (!File.Exists(packageJsonFullPath))
            {
                throw new FileNotFoundException($"package.json not found at: {packageJsonFullPath}");
            }

            var json = File.ReadAllText(packageJsonFullPath);

            // Simple JSON parsing without external dependencies
            // Looking for: "version": "x.y.z"
            var versionKey = "\"version\"";
            var versionStart = json.IndexOf(versionKey);
            if (versionStart == -1)
            {
                throw new System.Exception("version field not found in package.json");
            }

            var colonIndex = json.IndexOf(':', versionStart);
            var firstQuote = json.IndexOf('"', colonIndex);
            var secondQuote = json.IndexOf('"', firstQuote + 1);

            return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }
    }
}
