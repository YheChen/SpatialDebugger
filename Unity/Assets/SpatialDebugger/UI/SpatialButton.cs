using System;
using SpatialDebugger.Annotations;
using UnityEngine;
using UnityEngine.Events;

namespace SpatialDebugger.UI
{
    /// <summary>
    /// A world-space button made of a collider and procedural geometry.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT a uGUI Button on a world-space Canvas. That route needs
    /// the Interaction SDK's PointableCanvas / PointableCanvasModule /
    /// ClippedPlaneSurface graph, or an EventSystem input module -- and this
    /// project is Input-System-only, which rules out Meta's
    /// <c>OVRInputModule</c> (it is <c>ENABLE_LEGACY_INPUT_MANAGER</c>-gated).
    /// A collider driven by the same <see cref="Interaction.IPointerSource"/>
    /// the targeting already uses is far less scene wiring to get wrong, and
    /// it behaves identically under a mouse in the Editor and a pinch on device.
    /// </remarks>
    [RequireComponent(typeof(BoxCollider))]
    public class SpatialButton : MonoBehaviour
    {
        [SerializeField] private string label = "Button";
        [SerializeField] private Color accent = new Color(0.18f, 0.77f, 0.95f);
        [SerializeField] private bool interactable = true;

        /// <summary>Wired in the Inspector.</summary>
        public UnityEvent OnPressed = new UnityEvent();

        /// <summary>Wired from code.</summary>
        public event Action Pressed;

        private Transform _plate;
        private Renderer _plateRenderer;
        private Renderer _accentRenderer;
        private SpatialLabel _text;
        private bool _hovered;

        public string Label
        {
            get => label;
            set
            {
                label = value;
                if (_text != null) _text.Text = value;
            }
        }

        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                Refresh();
            }
        }

        public Vector2 Size { get; private set; } = new Vector2(0.18f, 0.045f);

        /// <summary>
        /// Builds the button under <paramref name="parent"/>. Called by the
        /// panel rather than by Unity, so sizing can be driven by layout.
        /// </summary>
        public static SpatialButton Create(Transform parent, string label, Vector2 size,
            Color accent, Vector3 localPosition)
        {
            var go = new GameObject("Button_" + label);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            // Layer 5 is Unity's built-in "UI" layer. The targeting raycaster
            // excludes it, so pointing at a button never also moves the target.
            go.layer = 5;

            var button = go.AddComponent<SpatialButton>();
            button.label = label;
            button.accent = accent;
            button.Size = size;
            button.Build();
            return button;
        }

        private void Build()
        {
            var size = Size;

            var collider = GetComponent<BoxCollider>();
            collider.size = new Vector3(size.x, size.y, 0.012f);
            collider.isTrigger = true;

            _plate = new GameObject("Plate").transform;
            _plate.SetParent(transform, false);
            _plate.gameObject.layer = 5;

            var plate = AnnotationVisuals.Primitive(
                PrimitiveType.Quad, _plate, "Face", new Vector3(0f, 0f, 0.004f),
                new Vector3(size.x, size.y, 1f),
                AnnotationVisuals.Transparent(IdleColor));
            plate.layer = 5;
            _plateRenderer = plate.GetComponent<Renderer>();

            var accentBar = AnnotationVisuals.Primitive(
                PrimitiveType.Quad, _plate, "Accent",
                new Vector3(-(size.x * 0.5f) + 0.004f, 0f, 0.003f),
                new Vector3(0.006f, size.y * 0.72f, 1f),
                AnnotationVisuals.Opaque(accent));
            accentBar.layer = 5;
            _accentRenderer = accentBar.GetComponent<Renderer>();

            _text = SpatialText.Create(_plate, label, size.y * 0.42f, Color.white);
            _text.transform.localPosition = new Vector3(0.006f, 0f, 0.001f);

            Refresh();
        }

        private Color IdleColor => interactable
            ? new Color(0.10f, 0.12f, 0.17f, 0.92f)
            : new Color(0.08f, 0.09f, 0.11f, 0.55f);

        private Color HoverColor => new Color(
            Mathf.Lerp(0.10f, accent.r, 0.32f),
            Mathf.Lerp(0.12f, accent.g, 0.32f),
            Mathf.Lerp(0.17f, accent.b, 0.32f), 0.96f);

        internal void SetHovered(bool hovered)
        {
            if (_hovered == hovered) return;
            _hovered = hovered;
            Refresh();
        }

        private void Refresh()
        {
            if (_plateRenderer != null)
            {
                _plateRenderer.sharedMaterial = AnnotationVisuals.Transparent(
                    _hovered && interactable ? HoverColor : IdleColor);
            }

            if (_accentRenderer != null)
            {
                var colour = interactable ? accent : new Color(accent.r, accent.g, accent.b, 0.3f);
                _accentRenderer.sharedMaterial = AnnotationVisuals.Opaque(colour);
            }

            if (_text != null)
            {
                _text.Color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            }

            // A hovered button leans very slightly toward the user.
            if (_plate != null)
            {
                _plate.localPosition = new Vector3(0f, 0f, _hovered && interactable ? -0.004f : 0f);
            }
        }

        internal void Activate()
        {
            if (!interactable) return;

            OnPressed?.Invoke();
            Pressed?.Invoke();
        }
    }
}
