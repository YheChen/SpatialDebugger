using NUnit.Framework;
using SpatialDebugger.Core;
using UnityEngine;

namespace SpatialDebugger.Tests
{
    /// <summary>
    /// How an annotation attaches to a real surface.
    /// </summary>
    /// <remarks>
    /// The rule is a pure function of the depth hit, so all of it is testable
    /// without a headset. What is NOT testable here is whether the sensor's
    /// normals are correct in a real room -- that is a physical result. A
    /// verified session returned confidence 1.00 for five hits and 0.00 for a
    /// sixth, which is why the low-confidence path matters and is covered.
    /// </remarks>
    public class SurfaceOrientationTests
    {
        private static readonly Vector3 Hit = new Vector3(1.2f, 0.95f, -0.4f);

        /// <summary>A head roughly where a standing user's would be.</summary>
        private static readonly Vector3 Viewer = new Vector3(1.2f, 1.6f, -1.4f);

        private const float Confident = 1f;

        private static void AssertDirection(Vector3 actual, Vector3 expected, string what)
        {
            Assert.That(Vector3.Angle(actual, expected), Is.LessThan(1f),
                what + ": expected " + expected.ToString("F2") + " got " + actual.ToString("F2"));
        }

        // -- classification -------------------------------------------------

        [Test]
        public void An_upward_normal_is_a_horizontal_surface()
        {
            Assert.That(SurfaceOrientation.Classify(Vector3.up, Confident),
                Is.EqualTo(SurfaceOrientation.Kind.Horizontal));
        }

        [Test]
        public void A_sideways_normal_is_a_vertical_surface()
        {
            Assert.That(SurfaceOrientation.Classify(Vector3.forward, Confident),
                Is.EqualTo(SurfaceOrientation.Kind.Vertical));
            Assert.That(SurfaceOrientation.Classify(Vector3.right, Confident),
                Is.EqualTo(SurfaceOrientation.Kind.Vertical));
        }

        [Test]
        public void A_forty_five_degree_normal_is_an_angled_surface()
        {
            var angled = (Vector3.up + Vector3.forward).normalized;

            Assert.That(SurfaceOrientation.Classify(angled, Confident),
                Is.EqualTo(SurfaceOrientation.Kind.Angled));
        }

        [Test]
        public void A_ceiling_classifies_the_same_as_a_floor()
        {
            // Classification describes the surface, not which face of it the
            // sensor happened to report.
            Assert.That(SurfaceOrientation.Classify(Vector3.down, Confident),
                Is.EqualTo(SurfaceOrientation.Kind.Horizontal));
        }

        [Test]
        public void A_low_confidence_normal_is_unknown()
        {
            Assert.That(SurfaceOrientation.Classify(Vector3.forward, 0f),
                Is.EqualTo(SurfaceOrientation.Kind.Unknown));
            Assert.That(
                SurfaceOrientation.Classify(
                    Vector3.forward, SurfaceOrientation.MinimumNormalConfidence - 0.01f),
                Is.EqualTo(SurfaceOrientation.Kind.Unknown));
        }

        // -- lift axis ------------------------------------------------------

        [Test]
        public void On_a_desk_the_label_lifts_along_the_normal_exactly_as_before()
        {
            AssertDirection(SurfaceOrientation.LiftAxis(Vector3.up), Vector3.up, "desk lift");
        }

        [Test]
        public void On_a_wall_the_label_lifts_up_the_wall_face_not_out_of_it()
        {
            // Straight out of the wall would point end-on at a viewer standing
            // in front of it, and the leader line would vanish.
            var lift = SurfaceOrientation.LiftAxis(Vector3.forward);

            AssertDirection(lift, Vector3.up, "wall lift");
            Assert.That(Mathf.Abs(Vector3.Dot(lift, Vector3.forward)), Is.LessThan(0.01f),
                "the lift must lie in the wall's plane");
        }

        [Test]
        public void On_a_ceiling_the_label_hangs_downward()
        {
            AssertDirection(SurfaceOrientation.LiftAxis(Vector3.down), Vector3.down, "ceiling lift");
        }

        [Test]
        public void On_an_angled_surface_the_lift_runs_up_the_slope()
        {
            var normal = (Vector3.up + Vector3.forward).normalized;
            var lift = SurfaceOrientation.LiftAxis(normal);

            Assert.That(Mathf.Abs(Vector3.Dot(lift, normal)), Is.LessThan(0.01f),
                "the lift must be perpendicular to the normal");
            Assert.That(lift.y, Is.GreaterThan(0f), "and should climb, not descend");
            AssertDirection(lift, (Vector3.up - Vector3.forward).normalized, "slope lift");
        }

        // -- normal direction -----------------------------------------------

        [Test]
        public void A_normal_pointing_away_from_the_viewer_is_flipped()
        {
            // Otherwise the marker is stood up inside the physical object.
            var inward = Vector3.forward;
            var towardViewer = Vector3.back;

            AssertDirection(SurfaceOrientation.OutwardNormal(inward, towardViewer),
                Vector3.back, "flipped normal");
        }

        [Test]
        public void A_normal_already_facing_the_viewer_is_left_alone()
        {
            AssertDirection(SurfaceOrientation.OutwardNormal(Vector3.up, Vector3.up),
                Vector3.up, "unflipped normal");
        }

        [Test]
        public void With_no_viewer_the_normal_is_taken_as_given()
        {
            AssertDirection(SurfaceOrientation.OutwardNormal(Vector3.forward, Vector3.zero),
                Vector3.forward, "no-viewer normal");
        }

