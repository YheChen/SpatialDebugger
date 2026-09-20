using System.Collections.Generic;

namespace SpatialDebugger.Vision
{
    /// <summary>
    /// Restricts the vision model's reply to the four demo classes.
    /// </summary>
    /// <remarks>
    /// The model is prompted to return exactly one class. Minor presentation
    /// differences such as case, surrounding quotes and final punctuation are
    /// tolerated, but prose and unsupported nouns become <c>unknown</c>. This
    /// prevents a plausible word elsewhere in a response from being promoted
    /// to a successful recognition.
    /// </remarks>
    public static class RecognitionText
    {
        public const string Unknown = "unknown";

        private static readonly HashSet<string> Allowed = new HashSet<string>
        {
            "laptop", "table", "chair", "wall", Unknown
        };

        /// <summary>
        /// One allowed class, <c>unknown</c> for a non-empty invalid response,
        /// or null when no response was returned.
        /// </summary>
        /// <param name="raw">The model's reply, verbatim.</param>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var candidate = raw.Trim().ToLowerInvariant().Trim(
                '"', '\'', '`', '.', ',', '!', '?', ':', ';');
            candidate = candidate.Trim();

            if (candidate.Length == 0) return null;
            return Allowed.Contains(candidate) ? candidate : Unknown;
        }

        /// <summary>True only for a genuine supported demo-class result.</summary>
        public static bool IsRecognizedClass(string value)
        {
            return value == "laptop" || value == "table" ||
                   value == "chair" || value == "wall";
        }

        /// <summary>Title case for display: "chair" -> "Chair".</summary>
        public static string ForDisplay(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return word;
            var trimmed = word.Trim();
            return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
        }
    }
}
