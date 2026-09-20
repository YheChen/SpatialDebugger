using SpatialDebugger.Core;
using UnityEngine;

namespace SpatialDebugger.Demo
{
    /// <summary>
    /// One definition of what a pinch produces.
    /// </summary>
    /// <remarks>
    /// Shared by <see cref="PinchAnnotationPlacer"/> and the tests, so the
    /// thing that is tested is the thing that runs on the headset rather than
    /// a reimplementation of it.
    /// </remarks>
    public static class PinchDemo
    {
        /// <summary>
        /// The label action for the <paramref name="index"/>th pinch at
        /// <paramref name="worldPoint"/>.
        /// </summary>
        /// <remarks>
        /// World space on purpose: that is what makes each pinch ADD an
        /// annotation fixed in the room, rather than move the previous one
        /// along with the target.
        /// </remarks>
        public static SpatialAction SpatialActionForPinch(int index, Vector3 worldPoint,
            float scale = 2.5f)
        {
            var entry = Vocabulary.At(index);

            var action = SpatialAction.Label(worldPoint, entry.ToLabel());
            action.Space = SpatialSpace.World;
            action.Scale = scale;
            return action;
        }
    }
}
