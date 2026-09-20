using System.Linq;
using NUnit.Framework;
using SpatialDebugger.Demo;
using SpatialDebugger.Vision;

namespace SpatialDebugger.Tests
{
    /// <summary>
    /// Turning a vision model's reply into something displayable.
    /// </summary>
    /// <remarks>
    /// The prompt asks for a single noun, but small models drift. These are
    /// the shapes moondream and friends actually produce.
    /// </remarks>
    public class RecognitionTests
    {
        private static string N(string raw) => RecognitionText.Normalize(raw, Vocabulary.KnownWords);

        [Test]
        public void A_clean_one_word_answer_passes_through()
        {
            Assert.AreEqual("chair", N("chair"));
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
        public void Articles_and_filler_are_skipped()
        {
            Assert.AreEqual("chair", N("a chair"));
            Assert.AreEqual("chair", N("the chair"));
        }

        [Test]
        public void A_known_word_is_found_inside_prose()
        {
            // The whole reason Normalize takes a vocabulary.
            Assert.AreEqual("chair", N("This is a wooden chair."));
            Assert.AreEqual("laptop", N("I see a laptop on the desk"));
            Assert.AreEqual("bottle", N("It appears to be a plastic bottle."));
        }

        [Test]
        public void An_unknown_word_still_yields_the_first_real_token()
        {
            Assert.AreEqual("stapler", N("stapler"));
            Assert.AreEqual("stapler", N("a stapler."));
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

        // -- description-shaped replies -------------------------------------
        // The prompt is now a question, because moondream returns an EMPTY
        // string for "answer with one word" and a deterministic junk token for
        // instruction blocks. So the normaliser must mine a sentence.

        [Test]
        public void The_noun_after_is_a_is_extracted()
        {
            Assert.AreEqual("chair", N("The main object in this image is a chair."));
            Assert.AreEqual("laptop", N("The main object in this image is a laptop."));
            Assert.AreEqual("table", N("This is an image of a table."));
        }

        [Test]
        public void An_adjective_does_not_win_over_the_noun()
        {
            Assert.AreEqual("chair", N("The main object is a wooden chair."));
            Assert.AreEqual("bottle", N("It is a clear plastic bottle."));
        }

        [Test]
        public void Narration_verbs_are_not_mistaken_for_the_object()
        {
            // Without the noise list these would normalise to "main" or "shows".
            Assert.AreEqual("backpack", N("The image shows a backpack on the floor."));
            Assert.AreEqual("cup", N("This picture features a cup."));
        }

        [Test]
        public void A_long_caption_still_yields_the_object()
        {
            Assert.AreEqual("table",
                N("The main object in this image is a coffee table, positioned in the "
                  + "center of the living room with a couch behind it."));
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
        public void The_demo_objects_all_translate()
        {
            foreach (var word in new[] { "chair", "laptop", "bottle", "backpack" })
            {
                var entry = Vocabulary.Lookup(word);
                Assert.IsNotNull(entry, word);
                Assert.AreNotEqual("—", entry.French, word + " should have a real translation");
            }
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
    }
}
