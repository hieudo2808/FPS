using Unity.Netcode;
using UnityEngine;
using System;

namespace FPS
{
    public enum DifficultyLevel
    {
        Easy,
        Medium,
        Hard,
        Pandemonium
    }

    [Serializable]
    public struct DifficultyStats
    {
        public float hpMultiplier;
        public float damageMultiplier;
        public float speedMultiplier;
        public int maxConcurrentAttackers;
        public float spawnIntervalMultiplier;
        public float maxAliveMultiplier;
        public float specialSpawnChance;
        public bool enableRubberBanding;
    }

    public class DifficultyManager : NetworkBehaviour
    {
        public static DifficultyManager Instance { get; private set; }

        public NetworkVariable<DifficultyLevel> CurrentDifficulty = new NetworkVariable<DifficultyLevel>(
            DifficultyLevel.Medium, 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Server
        );

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
                Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && NetworkGameManager.Instance != null)
            {
                CurrentDifficulty.Value = NetworkGameManager.Instance.SelectedDifficulty;
            }

            CurrentDifficulty.OnValueChanged += HandleDifficultyChanged;
            
            // Client and Server need to apply local states like RubberBanding System based on the synced difficulty
            ApplyDifficulty(CurrentDifficulty.Value);
        }

        public override void OnNetworkDespawn()
        {
            CurrentDifficulty.OnValueChanged -= HandleDifficultyChanged;
        }

        private void HandleDifficultyChanged(DifficultyLevel previous, DifficultyLevel current)
        {
            ApplyDifficulty(current);
        }

        private void ApplyDifficulty(DifficultyLevel level)
        {
            DifficultyStats stats = GetStats(level);
            
            if (RubberBandingSystem.HasInstance)
            {
                RubberBandingSystem.Instance.isEnabled = stats.enableRubberBanding;
            }
            
            Debug.Log($"[DifficultyManager] Difficulty set to {level}. HP: x{stats.hpMultiplier}, Damage: x{stats.damageMultiplier}, Speed: x{stats.speedMultiplier}, RubberBanding: {stats.enableRubberBanding}");
        }

        public void SetDifficulty(DifficultyLevel level)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[DifficultyManager] Only server can change difficulty.");
                return;
            }
            CurrentDifficulty.Value = level;
        }

        public DifficultyStats GetCurrentStats()
        {
            return GetStats(CurrentDifficulty.Value);
        }

        public DifficultyStats GetStats(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Easy:
                    return new DifficultyStats
                    {
                        hpMultiplier = 0.5f,
                        damageMultiplier = 0.5f,
                        speedMultiplier = 0.8f,
                        maxConcurrentAttackers = 2,
                        spawnIntervalMultiplier = 1.25f,
                        maxAliveMultiplier = 0.7f,
                        specialSpawnChance = 0.05f,
                        enableRubberBanding = false
                    };
                case DifficultyLevel.Hard:
                    return new DifficultyStats
                    {
                        hpMultiplier = 1.5f,
                        damageMultiplier = 1.5f,
                        speedMultiplier = 1.2f,
                        maxConcurrentAttackers = 4,
                        spawnIntervalMultiplier = 0.75f,
                        maxAliveMultiplier = 1.25f,
                        specialSpawnChance = 0.2f,
                        enableRubberBanding = true
                    };
                case DifficultyLevel.Pandemonium:
                    return new DifficultyStats
                    {
                        hpMultiplier = 3.0f,
                        damageMultiplier = 2.0f,
                        speedMultiplier = 1.5f,
                        maxConcurrentAttackers = 6,
                        spawnIntervalMultiplier = 0.5f,
                        maxAliveMultiplier = 1.75f,
                        specialSpawnChance = 0.35f,
                        enableRubberBanding = true
                    };
                case DifficultyLevel.Medium:
                default:
                    return new DifficultyStats
                    {
                        hpMultiplier = 1.0f,
                        damageMultiplier = 1.0f,
                        speedMultiplier = 1.0f,
                        maxConcurrentAttackers = 3,
                        spawnIntervalMultiplier = 1.0f,
                        maxAliveMultiplier = 1.0f,
                        specialSpawnChance = 0.15f,
                        enableRubberBanding = false
                    };
            }
        }
    }
}
