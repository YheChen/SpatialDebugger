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

        [Tooltip("Skip recognition entirely and show prepared vocabulary cards. " +
                 "This is the DEMO_SCRIPT fallback demo, and it is a deliberate " +
                 "choice made before going on stage -- never something the app " +
                 "slides into because the model went quiet.")]
        [SerializeField] private bool offlineVocabularyMode;

        [Tooltip("When present and the camera is ready, the label starts as " +
                 "ANALYZING and is rewritten with the recognised object. " +
                 "Absent or not ready, the deterministic cycle is used.")]
        [SerializeField] private VisionRecognizer recognizer;

        /// <summary>Shown while the model is thinking.</summary>
        public const string AnalyzingLabel =
            "<size=150%>ANALYZING\u2026</size>\n\nFR  \u2014\nES  \u2014" +
            "\n<size=65%>ASKING THE MODEL\u2026</size>";

        /// <summary>
        /// Footer on a label the vision model genuinely produced. Never shown
        /// for a failure or for the offline cycle.
        /// </summary>
        public const string RecognizedBadge = "AI RECOGNIZED";

        /// <summary>Footer on a prepared card, so it cannot be mistaken for AI.</summary>
        public const string OfflineBadge = "OFFLINE VOCABULARY";

        /// <summary>Footer for a class the depth sensor settled, not the model.</summary>
        public const string GeometricBadge = "DEPTH SURFACE";

        /// <summary>
        /// How flat a surface must be before geometry is allowed to name it.
        /// </summary>
        public const float HorizontalNormal = 0.85f;

        /// <summary>At or below this height, a flat surface is the floor.</summary>
        /// <remarks>
        /// The tracking origin is floor level, so world Y is height above the
        /// room's floor directly.
        /// </remarks>
        public const float FloorHeight = 0.35f;

        /// <summary>At or above this height, a flat surface is the ceiling.</summary>
        public const float CeilingHeight = 1.8f;

        /// <summary>
        /// Floor or ceiling from the depth normal, or null to leave it to the
        /// model.
        /// </summary>
        /// <remarks>
        /// The normal establishes only that the surface is <i>horizontal</i>,
        /// using its absolute Y: the sensor is free to report either face, so
        /// a floor can come back as -Y. Height is what says which horizontal
        /// surface it is.
        /// <para>
        /// Height is not optional here. A table top has a normal just as
        /// strongly vertical as a floor's, so naming floors from the normal
        /// alone would rename every table in the demo. The band between the
        /// two thresholds -- desk and table height -- deliberately returns
        /// null and leaves the existing recognition path untouched.
        /// </para>
        /// </remarks>
        public static string GeometricClass(Vector3 normal, float confidence, float height)
        {
            if (confidence < SurfaceOrientation.MinimumNormalConfidence) return null;
            if (normal.sqrMagnitude < 1e-6f) return null;

            if (Mathf.Abs(normal.normalized.y) < HorizontalNormal) return null;

            if (height <= FloorHeight) return "floor";
            if (height >= CeilingHeight) return "ceiling";

            return null;
        }

        /// <summary>Separator between the badge and the measured distance.</summary>
        /// <remarks>
        /// U+00B7, not U+2022: the font atlas is a static Latin-1 set with no
        /// fallback, and a bullet would render as nothing at all.
        /// </remarks>
        private const string BadgeSeparator = "  \u00B7  ";

        /// <summary>
        /// Shown when recognition was attempted and did not produce a word.
        /// </summary>
        /// <remarks>
        /// Deliberately not a vocabulary word. A failed recognition used to be
        /// replaced by the next entry in the deterministic cycle, which on
        /// stage is indistinguishable from a successful one -- the model goes
        /// down and the demo keeps confidently naming objects it never saw.
        /// An honest blank is worth more than a plausible invention. The
        /// reason for the failure goes to logcat, not to the label.
        /// </remarks>
        public const string UnrecognizedLabel =
            "<size=150%>NOT RECOGNIZED</size>\n\nFR  \u2014\nES  \u2014" +
            "\n<size=65%>NO ANSWER FROM THE MODEL</size>";

        /// <summary>
        /// The footer for a successful recognition: that it came from the
        /// model, and how far away the raycast measured the surface.
        /// </summary>
        public static string RecognizedFooter(float metres)
        {
            return RecognizedBadge + BadgeSeparator + metres.ToString("F2") + " m";
        }

        /// <summary>
        /// The footer for a class the depth sensor settled. Deliberately not
        /// the AI badge: the model did not produce this word.
        /// </summary>
        public static string GeometricFooter(float metres)
        {
            return GeometricBadge + BadgeSeparator + metres.ToString("F2") + " m";
        }

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

            // Surface attachment, resolved in one place. A low-confidence
            // normal changes nothing at all -- not the position, not the
            // orientation -- so the known-good presentation survives whenever
            // the sensor is unsure. A depth point is never rejected for having
            // a poor normal; the distance is measured separately and was the
            // point of the whole exercise.
            var viewer = Camera.main;
            var attachment = SurfaceOrientation.For(
                point, target.SurfaceNormal, target.NormalConfidence,
                viewer != null ? viewer.transform.position : point);

            if (dropMarker)
            {
                var marker = SpatialAction.Marker(attachment.Anchor);
                marker.Space = SpatialSpace.World;
                marker.Scale = labelScale;
                marker.UpAxis = attachment.MarkerUp;
                marker.HasUpAxis = attachment.Oriented;
                dispatcher.Dispatch(marker);
            }

            var index = cycleVocabulary ? PlacedCount : 0;
            var fallback = Vocabulary.At(index);
            var useRecognition = !offlineVocabularyMode &&
                                 recognizer != null && recognizer.CameraReady;

            // Place something at the pinched point immediately, either way.
            // Nothing waits on the network before the user sees a result.
            var labelAction = PinchDemo.SpatialActionForPinch(
                index, attachment.Anchor + attachment.LabelUp * labelLift, labelScale);
            labelAction.UpAxis = attachment.LabelUp;
            labelAction.HasUpAxis = attachment.Oriented;

            if (useRecognition) labelAction.Text = AnalyzingLabel;
            else labelAction.Text = fallback.ToLabel(OfflineBadge);

            // The HANDLE is what makes update-in-place possible. Captured per
            // pinch, so overlapping requests each rewrite their own label and
            // never each other's.
            var renderer = dispatcher.DispatchTracked(labelAction) as LabelRenderer;

            PlacedCount++;
            var placedIndex = PlacedCount;

            Debug.Log("[SpatialDebugger] placed annotation #" + placedIndex + " (" +
                      (useRecognition ? "analyzing" : fallback.English) + ") at " + point +
                      " surface=" + SurfaceOrientation.Describe(attachment.Surface));

            if (!useRecognition || renderer == null) return;

            // Captured now, from the raycast. Recomputing it later would
            // measure the user's head, not the surface.
            var metres = target.SelectionDistance;

            // Floor and ceiling are geometry, not perception. Null for every
            // other surface, which leaves the recognition path exactly as it
            // was for the four classes that already work.
            var geometric = GeometricClass(
                target.SurfaceNormal, target.NormalConfidence, point.y);

            recognizer.Recognise(point, result =>
            {
                // The annotation may have been cleared or budgeted away while
                // the model was thinking.
                if (renderer == null) return;

                var word = result != null && result.Success ? result.Word : null;

                // Geometry wins where it has an answer, but the badge follows
                // whoever actually produced the word on screen.
                var shown = geometric ?? word;

                if (!string.IsNullOrEmpty(shown))
                {
                    // A genuine recognition outside the dictionary is shown as
                    // itself with the translations marked absent -- never
                    // swapped for a deterministic word, which would be faking it.
                    var entry = Vocabulary.LookupOrEcho(RecognitionText.ForDisplay(shown));
                    var fromModel = string.Equals(shown, word, System.StringComparison.Ordinal);

                    Debug.Log("[SpatialDebugger] annotation #" + placedIndex +
                              " resolved as " + entry.English +
                              (fromModel ? " (model)" : " (depth geometry, model said " +
                                                        (word ?? "nothing") + ")"));

                    renderer.SetText(entry.ToLabel(
                        fromModel ? RecognizedFooter(metres) : GeometricFooter(metres)));
                    return;
                }

                // Attempted and failed. Say so, and say why in the log.
                Debug.LogWarning("[SpatialDebugger] annotation #" + placedIndex +
                                 " not recognised: " +
                                 (result != null && !string.IsNullOrEmpty(result.Error)
                                     ? result.Error
                                     : "no result"));
                renderer.SetText(UnrecognizedLabel);
            });
        }
    }
}
