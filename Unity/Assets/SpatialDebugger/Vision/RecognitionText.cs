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
            // A description prompt narrates before it names. Without these the
            // first "real" token is a verb: "The main object in this image is a
            // chair" would normalise to "main".
            "main", "depicts", "presents", "features", "shows", "displays",
            "scene", "view", "rendering", "contains", "centre", "center", "middle",
            "foreground", "background", "appears", "visible", "sitting", "standing",
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

            // "The main object in this image is a chair." -- the noun follows
            // "is a"/"is an". A description prompt produces this shape
            // reliably, so look for it before falling back to scanning.
            for (var i = 0; i < tokens.Length - 1; i++)
            {
                if (tokens[i] != "is") continue;

                var next = i + 1;
                if (next < tokens.Length && (tokens[next] == "a" || tokens[next] == "an")) next++;

                // Skip adjectives only if they are filler we already know.
                while (next < tokens.Length && Noise.Contains(tokens[next])) next++;

                if (next < tokens.Length && tokens[next].Length > 1)
                {
                    // Prefer a known word later in the phrase ("is a wooden
                    // chair" -> chair, not wooden).
                    if (known != null)
                    {
                        var vocabulary = new HashSet<string>();
                        foreach (var word in known)
                        {
                            if (!string.IsNullOrWhiteSpace(word)) vocabulary.Add(word.ToLowerInvariant());
                        }

                        for (var j = next; j < tokens.Length && j < next + 4; j++)
                        {
                            if (vocabulary.Contains(tokens[j])) return tokens[j];
                        }
                    }

                    return tokens[next];
                }
            }

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

        /// <summary>
        /// The four classes the demo is frozen around, plus the honest
        /// alternative.
        /// </summary>
        public static readonly string[] DemoClasses = { "laptop", "table", "chair", "wall" };

        /// <summary>
        /// Tight synonym table. Every entry is a word this model has actually
        /// been observed to use for one of the four, not a guess at what it
        /// might say.
        /// </summary>
        /// <remarks>
        /// "computer" is the one judgement call: moondream reaches for it
        /// constantly when it means a laptop. A desktop tower would be called
        /// a laptop by this mapping, which is a trade the frozen demo can
        /// afford and a general product could not.
        /// <para>
        /// "notebook" is deliberately absent even though it reads as a laptop
        /// synonym, because the vocabulary already has Notebook meaning the
        /// paper kind.
        /// </para>
        /// </remarks>
        private static readonly Dictionary<string, string> DemoSynonyms =
            new Dictionary<string, string>
            {
                { "laptop", "laptop" }, { "laptops", "laptop" },
                { "macbook", "laptop" }, { "computer", "laptop" },

                { "table", "table" }, { "tables", "table" },
                { "desk", "table" }, { "desks", "table" },
                { "tabletop", "table" }, { "countertop", "table" },

                { "chair", "chair" }, { "chairs", "chair" },
                { "seat", "chair" }, { "stool", "chair" }, { "armchair", "chair" },

                { "wall", "wall" }, { "walls", "wall" },
            };

        /// <summary>
        /// Constrains a recognised noun to the frozen demo classes, or null
        /// for "unknown".
        /// </summary>
        /// <remarks>
        /// The constraint lives here rather than in the prompt because it
        /// cannot live in the prompt. moondream is a 2024 VQA model with no
        /// instruction tuning: measured against the running Ollama, every
        /// constrained phrasing -- "choose exactly one of ... respond with
        /// only that word", "answer with laptop, table, chair, wall, or
        /// unknown" -- returns either an empty string (eval_count=1, immediate
        /// EOS) or the literal token "urn", on every image. Only the plain
        /// descriptive question answers reliably, so it is kept and the
        /// classification is done deterministically on the reply.
        /// <para>
        /// Anything outside the table becomes null. A couch stays a couch and
        /// is reported as unrecognised rather than being rounded to "chair",
        /// which would be inventing a result.
        /// </para>
        /// </remarks>
        public static string ToDemoClass(string noun)
        {
            if (string.IsNullOrWhiteSpace(noun)) return null;

            return DemoSynonyms.TryGetValue(noun.Trim().ToLowerInvariant(), out var demoClass)
                ? demoClass
                : null;
        }

        /// <summary>
        /// Normalises a reply and constrains it to the demo classes in one
        /// step. Null means unknown.
        /// </summary>
        public static string NormalizeToDemoClass(string raw, IEnumerable<string> known = null)
        {
            // Scan the whole reply for a class word first: the descriptive
            // prompt buries the noun ("The main object in this image is a
            // wooden chair"), and positional parsing can land on the adjective.
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var lowered = raw.ToLowerInvariant();
                foreach (var token in lowered.Split(NonWord,
                             System.StringSplitOptions.RemoveEmptyEntries))
                {
                    var direct = ToDemoClass(token);
                    if (direct != null) return direct;
                }
            }

            return ToDemoClass(Normalize(raw, known));
        }

        private static readonly char[] NonWord =
            { ' ', '.', ',', ';', ':', '!', '?', '"', '\'', '(', ')', '\n', '\r', '\t', '/' };

        /// <summary>Title case for display: "chair" -> "Chair".</summary>
        public static string ForDisplay(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return word;
            var trimmed = word.Trim();
            return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
        }
    }
}
