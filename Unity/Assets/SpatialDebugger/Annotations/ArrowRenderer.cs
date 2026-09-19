using SpatialDebugger.Core;
using UnityEngine;

namespace SpatialDebugger.Annotations
{
    /// <summary>
    /// A shaft with a cone head, running from <c>from</c> to <c>to</c>.
    /// </summary>
    /// <remarks>
    /// This is the action that carries the actual instruction -- "connect this
    /// wire here" -- so it is the one that most needs to be unambiguous. The
    /// head is inset from the endpoint by its own length so the tip lands
    /// exactly on <c>to</c> rather than overshooting it.
    /// </remarks>
    public class ArrowRenderer : AnnotationRenderer
    {
        protected override Color DefaultColor => AnnotationVisuals.ArrowColor;

        private Transform _head;
        private Vector3 _headBase;
        private Vector3 _direction;

        protected override void Build()
        {
            if (Action == null) return;

            var color = ResolvedColor;
            var scale = ResolvedScale;
            var material = AnnotationVisuals.Opaque(color);

            // The dispatcher positions this object at 'from', so the arrow is
            // drawn in local space from the origin to the offset endpoint.
            var to = Action.To - Action.From;
            var length = to.magnitude;

            if (length < 1e-4f)
            {
                Debug.LogWarning("[SpatialDebugger] arrow has zero length; skipping.");
                return;
            }

            _direction = to / length;

            var headLength = Mathf.Min(0.03f * scale, length * 0.35f);
            var headRadius = Mathf.Min(0.011f * scale, headLength * 0.6f);
            _headBase = _direction * (length - headLength);

            AnnotationVisuals.Segment(
                transform, "Shaft", Vector3.zero, _headBase, 0.004f * scale, material);

            var head = AnnotationVisuals.ArrowHead(transform, "Head", headLength, headRadius, material);
            head.transform.localPosition = _headBase;
            head.transform.localRotation = Quaternion.LookRotation(_direction, Vector3.up);
            _head = head.transform;

            // Small cap at the tail so the arrow reads as directional even
            // when seen end-on.
            AnnotationVisuals.Primitive(
                PrimitiveType.Sphere, transform, "Tail",
                Vector3.zero, Vector3.one * (0.008f * scale), material);

            BuildCaption(to, scale, color);
        }

        private void BuildCaption(Vector3 to, float scale, Color color)
        {
            if (string.IsNullOrWhiteSpace(Action.Text)) return;

            var caption = new GameObject("Caption");
            caption.transform.SetParent(transform, false);
            // Sit above the midpoint so the text never overlaps the shaft.
            caption.transform.localPosition = to * 0.5f + Vector3.up * (0.03f * scale);
            caption.AddComponent<Billboard>();

            var width = SpatialText.EstimateWidth(Action.Text) * scale;

            AnnotationVisuals.Primitive(
                PrimitiveType.Quad, caption.transform, "Backing",
                new Vector3(0f, 0f, 0.002f),
                new Vector3(width + 0.018f * scale, 0.032f * scale, 1f),
                AnnotationVisuals.Transparent(AnnotationVisuals.PanelColor));

            AnnotationVisuals.Primitive(
                PrimitiveType.Quad, caption.transform, "Accent",
                new Vector3(0f, -0.019f * scale, 0.0015f),
                new Vector3(width + 0.018f * scale, 0.003f * scale, 1f),
                AnnotationVisuals.Opaque(color));

            SpatialText.Create(caption.transform, Action.Text, 0.017f * scale, Color.white);
        }

        protected override void Update()
        {
            base.Update();
            if (_head == null) return;

            // Nudge the head along its own axis so the arrow reads as "go that way".
            var slide = Mathf.Sin(Age * 3f) * 0.004f * ResolvedScale;
            _head.localPosition = _headBase + _direction * slide;
        }
    }
}
