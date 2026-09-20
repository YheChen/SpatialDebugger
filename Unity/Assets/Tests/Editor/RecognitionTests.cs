using System.Linq;
using NUnit.Framework;
using SpatialDebugger.Annotations;
using SpatialDebugger.Demo;
using SpatialDebugger.Vision;

namespace SpatialDebugger.Tests
{
    /// <summary>
    /// Turning a vision model's reply into something displayable.
    /// </summary>
    /// <remarks>
    /// The frozen demo prompt asks Moondream to classify exactly one of
    /// four objects or unknown. Parsing must never mine a class out of prose.
    /// </remarks>
    public class RecognitionTests
    {
        private static string N(string raw) => RecognitionText.Normalize(raw);

        [Test]
        public void The_four_supported_one_word_answers_pass_through()
        {
            Assert.AreEqual("chair", N("chair"));
            Assert.AreEqual("laptop", N("laptop"));
            Assert.AreEqual("table", N("table"));
            Assert.AreEqual("wall", N("wall"));
        }

        [Test]
        public void Case_and_whitespace_are_normalised()
        {
            Assert.AreEqual("chair", N("  Chair  "));
            Assert.AreEqual("chair", N("CHAIR"));
            Assert.AreEqual("chair", N("chair\n"));
        }

        [Test]
        public void Punctuation_is_stripped()
        {
            Assert.AreEqual("chair", N("chair."));
            Assert.AreEqual("chair", N("\"chair\""));
            Assert.AreEqual("chair", N("chair!"));
        }

        [Test]
        public void Prose_is_not_mined_for_a_supported_class()
        {
            Assert.AreEqual("unknown", N("a chair"));
            Assert.AreEqual("unknown", N("The main object is a laptop."));
        }

        [Test]
        public void Unsupported_and_multiple_answers_become_unknown()
        {
            Assert.AreEqual("unknown", N("couch"));
            Assert.AreEqual("unknown", N("chair table"));
            Assert.AreEqual("unknown", N("urn"));
        }

        [Test]
        public void Explicit_unknown_remains_unknown()
        {
            Assert.AreEqual("unknown", N("unknown"));
            Assert.AreEqual("unknown", N(" UNKNOWN. "));
        }

        [Test]
        public void Empty_and_junk_replies_yield_null()
        {
            Assert.IsNull(N(null));
            Assert.IsNull(N(""));
            Assert.IsNull(N("   "));
            Assert.IsNull(N("..."));
            Assert.IsNull(N("!!!"));
        }

        [Test]
        public void Display_casing_capitalises()
        {
            Assert.AreEqual("Chair", RecognitionText.ForDisplay("chair"));
            Assert.AreEqual("Chair", RecognitionText.ForDisplay("Chair"));
            Assert.IsNull(RecognitionText.ForDisplay(null));
        }

        // -- translation of recognised words --------------------------------

        [Test]
        public void The_four_supported_classes_all_translate()
        {
            foreach (var word in new[] { "laptop", "table", "chair", "wall" })
            {
                var entry = Vocabulary.Lookup(word);
                Assert.IsNotNull(entry, word);
                Assert.AreNotEqual("—", entry.French, word + " should have a real translation");
            }

            Assert.AreEqual("ordinateur portable", Vocabulary.Lookup("laptop").French);
            Assert.AreEqual("portátil", Vocabulary.Lookup("laptop").Spanish);
            Assert.AreEqual("table", Vocabulary.Lookup("table").French);
            Assert.AreEqual("mesa", Vocabulary.Lookup("table").Spanish);
            Assert.AreEqual("chaise", Vocabulary.Lookup("chair").French);
            Assert.AreEqual("silla", Vocabulary.Lookup("chair").Spanish);
            Assert.AreEqual("mur", Vocabulary.Lookup("wall").French);
            Assert.AreEqual("pared", Vocabulary.Lookup("wall").Spanish);
        }

        [Test]
        public void Unknown_is_not_a_recognition_success()
        {
            Assert.IsFalse(new RecognitionResult { Word = "unknown" }.Success);
            Assert.IsFalse(new RecognitionResult { Word = null }.Success);
            Assert.IsTrue(new RecognitionResult { Word = "chair" }.Success);
        }

        [Test]
        public void The_likely_demo_objects_translate_too()
        {
            foreach (var word in new[] { "keyboard", "monitor", "phone", "table", "cup", "mouse", "book" })
            {
                Assert.IsNotNull(Vocabulary.Lookup(word), word + " should be translatable");
            }
        }

        [Test]
        public void An_unknown_recognition_is_shown_honestly_not_replaced()
        {
            // A genuine recognition outside the dictionary must survive as
            // itself. Substituting a known word would be faking the result.
            var entry = Vocabulary.LookupOrEcho("Stapler");

            Assert.AreEqual("Stapler", entry.English);
            Assert.AreEqual("—", entry.French);
            Assert.AreEqual("—", entry.Spanish);
            StringAssert.Contains("STAPLER", entry.ToLabel());
        }

        [Test]
        public void KnownWords_covers_the_whole_dictionary()
        {
            var words = Vocabulary.KnownWords.ToList();

            CollectionAssert.Contains(words, "Chair");
            CollectionAssert.Contains(words, "Keyboard");
            Assert.Greater(words.Count, 15);
        }

        [Test]
        public void The_analyzing_placeholder_is_shaped_like_a_finished_label()
        {
            // Same line count as a real answer, so the plate does not jump
            // when the text is replaced.
            var placeholder = PinchAnnotationPlacer.AnalyzingLabel;

            StringAssert.Contains("ANALYZING", placeholder);
            StringAssert.Contains("<size=", placeholder);
            Assert.AreEqual(
                Vocabulary.At(0).ToLabel().Split('\n').Length,
                placeholder.Split('\n').Length,
                "placeholder should have the same number of lines as a real label");
        }

        [Test]
        public void A_failed_recognition_does_not_borrow_a_vocabulary_word()
        {
            // The regression this guards: a failed recognition used to be
            // replaced by the next word in the deterministic cycle, which on
            // stage looks exactly like a successful one.
            var failed = PinchAnnotationPlacer.UnrecognizedLabel;

            foreach (var entry in Vocabulary.Cycle)
            {
                StringAssert.DoesNotContain(entry.English.ToUpperInvariant(), failed);
            }

            StringAssert.Contains("NOT RECOGNIZED", failed);
        }

        [Test]
        public void The_unrecognized_label_is_shaped_like_a_finished_label()
        {
            // Same line count, so the plate does not jump when ANALYZING is
            // replaced by the failure.
            Assert.AreEqual(
                PinchAnnotationPlacer.AnalyzingLabel.Split('\n').Length,
                PinchAnnotationPlacer.UnrecognizedLabel.Split('\n').Length);
        }

        [Test]
        public void Every_character_of_the_failure_label_is_in_the_font_atlas()
        {
            // The em dashes are the risk here: the atlas is static Latin-1, and
            // a missing glyph renders as nothing at all, silently, on device.
            var font = TMPro.TMP_Settings.defaultFontAsset;
            Assert.IsNotNull(font, "no default font asset");

            var visible = SpatialText.StripRichText(PinchAnnotationPlacer.UnrecognizedLabel);
            foreach (var character in visible)
            {
                if (char.IsWhiteSpace(character)) continue;

                Assert.IsTrue(font.HasCharacter(character),
                    string.Format("'{0}' (U+{1:X4}) in the failure label is not in the atlas",
                        character, (int)character));
            }
        }
    }
}
