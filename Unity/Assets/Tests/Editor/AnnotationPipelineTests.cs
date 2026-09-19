using System.Linq;
using NUnit.Framework;
using SpatialDebugger.Annotations;
using SpatialDebugger.Core;
using SpatialDebugger.Demo;
using UnityEngine;

namespace SpatialDebugger.Tests
{
    /// <summary>
    /// End-to-end coverage of the part of the demo that actually has to work on
    /// stage: a response goes in, GameObjects come out, anchored to the target.
    /// </summary>
    /// <remarks>
    /// Runs in edit mode, so it exercises the real renderers and the real
    /// procedural geometry without needing a headset or even play mode. What it
    /// cannot check is how any of it looks.
    /// </remarks>
    public class AnnotationPipelineTests
    {
        private GameObject _root;
        private SpatialTarget _target;
        private SpatialActionDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestRoot");

            var targetGo = new GameObject("Target");
            targetGo.transform.SetParent(_root.transform, false);
            // Somewhere arbitrary, so "anchored to the target" is a real claim
            // rather than an accident of sitting at the origin.
            targetGo.transform.position = new Vector3(1.5f, 0.9f, -2.25f);
            targetGo.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
            _target = targetGo.AddComponent<SpatialTarget>();

            var dispatcherGo = new GameObject("Dispatcher");
            dispatcherGo.transform.SetParent(_root.transform, false);
            _dispatcher = dispatcherGo.AddComponent<SpatialActionDispatcher>();
            _dispatcher.Target = _target;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private Transform[] Annotations() =>
            _target.AnnotationRoot.Cast<Transform>().Where(t => t.name.StartsWith("SD_")).ToArray();

        // -- the headline demo ---------------------------------------------

        [Test]
        public void The_demo_response_renders_every_action()
        {
            var response = DemoScenarios.Respond(DemoScenarios.Default);
            var rendered = _dispatcher.Dispatch(response);

            Assert.AreEqual(response.Actions.Count, rendered, "not every action rendered");
            Assert.AreEqual(response.Actions.Count, Annotations().Length);
        }

        [Test]
        public void Each_action_type_gets_its_own_renderer()
        {
            _dispatcher.Dispatch(DemoScenarios.Respond(DemoScenarios.Default));

            var root = _target.AnnotationRoot;
            Assert.IsNotNull(root.GetComponentInChildren<MarkerRenderer>(), "no marker");
            Assert.IsNotNull(root.GetComponentInChildren<LabelRenderer>(), "no label");
            Assert.IsNotNull(root.GetComponentInChildren<WarningRenderer>(), "no warning");
            Assert.IsNotNull(root.GetComponentInChildren<ArrowRenderer>(), "no arrow");
            Assert.IsNotNull(root.GetComponentInChildren<HighlightRenderer>(), "no highlight");
        }

        [Test]
        public void Annotations_are_parented_to_the_target()
        {
            _dispatcher.Dispatch(DemoScenarios.Respond(DemoScenarios.Default));

            foreach (var annotation in Annotations())
            {
                Assert.AreSame(_target.AnnotationRoot, annotation.parent);
            }
        }

        [Test]
        public void Target_space_coordinates_become_local_coordinates()
        {
            // The whole design rests on this: the backend sends [0, 0.15, 0]
            // meaning "15cm above whatever you pointed at", and it must land
            // there even though the target is rotated and metres from the origin.
            _dispatcher.Dispatch(SpatialAction.Warning(new Vector3(0f, 0.15f, 0f), "here"));

            var warning = Annotations().Single();
            Assert.AreEqual(new Vector3(0f, 0.15f, 0f), warning.localPosition);

            var expectedWorld = _target.transform.TransformPoint(new Vector3(0f, 0.15f, 0f));
            Assert.Less(Vector3.Distance(expectedWorld, warning.position), 0.0001f);
        }

        [Test]
        public void An_arrow_is_positioned_at_its_tail()
        {
            var from = new Vector3(0.1f, 0.25f, 0f);
            _dispatcher.Dispatch(SpatialAction.Arrow(from, new Vector3(0f, 0.02f, 0f), "Connect here"));

            var arrow = Annotations().Single();
            Assert.AreEqual(from, arrow.localPosition);
            Assert.IsNotNull(arrow.Find("Shaft"), "arrow has no shaft");
            Assert.IsNotNull(arrow.Find("Head"), "arrow has no head");
        }

        [Test]
        public void Renderers_build_real_geometry()
        {
            _dispatcher.Dispatch(DemoScenarios.Respond(DemoScenarios.Default));

            foreach (var annotation in Annotations())
            {
                var renderers = annotation.GetComponentsInChildren<MeshRenderer>();
                Assert.IsNotEmpty(renderers, annotation.name + " drew nothing");

                foreach (var renderer in renderers)
                {
                    Assert.IsNotNull(renderer.sharedMaterial,
                        annotation.name + "/" + renderer.name + " has no material");
                }
            }
        }

