using System.Linq;
using NUnit.Framework;
using SpatialDebugger.Core;
using SpatialDebugger.Demo;
using UnityEngine;

namespace SpatialDebugger.Tests
{
    /// <summary>
    /// The on-device demo, which is the one path that must work on stage.
    /// Mirrors backend/tests/test_analyze.py so both copies of the scenarios
    /// are held to the same contract.
    /// </summary>
    public class DemoScenarioTests
    {
        [Test]
        public void Every_scenario_produces_valid_actions()
        {
            foreach (var scenario in DemoScenarios.Catalogue)
            {
                Assert.IsNotEmpty(scenario.Speech, scenario.Id + " has no speech");

                var actions = scenario.BuildActions();
                Assert.IsNotEmpty(actions, scenario.Id + " has no actions");

                foreach (var action in actions)
                {
                    Assert.IsTrue(action.IsValid(out var error),
                        scenario.Id + ": " + error + " (" + action + ")");
                }
            }
        }

        [Test]
        public void Scenario_ids_are_unique()
        {
            var ids = DemoScenarios.Catalogue.Select(s => s.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
        }

        [Test]
        public void The_default_scenario_is_the_scripted_demo()
        {
            var actions = DemoScenarios.Default.BuildActions();
            var types = actions.Select(a => a.Type).ToList();

            CollectionAssert.Contains(types, SpatialActionType.Warning);
            CollectionAssert.Contains(types, SpatialActionType.Arrow);
            CollectionAssert.Contains(types, SpatialActionType.Marker);
            CollectionAssert.Contains(types, SpatialActionType.Label);
        }

        [Test]
        public void The_demo_warning_says_the_scripted_line()
        {
            var warning = DemoScenarios.Default.BuildActions()
                .First(a => a.Type == SpatialActionType.Warning);

            Assert.AreEqual("Possible incorrect connection", warning.Text);
        }

        [Test]
        public void The_demo_arrow_points_at_the_selected_target()
        {
            var arrow = DemoScenarios.Default.BuildActions()
                .First(a => a.Type == SpatialActionType.Arrow);

            // The head lands on (or right next to) the target origin...
            Assert.Less(arrow.To.magnitude, 0.05f, "arrow does not land on the target");

            // ...and starts somewhere clearly away from it.
            Assert.Greater(arrow.From.magnitude, 0.05f, "arrow starts on top of the target");
        }

        [Test]
        public void Every_coordinate_stays_within_arms_reach()
        {
            foreach (var scenario in DemoScenarios.Catalogue)
            {
                foreach (var action in scenario.BuildActions())
                {
                    AssertWithinReach(action.Position, scenario.Id, "position");
                    AssertWithinReach(action.From, scenario.Id, "from");
                    AssertWithinReach(action.To, scenario.Id, "to");
                }
            }
        }

        private static void AssertWithinReach(Vector3 vector, string scenarioId, string field)
        {
            Assert.LessOrEqual(Mathf.Abs(vector.x), 0.3f, scenarioId + "." + field + ".x");
            Assert.LessOrEqual(Mathf.Abs(vector.y), 0.3f, scenarioId + "." + field + ".y");
            Assert.LessOrEqual(Mathf.Abs(vector.z), 0.3f, scenarioId + "." + field + ".z");
        }

        [Test]
        public void Every_action_defaults_to_target_space()
        {
            foreach (var scenario in DemoScenarios.Catalogue)
            {
                foreach (var action in scenario.BuildActions())
                {
                    Assert.AreEqual(SpatialSpace.Target, action.Space, scenario.Id);
                }
            }
        }

        [Test]
        public void Arrows_have_non_zero_length()
        {
            foreach (var scenario in DemoScenarios.Catalogue)
            {
                foreach (var arrow in scenario.BuildActions()
                             .Where(a => a.Type == SpatialActionType.Arrow))
                {
                    Assert.Greater((arrow.To - arrow.From).magnitude, 0.001f,
                        scenario.Id + " has a zero-length arrow");
                }
            }
        }

        [Test]
        public void Lookup_is_case_insensitive_and_tolerates_junk()
        {
            Assert.IsNotNull(DemoScenarios.Get("LED_NOT_WORKING"));
            Assert.IsNull(DemoScenarios.Get("does_not_exist"));
            Assert.IsNull(DemoScenarios.Get(null));
        }

        [Test]
        public void Cycling_wraps_in_both_directions()
        {
            Assert.IsNotNull(DemoScenarios.At(0));
            Assert.IsNotNull(DemoScenarios.At(DemoScenarios.Count));
            Assert.IsNotNull(DemoScenarios.At(-1));
            Assert.AreEqual(DemoScenarios.At(0).Id, DemoScenarios.At(DemoScenarios.Count).Id);
        }

        [Test]
        public void Responses_are_fresh_objects_not_shared_state()
        {
            var first = DemoScenarios.Respond(DemoScenarios.Default);
            first.Actions[0].Text = "mutated";

            var second = DemoScenarios.Respond(DemoScenarios.Default);
            Assert.AreNotEqual("mutated", second.Actions[0].Text);
        }

        [Test]
        public void Responses_are_marked_local_and_successful()
        {
            var response = DemoScenarios.Respond(DemoScenarios.Default);

            Assert.IsTrue(response.Success);
            Assert.IsTrue(response.IsLocal);
            Assert.AreEqual("demo", response.Provider);
            Assert.IsNotEmpty(response.Speech);
        }

        [Test]
        public void A_target_label_is_prefixed_onto_the_speech()
        {
            var response = DemoScenarios.Respond(DemoScenarios.Default, "LED anode");
            StringAssert.StartsWith("LED anode:", response.Speech);
        }

        [Test]
        public void A_null_scenario_falls_back_to_the_default()
        {
            var response = DemoScenarios.Respond(null);
            Assert.AreEqual(DemoScenarios.DefaultId, response.Scenario);
        }

        [Test]
        public void The_unity_catalogue_matches_the_backend_catalogue()
        {
            // These ids are duplicated in backend/app/scenarios.py by hand.
            // If one side gains a scenario, this test says so.
            var expected = new[]
            {
                "led_not_working", "missing_resistor", "gpio_mismatch", "power_rail",
                "ground_missing", "i2c_pullup", "floating_input", "reversed_polarity"
            };

            CollectionAssert.AreEquivalent(
                expected, DemoScenarios.Catalogue.Select(s => s.Id).ToArray());
        }
    }
}
