using System;
using UnityEditor;
using UnityEngine;

namespace SpatialDebugger.EditorTools
{
    /// <summary>
    /// Imports TextMeshPro's Essential Resources from the command line.
    /// </summary>
    /// <remarks>
    /// <c>AssetDatabase.ImportPackage</c> is asynchronous. Under
    /// <c>-batchmode -quit</c> the editor tears down before the import lands, so
    /// it silently does nothing — which is how a build shipped with no text at
    /// all: <see cref="Annotations.SpatialText"/> fell back to legacy
    /// <see cref="TextMesh"/>, whose font material needs
    /// <c>GUI/Text Shader</c>, and that shader cannot be added to Always
    /// Included Shaders (it is <c>HideFlags.DontSave</c> and fails the build).
    /// Stripped shader, invisible labels.
    /// <para>
    /// So: run this WITHOUT <c>-quit</c>. It subscribes to the completion
    /// callbacks and exits the editor itself once the import has actually
    /// happened.
    /// </para>
    /// </remarks>
    public static class SpatialDebuggerTmpImport
    {
        /// <summary>CLI entry point. Exits 0 on success, 1 on failure.</summary>
        public static void ImportTextMeshProEssentials()
        {
            if (ResourcesPresent())
            {
                Debug.Log("[SpatialDebugger] TMP essential resources already present.");
                EditorApplication.Exit(0);
                return;
            }

            AssetDatabase.importPackageCompleted += OnCompleted;
            AssetDatabase.importPackageFailed += OnFailed;
            AssetDatabase.importPackageCancelled += OnCancelled;

            try
            {
                Debug.Log("[SpatialDebugger] importing TMP essential resources...");
                TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
            }
            catch (Exception exception)
            {
                Debug.LogError("[SpatialDebugger] TMP import threw: " + exception);
                EditorApplication.Exit(1);
            }
        }

        private static bool ResourcesPresent()
        {
            return AssetDatabase.FindAssets("t:TMP_Settings").Length > 0 &&
                   AssetDatabase.FindAssets("t:TMP_FontAsset").Length > 0;
        }

        private static void OnCompleted(string packageName)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var settings = AssetDatabase.FindAssets("t:TMP_Settings").Length;
            var fonts = AssetDatabase.FindAssets("t:TMP_FontAsset").Length;

            Debug.Log("[SpatialDebugger] TMP import completed: '" + packageName +
                      "' -> TMP_Settings=" + settings + " TMP_FontAsset=" + fonts);

            // Report the truth: a completed import that produced no font asset
            // is still a failure for our purposes.
            EditorApplication.Exit(settings > 0 && fonts > 0 ? 0 : 1);
        }

        private static void OnFailed(string packageName, string error)
        {
            Debug.LogError("[SpatialDebugger] TMP import FAILED: '" + packageName + "': " + error);
            EditorApplication.Exit(1);
        }

        private static void OnCancelled(string packageName)
        {
            Debug.LogError("[SpatialDebugger] TMP import CANCELLED: '" + packageName + "'");
            EditorApplication.Exit(1);
        }
    }
}
