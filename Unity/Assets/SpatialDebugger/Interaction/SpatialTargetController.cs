using System;
using System.Collections.Generic;
using SpatialDebugger.Core;
using UnityEngine;

namespace SpatialDebugger.Interaction
{
    /// <summary>
    /// Drives the reticle and places the debugging target.
    /// </summary>
    /// <remarks>
    /// <code>
    /// point at the environment -> see a reticle -> pinch -> target is set
    /// </code>
    /// It picks the highest-priority live <see cref="IPointerSource"/> each
    /// frame, so a user can switch between hands, controllers, gaze and the
    /// Editor mouse without any configuration.
    /// </remarks>
    public class SpatialTargetController : MonoBehaviour
    {
        [SerializeField] private SpatialRaycaster raycaster;

        [Tooltip("Pointer sources. Left empty, every source on this GameObject " +
                 "and its children is used.")]
        [SerializeField] private MonoBehaviour[] pointerSourceBehaviours;

        [Tooltip("The target to move. Created on first selection if empty.")]
        [SerializeField] private SpatialTarget target;

        [Tooltip("Draw a beam along the pointing ray, not just a reticle at the end.")]
        [SerializeField] private bool showRay = true;

        [SerializeField] private float reticleRadius = 0.012f;

        [Tooltip("Optional. When the pointer is over a UI button, the UI consumes " +
                 "the pinch and the target is not moved.")]
        [SerializeField] private UI.SpatialUIDriver uiDriver;

        [Tooltip("Told about every new target, so target-space annotations land " +
                 "in the right place. Serialized rather than wired by an event " +
                 "subscription, so the wiring survives into the saved scene.")]
        [SerializeField] private Annotations.SpatialActionDispatcher dispatcher;

        private readonly List<IPointerSource> _sources = new List<IPointerSource>();
        private Transform _reticle;
        private LineRenderer _beam;
        private IPointerSource _activeSource;

        /// <summary>Raised whenever the target is placed or moved.</summary>
        public event Action<SpatialTarget> TargetSelected;

        /// <summary>The current target, or null before the first selection.</summary>
        public SpatialTarget Target => target;

        public bool HasTarget => target != null;

        /// <summary>Name of the pointer source currently driving the ray, for the UI.</summary>
        public string ActiveSourceName => _activeSource != null ? _activeSource.SourceName : "None";

        /// <summary>How the last hit was resolved, for honest UI reporting.</summary>
        public SpatialHit.HitSource LastHitSource { get; private set; } = SpatialHit.HitSource.None;

        private void Awake()
        {
            if (raycaster == null) raycaster = GetComponent<SpatialRaycaster>();
            if (raycaster == null) raycaster = gameObject.AddComponent<SpatialRaycaster>();

            if (uiDriver == null) uiDriver = FindAnyObjectByType<UI.SpatialUIDriver>();
            if (dispatcher == null) dispatcher = FindAnyObjectByType<Annotations.SpatialActionDispatcher>();

            CollectSources();
            BuildReticle();
        }

        private void CollectSources()
        {
            _sources.Clear();

            if (pointerSourceBehaviours != null && pointerSourceBehaviours.Length > 0)
            {
                foreach (var behaviour in pointerSourceBehaviours)
                {
                    if (behaviour is IPointerSource source) _sources.Add(source);
                }
            }

            if (_sources.Count == 0)
            {
                foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour is IPointerSource source) _sources.Add(source);
                }
            }

