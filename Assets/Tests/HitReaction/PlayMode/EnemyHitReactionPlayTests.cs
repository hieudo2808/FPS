#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    /// <summary>
    /// The real enemy prefabs, offline (no NetworkManager, so this process is the authority):
    /// a hit is merged, decided, bends the skinned bone the hitbox sits on and sprays blood.
    /// </summary>
    public sealed class EnemyHitReactionPlayTests
    {
        const string Root = "Assets/FPS/Features/Characters/Content/Enemies/";
        static readonly string[] Prefabs =
        {
            Root + "ZombieRig/Prefabs/ZombieRig.prefab",
            Root + "Copzombie/Prefabs/copzombie_l_actisdato.prefab"
        };

        GameObject enemy;
        GameObject target;

        sealed class TestTarget : MonoBehaviour, IDamageable
        {
            public float received;
            public void TakeDamage(float amount) => received += amount;
            public bool IsDead => false;
        }

        [SetUp]
        public void Quiet() => LogAssert.ignoreFailingMessages = true; // No NavMesh in an empty test scene.

        [TearDown]
        public void Clean()
        {
            if (enemy != null) Object.Destroy(enemy);
            if (target != null) Object.Destroy(target);
            GameObject spray;
            while ((spray = GameObject.Find("BloodSprayEffect(Clone)")) != null) Object.DestroyImmediate(spray);
            LogAssert.ignoreFailingMessages = false;
        }

        IEnumerator Spawn(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            enemy = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            var animator = enemy.GetComponentInChildren<Animator>();
            animator.speed = 0f; // Freeze the clip so only the reaction moves the bones.
            for (int i = 0; i < 3; i++) yield return null;
        }

        static DamageInfo Shot(HitboxSegment segment, float amount, Vector3 from) =>
            new DamageInfo(amount, 1, 0, segment.GetComponent<Collider>().bounds.center,
                damageType: DamageType.Bullet, hitZone: segment.Zone, sourcePosition: from);

        static Transform SkinnedBoneOf(GameObject enemy, HitboxSegment segment)
        {
            var bones = enemy.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.bones).ToHashSet();
            Transform bone = segment.transform;
            while (bone != null && !bones.Contains(bone)) bone = bone.parent;
            return bone;
        }

        [UnityTest]
        public IEnumerator LegHitStumblesBendsTheLegAndBleeds([ValueSource(nameof(Prefabs))] string path)
        {
            yield return Spawn(path);
            var ai = enemy.GetComponent<EnemyAI>();
            var health = enemy.GetComponent<EnemyHealth>();
            var presenter = enemy.GetComponent<EnemyHitReactionPresenter>();
            Assert.That(presenter, Is.Not.Null, "Prefab carries the presenter");
            Assert.That(presenter.BloodPrefab, Is.Not.Null, "Prefab has the blood spray assigned");
            var leg = enemy.GetComponentsInChildren<HitboxSegment>(true).First(s => s.Zone == HitboxZone.Leg);
            Transform bone = SkinnedBoneOf(enemy, leg);
            Assert.That(bone, Is.Not.Null);

            yield return new WaitForEndOfFrame();
            Quaternion rest = bone.rotation;
            int blood = EnemyHitReactionPresenter.BloodSpawnedCount;
            health.TakeDamage(Shot(leg, 5f, enemy.transform.position + enemy.transform.forward * 6f + Vector3.up));

            yield return null; // EnemyAI.Update resolves the burst.
            Assert.That(ai.LastHitReaction, Is.EqualTo(EnemyHitReactionKind.Stumble));
            Assert.That(ai.IsMotionLocked, Is.True, "A stumble holds the enemy still");
            Assert.That(EnemyHitReactionPresenter.LastBone, Is.EqualTo(bone.name));
            Assert.That(EnemyHitReactionPresenter.BloodSpawnedCount, Is.EqualTo(blood + 1));
            var spray = GameObject.Find("BloodSprayEffect(Clone)");
            Assert.That(spray, Is.Not.Null, "Blood spray spawned");
            Assert.That(spray.GetComponentsInChildren<ParticleSystem>().All(p => !p.main.loop), Is.True, "A wound plays one cycle");
            Assert.That(Vector3.Distance(spray.transform.position, leg.GetComponent<Collider>().bounds.center), Is.LessThan(0.3f),
                "Blood starts at the hit point");

            float peak = 0f;
            float until = Time.time + 0.15f;
            while (Time.time < until)
            {
                yield return new WaitForEndOfFrame();
                peak = Mathf.Max(peak, Quaternion.Angle(rest, bone.rotation));
            }
            Debug.Log($"[HitReactionTest] {enemy.name} bone={bone.name} peak={peak:0.0}deg");
            Assert.That(peak, Is.GreaterThan(3f), "The hit leg visibly buckles");

            yield return new WaitForSeconds(EnemyHitReactionRules.StumbleSeconds + 0.1f);
            Assert.That(ai.IsMotionLocked, Is.False, "The stumble ends");
            yield return new WaitForSeconds(0.3f);
            yield return new WaitForEndOfFrame();
            Assert.That(Quaternion.Angle(rest, bone.rotation), Is.LessThan(0.5f), "The leg returns to the animated pose");
            Assert.That(presenter.ActiveImpulseCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator HitsInsideTheCooldownOnlyFlinchAndShotgunPelletsMerge()
        {
            yield return Spawn(Prefabs[0]);
            var ai = enemy.GetComponent<EnemyAI>();
            var health = enemy.GetComponent<EnemyHealth>();
            var segments = enemy.GetComponentsInChildren<HitboxSegment>(true);
            var leg = segments.First(s => s.Zone == HitboxZone.Leg);
            var chest = segments.First(s => s.Zone == HitboxZone.Chest);
            Vector3 from = enemy.transform.position + enemy.transform.forward * 4f + Vector3.up;

            health.TakeDamage(Shot(leg, 5f, from));
            yield return null;
            Assert.That(ai.LastHitReaction, Is.EqualTo(EnemyHitReactionKind.Stumble));
            int count = ai.HitReactionCount;

            yield return new WaitForSeconds(EnemyHitReactionRules.PresentInterval + 0.05f);
            // Eight pellets in one frame: one reaction, judged on their sum.
            for (int i = 0; i < 8; i++) health.TakeDamage(Shot(chest, health.MaxHealth * 0.05f, from));
            yield return null;
            Assert.That(ai.HitReactionCount, Is.EqualTo(count + 1), "Pellets of one shell are one reaction");
            Assert.That(ai.LastHitReaction, Is.EqualTo(EnemyHitReactionKind.Flinch), "Inside the cooldown a heavy hit only flinches");

            yield return new WaitForSeconds(EnemyHitReactionRules.GameplayCooldown);
            health.TakeDamage(Shot(leg, 5f, from));
            yield return null;
            Assert.That(ai.LastHitReaction, Is.EqualTo(EnemyHitReactionKind.Stumble), "After the cooldown the leg stumbles again");

            ai.ResetForPool();
            Assert.That(ai.HitReactionCount, Is.Zero, "Pool reuse starts clean");
        }

        [UnityTest]
        public IEnumerator HeavyHitDuringTheWindupCancelsTheSwing()
        {
            yield return Spawn(Prefabs[0]);
            var ai = enemy.GetComponent<EnemyAI>();
            var health = enemy.GetComponent<EnemyHealth>();
            target = new GameObject("Test target");
            target.transform.position = enemy.transform.position + enemy.transform.forward * 1.2f;
            var victim = target.AddComponent<TestTarget>();
            ai.BeginDirectorPursuit(target.transform);

            float until = Time.time + 3f;
            while (!ai.IsMotionLocked && Time.time < until) yield return null; // The swing takes the motion lock.
            Assert.That(ai.IsMotionLocked, Is.True, "The enemy started a swing at the target");

            var chest = enemy.GetComponentsInChildren<HitboxSegment>(true).First(s => s.Zone == HitboxZone.Chest);
            health.TakeDamage(Shot(chest, health.MaxHealth * 0.3f, target.transform.position + Vector3.up));
            yield return null;
            Assert.That(ai.LastHitReaction, Is.EqualTo(EnemyHitReactionKind.Interrupt));

            yield return new WaitForSeconds(1.0f); // Past the swing's impact time.
            Assert.That(victim.received, Is.Zero, "A broken swing deals no damage");

            yield return new WaitForSeconds(2.5f);
            Assert.That(victim.received, Is.GreaterThan(0f), "The enemy attacks again after recovering");
        }
    }
}
#endif
