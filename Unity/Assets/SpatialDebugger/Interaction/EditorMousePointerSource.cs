using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SpatialDebugger.Interaction
{
    /// <summary>
    /// Mouse pointing, so the whole targeting and annotation pipeline can be
    /// exercised in Play mode on a Mac with no headset attached.
    /// </summary>
    /// <remarks>
    /// Being able to test the demo without the Quest is what makes the rest of
    /// this verifiable at all before device time.
    /// </remarks>
    public class EditorMousePointerSource : MonoBehaviour, IPointerSource
    {
        [SerializeField] private Camera viewer;

        [Tooltip("Higher than head gaze so the mouse wins on desktop, lower than " +
                 "hand tracking so hands win in the headset.")]
        [SerializeField] private int priority = 20;

        public string SourceName => "Mouse";
        public int Priority => priority;

        private Camera Viewer
        {
            get
            {
                if (viewer != null) return viewer;
                viewer = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
                return viewer;
            }
        }

        public bool IsActive
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Application.isEditor && Mouse.current != null && Viewer != null;
#else
                return false;
#endif
            }
        }

        public PointerSample Sample()
        {
#if ENABLE_INPUT_SYSTEM
            var camera = Viewer;
            if (camera == null || Mouse.current == null) return PointerSample.Invalid;

            var screenPosition = Mouse.current.position.ReadValue();
            return new PointerSample
            {
                IsValid = true,
                Ray = camera.ScreenPointToRay(screenPosition),
                SelectStarted = Mouse.current.leftButton.wasPressedThisFrame,
                SelectHeld = Mouse.current.leftButton.isPressed
            };
#else
            return PointerSample.Invalid;
#endif
        }
    }
}
