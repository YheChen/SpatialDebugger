using System;
using System.Collections.Generic;

namespace SpatialDebugger.Demo
{
    /// <summary>One object and its translations.</summary>
    [Serializable]
    public class VocabularyEntry
    {
        public string English;
        public string French;
        public string Spanish;

        public VocabularyEntry(string english, string french, string spanish)
        {
            English = english;
            French = french;
            Spanish = spanish;
        }

        /// <summary>
        /// The label text as it appears in the headset.
        /// </summary>
        /// <remarks>
        /// <code>
        /// CHAIR
        ///
        /// FR  chaise
        /// ES  silla
        /// </code>
        /// The object name is enlarged with a TMP rich-text tag rather than a
        /// second text object, so the whole annotation stays one
        /// <see cref="Core.SpatialAction"/> and the existing renderer, plate
        /// sizing and billboarding are reused untouched.
        /// <para>
        /// Two-letter language codes, not flag emoji: the project's font atlas
        /// is a 250-glyph static Latin-1 set with no fallback configured, so an
        /// emoji would render as nothing at all. Every accented character used
        /// here (á à é ó ú ñ ç) was confirmed present in that atlas.
        /// </para>
        /// </remarks>
        public string ToLabel(string footer = null)
        {
            var label = "<size=150%>" + English.ToUpperInvariant() + "</size>\n\n" +
                        "FR  " + French + "\n" +
                        "ES  " + Spanish;

            // Always four lines, footer or not, so the backing plate does not
            // jump when ANALYZING is replaced by the answer.
            return label + "\n<size=65%>" + (footer ?? " ") + "</size>";
        }

        public override string ToString() => English;
    }

    /// <summary>
    /// The demo vocabulary, and the offline English-to-translation lookup.
    /// </summary>
    /// <remarks>
    /// Deliberately a plain static table with no dependencies: it works
    /// offline, with no backend, no camera and no AI. It serves two purposes —
    /// the deterministic pinch cycle, and the fallback translation source if a
    /// recogniser ever returns only an English object name.
    /// </remarks>
    public static class Vocabulary
    {
        private static readonly VocabularyEntry[] Entries =
        {
            new VocabularyEntry("Chair", "chaise", "silla"),
            new VocabularyEntry("Laptop", "ordinateur portable", "portátil"),
            new VocabularyEntry("Bottle", "bouteille", "botella"),
            new VocabularyEntry("Backpack", "sac à dos", "mochila"),
        };

        /// <summary>Extra words a recogniser might plausibly return.</summary>
        private static readonly VocabularyEntry[] Extra =
        {
            new VocabularyEntry("Table", "table", "mesa"),
            new VocabularyEntry("Cup", "tasse", "taza"),
            new VocabularyEntry("Book", "livre", "libro"),
            new VocabularyEntry("Phone", "téléphone", "teléfono"),
            new VocabularyEntry("Keyboard", "clavier", "teclado"),
            new VocabularyEntry("Monitor", "écran", "monitor"),
            new VocabularyEntry("Mouse", "souris", "ratón"),
            new VocabularyEntry("Chair", "chaise", "silla"),
            new VocabularyEntry("Desk", "bureau", "escritorio"),
            new VocabularyEntry("Screen", "écran", "pantalla"),
            new VocabularyEntry("Bag", "sac", "bolsa"),
            new VocabularyEntry("Can", "canette", "lata"),
            new VocabularyEntry("Mug", "tasse", "taza"),
            new VocabularyEntry("Headphones", "casque", "auriculares"),
            new VocabularyEntry("Notebook", "carnet", "cuaderno"),
            new VocabularyEntry("Door", "porte", "puerta"),
            new VocabularyEntry("Window", "fenêtre", "ventana"),
            new VocabularyEntry("Plant", "plante", "planta"),
            new VocabularyEntry("Lamp", "lampe", "lámpara"),
            new VocabularyEntry("Pen", "stylo", "bolígrafo"),

            // The rest of the demo shortlist, plus the synonyms Moondream
            // actually tends to return for them.
            new VocabularyEntry("Computer", "ordinateur", "computadora"),
            new VocabularyEntry("Couch", "canapé", "sofá"),
            new VocabularyEntry("Sofa", "canapé", "sofá"),
            new VocabularyEntry("Person", "personne", "persona"),
            new VocabularyEntry("Man", "homme", "hombre"),
            new VocabularyEntry("Woman", "femme", "mujer"),
            new VocabularyEntry("Wall", "mur", "pared"),
            new VocabularyEntry("Floor", "sol", "suelo"),
            new VocabularyEntry("Ceiling", "plafond", "techo"),
        };

        /// <summary>
        /// Every English word the lookup knows, for steering a vision model's
        /// reply toward a word we can translate.
        /// </summary>
        public static IEnumerable<string> KnownWords
        {
            get
            {
                foreach (var entry in Entries) yield return entry.English;
                foreach (var entry in Extra) yield return entry.English;
            }
        }

        /// <summary>The deterministic cycle, in order.</summary>
        public static IReadOnlyList<VocabularyEntry> Cycle => Entries;

        public static int Count => Entries.Length;

        /// <summary>
        /// Longest object name the plate can show before it reads badly at
        /// arm's length. Longer recognitions are elided.
        /// </summary>
        public const int MaximumWordLength = 22;

        /// <summary>Every entry in both tables, for tests and tooling.</summary>
        public static IEnumerable<VocabularyEntry> All
        {
            get
            {
                foreach (var entry in Entries) yield return entry;
                foreach (var entry in Extra) yield return entry;
            }
        }

        /// <summary>Wraps in both directions, so any pinch index is valid.</summary>
        public static VocabularyEntry At(int index)
        {
            if (Entries.Length == 0) return null;
            return Entries[((index % Entries.Length) + Entries.Length) % Entries.Length];
        }

        /// <summary>
        /// Translations for an English word, or null if it is not known.
        /// </summary>
        /// <remarks>
        /// The offline half of the recognition path: a recogniser that returns
        /// only "chair" still produces a full trilingual label.
        /// </remarks>
        public static VocabularyEntry Lookup(string english)
        {
            if (string.IsNullOrWhiteSpace(english)) return null;

            var needle = english.Trim();

            foreach (var table in new[] { Entries, Extra })
            {
                foreach (var entry in table)
                {
                    if (string.Equals(entry.English, needle, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Translations for an English word, falling back to showing the word
        /// on its own rather than nothing.
        /// </summary>
        public static VocabularyEntry LookupOrEcho(string english)
        {
            var found = Lookup(english);
            if (found != null) return found;

            // Show the genuine recognition with its translations marked
            // absent. Substituting a known word here would be faking the
            // result, which is worse than an honest gap.
            var word = string.IsNullOrWhiteSpace(english) ? "Object" : english.Trim();

            // Normalisation should have reduced the reply to one noun, but if
            // it ever hands back a phrase the plate is sized from the longest
            // line and would grow off the side of the room.
            if (word.Length > MaximumWordLength)
            {
                word = word.Substring(0, MaximumWordLength - 1) + "\u2026";
            }

            return new VocabularyEntry(word, "\u2014", "\u2014");
        }
    }
}
