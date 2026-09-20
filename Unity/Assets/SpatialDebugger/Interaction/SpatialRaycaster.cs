using UnityEngine;

namespace SpatialDebugger.Interaction
{
    /// <summary>Where a pointing ray landed, and how confident we are about it.</summary>
    public struct SpatialHit
    {
        public bool IsValid;
        public Vector3 Point;
        public Vector3 Normal;
        public float Distance;

        /// <summary>
        /// How much to trust <see cref="Normal"/>, in [0,1]. Only the depth
        /// sensor reports this; every other tier leaves it at 0.
        /// </summary>
        public float NormalConfidence;

        /// <summary>What produced the hit, for the UI and for honest reporting.</summary>
        public HitSource Source;

        public Collider Collider;

        public enum HitSource
        {
            None = 0,

            /// <summary>A real Unity collider in the scene.</summary>
            Physics = 1,

            /// <summary>A real-world surface from MRUK room geometry (Space Setup).</summary>
            SceneSurface = 2,

            /// <summary>
            /// Nothing was hit, so the point was projected to a fixed distance
            /// along the ray. Good enough to place a target in mid-air.
            /// </summary>
            Projected = 3,

            /// <summary>
            /// A real physical surface measured by the Quest depth sensor. No
            /// Space Setup and no colliders needed -- this is the tier that
            /// makes a label land on the actual chair rather than 1.5 m out.
            /// </summary>
            EnvironmentDepth = 4
        }

        public static readonly SpatialHit None = new SpatialHit { IsValid = false };
    }

    /// <summary>
    /// Resolves a pointing ray to a point in the room.
    /// </summary>
    /// <remarks>
    /// Three tiers, best first:
    /// <list type="number">
    /// <item>Meta environment depth, then MRUK scene surfaces -- real walls,
    /// tables and chairs. Depth needs no Space Setup; MRUK does. Both are
    /// optional and both degrade to "unavailable" rather than throwing.</item>
    /// <item>Unity physics colliders.</item>
    /// <item>A projection to a fixed distance, so pointing at empty space
    /// still places a target. This tier is why the demo cannot fail.</item>
    /// </list>
    /// </remarks>
    public class SpatialRaycaster : MonoBehaviour
    {
        [Tooltip("Use environment depth and MRUK scene surfaces when they are " +
                 "available. Falls back silently when they are not.")]
        [SerializeField] private bool useSceneSurfaces = true;

        [Tooltip("Everything except layer 5 (UI). Buttons live on the UI layer so " +
                 "pointing at one never also moves the target.")]
        [SerializeField] private LayerMask physicsLayers = ~(1 << 5);

        [SerializeField] private float maxDistance = 6f;

        [Tooltip("Where a target lands when the ray hits nothing at all.")]
        [SerializeField] private float fallbackDistance = 1.5f;

        private MrukSurfaceProbe _sceneProbe;

        /// <summary>
        /// The real-world surface probe. Settable so tests can drive the tier
        /// hierarchy without a headset; never assigned in the app.
        /// </summary>
        public MrukSurfaceProbe SurfaceProbe
        {
            get
            {
                if (_sceneProbe == null) _sceneProbe = new MrukSurfaceProbe();
                return _sceneProbe;
            }
            set { _sceneProbe = value; }
        }

        /// <summary>Where a target lands when nothing at all is hit.</summary>
        public float FallbackDistance => fallbackDistance;

        /// <summary>True when real-world surfaces are actually usable right now.</summary>
        public bool SceneSurfacesAvailable => useSceneSurfaces && SurfaceProbe.IsAvailable;

        public SpatialHit Raycast(Ray ray)
        {
            // Tier 1: the physical environment -- depth sensor first, then MRUK
            // room geometry if the user happens to have run Space Setup.
            if (useSceneSurfaces && SurfaceProbe.TryRaycast(ray, maxDistance, out var sceneHit))
            {
                return sceneHit;
            }

            // Tier 2: Unity colliders (the Editor test surface, and any props).
            if (Physics.Raycast(ray, out var hit, maxDistance, physicsLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return new SpatialHit
                {
                    IsValid = true,
                    Point = hit.point,
                    Normal = hit.normal,
                    Distance = hit.distance,
                    Source = SpatialHit.HitSource.Physics,
                    Collider = hit.collider
                };
            }

            // Tier 3: nothing in the way, so put the target an arm's length down
            // the ray. The user can still annotate a point in mid-air, and
            // placement never simply stops working.
            return Fallback(ray, fallbackDistance);
        }

        /// <summary>The last tier: a point projected along the ray.</summary>
        public static SpatialHit Fallback(Ray ray, float distance)
        {
            return new SpatialHit
            {
                IsValid = true,
                Point = ray.origin + ray.direction.normalized * distance,
                // No surface was hit, so there is no surface normal. Report
                // world up: annotations authored "above the target" must stand
                // up in the room, not march back along the ray toward the user.
                Normal = Vector3.up,
                Distance = distance,
                Source = SpatialHit.HitSource.Projected
            };
        }
    }
}
