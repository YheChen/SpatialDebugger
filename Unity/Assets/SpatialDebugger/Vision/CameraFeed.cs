using System;
using Meta.XR;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace SpatialDebugger.Vision
{
    /// <summary>
    /// Owns Quest passthrough camera access, and reports honestly when it is
    /// unavailable.
    /// </summary>
    /// <remarks>
    /// Checkpoint 1 of the recognition work: prove the camera can be opened
    /// without disturbing anything that already works. Nothing here touches
    /// passthrough, hand tracking, pinch detection or annotation placement —
    /// it only adds a component and asks for a permission.
    /// <para>
    /// <see cref="PassthroughCameraAccess"/> never requests the permission
    /// itself; it polls <c>Permission.HasUserAuthorizedPermission</c> in
    /// <c>OnEnable</c> and, failing that, waits in a coroutine until it is
    /// granted. So requesting it here is sufficient — the component picks it up
    /// on its own once the user accepts.
    /// </para>
    /// <para>
    /// Hard device gate, from the SDK source: Quest 3 or 3S only, and Horizon
    /// OS SDK 74 or newer. <see cref="PassthroughCameraAccess.IsSupported"/>
    /// returns false otherwise and this component stays quietly inert.
    /// </para>
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

        [Tooltip("Seconds between status lines in logcat while waiting. 0 silences them.")]
        [SerializeField] private float statusLogInterval = 2f;

        /// <summary>Why the camera is not usable, or empty when it is.</summary>
        public string Status { get; private set; } = "starting";

        /// <summary>True when a frame can be read right now.</summary>
        public bool IsReady => _access != null && _access.IsPlaying;

        /// <summary>Raised once, the first time a frame becomes available.</summary>
        public event Action FirstFrameReady;

        private PassthroughCameraAccess _access;
        private float _nextLog;
        private bool _announced;
        private bool _permissionRequested;

        private void Awake()
        {
            // Supported is a static device check; asking early avoids creating
            // the component at all on hardware that cannot use it.
            if (!PassthroughCameraAccess.IsSupported)
            {
                Status = "unsupported device or Horizon OS older than v74";
                Debug.LogWarning("[SpatialDebugger][camera] " + Status +
                                 " - recognition will stay in deterministic mode.");
                enabled = false;
                return;
            }

            RequestPermissionIfNeeded();
        }

        private void RequestPermissionIfNeeded()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(CameraPermission))
            {
                Debug.Log("[SpatialDebugger][camera] permission already granted.");
                return;
            }

            if (_permissionRequested) return;
            _permissionRequested = true;

            Debug.Log("[SpatialDebugger][camera] requesting " + CameraPermission);
            Permission.RequestUserPermission(CameraPermission);
#else
            Debug.Log("[SpatialDebugger][camera] editor: no Android permission to request.");
#endif
        }

        private void OnEnable()
        {
            if (_access != null) return;

            // Created rather than serialised so a device that cannot support it
            // never gets the component at all.
            _access = gameObject.GetComponent<PassthroughCameraAccess>();
            if (_access == null)
            {
                _access = gameObject.AddComponent<PassthroughCameraAccess>();
            }

            _access.CameraPosition = eye;
            _access.RequestedResolution = requestedResolution;
            _access.enabled = true;

            Status = "waiting for permission";
        }

        private void Update()
        {
            if (_access == null) return;

            if (_access.IsPlaying)
            {
                if (!_announced)
                {
                    _announced = true;
                    Status = string.Empty;

                    var intrinsics = _access.Intrinsics;
                    Debug.Log("[SpatialDebugger][camera] READY " +
                              _access.CurrentResolution.x + "x" + _access.CurrentResolution.y +
                              " focal=" + intrinsics.FocalLength +
                              " principal=" + intrinsics.PrincipalPoint +
                              " sensor=" + intrinsics.SensorResolution);

                    try
                    {
                        FirstFrameReady?.Invoke();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError("[SpatialDebugger][camera] FirstFrameReady threw: " + exception);
                    }
                }

                return;
            }

            // Not playing yet. Keep asking, and say why at a readable rate.
            RequestPermissionIfNeeded();

            if (statusLogInterval <= 0f || Time.unscaledTime < _nextLog) return;
            _nextLog = Time.unscaledTime + statusLogInterval;

#if UNITY_ANDROID && !UNITY_EDITOR
            var granted = Permission.HasUserAuthorizedPermission(CameraPermission);
#else
            const bool granted = false;
#endif
            Status = granted ? "permission granted, camera not playing yet" : "permission not granted";
            Debug.Log("[SpatialDebugger][camera] " + Status);
        }

        /// <summary>
        /// The current camera frame, or null when unavailable.
        /// </summary>
        /// <remarks>
        /// On device this is a <see cref="RenderTexture"/>, not a
        /// <see cref="Texture2D"/>, so it cannot be read with
        /// <c>GetPixels</c> directly — a GPU readback is required. Only in the
        /// Editor is it a Texture2D.
        /// </remarks>
        public Texture GetTexture()
        {
            return _access != null && _access.IsPlaying ? _access.GetTexture() : null;
        }

        /// <summary>The camera pose when the current frame was captured.</summary>
        public bool TryGetCameraPose(out Pose pose)
        {
            if (_access != null && _access.IsPlaying)
            {
                pose = _access.GetCameraPose();
                return true;
            }

            pose = default;
            return false;
        }
    }
}
