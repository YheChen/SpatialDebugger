using System;
using System.Collections;
using Meta.XR;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace SpatialDebugger.Vision
{
    /// <summary>
    /// Owns Quest passthrough camera access, and says loudly what it is doing.
    /// </summary>
    /// <remarks>
    /// The first version of this component logged nothing on device and sat
    /// permanently inert. Two mistakes, both in <c>Awake</c>:
    /// <list type="number">
    /// <item>The very first statement called
    /// <see cref="PassthroughCameraAccess.IsSupported"/>, which calls
    /// <c>OVRPlugin.GetSystemHeadsetType()</c> and then JNI
    /// (<c>new AndroidJavaClass("vros.os.VrosBuild")</c>). Either can throw, or
    /// report <c>None</c>, before OVRPlugin has initialised. Nothing was logged
    /// above it, so a throw produced total silence.</item>
    /// <item>It was a ONE-SHOT check at the earliest possible moment, and a
    /// negative answer set <c>enabled = false</c> permanently. OVRPlugin
    /// initialisation and the permission grant can both land after frame 0, so
    /// the component gave up before the thing it was waiting for could
    /// possibly have happened.</item>
    /// </list>
    /// Hence the shape here: log before anything that can throw, do every
    /// device-dependent check inside a retrying coroutine, wrap every native
    /// and JNI call, and never disable permanently.
    /// </remarks>
    public class CameraFeed : MonoBehaviour
    {
        /// <summary>The permission the passthrough camera needs.</summary>
        public const string CameraPermission = "horizonos.permission.HEADSET_CAMERA";

        [Tooltip("Which eye's camera to open. One instance per eye is the SDK limit; " +
                 "a second with the same position hard-fails.")]
        [SerializeField] private PassthroughCameraAccess.CameraPositionType eye =
            PassthroughCameraAccess.CameraPositionType.Left;

        [Tooltip("Lower is cheaper to read back. Recognition does not need detail.")]
        [SerializeField] private Vector2Int requestedResolution = new Vector2Int(1280, 960);

        [Tooltip("Seconds to keep waiting for device support before reporting it as absent. " +
                 "OVRPlugin is often not ready in the first frames.")]
        [SerializeField] private float supportTimeout = 15f;

        [Tooltip("Seconds to wait for the first frame once the camera has been told to play.")]
        [SerializeField] private float frameTimeout = 20f;

        [Tooltip("Read pixels back once to prove real image data, not just a non-null texture.")]
        [SerializeField] private bool verifyPixels = true;

        /// <summary>Human-readable state, for the UI and for diagnosis.</summary>
        public string Status { get; private set; } = "starting";

        /// <summary>True when a frame can be read right now.</summary>
        public bool IsReady { get; private set; }

        /// <summary>Raised once, the first time a frame is confirmed available.</summary>
        public event Action FirstFrameReady;

        private PassthroughCameraAccess _access;

        // -- logging -------------------------------------------------------
        // One line per state transition. Never per frame.

        private const string Tag = "[SpatialDebugger][camera] ";
        private static void Log(string message) => Debug.Log(Tag + message);
        private static void Warn(string message) => Debug.LogWarning(Tag + message);
        private static void Fail(string message) => Debug.LogError(Tag + "ERROR " + message);

        // -- lifecycle -----------------------------------------------------

        private void Awake()
        {
            // FIRST statement. Nothing above this can throw, so silence here
            // would mean the component is not running at all.
            Log("Awake");
        }

        private void OnEnable()
        {
            Log("OnEnable");
        }

        private void Start()
        {
            Log("Start");
            StartCoroutine(Bootstrap());
        }

        private IEnumerator Bootstrap()
        {
            // --- 1. device support, retried: OVRPlugin may not be up yet ---
            var deadline = Time.realtimeSinceStartup + supportTimeout;
            string detail = "not checked";
            var supported = false;
            var reported = string.Empty;

            while (Time.realtimeSinceStartup < deadline)
            {
                supported = TryIsSupported(out detail);
                if (supported) break;

                // Report each DISTINCT reason once, rather than every frame.
                if (detail != reported)
                {
                    reported = detail;
                    Log("waiting for device support: " + detail);
                }

                yield return new WaitForSeconds(0.25f);
            }

            if (!supported)
            {
                Status = "unsupported: " + detail;
                Warn("NOT SUPPORTED after " + supportTimeout + "s (" + detail + "). " +
                     "Passthrough Camera Access needs Quest 3 or 3S on Horizon OS SDK 74+. " +
                     "Staying in deterministic mode; the rest of the app is unaffected.");
                yield break;
            }

            Log("supported=true");

            // --- 2. permission ------------------------------------------------
            if (!HasPermission())
            {
                Log("permission=false, requesting " + CameraPermission);
                RequestPermission();

                // The grant is a user action and can take as long as it takes.
                while (!HasPermission()) yield return new WaitForSeconds(0.25f);
            }

            Log("permission=true");

            // --- 3. create the camera component -------------------------------
            if (!TryCreateAccess(out var createError))
            {
                Status = "could not create PassthroughCameraAccess";
                Fail(createError);
                yield break;
            }

            Log("PassthroughCameraAccess found=true (eye=" + eye +
                ", requested=" + requestedResolution.x + "x" + requestedResolution.y + ")");
            Status = "waiting for texture";
            Log("waiting for texture");

            // --- 4. first frame ----------------------------------------------
            deadline = Time.realtimeSinceStartup + frameTimeout;
            var warned = false;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (_access == null)
                {
                    Fail("PassthroughCameraAccess disappeared while waiting for a frame.");
                    yield break;
                }

                if (_access.IsPlaying && _access.GetTexture() != null) break;

                // Halfway through, say what we are stuck on. Once.
                if (!warned && Time.realtimeSinceStartup > deadline - frameTimeout * 0.5f)
                {
                    warned = true;
                    Log("still waiting: isPlaying=" + _access.IsPlaying +
                        " texture=" + (_access.GetTexture() != null) +
                        " componentEnabled=" + _access.enabled);
                }

                yield return new WaitForSeconds(0.25f);
            }

            var texture = _access != null ? _access.GetTexture() : null;
            if (texture == null)
            {
                Status = "no texture after " + frameTimeout + "s";
                Fail("no texture after " + frameTimeout + "s. isPlaying=" +
                     (_access != null && _access.IsPlaying) +
                     " - the camera was told to play but never produced a frame.");
                yield break;
            }

            // --- 5. prove it is real image data -------------------------------
            var resolution = _access.CurrentResolution;
            Log("texture received " + texture.width + "x" + texture.height +
                " type=" + texture.GetType().Name + " reportedResolution=" +
                resolution.x + "x" + resolution.y);

            if (verifyPixels) VerifyPixels();

            // --- 6. ready -----------------------------------------------------
            IsReady = true;
            Status = string.Empty;

            var intrinsics = SafeIntrinsics();
            Log("READY " + texture.width + "x" + texture.height + " " + intrinsics);

            try
            {
                FirstFrameReady?.Invoke();
            }
            catch (Exception exception)
            {
                Fail("FirstFrameReady handler threw: " + exception);
            }
        }

        // -- guarded native calls ------------------------------------------

        /// <summary>
        /// <see cref="PassthroughCameraAccess.IsSupported"/> without letting a
        /// native or JNI failure escape. Reports WHY, so an early call that
        /// merely ran before OVRPlugin was ready is distinguishable from real
        /// unsupported hardware.
        /// </summary>
        private static bool TryIsSupported(out string detail)
        {
            try
            {
                if (PassthroughCameraAccess.IsSupported)
                {
                    detail = "supported";
                    return true;
                }

                detail = "IsSupported=false (headset not Quest 3/3S, OS older than v74, " +
                         "or OVRPlugin not initialised yet)";
                return false;
            }
            catch (Exception exception)
            {
                detail = "IsSupported threw " + exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        private bool TryCreateAccess(out string error)
        {
            error = null;

            try
            {
                _access = GetComponent<PassthroughCameraAccess>();
                if (_access == null) _access = gameObject.AddComponent<PassthroughCameraAccess>();

                // Both are plain serialized fields; set them before enabling.
                _access.CameraPosition = eye;
                _access.RequestedResolution = requestedResolution;
                _access.enabled = true;
                return true;
            }
            catch (Exception exception)
            {
                error = "AddComponent<PassthroughCameraAccess> threw: " + exception;
                return false;
            }
        }

        /// <summary>
        /// Reads pixels back once. A non-null texture only proves an object
        /// exists; this proves the camera is delivering image data.
        /// </summary>
        private void VerifyPixels()
        {
            try
            {
                var colors = _access.GetColors();

                if (!colors.IsCreated || colors.Length == 0)
                {
                    Warn("pixel check: readback produced no data (texture exists but is empty)");
                    return;
                }

                // Sample rather than average millions of pixels.
                long r = 0, g = 0, b = 0;
                var step = Mathf.Max(1, colors.Length / 2000);
                var sampled = 0;

                for (var i = 0; i < colors.Length; i += step)
                {
                    r += colors[i].r;
                    g += colors[i].g;
                    b += colors[i].b;
                    sampled++;
                }

                if (sampled == 0)
                {
                    Warn("pixel check: no samples taken");
                    return;
                }

                Log("pixel check: " + colors.Length + " pixels, mean RGB " +
                    (r / sampled) + "," + (g / sampled) + "," + (b / sampled) +
                    (r + g + b == 0 ? "  <-- ALL BLACK, camera may be blocked" : "  (real image data)"));
            }
            catch (Exception exception)
            {
                Warn("pixel check threw (not fatal): " + exception.Message);
            }
        }

        private string SafeIntrinsics()
        {
            try
            {
                var intrinsics = _access.Intrinsics;
                return "focal=" + intrinsics.FocalLength +
                       " principal=" + intrinsics.PrincipalPoint +
                       " sensor=" + intrinsics.SensorResolution;
            }
            catch (Exception exception)
            {
                return "(intrinsics unavailable: " + exception.Message + ")";
            }
        }

        private static bool HasPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return Permission.HasUserAuthorizedPermission(CameraPermission);
            }
            catch (Exception exception)
            {
                Fail("permission check threw: " + exception.Message);
                return false;
            }
#else
            return true; // nothing to grant in the Editor
#endif
        }

        private static void RequestPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Permission.RequestUserPermission(CameraPermission);
            }
            catch (Exception exception)
            {
                Fail("permission request threw: " + exception.Message);
            }
#endif
        }

        // -- public API for the recognition step ---------------------------

        /// <summary>
        /// The current camera frame, or null when unavailable. On device this
        /// is a <see cref="RenderTexture"/>, so pixels need a GPU readback.
        /// </summary>
        public Texture GetTexture()
        {
            return _access != null && _access.IsPlaying ? _access.GetTexture() : null;
        }

        /// <summary>The camera pose when the current frame was captured.</summary>
        public bool TryGetCameraPose(out Pose pose)
        {
            try
            {
                if (_access != null && _access.IsPlaying)
                {
                    pose = _access.GetCameraPose();
                    return true;
                }
            }
            catch (Exception exception)
            {
                Fail("GetCameraPose threw: " + exception.Message);
            }

            pose = default;
            return false;
        }
    }
}
