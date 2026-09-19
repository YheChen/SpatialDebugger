using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SpatialDebugger.Interaction
{
    /// <summary>
    /// Points wherever the user is looking.
    /// </summary>
    /// <remarks>
    /// The universal fallback: it needs no controllers, no hand tracking and
    /// no Meta SDK, so targeting always works. On device it is driven by the
    /// headset pose; in the Editor it follows the scene camera.
    /// <para>
    /// Committing is deliberately multi-route -- a controller trigger, a
    /// keyboard press, a mouse click, or <see cref="TriggerSelect"/> called
    /// from a UI button -- so there is always some way to place a target.
    /// </para>
    /// </remarks>
    public class HeadGazePointerSource : MonoBehaviour, IPointerSource
    {
        [Tooltip("Camera to gaze from. Defaults to Camera.main, which on Quest is the " +
                 "rig's centre-eye anchor.")]
        [SerializeField] private Camera viewer;

        [SerializeField] private int priority = 10;

        public string SourceName => "Head gaze";
        public int Priority => priority;

        private bool _queuedSelect;

        public bool IsActive => Viewer != null;

        private Camera Viewer
        {
            get
            {
                if (viewer != null) return viewer;
                viewer = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
                return viewer;
            }
        }

        /// <summary>Commit from a UI button or a script. Consumed next frame.</summary>
        public void TriggerSelect()
        {
            _queuedSelect = true;
        }

        public PointerSample Sample()
        {
            var camera = Viewer;
            if (camera == null) return PointerSample.Invalid;

            var selectStarted = _queuedSelect || SelectPressedThisFrame();
            _queuedSelect = false;

            return new PointerSample
            {
                IsValid = true,
                Ray = new Ray(camera.transform.position, camera.transform.forward),
                SelectStarted = selectStarted,
                SelectHeld = selectStarted || SelectHeldNow()
            };
        }

        // -- input ---------------------------------------------------------
        // This project is configured for the Input System package only
        // (activeInputHandler = 1), so UnityEngine.Input would throw.

        private static bool SelectPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) return true;
#endif
            return false;
        }

        private static bool SelectHeldNow()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
            if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) return true;
#endif
            return false;
        }
    }
}
