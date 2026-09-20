using UnityEngine;

namespace SpatialDebugger.Annotations
{
    /// <summary>
    /// A floating caption with a leader line down to the point it names.
    /// </summary>
    /// <remarks>
    /// The leader line matters: a caption alone in mid-air is ambiguous about
    /// which part of the board it refers to.
    /// </remarks>
    public class LabelRenderer : AnnotationRenderer
    {
        protected override Color DefaultColor => AnnotationVisuals.LabelColor;

        /// <summary>How far above the anchor the caption floats, in metres.</summary>
        protected virtual float Lift => 0.05f;

        protected override void Build()
        {
            var color = ResolvedColor;
            var scale = ResolvedScale;
            var lift = Lift * scale;

            // Anchor dot at the exact point being named.
            AnnotationVisuals.Primitive(
                PrimitiveType.Sphere, transform, "Anchor",
                Vector3.zero, Vector3.one * (0.008f * scale),
                AnnotationVisuals.Opaque(color));

            // Leader line from the anchor up to the caption.
            AnnotationVisuals.Segment(
                transform, "Leader", Vector3.zero, Vector3.up * lift,
                0.0015f * scale, AnnotationVisuals.Opaque(color));

            // The caption itself, billboarded so it is always readable.
            var plate = new GameObject("Plate");
            plate.transform.SetParent(transform, false);
            plate.transform.localPosition = Vector3.up * lift;
            plate.AddComponent<Billboard>();

            var text = Action != null ? Action.Text : string.Empty;
            var width = SpatialText.EstimateWidth(text) * scale;
            var lines = string.IsNullOrEmpty(text) ? 1 : text.Split('\n').Length;
            var height = 0.028f * scale * lines;

            AnnotationVisuals.Primitive(
                PrimitiveType.Quad, plate.transform, "Backing",
                new Vector3(0f, 0f, 0.002f),
                new Vector3(width + 0.02f * scale, height + 0.012f * scale, 1f),
                AnnotationVisuals.Transparent(AnnotationVisuals.PanelColor));

            // A thin coloured bar under the caption keys it to the action type,
            // and is the only cue left if TMP resources are missing.
            AnnotationVisuals.Primitive(
                PrimitiveType.Quad, plate.transform, "Accent",
                new Vector3(0f, -(height * 0.5f + 0.004f * scale), 0.0015f),
                new Vector3(width + 0.02f * scale, 0.004f * scale, 1f),
                AnnotationVisuals.Opaque(color));

            // height covers every line; the cap height of one line is what
            // SpatialText needs.
            SpatialText.Create(plate.transform, text, height / lines * 0.55f, Color.white);
        }
    }
}
