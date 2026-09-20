using System.Collections.Generic;
using System.Text;

namespace SpatialDebugger.Vision
{
    /// <summary>
    /// Turns whatever a vision model actually says into a single noun.
    /// </summary>
    /// <remarks>
    /// The prompt asks for one word, but small models drift: "a chair.",
    /// "This is a wooden chair", "chair\n". Pure string handling, no Unity
    /// types, so it is directly testable.
    /// </remarks>
    public static class RecognitionText
    {
        private static readonly HashSet<string> Noise = new HashSet<string>
        {
            "a", "an", "the", "this", "that", "is", "it", "its", "there",
            "i", "see", "im", "seeing", "looks", "like", "appears", "to", "be",
            "of", "in", "on", "at", "image", "picture", "photo", "object", "likely",
        };

        /// <summary>
        /// The recognised noun, or null when nothing usable came back.
        /// </summary>
        /// <param name="raw">The model's reply, verbatim.</param>
        /// <param name="known">
        /// Optional vocabulary. When a known word appears anywhere in the
        /// reply it wins, which rescues "a wooden chair" -> "chair".
        /// </param>
        public static string Normalize(string raw, IEnumerable<string> known = null)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            // Letters, spaces and hyphens only; everything else becomes a space.
            var cleaned = new StringBuilder(raw.Length);
            foreach (var character in raw.ToLowerInvariant())
            {
                if (char.IsLetter(character) || character == '-') cleaned.Append(character);
                else cleaned.Append(' ');
            }

            var tokens = cleaned.ToString()
                .Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0) return null;

            // A known word anywhere in the reply beats positional guessing.
            if (known != null)
            {
                var lookup = new HashSet<string>();
                foreach (var word in known)
                {
                    if (!string.IsNullOrWhiteSpace(word)) lookup.Add(word.ToLowerInvariant());
                }

                foreach (var token in tokens)
                {
                    if (lookup.Contains(token)) return token;
                }
            }

            // Otherwise the first token that is not filler.
            foreach (var token in tokens)
            {
                if (token.Length > 1 && !Noise.Contains(token)) return token;
            }

            // Everything was filler: fall back to the first token so a reply
            // that is genuinely one short word still survives.
            return tokens[0].Length > 0 ? tokens[0] : null;
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
