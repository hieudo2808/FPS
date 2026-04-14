using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class GameDirector : NetworkBehaviour
    {
        public static GameDirector Instance { get; private set; }

        [Tooltip("The dynamic HP multiplier for enemies based on player count.")]
        public float CurrentDifficultyMultiplier { get; private set; } = 1.0f;
        
        [Tooltip("True if elites and bosses should be spawned (requires 3+ players)")]
        public bool AllowEliteSpawns { get; private set; } = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
                
                // Calculate initially for the host
                RecalculateDifficulty();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
            
            if (Instance == this) Instance = null;
        }

        private void HandleClientConnected(ulong clientId)
        {
            RecalculateDifficulty();
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            RecalculateDifficulty();
        }

        private void RecalculateDifficulty()
        {
            int playerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
            
            // Dynamic Co-op Scaling Logic
            if (playerCount <= 1)
            {
                CurrentDifficultyMultiplier = 1.0f;
                AllowEliteSpawns = false;
                Debug.Log($"[GameDirector] 1 Player (Solo). Enemy HP Multiplier: {CurrentDifficultyMultiplier}x");
            }
            else if (playerCount == 2)
            {
                CurrentDifficultyMultiplier = 1.3f;
                AllowEliteSpawns = false;
                Debug.Log($"[GameDirector] 2 Players (Duo). Scaling Enemy HP to {CurrentDifficultyMultiplier}x");
            }
            else
            {
                // 3 or 4 players
                CurrentDifficultyMultiplier = 2.0f;
                AllowEliteSpawns = true;
                Debug.Log($"[GameDirector] {playerCount} Players (Squad). Scaling Enemy HP to {CurrentDifficultyMultiplier}x! Elites enabled!");
            }
            
            // Note: Since this is IsServer only, enemies will read CurrentDifficultyMultiplier when they spawn
            // to set their max health accordingly.
        }
    }
}