            // Highest priority first, so the frame loop can take the first live one.
            _sources.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            if (_sources.Count == 0)
            {
                Debug.LogWarning("[SpatialDebugger] SpatialTargetController found no pointer " +
                                 "sources. Targeting will not work.");
            }
        }

        private void BuildReticle()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Reticle";

            var collider = go.GetComponent<Collider>();
            if (collider != null) Annotations.AnnotationVisuals.SafeDestroy(collider);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial =
                    Annotations.AnnotationVisuals.Opaque(new Color(0.18f, 0.77f, 0.95f));
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            go.transform.localScale = Vector3.one * (reticleRadius * 2f);
            go.SetActive(false);
            _reticle = go.transform;

            if (!showRay) return;

            var beamGo = new GameObject("PointerBeam");
            beamGo.transform.SetParent(transform, false);

            _beam = beamGo.AddComponent<LineRenderer>();
            _beam.useWorldSpace = true;
            _beam.positionCount = 2;
            _beam.startWidth = 0.004f;
            _beam.endWidth = 0.001f;
            _beam.sharedMaterial =
                Annotations.AnnotationVisuals.Opaque(new Color(0.18f, 0.77f, 0.95f, 0.8f));
            _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _beam.receiveShadows = false;
            _beam.enabled = false;
        }

        private void Update()
        {
            if (!TryGetSample(out var sample))
            {
                SetPointerVisible(false);
                return;
            }

            // The UI gets first refusal on the pointer. Pumping it from here
            // rather than from its own Update keeps the two raycasts in a
            // guaranteed order.
            if (uiDriver != null && uiDriver.ProcessPointer(sample))
            {
                SetPointerVisible(false);
                return;
            }

            var hit = raycaster.Raycast(sample.Ray);
            if (!hit.IsValid)
            {
                SetPointerVisible(false);
                return;
            }

            LastHitSource = hit.Source;
            SetPointerVisible(true);

            _reticle.position = hit.Point;
            _reticle.rotation = hit.Normal.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(hit.Normal)
                : Quaternion.identity;

            if (_beam != null)
            {
                _beam.SetPosition(0, sample.Ray.origin);
                _beam.SetPosition(1, hit.Point);
            }

            if (sample.SelectStarted) PlaceTarget(hit);
        }

        private bool TryGetSample(out PointerSample sample)
        {
            sample = PointerSample.Invalid;

            for (var i = 0; i < _sources.Count; i++)
            {
                var source = _sources[i];
                if (source == null || !source.IsActive) continue;

                var candidate = source.Sample();
                if (!candidate.IsValid) continue;

                _activeSource = source;
                sample = candidate;
                return true;
            }

            _activeSource = null;
            return false;
        }

        private void SetPointerVisible(bool visible)
        {
            if (_reticle != null && _reticle.gameObject.activeSelf != visible)
            {
                _reticle.gameObject.SetActive(visible);
            }

            if (_beam != null && _beam.enabled != visible) _beam.enabled = visible;
        }

        // -- target placement ----------------------------------------------

        /// <summary>Places the target at a hit. Public so the UI can re-place it.</summary>
        public void PlaceTarget(SpatialHit hit)
        {
            // One line per selection, never per frame. This is how a physical
            // tester tells a label that genuinely landed on a surface from one
            // that only looks right because the fallback happened to agree.
            Debug.Log(TargetTag + DescribeHitForLog(hit));

            if (target == null)
            {
                var go = new GameObject("SpatialTarget");
                target = go.AddComponent<SpatialTarget>();
                BuildTargetVisual(go.transform);
            }

            target.transform.position = hit.Point;
            target.SurfaceNormal = hit.Normal;
            target.SelectedBy = SourceToOrigin(hit.Source);
            target.Label = DescribeTarget(hit);

            // Face the viewer while keeping +Y along the surface normal, so
            // annotations authored "above the board" sit above the board.
            var viewer = Camera.main;
            if (viewer != null) target.FaceViewer(viewer.transform.position);
            else target.AlignToSurface(hit.Normal);

            // Annotations are parented to the target, so the dispatcher has to
            // learn about it before any analysis runs.
            if (dispatcher != null) dispatcher.Target = target;

            TargetSelected?.Invoke(target);
        }

        /// <summary>
        /// Places a target in front of the viewer with no pointing at all.
        /// The UI exposes this so the demo can run even if every input
        /// modality fails.
        /// </summary>
        public void PlaceTargetInFrontOfViewer(float distance = 0.6f)
        {
            var viewer = Camera.main != null ? Camera.main.transform : null;
            if (viewer == null)
            {
                Debug.LogWarning("[SpatialDebugger] no camera; cannot place a fallback target.");
                return;
            }

            PlaceTarget(new SpatialHit
            {
                IsValid = true,
                Point = viewer.position + viewer.forward * distance,
                Normal = -viewer.forward,
                Distance = distance,
                Source = SpatialHit.HitSource.Projected
            });
        }

        private const string TargetTag = "[SpatialDebugger][target] ";

        /// <summary>
        /// Which tier resolved this placement, and at what distance.
        /// </summary>
        /// <remarks>
        /// When nothing real was hit the line also carries the depth sensor's
        /// own reason, straight from <c>EnvironmentRaycastHitStatus</c> -- the
        /// difference between "depth is switched off" and "you pointed at
        /// something the depth camera cannot see" is otherwise invisible.
        /// </remarks>
        private string DescribeHitForLog(SpatialHit hit)
        {
            var distance = hit.Distance.ToString("F2") + "m";
            var point = "point=" + hit.Point.ToString("F2");

            switch (hit.Source)
            {
                case SpatialHit.HitSource.EnvironmentDepth:
                    return "depth hit distance=" + distance + " " + point +
                           " normalConfidence=" + hit.NormalConfidence.ToString("F2");

                case SpatialHit.HitSource.SceneSurface:
                    return "MRUK scene hit distance=" + distance + " " + point;

                case SpatialHit.HitSource.Physics:
                    return "physics hit distance=" + distance + " " + point +
                           " collider=" + (hit.Collider != null ? hit.Collider.name : "unnamed");

                default:
                    return "fallback distance=" + distance + " " + point +
                           " (depth: " + DepthStatus() + ")";
            }
        }

        private string DepthStatus()
        {
            return raycaster != null ? raycaster.SurfaceProbe.LastDepthStatus : "no raycaster";
        }

        private static SpatialTarget.Origin SourceToOrigin(SpatialHit.HitSource source)
        {
            switch (source)
            {
                case SpatialHit.HitSource.EnvironmentDepth:
                case SpatialHit.HitSource.SceneSurface: return SpatialTarget.Origin.SceneSurface;
                case SpatialHit.HitSource.Physics: return SpatialTarget.Origin.HandRay;
                default: return SpatialTarget.Origin.Fallback;
            }
        }

        private string DescribeTarget(SpatialHit hit)
        {
            switch (hit.Source)
            {
                case SpatialHit.HitSource.EnvironmentDepth:
                case SpatialHit.HitSource.SceneSurface: return "Point on a real surface";
                case SpatialHit.HitSource.Physics:
                    return hit.Collider != null ? hit.Collider.name : "Point on an object";
                default: return "Point in space";
            }
        }

        /// <summary>A small reticle frame so the user can see where the target landed.</summary>
        private static void BuildTargetVisual(Transform parent)
        {
            var material = Annotations.AnnotationVisuals.Opaque(new Color(0.18f, 0.95f, 0.6f));

            // Four corner ticks read as a "selection bracket" without occluding
            // whatever the user actually wants to look at.
            const float half = 0.035f;
            const float tick = 0.012f;
            const float thickness = 0.002f;

            for (var i = 0; i < 4; i++)
            {
                var sx = (i & 1) == 0 ? -1f : 1f;
                var sz = (i & 2) == 0 ? -1f : 1f;

                Annotations.AnnotationVisuals.Primitive(
                    PrimitiveType.Cube, parent, "Tick" + i + "X",
                    new Vector3(sx * (half - tick * 0.5f), 0f, sz * half),
                    new Vector3(tick, thickness, thickness), material);

                Annotations.AnnotationVisuals.Primitive(
                    PrimitiveType.Cube, parent, "Tick" + i + "Z",
                    new Vector3(sx * half, 0f, sz * (half - tick * 0.5f)),
                    new Vector3(thickness, thickness, tick), material);
            }
        }
    }
}
