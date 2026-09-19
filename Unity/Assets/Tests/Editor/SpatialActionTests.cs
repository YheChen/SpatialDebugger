using NUnit.Framework;
using SpatialDebugger.Core;
using UnityEngine;

namespace SpatialDebugger.Tests
{
    /// <summary>
    /// The C# half of the wire contract. Mirrors backend/tests/test_actions.py:
    /// the two protocol definitions are kept in step by hand, so both halves
    /// are pinned by tests.
    /// </summary>
    public class SpatialActionTests
    {
        [Test]
        public void Label_without_text_is_invalid()
        {
            var action = new SpatialAction
            {
                Type = SpatialActionType.Label,
                Position = Vector3.zero,
                HasPosition = true
            };

            Assert.IsFalse(action.IsValid(out var error));
            StringAssert.Contains("text", error);
        }

        [Test]
        public void Label_without_position_is_invalid()
        {
            var action = new SpatialAction { Type = SpatialActionType.Label, Text = "GPIO 12" };
            Assert.IsFalse(action.IsValid(out var error));
            StringAssert.Contains("position", error);
        }

        [Test]
        public void Label_with_text_and_position_is_valid()
        {
            Assert.IsTrue(SpatialAction.Label(Vector3.zero, "GPIO 12").IsValid(out _));
        }

        [Test]
        public void Warning_follows_the_same_rules_as_label()
        {
            Assert.IsTrue(SpatialAction.Warning(Vector3.up, "bad").IsValid(out _));

            var noText = new SpatialAction
            {
                Type = SpatialActionType.Warning,
                Position = Vector3.up,
                HasPosition = true
            };
            Assert.IsFalse(noText.IsValid(out _));
        }

        [Test]
        public void Arrow_needs_both_endpoints()
        {
            var onlyFrom = new SpatialAction
            {
                Type = SpatialActionType.Arrow,
                From = Vector3.up,
                HasFrom = true
            };
            Assert.IsFalse(onlyFrom.IsValid(out _));

            var both = SpatialAction.Arrow(Vector3.up, Vector3.zero);
            Assert.IsTrue(both.IsValid(out _));
        }

        [Test]
        public void Marker_needs_only_a_position()
        {
            var marker = SpatialAction.Marker(Vector3.zero);
            Assert.IsTrue(marker.IsValid(out _));
            Assert.IsNull(marker.Text);
        }

        [Test]
        public void Highlight_needs_only_a_position()
        {
            Assert.IsTrue(SpatialAction.Highlight(Vector3.zero, 0.05f).IsValid(out _));
        }

        [Test]
        public void Clear_needs_nothing()
        {
            Assert.IsTrue(SpatialAction.Clear().IsValid(out _));
        }

        [Test]
        public void Unknown_type_is_invalid()
        {
            var action = new SpatialAction { Type = SpatialActionType.Unknown };
            Assert.IsFalse(action.IsValid(out _));
        }

        [Test]
        public void Non_positive_scale_is_invalid()
        {
            var action = SpatialAction.Marker(Vector3.zero);
            action.Scale = 0f;
            Assert.IsFalse(action.IsValid(out var error));
            StringAssert.Contains("scale", error);
        }

        [Test]
        public void Type_names_parse_case_insensitively()
        {
            Assert.AreEqual(SpatialActionType.Label, SpatialAction.ParseType("label"));
            Assert.AreEqual(SpatialActionType.Warning, SpatialAction.ParseType("WARNING"));
            Assert.AreEqual(SpatialActionType.Arrow, SpatialAction.ParseType("Arrow"));
            Assert.AreEqual(SpatialActionType.Highlight, SpatialAction.ParseType("highlight"));
            Assert.AreEqual(SpatialActionType.Clear, SpatialAction.ParseType("clear"));
        }

        [Test]
        public void Unrecognised_type_name_becomes_Unknown()
        {
            Assert.AreEqual(SpatialActionType.Unknown, SpatialAction.ParseType("explode"));
            Assert.AreEqual(SpatialActionType.Unknown, SpatialAction.ParseType(null));
            Assert.AreEqual(SpatialActionType.Unknown, SpatialAction.ParseType(""));
        }

        [Test]
        public void Space_defaults_to_target()
        {
            Assert.AreEqual(SpatialSpace.Target, SpatialAction.ParseSpace(null));
            Assert.AreEqual(SpatialSpace.Target, SpatialAction.ParseSpace("target"));
            Assert.AreEqual(SpatialSpace.Target, SpatialAction.ParseSpace("nonsense"));
            Assert.AreEqual(SpatialSpace.World, SpatialAction.ParseSpace("world"));
        }

        [Test]
        public void Hex_colours_parse()
        {
            Assert.IsTrue(SpatialAction.TryParseColor("#FF4136", out var colour));
            Assert.AreEqual(1f, colour.r, 0.01f);
            Assert.AreEqual(0.255f, colour.g, 0.01f);

            Assert.IsTrue(SpatialAction.TryParseColor("#FF4136FF", out _));
        }

        [Test]
        public void Named_colours_parse_too()
        {
            // Deliberately more lenient than the backend's validator: a model
            // that emits "red" should get red, not a silent fallback.
            Assert.IsTrue(SpatialAction.TryParseColor("red", out var red));
            Assert.AreEqual(Color.red, red);
        }

        [Test]
        public void Junk_colours_do_not_parse()
        {
            Assert.IsFalse(SpatialAction.TryParseColor("not-a-colour", out _));
            Assert.IsFalse(SpatialAction.TryParseColor("#GGGGGG", out _));
            Assert.IsFalse(SpatialAction.TryParseColor(null, out _));
            Assert.IsFalse(SpatialAction.TryParseColor("", out _));
        }

        [Test]
        public void WithColor_sets_the_has_colour_flag()
        {
            var action = SpatialAction.Marker(Vector3.zero);
            Assert.IsFalse(action.HasColor);

            action.WithColor(Color.red);
            Assert.IsTrue(action.HasColor);
            Assert.AreEqual(Color.red, action.Color);
        }
    }
}
