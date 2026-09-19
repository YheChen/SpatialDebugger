using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SpatialDebugger.EditorTools
{
    /// <summary>
    /// Headless Android build, for the CLI and for CI.
    /// </summary>
    /// <remarks>
    /// Invoke with:
    /// <code>
    /// Unity -batchmode -nographics -quit -projectPath Unity \
    ///   -logFile build.log \
    ///   -executeMethod SpatialDebugger.EditorTools.SpatialDebuggerBuild.BuildAndroid
    /// </code>
    /// Note that compile errors never reach <see cref="BuildReport"/>: if
    /// scripts fail to compile, <c>-executeMethod</c> is never called at all
    /// and Unity only prints "Scripts have compiler errors." So a green build
    /// log means both things passed.
    /// </remarks>
    public static class SpatialDebuggerBuild
    {
        private const string DefaultOutput = "Build/Android/SpatialDebugger.apk";

        [MenuItem("Tools/SpatialDebugger/3. Build Android APK", priority = 30)]
        public static void BuildAndroidMenu()
        {
            var report = Build(DefaultOutput);
            if (report != null && report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(DefaultOutput);
            }
        }

        /// <summary>CLI entry point. Exits non-zero on failure.</summary>
        public static void BuildAndroid()
        {
            var output = ArgumentValue("-sdOutput") ?? DefaultOutput;
            var report = Build(output);

            var succeeded = report != null && report.summary.result == BuildResult.Succeeded;
            if (succeeded) return;

            // -quit alone would exit 0 even after a failed build.
            EditorApplication.Exit(1);
        }

        private static BuildReport Build(string output)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                // Fall back to the generated scene rather than building an
                // empty player that looks like it worked.
                if (File.Exists(SpatialDebuggerSceneSetup.ScenePath))
                {
                    scenes = new[] { SpatialDebuggerSceneSetup.ScenePath };
                }
                else
                {
                    Debug.LogError("[SpatialDebugger] no scenes in build settings and " +
                                   SpatialDebuggerSceneSetup.ScenePath + " does not exist. " +
                                   "Run Tools > SpatialDebugger > 2. Build MR Scene first.");
                    return null;
                }
            }

            // BuildPipeline does not reliably create missing parent folders.
            var directory = Path.GetDirectoryName(Path.GetFullPath(output));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[SpatialDebugger] switching active build target to Android");
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android, BuildTarget.Android);
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            Debug.Log("[SpatialDebugger] building " + output + " from " +
                      scenes.Length + " scene(s): " + string.Join(", ", scenes));

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            catch (Exception exception)
            {
                Debug.LogError("[SpatialDebugger] build threw: " + exception);
                return null;
            }

            var summary = report.summary;
            Debug.Log("[SpatialDebugger] BUILD " + summary.result +
                      " | errors=" + summary.totalErrors +
                      " warnings=" + summary.totalWarnings +
                      " size=" + summary.totalSize + " bytes" +
                      " time=" + summary.totalTime +
                      " output=" + summary.outputPath);

            if (summary.result != BuildResult.Succeeded)
            {
                foreach (var step in report.steps)
                {
                    foreach (var message in step.messages)
                    {
                        if (message.type == LogType.Error || message.type == LogType.Exception)
                        {
                            Debug.LogError("[SpatialDebugger] build step '" + step.name + "': " +
                                           message.content);
                        }
                    }
                }
            }

            return report;
        }

        private static string ArgumentValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }
    }
}
