using UnityEngine;

namespace SpatialDebugger.Interaction
{
    /// <summary>
    /// Hand-tracked pointing: ray from the hand's pointer pose, commit on an
    /// index pinch.
    /// </summary>
    /// <remarks>
    /// Talks to <see cref="OVRHand"/> directly rather than going through the
    /// Interaction SDK's interactor graph. That is a deliberate trade: the
    /// interactor graph is the right tool for grabbing and poking UI, but for
    /// "where is this hand pointing, and did it just pinch" the raw API is far
    /// less scene wiring to get wrong, and it degrades to the other pointer
    /// sources cleanly when hands are not tracked.
    /// <para>
    /// Hands are discovered rather than assigned, because the Meta rig creates
    /// them at runtime. <c>OVRHand.HandType</c> is internal to the Oculus.VR
    /// assembly, so handedness is read through the public
    /// <c>GetHand()</c> instead.
    /// </para>
    /// </remarks>
    public class MetaHandPointerSource : MonoBehaviour, IPointerSource
    {
        [Tooltip("Which hand to follow. Either means whichever is pinching, " +
                 "preferring the right.")]
        [SerializeField] private Handedness handedness = Handedness.Either;

        [Tooltip("Above head gaze and the mouse: in the headset, hands should win.")]
        [SerializeField] private int priority = 40;

        [Tooltip("Ignore low-confidence tracking. Prevents the ray jittering off " +
                 "when a hand is half out of view.")]
        [SerializeField] private bool requireHighConfidence = true;

        public enum Handedness
        {
            Either = 0,
            Left,
            Right
        }

        public string SourceName => "Hand tracking";
        public int Priority => priority;

        private OVRHand[] _hands;
        private float _lastScan = -99f;
        private bool _wasPinching;

        /// <summary>Re-scanned periodically: the rig spawns hands after start-up.</summary>
        private OVRHand[] Hands
        {
            get
            {
                if (_hands != null && _hands.Length > 0) return _hands;
                if (Time.unscaledTime - _lastScan < 1f) return _hands;

                _lastScan = Time.unscaledTime;
                _hands = FindObjectsByType<OVRHand>(FindObjectsSortMode.None);
                return _hands;
            }
        }

        public bool IsActive => PickHand() != null;

        public PointerSample Sample()
        {
            var hand = PickHand();
            if (hand == null)
            {
                _wasPinching = false;
                return PointerSample.Invalid;
            }

            var pose = hand.PointerPose;
            if (pose == null) return PointerSample.Invalid;

            var pinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
            var started = pinching && !_wasPinching;
            _wasPinching = pinching;

            return new PointerSample
            {
                IsValid = true,
                Ray = new Ray(pose.position, pose.forward),
                SelectStarted = started,
                SelectHeld = pinching
            };
        }

        /// <summary>0..1 index pinch strength, for a reticle that reacts before commit.</summary>
        public float PinchStrength()
        {
            var hand = PickHand();
            return hand != null ? hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) : 0f;
        }

        private OVRHand PickHand()
        {
            var hands = Hands;
            if (hands == null) return null;

            OVRHand best = null;

            foreach (var hand in hands)
            {
                if (hand == null || !hand.isActiveAndEnabled) continue;
                if (!hand.IsTracked || !hand.IsPointerPoseValid) continue;
                if (requireHighConfidence && !hand.IsDataHighConfidence) continue;

                // A system gesture (palm-up menu) means the user is talking to
                // the OS, not to us.
                if (hand.IsSystemGestureInProgress) continue;

                if (!MatchesHandedness(hand)) continue;

                // Prefer a hand that is actively pinching, so mid-gesture the
                // ray does not hop to the other hand.
                if (hand.GetFingerIsPinching(OVRHand.HandFinger.Index)) return hand;

                if (best == null) best = hand;
            }

            return best;
        }

        private bool MatchesHandedness(OVRHand hand)
        {
            if (handedness == Handedness.Either) return true;

            var which = hand.GetHand();
            return handedness == Handedness.Left
                ? which == OVRPlugin.Hand.HandLeft
                : which == OVRPlugin.Hand.HandRight;
        }
    }
}
