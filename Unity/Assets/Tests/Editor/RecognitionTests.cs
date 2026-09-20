using System.Linq;
using NUnit.Framework;
using SpatialDebugger.Annotations;
using SpatialDebugger.Demo;
using SpatialDebugger.Vision;
using UnityEngine;

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
                Vocabulary.At(0).ToLabel(PinchAnnotationPlacer.RecognizedBadge).Split('\n').Length,
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
        public void Only_a_real_recognition_claims_the_AI_badge()
        {
            var recognised = Vocabulary.Lookup("Chair")
                .ToLabel(PinchAnnotationPlacer.RecognizedFooter(0.7f));

            StringAssert.Contains(PinchAnnotationPlacer.RecognizedBadge, recognised);
            StringAssert.Contains("0.70 m", recognised);

            // The two states that must never claim it.
            StringAssert.DoesNotContain(
                PinchAnnotationPlacer.RecognizedBadge, PinchAnnotationPlacer.UnrecognizedLabel);
            StringAssert.DoesNotContain(
                PinchAnnotationPlacer.RecognizedBadge, PinchAnnotationPlacer.AnalyzingLabel);
            StringAssert.DoesNotContain(
                PinchAnnotationPlacer.RecognizedBadge,
                Vocabulary.At(0).ToLabel(PinchAnnotationPlacer.OfflineBadge));
        }

        [Test]
        public void An_offline_card_says_so()
        {
            var offline = Vocabulary.At(0).ToLabel(PinchAnnotationPlacer.OfflineBadge);
            StringAssert.Contains("OFFLINE", offline);
        }

        [Test]
        public void The_distance_is_formatted_to_two_places_in_metres()
        {
            StringAssert.Contains("0.70 m", PinchAnnotationPlacer.RecognizedFooter(0.7f));
            StringAssert.Contains("2.99 m", PinchAnnotationPlacer.RecognizedFooter(2.9914f));
        }

        [Test]
        public void Every_state_has_the_same_number_of_lines()
        {
            // The plate is sized from the line count, so a mismatch makes the
            // annotation visibly jump when the answer lands.
            var expected = Vocabulary.At(0).ToLabel(PinchAnnotationPlacer.RecognizedBadge)
                .Split('\n').Length;

            Assert.AreEqual(expected, PinchAnnotationPlacer.AnalyzingLabel.Split('\n').Length);
            Assert.AreEqual(expected, PinchAnnotationPlacer.UnrecognizedLabel.Split('\n').Length);
            Assert.AreEqual(expected, Vocabulary.At(0).ToLabel().Split('\n').Length);
        }

        [Test]
        public void An_overlong_recognition_is_elided_rather_than_shown_whole()
        {
            var entry = Vocabulary.LookupOrEcho(
                "a large comfortable upholstered sitting apparatus");

            Assert.LessOrEqual(entry.English.Length, Vocabulary.MaximumWordLength);
            StringAssert.EndsWith("\u2026", entry.English);
        }

        [Test]
        public void The_demo_shortlist_is_all_translated()
        {
            var shortlist = new[]
            {
                "chair", "table", "desk", "laptop", "computer", "bottle", "phone",
                "backpack", "couch", "person", "door", "wall", "floor", "ceiling",
                "monitor", "keyboard", "mouse", "cup", "book", "window"
            };

            foreach (var word in shortlist)
            {
                var entry = Vocabulary.Lookup(word);
                Assert.IsNotNull(entry, word + " has no translation");
                Assert.AreNotEqual("\u2014", entry.French, word + " has no French");
                Assert.AreNotEqual("\u2014", entry.Spanish, word + " has no Spanish");
            }
        }

        // -- the four frozen demo classes ------------------------------------

        [Test]
        public void Each_demo_class_is_recognised_from_a_descriptive_reply()
        {
            var replies = new[]
            {
                new[] { "The main object in this image is a laptop.", "laptop" },
                new[] { "The main object in this image is a wooden table.", "table" },
                new[] { "The main object in the image is a chair.", "chair" },
                new[] { "The main object in this image is a white wall.", "wall" },
            };

            foreach (var pair in replies)
            {
                Assert.AreEqual(pair[1],
                    RecognitionText.NormalizeToDemoClass(pair[0], Vocabulary.KnownWords),
                    pair[0]);
            }
        }

        [Test]
        public void Observed_synonyms_map_onto_a_demo_class()
        {
            Assert.AreEqual("table", RecognitionText.ToDemoClass("desk"));
            Assert.AreEqual("chair", RecognitionText.ToDemoClass("stool"));
            Assert.AreEqual("laptop", RecognitionText.ToDemoClass("computer"));
            Assert.AreEqual("wall", RecognitionText.ToDemoClass("walls"));
        }

        [Test]
        public void Anything_outside_the_four_classes_is_unknown()
        {
            // The regression this guards is the tempting one: rounding a couch
            // to a chair so the demo always shows something.
            foreach (var outside in new[]
                     {
                         "The main object in this image is a couch, which is situated in a room.",
                         "The main object in this image is the Golden Gate Bridge.",
                         "The main object in the image is a small cartoon character.",
                         "urn", "", "   ", null
                     })
            {
                Assert.IsNull(RecognitionText.NormalizeToDemoClass(outside, Vocabulary.KnownWords),
                    "should be unknown: " + (outside ?? "null"));
            }
        }

        [Test]
        public void A_class_word_wins_over_the_adjective_in_front_of_it()
        {
            Assert.AreEqual("chair",
                RecognitionText.NormalizeToDemoClass(
                    "The main object is a large comfortable wooden chair.",
                    Vocabulary.KnownWords));
        }

        [Test]
        public void Every_demo_class_has_the_agreed_translations()
        {
            var expected = new[]
            {
                new[] { "laptop", "ordinateur portable", "portátil" },
                new[] { "table", "table", "mesa" },
                new[] { "chair", "chaise", "silla" },
                new[] { "wall", "mur", "pared" },
            };

            foreach (var row in expected)
            {
                var entry = Vocabulary.Lookup(row[0]);
                Assert.IsNotNull(entry, row[0] + " is not in the vocabulary");
                Assert.AreEqual(row[1], entry.French, row[0] + " French");
                Assert.AreEqual(row[2], entry.Spanish, row[0] + " Spanish");
            }
        }

        [Test]
        public void Every_demo_class_survives_the_whole_display_path()
        {
            // normalize -> title case -> vocabulary -> label, the way the
            // placer does it, so a class cannot pass normalisation and then
            // lose its translations.
            foreach (var demoClass in RecognitionText.DemoClasses)
            {
                var entry = Vocabulary.LookupOrEcho(RecognitionText.ForDisplay(demoClass));
                var label = entry.ToLabel(PinchAnnotationPlacer.RecognizedFooter(0.7f));

                StringAssert.Contains(demoClass.ToUpperInvariant(), label);
                StringAssert.DoesNotContain("\u2014", label, demoClass + " lost a translation");
                StringAssert.Contains(PinchAnnotationPlacer.RecognizedBadge, label);
            }
        }

        // -- floor and ceiling, decided by geometry ---------------------------

        private const float Sure = 1f;

        [Test]
        public void A_flat_surface_at_the_users_feet_is_the_floor()
        {
            Assert.AreEqual("floor",
                PinchAnnotationPlacer.GeometricClass(Vector3.up, Sure, 0.02f));
        }

        [Test]
        public void A_flat_surface_overhead_is_the_ceiling()
        {
            Assert.AreEqual("ceiling",
                PinchAnnotationPlacer.GeometricClass(Vector3.down, Sure, 2.45f));
        }

        [Test]
        public void Either_face_of_a_horizontal_surface_gives_the_same_answer()
        {
            // The sensor is free to report either side, so horizontality is
            // taken from the absolute Y and height decides which surface it is.
            Assert.AreEqual("floor",
                PinchAnnotationPlacer.GeometricClass(Vector3.down, Sure, 0.02f));
            Assert.AreEqual("ceiling",
                PinchAnnotationPlacer.GeometricClass(Vector3.up, Sure, 2.45f));
        }

        [Test]
        public void A_vertical_wall_is_never_overridden()
        {
            foreach (var height in new[] { 0.1f, 1.0f, 2.4f })
            {
                Assert.IsNull(
                    PinchAnnotationPlacer.GeometricClass(Vector3.forward, Sure, height),
                    "a wall at " + height + "m must be left to the model");
                Assert.IsNull(
                    PinchAnnotationPlacer.GeometricClass(Vector3.right, Sure, height));
            }
        }

        [Test]
        public void A_table_top_is_left_to_the_model()
        {
            // The regression this guards: a table has a normal every bit as
            // vertical as the floor's, so naming floors from the normal alone
            // would rename every table in the demo.
            foreach (var height in new[] { 0.45f, 0.74f, 1.1f })
            {
                Assert.IsNull(
                    PinchAnnotationPlacer.GeometricClass(Vector3.up, Sure, height),
                    "a table top at " + height + "m must be left to the model");
            }
        }

        [Test]
        public void A_low_confidence_normal_never_overrides_the_model()
        {
            Assert.IsNull(PinchAnnotationPlacer.GeometricClass(Vector3.up, 0f, 0.02f));
            Assert.IsNull(PinchAnnotationPlacer.GeometricClass(Vector3.down, 0.2f, 2.45f));
        }

        [Test]
        public void An_absent_normal_never_overrides_the_model()
        {
            Assert.IsNull(PinchAnnotationPlacer.GeometricClass(Vector3.zero, Sure, 0.02f));
        }

        [Test]
        public void A_surface_tilted_past_the_threshold_is_left_to_the_model()
        {
            // 45 degrees: |y| is 0.71, below the 0.85 the override demands.
            var tilted = (Vector3.up + Vector3.forward).normalized;
            Assert.IsNull(PinchAnnotationPlacer.GeometricClass(tilted, Sure, 0.02f));
        }

        [Test]
        public void Floor_and_ceiling_are_demo_classes_with_the_agreed_translations()
        {
            CollectionAssert.Contains(RecognitionText.DemoClasses, "floor");
            CollectionAssert.Contains(RecognitionText.DemoClasses, "ceiling");

            var floor = Vocabulary.Lookup("floor");
            Assert.AreEqual("sol", floor.French);
            Assert.AreEqual("suelo", floor.Spanish);

            var ceiling = Vocabulary.Lookup("ceiling");
            Assert.AreEqual("plafond", ceiling.French);
            Assert.AreEqual("techo", ceiling.Spanish);
        }

        [Test]
        public void A_model_reply_naming_the_floor_still_classifies()
        {
            Assert.AreEqual("floor", RecognitionText.NormalizeToDemoClass(
                "The main object in this image is a wooden floor.", Vocabulary.KnownWords));
            Assert.AreEqual("ceiling", RecognitionText.NormalizeToDemoClass(
                "The main object in this image is a white ceiling.", Vocabulary.KnownWords));
        }

        [Test]
        public void A_geometric_answer_does_not_claim_the_AI_badge()
        {
            var geometric = PinchAnnotationPlacer.GeometricFooter(0.7f);

            StringAssert.DoesNotContain(PinchAnnotationPlacer.RecognizedBadge, geometric);
            StringAssert.Contains("0.70 m", geometric);
        }

        [Test]
        public void The_four_original_classes_are_untouched()
        {
            foreach (var word in new[] { "laptop", "table", "chair", "wall" })
            {
                CollectionAssert.Contains(RecognitionText.DemoClasses, word);
                Assert.AreEqual(word, RecognitionText.ToDemoClass(word));
            }
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
