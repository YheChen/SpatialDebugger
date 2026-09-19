using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using SpatialDebugger.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace SpatialDebugger.AI
{
    /// <summary>Backend reachability, as last observed.</summary>
    public enum BackendStatus
    {
        Unknown = 0,
        Checking,
        Online,
        Offline
    }

    /// <summary>
    /// Talks to the FastAPI service over the LAN.
    /// </summary>
    /// <remarks>
    /// Every failure mode -- no route to host, DNS failure, timeout, 4xx/5xx,
    /// a 200 carrying junk -- resolves to an <see cref="AIResponse"/> with
    /// <see cref="AIResponse.Success"/> false and a specific
    /// <see cref="AIFailure"/>. Nothing throws into the caller, because the
    /// caller is a button in a headset.
    /// </remarks>
    public class AIClient : MonoBehaviour
    {
        [Tooltip("Leave empty to use Resources/SpatialDebuggerConfig. Set this to " +
                 "override the backend host for this scene only.")]
        [SerializeField] private string hostOverride = string.Empty;

        [SerializeField] private int portOverride;

        public BackendStatus Status { get; private set; } = BackendStatus.Unknown;

        /// <summary>Raised whenever <see cref="Status"/> changes, so UI can react.</summary>
        public event Action<BackendStatus> StatusChanged;

        private SpatialDebuggerConfig Config => SpatialDebuggerConfig.Instance;

        /// <summary>The base URL actually in use, for display in the UI.</summary>
        public string BaseUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(hostOverride)) return Config.BaseUrl;

                var host = hostOverride.Trim().TrimEnd('/');
                if (host.StartsWith("http://") || host.StartsWith("https://")) return host;

                var port = portOverride > 0 ? portOverride : Config.BackendPort;
                return "http://" + host + ":" + port;
            }
        }

        private string UrlFor(string path) =>
            BaseUrl + (path.StartsWith("/") ? path : "/" + path);

        private void Start()
        {
            if (Config.ProbeHealthOnStart) CheckHealth();
        }

        // -- public API ----------------------------------------------------

        /// <summary>Set the backend address at runtime (from the in-headset UI).</summary>
        public void SetHost(string host, int port = 0)
        {
            hostOverride = string.Empty; // an explicit runtime set beats the scene override
            Config.BackendHost = host;
            if (port > 0) Config.BackendPort = port;
            SetStatus(BackendStatus.Unknown);
        }

        public void CheckHealth(Action<bool> onComplete = null)
        {
            StartCoroutine(HealthRoutine(onComplete));
        }

        public void Ask(AIAskRequest request, Action<AIResponse> onComplete)
        {
            StartCoroutine(PostRoutine("/ask", request.ToJson(), onComplete));
        }

        public void Analyze(AIAnalyzeRequest request, Action<AIResponse> onComplete)
        {
            StartCoroutine(PostRoutine("/analyze", request.ToJson(), onComplete));
        }

        // -- coroutines ----------------------------------------------------

        private IEnumerator HealthRoutine(Action<bool> onComplete)
        {
            SetStatus(BackendStatus.Checking);

            using (var request = UnityWebRequest.Get(UrlFor("/health")))
            {
                request.timeout = Config.TimeoutSeconds;
                yield return request.SendWebRequest();

                var healthy = request.result == UnityWebRequest.Result.Success;
                if (healthy)
                {
                    // A 200 from something that is not our backend is not healthy.
                    healthy = LooksLikeOurBackend(request.downloadHandler.text);
                }

                SetStatus(healthy ? BackendStatus.Online : BackendStatus.Offline);
                onComplete?.Invoke(healthy);
            }
        }

        private static bool LooksLikeOurBackend(string body)
        {
            try
            {
                return JToken.Parse(body) is JObject root &&
                       (string)root["service"] == "spatialdebugger-backend";
            }
            catch (Exception)
            {
                return false;
            }
        }

        private IEnumerator PostRoutine(string path, string body, Action<AIResponse> onComplete)
        {
            var url = UrlFor(path);

            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(RequestEncoding.Utf8(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = Config.TimeoutSeconds;

                yield return request.SendWebRequest();

                AIResponse response;

                switch (request.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                        // UnityWebRequest reports a timeout as a connection error
                        // with a specific message; separate the two so the UI can
                        // say something useful.
                        var timedOut = !string.IsNullOrEmpty(request.error) &&
                                       request.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
                        response = AIResponse.Failed(
                            timedOut ? AIFailure.Timeout : AIFailure.ConnectionFailed,
                            "Could not reach " + url + " (" + request.error + ")");
                        SetStatus(BackendStatus.Offline);
                        break;

                    case UnityWebRequest.Result.DataProcessingError:
                        response = AIResponse.Failed(AIFailure.MalformedResponse,
                            "Backend response could not be read: " + request.error);
                        break;

                    case UnityWebRequest.Result.ProtocolError:
                        response = AIResponse.Failed(AIFailure.HttpError,
                            "Backend returned HTTP " + request.responseCode + ": " +
                            Truncate(request.downloadHandler?.text, 200));
                        SetStatus(BackendStatus.Online); // it answered, just not happily
                        break;

                    case UnityWebRequest.Result.Success:
                        if (AIResponse.TryParse(request.downloadHandler.text, out var parsed, out var error))
                        {
                            response = parsed;
                            SetStatus(BackendStatus.Online);
                        }
                        else
                        {
                            response = AIResponse.Failed(AIFailure.MalformedResponse, error);
                        }
                        break;

                    default:
                        response = AIResponse.Failed(AIFailure.ConnectionFailed,
                            "Unexpected request result: " + request.result);
                        break;
                }

                if (!response.Success)
                {
                    Debug.LogWarning("[SpatialDebugger] " + path + " failed: " +
                                     response.Failure + " - " + response.Error);
                }

                onComplete?.Invoke(response);
            }
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= max ? value : value.Substring(0, max) + "...";
        }

        private void SetStatus(BackendStatus status)
        {
            if (Status == status) return;
            Status = status;
            StatusChanged?.Invoke(status);
        }
    }
}
