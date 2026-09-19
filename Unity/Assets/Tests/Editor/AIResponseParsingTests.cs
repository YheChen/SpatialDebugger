using NUnit.Framework;
using SpatialDebugger.AI;
using SpatialDebugger.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpatialDebugger.Tests
{
    /// <summary>
    /// Parsing of real backend payloads, and of every malformed shape a
    /// backend, a proxy or a captive portal might hand back instead.
    /// </summary>
    /// <remarks>
    /// This is the code most likely to meet something unexpected on demo day,
    /// and the code least able to be tested on device.
    /// </remarks>
    public class AIResponseParsingTests
    {
        /// <summary>A verbatim POST /analyze body from the FastAPI service.</summary>
        private const string RealPayload = @"{
          ""speech"": ""The LED's anode looks like it lands on GPIO 12."",
          ""actions"": [
            {""type"":""marker"",""space"":""target"",""position"":[0.0,0.0,0.0],""color"":""#FF4136"",""scale"":1.0,""duration"":0.0},
            {""type"":""label"",""space"":""target"",""position"":[0.0,0.06,0.0],""text"":""GPIO 12"",""color"":""#2EC4F3"",""scale"":1.0,""duration"":0.0},
            {""type"":""warning"",""space"":""target"",""position"":[0.0,0.15,0.0],""text"":""Possible incorrect connection"",""color"":""#FF4136"",""scale"":1.0,""duration"":0.0},
            {""type"":""arrow"",""space"":""target"",""from"":[0.1,0.25,0.0],""to"":[0.0,0.02,0.0],""text"":""Connect here"",""color"":""#FF9F1C"",""scale"":1.0,""duration"":0.0},
            {""type"":""highlight"",""space"":""target"",""position"":[0.0,0.0,0.0],""color"":""#FF4136"",""scale"":1.0,""radius"":0.05,""duration"":0.0}
          ],
          ""scenario"": ""led_not_working"",
          ""provider"": ""mock"",
          ""confidence"": 0.82
        }";

        [Test]
        public void Parses_a_real_backend_payload()
        {
            Assert.IsTrue(AIResponse.TryParse(RealPayload, out var response, out var error), error);

            Assert.AreEqual(5, response.Actions.Count);
            Assert.AreEqual("led_not_working", response.Scenario);
            Assert.AreEqual("mock", response.Provider);
            Assert.AreEqual(0.82f, response.Confidence, 0.001f);
            StringAssert.Contains("GPIO 12", response.Speech);
        }

        [Test]
        public void Reads_the_arrow_endpoints_under_their_wire_names()
        {
            AIResponse.TryParse(RealPayload, out var response, out _);
            var arrow = response.Actions.Find(a => a.Type == SpatialActionType.Arrow);

            Assert.IsNotNull(arrow);
            Assert.IsTrue(arrow.HasFrom);
            Assert.IsTrue(arrow.HasTo);
            Assert.AreEqual(new Vector3(0.1f, 0.25f, 0f), arrow.From);
            Assert.AreEqual(new Vector3(0f, 0.02f, 0f), arrow.To);
        }

        [Test]
        public void Reads_colour_radius_and_space()
        {
            AIResponse.TryParse(RealPayload, out var response, out _);
            var highlight = response.Actions.Find(a => a.Type == SpatialActionType.Highlight);

            Assert.IsNotNull(highlight);
            Assert.IsTrue(highlight.HasColor);
            Assert.IsTrue(highlight.HasRadius);
            Assert.AreEqual(0.05f, highlight.Radius, 0.0001f);
            Assert.AreEqual(SpatialSpace.Target, highlight.Space);
        }

        [Test]
        public void Distinguishes_an_absent_field_from_a_zero_vector()
        {
            AIResponse.TryParse(
                @"{""speech"":""x"",""actions"":[{""type"":""marker"",""position"":[0,0,0]}]}",
                out var response, out _);

            var marker = response.Actions[0];
            Assert.IsTrue(marker.HasPosition, "position [0,0,0] must count as present");
            Assert.IsFalse(marker.HasFrom, "absent 'from' must not read as Vector3.zero");
            Assert.IsFalse(marker.HasColor);
            Assert.IsFalse(marker.HasRadius);
        }

        [Test]
        public void Drops_a_malformed_action_but_keeps_the_rest()
        {
            LogAssert.ignoreFailingMessages = true;

            var body = @"{""speech"":""x"",""actions"":[
                {""type"":""marker"",""position"":[0,0,0]},
                {""type"":""label"",""position"":[0,0,0]},
                {""type"":""nonsense"",""position"":[0,0,0]},
                {""type"":""arrow"",""from"":[0,1,0]}
            ]}";

            Assert.IsTrue(AIResponse.TryParse(body, out var response, out _));

            // The label has no text, "nonsense" is not a type, and the arrow has
            // no 'to'. Only the marker survives.
            Assert.AreEqual(1, response.Actions.Count);
            Assert.AreEqual(SpatialActionType.Marker, response.Actions[0].Type);
        }

        [Test]
        public void Rejects_a_non_numeric_vector_component()
        {
            LogAssert.ignoreFailingMessages = true;

            AIResponse.TryParse(
                @"{""speech"":""x"",""actions"":[{""type"":""marker"",""position"":[""a"",0,0]}]}",
                out var response, out _);

            Assert.AreEqual(0, response.Actions.Count);
        }

        [Test]
        public void Rejects_a_short_vector()
        {
            LogAssert.ignoreFailingMessages = true;

            AIResponse.TryParse(
                @"{""speech"":""x"",""actions"":[{""type"":""marker"",""position"":[0,0]}]}",
                out var response, out _);

            Assert.AreEqual(0, response.Actions.Count);
        }

        [Test]
        public void Rejects_non_finite_coordinates()
        {
            LogAssert.ignoreFailingMessages = true;

            AIResponse.TryParse(
                @"{""speech"":""x"",""actions"":[{""type"":""marker"",""position"":[NaN,0,0]}]}",
                out var response, out _);

            Assert.IsTrue(response == null || response.Actions.Count == 0);
        }

        [Test]
        public void Rejects_an_empty_body()
        {
            Assert.IsFalse(AIResponse.TryParse("", out _, out var error));
            StringAssert.Contains("empty", error.ToLowerInvariant());
        }

        [Test]
        public void Rejects_html_or_other_non_json()
        {
            Assert.IsFalse(AIResponse.TryParse("<html>502 Bad Gateway</html>", out _, out var error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void Rejects_a_bare_array()
        {
            Assert.IsFalse(AIResponse.TryParse("[1,2,3]", out _, out var error));
            StringAssert.Contains("object", error);
        }

        [Test]
        public void Surfaces_the_backend_error_envelope()
        {
            Assert.IsFalse(AIResponse.TryParse(
                @"{""error"":""invalid_request"",""detail"":""question required""}",
                out _, out var error));

            StringAssert.Contains("invalid_request", error);
        }

        [Test]
        public void Tolerates_a_response_with_no_actions()
        {
            Assert.IsTrue(AIResponse.TryParse(@"{""speech"":""nothing to see""}",
                out var response, out _));

            Assert.IsNotNull(response.Actions);
            Assert.AreEqual(0, response.Actions.Count);
        }

        [Test]
        public void Parses_a_clear_action()
        {
            Assert.IsTrue(AIResponse.TryParse(
                @"{""speech"":""cleared"",""actions"":[{""type"":""clear""}]}",
                out var response, out _));

            Assert.AreEqual(1, response.Actions.Count);
            Assert.AreEqual(SpatialActionType.Clear, response.Actions[0].Type);
        }

        [Test]
        public void Parses_world_space()
        {
            AIResponse.TryParse(
                @"{""speech"":""x"",""actions"":[{""type"":""marker"",""space"":""world"",""position"":[1,2,3]}]}",
                out var response, out _);

            Assert.AreEqual(SpatialSpace.World, response.Actions[0].Space);
            Assert.AreEqual(new Vector3(1, 2, 3), response.Actions[0].Position);
        }

        [Test]
        public void Failed_responses_carry_a_typed_reason()
        {
            var failure = AIResponse.Failed(AIFailure.Timeout, "took too long");

            Assert.IsFalse(failure.Success);
            Assert.AreEqual(AIFailure.Timeout, failure.Failure);
            Assert.AreEqual("took too long", failure.Error);
            Assert.IsEmpty(failure.Actions);
        }
    }
}
