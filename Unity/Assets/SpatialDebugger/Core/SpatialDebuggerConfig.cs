using UnityEngine;

namespace SpatialDebugger.Core
{
    /// <summary>
    /// Project-wide settings, loaded from <c>Resources/SpatialDebuggerConfig</c>.
    /// </summary>
    /// <remarks>
    /// The backend host is configuration, never a constant. On a Quest,
    /// <c>localhost</c> is the headset itself -- it is not the Mac running
    /// uvicorn -- so the LAN address has to be supplied and has to be
    /// changeable without a rebuild. <see cref="BackendHost"/> reads through
    /// to <see cref="PlayerPrefs"/> so a value typed in-headset survives a
    /// restart.
    /// </remarks>
    public class SpatialDebuggerConfig : ScriptableObject
    {
        public const string ResourcePath = "SpatialDebuggerConfig";
        private const string HostPrefKey = "SpatialDebugger.BackendHost";
        private const string PortPrefKey = "SpatialDebugger.BackendPort";

        [Header("Backend")]
        [Tooltip("LAN IP or hostname of the machine running the FastAPI service. " +
                 "On a Quest this must NOT be 'localhost'. Find it with: ipconfig getifaddr en0")]
        [SerializeField] private string backendHost = "192.168.1.100";

        [SerializeField] private int backendPort = 8000;
        [SerializeField] private bool useHttps;

        [Tooltip("Seconds before a request is abandoned and the demo fallback runs.")]
        [SerializeField] private int timeoutSeconds = 8;

        [Header("Behaviour")]
        [Tooltip("When the backend cannot be reached, answer from the on-device demo " +
                 "instead of showing an error. Leave this on for the demo.")]
        [SerializeField] private bool fallBackToLocalDemo = true;

        [Tooltip("Ping /health on startup so the UI can show backend status.")]
        [SerializeField] private bool probeHealthOnStart = true;

        [Header("Annotations")]
        [Tooltip("Metres. Annotations from the backend are authored at breadboard " +
                 "scale; raise this if they read too small in the headset.")]
        [SerializeField] private float annotationScale = 1f;

        [SerializeField] private int maxConcurrentAnnotations = 64;

        private static SpatialDebuggerConfig _instance;

        /// <summary>
        /// The active config. Falls back to an in-memory default so nothing
        /// null-references if the asset is missing.
        /// </summary>
        public static SpatialDebuggerConfig Instance
        {
            get
            {
                if (_instance != null) return _instance;

                _instance = Resources.Load<SpatialDebuggerConfig>(ResourcePath);
                if (_instance == null)
                {
                    Debug.LogWarning(
                        "[SpatialDebugger] No Resources/" + ResourcePath + " asset found. " +
                        "Using built-in defaults. Run Tools > SpatialDebugger > Set Up Scene to create one.");
                    _instance = CreateInstance<SpatialDebuggerConfig>();
                }

                return _instance;
            }
        }

        // -- backend -------------------------------------------------------

        /// <summary>Host, with any in-headset override applied.</summary>
        public string BackendHost
        {
            get
            {
                var stored = PlayerPrefs.GetString(HostPrefKey, string.Empty);
                return string.IsNullOrWhiteSpace(stored) ? backendHost : stored;
            }
            set
            {
                var trimmed = (value ?? string.Empty).Trim();
                PlayerPrefs.SetString(HostPrefKey, trimmed);
                PlayerPrefs.Save();
            }
        }

        public int BackendPort
        {
            get
            {
                var stored = PlayerPrefs.GetInt(PortPrefKey, 0);
                return stored > 0 ? stored : backendPort;
            }
            set
            {
                PlayerPrefs.SetInt(PortPrefKey, Mathf.Clamp(value, 1, 65535));
                PlayerPrefs.Save();
            }
        }

        public bool UseHttps => useHttps;
        public int TimeoutSeconds => Mathf.Max(1, timeoutSeconds);
        public bool FallBackToLocalDemo => fallBackToLocalDemo;
        public bool ProbeHealthOnStart => probeHealthOnStart;
        public float AnnotationScale => Mathf.Max(0.01f, annotationScale);
        public int MaxConcurrentAnnotations => Mathf.Max(1, maxConcurrentAnnotations);

        /// <summary>Base URL with no trailing slash, e.g. <c>http://192.168.1.42:8000</c>.</summary>
        public string BaseUrl
        {
            get
            {
                var host = BackendHost;
                if (string.IsNullOrWhiteSpace(host)) host = "127.0.0.1";
                host = host.Trim().TrimEnd('/');

                // Tolerate someone pasting a full URL into the host field.
                if (host.StartsWith("http://") || host.StartsWith("https://"))
                {
                    return host;
                }

                var scheme = useHttps ? "https" : "http";
                return scheme + "://" + host + ":" + BackendPort;
            }
        }

        public string UrlFor(string path)
        {
            if (string.IsNullOrEmpty(path)) return BaseUrl;
            return BaseUrl + (path.StartsWith("/") ? path : "/" + path);
        }

        /// <summary>Forget any in-headset override and go back to the asset values.</summary>
        public void ResetHostOverride()
        {
            PlayerPrefs.DeleteKey(HostPrefKey);
            PlayerPrefs.DeleteKey(PortPrefKey);
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: set the values baked into the asset.</summary>
        public void EditorSetBackend(string host, int port)
        {
            backendHost = host;
            backendPort = port;
        }
#endif
    }
}
