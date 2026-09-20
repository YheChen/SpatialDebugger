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

        /// <summary>What produced the hit, for the UI and for honest reporting.</summary>
        public HitSource Source;

        public Collider Collider;

        public enum HitSource
        {
            None = 0,

            /// <summary>A real Unity collider in the scene.</summary>
            Physics,

            /// <summary>A real-world surface from MRUK scene understanding.</summary>
            SceneSurface,

            /// <summary>
            /// Nothing was hit, so the point was projected to a fixed distance
            /// along the ray. Good enough to place a target in mid-air.
            /// </summary>
            Projected
        }

        public static readonly SpatialHit None = new SpatialHit { IsValid = false };
    }

    /// <summary>
    /// Resolves a pointing ray to a point in the room.
    /// </summary>
    /// <remarks>
    /// Three tiers, best first:
    /// <list type="number">
    /// <item>MRUK scene surfaces -- real walls, tables and floors. Requires the
    /// user to have run Space Setup on the headset, so it is optional and
    /// resolved by reflection: the app compiles and runs identically whether
    /// or not MRUK is present.</item>
    /// <item>Unity physics colliders.</item>
    /// <item>A projection to a fixed distance, so pointing at empty space
    /// still places a target. This tier is why the demo cannot fail.</item>
    /// </list>
    /// </remarks>
    public class SpatialRaycaster : MonoBehaviour
    {
        [Tooltip("Use MRUK scene surfaces when scene data is available. Falls back " +
                 "silently when it is not.")]
        [SerializeField] private bool useSceneSurfaces = true;

        [Tooltip("Everything except layer 5 (UI). Buttons live on the UI layer so " +
                 "pointing at one never also moves the target.")]
        [SerializeField] private LayerMask physicsLayers = ~(1 << 5);

        [SerializeField] private float maxDistance = 6f;

        [Tooltip("Where a target lands when the ray hits nothing at all.")]
        [SerializeField] private float fallbackDistance = 1.5f;

        private MrukSurfaceProbe _sceneProbe;

        private MrukSurfaceProbe SceneProbe
        {
            get
            {
                if (_sceneProbe == null) _sceneProbe = new MrukSurfaceProbe();
                return _sceneProbe;
            }
        }

        /// <summary>True when real-world surfaces are actually usable right now.</summary>
        public bool SceneSurfacesAvailable => useSceneSurfaces && SceneProbe.IsAvailable;

        public SpatialHit Raycast(Ray ray)
        {
            if (useSceneSurfaces && SceneProbe.TryRaycast(ray, maxDistance, out var sceneHit))
            {
                return sceneHit;
            }

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

            // Nothing in the way: put the target an arm's length down the ray so
            // the user can still annotate a point in mid-air.
            return new SpatialHit
            {
                IsValid = true,
                Point = ray.origin + ray.direction.normalized * fallbackDistance,
                // No surface was hit, so there is no surface normal. Report
                // world up: annotations authored "above the target" must stand
                // up in the room, not march back along the ray toward the user.
                Normal = Vector3.up,
                Distance = fallbackDistance,
                Source = SpatialHit.HitSource.Projected
            };
        }
    }
}
