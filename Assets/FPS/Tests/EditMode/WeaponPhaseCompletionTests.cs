using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FPS.Tests
{
    public sealed class WeaponPhaseCompletionTests
    {
        private const string ContentRoot = "Assets/FPS/Features/Weapons/Content";

        private static readonly Dictionary<string, string> ControllerPaths = new()
        {
            ["Vandal"] = $"{ContentRoot}/Vandal/Animation/VandalAnim.controller",
            ["Classic"] = $"{ContentRoot}/Classic/Animations/ClassicAnim.controller",
            ["Operator"] = $"{ContentRoot}/Operator/Animation/Operator.controller",
            ["Odin"] = $"{ContentRoot}/Odin/Animation/OdinAnim.controller",
            ["Bucky"] = $"{ContentRoot}/Bucky/Animations/BuckyAnim.controller"
        };

        [TestCase("Vandal")]
        [TestCase("Classic")]
        [TestCase("Operator")]
        [TestCase("Odin")]
        [TestCase("Bucky")]
        public void FireInterval_IsBakedFromGunAnimator(string weaponName)
        {
            WeaponData data = LoadData(weaponName);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                ControllerPaths[weaponName]);
            AnimatorState fire = controller.layers[0].stateMachine.states
                .Select(item => item.state).Single(state => state.name == "Fire");
            AnimationClip clip = fire.motion as AnimationClip;
            Assert.NotNull(clip);

            float expected = data.restartFireAnimationPerShot
                ? clip.length / fire.speed
                : (data.fireLoopEndFrame - data.fireLoopStartFrame) / (clip.frameRate * fire.speed);
            Assert.AreEqual(expected, data.FireInterval, 0.0002f);
        }

        [Test]
        public void Odin_ThirtyFramesAtSpeedElevenMatchesGameplayInterval()
        {
            WeaponData odin = LoadData("Odin");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                ControllerPaths["Odin"]);
            AnimatorState fire = controller.layers[0].stateMachine.states
                .Select(item => item.state).Single(state => state.name == "Fire");
            Assert.AreEqual(11f, fire.speed, 0.0001f);
            Assert.AreEqual(30, odin.fireLoopEndFrame - odin.fireLoopStartFrame);
            Assert.AreEqual(0.113636f, odin.FireInterval, 0.0002f);
        }

        [Test]
        public void FixedArchetypeData_MatchesLockedDesign()
        {
            AssertWeapon("Vandal", 1, 25f, 0.15f, 100f, 40f, 70f, 0.8f);
            AssertWeapon("Classic", 1, 20f, 0.25f, 60f, 20f, 40f, 0.7f);
            AssertWeapon("Operator", 1, 150f, 2.5f, 200f, 200f, 200f, 1f);
            AssertWeapon("Odin", 1, 20f, 0.6f, 120f, 30f, 60f, 0.65f);
            AssertWeapon("Bucky", 8, 18.75f, 4f, 25f, 8f, 18f, 0.15f);

            WeaponData bucky = LoadData("Bucky");
            Assert.AreEqual(150f, bucky.projectileCount * bucky.damage, 0.0001f);
            Assert.AreEqual(FireMode.Single, bucky.fireMode,
                "Bucky consumes one shell per trigger; projectileCount supplies the simultaneous pellet burst.");
            Assert.IsNull(bucky.recoilPattern);
            Assert.AreEqual(ReloadMode.PerShell, bucky.reloadMode);

            WeaponData odin = LoadData("Odin");
            Assert.AreEqual(300, odin.totalAmmo);
            Assert.NotNull(odin.recoilPattern);
            Assert.AreEqual(12, odin.recoilPattern.shots.Length);

            WeaponData op = LoadData("Operator");
            Assert.True(op.supportsAim);
            Assert.AreEqual(0f, op.aimedSpreadAngle, 0.0001f);
            Assert.AreEqual(25f, op.aimedWorldFov, 0.0001f);
            Assert.AreEqual(0.12f, op.aimTransitionDuration, 0.0001f);
            Assert.AreEqual(0.65f, op.aimedSensitivityMultiplier, 0.0001f);
            Assert.True(op.showScopeOverlay);
            Assert.NotNull(op.scopeOverlaySprite);
            Assert.True(op.exitAimAfterShot);

            WeaponData vandal = LoadData("Vandal");
            Assert.True(vandal.supportsAim);
            Assert.AreEqual(50f, vandal.aimedWorldFov, 0.0001f);
            Assert.False(vandal.showScopeOverlay);
            Assert.False(vandal.exitAimAfterShot);
        }

        [Test]
        public void NormalZombie_HasDeliberateBodyShotBudgets()
        {
            const float normalZombieHealth = 100f;

            Assert.AreEqual(4, ShotsToKill(LoadData("Vandal"), normalZombieHealth));
            Assert.AreEqual(5, ShotsToKill(LoadData("Classic"), normalZombieHealth));
            Assert.AreEqual(1, ShotsToKill(LoadData("Operator"), normalZombieHealth));
            Assert.AreEqual(5, ShotsToKill(LoadData("Odin"), normalZombieHealth));

            WeaponData bucky = LoadData("Bucky");
            Assert.GreaterOrEqual(bucky.damage * bucky.projectileCount, normalZombieHealth,
                "A close-range Bucky blast should kill a normal zombie when enough pellets connect.");
        }

        [Test]
        public void Falloff_UsesInclusiveBoundariesAndMinimumMultiplier()
        {
            WeaponData vandal = LoadData("Vandal");
            Assert.AreEqual(1f, vandal.EvaluateDamageMultiplier(40f), 0.0001f);
            Assert.AreEqual(0.9f, vandal.EvaluateDamageMultiplier(55f), 0.0001f);
            Assert.AreEqual(0.8f, vandal.EvaluateDamageMultiplier(70f), 0.0001f);
            Assert.AreEqual(0.8f, vandal.EvaluateDamageMultiplier(100f), 0.0001f);

            WeaponData op = LoadData("Operator");
            Assert.AreEqual(1f, op.EvaluateDamageMultiplier(200f), 0.0001f);
        }

        [Test]
        public void Spread_IsDeterministicPerShotSequenceAndInsideCone()
        {
            uint seed = WeaponBallistics.BuildShotSeed(17, 91, 0);
            Vector3 first = WeaponBallistics.GetProjectileDirection(Vector3.forward, 4f, seed, 3);
            Vector3 repeated = WeaponBallistics.GetProjectileDirection(Vector3.forward, 4f, seed, 3);
            uint nextSeed = WeaponBallistics.BuildShotSeed(17, 92, 0);
            Vector3 nextShot = WeaponBallistics.GetProjectileDirection(Vector3.forward, 4f, nextSeed, 3);

            Assert.AreEqual(first, repeated);
            Assert.AreNotEqual(first, nextShot);
            Assert.LessOrEqual(Vector3.Angle(Vector3.forward, first), 4.001f);
        }

        [Test]
        public void RecoilVariation_IsDeterministicAndSustainedDirectionDoesNotAlternateEveryShot()
        {
            RecoilPattern odin = LoadData("Odin").recoilPattern;
            Assert.NotNull(odin);

            int firstSustainedShot = odin.shots.Length;
            Vector2 first = odin.GetShot(firstSustainedShot);
            Assert.AreEqual(first, odin.GetShot(firstSustainedShot));
            Assert.GreaterOrEqual(Mathf.Abs(first.y), 0.9f);

            float firstDirection = Mathf.Sign(first.y);
            for (int offset = 1; offset < odin.sustainedDirectionHoldShots; offset++)
            {
                Vector2 next = odin.GetShot(firstSustainedShot + offset);
                Assert.AreEqual(firstDirection, Mathf.Sign(next.y),
                    "Sustained recoil should sweep for several shots, not alternate left/right every bullet.");
            }

            Vector2 nextSpray = odin.GetShot(firstSustainedShot, 1u);
            Assert.AreNotEqual(first, nextSpray,
                "A new spray sequence should not repeat the exact previous recoil path.");
            Assert.AreEqual(nextSpray, odin.GetShot(firstSustainedShot, 1u),
                "A shot remains deterministic inside one spray sequence.");
        }

        [TestCase("Vandal")]
        [TestCase("Classic")]
        [TestCase("Operator")]
        [TestCase("Odin")]
        [TestCase("Bucky")]
        public void VisualBulletPrefab_HasProjectileDriver(string weaponName)
        {
            WeaponData data = LoadData(weaponName);
            Assert.NotNull(data.bulletPrefab, $"{weaponName} must have a visual bullet prefab.");
            Assert.NotNull(data.bulletPrefab.GetComponent<VisualBulletProjectile>(),
                $"{weaponName}'s visual bullet must move independently of Rigidbody physics.");
        }

        [TestCase("Vandal")]
        [TestCase("Classic")]
        [TestCase("Operator")]
        [TestCase("Odin")]
        [TestCase("Bucky")]
        public void ShotFeedback_HasReusableMuzzleFlashAndSurfaceImpact(string weaponName)
        {
            WeaponData data = LoadData(weaponName);
            Assert.NotNull(data.muzzleFlashPrefab, $"{weaponName} must show a muzzle flash.");
            Assert.IsNotEmpty(data.muzzleFlashPrefab.GetComponentsInChildren<ParticleSystem>(true));
            Assert.NotNull(data.surfaceImpactPrefab,
                $"{weaponName} must create visible map impacts and bullet holes.");
            Assert.IsNotEmpty(data.surfaceImpactPrefab.GetComponentsInChildren<ParticleSystem>(true));
            Assert.True(data.surfaceImpactPrefab.GetComponentsInChildren<Renderer>(true)
                .Any(renderer => renderer.name == "BulletHole"),
                $"{weaponName}'s impact prefab must contain a visible BulletHole renderer.");
            Assert.GreaterOrEqual(data.maxConcurrentSurfaceImpacts, data.projectileCount,
                "One Bucky blast must not evict its own pellet impacts.");
        }

        private static void AssertWeapon(
            string weaponName,
            int projectileCount,
            float damage,
            float spread,
            float maximumRange,
            float falloffStart,
            float falloffEnd,
            float minimumMultiplier)
        {
            WeaponData data = LoadData(weaponName);
            Assert.AreEqual(projectileCount, data.projectileCount);
            Assert.AreEqual(damage, data.damage, 0.0001f);
            Assert.AreEqual(spread, data.hipSpreadAngle, 0.0001f);
            Assert.AreEqual(maximumRange, data.maximumRange, 0.0001f);
            Assert.AreEqual(falloffStart, data.falloffStartDistance, 0.0001f);
            Assert.AreEqual(falloffEnd, data.falloffEndDistance, 0.0001f);
            Assert.AreEqual(minimumMultiplier, data.minimumDamageMultiplier, 0.0001f);
        }

        private static WeaponData LoadData(string weaponName)
        {
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(
                $"{ContentRoot}/{weaponName}/{weaponName}.asset");
            Assert.NotNull(data, weaponName);
            return data;
        }

        private static int ShotsToKill(WeaponData data, float health)
        {
            return Mathf.CeilToInt(health / Mathf.Max(0.0001f, data.damage));
        }
    }
}
