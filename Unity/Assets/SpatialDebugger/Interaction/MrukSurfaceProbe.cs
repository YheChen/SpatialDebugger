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
    /// needs no Space Setup, so it hits a breadboard on a real table. It is
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
        private EnvironmentRaycastManager _depth;
        private bool _searchedForDepth;

        /// <summary>Set once a depth raycast has genuinely returned a Hit.</summary>
        private bool _depthEverHit;

        /// <summary>True when either Meta source can answer a query right now.</summary>
        public bool IsAvailable => DepthUsable || RoomAvailable;

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
        /// Whether it is worth *trying* a depth raycast. Readiness itself is
        /// not publicly observable on <see cref="EnvironmentRaycastManager"/>
        /// -- its IsReady is private -- so the only honest check is to cast
        /// and read <see cref="EnvironmentRaycastHit.status"/>.
        /// </summary>
        private bool DepthUsable
        {
            get
            {
                if (!EnvironmentRaycastManager.IsSupported) return false;

                if (!_searchedForDepth)
                {
                    _searchedForDepth = true;
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

        public bool TryRaycast(Ray ray, float maxDistance, out SpatialHit hit)
        {
            hit = SpatialHit.None;

            if (TryDepthRaycast(ray, maxDistance, out hit)) return true;
            if (TryRoomRaycast(ray, maxDistance, out hit)) return true;

            return false;
        }

        private bool TryDepthRaycast(Ray ray, float maxDistance, out SpatialHit hit)
        {
            hit = SpatialHit.None;
            if (!DepthUsable) return false;

            if (!_depth.Raycast(ray, out var depthHit, maxDistance)) return false;

            // The bool alone is not enough: NotReady, RayOccluded and
            // HitPointOutsideOfCameraFrustum all come back through status.
            if (depthHit.status != EnvironmentRaycastHitStatus.Hit) return false;

            _depthEverHit = true;
            hit = new SpatialHit
            {
                IsValid = true,
                Point = depthHit.point,
                Normal = depthHit.normal,
                Distance = Vector3.Distance(ray.origin, depthHit.point),
                Source = SpatialHit.HitSource.SceneSurface
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
