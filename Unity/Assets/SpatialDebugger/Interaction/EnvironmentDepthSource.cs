using System;
using System.Collections;
using Meta.XR;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace SpatialDebugger.Interaction
{
    /// <summary>
    /// Brings Meta's <see cref="EnvironmentRaycastManager"/> up, but only once
    /// it can actually work, and says loudly what it is doing.
    /// </summary>
    /// <remarks>
    /// Environment depth is what turns "a label 1.5 m down the ray" into "a
    /// label on the chair". It is sensor-driven, so it needs no Space Setup and
    /// no room scan -- but it does need three things this component waits for,
    /// in order:
    /// <list type="number">
    /// <item>OVRPlugin initialised. <see cref="EnvironmentRaycastManager.IsSupported"/>
    /// caches its answer in a <c>static</c> for the lifetime of the process, so
    /// asking it too early pins depth to "unsupported" for the whole session.
    /// That is why the manager is added at runtime instead of being serialised
    /// into the scene: a scene-resident manager asks in its own
    /// <c>Awake</c>.</item>
    /// <item>Quest 3 / 3S hardware that reports environment raycast support.</item>
    /// <item>The <c>USE_SCENE</c> runtime permission. The manager's OpenXR
    /// provider waits for it and never requests it itself. It is requested here
    /// rather than by flipping <c>OVRManager.requestScenePermissionOnStartup</c>
    /// so the verified camera rig is not touched.</item>
    /// </list>
    /// If any of that fails the component logs why and stops. It never disables
    /// anything else: <see cref="SpatialRaycaster"/> keeps its physics tier and
    /// its fixed-distance fallback, so placement carries on working.
    /// </remarks>
    public class EnvironmentDepthSource : MonoBehaviour
    {
        /// <summary>Required by the environment raycaster's OpenXR provider.</summary>
        public const string ScenePermission = "com.oculus.permission.USE_SCENE";

        private const string Tag = "[SpatialDebugger][depth] ";

        [Tooltip("Seconds to wait before asking for USE_SCENE, so the camera's " +
                 "own permission dialog is not competing with this one.")]
        [SerializeField] private float startupDelay = 2f;

        [Tooltip("Seconds to wait for OVRPlugin to initialise before giving up.")]
        [SerializeField] private float pluginTimeout = 20f;

        [Tooltip("Seconds to wait for the user to answer the USE_SCENE prompt.")]
        [SerializeField] private float permissionTimeout = 120f;

        /// <summary>
        /// The live manager, or null when depth is unavailable. Static so the
        /// raycaster's probe can find it without a scene reference.
        /// </summary>
        public static EnvironmentRaycastManager Manager { get; private set; }

        /// <summary>Human-readable state, for the UI and for reporting.</summary>
        public string Status { get; private set; } = "starting";

        private static void Log(string message) => Debug.Log(Tag + message);
        private static void Warn(string message) => Debug.LogWarning(Tag + message);

        private void Awake()
        {
            // First statement, deliberately: the camera path once went silent
            // because its Awake called into a native API before logging
            // anything at all. If this line is missing from logcat, the
            // component is not in the built scene.
            Log("Awake");
        }

        private void Start()
        {
            StartCoroutine(Bootstrap());
        }

        private void OnDestroy()
        {
            if (Manager != null && Manager.gameObject == gameObject) Manager = null;
        }

        private IEnumerator Bootstrap()
        {
            if (Application.isEditor)
            {
                // IsSupported is false off-device, and asking would cache that.
                Status = "editor: environment depth is device-only";
                Log(Status + " -- physics and the fixed fallback still apply");
                yield break;
            }

            // Someone may have put a manager in the scene by hand.
            var existing = FindAnyObjectByType<EnvironmentRaycastManager>();
            if (existing != null)
            {
                Manager = existing;
                Status = "using the manager already in the scene";
                Log(Status);
                yield break;
            }

            yield return new WaitForSeconds(startupDelay);

            // --- 1. OVRPlugin ------------------------------------------------
            var deadline = Time.realtimeSinceStartup + pluginTimeout;
            while (!PluginReady() && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForSeconds(0.25f);
            }

            if (!PluginReady())
            {
                Status = "OVRPlugin never initialised";
                Warn(Status + " after " + pluginTimeout + "s. No environment depth; " +
                     "keeping physics and the fixed fallback.");
                yield break;
            }

            Log("OVRPlugin initialised");

            // --- 2. hardware support -----------------------------------------
            // Wrapped: this is the call whose answer is cached forever, and it
            // reaches native code.
            bool supported;
            string supportError = null;
            try
            {
                supported = EnvironmentRaycastManager.IsSupported;
            }
            catch (Exception exception)
            {
                supported = false;
                supportError = exception.Message;
            }

            if (!supported)
            {
                Status = "environment depth not supported";
                Warn(Status + (supportError != null ? " (threw: " + supportError + ")" : "") +
                     ". Needs Quest 3 or 3S. Keeping physics and the fixed fallback.");
                yield break;
            }

            Log("supported=true");

            // --- 3. USE_SCENE permission -------------------------------------
            if (!HasPermission())
            {
                Log("permission=false, requesting " + ScenePermission);
                RequestPermission();

                var permissionDeadline = Time.realtimeSinceStartup + permissionTimeout;
                while (!HasPermission() && Time.realtimeSinceStartup < permissionDeadline)
                {
                    yield return new WaitForSeconds(0.25f);
                }
            }

            if (!HasPermission())
            {
                Status = ScenePermission + " not granted";
                Warn(Status + ". No environment depth; keeping physics and the " +
                     "fixed fallback.");
                yield break;
            }

            Log("permission=true");

            // --- 4. the manager ----------------------------------------------
            // Adding the component runs its Awake and OnEnable synchronously,
            // which creates the native raycaster handle. Raycasts stay NotReady
            // for a few frames after that, which is why the probe retries rather
            // than latching a single answer.
            try
            {
                Manager = gameObject.AddComponent<EnvironmentRaycastManager>();
            }
            catch (Exception exception)
            {
                Status = "could not create EnvironmentRaycastManager";
                Warn(Status + ": " + exception.Message);
                yield break;
            }

            Status = "READY";
            Log("EnvironmentRaycastManager created. Depth raycasts become Ready a " +
                "few frames from now; until then the probe reports NotReady and " +
                "placement uses physics or the fixed fallback.");
        }

        private static bool PluginReady()
        {
            try
            {
                return OVRPlugin.initialized;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool HasPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return Permission.HasUserAuthorizedPermission(ScenePermission);
            }
            catch (Exception exception)
            {
                Warn("permission check threw: " + exception.Message);
                return false;
            }
#else
            return true; // nothing to grant off-device
#endif
        }

        private static void RequestPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Permission.RequestUserPermission(ScenePermission);
            }
            catch (Exception exception)
            {
                Warn("permission request threw: " + exception.Message);
            }
#endif
        }
    }
}
