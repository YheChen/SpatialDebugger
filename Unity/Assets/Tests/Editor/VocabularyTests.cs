using System.Linq;
using NUnit.Framework;
using SpatialDebugger.Annotations;
using SpatialDebugger.Demo;

namespace SpatialDebugger.Tests
{
    /// <summary>
    /// The deterministic language-learning content, which must work offline
    /// with no backend, no camera and no AI.
    /// </summary>
    public class VocabularyTests
    {
        [Test]
        public void The_demo_cycle_is_the_four_scripted_objects_in_order()
        {
            var expected = new[] { "Chair", "Laptop", "Bottle", "Backpack" };
            CollectionAssert.AreEqual(expected, Vocabulary.Cycle.Select(e => e.English).ToArray());
        }

        [Test]
        public void Successive_pinches_walk_the_cycle_and_wrap()
        {
            Assert.AreEqual("Chair", Vocabulary.At(0).English);
            Assert.AreEqual("Laptop", Vocabulary.At(1).English);
            Assert.AreEqual("Bottle", Vocabulary.At(2).English);
            Assert.AreEqual("Backpack", Vocabulary.At(3).English);
            Assert.AreEqual("Chair", Vocabulary.At(4).English, "pinch 5 must return to the start");
        }

        [Test]
        public void Index_wrapping_handles_negatives()
        {
            Assert.IsNotNull(Vocabulary.At(-1));
            Assert.AreEqual(Vocabulary.At(3).English, Vocabulary.At(-1).English);
        }

        [Test]
        public void Every_entry_has_all_three_languages()
        {
            foreach (var entry in Vocabulary.Cycle)
            {
                Assert.IsNotEmpty(entry.English, "english");
                Assert.IsNotEmpty(entry.French, entry.English + " french");
                Assert.IsNotEmpty(entry.Spanish, entry.English + " spanish");
            }
        }

        [Test]
        public void The_label_shows_the_object_name_and_both_translations()
        {
            var label = Vocabulary.At(0).ToLabel();

            StringAssert.Contains("CHAIR", label);
            StringAssert.Contains("FR", label);
            StringAssert.Contains("chaise", label);
            StringAssert.Contains("ES", label);
            StringAssert.Contains("silla", label);
        }

        [Test]
        public void The_object_name_is_visually_prominent()
        {
            StringAssert.Contains("<size=", Vocabulary.At(0).ToLabel());
        }

        [Test]
        public void The_label_uses_no_emoji()
        {
            // The font atlas is a static Latin-1 set with no fallback, so an
            // emoji would render as nothing at all.
            foreach (var entry in Vocabulary.Cycle)
            {
                foreach (var character in SpatialText.StripRichText(entry.ToLabel()))
                {
                    Assert.Less((int)character, 0x2500,
                        entry.English + " uses a character outside the Latin range");
                }
            }
        }

        [Test]
        public void Accented_translations_survive_as_written()
        {
            Assert.AreEqual("portátil", Vocabulary.Lookup("Laptop").Spanish);
            Assert.AreEqual("sac à dos", Vocabulary.Lookup("Backpack").French);
        }

        // -- the offline translation fallback ------------------------------

        [Test]
        public void Lookup_is_case_insensitive()
        {
            Assert.AreEqual("chaise", Vocabulary.Lookup("chair").French);
            Assert.AreEqual("chaise", Vocabulary.Lookup("CHAIR").French);
            Assert.AreEqual("chaise", Vocabulary.Lookup("  Chair ").French);
        }

        [Test]
        public void Lookup_covers_words_a_recogniser_might_return()
        {
            foreach (var word in new[] { "table", "cup", "book", "phone", "keyboard", "monitor" })
            {
                Assert.IsNotNull(Vocabulary.Lookup(word), word + " should be translatable");
            }
        }

        [Test]
        public void Lookup_returns_null_for_an_unknown_word()
        {
            Assert.IsNull(Vocabulary.Lookup("flux capacitor"));
            Assert.IsNull(Vocabulary.Lookup(null));
            Assert.IsNull(Vocabulary.Lookup("   "));
        }

        [Test]
        public void LookupOrEcho_never_returns_null_so_a_label_always_renders()
        {
            var unknown = Vocabulary.LookupOrEcho("flux capacitor");
            Assert.IsNotNull(unknown);
            Assert.AreEqual("flux capacitor", unknown.English);
            Assert.IsNotEmpty(unknown.ToLabel());

            Assert.IsNotNull(Vocabulary.LookupOrEcho(null));
            Assert.IsNotNull(Vocabulary.LookupOrEcho(""));
        }

        // -- rich text -----------------------------------------------------

        [Test]
        public void StripRichText_removes_tags_but_keeps_the_words()
        {
            Assert.AreEqual("CHAIR", SpatialText.StripRichText("<size=150%>CHAIR</size>"));
            Assert.AreEqual("plain", SpatialText.StripRichText("plain"));
            Assert.AreEqual("", SpatialText.StripRichText(""));
            Assert.IsNull(SpatialText.StripRichText(null));
        }

        [Test]
        public void Plate_width_ignores_rich_text_tags()
        {
            // Counting the tag characters would oversize the backing quad.
            Assert.AreEqual(
                SpatialText.EstimateWidth("BACKPACK"),
                SpatialText.EstimateWidth("<size=150%>BACKPACK</size>"),
                0.0001f);
        }

        [Test]
        public void Plate_width_follows_the_longest_line()
        {
            var narrow = SpatialText.EstimateWidth(Vocabulary.Lookup("Chair").ToLabel());
            var wide = SpatialText.EstimateWidth(Vocabulary.Lookup("Laptop").ToLabel());

            Assert.Greater(wide, narrow, "'ordinateur portable' needs a wider plate than 'chaise'");
        }
    }
}
