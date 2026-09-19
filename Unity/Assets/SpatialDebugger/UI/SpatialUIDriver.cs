using SpatialDebugger.Interaction;
using UnityEngine;

namespace SpatialDebugger.UI
{
    /// <summary>
    /// Routes a pointing ray to <see cref="SpatialButton"/>s.
    /// </summary>
    /// <remarks>
    /// Owned and pumped by <see cref="SpatialTargetController"/> rather than
    /// running its own Update, so there is never a frame-order race between
    /// "did the pointer hit the UI" and "should we move the target".
    /// </remarks>
    public class SpatialUIDriver : MonoBehaviour
    {
        [Tooltip("Layer the buttons live on. 5 is Unity's built-in UI layer.")]
        [SerializeField] private int uiLayer = 5;

        [SerializeField] private float maxDistance = 6f;

        private SpatialButton _hovered;
        private Transform _cursor;

        /// <summary>True when the pointer was over a button last time it was pumped.</summary>
        public bool IsPointerOverUI { get; private set; }

        private void Awake()
        {
            BuildCursor();
        }

        private void BuildCursor()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "UICursor";
            go.layer = uiLayer;

            var collider = go.GetComponent<Collider>();
            if (collider != null) Annotations.AnnotationVisuals.SafeDestroy(collider);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial =
                    Annotations.AnnotationVisuals.Opaque(new Color(1f, 1f, 1f, 0.9f));
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            go.transform.localScale = Vector3.one * 0.008f;
            go.SetActive(false);
            _cursor = go.transform;
        }

        /// <summary>
        /// Feeds one frame's pointing to the UI. Returns true when the UI
        /// consumed it, in which case the caller must not place a target.
        /// </summary>
        public bool ProcessPointer(PointerSample sample)
        {
            if (!sample.IsValid)
            {
                ClearHover();
                return false;
            }

            var mask = 1 << uiLayer;
            if (!Physics.Raycast(sample.Ray, out var hit, maxDistance, mask,
                    QueryTriggerInteraction.Collide))
            {
                ClearHover();
                return false;
            }

            var button = hit.collider.GetComponentInParent<SpatialButton>();
            if (button == null)
            {
                ClearHover();
                return false;
            }

            if (_hovered != button)
            {
                if (_hovered != null) _hovered.SetHovered(false);
                _hovered = button;
                _hovered.SetHovered(true);
            }

            if (_cursor != null)
            {
                _cursor.gameObject.SetActive(true);
                _cursor.position = hit.point;
            }

            IsPointerOverUI = true;

            if (sample.SelectStarted) button.Activate();

            return true;
        }

        private void ClearHover()
        {
            if (_hovered != null)
            {
                _hovered.SetHovered(false);
                _hovered = null;
            }

            if (_cursor != null && _cursor.gameObject.activeSelf)
            {
                _cursor.gameObject.SetActive(false);
            }

            IsPointerOverUI = false;
        }
    }
}
