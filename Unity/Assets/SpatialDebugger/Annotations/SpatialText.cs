using UnityEngine;

namespace SpatialDebugger.Annotations
{
    /// <summary>Where a label's text sits relative to its transform origin.</summary>
    public enum SpatialTextAlign
    {
        Center = 0,
        Left
    }

    /// <summary>
    /// World-space text that renders whether or not TextMeshPro's resources
    /// have been imported.
    /// </summary>
    /// <remarks>
    /// TextMeshPro needs its "Essential Resources" — a <c>TMP Settings</c>
    /// asset and a default font — imported into the project. A fresh project
    /// does not have them, the import cannot be driven reliably from batch
    /// mode, and a <c>TextMeshPro</c> with no font asset draws <em>nothing</em>.
    /// In a headset that is indistinguishable from a broken app.
    /// <para>
    /// So this wraps two backends: TMP when it is usable, and Unity's legacy
    /// <see cref="TextMesh"/> with a built-in font otherwise. The legacy path is
    /// less pretty and has no wrapping, but it has no asset dependencies at all,
    /// which means annotation text cannot silently vanish.
    /// </para>
    /// </remarks>
    public class SpatialLabel : MonoBehaviour
    {
        private TMPro.TextMeshPro _tmp;
        private TextMesh _legacy;
        private string _text = string.Empty;
        private Color _color = Color.white;

        /// <summary>Which backend actually drew this label, for diagnostics.</summary>
        public bool UsingTextMeshPro => _tmp != null;

        public string Text
        {
            get => _text;
            set
            {
                _text = value ?? string.Empty;
                if (_tmp != null) _tmp.text = _text;
                if (_legacy != null) _legacy.text = _text;
            }
        }

        public Color Color
        {
            get => _color;
            set
            {
                _color = value;
                if (_tmp != null) _tmp.color = value;
                if (_legacy != null) _legacy.color = value;
            }
        }

        internal void BuildTmp(string content, float fontSize, Color color, SpatialTextAlign align)
        {
            _tmp = gameObject.AddComponent<TMPro.TextMeshPro>();
            _tmp.fontSize = fontSize;
            _tmp.alignment = align == SpatialTextAlign.Center
                ? TMPro.TextAlignmentOptions.Center
                : TMPro.TextAlignmentOptions.TopLeft;
            _tmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
            _tmp.overflowMode = TMPro.TextOverflowModes.Overflow;
            _tmp.rectTransform.sizeDelta = new Vector2(2.4f, 0.6f);

            Text = content;
            Color = color;
            StripShadows(_tmp.GetComponent<MeshRenderer>());
        }

        internal void BuildLegacy(string content, Color color, SpatialTextAlign align)
        {
            _legacy = gameObject.AddComponent<TextMesh>();
            _legacy.font = BuiltinFont();
            _legacy.fontSize = 64;
            _legacy.characterSize = 0.02f;
            _legacy.richText = false;
            _legacy.anchor = align == SpatialTextAlign.Center
                ? TextAnchor.MiddleCenter
                : TextAnchor.UpperLeft;
            _legacy.alignment = align == SpatialTextAlign.Center
                ? TextAlignment.Center
                : TextAlignment.Left;

            var renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer != null && _legacy.font != null)
            {
                renderer.sharedMaterial = _legacy.font.material;
            }

            Text = content;
            Color = color;
            StripShadows(renderer);
        }

        private static void StripShadows(Renderer renderer)
        {
            if (renderer == null) return;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Font _builtinFont;

        private static Font BuiltinFont()
        {
            if (_builtinFont != null) return _builtinFont;

            // Unity 2022+ renamed the built-in Arial to LegacyRuntime.ttf.
            _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (_builtinFont == null)
            {
                _builtinFont = Font.CreateDynamicFontFromOSFont("Arial", 64);
            }

            if (_builtinFont == null)
            {
                Debug.LogError("[SpatialDebugger] no font available at all; annotation text " +
                               "will not render.");
            }

            return _builtinFont;
        }
    }

    /// <summary>Factory for <see cref="SpatialLabel"/>.</summary>
    public static class SpatialText
    {
        private static bool _checked;
        private static bool _tmpAvailable;

        /// <summary>
        /// True when TMP has a usable default font asset. False means the
        /// legacy backend is in use — text still renders.
        /// </summary>
        public static bool IsTextMeshProAvailable
        {
            get
            {
                if (_checked) return _tmpAvailable;
                _checked = true;

                try
                {
                    _tmpAvailable = TMPro.TMP_Settings.instance != null &&
                                    TMPro.TMP_Settings.defaultFontAsset != null;
                }
                catch (System.Exception)
                {
                    // Touching TMP_Settings before the resources exist can throw.
                    _tmpAvailable = false;
                }

                if (!_tmpAvailable)
                {
                    Debug.LogWarning(
                        "[SpatialDebugger] TextMeshPro Essential Resources are not imported, so " +
                        "annotation text is falling back to legacy TextMesh. For nicer text, use " +
                        "Window > TextMeshPro > Import TMP Essential Resources.");
                }

                return _tmpAvailable;
            }
        }

        /// <summary>
        /// World-space text as a child of <paramref name="parent"/>. Never
        /// returns null.
        /// </summary>
        /// <param name="worldHeight">Approximate cap height in metres.</param>
        public static SpatialLabel Create(Transform parent, string content, float worldHeight,
            Color color, SpatialTextAlign align = SpatialTextAlign.Center)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            if (parent != null) go.layer = parent.gameObject.layer;

            var label = go.AddComponent<SpatialLabel>();

            if (IsTextMeshProAvailable)
            {
                // TMP authors in its own units; 3.2 reads well at this scale.
                label.BuildTmp(content, 3.2f, color, align);
                go.transform.localScale = Vector3.one * (worldHeight / 0.028f * 0.012f);
            }
            else
            {
                label.BuildLegacy(content, color, align);
                // characterSize 0.02 * fontSize 64 lands near 1.28 world units
                // per line, so scale down to the requested cap height.
                go.transform.localScale = Vector3.one * (worldHeight / 1.28f);
            }

            return label;
        }

        /// <summary>Approximate world width of a rendered string, for sizing backing panels.</summary>
        public static float EstimateWidth(string content, float perCharacter = 0.011f,
            float minimum = 0.06f)
        {
            if (string.IsNullOrEmpty(content)) return minimum;
            return Mathf.Max(minimum, content.Length * perCharacter);
        }

        internal static void ResetCacheForTests()
        {
            _checked = false;
            _tmpAvailable = false;
        }
    }
}
