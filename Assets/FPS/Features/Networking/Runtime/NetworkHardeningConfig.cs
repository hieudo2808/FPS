using System;
using UnityEngine;

namespace FPS
{
    public static class NetworkHardeningRuntime
    {
        public static NetworkHardeningSettings Current { get; private set; } = NetworkHardeningSettings.Default;

        public static void Apply(NetworkHardeningSettings settings) => Current = settings;
        public static void Reset() => Current = NetworkHardeningSettings.Default;
    }

    [CreateAssetMenu(fileName = "NetworkHardeningConfig", menuName = "FPS/Networking/Hardening Config")]
    public sealed class NetworkHardeningConfig : ScriptableObject
    {
        [Header("Session")]
        [SerializeField, Min(1f)] private float operationTimeoutSeconds = 20f;
        [SerializeField, Min(1f)] private float sceneLoadTimeoutSeconds = 45f;
        [SerializeField, Min(1f)] private float reconnectGraceSeconds = 60f;
        [SerializeField, Min(0f)] private float reconnectTransportGraceSeconds = 5f;
        [SerializeField, Min(1)] private int maxPlayers = 4;

        [Header("Input and hit validation")]
        [SerializeField, Range(0.02f, 0.5f)] private float inputSilenceSeconds = 0.1f;
        [SerializeField, Min(0)] private int maxFutureInputTicks = 2;
        [SerializeField, Min(1)] private int maxPastInputTicks = 60;
        [SerializeField, Range(0.05f, 0.5f)] private float maxRewindSeconds = 0.25f;
        [SerializeField, Range(0f, 0.2f)] private float rewindJitterMarginSeconds = 0.03f;

        [Header("Replication thresholds")]
        [SerializeField, Range(0.02f, 0.3f)] private float ownerCorrectionSmoothSeconds = 0.1f;
        [SerializeField, Min(0.5f)] private float ownerHardSnapDistance = 2f;
        [SerializeField, Range(0.001f, 0.5f)] private float enemyPositionThreshold = 0.05f;
        [SerializeField, Range(0.01f, 10f)] private float enemyRotationThresholdDegrees = 1f;

        [Header("Abuse limits")]
        [SerializeField, Min(1)] private int pickupRequestsPerSecond = 10;
        [SerializeField, Min(1)] private int reconnectAttemptsPerWindow = 3;
        [SerializeField, Min(1f)] private float reconnectAttemptWindowSeconds = 10f;

        public NetworkHardeningSettings ToSettings()
        {
            return new NetworkHardeningSettings(
                operationTimeoutSeconds,
                sceneLoadTimeoutSeconds,
                reconnectGraceSeconds,
                reconnectTransportGraceSeconds,
                maxPlayers,
                inputSilenceSeconds,
                maxFutureInputTicks,
                maxPastInputTicks,
                maxRewindSeconds,
                rewindJitterMarginSeconds,
                ownerCorrectionSmoothSeconds,
                ownerHardSnapDistance,
                enemyPositionThreshold,
                enemyRotationThresholdDegrees,
                pickupRequestsPerSecond,
                reconnectAttemptsPerWindow,
                reconnectAttemptWindowSeconds);
        }
    }

    public readonly struct NetworkHardeningSettings
    {
        public static readonly NetworkHardeningSettings Default = new(
            20f, 45f, 60f, 5f, 4, 0.1f, 2, 60, 0.25f, 0.03f,
            0.1f, 2f, 0.05f, 1f, 10, 3, 10f);

        public readonly float OperationTimeoutSeconds;
        public readonly float SceneLoadTimeoutSeconds;
        public readonly float ReconnectGraceSeconds;
        public readonly float ReconnectTransportGraceSeconds;
        public float ReconnectReservationSeconds => ReconnectGraceSeconds + ReconnectTransportGraceSeconds;
        public readonly int MaxPlayers;
        public readonly float InputSilenceSeconds;
        public readonly int MaxFutureInputTicks;
        public readonly int MaxPastInputTicks;
        public readonly float MaxRewindSeconds;
        public readonly float RewindJitterMarginSeconds;
        public readonly float OwnerCorrectionSmoothSeconds;
        public readonly float OwnerHardSnapDistance;
        public readonly float EnemyPositionThreshold;
        public readonly float EnemyRotationThresholdDegrees;
        public readonly int PickupRequestsPerSecond;
        public readonly int ReconnectAttemptsPerWindow;
        public readonly float ReconnectAttemptWindowSeconds;

        public NetworkHardeningSettings(
            float operationTimeoutSeconds,
            float sceneLoadTimeoutSeconds,
            float reconnectGraceSeconds,
            float reconnectTransportGraceSeconds,
            int maxPlayers,
            float inputSilenceSeconds,
            int maxFutureInputTicks,
            int maxPastInputTicks,
            float maxRewindSeconds,
            float rewindJitterMarginSeconds,
            float ownerCorrectionSmoothSeconds,
            float ownerHardSnapDistance,
            float enemyPositionThreshold,
            float enemyRotationThresholdDegrees,
            int pickupRequestsPerSecond,
            int reconnectAttemptsPerWindow,
            float reconnectAttemptWindowSeconds)
        {
            OperationTimeoutSeconds = Mathf.Max(1f, operationTimeoutSeconds);
            SceneLoadTimeoutSeconds = Mathf.Max(1f, sceneLoadTimeoutSeconds);
            ReconnectGraceSeconds = Mathf.Max(1f, reconnectGraceSeconds);
            ReconnectTransportGraceSeconds = Mathf.Max(0f, reconnectTransportGraceSeconds);
            MaxPlayers = Mathf.Max(1, maxPlayers);
            InputSilenceSeconds = Mathf.Clamp(inputSilenceSeconds, 0.02f, 0.5f);
            MaxFutureInputTicks = Mathf.Max(0, maxFutureInputTicks);
            MaxPastInputTicks = Mathf.Max(1, maxPastInputTicks);
            MaxRewindSeconds = Mathf.Clamp(maxRewindSeconds, 0.05f, 0.5f);
            RewindJitterMarginSeconds = Mathf.Clamp(rewindJitterMarginSeconds, 0f, 0.2f);
            OwnerCorrectionSmoothSeconds = Mathf.Clamp(ownerCorrectionSmoothSeconds, 0.02f, 0.3f);
            OwnerHardSnapDistance = Mathf.Max(0.5f, ownerHardSnapDistance);
            EnemyPositionThreshold = Mathf.Clamp(enemyPositionThreshold, 0.001f, 0.5f);
            EnemyRotationThresholdDegrees = Mathf.Clamp(enemyRotationThresholdDegrees, 0.01f, 10f);
            PickupRequestsPerSecond = Mathf.Max(1, pickupRequestsPerSecond);
            ReconnectAttemptsPerWindow = Mathf.Max(1, reconnectAttemptsPerWindow);
            ReconnectAttemptWindowSeconds = Mathf.Max(1f, reconnectAttemptWindowSeconds);
        }
    }
}
