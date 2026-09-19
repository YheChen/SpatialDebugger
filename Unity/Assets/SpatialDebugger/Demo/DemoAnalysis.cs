using System;
using System.Collections;
using SpatialDebugger.AI;
using SpatialDebugger.Annotations;
using SpatialDebugger.Core;
using UnityEngine;

namespace SpatialDebugger.Demo
{
    /// <summary>
    /// The guaranteed demo path.
    /// </summary>
    /// <remarks>
    /// <code>
    /// user selects a physical location
    ///     -> target appears
    ///     -> press "Demo Analysis"
    ///     -> mock analysis
    ///     -> warning appears: "Possible incorrect connection"
    ///     -> arrow points to the selected position
    /// </code>
    /// <see cref="RunLocal"/> never touches the network. <see cref="Run"/> tries
    /// the backend first and falls back to the local scenarios on any failure,
    /// so the same button works whether or not uvicorn is running.
    /// </remarks>
    public class DemoAnalysis : MonoBehaviour
    {
        [Tooltip("Optional. When set, Run() tries the backend before falling back.")]
        [SerializeField] private AIClient client;

        [SerializeField] private SpatialActionDispatcher dispatcher;

        [Tooltip("Clear existing annotations before rendering a new analysis.")]
        [SerializeField] private bool clearBeforeRun = true;

        [Tooltip("Seconds of 'thinking' before local results appear, so the demo reads " +
                 "as analysis rather than as a canned animation. 0 disables it.")]
        [SerializeField] private float simulatedThinkingSeconds = 0.6f;

        [Tooltip("Scenario the Demo Analysis button runs. Empty means the default.")]
        [SerializeField] private string scenarioId = DemoScenarios.DefaultId;

        private int _cycleIndex;
        private bool _busy;

        /// <summary>True while an analysis is in flight; UI disables its buttons on this.</summary>
        public bool IsBusy => _busy;

        /// <summary>Raised when an analysis starts.</summary>
        public event Action AnalysisStarted;

        /// <summary>Raised with the response once it has been rendered.</summary>
        public event Action<AIResponse> AnalysisCompleted;

        public SpatialActionDispatcher Dispatcher
        {
            get => dispatcher;
            set => dispatcher = value;
        }

        public AIClient Client
        {
            get => client;
            set => client = value;
        }

        private void Awake()
        {
            if (dispatcher == null) dispatcher = FindAnyObjectByType<SpatialActionDispatcher>();
            if (client == null) client = FindAnyObjectByType<AIClient>();
        }

        // -- entry points --------------------------------------------------

        /// <summary>
        /// The "Demo Analysis" button. Purely local: no network, no backend,
        /// no CV, no sponsor APIs.
        /// </summary>
        public void RunLocal()
        {
            RunScenario(DemoScenarios.Get(scenarioId) ?? DemoScenarios.Default);
        }

        /// <summary>Steps to the next scenario. Handy for showing range in a demo.</summary>
        public void RunNextScenario()
        {
            _cycleIndex++;
            RunScenario(DemoScenarios.At(_cycleIndex));
        }

        public void RunScenario(string id)
        {
            RunScenario(DemoScenarios.Get(id) ?? DemoScenarios.Default);
        }

        public void RunScenario(DemoScenarios.Scenario scenario)
        {
            if (_busy) return;
            StartCoroutine(LocalRoutine(scenario));
        }

        /// <summary>
        /// The "Ask AI" button: backend first, local scenarios if it is
        /// unreachable, slow or broken.
        /// </summary>
        public void Run(string question = null)
        {
            if (_busy) return;
            StartCoroutine(BackendRoutine(question));
        }

        /// <summary>The "Clear" button.</summary>
        public void Clear()
        {
            if (dispatcher != null) dispatcher.ClearAll();
        }

        // -- routines ------------------------------------------------------

        private IEnumerator LocalRoutine(DemoScenarios.Scenario scenario)
        {
            _busy = true;
            AnalysisStarted?.Invoke();

            if (simulatedThinkingSeconds > 0f)
            {
                yield return new WaitForSeconds(simulatedThinkingSeconds);
            }

            var response = DemoScenarios.Respond(scenario, TargetLabel());
            Render(response);

            _busy = false;
            AnalysisCompleted?.Invoke(response);
        }

        private IEnumerator BackendRoutine(string question)
        {
            _busy = true;
            AnalysisStarted?.Invoke();

            AIResponse result = null;

            if (client != null)
            {
                var request = new AIAnalyzeRequest
                {
                    Question = question,
                    Context = "ESP32 breadboard circuit",
                    TargetLabel = TargetLabel()
                };

                var done = false;
                client.Analyze(request, response =>
                {
                    result = response;
                    done = true;
                });

                // The client's own timeout bounds this; no extra guard needed.
                while (!done) yield return null;
            }

            if (result == null || !result.Success || result.Actions.Count == 0)
            {
                var reason = result == null
                    ? "no AIClient in the scene"
                    : (result.Success ? "backend returned no actions" : result.Failure.ToString());
                Debug.Log("[SpatialDebugger] falling back to the on-device demo (" + reason + ")");

                result = DemoScenarios.Respond(
                    DemoScenarios.Get(scenarioId) ?? DemoScenarios.Default, TargetLabel());
                result.Speech += "  (offline)";
            }

            Render(result);

            _busy = false;
            AnalysisCompleted?.Invoke(result);
        }

        private void Render(AIResponse response)
        {
            if (dispatcher == null)
            {
                Debug.LogError("[SpatialDebugger] DemoAnalysis has no SpatialActionDispatcher; " +
                               "nothing will be drawn.");
                return;
            }

            if (dispatcher.Target == null)
            {
                Debug.LogWarning("[SpatialDebugger] no target selected; select a point first.");
                return;
            }

            if (clearBeforeRun) dispatcher.ClearAll();

            var rendered = dispatcher.Dispatch(response);
            Debug.Log("[SpatialDebugger] " + response.Provider + " analysis '" +
                      (response.Scenario ?? "-") + "': rendered " + rendered + "/" +
                      response.Actions.Count + " actions. " + response.Speech);
        }

        private string TargetLabel()
        {
            var target = dispatcher != null ? dispatcher.Target : null;
            return target != null ? target.Label : null;
        }
    }
}
