using UnityEngine;

namespace SpatialDebugger.Annotations
{
    /// <summary>
    /// A pin: a sphere on a short stalk with a ground ring.
    /// </summary>
    /// <remarks>
    /// The ring and the stalk exist for depth perception. A bare sphere
    /// floating over a breadboard is genuinely hard to localise in passthrough;
    /// a stalk down to a ring on the surface reads as "this exact spot".
    /// </remarks>
    public class MarkerRenderer : AnnotationRenderer
    {
        protected override Color DefaultColor => AnnotationVisuals.MarkerColor;

        private Transform _head;
        private float _headHeight;

        protected override void Build()
        {
            var color = ResolvedColor;
            var scale = ResolvedScale;
            var material = AnnotationVisuals.Opaque(color);

            _headHeight = 0.022f * scale;

            AnnotationVisuals.Segment(
                transform, "Stalk", Vector3.zero, Vector3.up * _headHeight,
                0.002f * scale, material);

            var head = AnnotationVisuals.Primitive(
                PrimitiveType.Sphere, transform, "Head",
                Vector3.up * _headHeight, Vector3.one * (0.012f * scale), material);
            _head = head.transform;

            // Flattened sphere as a ground ring; a torus would need a custom mesh.
            AnnotationVisuals.Primitive(
                PrimitiveType.Cylinder, transform, "Ring",
                Vector3.zero,
                new Vector3(0.03f * scale, 0.0006f * scale, 0.03f * scale),
                AnnotationVisuals.Transparent(new Color(color.r, color.g, color.b, 0.35f)));
        }

        protected override void Update()
        {
            base.Update();
            if (_head == null) return;

            // Gentle bob so the marker is findable when the user looks away and back.
            var bob = Mathf.Sin(Age * 2.2f) * 0.0025f * ResolvedScale;
            _head.localPosition = Vector3.up * (_headHeight + bob);
        }
    }
}
