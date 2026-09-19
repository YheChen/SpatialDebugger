using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Meta.XR;
using SpatialDebugger.Annotations;
using SpatialDebugger.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace SpatialDebugger.EditorTools
{
    /// <summary>
    /// One-shot, idempotent project configuration for a Quest 3 build.
    /// </summary>
    /// <remarks>
    /// Meta's own Project Setup Tool cannot be driven from script --
    /// <c>OVRProjectSetup.FixTasks</c> is internal and
    /// <c>InternalsVisibleTo</c> only names Meta's assemblies -- so this
    /// reproduces the specific fixes this project needs, using public API
    /// only. Everything checks before it writes, so running it twice is safe.
    /// </remarks>
    public static class SpatialDebuggerProjectSetup
    {
        private const string XrSettingsFolder = "Assets/XR";
        private const string XrSettingsAsset = XrSettingsFolder + "/XRGeneralSettings.asset";
        private const string ConfigFolder = "Assets/SpatialDebugger/Resources";
        private const string ConfigAsset = ConfigFolder + "/SpatialDebuggerConfig.asset";
        private const string DefaultBundleId = "com.htn2026.spatialdebugger";

        private const string OpenXrLoaderTypeName = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string MetaFeatureSetId = "com.meta.openxr.featureset.metaxr";

        [MenuItem("Tools/SpatialDebugger/1. Configure Project", priority = 10)]
        public static void ConfigureProject()
        {
            var report = new List<string>();

            report.Add(EnsureConfigAsset());
            report.Add(EnsureTextMeshProResources());
            report.Add(EnsureAlwaysIncludedShaders());
            report.Add(ConfigurePlayerSettings());
            report.Add(ConfigureXrLoader());
            report.Add(ConfigureOpenXrFeatures());
            report.Add(ConfigureOculusProjectConfig());
            report.Add(RegenerateAndroidManifest());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SpatialDebugger] Project configuration:\n  " +
                      string.Join("\n  ", report.Where(line => !string.IsNullOrEmpty(line))));
        }

        // -- config asset --------------------------------------------------

        private static string EnsureConfigAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SpatialDebuggerConfig>(ConfigAsset);
            if (existing != null) return "config asset: already present";

            Directory.CreateDirectory(ConfigFolder);

            var config = ScriptableObject.CreateInstance<SpatialDebuggerConfig>();
            config.EditorSetBackend(GuessLanAddress(), 8000);

            AssetDatabase.CreateAsset(config, ConfigAsset);
            return "config asset: created at " + ConfigAsset + " (backend " + config.BaseUrl + ")";
        }

        /// <summary>
        /// Best-effort LAN address of this Mac, so the default is at least
        /// plausible. The user can still change it in-headset.
        /// </summary>
        private static string GuessLanAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var address in host.AddressList)
                {
                    if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                    var text = address.ToString();
                    if (text.StartsWith("127.")) continue;
                    return text;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[SpatialDebugger] could not determine a LAN address: " +
                                 exception.Message);
            }

            return "192.168.1.100";
        }

        // -- TextMeshPro ---------------------------------------------------

        /// <summary>
        /// Imports the TextMeshPro Essential Resources if they are missing.
        /// </summary>
        /// <remarks>
        /// Without them <c>TMP_Settings.defaultFontAsset</c> is null and every
        /// <see cref="TMPro.TextMeshPro"/> renders nothing at all. That is why
        /// <see cref="Annotations.SpatialText"/> falls back to legacy TextMesh —
        /// this import is a quality improvement, not a prerequisite.
        /// <para>
        /// <c>TMP_PackageResourceImporter.ImportResources</c> is tried first
        /// because it knows where the package lives, then
        /// <see cref="AssetDatabase.ImportPackage"/> directly. Both are async
        /// in batch mode, so this verifies the result and says plainly when the
        /// import has not landed rather than reporting a success it cannot see.
        /// </para>
        /// </remarks>
        private static string EnsureTextMeshProResources()
        {
            if (TextMeshProResourcesPresent()) return "TMP resources: already imported";

            try
            {
                TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[SpatialDebugger] TMP_PackageResourceImporter failed: " +
                                 exception.Message);
            }

            if (TextMeshProResourcesPresent()) return "TMP resources: imported";

            // Second route: import the .unitypackage the package ships, directly.
            var packagePath = FindTextMeshProEssentialsPackage();
            if (packagePath != null)
            {
                try
                {
                    AssetDatabase.ImportPackage(packagePath, false);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[SpatialDebugger] AssetDatabase.ImportPackage failed: " +
                                     exception.Message);
                }
            }

            if (TextMeshProResourcesPresent()) return "TMP resources: imported";

            // Not fatal: SpatialText falls back to legacy TextMesh, so text
            // still renders. TMP is only nicer.
            return "TMP resources: not imported (text falls back to legacy TextMesh and still " +
                   "renders). For nicer text use Window > TextMeshPro > Import TMP Essential " +
                   "Resources, then re-run this tool.";
        }

        private static bool TextMeshProResourcesPresent()
        {
            // The settings asset is what TMP actually needs; the folder name has
            // moved between versions, so look for the asset rather than a path.
            return AssetDatabase.FindAssets("t:TMP_Settings").Length > 0 &&
                   AssetDatabase.FindAssets("t:TMP_FontAsset").Length > 0;
        }

        private static string FindTextMeshProEssentialsPackage()
        {
            foreach (var root in new[] { "Packages/com.unity.ugui", "Packages/com.unity.textmeshpro" })
            {
                var candidate = Path.Combine(root, "Package Resources",
                    "TMP Essential Resources.unitypackage");
                if (File.Exists(candidate)) return candidate;
            }

            // Fall back to the resolved package location on disk.
            try
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                    "Packages/com.unity.ugui/package.json");
                if (info != null)
                {
                    var candidate = Path.Combine(info.resolvedPath, "Package Resources",
                        "TMP Essential Resources.unitypackage");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch (Exception)
            {
                // Nothing useful to do; the caller reports the failure.
            }

            return null;
        }

        // -- shaders -------------------------------------------------------

        /// <summary>
        /// Pins the shaders the annotation renderers look up at runtime into
        /// the build. <c>Shader.Find</c> returns null in a player for any
        /// shader nothing references, which would leave every annotation
        /// magenta on device but fine in the Editor.
        /// </summary>
        private static string EnsureAlwaysIncludedShaders()
        {
            var graphicsSettings = AssetDatabase.LoadAllAssetsAtPath(
                "ProjectSettings/GraphicsSettings.asset").FirstOrDefault();

            if (graphicsSettings == null) return "shaders: GraphicsSettings.asset not readable";

            var serialized = new SerializedObject(graphicsSettings);
            var included = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (included == null) return "shaders: m_AlwaysIncludedShaders not found";

            // Prune anything unbuildable first, so a bad entry (from an earlier
            // run or from elsewhere) self-heals instead of failing every build.
            var removed = new List<string>();
            for (var i = included.arraySize - 1; i >= 0; i--)
            {
                var shader = included.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader != null && !IsBuiltInDefaultResource(shader)) continue;

                removed.Add(shader != null ? shader.name : "(missing)");
                included.DeleteArrayElementAtIndex(i);
            }

            var present = new HashSet<string>();
            for (var i = 0; i < included.arraySize; i++)
            {
                var shader = included.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader != null) present.Add(shader.name);
            }

            var added = new List<string>();
            var skipped = new List<string>();
            foreach (var name in AnnotationVisuals.RequiredShaders)
            {
                if (present.Contains(name)) continue;

                var shader = Shader.Find(name);
                if (shader == null) continue; // not every fallback exists in every pipeline

                if (IsBuiltInDefaultResource(shader))
                {
                    skipped.Add(name);
                    continue;
                }

                included.InsertArrayElementAtIndex(included.arraySize);
                included.GetArrayElementAtIndex(included.arraySize - 1).objectReferenceValue = shader;
                added.Add(name);
            }

            if (added.Count == 0 && removed.Count == 0) return "shaders: already pinned";

            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var parts = new List<string>();
            if (added.Count > 0) parts.Add("pinned " + string.Join(", ", added));
            if (removed.Count > 0) parts.Add("removed unbuildable " + string.Join(", ", removed));
            if (skipped.Count > 0) parts.Add("skipped built-in " + string.Join(", ", skipped));
            return "shaders: " + string.Join("; ", parts);
        }

        /// <summary>
        /// True for shaders that live in Unity's internal default-resources
        /// bundle. They are flagged <see cref="HideFlags.DontSave"/>, and
        /// putting one in Always Included Shaders fails the player build with
        /// "An asset is marked with HideFlags.DontSave but is included in the
        /// build".
        /// </summary>
        private static bool IsBuiltInDefaultResource(Shader shader)
        {
            if (shader == null) return true;
            if ((shader.hideFlags & HideFlags.DontSave) != 0) return true;

            var path = AssetDatabase.GetAssetPath(shader);
            return path == "Library/unity default resources";
        }

        // -- player settings -----------------------------------------------

        private static string ConfigurePlayerSettings()
        {
            var changes = new List<string>();
            var android = NamedBuildTarget.Android;

            if (PlayerSettings.GetScriptingBackend(android) != ScriptingImplementation.IL2CPP)
            {
                PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
                changes.Add("IL2CPP");
            }

            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                changes.Add("ARM64");
            }

            // Auto can resolve to an SDK the store rejects; Meta v205 wants 34.
            if (PlayerSettings.Android.targetSdkVersion != (AndroidSdkVersions)34)
            {
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
                changes.Add("targetSdk 34");
            }

            if (PlayerSettings.Android.minSdkVersion < (AndroidSdkVersions)32)
            {
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;
                changes.Add("minSdk 32");
            }

            var identifier = PlayerSettings.GetApplicationIdentifier(android);
            if (string.IsNullOrEmpty(identifier) || identifier.Contains("urpblank") ||
                identifier.Contains("UnityTechnologies"))
            {
                PlayerSettings.SetApplicationIdentifier(android, DefaultBundleId);
                changes.Add("bundle id " + DefaultBundleId);
            }

            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.LandscapeLeft)
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                changes.Add("landscape-left");
            }

            // Passthrough needs a transparent eye buffer, which needs the
            // colour space and alpha to behave; linear is also what URP wants.
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                changes.Add("linear colour space");
            }

            return changes.Count == 0
                ? "player settings: already correct"
                : "player settings: set " + string.Join(", ", changes);
        }

        // -- XR loader -----------------------------------------------------

        /// <summary>
        /// Creates <c>Assets/XR/XRGeneralSettings.asset</c> and assigns the
        /// OpenXR loader for Android.
        /// </summary>
        /// <remarks>
        /// Without this, <c>XRGeneralSettings.Instance</c> is null at runtime
        /// and XR Management has nothing to start: the APK installs and
        /// launches, but as a flat 2D Android app.
        /// </remarks>
        private static string ConfigureXrLoader()
        {
            var perTarget = GetOrCreatePerBuildTargetSettings();
            if (perTarget == null) return "XR loader: FAILED to create settings asset";

            const BuildTargetGroup group = BuildTargetGroup.Android;

            if (!perTarget.HasManagerSettingsForBuildTarget(group))
            {
                perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            }

            var manager = perTarget.ManagerSettingsForBuildTarget(group);
            if (manager == null) return "XR loader: no XRManagerSettings for Android";

            var already = manager.activeLoaders.Any(
                loader => loader != null && loader.GetType().FullName == OpenXrLoaderTypeName);

            if (!already)
            {
                if (!XRPackageMetadataStore.AssignLoader(manager, OpenXrLoaderTypeName, group))
                {
                    return "XR loader: FAILED to assign OpenXRLoader";
                }
            }

            var settings = perTarget.SettingsForBuildTarget(group);
            if (settings != null && !settings.InitManagerOnStart)
            {
                settings.InitManagerOnStart = true;
            }

            EditorUtility.SetDirty(perTarget);
            AssetDatabase.SaveAssets();

            return already
                ? "XR loader: OpenXR already active for Android"
                : "XR loader: assigned OpenXRLoader for Android";
        }

        private static XRGeneralSettingsPerBuildTarget GetOrCreatePerBuildTargetSettings()
        {
            EditorBuildSettings.TryGetConfigObject(
                XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perTarget);

            if (perTarget != null) return perTarget;

            perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XrSettingsAsset);

            if (perTarget == null)
            {
                Directory.CreateDirectory(XrSettingsFolder);
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, XrSettingsAsset);
                AssetDatabase.SaveAssets();
            }

            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perTarget, true);
            return perTarget;
        }

        // -- OpenXR features -----------------------------------------------

        /// <summary>
        /// Enables the Meta XR feature set, the Meta XR feature itself, and the
        /// Oculus Touch interaction profile.
        /// </summary>
        /// <remarks>
        /// Meta v205 integrates with Quest as an OpenXR *feature*, not a loader,
        /// so without this the loader starts but none of the Meta functionality
        /// exists. Without the Touch profile, <c>OVRInput</c> silently reports
        /// nothing.
        /// <para>
        /// The subtlety that cost a failed Android build: OpenXR settings can
        /// end up holding <em>two</em> instances of the same feature for a build
        /// target. Meta's build hook resolves the feature through
        /// <see cref="FeatureHelpers.GetFeatureWithIdForBuildTarget"/> and gives
        /// up if what it finds is disabled — so enabling some other instance of
        /// the same feature is silently useless. Symptom: the Gradle build dies
        /// with "2 files found with path 'lib/arm64-v8a/libopenxr_loader.so'",
        /// because the hook that would have disabled Unity's copy never ran.
        /// Hence: enable every instance of the type, and then verify through the
        /// same lookup Meta uses.
        /// </para>
        /// </remarks>
        private static string ConfigureOpenXrFeatures()
        {
            const BuildTargetGroup group = BuildTargetGroup.Android;
            var notes = new List<string>();

            try
            {
                // Populate the Android feature list before touching anything.
                FeatureHelpers.RefreshFeatures(group);

                OpenXRFeatureSetManager.activeBuildTarget = group;
                var featureSet = OpenXRFeatureSetManager.GetFeatureSetWithId(group, MetaFeatureSetId);

                if (featureSet == null)
                {
                    notes.Add("Meta XR feature set NOT FOUND");
                }
                else if (!featureSet.isEnabled)
                {
                    featureSet.isEnabled = true;
                    OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(group);
                    notes.Add("enabled Meta XR feature set");
                }
                else
                {
                    notes.Add("Meta XR feature set already enabled");
                }

                // The feature set may have added instances; re-read.
                FeatureHelpers.RefreshFeatures(group);

                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (settings == null)
                {
                    notes.Add("no OpenXRSettings for Android");
                    return "OpenXR features: " + string.Join("; ", notes);
                }

                notes.Add(EnableEveryInstance<Meta.XR.MetaXRFeature>(settings, "MetaXRFeature"));
                notes.Add(EnableEveryInstance<OculusTouchControllerProfile>(
                    settings, "OculusTouchControllerProfile"));

                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();

                notes.Add(VerifyMetaFeatureResolves(group));
            }
            catch (Exception exception)
            {
                notes.Add("FAILED: " + exception.Message);
            }

            return "OpenXR features: " + string.Join("; ", notes);
        }

        /// <summary>
        /// Enables every instance of a feature type, so a duplicated instance
        /// cannot leave a disabled one where a lookup will find it.
        /// </summary>
        private static string EnableEveryInstance<TFeature>(OpenXRSettings settings, string label)
            where TFeature : OpenXRFeature
        {
            var instances = settings.GetFeatures<TFeature>();
            if (instances == null || instances.Length == 0) return label + ": NOT FOUND";

            var changed = 0;
            foreach (var feature in instances)
            {
                if (feature == null || feature.enabled) continue;
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
                changed++;
            }

            var suffix = instances.Length > 1 ? " (" + instances.Length + " instances)" : string.Empty;
            return changed == 0
                ? label + ": already enabled" + suffix
                : label + ": enabled " + changed + suffix;
        }

        /// <summary>
        /// Replicates Meta's own precondition check, so a misconfiguration is
        /// reported here rather than five minutes into a Gradle build.
        /// </summary>
        private static string VerifyMetaFeatureResolves(BuildTargetGroup group)
        {
            var resolved = FeatureHelpers.GetFeatureWithIdForBuildTarget(
                group, Meta.XR.MetaXRFeature.featureId);

            if (resolved == null)
            {
                return "VERIFY FAILED: MetaXRFeature does not resolve for Android";
            }

            if (!resolved.enabled)
            {
                return "VERIFY FAILED: the MetaXRFeature instance Meta's build hook resolves is " +
                       "DISABLED. The Android build will fail on duplicate libopenxr_loader.so. " +
                       "Delete Assets/XR and re-run this tool.";
            }

            return "verified: Meta's own lookup resolves an enabled MetaXRFeature";
        }

        // -- Meta project config -------------------------------------------

        /// <summary>
        /// Turns on the device features this app needs. Each of these also
        /// controls whether the corresponding permission is written into the
        /// Android manifest, so leaving one off silently disables the feature
        /// on device with no error.
        /// </summary>
        private static string ConfigureOculusProjectConfig()
        {
            var config = OVRProjectConfig.CachedProjectConfig;
            if (config == null) return "Meta project config: not available yet (re-run after import)";

            var changes = new List<string>();

            if (config.handTrackingSupport != OVRProjectConfig.HandTrackingSupport.ControllersAndHands)
            {
                config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
                changes.Add("hand tracking");
            }

            if (config.handTrackingFrequency != OVRProjectConfig.HandTrackingFrequency.HIGH)
            {
                config.handTrackingFrequency = OVRProjectConfig.HandTrackingFrequency.HIGH;
                changes.Add("hand frequency HIGH");
            }

            if (config.insightPassthroughSupport != OVRProjectConfig.FeatureSupport.Required)
            {
                config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Required;
                changes.Add("passthrough required");
            }

            // Scene + anchors are what MRUK needs; without them MRUK gets no
            // data and the USE_SCENE permission is never written.
            if (config.sceneSupport != OVRProjectConfig.FeatureSupport.Supported)
            {
                config.sceneSupport = OVRProjectConfig.FeatureSupport.Supported;
                changes.Add("scene support");
            }

            if (config.anchorSupport != OVRProjectConfig.AnchorSupport.Enabled)
            {
                config.anchorSupport = OVRProjectConfig.AnchorSupport.Enabled;
                changes.Add("anchor support");
            }

            if (!config.targetDeviceTypes.Contains(OVRProjectConfig.DeviceType.Quest3))
            {
                config.targetDeviceTypes.Add(OVRProjectConfig.DeviceType.Quest3);
                changes.Add("Quest 3 target");
            }

            if (!config.focusAware)
            {
                config.focusAware = true;
                changes.Add("focus aware");
            }

            if (changes.Count == 0) return "Meta project config: already correct";

            OVRProjectConfig.CommitProjectConfig(config);
            return "Meta project config: set " + string.Join(", ", changes);
        }

        private const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";

        /// <summary>
        /// Rewrites the Android manifest from the current Meta project config.
        /// </summary>
        /// <remarks>
        /// Must use <c>GenerateOrUpdateAndroidManifest(silentMode: true)</c>.
        /// The obvious-looking <c>GenerateManifestForSubmission()</c> calls the
        /// internal with <c>silentMode: false</c>, which puts up a "replace the
        /// existing manifest?" dialog — and in batch mode
        /// <see cref="EditorUtility.DisplayDialog"/> returns false, so it
        /// silently does nothing and reports success. That produced an APK with
        /// no hand-tracking permission that looked like it had built correctly.
        /// </remarks>
        private static string RegenerateAndroidManifest()
        {
            try
            {
                OVRManifestPreprocessor.GenerateOrUpdateAndroidManifest(silentMode: true);
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                return "AndroidManifest: FAILED (" + exception.Message +
                       ") - use Meta > Tools > Update AndroidManifest.xml";
            }

            return "AndroidManifest: updated; " + VerifyManifest();
        }

        /// <summary>
        /// Reads the manifest back and names anything missing, rather than
        /// trusting that the write did what it said.
        /// </summary>
        private static string VerifyManifest()
        {
            if (!File.Exists(ManifestPath)) return "VERIFY FAILED: " + ManifestPath + " is missing";

            string contents;
            try
            {
                contents = File.ReadAllText(ManifestPath);
            }
            catch (Exception exception)
            {
                return "VERIFY FAILED: could not read the manifest (" + exception.Message + ")";
            }

            var required = new Dictionary<string, string>
            {
                { "oculus.software.handtracking", "hand tracking feature" },
                { "com.oculus.permission.HAND_TRACKING", "hand tracking permission" },
                { "com.oculus.permission.USE_SCENE", "scene permission" },
                { "com.oculus.permission.USE_ANCHOR_API", "anchor permission" },
                { "com.oculus.intent.category.VR", "VR launch category" },
            };

            var missing = required
                .Where(entry => !contents.Contains(entry.Key))
                .Select(entry => entry.Value)
                .ToList();

            return missing.Count == 0
                ? "verified: hand tracking, scene, anchor and VR entries all present"
                : "VERIFY FAILED: manifest is missing " + string.Join(", ", missing);
        }
    }
}