        [Test]
        public void Annotations_carry_no_colliders()
        {
            // Annotations are decoration. A collider on one would block the
            // targeting raycast and make the board impossible to re-select.
            _dispatcher.Dispatch(DemoScenarios.Respond(DemoScenarios.Default));

            foreach (var annotation in Annotations())
            {
                Assert.IsEmpty(annotation.GetComponentsInChildren<Collider>(),
                    annotation.name + " has a collider");
            }
        }

        [Test]
        public void Text_renders_through_some_backend()
        {
            _dispatcher.Dispatch(SpatialAction.Label(Vector3.zero, "GPIO 12"));

            var label = Annotations().Single();
            var text = label.GetComponentInChildren<SpatialLabel>();

            Assert.IsNotNull(text, "the label produced no text at all");
            Assert.AreEqual("GPIO 12", text.Text);
        }

        // -- clearing --------------------------------------------------------

        [Test]
        public void Clear_removes_everything()
        {
            _dispatcher.Dispatch(DemoScenarios.Respond(DemoScenarios.Default));
            Assert.IsNotEmpty(Annotations());

            _dispatcher.ClearAll();
            Assert.IsEmpty(Annotations());
            Assert.AreEqual(0, _dispatcher.LiveCount);
        }

        [Test]
        public void A_clear_action_clears()
        {
            _dispatcher.Dispatch(DemoScenarios.Respond(DemoScenarios.Default));
            _dispatcher.Dispatch(SpatialAction.Clear());

            Assert.IsEmpty(Annotations());
        }

        [Test]
        public void Re_running_an_analysis_does_not_accumulate()
        {
            for (var i = 0; i < 3; i++)
            {
                _dispatcher.ClearAll();
                _dispatcher.Dispatch(DemoScenarios.Respond(DemoScenarios.Default));
            }

            Assert.AreEqual(DemoScenarios.Default.BuildActions().Count, Annotations().Length);
        }

        // -- failure handling ------------------------------------------------

        [Test]
        public void An_invalid_action_is_rejected_not_rendered()
        {
            SpatialAction rejected = null;
            _dispatcher.ActionRejected += (action, _) => rejected = action;

            // A label with no text.
            var bad = new SpatialAction
            {
                Type = SpatialActionType.Label,
                Position = Vector3.zero,
                HasPosition = true
            };

            Assert.IsFalse(_dispatcher.Dispatch(bad));
            Assert.AreSame(bad, rejected);
            Assert.IsEmpty(Annotations());
        }

        [Test]
        public void A_target_space_action_is_rejected_when_no_target_is_selected()
        {
            _dispatcher.Target = null;

            Assert.IsFalse(_dispatcher.Dispatch(SpatialAction.Marker(Vector3.zero)));
        }

        [Test]
        public void A_null_action_is_rejected_without_throwing()
        {
            Assert.IsFalse(_dispatcher.Dispatch((SpatialAction)null));
        }

        [Test]
        public void World_space_actions_do_not_need_a_target()
        {
            _dispatcher.Target = null;

            var action = SpatialAction.Marker(new Vector3(1f, 2f, 3f));
            action.Space = SpatialSpace.World;

            Assert.IsTrue(_dispatcher.Dispatch(action));
            Assert.AreEqual(1, _dispatcher.LiveCount);
        }

        [Test]
        public void Dispatching_a_failed_response_renders_nothing()
        {
            var failure = AI.AIResponse.Failed(AI.AIFailure.ConnectionFailed, "no route to host");

            Assert.AreEqual(0, _dispatcher.Dispatch(failure));
            Assert.IsEmpty(Annotations());
        }

        [Test]
        public void Every_scenario_renders_without_error()
        {
            foreach (var scenario in DemoScenarios.Catalogue)
            {
                _dispatcher.ClearAll();

                var response = DemoScenarios.Respond(scenario);
                var rendered = _dispatcher.Dispatch(response);

                Assert.AreEqual(response.Actions.Count, rendered, scenario.Id + " lost actions");
            }
        }

        [Test]
        public void A_backend_payload_renders_end_to_end()
        {
            // Parse a real response body, then render it. This is the whole
            // Unity-side path in one test.
            const string body = @"{
              ""speech"": ""Check GPIO 12."",
              ""actions"": [
                {""type"":""marker"",""space"":""target"",""position"":[0,0,0]},
                {""type"":""warning"",""space"":""target"",""position"":[0,0.15,0],""text"":""Possible incorrect connection""},
                {""type"":""arrow"",""space"":""target"",""from"":[0.1,0.25,0],""to"":[0,0.02,0],""text"":""Connect here""}
              ]}";

            Assert.IsTrue(AI.AIResponse.TryParse(body, out var response, out var error), error);
            Assert.AreEqual(3, _dispatcher.Dispatch(response));
            Assert.AreEqual(3, Annotations().Length);
        }
    }
}
