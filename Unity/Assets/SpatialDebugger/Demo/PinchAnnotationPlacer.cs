using SpatialDebugger.Annotations;
using SpatialDebugger.Core;
using SpatialDebugger.Interaction;
using UnityEngine;

namespace SpatialDebugger.Demo
{
    /// <summary>
    /// Every pinch drops a new label at the pointed location.
    /// </summary>
    /// <remarks>
    /// The smallest end-to-end spatial annotation demo. It listens to the
    /// existing targeting system rather than reading hands itself, so the
    /// ray, reticle, pinch detection and fallback placement are all the ones
    /// already proven on device.
    /// <para>
    /// Annotations are dispatched in <see cref="SpatialSpace.World"/>, which
    /// parents them under the dispatcher's world root instead of the single
    /// moving target. That is what makes each pinch add a new annotation
    /// rather than relocate the previous one, and what keeps them fixed in
    /// the room when the head moves.
    /// </para>
    /// </remarks>
    public class PinchAnnotationPlacer : MonoBehaviour
    {
        [SerializeField] private SpatialTargetController targetController;
        [SerializeField] private SpatialActionDispatcher dispatcher;

        [Tooltip("Shown at every pinched location. Use \\n for line breaks.")]
        [SerializeField, TextArea]
        private string labelText = "OBJECT\nFrench: objet\nSpanish: objeto";

        [Tooltip("Metres the label floats above the pinched point.")]
        [SerializeField] private float labelLift = 0.06f;

        [Tooltip("Size multiplier so the label reads at arm's length and beyond.")]
        [SerializeField] private float labelScale = 2.5f;

        [SerializeField] private bool dropMarker = true;

        /// <summary>How many annotations this component has placed.</summary>
        public int PlacedCount { get; private set; }

        private void Awake()
        {
            if (targetController == null) targetController = FindAnyObjectByType<SpatialTargetController>();
            if (dispatcher == null) dispatcher = FindAnyObjectByType<SpatialActionDispatcher>();
        }

        private void OnEnable()
        {
            if (targetController != null) targetController.TargetSelected += OnTargetSelected;
        }

        private void OnDisable()
        {
            if (targetController != null) targetController.TargetSelected -= OnTargetSelected;
        }

        private void OnTargetSelected(SpatialTarget target)
        {
            if (target == null || dispatcher == null) return;

            // World position of the pinch, captured now: the target itself
            // will move on the next pinch, the annotation must not.
            var point = target.transform.position;

            if (dropMarker)
            {
                var marker = SpatialAction.Marker(point);
                marker.Space = SpatialSpace.World;
                marker.Scale = labelScale;
                dispatcher.Dispatch(marker);
            }

            var label = SpatialAction.Label(point + Vector3.up * labelLift, labelText);
            label.Space = SpatialSpace.World;
            label.Scale = labelScale;
            dispatcher.Dispatch(label);

            PlacedCount++;
            Debug.Log("[SpatialDebugger] placed annotation #" + PlacedCount + " at " + point);
        }
    }
}
