#if UNITY_ANDROID
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;
using Debug = UnityEngine.Debug;

/*
 * AGP 8.9.3 needs Gradle 8.11.1. Unity 6000.0 ships 8.11, so File > Build
 * fails unless External Tools points at the project wrapper dist.
 * Uses only ./gradlew (downloads 8.11.1). Does not read Homebrew or PATH.
 */
[InitializeOnLoad]
internal static class AndroidGradleVersion
{
    private const string Tag = "CloudXUnityDemo";
    private const string RequiredDistribution = "gradle-8.11.1";

    static AndroidGradleVersion()
    {
        EditorApplication.delayCall += EnsureWrapperGradle;
    }

    private static void EnsureWrapperGradle()
    {
        if (PointsAtRequiredDistribution(AndroidExternalToolsSettings.gradlePath))
            return;

        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
        {
            Debug.LogError($"[{Tag}] Could not resolve Unity project root for Gradle wrapper.");
            return;
        }

        var gradleHome = FindWrapperDistribution() ?? DownloadWrapperDistribution(projectRoot);
        if (string.IsNullOrEmpty(gradleHome) || !PointsAtRequiredDistribution(gradleHome))
        {
            Debug.LogError(
                $"[{Tag}] Gradle {RequiredDistribution} is not available. " +
                "Run ./gradlew --version from the project root, then reopen the Editor.");
            return;
        }

        AndroidExternalToolsSettings.gradlePath = gradleHome;
        Debug.Log($"[{Tag}] Unity Android Gradle set to {gradleHome}");
    }

    private static bool PointsAtRequiredDistribution(string gradlePath)
    {
        if (string.IsNullOrEmpty(gradlePath))
            return false;
        if (gradlePath.IndexOf(RequiredDistribution, StringComparison.Ordinal) < 0)
            return false;
        return File.Exists(GradleLauncher(gradlePath));
    }

    private static string GradleLauncher(string gradleHome)
    {
        var unix = Path.Combine(gradleHome, "bin", "gradle");
        return File.Exists(unix) ? unix : Path.Combine(gradleHome, "bin", "gradle.bat");
    }

    private static string FindWrapperDistribution()
    {
        var userHome = Environment.GetEnvironmentVariable("GRADLE_USER_HOME");
        if (string.IsNullOrEmpty(userHome))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            userHome = Path.Combine(home, ".gradle");
        }

        var distRoot = Path.Combine(userHome, "wrapper", "dists", $"{RequiredDistribution}-bin");
        if (!Directory.Exists(distRoot))
            return null;

        foreach (var hashDir in Directory.GetDirectories(distRoot))
        {
            var candidate = Path.Combine(hashDir, RequiredDistribution);
            if (PointsAtRequiredDistribution(candidate))
                return candidate;
        }

        return null;
    }

    private static string DownloadWrapperDistribution(string projectRoot)
    {
        var wrapperName = Application.platform == RuntimePlatform.WindowsEditor
            ? "gradlew.bat"
            : "gradlew";
        var wrapper = Path.Combine(projectRoot, wrapperName);
        if (!File.Exists(wrapper))
        {
            Debug.LogError($"[{Tag}] Gradle wrapper not found at {wrapper}");
            return null;
        }

        /*
         * Do not exec the wrapper as FileName. Windows CoreCLR cannot launch a
         * .bat with UseShellExecute=false, and a zip extract may drop +x on
         * Unix gradlew. Invoke through the platform shell so both cases work
         * with spaces in the project path.
         */
        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/c \"" + wrapper + "\" --version";
        }
        else
        {
            startInfo.FileName = "/bin/sh";
            startInfo.Arguments = "\"" + wrapper + "\" --version";
        }

        try
        {
            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                    return null;
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Debug.LogError($"[{Tag}] {wrapperName} --version exited {process.ExitCode}");
                    return null;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[{Tag}] Failed to run Gradle wrapper: {e.Message}");
            return null;
        }

        return FindWrapperDistribution();
    }
}
#endif
