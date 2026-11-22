using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Cosmetic hit reaction played on every peer from the server's replicated hit. The bone the
    /// hitbox belongs to (and a couple of its parents, weaker) is bent away from the bullet after the
    /// Animator has posed the skeleton, then eases back. It works on humanoid and generic rigs alike
    /// because it only uses the transforms the hitboxes already sit on. Gameplay never reads it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHitReactionPresenter : MonoBehaviour
    {
        private const int MaxImpulses = 6;
        private const int MaxParentBones = 2;
        private const int GlobalBloodCap = 24;

        [Header("Flinch")]
        [SerializeField, Range(0f, 45f)] private float lightAngle = 8f;
        [SerializeField, Range(0f, 60f)] private float heavyAngle = 26f;
        [SerializeField, Min(0.01f)] private float riseSeconds = 0.05f;
        [SerializeField, Min(0.05f)] private float recoverSeconds = 0.28f;
        [SerializeField, Min(0.05f)] private float heavyRecoverSeconds = 0.5f;
        [SerializeField, Range(0f, 1f)] private float parentFalloff = 0.45f;

        [Header("Blood")]
        [Tooltip("Cosmetic spray at the hit point. Source: Unity Particle Pack (see SOURCE.md next to the prefab).")]
        [SerializeField] private GameObject bloodPrefab;
        [SerializeField, Min(0.1f)] private float bloodLifetime = 2.5f;
        [SerializeField, Min(0.1f)] private float bloodScale = 1f;

        private struct Impulse
        {
            public Transform bone;
            public Vector3 localAxis; // in this enemy's root space, so it turns with the body
            public float amplitude, age, recover;
        }

        private static readonly Queue<GameObject> liveBlood = new Queue<GameObject>(GlobalBloodCap);
        private readonly List<Impulse> impulses = new List<Impulse>(MaxImpulses);
        private readonly HashSet<Transform> skinnedBones = new HashSet<Transform>();
        private Renderer[] renderers;
        private Animator animator;
        private bool bonesCached;

        // Process-wide diagnostics for acceptance tests and the peer probe. Never read by gameplay.
        public static int PlayedCount { get; private set; }
        public static int BloodSpawnedCount { get; private set; }
        public static EnemyHitReactionKind LastKind { get; private set; }
        public static string LastBone { get; private set; } = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            liveBlood.Clear();
            PlayedCount = BloodSpawnedCount = 0;
            LastKind = EnemyHitReactionKind.None;
            LastBone = string.Empty;
        }

        public int ActiveImpulseCount => impulses.Count;
        public GameObject BloodPrefab => bloodPrefab;

        public void Play(HitboxSegment segment, Vector3 segmentOffset, Vector3 travel, float strength01,
            EnemyHitReactionKind kind)
        {
            if (!isActiveAndEnabled) return;
            CacheBones();
            bool heavy = kind == EnemyHitReactionKind.Interrupt;
            float strength = heavy ? 1f : Mathf.Clamp01(strength01);
            float recover = Mathf.Lerp(recoverSeconds, heavyRecoverSeconds, strength);

            travel.y = 0f;
            if (travel.sqrMagnitude < 0.0001f) travel = transform.forward;
            travel.Normalize();
            // Bending around this axis tips the bone along the bullet's travel: away from the shooter.
            Vector3 localAxis = Quaternion.Inverse(transform.rotation) * Vector3.Cross(Vector3.up, travel);

            HitboxSegment target = segment != null ? segment : FindTorso();
            HitboxZone zone = target != null ? target.Zone : HitboxZone.Body;
            float amplitude = Mathf.Lerp(lightAngle, heavyAngle, strength) * ZoneScale(zone);
            Transform bone = target != null ? ResolveBone(target.transform) : null;
            if (bone == null)
            {
                // The Body hitbox is usually a capsule on the root with no bone of its own: bend the torso.
                HitboxSegment torso = FindTorso();
                bone = torso != null && torso != target ? ResolveBone(torso.transform) : null;
            }
            AddChain(bone, localAxis, amplitude, recover);
            PlayedCount++;
            LastKind = kind;
            LastBone = bone != null ? bone.name : string.Empty;

            if (heavy && zone != HitboxZone.Chest && zone != HitboxZone.Body)
            {
                // Breaking a swing moves the whole upper body, not just the limb that was hit.
                HitboxSegment torso = FindTorso();
                if (torso != null) AddChain(ResolveBone(torso.transform), localAxis, heavyAngle * 0.6f, recover);
            }

            if (segment != null)
                SpawnBlood(segment.transform.position + segment.transform.rotation * segmentOffset,
                    segment.transform.position, travel);
        }

        public void Clear() => impulses.Clear();

        private void OnDisable() => impulses.Clear();

        private void LateUpdate()
        {
            if (impulses.Count == 0) return;
            float dt = Time.deltaTime;
            bool apply = ShouldApplyPose();
            Quaternion root = transform.rotation;
            for (int index = impulses.Count - 1; index >= 0; index--)
            {
                Impulse impulse = impulses[index];
                impulse.age += dt;
                float weight = Envelope(impulse.age, riseSeconds, impulse.recover);
                if (weight <= 0f || impulse.bone == null)
                {
                    impulses.RemoveAt(index);
                    continue;
                }
                impulses[index] = impulse;
                if (!apply) continue;
                Vector3 axis = root * impulse.localAxis;
                impulse.bone.rotation = Quaternion.AngleAxis(impulse.amplitude * weight, axis) * impulse.bone.rotation;
            }
        }

        /// <summary>0 → 1 over <paramref name="rise"/>, then eases back to 0 over <paramref name="recover"/>.</summary>
        public static float Envelope(float age, float rise, float recover)
        {
            if (age <= 0f) return 0f;
            if (age < rise) return age / rise;
            float t = (age - rise) / recover;
            if (t >= 1f) return 0f;
            float remaining = 1f - t;
            return remaining * remaining;
        }

        private static float ZoneScale(HitboxZone zone) => zone switch
        {
            HitboxZone.Head => 1.25f,
            HitboxZone.Arm => 1.1f,
            HitboxZone.Leg => 0.9f,
            HitboxZone.Chest => 0.9f,
            _ => 0.8f
        };

        private void AddChain(Transform bone, Vector3 localAxis, float amplitude, float recover)
        {
            float scale = 1f;
            for (int depth = 0; bone != null && bone != transform && depth <= MaxParentBones; depth++)
            {
                if (impulses.Count >= MaxImpulses) impulses.RemoveAt(0);
                impulses.Add(new Impulse { bone = bone, localAxis = localAxis, amplitude = amplitude * scale, recover = recover });
                scale *= parentFalloff;
                bone = bone.parent;
                if (bone != null && !skinnedBones.Contains(bone)) break;
            }
        }

        /// <summary>The hitbox may sit on the bone or on a child of it. Return the skinned bone.</summary>
        private Transform ResolveBone(Transform from)
        {
            Transform current = from;
            for (int step = 0; current != null && current != transform && step < 4; step++)
            {
                if (skinnedBones.Count == 0 || skinnedBones.Contains(current)) return current;
                current = current.parent;
            }
            return skinnedBones.Count == 0 ? from : null;
        }

        private HitboxSegment FindTorso()
        {
            HitboxSegment body = null;
            foreach (HitboxSegment segment in GetComponentsInChildren<HitboxSegment>(true))
            {
                if (segment.Zone == HitboxZone.Chest) return segment;
                if (segment.Zone == HitboxZone.Body && body == null) body = segment;
            }
            return body;
        }

        private void CacheBones()
        {
            if (bonesCached) return;
            bonesCached = true;
            animator = GetComponentInChildren<Animator>();
            renderers = GetComponentsInChildren<Renderer>(true);
            foreach (SkinnedMeshRenderer skinned in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (Transform bone in skinned.bones)
                    if (bone != null) skinnedBones.Add(bone);
        }

        /// <summary>
        /// The bend is re-applied on top of the Animator's pose each frame. If a culled Animator stops
        /// writing the pose, adding to it again would accumulate, so skip while it is culled.
        /// </summary>
        private bool ShouldApplyPose()
        {
            if (animator == null || !animator.enabled) return false;
            if (animator.cullingMode == AnimatorCullingMode.AlwaysAnimate) return true;
            foreach (Renderer item in renderers)
                if (item != null && item.isVisible) return true;
            return false;
        }

        private void SpawnBlood(Vector3 point, Vector3 boneCenter, Vector3 travel)
        {
            if (bloodPrefab == null) return;
            Vector3 outward = point - boneCenter;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : -travel;
            // Mostly out of the wound, leaning back toward the shooter so the player sees it.
            Vector3 direction = (outward - travel * 0.6f).normalized;
            if (direction.sqrMagnitude < 0.0001f) direction = -travel;
            GameObject spray = Instantiate(bloodPrefab, point, Quaternion.LookRotation(direction));
            spray.transform.localScale *= bloodScale;
            // The Particle Pack spray is authored as a looping demo; a wound plays one cycle.
            foreach (ParticleSystem system in spray.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.loop = false;
            }
            Destroy(spray, bloodLifetime);
            BloodSpawnedCount++;
            while (liveBlood.Count > 0 && liveBlood.Peek() == null) liveBlood.Dequeue();
            liveBlood.Enqueue(spray);
            while (liveBlood.Count > GlobalBloodCap)
            {
                GameObject oldest = liveBlood.Dequeue();
                if (oldest != null) Destroy(oldest);
            }
        }
    }
}
