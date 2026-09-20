using UnityEngine;

namespace SpatialDebugger.Core
{
    /// <summary>
    /// The point in the room the user selected as "the thing I'm debugging".
    /// </summary>
    /// <remarks>
    /// This is the anchor the whole product hangs off. Annotations are parented
    /// here, so a backend response expressed in <see cref="SpatialSpace.Target"/>
    /// coordinates lands in the right place without anyone knowing real Quest
    /// world coordinates. Precise computer-vision localisation, when it exists,
    /// will do nothing more than move this transform.
    /// </remarks>
    public class SpatialTarget : MonoBehaviour
    {
        [Tooltip("Human-readable name sent to the backend as target_label.")]
        [SerializeField] private string label = "Selected point";

        [Tooltip("Surface normal at the point that was selected, if known.")]
        [SerializeField] private Vector3 surfaceNormal = Vector3.up;

        [Tooltip("How much to trust surfaceNormal, in [0,1]. Only the depth " +
                 "sensor reports this; everything else leaves it at 0.")]
        [SerializeField] private float normalConfidence;

        /// <summary>
        /// Confidence in <see cref="SurfaceNormal"/>, in [0,1]. Zero means the
        /// normal is a placeholder, not a measurement.
        /// </summary>
        public float NormalConfidence
        {
            get => normalConfidence;
            set => normalConfidence = value;
        }

        /// <summary>Where annotations get parented. Always this transform.</summary>
        public Transform AnnotationRoot => transform;

        public string Label
        {
            get => label;
            set => label = value;
        }

        public Vector3 SurfaceNormal
        {
            get => surfaceNormal.sqrMagnitude < 1e-6f ? Vector3.up : surfaceNormal.normalized;
            set => surfaceNormal = value;
        }

        /// <summary>How the target was chosen, for the UI and for debugging.</summary>
        public enum Origin
        {
            Unknown = 0,
            HandRay,
            Controller,
            EditorMouse,
            SceneSurface,
            Fallback
        }

        public Origin SelectedBy { get; set; } = Origin.Unknown;

        /// <summary>
        /// Points the target's local +Y along the surface normal, so annotations
        /// authored "above the board" sit above the board rather than above the room.
        /// </summary>
        public void AlignToSurface(Vector3 normal)
        {
            SurfaceNormal = normal;
            if (normal.sqrMagnitude < 1e-6f) return;

            transform.rotation = Quaternion.FromToRotation(Vector3.up, SurfaceNormal);
        }

        /// <summary>
        /// Keeps the target's +Y along its surface normal while turning its
        /// +Z to face the viewer, so "up" and "toward me" both behave.
        /// </summary>
        public void FaceViewer(Vector3 viewerPosition)
        {
            var up = SurfaceNormal;
            var toViewer = viewerPosition - transform.position;

            // Project the viewer direction onto the surface plane.
            var forward = Vector3.ProjectOnPlane(toViewer, up);
            if (forward.sqrMagnitude < 1e-6f)
            {
                transform.rotation = Quaternion.FromToRotation(Vector3.up, up);
                return;
            }

            transform.rotation = Quaternion.LookRotation(forward.normalized, up);
        }
    }
}
