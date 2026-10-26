#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HABIMON.Editor
{
    /// <summary>Produces a local validation APK using the project's enabled build scenes.</summary>
    public static class ChatbotAndroidBuildValidation
    {
        public static void BuildDebugApk()
        {
            string output = Environment.GetEnvironmentVariable("HABIMON_ANDROID_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(output))
                output = "/tmp/HABIMON-chatbot-android.apk";

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes are configured for the Android validation build.");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("Android validation build result: " + report.summary.result +
                "; output=" + output + "; errors=" + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Android validation build failed; inspect the Unity build log.");
        }
    }
}
#endif
