using NUnit.Framework;
using SpatialDebugger.Annotations;
using SpatialDebugger.Demo;

namespace SpatialDebugger.Tests
{
    /// <summary>
    /// Guards the vocabulary against the font atlas.
    /// </summary>
    /// <remarks>
    /// The project ships LiberationSans SDF with a STATIC atlas (250 glyphs,
    /// Latin-1) and <c>m_fallbackFontAssets: []</c>. A character outside that
    /// set renders as nothing at all — silently, and only on device. This test
    /// is what stops someone adding a word with a glyph the font cannot draw.
    /// </remarks>
    public class VocabularyFontTests
    {
        [Test]
        public void The_default_font_asset_is_available()
        {
            Assert.IsTrue(SpatialText.IsTextMeshProAvailable,
                "TMP essential resources are missing; annotation text will not render properly");
            Assert.IsNotNull(TMPro.TMP_Settings.defaultFontAsset);
        }

        [Test]
        public void Every_character_in_every_label_exists_in_the_font_atlas()
        {
            var font = TMPro.TMP_Settings.defaultFontAsset;
            Assert.IsNotNull(font, "no default font asset");

            foreach (var entry in Vocabulary.Cycle)
            {
                var visible = SpatialText.StripRichText(entry.ToLabel());

                foreach (var character in visible)
                {
                    if (character == '\n' || character == '\r') continue;

                    Assert.IsTrue(font.HasCharacter(character),
                        string.Format("'{0}' (U+{1:X4}) in the {2} label is not in the font atlas",
                            character, (int)character, entry.English));
                }
            }
        }

        [Test]
        public void The_accented_characters_the_translations_need_are_present()
        {
            var font = TMPro.TMP_Settings.defaultFontAsset;
            foreach (var character in new[] { 'á', 'à', 'é', 'ó', 'ú', 'ñ', 'ç', 'ê', 'í' })
            {
                Assert.IsTrue(font.HasCharacter(character),
                    "U+" + ((int)character).ToString("X4") + " missing from the atlas");
            }
        }
    }
}
