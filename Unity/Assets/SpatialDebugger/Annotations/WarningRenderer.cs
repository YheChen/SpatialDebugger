using UnityEngine;

namespace SpatialDebugger.Annotations
{
    /// <summary>
    /// A label that demands attention: red, marked with a cross, and pulsing.
    /// </summary>
    /// <remarks>
    /// Subclasses <see cref="LabelRenderer"/> because a warning *is* a label
    /// with a different default colour and more urgency. It floats higher so
    /// a warning and a label on the same point do not overlap.
    /// </remarks>
    public class WarningRenderer : LabelRenderer
    {
        protected override Color DefaultColor => AnnotationVisuals.WarningColor;

        protected override float Lift => 0.09f;

        private Transform _badge;
        private Vector3 _badgeScale;

        protected override void Build()
        {
            base.Build();

            var color = ResolvedColor;
            var scale = ResolvedScale;

            var plate = transform.Find("Plate");
            if (plate == null) return;

            // A cross badge to the left of the caption. Two crossed bars read
            // as "wrong" without depending on a font or an icon atlas.
            var badge = new GameObject("Badge");
            badge.transform.SetParent(plate, false);

            var text = Action != null ? Action.Text : string.Empty;
            var width = SpatialText.EstimateWidth(text) * scale;
            badge.transform.localPosition = new Vector3(-(width * 0.5f + 0.022f * scale), 0f, 0f);

            var barMaterial = AnnotationVisuals.Opaque(color);
            var arm = 0.011f * scale;

            var first = AnnotationVisuals.Primitive(
                PrimitiveType.Cube, badge.transform, "Stroke1",
                Vector3.zero, new Vector3(arm * 2f, 0.0035f * scale, 0.0035f * scale), barMaterial);
            first.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var second = AnnotationVisuals.Primitive(
                PrimitiveType.Cube, badge.transform, "Stroke2",
                Vector3.zero, new Vector3(arm * 2f, 0.0035f * scale, 0.0035f * scale), barMaterial);
            second.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);

            _badge = badge.transform;
            _badgeScale = _badge.localScale;
        }

        protected override void Update()
        {
            base.Update();
            if (_badge == null) return;

            // Slow, shallow pulse: noticeable in peripheral vision, not nauseating.
            var pulse = 1f + Mathf.Sin(Age * 4f) * 0.12f;
            _badge.localScale = _badgeScale * pulse;
        }
    }
}
