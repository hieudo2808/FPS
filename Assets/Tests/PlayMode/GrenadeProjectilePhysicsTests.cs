#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FPS.Tests
{
    public sealed class GrenadeProjectilePhysicsTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly float[] AimPitches = { 0f, 35f, -45f, 80f };

        [UnityTest]
        public IEnumerator AuthoredProjectileFollowsBallisticArcAndCanLandBeforeFuse([ValueSource(nameof(AimPitches))] float aimPitch)
        {
            var scene = SceneManager.CreateScene("Grenade ballistic regression", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Features/Survival/Content/Prefabs/GrenadeProjectile.prefab");
                var grenade = Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(grenade, scene);
                var projectile = grenade.GetComponent<SurvivalProjectile>();
                Vector3 direction = Quaternion.Euler(-aimPitch, 0f, 0f) * Vector3.forward;
                projectile.InitializeServer(ThrowableKind.Frag, 0, null, direction);
                Vector3 velocity = (Vector3)typeof(SurvivalProjectile).GetField("launchVelocity", Private).GetValue(projectile);
                float fuse = new SerializedObject(projectile).FindProperty("fuseDuration").floatValue;
                var body = grenade.GetComponent<Rigidbody>();
                Assert.That(body.useGravity, Is.True);
                Assert.That(body.linearDamping, Is.Zero);
                Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
                Assert.That(velocity.magnitude, Is.EqualTo(28f).Within(.001f));
                Assert.That(velocity.z, Is.GreaterThanOrEqualTo(-.0001f), "A high lob must never reverse direction.");
                Vector3 start = new(0f, 1.45f, 0f);
                body.position = start;
                body.linearVelocity = velocity;
                float elapsed = 0f;
                float gravity = -Physics.gravity.y;
                float landingTime = (velocity.y + Mathf.Sqrt(velocity.y * velocity.y + 2f * gravity * start.y)) / gravity;
                Assert.That(landingTime, Is.LessThan(fuse), "The fuse must allow the complete lob.");
                while (body.position.y > 0f && elapsed < fuse)
                {
                    scene.GetPhysicsScene().Simulate(.01f);
                    elapsed += .01f;
                    Vector3 expected = start + velocity * elapsed + .5f * Physics.gravity * elapsed * elapsed;
                    Assert.That(Vector3.Distance(body.position, expected), Is.LessThan(.4f), "Rigidbody trajectory versus constant-gravity projectile motion.");
                }
                Assert.That(body.position.y, Is.LessThanOrEqualTo(0f));
                Assert.That(body.position.z, Is.EqualTo(velocity.z * landingTime).Within(.4f));
                if (aimPitch == 0f) Assert.That(body.position.z, Is.InRange(30f, 40f));
                if (aimPitch == 35f) Assert.That(body.position.z, Is.InRange(75f, 85f));
                if (aimPitch == -45f) Assert.That(body.position.z, Is.LessThan(6f));
                if (aimPitch == 80f) Assert.That(body.position.z, Is.LessThan(.1f));
                TestContext.WriteLine($"Aim {aimPitch} degrees: {velocity.magnitude:F1} m/s, range {body.position.z:F2} m, flight {elapsed:F2} s.");
            }
            finally
            {
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }
    }
}
#endif
