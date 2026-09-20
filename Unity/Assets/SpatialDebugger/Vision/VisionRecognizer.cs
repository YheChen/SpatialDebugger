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

        [Header("Reliability")]
        [Tooltip("Ollama serves one small model badly under concurrent load, so " +
                 "requests are queued and run one at a time.")]
        [SerializeField] private int maxQueued = 4;

        [Tooltip("Retries for an empty answer. Measured: a cold model does NOT return " +
                 "an empty body -- Ollama blocks and then answers -- so this is only a " +
                 "cheap guard against a one-off, not the fix for empty replies.")]
        [SerializeField] private int loadRetries = 1;

        [Tooltip("Send a tiny prompt at startup so the first real pinch is not the " +
                 "one that pays the model-load cost.")]
        [SerializeField] private bool warmUpOnStart = true;

        [Header("Debug")]
        [Tooltip("Write the exact JPEG sent to Ollama to persistentDataPath, for adb pull.")]
        [SerializeField] private bool saveDebugImage = true;

        /// <summary>
        /// A question, not an instruction — and deliberately NOT "answer with
        /// one word".
        /// </summary>
        /// <remarks>
        /// Measured against this exact Ollama and model, not guessed.
        /// moondream is a 2024 VQA model (phi2, ~1.6B, temperature 0) whose
        /// template is literally <c>Question: {prompt}\n\nAnswer:</c>, with no
        /// instruction tuning. Consequences, all reproduced:
        /// <list type="bullet">
        /// <item>"Answer with one word." → the model emits EOS immediately and
        /// returns an EMPTY string. That is the raw="" seen on device.</item>
        /// <item>An instruction block listing examples → the literal token
        /// "urn", deterministically, REGARDLESS OF THE IMAGE. The "urn"
        /// observed on hardware was never a perception.</item>
        /// <item>"Describe this image." → non-empty on every run tested, with
        /// accurate content.</item>
        /// </list>
        /// So ask a natural question and let
        /// <see cref="RecognitionText"/> pull the noun out of the sentence,
        /// rather than fighting the model for a one-word answer it cannot give.
        /// </remarks>
        private const string Prompt = "What is the main object in this image? Describe it.";

        private OllamaProvider _provider;
        private readonly Queue<PendingRequest> _queue = new Queue<PendingRequest>();
        private bool _busy;

        /// <summary>
        /// A captured frame waiting for its turn at the model.
        /// </summary>
        /// <remarks>
        /// Holds the finished JPEG, not the world point. Capturing at dequeue
        /// time would photograph wherever the head had turned in the seconds
        /// the request spent queued, which is a worse failure than no crop at
        /// all: it would confidently name the wrong object.
        /// </remarks>
        private class PendingRequest
        {
            public byte[] Jpeg;
            public Action<RecognitionResult> OnComplete;
        }

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

        private void Start()
        {
            if (warmUpOnStart) StartCoroutine(WarmUp());
        }

        /// <summary>
        /// Pays the model-load cost up front. Measured: loading moondream takes
        /// about 3 seconds, and Ollama BLOCKS the first request while it
        /// happens rather than failing. Without this, the user's first pinch is
        /// the one that waits.
        /// </summary>
        private IEnumerator WarmUp()
        {
            Log("warm-up: asking Ollama to load " + model);

            Task<ChatResponse> task = null;
            try
            {
                task = _provider.ChatAsync(new ChatRequest("hi"), null, CancellationToken.None);
            }
            catch (Exception exception)
            {
                Warn("warm-up could not start: " + exception.Message);
                yield break;
            }

            var started = Time.realtimeSinceStartup;
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted)
            {
                Warn("warm-up FAILED: " + (task.Exception?.GetBaseException().Message ?? "unknown") +
                     " - is `ollama serve` running and `adb reverse tcp:11434 tcp:11434` set?");
            }
            else
            {
                Log("warm-up done in " + (Time.realtimeSinceStartup - started).ToString("F1") + "s" +
                    " (" + Describe(task.Result) + ")");
            }
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
            // Capture happens NOW, at the pinch, not when the queue reaches
            // this request. The frame and the pose must match the moment the
            // user actually pointed.
            var jpeg = Capture(worldPoint, out var captureError);

            if (jpeg == null)
            {
                Finish(new RecognitionResult { Error = captureError }, onComplete);
                return;
            }

            if (_queue.Count >= Mathf.Max(1, maxQueued))
            {
                Warn("queue full (" + _queue.Count + "); using deterministic fallback for this pinch");
                Finish(new RecognitionResult { Error = "queue full" }, onComplete);
                return;
            }

            _queue.Enqueue(new PendingRequest { Jpeg = jpeg, OnComplete = onComplete });

            if (!_busy) StartCoroutine(DrainQueue());
        }

        /// <summary>
        /// Grabs the current camera frame, cropped toward the pinched point.
        /// Returns null and sets <paramref name="error"/> on any failure.
        /// </summary>
        private byte[] Capture(Vector3 worldPoint, out string error)
        {
            error = null;

            if (cameraFeed == null || !cameraFeed.IsReady)
            {
                error = "camera not ready";
                return null;
            }

            var texture = cameraFeed.GetTexture();
            if (texture == null)
            {
                error = "GetTexture returned null";
                return null;
            }

            var centre = new Vector2(0.5f, 0.5f);
            var inside = cameraFeed.TryGetViewportPoint(worldPoint, out var projected, out var why);

            cameraFeed.TryGetPosition(out var cameraPosition);
            Log("projection: target=" + Format(worldPoint) + " camera=" + Format(cameraPosition) +
                " viewport=(" + projected.x.ToString("F2") + "," + projected.y.ToString("F2") + ")" +
                " inside=" + inside + " [" + why + "]");

            if (inside)
            {
                centre = projected;
            }
            else
            {
                // Say so rather than silently pretending the crop is on target.
                Warn("target not in camera frame (" + why + "); cropping the frame centre instead");
            }

            var jpeg = CameraCapture.EncodeCrop(texture, centre, cropFraction, jpegQuality,
                out var width, out var height, out var encodeError);

            if (jpeg == null || jpeg.Length == 0)
            {
                error = "jpeg encode failed: " + (encodeError ?? "empty");
                return null;
            }

            Log("captured " + width + "x" + height + ", jpeg=" + jpeg.Length + " bytes, " +
                "crop centre=(" + centre.x.ToString("F2") + "," + centre.y.ToString("F2") + ")" +
                " from frame " + texture.width + "x" + texture.height +
                (_queue.Count > 0 ? " [" + _queue.Count + " queued ahead]" : string.Empty));

            if (saveDebugImage) SaveDebugImage(jpeg);

            return jpeg;
        }

        private IEnumerator DrainQueue()
        {
            _busy = true;

            while (_queue.Count > 0)
            {
                var pending = _queue.Dequeue();
                yield return RecogniseRoutine(pending.Jpeg, pending.OnComplete);
            }

            _busy = false;
        }

        private IEnumerator RecogniseRoutine(byte[] jpg, Action<RecognitionResult> onComplete)
        {
            var result = new RecognitionResult();

            // -- request -------------------------------------------------------
            var request = new ChatRequest(Prompt, new List<ImageInput>
            {
                new ImageInput { bytes = jpg, mimeType = "image/jpeg" }
            });

            for (var attempt = 0; attempt <= Mathf.Max(0, loadRetries); attempt++)
            {
                Log("request started" + (attempt > 0 ? " (retry " + attempt + ")" : string.Empty));

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
                    break;
                }

                var started = Time.realtimeSinceStartup;
                var deadline = started + Mathf.Max(1f, timeoutSeconds);

                // Poll rather than await: the main thread stays free, so the
                // user can move their head and hands while this runs.
                //
                // The deadline is enforced HERE because OllamaProvider never
                // forwards the CancellationToken on the non-streaming path --
                // cancelling the source does not stop the request. Without
                // this, one hung call would block the queue forever.
                while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;

                var elapsed = Time.realtimeSinceStartup - started;
                cancellation.Cancel();
                cancellation.Dispose();

                if (!task.IsCompleted)
                {
                    result.Error = "timed out after " + timeoutSeconds + "s (abandoned)";
                    break;
                }

                if (task.IsFaulted)
                {
                    result.Error = "request failed: " +
                                   (task.Exception?.GetBaseException().Message ?? "unknown");
                    break;
                }

                if (task.IsCanceled)
                {
                    result.Error = "timed out after " + timeoutSeconds + "s";
                    break;
                }

                var response = task.Result;
                var raw = response?.text;
                result.Raw = raw;

                Log("response raw=\"" + Truncate(raw) + "\" len=" +
                    (raw?.Length ?? 0) + " in " + elapsed.ToString("F1") + "s (" +
                    Describe(response) + ")");

                // Constrained to the four frozen demo classes. Anything else
                // is reported as unrecognised rather than rounded to the
                // nearest one, which would be inventing a result.
                var word = RecognitionText.NormalizeToDemoClass(raw, Vocabulary.KnownWords);
                if (!string.IsNullOrEmpty(word))
                {
                    result.Word = word;
                    result.Error = null;
                    Log("normalized=\"" + word + "\"");
                    break;
                }

                // HttpTransport collapses every HTTP and network failure into
                // null, which the provider then turns into an empty string. A
                // null raw body therefore means the request never reached
                // Ollama -- a dropped adb reverse, a stopped server -- and is
                // worth saying out loud rather than blaming the model.
                if (!(response?.Raw is string rawBody) || string.IsNullOrEmpty(rawBody))
                {
                    result.Error = "no response from Ollama (transport failure: is `ollama serve` " +
                                   "running, and is `adb reverse tcp:11434 tcp:11434` still set?)";
                    break;
                }

                result.Error = "model returned an empty answer";
                break;
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

        /// <summary>
        /// Summarises what Ollama actually returned, from the raw JSON the
        /// provider hands back.
        /// </summary>
        /// <remarks>
        /// Meta's provider collapses a model-load reply into an empty string
        /// and logs it under its own "[Ollama]" tag, so the caller cannot
        /// otherwise tell "model was loading" from "model said nothing".
        /// <see cref="ChatResponse.Raw"/> carries the original body; reading
        /// done_reason and error out of it is what makes that distinction.
        /// </remarks>
        private static string Describe(ChatResponse response)
        {
            var raw = response?.Raw as string;
            if (string.IsNullOrEmpty(raw)) return "no raw body";

            var reason = ExtractJsonString(raw, "done_reason");
            var error = ExtractJsonString(raw, "error");

            var parts = new List<string> { "body=" + raw.Length + "B" };
            if (!string.IsNullOrEmpty(reason)) parts.Add("done_reason=" + reason);
            if (!string.IsNullOrEmpty(error)) parts.Add("ERROR=" + error);

            return string.Join(" ", parts);
        }

        private static bool IsModelLoading(ChatResponse response)
        {
            var raw = response?.Raw as string;
            return !string.IsNullOrEmpty(raw) &&
                   ExtractJsonString(raw, "done_reason") == "load";
        }

        /// <summary>Pulls one string field out of a flat JSON body.</summary>
        private static string ExtractJsonString(string json, string field)
        {
            var key = "\"" + field + "\"";
            var at = json.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) return null;

            var colon = json.IndexOf(':', at + key.Length);
            if (colon < 0) return null;

            var open = json.IndexOf('"', colon + 1);
            if (open < 0) return null;

            var close = json.IndexOf('"', open + 1);
            return close < 0 ? null : json.Substring(open + 1, close - open - 1);
        }

        /// <summary>
        /// Writes the exact bytes sent to Ollama, so the crop can be inspected
        /// off-device rather than guessed at.
        /// </summary>
        private void SaveDebugImage(byte[] jpg)
        {
            try
            {
                var path = System.IO.Path.Combine(
                    Application.persistentDataPath, "last_recognition.jpg");
                System.IO.File.WriteAllBytes(path, jpg);
                Log("debug image saved: " + path);
            }
            catch (Exception exception)
            {
                Warn("could not save debug image: " + exception.Message);
            }
        }

        private static string Format(Vector3 v)
        {
            return "(" + v.x.ToString("F2") + "," + v.y.ToString("F2") + "," + v.z.ToString("F2") + ")";
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var single = value.Replace("\n", "\\n").Replace("\r", string.Empty);
            return single.Length <= 120 ? single : single.Substring(0, 120) + "...";
        }
    }
}
