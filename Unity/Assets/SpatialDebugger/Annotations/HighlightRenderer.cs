using UnityEngine;

namespace SpatialDebugger.Annotations
{
    /// <summary>
    /// A translucent sphere that calls out a whole region rather than a point.
    /// </summary>
    /// <remarks>
    /// Rendered as two nested spheres: a soft fill for volume, and a brighter
    /// shell that breathes. A single translucent sphere in passthrough tends
    /// to disappear against a busy background.
    /// </remarks>
    public class HighlightRenderer : AnnotationRenderer
    {
        protected override Color DefaultColor => AnnotationVisuals.HighlightColor;

        private Transform _shell;
        private float _radius;

        protected override void Build()
        {
            var color = ResolvedColor;
            var scale = ResolvedScale;

            _radius = (Action != null && Action.HasRadius ? Action.Radius : 0.04f) * scale;
            var diameter = _radius * 2f;

            AnnotationVisuals.Primitive(
                PrimitiveType.Sphere, transform, "Fill",
                Vector3.zero, Vector3.one * diameter,
                AnnotationVisuals.Transparent(new Color(color.r, color.g, color.b, 0.18f)));

            var shell = AnnotationVisuals.Primitive(
                PrimitiveType.Sphere, transform, "Shell",
                Vector3.zero, Vector3.one * (diameter * 1.06f),
                AnnotationVisuals.Transparent(new Color(color.r, color.g, color.b, 0.30f)));
            _shell = shell.transform;

            // A flat disc on the surface plane anchors the sphere to the object.
            AnnotationVisuals.Primitive(
                PrimitiveType.Cylinder, transform, "Footprint",
                new Vector3(0f, -_radius, 0f),
                new Vector3(diameter, 0.0008f * scale, diameter),
                AnnotationVisuals.Transparent(new Color(color.r, color.g, color.b, 0.40f)));
        }

        protected override void Update()
        {
            base.Update();
            if (_shell == null) return;

            var breathe = 1.06f + Mathf.Sin(Age * 2.4f) * 0.05f;
            _shell.localScale = Vector3.one * (_radius * 2f * breathe);
        }
    }
}
