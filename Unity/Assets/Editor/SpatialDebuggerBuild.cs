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
            var scenes = ResolveScenes();
            if (scenes == null) return null;

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

        /// <summary>
        /// The scenes to build, with the generated MR scene guaranteed first.
        /// </summary>
        /// <remarks>
        /// Deliberately does NOT trust <see cref="EditorBuildSettings.scenes"/>.
        /// A Unity Editor left open on the project writes its in-memory build
        /// list back over ProjectSettings, which is exactly how an APK once
        /// shipped containing only the stock URP <c>SampleScene</c>: XR started
        /// correctly, but the headset showed a flat grey-floor/blue-sky template
        /// scene with no camera rig, no passthrough and no hand tracking —
        /// while the build log said "building ... from 1 scene(s)".
        /// <para>
        /// The generated scene is the application. If it is missing, that is a
        /// hard error, not something to paper over.
        /// </para>
        /// </remarks>
        private static string[] ResolveScenes()
        {
            var generated = SpatialDebuggerSceneSetup.ScenePath;

            if (!File.Exists(generated))
            {
                Debug.LogError("[SpatialDebugger] " + generated + " does not exist. " +
                               "Run Tools > SpatialDebugger > 2. Build MR Scene first.");
                return null;
            }

            var others = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .Where(path => path != generated && File.Exists(path))
                .ToArray();

            var scenes = new[] { generated }.Concat(others).ToArray();

            if (others.Length > 0)
            {
                Debug.LogWarning("[SpatialDebugger] build settings also list " +
                                 string.Join(", ", others) +
                                 ". Forcing " + generated + " to scene 0 so it is what launches.");
            }

            return scenes;
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
