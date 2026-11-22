using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public sealed class WaypointProjectionTests
    {
        [TestCase(1920, 1080)]
        [TestCase(3840, 2160)]
        [TestCase(1440, 1080)]
        public void EdgeArrowStaysLatchedUntilTheSamePointIsBackInside(int width, int height)
        {
            var gameObject = new GameObject("Waypoint test camera");
            var texture = new RenderTexture(width, height, 0);
            try
            {
                var camera = gameObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.targetTexture = texture;
                float scale = height / 1080f;
                var bounds = new Rect(100 * scale, 100 * scale, width - 200 * scale, height - 200 * scale);
                Vector3 worldPoint = camera.ScreenToWorldPoint(new Vector3(bounds.xMax + 2 * scale, bounds.center.y, 10));
                bool edge = false;
                Vector2 history = default;
                Vector2 Project(float xOffset, out Vector2 bearing)
                {
                    Aim(xOffset);
                    return HudMarkerProjection.Project(camera, worldPoint, bounds, 24 * scale, ref edge, ref history, out bearing);
                }
                void Aim(float xOffset)
                {
                    // Translate along the camera's right axis to move the same world point by xOffset on screen.
                    camera.transform.position = Vector3.zero;
                    var desired = camera.ScreenToWorldPoint(new Vector3(bounds.xMax + xOffset * scale, bounds.center.y, 10));
                    camera.transform.position = worldPoint - desired;
                }

                Project(-4, out _);
                Assert.That(edge, Is.False);
                Vector2 outside = Project(3, out var bearing);
                Assert.That(edge, Is.True);
                Assert.That(outside.x, Is.EqualTo(bounds.xMax).Within(.1f));
                Assert.That(bearing.x, Is.GreaterThan(.99f));
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(Project(-8, out _).x, Is.EqualTo(bounds.xMax).Within(.1f));
                    Assert.That(edge, Is.True);
                    Project(5, out _);
                    Assert.That(edge, Is.True);
                }
                Project(-26, out _);
                Assert.That(edge, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(texture);
            }
        }

        [TestCase(0f)]
        [TestCase(-.95f)]
        public void AlmostDirectlyBehindKeepsThePreviousTurnSide(float height)
        {
            var gameObject = new GameObject("Waypoint test camera");
            var texture = new RenderTexture(1920, 1080, 0);
            try
            {
                var camera = gameObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.targetTexture = texture;
                var bounds = new Rect(64, 64, 1792, 952);
                var target = new Vector3(0, height, -5);
                bool edge = false;
                Vector2 history = default;
                camera.transform.rotation = Quaternion.Euler(0, 10, 0);
                Vector2 first = HudMarkerProjection.Project(camera, target, bounds, 24, ref edge, ref history, out var bearing);
                Assert.That(edge, Is.True);
                Assert.That(bearing.x, Is.GreaterThan(.99f));
                Assert.That(first.x, Is.EqualTo(bounds.xMax).Within(.1f));

                camera.transform.rotation = Quaternion.Euler(0, -1, 0);
                Vector2 rear = HudMarkerProjection.Project(camera, target, bounds, 24, ref edge, ref history, out bearing);
                Assert.That(edge, Is.True);
                Assert.That(rear.x, Is.EqualTo(first.x).Within(.1f));
                Assert.That(bearing.x, Is.GreaterThan(.99f));

                camera.transform.rotation = Quaternion.Euler(0, -10, 0);
                Vector2 left = HudMarkerProjection.Project(camera, target, bounds, 24, ref edge, ref history, out bearing);
                Assert.That(bearing.x, Is.LessThan(-.99f));
                Assert.That(bearing.y, Is.Zero, "Even a lower point behind the player needs a horizontal turn.");
                Assert.That(left.x, Is.EqualTo(bounds.xMin).Within(.1f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void AvoidanceCanNudgeNearAnOverlayAndRefusesToJumpToTheEdge()
        {
            var bounds = new Rect(50, 50, 750, 500);
            var overlap = new[] { new Rect(390, 270, 50, 60) };
            var point = new Vector2(405, 300);
            var footprint = new Rect(-10, -10, 20, 20);
            Assert.That(HudMarkerProjection.TryAvoidHud(point, bounds, overlap, footprint, 60, out var placement), Is.True);
            Assert.That(placement.y, Is.EqualTo(point.y));
            Assert.That(placement.x, Is.InRange(point.x - 60, 380));
            Assert.That(bounds.Contains(placement), Is.True);
            Assert.That(HudMarkerProjection.TryAvoidHud(point, bounds, overlap, footprint, 10, out _), Is.False);
        }
    }
}