        // -- the whole attachment --------------------------------------------

        [Test]
        public void A_desk_hit_keeps_the_marker_upright_and_the_label_above_it()
        {
            var attachment = SurfaceOrientation.For(Hit, Vector3.up, Confident, Viewer);

            Assert.That(attachment.Oriented, Is.True);
            Assert.That(attachment.Surface, Is.EqualTo(SurfaceOrientation.Kind.Horizontal));
            AssertDirection(attachment.MarkerUp, Vector3.up, "desk marker");
            AssertDirection(attachment.LabelUp, Vector3.up, "desk label");
        }

        [Test]
        public void A_wall_hit_stands_the_marker_out_of_the_wall()
        {
            // Viewer is at -Z relative to the hit, so the outward normal is -Z.
            var attachment = SurfaceOrientation.For(
                Hit, Vector3.forward, Confident, Hit + Vector3.back * 2f);

            Assert.That(attachment.Surface, Is.EqualTo(SurfaceOrientation.Kind.Vertical));
            AssertDirection(attachment.MarkerUp, Vector3.back, "wall marker stands out of the wall");
            AssertDirection(attachment.LabelUp, Vector3.up, "wall label climbs the wall");
        }

        [Test]
        public void A_low_confidence_hit_falls_back_to_the_known_good_orientation()
        {
            var attachment = SurfaceOrientation.For(Hit, Vector3.forward, 0f, Viewer);

            Assert.That(attachment.Oriented, Is.False);
            Assert.That(attachment.Surface, Is.EqualTo(SurfaceOrientation.Kind.Unknown));
            AssertDirection(attachment.MarkerUp, Vector3.up, "fallback marker");
            AssertDirection(attachment.LabelUp, Vector3.up, "fallback label");
        }

        [Test]
        public void A_low_confidence_hit_still_uses_the_measured_depth_position_untouched()
        {
            // The normal being unreliable says nothing about the distance.
            var attachment = SurfaceOrientation.For(Hit, Vector3.forward, 0f, Viewer);

            Assert.That(attachment.Anchor, Is.EqualTo(Hit));
        }

        [Test]
        public void Orientation_never_moves_the_anchor_by_more_than_the_standoff()
        {
            var normals = new[]
            {
                Vector3.up, Vector3.down, Vector3.forward, Vector3.right,
                (Vector3.up + Vector3.forward).normalized,
                (Vector3.up + Vector3.right + Vector3.forward).normalized
            };

            foreach (var normal in normals)
            {
                var attachment = SurfaceOrientation.For(Hit, normal, Confident, Viewer);

                Assert.That(Vector3.Distance(attachment.Anchor, Hit),
                    Is.EqualTo(SurfaceOrientation.SurfaceOffset).Within(1e-5f),
                    "normal " + normal.ToString("F2") + " must not move the depth point");
            }

            Assert.That(SurfaceOrientation.SurfaceOffset, Is.LessThanOrEqualTo(0.01f),
                "the standoff is an anti-coplanarity nudge, not a placement offset");
        }

        [Test]
        public void The_standoff_pushes_out_of_the_surface_never_into_it()
        {
            var attachment = SurfaceOrientation.For(
                Hit, Vector3.forward, Confident, Hit + Vector3.back * 2f);

            // Viewer is at -Z, so the anchor must have moved toward -Z.
            Assert.That(attachment.Anchor.z, Is.LessThan(Hit.z));
        }

        // -- rotation --------------------------------------------------------

        [Test]
        public void The_rotation_puts_local_up_on_the_requested_axis()
        {
            foreach (var axis in new[] { Vector3.up, Vector3.down, Vector3.forward, Vector3.right,
                                         (Vector3.up + Vector3.right).normalized })
            {
                var rotated = SurfaceOrientation.Rotation(axis) * Vector3.up;
                AssertDirection(rotated, axis.normalized, "rotation for " + axis.ToString("F2"));
            }
        }

        [Test]
        public void A_zero_axis_rotates_nothing()
        {
            Assert.That(SurfaceOrientation.Rotation(Vector3.zero), Is.EqualTo(Quaternion.identity));
        }

        // -- the one link between the rule and the scene ----------------------

        [Test]
        public void The_dispatcher_tips_an_annotation_onto_the_surface()
        {
            var go = new GameObject("Dispatcher");
            try
            {
                var dispatcher = go.AddComponent<Annotations.SpatialActionDispatcher>();

                var action = SpatialAction.Marker(Hit);
                action.Space = SpatialSpace.World;
                action.UpAxis = Vector3.back;
                action.HasUpAxis = true;

                var renderer = dispatcher.DispatchTracked(action);
                Assert.That(renderer, Is.Not.Null);

                // Renderers build their stalk, ring and leader along local +Y.
                AssertDirection(renderer.transform.up, Vector3.back, "host up axis");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void An_annotation_with_no_up_axis_is_left_upright()
        {
            var go = new GameObject("Dispatcher");
            try
            {
                var dispatcher = go.AddComponent<Annotations.SpatialActionDispatcher>();

                var action = SpatialAction.Marker(Hit);
                action.Space = SpatialSpace.World;

                var renderer = dispatcher.DispatchTracked(action);

                Assert.That(renderer.transform.localRotation,
                    Is.EqualTo(Quaternion.identity),
                    "no normal must mean exactly the old behaviour");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
