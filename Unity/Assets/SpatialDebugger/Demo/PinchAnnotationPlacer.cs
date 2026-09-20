using SpatialDebugger.Annotations;
using SpatialDebugger.Core;
using SpatialDebugger.Interaction;
using SpatialDebugger.Vision;
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

        [Tooltip("Successive pinches walk through the vocabulary instead of " +
                 "repeating one word. Off means every pinch shows the same entry.")]
        [SerializeField] private bool cycleVocabulary = true;

        [Tooltip("Metres the label floats above the pinched point. Must clear the " +
                 "marker and the plate's own half-height, or the label covers the " +
                 "object it is naming.")]
        [SerializeField] private float labelLift = 0.18f;

        [Tooltip("Size multiplier so the label reads at arm's length and beyond.")]
        [SerializeField] private float labelScale = 2.5f;

        [SerializeField] private bool dropMarker = true;

        [Tooltip("When present and the camera is ready, the label starts as " +
                 "ANALYZING and is rewritten with the recognised object. " +
                 "Absent or not ready, the deterministic cycle is used.")]
        [SerializeField] private VisionRecognizer recognizer;

        /// <summary>Shown while the model is thinking.</summary>
        public const string AnalyzingLabel = "<size=150%>ANALYZING\u2026</size>\n\nFR  \u2014\nES  \u2014";

        /// <summary>How many annotations this component has placed.</summary>
        public int PlacedCount { get; private set; }

        private void Awake()
        {
            if (targetController == null) targetController = FindAnyObjectByType<SpatialTargetController>();
            if (dispatcher == null) dispatcher = FindAnyObjectByType<SpatialActionDispatcher>();
            if (recognizer == null) recognizer = FindAnyObjectByType<VisionRecognizer>();
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

            var index = cycleVocabulary ? PlacedCount : 0;
            var fallback = Vocabulary.At(index);
            var useRecognition = recognizer != null && recognizer.CameraReady;

            // Place something at the pinched point immediately, either way.
            // Nothing waits on the network before the user sees a result.
            var labelAction = PinchDemo.SpatialActionForPinch(
                index, point + Vector3.up * labelLift, labelScale);

            if (useRecognition) labelAction.Text = AnalyzingLabel;

            // The HANDLE is what makes update-in-place possible. Captured per
            // pinch, so overlapping requests each rewrite their own label and
            // never each other's.
            var renderer = dispatcher.DispatchTracked(labelAction) as LabelRenderer;

            PlacedCount++;
            var placedIndex = PlacedCount;

            Debug.Log("[SpatialDebugger] placed annotation #" + placedIndex + " (" +
                      (useRecognition ? "analyzing" : fallback.English) + ") at " + point);

            if (!useRecognition || renderer == null) return;

            recognizer.Recognise(point, result =>
            {
                // The annotation may have been cleared or budgeted away while
                // the model was thinking.
                if (renderer == null) return;

                VocabularyEntry entry;
                if (result != null && result.Success)
                {
                    // A genuine recognition outside the dictionary is shown as
                    // itself with the translations marked absent -- never
                    // swapped for a deterministic word, which would be faking it.
                    entry = Vocabulary.LookupOrEcho(RecognitionText.ForDisplay(result.Word));
                    Debug.Log("[SpatialDebugger] annotation #" + placedIndex +
                              " recognised as " + entry.English);
                }
                else
                {
                    entry = fallback;
                    Debug.Log("[SpatialDebugger] annotation #" + placedIndex +
                              " fell back to " + entry.English);
                }

                renderer.SetText(entry.ToLabel());
            });
        }
    }
}
