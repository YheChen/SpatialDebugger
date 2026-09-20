using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Meta.XR.BuildingBlocks.AIBlocks;
using SpatialDebugger.Demo;
using UnityEngine;

namespace SpatialDebugger.Vision
{
    /// <summary>What a recognition attempt produced.</summary>
    public class RecognitionResult
    {
        /// <summary>The recognised noun, lowercase, or null on failure.</summary>
        public string Word;

        /// <summary>The model's verbatim reply, for diagnosis.</summary>
        public string Raw;

        public bool Success => !string.IsNullOrEmpty(Word);

        /// <summary>Why it failed, when it did.</summary>
        public string Error;
    }

    /// <summary>
    /// Recognises the object in a camera frame using a local Ollama model.
    /// </summary>
    /// <remarks>
    /// Uses Meta's <see cref="OllamaProvider"/> through <see cref="IChatTask"/>
    /// rather than <c>ObjectDetectionAgent</c>. That is not a shortcut:
    /// <c>OllamaProvider</c> does not implement <c>IObjectDetectionTask</c>, so
    /// the agent cannot drive it. It is also the better fit, because the pinch
    /// ray already establishes WHERE the object is — only the noun is missing,
    /// and bounding boxes would be discarded.
    /// <para>
    /// The transport is <c>http://127.0.0.1:11434</c> on the headset, reaching
    /// the Mac through <c>adb reverse tcp:11434 tcp:11434</c>. No LAN, no
    /// venue Wi-Fi.
    /// </para>
    /// </remarks>
    public class VisionRecognizer : MonoBehaviour
    {
        [Header("Ollama")]
        [Tooltip("On the headset this is the Quest itself; adb reverse forwards it to the Mac.")]
        [SerializeField] private string host = "http://127.0.0.1:11434";

        [Tooltip("Model name as `ollama list` shows it.")]
        [SerializeField] private string model = "moondream";

        [Tooltip("Seconds before a recognition attempt is abandoned.")]
        [SerializeField] private float timeoutSeconds = 25f;

        [Header("Capture")]
        [Tooltip("Crop size as a fraction of the frame, around the pinched point.")]
        [SerializeField, Range(0.2f, 1f)] private float cropFraction = 0.6f;

        [SerializeField, Range(40, 95)] private int jpegQuality = 80;

        [SerializeField] private CameraFeed cameraFeed;

        private const string Prompt =
            "You are identifying the single physical object the user is pointing at in this image. " +
            "Return ONLY the common English noun for the most likely object. " +
            "Examples: chair, laptop, bottle, backpack, keyboard, monitor, phone, table, cup. " +
            "Return only the noun, no sentence or punctuation.";

        private OllamaProvider _provider;

        private const string Tag = "[SpatialDebugger][vision] ";
        private static void Log(string message) => Debug.Log(Tag + message);
        private static void Warn(string message) => Debug.LogWarning(Tag + message);

        /// <summary>True when the camera can supply a frame right now.</summary>
        public bool CameraReady => cameraFeed != null && cameraFeed.IsReady;

        private void Awake()
        {
            if (cameraFeed == null) cameraFeed = FindAnyObjectByType<CameraFeed>();

            // Created in code rather than as an asset: there is nothing to
            // author, and it keeps the provider's configuration next to the
            // code that depends on it.
            _provider = ScriptableObject.CreateInstance<OllamaProvider>();
            _provider.host = host;
            _provider.model = model;

            Log("provider ready host=" + host + " model=" + model +
                " vision=" + _provider.SupportsVision);
        }

        private void OnDestroy()
        {
            if (_provider != null) Destroy(_provider);
        }

        /// <summary>
        /// Recognises the object at <paramref name="worldPoint"/> and calls
        /// back on the main thread. Never throws.
        /// </summary>
        public void Recognise(Vector3 worldPoint, Action<RecognitionResult> onComplete)
        {
            StartCoroutine(RecogniseRoutine(worldPoint, onComplete));
        }

        private IEnumerator RecogniseRoutine(Vector3 worldPoint, Action<RecognitionResult> onComplete)
        {
            var result = new RecognitionResult();

            // -- capture -----------------------------------------------------
            if (cameraFeed == null || !cameraFeed.IsReady)
            {
                result.Error = "camera not ready";
                Finish(result, onComplete);
                yield break;
            }

            var texture = cameraFeed.GetTexture();
            if (texture == null)
            {
                result.Error = "GetTexture returned null";
                Finish(result, onComplete);
                yield break;
            }

            // Project the pinched point into the image so the crop follows what
            // the user actually pointed at. Falls back to the centre.
            var centre = new Vector2(0.5f, 0.5f);
            if (cameraFeed.TryGetViewportPoint(worldPoint, out var projected))
            {
                centre = projected;
            }

            var jpg = CameraCapture.EncodeCrop(texture, centre, cropFraction, jpegQuality,
                out var width, out var height, out var captureError);

            if (jpg == null || jpg.Length == 0)
            {
                result.Error = "jpeg encode failed: " + (captureError ?? "empty");
                Finish(result, onComplete);
                yield break;
            }

            Log("captured " + width + "x" + height + ", jpeg=" + jpg.Length + " bytes, " +
                "crop centre=(" + centre.x.ToString("F2") + "," + centre.y.ToString("F2") + ")");

            // -- request -----------------------------------------------------
            Log("request started");

            var request = new ChatRequest(Prompt, new List<ImageInput>
            {
                new ImageInput { bytes = jpg, mimeType = "image/jpeg" }
            });

            var cancellation = new CancellationTokenSource(
                TimeSpan.FromSeconds(Mathf.Max(1f, timeoutSeconds)));

            Task<ChatResponse> task = null;
            try
            {
                task = _provider.ChatAsync(request, null, cancellation.Token);
            }
            catch (Exception exception)
            {
                result.Error = "ChatAsync threw: " + exception.Message;
                cancellation.Dispose();
                Finish(result, onComplete);
                yield break;
            }

            var started = Time.realtimeSinceStartup;

            // Polling the Task keeps the main thread free: the user can still
            // move their head and hands while this runs.
            while (!task.IsCompleted) yield return null;

            var elapsed = Time.realtimeSinceStartup - started;
            cancellation.Dispose();

            // -- interpret ---------------------------------------------------
            if (task.IsFaulted)
            {
                var inner = task.Exception?.GetBaseException();
                result.Error = "request failed: " + (inner?.Message ?? "unknown");
            }
            else if (task.IsCanceled)
            {
                result.Error = "timed out after " + timeoutSeconds + "s";
            }
            else
            {
                var raw = task.Result?.text;
                result.Raw = raw;
                Log("response raw=\"" + Truncate(raw) + "\" in " + elapsed.ToString("F1") + "s");

                var word = RecognitionText.Normalize(raw, Vocabulary.KnownWords);
                if (string.IsNullOrEmpty(word))
                {
                    result.Error = "empty or unusable response";
                }
                else
                {
                    result.Word = word;
                    Log("normalized=\"" + word + "\"");
                }
            }

            Finish(result, onComplete);
        }

        private static void Finish(RecognitionResult result, Action<RecognitionResult> onComplete)
        {
            if (!result.Success)
            {
                Warn("FAILED: " + result.Error + "; using deterministic fallback");
            }

            try
            {
                onComplete?.Invoke(result);
            }
            catch (Exception exception)
            {
                Debug.LogError(Tag + "callback threw: " + exception);
            }
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var single = value.Replace("\n", "\\n").Replace("\r", string.Empty);
            return single.Length <= 120 ? single : single.Substring(0, 120) + "...";
        }
    }
}
