using System;
using System.Collections.Generic;
using SpatialDebugger.Core;
using UnityEngine;

namespace SpatialDebugger.Annotations
{
    /// <summary>
    /// Turns a list of <see cref="SpatialAction"/> into live annotations.
    /// </summary>
    /// <remarks>
    /// <code>
    /// AI response
    ///     -> SpatialActionDispatcher
    ///         -> Label / Warning / Marker / Arrow / Highlight renderers
    /// </code>
    /// The dispatcher is the only place that knows how an action type maps to
    /// a renderer, and the only place that resolves
    /// <see cref="SpatialSpace"/>. It never throws on bad input: an action it
    /// cannot render is logged and skipped, because a partial annotation set
    /// still demos and an exception mid-presentation does not.
    /// </remarks>
    public class SpatialActionDispatcher : MonoBehaviour
    {
        [Tooltip("The selected debugging target. Target-space actions are parented here. " +
                 "Usually assigned at runtime by SpatialTargetController.")]
        [SerializeField] private SpatialTarget target;

        [Tooltip("Parent for world-space actions. Defaults to this object.")]
        [SerializeField] private Transform worldRoot;

        private readonly List<AnnotationRenderer> _live = new List<AnnotationRenderer>();

        /// <summary>Raised after any dispatch or clear, with the live count.</summary>
        public event Action<int> AnnotationsChanged;

        /// <summary>Raised for each action that could not be rendered.</summary>
        public event Action<SpatialAction, string> ActionRejected;

        public int LiveCount
        {
            get
            {
                Prune();
                return _live.Count;
            }
        }

        public SpatialTarget Target
        {
            get => target;
            set => target = value;
        }

        private Transform WorldRoot => worldRoot != null ? worldRoot : transform;

        // -- dispatch ------------------------------------------------------

        /// <summary>Renders a whole backend response. Returns how many landed.</summary>
        public int Dispatch(AI.AIResponse response)
        {
            if (response == null || response.Actions == null) return 0;
            return Dispatch(response.Actions);
        }

        public int Dispatch(IEnumerable<SpatialAction> actions)
        {
            if (actions == null) return 0;

            var rendered = 0;
            foreach (var action in actions)
            {
                if (Dispatch(action)) rendered++;
            }

            AnnotationsChanged?.Invoke(LiveCount);
            return rendered;
        }

        /// <summary>Renders one action. Returns false if it was rejected.</summary>
        public bool Dispatch(SpatialAction action)
        {
            return DispatchTracked(action) != null || action?.Type == SpatialActionType.Clear;
        }

        /// <summary>
        /// Renders one action and hands back the renderer, so the caller can
        /// update that specific annotation later.
        /// </summary>
        /// <remarks>
        /// Additive. The dispatcher keeps a flat list with no id lookup, and
        /// redesigning that to support update-by-id would risk the verified
        /// demo for no benefit — holding the handle is both smaller and
        /// sufficient. Returns null for a rejected action, and for
        /// <see cref="SpatialActionType.Clear"/>, which produces no renderer.
        /// </remarks>
        public AnnotationRenderer DispatchTracked(SpatialAction action)
        {
            if (action == null)
            {
                ActionRejected?.Invoke(null, "action was null");
                return null;
            }

            if (action.Type == SpatialActionType.Clear)
            {
                ClearAll();
                return null;
            }

            if (!action.IsValid(out var error))
            {
                Debug.LogWarning("[SpatialDebugger] rejected action: " + error);
                ActionRejected?.Invoke(action, error);
                return null;
            }

            var parent = ResolveParent(action.Space, out var parentError);
            if (parent == null)
            {
                Debug.LogWarning("[SpatialDebugger] rejected action: " + parentError);
                ActionRejected?.Invoke(action, parentError);
                return null;
            }

            Prune();
            EnforceBudget();

            var host = new GameObject("SD_" + action.Type);
            host.transform.SetParent(parent, false);

            // An arrow is positioned at its tail and draws to 'to' in local
            // space; everything else sits at its own position.
            host.transform.localPosition = action.Type == SpatialActionType.Arrow
                ? action.From
                : action.Position;
            host.transform.localRotation = Quaternion.identity;

            var renderer = AddRenderer(host, action.Type);
            if (renderer == null)
            {
                AnnotationVisuals.SafeDestroy(host);
                var message = "no renderer for " + action.Type;
                ActionRejected?.Invoke(action, message);
                return null;
            }

            renderer.Initialise(action);
            _live.Add(renderer);
            return renderer;
        }

        private static AnnotationRenderer AddRenderer(GameObject host, SpatialActionType type)
        {
            switch (type)
            {
                case SpatialActionType.Label: return host.AddComponent<LabelRenderer>();
                case SpatialActionType.Warning: return host.AddComponent<WarningRenderer>();
                case SpatialActionType.Marker: return host.AddComponent<MarkerRenderer>();
                case SpatialActionType.Arrow: return host.AddComponent<ArrowRenderer>();
                case SpatialActionType.Highlight: return host.AddComponent<HighlightRenderer>();
                default: return null;
            }
        }

        private Transform ResolveParent(SpatialSpace space, out string error)
        {
            error = null;

            if (space == SpatialSpace.World) return WorldRoot;

            if (target == null)
            {
                error = "action is in target space but no target is selected";
                return null;
            }

            return target.AnnotationRoot;
        }

        // -- lifecycle -----------------------------------------------------

        /// <summary>Removes every annotation. This is the 'clear' action.</summary>
        public void ClearAll()
        {
            foreach (var renderer in _live)
            {
                if (renderer != null) AnnotationVisuals.SafeDestroy(renderer.gameObject);
            }

            _live.Clear();
            AnnotationsChanged?.Invoke(0);
        }

        private void Prune()
        {
            // Renderers with a duration destroy themselves, so the list needs
            // sweeping for Unity's "destroyed but not null" objects.
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i] == null) _live.RemoveAt(i);
            }
        }

        /// <summary>Drops the oldest annotations once the configured cap is hit.</summary>
        private void EnforceBudget()
        {
            var budget = SpatialDebuggerConfig.Instance.MaxConcurrentAnnotations;
            while (_live.Count >= budget && _live.Count > 0)
            {
                var oldest = _live[0];
                _live.RemoveAt(0);
                if (oldest != null) AnnotationVisuals.SafeDestroy(oldest.gameObject);
            }
        }

        private void OnDestroy()
        {
            _live.Clear();
        }
    }
}
