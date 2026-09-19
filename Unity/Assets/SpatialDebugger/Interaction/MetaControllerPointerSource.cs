using UnityEngine;

namespace SpatialDebugger.Interaction
{
    /// <summary>
    /// Touch controller pointing: ray from the controller anchor, commit on
    /// the index trigger.
    /// </summary>
    /// <remarks>
    /// Present because controllers are more reliable than hand tracking in bad
    /// lighting, and a demo should not depend on the venue's lighting.
    /// Requires the Oculus Touch Interaction Profile to be enabled in OpenXR
    /// settings -- without it <see cref="OVRInput"/> silently reports nothing.
    /// </remarks>
    public class MetaControllerPointerSource : MonoBehaviour, IPointerSource
    {
        [Tooltip("Below hand tracking: if the user has put the controllers down " +
                 "and is using hands, hands should win.")]
        [SerializeField] private int priority = 30;

        public string SourceName => "Controller";
        public int Priority => priority;

        private OVRCameraRig _rig;
        private bool _wasPressed;

        private OVRCameraRig Rig
        {
            get
            {
                if (_rig == null) _rig = FindAnyObjectByType<OVRCameraRig>();
                return _rig;
            }
        }

        private OVRInput.Controller Active
        {
            get
            {
                var active = OVRInput.GetActiveController();

                // GetActiveController can report combined masks; narrow to one.
                if ((active & OVRInput.Controller.RTouch) != 0) return OVRInput.Controller.RTouch;
                if ((active & OVRInput.Controller.LTouch) != 0) return OVRInput.Controller.LTouch;
                return OVRInput.Controller.None;
            }
        }

        public bool IsActive => Active != OVRInput.Controller.None && AnchorFor(Active) != null;

        public PointerSample Sample()
        {
            var controller = Active;
            if (controller == OVRInput.Controller.None)
            {
                _wasPressed = false;
                return PointerSample.Invalid;
            }

            var anchor = AnchorFor(controller);
            if (anchor == null) return PointerSample.Invalid;

            var pressed = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller);
            var started = pressed && !_wasPressed;
            _wasPressed = pressed;

            return new PointerSample
            {
                IsValid = true,
                Ray = new Ray(anchor.position, anchor.forward),
                SelectStarted = started,
                SelectHeld = pressed
            };
        }

        private Transform AnchorFor(OVRInput.Controller controller)
        {
            var rig = Rig;
            if (rig == null) return null;

            return controller == OVRInput.Controller.LTouch
                ? rig.leftHandAnchor
                : rig.rightHandAnchor;
        }
    }
}
