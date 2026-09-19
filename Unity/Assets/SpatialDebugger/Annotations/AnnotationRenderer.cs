using SpatialDebugger.Core;
using UnityEngine;

namespace SpatialDebugger.Annotations
{
    /// <summary>
    /// Base for every annotation visual.
    /// </summary>
    /// <remarks>
    /// One renderer owns one <see cref="SpatialAction"/> and the GameObject
    /// subtree that draws it. The dispatcher parents these under the selected
    /// target, so <c>space: "target"</c> coordinates become local coordinates
    /// and nothing has to know real Quest world positions.
    /// </remarks>
    public abstract class AnnotationRenderer : MonoBehaviour
    {
        public SpatialAction Action { get; private set; }

        private float _despawnAt = -1f;
        private float _spawnedAt;

        /// <summary>Per-type default when the action carries no colour.</summary>
        protected abstract Color DefaultColor { get; }

        protected Color ResolvedColor =>
            Action != null && Action.HasColor ? Action.Color : DefaultColor;

        /// <summary>Metres-per-unit multiplier from the action and global config.</summary>
        protected float ResolvedScale
        {
            get
            {
                var actionScale = Action != null ? Action.Scale : 1f;
                return Mathf.Max(0.01f, actionScale * SpatialDebuggerConfig.Instance.AnnotationScale);
            }
        }

        /// <summary>Seconds since this annotation appeared, for entry animations.</summary>
        protected float Age => Time.time - _spawnedAt;

        internal void Initialise(SpatialAction action)
        {
            Action = action;
            _spawnedAt = Time.time;

            if (action != null && action.Duration > 0f)
            {
                _despawnAt = Time.time + action.Duration;
            }

            Build();
        }

        /// <summary>Construct the visual. Called once, right after Initialise.</summary>
        protected abstract void Build();

        protected virtual void Update()
        {
            if (_despawnAt > 0f && Time.time >= _despawnAt)
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Keeps a transform facing the viewer.
    /// </summary>
    /// <remarks>
    /// Resolves the camera lazily and re-resolves if it goes away: on Quest the
    /// main camera is the rig's centre-eye anchor, which may not exist yet when
    /// an annotation spawns during scene start-up.
    /// </remarks>
    public class Billboard : MonoBehaviour
    {
        [Tooltip("Keep the label upright instead of tilting with the head.")]
        public bool LockVertical = true;

        private Transform _viewer;

        private Transform Viewer
        {
            get
            {
                if (_viewer != null) return _viewer;

                var camera = Camera.main;
                if (camera == null)
                {
                    camera = FindAnyObjectByType<Camera>();
                }

                _viewer = camera != null ? camera.transform : null;
                return _viewer;
            }
        }

        private void LateUpdate()
        {
            var viewer = Viewer;
            if (viewer == null) return;

            var forward = transform.position - viewer.position;
            if (LockVertical) forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) return;

            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}
