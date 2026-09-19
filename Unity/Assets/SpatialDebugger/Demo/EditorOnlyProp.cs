using UnityEngine;

namespace SpatialDebugger.Demo
{
    /// <summary>
    /// Removes its GameObject outside the Editor.
    /// </summary>
    /// <remarks>
    /// Used for the stand-in breadboard: in Play mode on a Mac there is
    /// nothing physical to point at, so a prop with a collider is what makes
    /// the whole targeting-and-annotation pipeline testable without a headset.
    /// In passthrough on device a floating grey box would just be confusing,
    /// so it deletes itself.
    /// </remarks>
    public class EditorOnlyProp : MonoBehaviour
    {
        private void Awake()
        {
            if (Application.isEditor) return;
            Destroy(gameObject);
        }
    }
}
