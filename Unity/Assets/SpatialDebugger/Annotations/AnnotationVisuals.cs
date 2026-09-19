using System.Collections.Generic;
using UnityEngine;

namespace SpatialDebugger.Annotations
{
    /// <summary>
    /// Builds every annotation visual in code.
    /// </summary>
    /// <remarks>
    /// Nothing here loads a prefab. Procedural construction means the scene
    /// has no asset GUIDs to go stale, the Editor setup tool stays trivial,
    /// and the same code path runs in the Editor and on device.
    /// <para>
    /// Materials are the one real hazard: <see cref="Shader.Find"/> returns
    /// null in a player build for any shader nothing references, so the
    /// Editor setup tool also adds these shaders to Graphics Settings'
    /// "Always Included Shaders". <see cref="ResolveShader"/> walks a fallback
    /// chain regardless, so a stripped shader degrades to something visible
    /// rather than to magenta.
    /// </para>
    /// </remarks>
    public static class AnnotationVisuals
    {
        public static readonly Color LabelColor = new Color(0.18f, 0.77f, 0.95f);
        public static readonly Color WarningColor = new Color(1f, 0.25f, 0.21f);
        public static readonly Color MarkerColor = new Color(1f, 0.62f, 0.11f);
        public static readonly Color ArrowColor = new Color(1f, 0.62f, 0.11f);
        public static readonly Color HighlightColor = new Color(1f, 0.25f, 0.21f);
        public static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.08f, 0.82f);

        /// <summary>Shaders the Editor tool must pin into the build.</summary>
        public static readonly string[] RequiredShaders =
        {
            "Universal Render Pipeline/Unlit",
            "Unlit/Color",
            "Sprites/Default",
            "TextMeshPro/Distance Field",
            "TextMeshPro/Mobile/Distance Field"
        };

        private static readonly Dictionary<int, Material> OpaqueCache = new Dictionary<int, Material>();
        private static readonly Dictionary<int, Material> TransparentCache = new Dictionary<int, Material>();
        private static Shader _unlitShader;

        // -- materials -----------------------------------------------------

        private static Shader ResolveShader()
        {
            if (_unlitShader != null) return _unlitShader;

            // URP first (this project renders with URP 17.x), then built-ins
            // that survive in every pipeline.
            _unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                           ?? Shader.Find("Unlit/Color")
                           ?? Shader.Find("Sprites/Default");

            if (_unlitShader == null)
            {
                Debug.LogError("[SpatialDebugger] No usable unlit shader found. Annotations " +
                               "will render with the error shader. Run " +
                               "Tools > SpatialDebugger > Set Up Scene to pin the shaders into the build.");
            }

            return _unlitShader;
        }

        public static Material Opaque(Color color)
        {
            var key = ColorKey(color);
            if (OpaqueCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var material = new Material(ResolveShader()) { name = "SD_Opaque_" + key };
            SetColor(material, color);
            OpaqueCache[key] = material;
            return material;
        }

        public static Material Transparent(Color color)
        {
            var key = ColorKey(color);
            if (TransparentCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var material = new Material(ResolveShader()) { name = "SD_Transparent_" + key };
            SetColor(material, color);
            MakeTransparent(material);
            TransparentCache[key] = material;
            return material;
        }

        /// <summary>
        /// Flips a URP/Unlit material to the transparent surface mode. URP reads
        /// these properties *and* the shader keyword, so both have to be set.
        /// </summary>
        private static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);   // 0 opaque, 1 transparent
            material.SetFloat("_Blend", 0f);     // alpha
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static void SetColor(Material material, Color color)
        {
            // URP/Unlit uses _BaseColor; the built-in fallbacks use _Color.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static int ColorKey(Color color)
        {
            return (Mathf.RoundToInt(color.r * 255) << 24) |
                   (Mathf.RoundToInt(color.g * 255) << 16) |
                   (Mathf.RoundToInt(color.b * 255) << 8) |
                   Mathf.RoundToInt(color.a * 255);
        }

        // -- primitives ----------------------------------------------------

        /// <summary>
        /// A primitive with its collider stripped. Annotations are decoration:
        /// leaving colliders on them would block the targeting raycast.
        /// </summary>
        public static GameObject Primitive(PrimitiveType type, Transform parent, string name,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;

            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return go;
        }

        /// <summary>A thin line between two local-space points, drawn as a stretched cube.</summary>
        public static GameObject Segment(Transform parent, string name, Vector3 from, Vector3 to,
            float thickness, Material material)
        {
            var go = Primitive(PrimitiveType.Cube, parent, name, Vector3.zero, Vector3.one, material);
            PlaceSegment(go.transform, from, to, thickness);
            return go;
        }

        public static void PlaceSegment(Transform transform, Vector3 from, Vector3 to, float thickness)
        {
            var delta = to - from;
            var length = delta.magnitude;

            transform.localPosition = (from + to) * 0.5f;
            transform.localRotation = length > 1e-5f
                ? Quaternion.LookRotation(delta.normalized, Vector3.up)
                : Quaternion.identity;
            transform.localScale = new Vector3(thickness, thickness, Mathf.Max(length, 1e-4f));
        }

        /// <summary>
        /// A cone pointing along +Z in local space, built from a cylinder
        /// because Unity has no cone primitive.
        /// </summary>
        public static GameObject ArrowHead(Transform parent, string name, float length, float radius,
            Material material)
        {
            var head = new GameObject(name);
            head.transform.SetParent(parent, false);

            var mesh = ConeMesh(length, radius, 12);
            var filter = head.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = head.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return head;
        }

        /// <summary>Cone with its base at the origin and its tip at +Z * length.</summary>
        private static Mesh ConeMesh(float length, float radius, int segments)
        {
            segments = Mathf.Max(3, segments);

            var vertices = new Vector3[segments + 2];
            vertices[0] = Vector3.zero;                    // base centre
            vertices[1] = new Vector3(0f, 0f, length);     // tip

            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                vertices[i + 2] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            }

            var triangles = new int[segments * 6];
            for (var i = 0; i < segments; i++)
            {
                var current = i + 2;
                var next = (i + 1) % segments + 2;

                // Side facing outward from the tip.
                triangles[i * 6 + 0] = 1;
                triangles[i * 6 + 1] = next;
                triangles[i * 6 + 2] = current;

                // Base cap.
                triangles[i * 6 + 3] = 0;
                triangles[i * 6 + 4] = current;
                triangles[i * 6 + 5] = next;
            }

            var mesh = new Mesh { name = "SD_Cone" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
