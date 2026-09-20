using Meta.XR;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace SpatialDebugger.Interaction
{
    /// <summary>
    /// Optional real-world surface queries, behind one seam.
    /// </summary>
    /// <remarks>
    /// Two independent Meta sources, because they fail in different ways:
    /// <list type="bullet">
    /// <item><see cref="EnvironmentRaycastManager"/> is depth-sensor driven and
    /// needs no Space Setup, so it hits a chair on a real floor. It is
    /// device-only -- <c>IsSupported</c> is false in the Editor -- and takes a
    /// few frames after enable to become ready.</item>
    /// <item><see cref="MRUK"/> room geometry needs the user to have run Space
    /// Setup, and only knows about planes and volumes it captured.</item>
    /// </list>
    /// Everything here null-checks and degrades to "unavailable" rather than
    /// throwing, so <see cref="SpatialRaycaster"/> can always fall through to
    /// physics and then to a projected point.
    /// <para>
    /// Note: the <see cref="RaycastHit"/> MRUK returns has no collider
    /// populated -- only point, normal and distance are meaningful.
    /// </para>
    /// </remarks>
    public class MrukSurfaceProbe
    {
        /// <summary>
        /// How often to look for the depth manager while it is still missing.
        /// </summary>
        /// <remarks>
        /// <see cref="EnvironmentDepthSource"/> creates it seconds into the
        /// session -- after OVRPlugin is up and USE_SCENE has been granted --
        /// so a one-shot search would look once, find nothing, and leave depth
        /// disabled for the whole run. Throttled because this is asked from
        /// <c>Update</c> and <c>FindAnyObjectByType</c> is not free.
        /// </remarks>
        private const float DepthSearchInterval = 0.5f;

        private EnvironmentRaycastManager _depth;
        private float _nextDepthSearch;

        /// <summary>Set once a depth raycast has genuinely returned a Hit.</summary>
        private bool _depthEverHit;

        /// <summary>
        /// Why the last depth raycast did or did not produce a point, straight
        /// from <see cref="EnvironmentRaycastHitStatus"/>. For diagnostics: it
        /// is the difference between "depth is off" and "you pointed at a
        /// surface the depth camera cannot see".
        /// </summary>
        public string LastDepthStatus { get; private set; } = "not attempted";

        /// <summary>True when either Meta source can answer a query right now.</summary>
        public virtual bool IsAvailable => DepthUsable || RoomAvailable;

        /// <summary>Human-readable source name for the UI.</summary>
        public string ActiveSource
        {
            get
            {
                if (_depthEverHit) return "Depth";
                if (RoomAvailable) return "Scene";
                return "None";
            }
        }

        /// <summary>
        /// Whether it is worth *trying* a depth raycast.
        /// </summary>
        /// <remarks>
        /// Deliberately does not consult <see cref="EnvironmentRaycastManager.IsSupported"/>.
        /// That property caches its answer in a static for the lifetime of the
        /// process, so asking it before OVRPlugin has initialised would pin it
        /// to false for the whole session. <see cref="EnvironmentDepthSource"/>
        /// asks it once, at a moment when the answer is meaningful, and only
        /// creates the manager if it is true. Readiness itself is not publicly
        /// observable either -- the manager's IsReady is private -- so the only
        /// honest check is to cast and read
        /// <see cref="EnvironmentRaycastHit.status"/>.
        /// </remarks>
        private bool DepthUsable
        {
            get
            {
                if (_depth != null) return _depth.isActiveAndEnabled;

                if (Time.realtimeSinceStartup < _nextDepthSearch) return false;
                _nextDepthSearch = Time.realtimeSinceStartup + DepthSearchInterval;

                _depth = EnvironmentDepthSource.Manager;
                if (_depth == null)
                {
                    _depth = Object.FindAnyObjectByType<EnvironmentRaycastManager>();
                }

                return _depth != null && _depth.isActiveAndEnabled;
            }
        }

        private static bool RoomAvailable
        {
            get
            {
                // MRUK.Instance is null until an MRUK component's Awake has run,
                // and IsInitialized only flips true after a load succeeds.
                var mruk = MRUK.Instance;
                if (mruk == null || !mruk.IsInitialized) return false;
                return mruk.GetCurrentRoom() != null;
            }
        }

        /// <summary>
        /// True when the loaded room is real captured geometry rather than one
        /// of MRUK's synthetic fallback rooms. Worth surfacing: a synthetic room
        /// means annotations will not line up with anything physical.
        /// </summary>
        public static bool HasRealSceneData
        {
            get
            {
                var mruk = MRUK.Instance;
                if (mruk == null || !mruk.IsInitialized) return false;

                var room = mruk.GetCurrentRoom();
                return room != null && room.IsLocal;
            }
        }

        public virtual bool TryRaycast(Ray ray, float maxDistance, out SpatialHit hit)
        {
            hit = SpatialHit.None;

            if (TryDepthRaycast(ray, maxDistance, out hit)) return true;
            if (TryRoomRaycast(ray, maxDistance, out hit)) return true;

            return false;
        }

        private bool TryDepthRaycast(Ray ray, float maxDistance, out SpatialHit hit)
        {
            hit = SpatialHit.None;

            if (!DepthUsable)
            {
                LastDepthStatus = "no EnvironmentRaycastManager";
                return false;
            }

            // The return value alone is not enough: NotReady, RayOccluded and
            // HitPointOutsideOfCameraFrustum all come back only through status,
            // and they are what a user needs to see when a pinch lands wrong.
            // Raycast returns true exactly when status is Hit, so reading the
            // status instead of the bool changes nothing except the diagnostics.
            _depth.Raycast(ray, out var depthHit, maxDistance);
            LastDepthStatus = depthHit.status.ToString();

            if (depthHit.status != EnvironmentRaycastHitStatus.Hit) return false;

            _depthEverHit = true;
            hit = new SpatialHit
            {
                IsValid = true,
                Point = depthHit.point,
                Normal = depthHit.normal,
                // EnvironmentRaycastHit carries no distance of its own.
                Distance = Vector3.Distance(ray.origin, depthHit.point),
                NormalConfidence = depthHit.normalConfidence,
                Source = SpatialHit.HitSource.EnvironmentDepth
            };
            return true;
        }

        private static bool TryRoomRaycast(Ray ray, float maxDistance, out SpatialHit hit)
        {
            hit = SpatialHit.None;

            var mruk = MRUK.Instance;
            if (mruk == null || !mruk.IsInitialized) return false;

            var room = mruk.GetCurrentRoom();
            if (room == null) return false;

            // A default LabelFilter allows every label through. The obsolete
            // Included()/Excluded() helpers are deliberately not used.
            if (!room.Raycast(ray, maxDistance, new LabelFilter(), out var roomHit, out var anchor))
            {
                return false;
            }

            hit = new SpatialHit
            {
                IsValid = true,
                Point = roomHit.point,
                Normal = roomHit.normal,
                Distance = roomHit.distance,
                Source = SpatialHit.HitSource.SceneSurface,
                // Intentionally left null: MRUK does not populate the collider.
                Collider = null
            };

            if (anchor == null)
            {
                // A hit with no anchor is still a usable point, just less trusted.
                Debug.Log("[SpatialDebugger] MRUK hit had no anchor.");
            }

            return true;
        }
    }
}
