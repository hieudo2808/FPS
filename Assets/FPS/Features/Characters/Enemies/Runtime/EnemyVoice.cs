using UnityEngine;

namespace FPS
{
    /// <summary>
    /// An enemy's voice on every peer: idle growls while alive and near the listener, plus attack, pain and
    /// death calls driven by the replicated EnemyAI presentation. Purely cosmetic; the server never waits on it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyVoice : MonoBehaviour, IPoolResettable
    {
        [SerializeField] private SoundEvent idle;
        [SerializeField] private SoundEvent attack;
        [SerializeField] private SoundEvent hurt;
        [SerializeField] private SoundEvent death;
        [Tooltip("Seconds between idle growls (random in range).")]
        [SerializeField] private Vector2 idleInterval = new(4f, 9f);
        [Tooltip("Idle growls are skipped beyond this listener distance.")]
        [SerializeField, Min(1f)] private float idleRange = 22f;
        [Tooltip("Minimum seconds between two pain calls from this enemy.")]
        [SerializeField, Min(0f)] private float hurtCooldown = 0.7f;

        private EnemyAI enemy;
        private float nextIdle;
        private float nextHurt;
        private AudioVoice current;

        public SoundEvent Idle => idle;
        public SoundEvent Attack => attack;
        public SoundEvent Hurt => hurt;
        public SoundEvent Death => death;

        public void Configure(SoundEvent idleSound, SoundEvent attackSound, SoundEvent hurtSound, SoundEvent deathSound)
        {
            idle = idleSound; attack = attackSound; hurt = hurtSound; death = deathSound;
        }

        private void Awake() => enemy = GetComponent<EnemyAI>();
        private void OnEnable() => ScheduleIdle(0.5f);

        public void ResetForPool()
        {
            GameAudio.Stop(current, 0f);
            current = default;
            nextHurt = 0f;
            ScheduleIdle(0.5f);
        }

        private void ScheduleIdle(float minimum)
            => nextIdle = Time.time + Mathf.Max(minimum, Random.Range(idleInterval.x, idleInterval.y));

        private void Update()
        {
            if (idle == null || Time.time < nextIdle || !GameAudio.CanPlay) return;
            ScheduleIdle(1f);
            if (enemy != null && enemy.PresentsDead) return;
            if (!GameAudio.TryGetListenerPosition(out Vector3 listener)
                || (listener - transform.position).sqrMagnitude > idleRange * idleRange) return;
            if (!GameAudio.IsPlaying(current)) current = GameAudio.Play(idle, Mouth, 1f, transform);
        }

        private Vector3 Mouth => transform.position + Vector3.up * 1.6f;

        /// <summary>Returns false when this enemy has no attack voice, so the caller can use its own clip.</summary>
        public bool PlayAttack()
        {
            if (attack == null) return false;
            Interrupt();
            current = GameAudio.Play(attack, Mouth, 1f, transform);
            ScheduleIdle(2f);
            return true;
        }

        public bool PlayHurt()
        {
            if (hurt == null || Time.time < nextHurt) return hurt != null;
            nextHurt = Time.time + hurtCooldown;
            Interrupt();
            current = GameAudio.Play(hurt, Mouth, 1f, transform);
            ScheduleIdle(2f);
            return true;
        }

        public bool PlayDeath()
        {
            if (death == null) return false;
            Interrupt();
            current = GameAudio.Play(death, Mouth);
            nextIdle = float.PositiveInfinity;
            return true;
        }

        // One mouth: a new call cuts the growl that is still going.
        private void Interrupt() => GameAudio.Stop(current, 0.06f);
    }
}
