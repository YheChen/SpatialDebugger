using SpatialDebugger.AI;
using SpatialDebugger.Annotations;
using SpatialDebugger.Core;
using SpatialDebugger.Demo;
using SpatialDebugger.Interaction;
using UnityEngine;

namespace SpatialDebugger.UI
{
    /// <summary>
    /// The in-headset control panel.
    /// </summary>
    /// <remarks>
    /// <code>
    /// +----------------------------+
    /// | SpatialDebugger            |
    /// | Target selected            |
    /// | Backend: online            |
    /// |                            |
    /// | [ Ask AI ]                 |
    /// | [ Demo Analysis ]          |
    /// | [ Next Scenario ]          |
    /// | [ Clear ]                  |
    /// | [ Target In Front ]        |
    /// +----------------------------+
    /// </code>
    /// Built procedurally: no prefab, no Canvas, no EventSystem. It follows
    /// the user at a comfortable distance so it is reachable without walking
    /// back to wherever it was spawned.
    /// </remarks>
    public class SpatialDebuggerPanel : MonoBehaviour
    {
        [SerializeField] private DemoAnalysis analysis;
        [SerializeField] private SpatialTargetController targetController;
        [SerializeField] private SpatialActionDispatcher dispatcher;
        [SerializeField] private AIClient client;

        [Header("Placement")]
        [Tooltip("Metres in front of the viewer.")]
        [SerializeField] private float followDistance = 0.75f;

        [Tooltip("Metres below eye level, so it does not cover what you are debugging.")]
        [SerializeField] private float followDrop = 0.25f;

        [Tooltip("How quickly the panel catches up. 0 pins it in place once spawned.")]
        [SerializeField] private float followSharpness = 2.5f;

        [SerializeField] private bool followViewer = true;

        private static readonly Vector2 ButtonSize = new Vector2(0.20f, 0.042f);
        private const float PanelWidth = 0.24f;

        private SpatialLabel _statusText;
        private SpatialLabel _speechText;
        private SpatialButton _askButton;
        private SpatialButton _demoButton;
        private SpatialButton _nextButton;
        private SpatialButton _clearButton;
        private SpatialButton _frontButton;
        private Transform _body;

        private void Awake()
        {
            if (analysis == null) analysis = FindAnyObjectByType<DemoAnalysis>();
            if (targetController == null) targetController = FindAnyObjectByType<SpatialTargetController>();
            if (dispatcher == null) dispatcher = FindAnyObjectByType<SpatialActionDispatcher>();
            if (client == null) client = FindAnyObjectByType<AIClient>();

            Build();
            Subscribe();
            RefreshStatus();
        }

        private void Subscribe()
        {
            if (targetController != null)
            {
                targetController.TargetSelected += _ => RefreshStatus();
            }

            if (analysis != null)
            {
                analysis.AnalysisStarted += OnAnalysisStarted;
                analysis.AnalysisCompleted += OnAnalysisCompleted;
            }

            if (client != null)
            {
                client.StatusChanged += _ => RefreshStatus();
            }
        }

        private void OnDestroy()
        {
            if (analysis != null)
            {
                analysis.AnalysisStarted -= OnAnalysisStarted;
                analysis.AnalysisCompleted -= OnAnalysisCompleted;
            }
        }

        // -- construction --------------------------------------------------

        private void Build()
        {
            _body = new GameObject("Panel").transform;
            _body.SetParent(transform, false);

            const float height = 0.34f;

            AnnotationVisuals.Primitive(
                PrimitiveType.Quad, _body, "Backing", new Vector3(0f, 0f, 0.01f),
                new Vector3(PanelWidth, height, 1f),
                AnnotationVisuals.Transparent(new Color(0.03f, 0.04f, 0.06f, 0.88f)));

            AnnotationVisuals.Primitive(
                PrimitiveType.Quad, _body, "TitleBar",
                new Vector3(0f, height * 0.5f - 0.014f, 0.008f),
                new Vector3(PanelWidth, 0.028f, 1f),
                AnnotationVisuals.Opaque(new Color(0.18f, 0.77f, 0.95f)));

            var title = SpatialText.Create(_body, "SpatialDebugger", 0.014f,
                new Color(0.02f, 0.05f, 0.08f));
            title.transform.localPosition = new Vector3(0f, height * 0.5f - 0.014f, 0.006f);

            _statusText = SpatialText.Create(_body, "", 0.009f, new Color(0.75f, 0.82f, 0.9f),
                SpatialTextAlign.Left);
            _statusText.transform.localPosition =
                new Vector3(-PanelWidth * 0.5f + 0.012f, 0.128f, 0.006f);

            var top = 0.064f;
            const float step = 0.048f;

            _askButton = SpatialButton.Create(_body, "Ask AI", ButtonSize,
                new Color(0.18f, 0.77f, 0.95f), new Vector3(0f, top, 0.004f));
            _demoButton = SpatialButton.Create(_body, "Demo Analysis", ButtonSize,
                new Color(0.18f, 0.95f, 0.6f), new Vector3(0f, top - step, 0.004f));
            _nextButton = SpatialButton.Create(_body, "Next Scenario", ButtonSize,
                new Color(1f, 0.62f, 0.11f), new Vector3(0f, top - step * 2f, 0.004f));
            _clearButton = SpatialButton.Create(_body, "Clear", ButtonSize,
                new Color(0.7f, 0.72f, 0.78f), new Vector3(0f, top - step * 3f, 0.004f));
            _frontButton = SpatialButton.Create(_body, "Target In Front", ButtonSize,
                new Color(0.75f, 0.55f, 1f), new Vector3(0f, top - step * 4f, 0.004f));

            _speechText = SpatialText.Create(_body, "", 0.008f, new Color(0.62f, 0.7f, 0.8f),
                SpatialTextAlign.Left);
            _speechText.transform.localPosition =
                new Vector3(-PanelWidth * 0.5f + 0.012f, top - step * 4f - 0.03f, 0.006f);

            _askButton.Pressed += () => { if (analysis != null) analysis.Run(); };
            _demoButton.Pressed += () => { if (analysis != null) analysis.RunLocal(); };
            _nextButton.Pressed += () => { if (analysis != null) analysis.RunNextScenario(); };
            _clearButton.Pressed += () =>
            {
                if (analysis != null) analysis.Clear();
                SetSpeech(string.Empty);
                RefreshStatus();
            };
            _frontButton.Pressed += () =>
            {
                if (targetController != null) targetController.PlaceTargetInFrontOfViewer();
            };
        }

