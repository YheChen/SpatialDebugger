using TMPro;
using UnityEngine;

namespace SpatialDebugger.Annotations
{
    /// <summary>
    /// Creates world-space text, and tells you honestly whether it will render.
    /// </summary>
    /// <remarks>
    /// TextMeshPro needs its "Essential Resources" imported into the project
    /// (a <c>TMP Settings</c> asset and a default font). A fresh project does
    /// not have them, and a <see cref="TextMeshPro"/> with no font asset draws
    /// nothing at all -- which in a headset looks exactly like a broken demo.
    /// The Editor setup tool imports them; <see cref="IsAvailable"/> reports
    /// whether that happened so the caller can substitute a coloured plate
    /// rather than silently showing empty space.
    /// </remarks>
    public static class SpatialText
    {
        private static bool _checked;
        private static bool _available;

        /// <summary>True when TMP has a usable default font asset.</summary>
        public static bool IsAvailable
        {
            get
            {
                if (_checked) return _available;
                _checked = true;

                try
                {
                    _available = TMP_Settings.instance != null &&
                                 TMP_Settings.defaultFontAsset != null;
                }
                catch (System.Exception)
                {
                    // Touching TMP_Settings before the resources exist can throw.
                    _available = false;
                }

                if (!_available)
                {
                    Debug.LogWarning(
                        "[SpatialDebugger] TextMeshPro Essential Resources are not imported, so " +
                        "annotation text cannot render. Run Tools > SpatialDebugger > Set Up Scene, " +
                        "or Window > TextMeshPro > Import TMP Essential Resources.");
                }

                return _available;
            }
        }

        /// <summary>
        /// World-space text centred on <paramref name="parent"/>'s origin.
        /// Returns null when TMP is unusable.
        /// </summary>
        public static TextMeshPro Create(Transform parent, string content, float height,
            Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            if (!IsAvailable) return null;

            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshPro>();
            text.text = content ?? string.Empty;
            text.fontSize = height;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;

            // TMP authors at "1 unit = 1 metre" but its default font size is
            // tuned for UI, so a world-space label needs scaling down hard.
            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(2.4f, 0.6f);
            go.transform.localScale = Vector3.one * 0.05f;

            var renderer = text.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return text;
        }

        /// <summary>Approximate world width of a rendered string, for sizing backing panels.</summary>
        public static float EstimateWidth(string content, float perCharacter = 0.011f, float minimum = 0.06f)
        {
            if (string.IsNullOrEmpty(content)) return minimum;
            return Mathf.Max(minimum, content.Length * perCharacter);
        }

        internal static void ResetCacheForTests()
        {
            _checked = false;
            _available = false;
        }
    }
}
