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
            // A <size=...%> tag makes its line taller than a normal one; give
            // the plate a little slack so an enlarged heading is not clipped.
            if (text != null && text.Contains("<size=")) lines += 1;
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
            _label = SpatialText.Create(plate.transform, text, height / lines * 0.55f, Color.white);
            _plate = plate.transform;
        }

        private SpatialLabel _label;
        private Transform _plate;

        /// <summary>
        /// Replaces the caption after the annotation has been built, resizing
        /// the backing plate to fit.
        /// </summary>
        /// <remarks>
        /// Exists for the recognition flow: a label is placed immediately
        /// saying "analysing", then rewritten in place when the answer
        /// arrives. Resizing matters because the plate is sized once from the
        /// text it was built with, so a longer answer would otherwise overflow
        /// a placeholder-sized quad.
        /// </remarks>
        public void SetText(string text)
        {
            if (_label == null || _plate == null) return;

            _label.Text = text;

            var scale = ResolvedScale;
            var width = SpatialText.EstimateWidth(text) * scale;
            var lines = string.IsNullOrEmpty(text) ? 1 : text.Split('\n').Length;
            if (text != null && text.Contains("<size=")) lines += 1;
            var height = 0.028f * scale * lines;

            var backing = _plate.Find("Backing");
            if (backing != null)
            {
                backing.localScale =
                    new Vector3(width + 0.02f * scale, height + 0.012f * scale, 1f);
            }

            var accent = _plate.Find("Accent");
            if (accent != null)
            {
                accent.localScale = new Vector3(width + 0.02f * scale, 0.004f * scale, 1f);
                accent.localPosition =
                    new Vector3(0f, -(height * 0.5f + 0.004f * scale), 0.0015f);
            }

            // Cap height is per line, so it changes when the line count does.
            _label.transform.localScale = Vector3.one;
            var rebuilt = SpatialText.Create(_plate, text, height / lines * 0.55f, Color.white);
            if (rebuilt != null)
            {
                rebuilt.transform.localPosition = _label.transform.localPosition;
                AnnotationVisuals.SafeDestroy(_label.gameObject);
                _label = rebuilt;
            }
        }
    }
}
