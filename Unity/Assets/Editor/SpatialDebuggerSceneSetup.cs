using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpatialDebugger.AI;
using SpatialDebugger.Annotations;
using SpatialDebugger.Demo;
using SpatialDebugger.Interaction;
using SpatialDebugger.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpatialDebugger.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/Scenes/SpatialDebugger.unity</c> from scratch.
    /// </summary>
    /// <remarks>
    /// Scene YAML is never hand-edited: the scene is a build output of this
    /// script, so it can be regenerated whenever the SDK or the code moves.
    /// Re-running replaces the scene, which is what makes it idempotent.
    /// </remarks>
    public static class SpatialDebuggerSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/SpatialDebugger.unity";

        /// <summary>
        /// The control panel is debug UI. Off for the clean pinch-to-annotate
        /// demo; flip to true to get the buttons back.
        /// </summary>
        private const bool ShowDebugPanel = false;

        private const string CameraRigPrefab =
            "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        private const string HandPrefab =
            "Packages/com.meta.xr.sdk.core/Prefabs/OVRHandPrefab.prefab";

        [MenuItem("Tools/SpatialDebugger/2. Build MR Scene", priority = 20)]
        public static void BuildScene()
        {
            var log = new List<string>();

            // Project configuration first: the scene wants the config asset and
            // TMP resources to already exist.
            SpatialDebuggerProjectSetup.ConfigureProject();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting(scene, log);
            var rig = BuildCameraRig(scene, log);
            var systems = BuildSystems(scene, rig, log);
            BuildPanel(scene, systems, log);
            BuildEditorTestSurface(scene, log);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("[SpatialDebugger] failed to save " + ScenePath);
                return;
            }

            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.Add("saved " + ScenePath + " and set it as build scene 0");
            Debug.Log("[SpatialDebugger] Scene setup:\n  " + string.Join("\n  ", log));
        }

        // -- lighting ------------------------------------------------------

        private static void BuildLighting(Scene scene, List<string> log)
        {
            var go = new GameObject("Directional Light");
            SceneManager.MoveGameObjectToScene(go, scene);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            log.Add("light: directional, no shadows");
        }

        // -- camera rig ----------------------------------------------------

        private static GameObject BuildCameraRig(Scene scene, List<string> log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraRigPrefab);
            if (prefab == null)
            {
                log.Add("camera rig: PREFAB NOT FOUND at " + CameraRigPrefab);
                return null;
            }

            var rig = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (rig == null)
            {
                log.Add("camera rig: instantiation FAILED");
                return null;
            }

            rig.name = "OVRCameraRig";
            log.Add("camera rig: instantiated " + CameraRigPrefab);

            ConfigureManager(rig, log);
            ConfigurePassthrough(rig, log);
            ConfigureCenterEyeCamera(rig, log);
            AttachHands(rig, log);

            return rig;
        }

        private static void ConfigureManager(GameObject rig, List<string> log)
        {
            var manager = rig.GetComponentInChildren<OVRManager>(true);
            if (manager == null)
            {
                log.Add("OVRManager: NOT FOUND on the rig");
                return;
            }

            var serialized = new SerializedObject(manager);

            // isInsightPassthroughEnabled is a plain public field that
            // OVRManager polls every frame.
            SetBool(serialized, "isInsightPassthroughEnabled", true);

            // Floor level so "0.15m above the target" means what it says,
            // relative to the room rather than to wherever the head started.
            SetEnum(serialized, "_trackingOriginType", (int)OVRManager.TrackingOrigin.FloorLevel);

            // Passthrough needs the guardian out of the way.
            SetBool(serialized, "shouldBoundaryVisibilityBeSuppressed", true);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            log.Add("OVRManager: passthrough on, floor-level tracking origin");
        }

        private static void ConfigurePassthrough(GameObject rig, List<string> log)
        {
            if (rig.GetComponent<OVRPassthroughLayer>() != null)
            {
                log.Add("passthrough layer: already present");
                return;
            }

            // overlayType defaults to Underlay, which is the only mode Meta is
            // carrying forward, so it is deliberately left alone (the property
            // is [Obsolete]).
            var layer = rig.AddComponent<OVRPassthroughLayer>();
            layer.textureOpacity = 1f;

            log.Add("passthrough layer: added to the rig root (underlay)");
        }

        /// <summary>
        /// Passthrough shows through the eye buffer only where the buffer is
        /// transparent, so the centre-eye camera must clear to a fully
        /// transparent colour. Getting this wrong yields a black void with the
        /// annotations floating in it -- and it looks correct in the Editor.
        /// </summary>
        private static void ConfigureCenterEyeCamera(GameObject rig, List<string> log)
        {
            var anchor = rig.transform.Find("TrackingSpace/CenterEyeAnchor");
            if (anchor == null)
            {
                log.Add("centre-eye camera: TrackingSpace/CenterEyeAnchor NOT FOUND");
                return;
            }

            var camera = anchor.GetComponent<Camera>();
            if (camera == null)
            {
                log.Add("centre-eye camera: no Camera component");
                return;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;

            if (!anchor.gameObject.CompareTag("MainCamera"))
            {
                anchor.gameObject.tag = "MainCamera";
            }

            EditorUtility.SetDirty(camera);
            log.Add("centre-eye camera: clears to transparent, tagged MainCamera");
        }

        private static void AttachHands(GameObject rig, List<string> log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandPrefab);
            if (prefab == null)
            {
                log.Add("hands: PREFAB NOT FOUND at " + HandPrefab);
                return;
            }

            AttachHand(rig, prefab, "TrackingSpace/LeftHandAnchor", true, log);
            AttachHand(rig, prefab, "TrackingSpace/RightHandAnchor", false, log);
        }

        /// <summary>
        /// Handedness lives in serialized fields that are internal or protected
        /// to the Oculus.VR assembly (<c>HandType</c>, <c>_skeletonType</c>,
        /// <c>_meshType</c>), so it is set through SerializedObject rather than
        /// by assignment. The skeleton/mesh variants are the OpenXR ones
        /// because this project's OVRRuntimeSettings selects the OpenXR hand
        /// skeleton.
        /// </summary>
        private static void AttachHand(GameObject rig, GameObject prefab, string anchorPath,
            bool left, List<string> log)
        {
            var anchor = rig.transform.Find(anchorPath);
            if (anchor == null)
            {
                log.Add("hands: anchor " + anchorPath + " NOT FOUND");
                return;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, anchor) as GameObject;
            if (instance == null)
            {
                log.Add("hands: instantiation FAILED for " + anchorPath);
                return;
            }

            instance.name = left ? "OVRHandLeft" : "OVRHandRight";

            var wantedHand = (int)(left ? OVRHand.Hand.HandLeft : OVRHand.Hand.HandRight);
            var wantedSkeleton = (int)(left ? OVRSkeleton.SkeletonType.XRHandLeft
                                            : OVRSkeleton.SkeletonType.XRHandRight);
            var wantedMesh = (int)(left ? OVRMesh.MeshType.XRHandLeft
                                        : OVRMesh.MeshType.XRHandRight);

            var results = new List<string>();
            results.Add(SetAndVerifyEnum(instance.GetComponent<OVRHand>(), "HandType", wantedHand));
            results.Add(SetAndVerifyEnum(instance.GetComponent<OVRSkeleton>(), "_skeletonType",
                wantedSkeleton));
            results.Add(SetAndVerifyEnum(instance.GetComponent<OVRMesh>(), "_meshType", wantedMesh));

            log.Add("hands: " + instance.name + " attached to " + anchorPath + " [" +
                    string.Join(", ", results) + "]");
        }

        /// <summary>
        /// Writes a serialized enum field and reads it straight back.
        /// </summary>
        /// <remarks>
        /// Handedness lives in fields that are internal or protected to the
        /// Oculus.VR assembly, so it can only be set through SerializedObject —
        /// which means a typo'd field name, or a write that does not stick to a
        /// prefab instance, fails silently and leaves a hand configured as
        /// <c>Hand.None</c>. On device that is simply a hand that never tracks.
        /// So: verify.
        /// <para>
        /// The numbers are not what they look like. These enums take their
        /// values from <c>OVRPlugin</c>, where <c>None = -1</c>, so
        /// <c>HandLeft = 0</c> and <c>HandRight = 1</c>, and
        /// <c>SkeletonType.XRHandLeft = 4</c>, <c>XRHandRight = 5</c>. A
        /// serialized <c>HandType: 0</c> therefore means <em>HandLeft</em>, not
        /// None. Compare against <c>intValue</c> (the underlying value, which is
        /// what a C# cast gives) and never against <c>enumValueIndex</c> (the
        /// position in the declaration), or everything looks off by one.
        /// </para>
        /// </remarks>
        private static string SetAndVerifyEnum(Component target, string field, int value)
        {
            if (target == null) return field + ": COMPONENT MISSING";

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) return field + ": FIELD NOT FOUND";

            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Re-read through a fresh SerializedObject: a stale one would just
            // echo back what we set.
            var readBack = new SerializedObject(target).FindProperty(field);
            var actual = readBack != null ? readBack.intValue : int.MinValue;

            return actual == value
                ? field + "=" + actual
                : field + ": WROTE " + value + " BUT READ " + actual;
        }

        // -- systems -------------------------------------------------------

        private static GameObject BuildSystems(Scene scene, GameObject rig, List<string> log)
        {
            var systems = new GameObject("SpatialDebuggerSystems");
            SceneManager.MoveGameObjectToScene(systems, scene);

            var client = systems.AddComponent<AIClient>();
            var dispatcher = systems.AddComponent<SpatialActionDispatcher>();
            var uiDriver = systems.AddComponent<SpatialUIDriver>();
            systems.AddComponent<SpatialRaycaster>();
            var targetController = systems.AddComponent<SpatialTargetController>();
            var analysis = systems.AddComponent<DemoAnalysis>();

            // Checkpoint 1: passthrough camera access, on its own object so it
            // can be disabled without touching anything that already works.
            var vision = new GameObject("CameraFeed");
            vision.transform.SetParent(systems.transform, false);
            vision.AddComponent<Vision.CameraFeed>();
            log.Add("camera feed: passthrough camera access (inert if unsupported)");

            var sources = new GameObject("PointerSources");
            sources.transform.SetParent(systems.transform, false);
            sources.AddComponent<MetaHandPointerSource>();
            sources.AddComponent<MetaControllerPointerSource>();
            sources.AddComponent<EditorMousePointerSource>();
            sources.AddComponent<HeadGazePointerSource>();

            // Wire the serialized references now so nothing depends on
            // FindAnyObjectByType at runtime.
            SetReference(analysis, "client", client);
            SetReference(analysis, "dispatcher", dispatcher);
            SetReference(targetController, "uiDriver", uiDriver);

            // The demo: every pinch drops a world-fixed label.
            var placer = systems.AddComponent<PinchAnnotationPlacer>();
            SetReference(placer, "targetController", targetController);
            SetReference(placer, "dispatcher", dispatcher);
            log.Add("pinch placer: each pinch drops a world-space label + marker");

            SetReference(targetController, "dispatcher", dispatcher);

            log.Add("systems: AIClient, dispatcher, target controller, UI driver, demo analysis");
            log.Add("pointer sources: hand, controller, editor mouse, head gaze");

            if (rig == null)
            {
                log.Add("WARNING: no camera rig, so targeting has no viewer to work from");
            }

            return systems;
        }

        private static void BuildPanel(Scene scene, GameObject systems, List<string> log)
        {
            var go = new GameObject("SpatialDebuggerPanel");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(0f, 1.2f, 0.75f);

            var panel = go.AddComponent<SpatialDebuggerPanel>();
            go.SetActive(ShowDebugPanel);

            if (systems != null)
            {
                SetReference(panel, "analysis", systems.GetComponent<DemoAnalysis>());
                SetReference(panel, "targetController", systems.GetComponent<SpatialTargetController>());
                SetReference(panel, "dispatcher", systems.GetComponent<SpatialActionDispatcher>());
                SetReference(panel, "client", systems.GetComponent<AIClient>());
            }

            log.Add("panel: world-space control panel, " + (ShowDebugPanel ? "shown" : "HIDDEN (debug UI)"));
        }

        /// <summary>
        /// A stand-in breadboard with a collider. Present so the whole
        /// target-to-annotation pipeline can be exercised in Play mode on a
        /// Mac; it deletes itself outside the Editor.
        /// </summary>
        private static void BuildEditorTestSurface(Scene scene, List<string> log)
        {
            var root = new GameObject("EditorTestSurface");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = new Vector3(0f, 0.9f, 0.9f);
            root.AddComponent<EditorOnlyProp>();

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Breadboard";
            board.transform.SetParent(root.transform, false);
            board.transform.localScale = new Vector3(0.17f, 0.012f, 0.06f);

            var renderer = board.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = AnnotationVisuals.Opaque(new Color(0.82f, 0.82f, 0.78f));
            }

            log.Add("editor test surface: stand-in breadboard at " + root.transform.position +
                    " (Editor only)");
        }

        // -- build settings ------------------------------------------------

        /// <summary>
        /// Makes the generated scene scene 0, and drops the stock URP template
        /// scene.
        /// </summary>
        /// <remarks>
        /// SampleScene being left in the list is what let a build ship the
        /// template scene instead of this one.
        /// </remarks>
        private static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == ScenePath || s.path == "Assets/Scenes/SampleScene.unity");
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // -- serialized-field helpers --------------------------------------

        private static void SetBool(SerializedObject serialized, string field, bool value)
        {
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning("[SpatialDebugger] serialized field '" + field + "' not found on " +
                                 serialized.targetObject.GetType().Name);
                return;
            }

            property.boolValue = value;
        }

        private static void SetEnum(SerializedObject serialized, string field, int value)
        {
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning("[SpatialDebugger] serialized field '" + field + "' not found on " +
                                 serialized.targetObject.GetType().Name);
                return;
            }

            property.intValue = value;
        }

        /// <summary>
        /// Assigns a private [SerializeField] object reference. Used so the
        /// generated scene has real wiring rather than relying on runtime
        /// lookups that can silently find the wrong object.
        /// </summary>
        private static void SetReference(UnityEngine.Object target, string field,
            UnityEngine.Object value)
        {
            if (target == null || value == null) return;

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning("[SpatialDebugger] serialized field '" + field + "' not found on " +
                                 target.GetType().Name);
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
