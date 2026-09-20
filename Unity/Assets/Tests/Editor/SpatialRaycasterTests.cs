using NUnit.Framework;
using SpatialDebugger.Core;
using SpatialDebugger.Interaction;
using UnityEngine;

namespace SpatialDebugger.Tests
{
    /// <summary>
    /// The placement tier hierarchy: environment depth, then physics, then a
    /// fixed-distance projection.
    /// </summary>
    /// <remarks>
    /// These tests do NOT verify Quest environment depth. Depth is device-only
    /// -- <c>EnvironmentRaycastManager.IsSupported</c> is false off-headset --
    /// so the real sensor is stubbed out at the probe seam. What is verified is
    /// the part that lives in this repo and can silently regress: which tier
    /// wins, and that the fallback still catches everything. Whether the sensor
    /// returns a sane point is a physical test, not this.
    /// </remarks>
    public class SpatialRaycasterTests
    {
        /// <summary>Stands in for the Meta depth sensor and MRUK.</summary>
        private class StubProbe : MrukSurfaceProbe
        {
            public SpatialHit? Result;

            public override bool IsAvailable => Result.HasValue;

            public override bool TryRaycast(Ray ray, float maxDistance, out SpatialHit hit)
            {
                if (Result.HasValue)
                {
                    hit = Result.Value;
                    return true;
                }

                hit = SpatialHit.None;
                return false;
            }
        }

        // Far from anything the loaded scene might contain, so a stray collider
        // cannot quietly turn a "nothing was hit" test green for the wrong reason.
        private static readonly Vector3 Origin = new Vector3(1000f, 1000f, 1000f);

        private GameObject _root;
        private SpatialRaycaster _raycaster;
        private StubProbe _probe;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("RaycasterTestRoot");

            var go = new GameObject("Raycaster");
            go.transform.SetParent(_root.transform, false);
            _raycaster = go.AddComponent<SpatialRaycaster>();

            _probe = new StubProbe();
            _raycaster.SurfaceProbe = _probe;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private Ray ForwardRay() => new Ray(Origin, Vector3.forward);

        /// <summary>A collider one metre down the ray, on the default layer.</summary>
        private GameObject AddPhysicsSurface(float distance)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "PhysicsSurface";
            wall.transform.SetParent(_root.transform, false);
            wall.transform.position = Origin + Vector3.forward * (distance + 0.5f);
            wall.transform.localScale = new Vector3(4f, 4f, 1f);

            // Edit mode does not run the physics loop; the raycast still needs
            // the collider's transform pushed into the physics world.
            Physics.SyncTransforms();
            return wall;
        }

        private SpatialHit DepthHitAt(float distance)
        {
            return new SpatialHit
            {
                IsValid = true,
                Point = Origin + Vector3.forward * distance,
                Normal = Vector3.back,
                Distance = distance,
                NormalConfidence = 0.9f,
                Source = SpatialHit.HitSource.EnvironmentDepth
            };
        }

        [Test]
        public void DepthHitWinsOverPhysics()
        {
            AddPhysicsSurface(1f);
            _probe.Result = DepthHitAt(0.72f);

            var hit = _raycaster.Raycast(ForwardRay());

            Assert.That(hit.Source, Is.EqualTo(SpatialHit.HitSource.EnvironmentDepth));
            Assert.That(hit.Distance, Is.EqualTo(0.72f).Within(1e-4f));
            Assert.That(hit.Point.z, Is.EqualTo(Origin.z + 0.72f).Within(1e-3f));
        }

        [Test]
        public void PhysicsWinsWhenDepthDoesNotAnswer()
        {
            AddPhysicsSurface(1f);
            _probe.Result = null;

            var hit = _raycaster.Raycast(ForwardRay());

            Assert.That(hit.Source, Is.EqualTo(SpatialHit.HitSource.Physics));
            Assert.That(hit.Distance, Is.EqualTo(1f).Within(0.01f));
            Assert.That(hit.Collider, Is.Not.Null);
        }

        [Test]
        public void FallsBackToFixedDistanceWhenNothingIsHit()
        {
            _probe.Result = null;

            var hit = _raycaster.Raycast(ForwardRay());

            Assert.That(hit.IsValid, Is.True, "placement must never simply stop working");
            Assert.That(hit.Source, Is.EqualTo(SpatialHit.HitSource.Projected));
            Assert.That(hit.Distance, Is.EqualTo(_raycaster.FallbackDistance).Within(1e-4f));
            var expected = Origin + Vector3.forward * _raycaster.FallbackDistance;
            Assert.That(Vector3.Distance(hit.Point, expected), Is.LessThan(1e-3f));
        }

        [Test]
        public void FallbackNormalIsWorldUpNotTheRay()
        {
            _probe.Result = null;

            var hit = _raycaster.Raycast(ForwardRay());

            // Annotations authored "above the target" must stand up in the room.
            Assert.That(Vector3.Distance(hit.Normal, Vector3.up), Is.LessThan(1e-4f));
        }

        [Test]
        public void FallbackNormalisesTheRayDirection()
        {
            _probe.Result = null;

            // A direction of length 3 must still land 1.5 m out, not 4.5 m.
            var hit = _raycaster.Raycast(new Ray(Origin, Vector3.forward * 3f));

            Assert.That(hit.Point.z,
                Is.EqualTo(Origin.z + _raycaster.FallbackDistance).Within(1e-3f));
        }

        [Test]
        public void ADepthHitIsReportedAsARealSurface()
        {
            var controllerGo = new GameObject("TargetController");
            controllerGo.transform.SetParent(_root.transform, false);
            var controller = controllerGo.AddComponent<SpatialTargetController>();

            controller.PlaceTarget(DepthHitAt(0.72f));

            Assert.That(controller.Target, Is.Not.Null);
            Assert.That(controller.Target.SelectedBy, Is.EqualTo(SpatialTarget.Origin.SceneSurface));
            Assert.That(controller.Target.transform.position.z,
                Is.EqualTo(Origin.z + 0.72f).Within(1e-3f));

            if (controller.Target != null) Object.DestroyImmediate(controller.Target.gameObject);
        }

        [Test]
        public void FallbackIsStillReportedAsAProjectedPoint()
        {
            var controllerGo = new GameObject("TargetController");
            controllerGo.transform.SetParent(_root.transform, false);
            var controller = controllerGo.AddComponent<SpatialTargetController>();

            controller.PlaceTarget(SpatialRaycaster.Fallback(ForwardRay(), 1.5f));

            Assert.That(controller.Target.SelectedBy, Is.EqualTo(SpatialTarget.Origin.Fallback));

            if (controller.Target != null) Object.DestroyImmediate(controller.Target.gameObject);
        }
    }
}
