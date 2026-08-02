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

        [Header("Static profile overrides")]
        [SerializeField] private StaticDifficultyProfileAsset[] profileOverrides;

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
            
            GameLog.Info(() => $"[DifficultyManager] Difficulty set to {level}. HP: x{stats.hpMultiplier}, Damage: x{stats.damageMultiplier}, Speed: x{stats.speedMultiplier}, RubberBanding: {stats.enableRubberBanding}");
        }

        public void SetDifficulty(DifficultyLevel level)
        {
            if (!IsServer)
            {
                GameLog.Warning("[DifficultyManager] Only server can change difficulty.");
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
            return StaticDifficultyProfiles.TryGetOverride(profileOverrides, level, out DifficultyStats overrideStats)
                ? overrideStats
                : StaticDifficultyProfiles.Get(level);
        }
    }
}