        // -- state ---------------------------------------------------------

        private void OnAnalysisStarted()
        {
            SetButtonsInteractable(false);
            SetSpeech("Analysing...");
        }

        private void OnAnalysisCompleted(AIResponse response)
        {
            SetButtonsInteractable(true);
            SetSpeech(response != null ? response.Speech : string.Empty);
            RefreshStatus();
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (_askButton != null) _askButton.Interactable = interactable;
            if (_demoButton != null) _demoButton.Interactable = interactable;
            if (_nextButton != null) _nextButton.Interactable = interactable;
        }

        private void SetSpeech(string speech)
        {
            if (_speechText == null) return;
            _speechText.Text = string.IsNullOrEmpty(speech) ? string.Empty : "\"" + Wrap(speech, 34) + "\"";
        }

        /// <summary>
        /// Reports what is actually true, not what we hope is true. If there is
        /// no target, or the backend is unreachable, the panel says so.
        /// </summary>
        public void RefreshStatus()
        {
            if (_statusText == null) return;

            var hasTarget = targetController != null && targetController.HasTarget;
            var targetLine = hasTarget
                ? "Target: " + targetController.Target.Label
                : "No target - point and pinch";

            var backendLine = "Backend: " + (client == null
                ? "not configured"
                : client.Status.ToString().ToLowerInvariant());

            var pointerLine = "Pointer: " + (targetController != null
                ? targetController.ActiveSourceName
                : "none");

            var annotationLine = dispatcher != null
                ? "Annotations: " + dispatcher.LiveCount
                : string.Empty;

            _statusText.Text = string.Join("\n",
                targetLine, backendLine, pointerLine, annotationLine);

            if (_askButton != null) _askButton.Interactable = hasTarget;
            if (_demoButton != null) _demoButton.Interactable = hasTarget;
            if (_nextButton != null) _nextButton.Interactable = hasTarget;
        }

        /// <summary>
        /// Hard-wraps at a character count. The legacy text backend has no word
        /// wrapping of its own, and the speech line is the one string long
        /// enough to run off the panel.
        /// </summary>
        private static string Wrap(string text, int columns)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var builder = new System.Text.StringBuilder(text.Length + 16);
            var lineLength = 0;

            foreach (var word in text.Split(' '))
            {
                if (lineLength > 0 && lineLength + word.Length + 1 > columns)
                {
                    builder.Append('\n');
                    lineLength = 0;
                }
                else if (lineLength > 0)
                {
                    builder.Append(' ');
                    lineLength++;
                }

                builder.Append(word);
                lineLength += word.Length;
            }

            return builder.ToString();
        }

        // -- placement -----------------------------------------------------

        private void LateUpdate()
        {
            if (!followViewer) return;

            var viewer = Camera.main;
            if (viewer == null) return;

            var forward = viewer.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-5f) forward = Vector3.forward;
            forward.Normalize();

            var wanted = viewer.transform.position + forward * followDistance + Vector3.down * followDrop;

            if (followSharpness <= 0f)
            {
                transform.position = wanted;
            }
            else
            {
                // Frame-rate independent smoothing.
                var t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, wanted, t);
            }

            transform.rotation = Quaternion.LookRotation(
                (transform.position - viewer.transform.position).normalized, Vector3.up);
        }

        private float _statusTimer;

        private void Update()
        {
            // Backend status and annotation counts change without an event.
            _statusTimer += Time.deltaTime;
            if (_statusTimer < 0.5f) return;
            _statusTimer = 0f;
            RefreshStatus();
        }
    }
}
