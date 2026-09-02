using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Scene-authored server service that converts infection cough pulses into
    /// short-lived target priorities for common infected. It never spreads infection.
    /// </summary>
    public sealed class InfectionThreatService : NetworkBehaviour
    {
        private struct ThreatPulse
        {
            public Vector3 position;
            public float radius;
            public float expiresAt;
            public PlayerInfectionController source;
        }

        public static InfectionThreatService Instance { get; private set; }

        [SerializeField, Min(0.1f)] private float sourceLifetime = 3f;
        private readonly List<ThreatPulse> pulses = new(8);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            PlayerInfectionController.GlobalInfectionNoiseServer += HandleNoiseServer;
        }

        private void OnDisable()
        {
            PlayerInfectionController.GlobalInfectionNoiseServer -= HandleNoiseServer;
            if (Instance == this)
                Instance = null;
            pulses.Clear();
        }

        private void HandleNoiseServer(
            Vector3 position,
            float radius,
            PlayerInfectionController source)
        {
            if (source == null || radius <= 0f)
                return;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
                return;

            float expiresAt = Time.time + sourceLifetime;
            for (int i = 0; i < pulses.Count; i++)
            {
                if (pulses[i].source != source)
                    continue;
                pulses[i] = new ThreatPulse
                {
                    position = position,
                    radius = radius,
                    expiresAt = expiresAt,
                    source = source
                };
                return;
            }

            pulses.Add(new ThreatPulse
            {
                position = position,
                radius = radius,
                expiresAt = expiresAt,
                source = source
            });
        }

        public bool TryGetPriorityTarget(Vector3 enemyPosition, out Transform target)
        {
            target = null;
            float bestDistance = float.MaxValue;
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                ThreatPulse pulse = pulses[i];
                if (pulse.source == null || Time.time > pulse.expiresAt)
                {
                    pulses.RemoveAt(i);
                    continue;
                }

                float distance = (enemyPosition - pulse.position).sqrMagnitude;
                if (distance > pulse.radius * pulse.radius || distance >= bestDistance)
                    continue;

                PlayerHealth health = pulse.source.GetComponent<PlayerHealth>();
                if (health == null || health.IsDead || health.LifeState != PlayerLifeState.Alive)
                    continue;

                bestDistance = distance;
                target = pulse.source.transform;
            }

            return target != null;
        }
    }
}
