using UnityEngine;

namespace SpatialDebugger.Interaction
{
    /// <summary>One frame's worth of pointing.</summary>
    public struct PointerSample
    {
        /// <summary>False when this source has nothing to say this frame.</summary>
        public bool IsValid;

        /// <summary>The ray, in world space.</summary>
        public Ray Ray;

        /// <summary>True on the frame the user committed (pinch, trigger, click).</summary>
        public bool SelectStarted;

        /// <summary>True while the commit is held.</summary>
        public bool SelectHeld;

        public static readonly PointerSample Invalid = new PointerSample { IsValid = false };
    }

    /// <summary>
    /// Something that can point at the world.
    /// </summary>
    /// <remarks>
    /// Deliberately not tied to Meta's SDK. Hand tracking, a controller, head
    /// gaze and the Editor mouse are all just pointer sources, so targeting
    /// works in the Editor without a headset and keeps working if a specific
    /// input modality is unavailable on device.
    /// </remarks>
    public interface IPointerSource
    {
        /// <summary>Human-readable, shown in the UI so the user knows what is driving the ray.</summary>
        string SourceName { get; }

        /// <summary>Higher wins when several sources are live at once.</summary>
        int Priority { get; }

        /// <summary>True when this source can produce a ray right now.</summary>
        bool IsActive { get; }

        PointerSample Sample();
    }
}
