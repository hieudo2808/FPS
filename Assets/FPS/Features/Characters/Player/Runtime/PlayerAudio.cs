using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Player body sounds on every peer, from replicated motion and life state only: footsteps paced by
    /// stride, landings, pain on real damage, the downed call and breathing loop, and the death call.
    /// The owner hears itself in 2D; teammates hear it in 3D.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAudio : MonoBehaviour
    {
        [SerializeField] private PlayerVoiceSet voice;
        [SerializeField] private SoundEvent footstep;
        [SerializeField] private SoundEvent land;
        [SerializeField] private SoundEvent downedBreath;
        [SerializeField, Min(0f)] private float hurtCooldown = 0.8f;
        [SerializeField, Min(0f)] private float minimumHurtDamage = 3f;
        [Range(0f, 1f), SerializeField] private float ownFootstepVolume = 0.55f;
        [SerializeField] private LayerMask groundMask = ~0;

        private static readonly RaycastHit[] Hits = new RaycastHit[8];
        private PlayerHealth health;
        private NetworkObject network;
        private CharacterController body;
        private Vector3 lastPosition;
        private bool hasLastPosition;
        private float speed;
        private float stride;
        private bool grounded = true;
        private float airTime;
        private float lastHealth = -1f;
        private float nextHurt;
        private bool breathing;
        private AudioVoice voiceHandle;
        private AudioVoice breathHandle;

        public int StepsPlayed { get; private set; }
        public int LandingsPlayed { get; private set; }
        public int VoiceLinesPlayed { get; private set; }
        public bool Breathing => breathing;
        public PlayerVoiceSet Voice => voice;

        public void Configure(PlayerVoiceSet voiceSet, SoundEvent step, SoundEvent landing, SoundEvent breath)
        {
            voice = voiceSet; footstep = step; land = landing; downedBreath = breath;
        }

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
            network = GetComponent<NetworkObject>();
            body = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            hasLastPosition = false;
            if (health == null) return;
            health.HealthChangedEvent += OnHealthChanged;
            health.LifeStateChanged += OnLifeStateChanged;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.HealthChangedEvent -= OnHealthChanged;
                health.LifeStateChanged -= OnLifeStateChanged;
            }
            StopBreath();
        }

        private bool IsLocal => network == null || !network.IsSpawned || network.IsOwner;
        private bool Alive => health == null || (health.LifeState == PlayerLifeState.Alive && !health.IsDead);
        private Vector3 Head => transform.position + Vector3.up * 1.5f;

        private void Update()
        {
            bool shouldBreathe = health != null && health.LifeState == PlayerLifeState.Downed && !health.IsDead;
            if (shouldBreathe != breathing)
            {
                if (shouldBreathe) StartBreath();
                else StopBreath();
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 position = transform.position;
            if (!hasLastPosition) { lastPosition = position; hasLastPosition = true; return; }
            Vector3 moved = position - lastPosition;
            lastPosition = position;
            float planar = new Vector2(moved.x, moved.z).magnitude;
            if (planar > 3f) { stride = 0f; return; } // spawn or teleport

            speed = Mathf.Lerp(speed, planar / dt, 1f - Mathf.Exp(-10f * dt));
            if (!ProbeGround())
            {
                grounded = false;
                airTime += dt;
                return;
            }
            if (!grounded)
            {
                if (airTime > 0.3f && Alive) PlayLanding(airTime);
                grounded = true;
                airTime = 0f;
                stride = AudioRules.StrideLength(speed) * 0.5f;
            }
            if (!Alive || speed < 0.8f)
            {
                // Standing still: the first step after starting to move comes quickly.
                stride = AudioRules.StrideLength(speed) * 0.6f;
                return;
            }
            stride += planar;
            float length = AudioRules.StrideLength(speed);
            if (stride < length) return;
            stride -= length;
            PlayStep(AudioRules.FootstepVolume(speed));
        }

        private bool ProbeGround()
        {
            Vector3 origin = transform.position + Vector3.up * 0.3f;
            float length = 0.6f;
            if (body != null)
            {
                origin = transform.TransformPoint(body.center);
                length = body.height * 0.5f * Mathf.Abs(transform.lossyScale.y) + 0.3f;
            }
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, Hits, length, groundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (Hits[i].transform != null && !Hits[i].transform.IsChildOf(transform)) return true;
            return false;
        }

        private void PlayStep(float volume)
        {
            if (footstep == null) return;
            StepsPlayed++;
            if (IsLocal) GameAudio.PlayLocal(footstep, volume * ownFootstepVolume);
            else GameAudio.Play(footstep, transform.position, volume);
        }

        private void PlayLanding(float fallSeconds)
        {
            LandingsPlayed++;
            float volume = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(0.3f, 1.2f, fallSeconds));
            if (land == null) { PlayStep(volume); return; }
            if (IsLocal) GameAudio.PlayLocal(land, volume * ownFootstepVolume);
            else GameAudio.Play(land, transform.position, volume);
        }

        private void OnHealthChanged(float current, float max)
        {
            float previous = lastHealth;
            lastHealth = current;
            if (previous < 0f || current <= 0f || previous - current < minimumHurtDamage || !Alive || Time.time < nextHurt) return;
            nextHurt = Time.time + hurtCooldown;
            Say(voice != null ? voice.hurt : null);
        }

        private void OnLifeStateChanged(PlayerLifeState from, PlayerLifeState to)
        {
            if (health != null) lastHealth = health.CurrentHealth;
            if (to == PlayerLifeState.Downed && from != PlayerLifeState.Downed) Say(voice != null ? voice.downed : null);
            else if (to == PlayerLifeState.Dead && from != PlayerLifeState.Dead) Say(voice != null ? voice.death : null);
        }

        private void Say(SoundEvent line)
        {
            if (line == null) return;
            GameAudio.Stop(voiceHandle, 0.05f);
            voiceHandle = IsLocal ? GameAudio.PlayLocal(line, 0.8f) : GameAudio.Play(line, Head, 1f, transform);
            VoiceLinesPlayed++;
        }

        private void StartBreath()
        {
            breathing = true;
            if (downedBreath == null) return;
            GameAudio.Stop(breathHandle, 0f);
            breathHandle = IsLocal ? GameAudio.PlayLocal(downedBreath) : GameAudio.Play(downedBreath, Head, 1f, transform);
        }

        private void StopBreath()
        {
            breathing = false;
            GameAudio.Stop(breathHandle, 0.4f);
            breathHandle = default;
        }
    }
}
