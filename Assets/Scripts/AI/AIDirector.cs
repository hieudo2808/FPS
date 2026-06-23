using System.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

namespace FPS
{
    public enum GamePhase { BUILD, PEAK, RELAX }

    public class AIDirector : NetworkBehaviour
    {
        public static AIDirector Instance { get; private set; }

        [Header("Pacing Settings")]
        [SerializeField] private float buildDuration = 45f;
        [SerializeField] private float peakDuration = 15f;
        [SerializeField] private float relaxDuration = 20f;

        [Header("Spawn Settings")]
        [SerializeField] private float baseSpawnInterval = 2f;
        [SerializeField] private int maxZombiesAlive = 30;
        [SerializeField] private float spawnIntervalMin = 0.5f;

        [Header("Learning Rate")]
        [SerializeField] private float learningRate = 0.01f;
        [SerializeField] private float maxHPModifier = 2f;
        [SerializeField] private float maxSpeedModifier = 1.5f;
        [SerializeField] private float maxDamageModifier = 1.5f;

        [Header("UI References (Optional)")]
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI zombieCountText;
        [SerializeField] private GameObject waveAnnouncementPanel;
        [SerializeField] private TextMeshProUGUI announcementText;

        [Header("Special Infected")]
        [SerializeField] private bool enableSpecialInfected = true;
        [SerializeField] private float specialSpawnChance = 0.15f;

        [Header("Smart Spawning")]
        [SerializeField] private bool useSmartSpawning = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        private NetworkVariable<GamePhase> networkPhase = new NetworkVariable<GamePhase>(
            GamePhase.BUILD, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
        );

        private NetworkVariable<int> networkZombiesAlive = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
        );

        private NetworkVariable<int> networkTotalKills = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
        );

        private float phaseTimer;
        private float intensity;
        private float spawnTimer;

        private float hpModifier = 1f;
        private float speedModifier = 1f;
        private float damageModifier = 1f;

        public GamePhase CurrentPhase => networkPhase.Value;
        public float Intensity => intensity;
        public int ZombiesAlive => networkZombiesAlive.Value;
        public int TotalKills => networkTotalKills.Value;
        public float HPModifier => hpModifier;
        public float SpeedModifier => speedModifier;
        public float DamageModifier => damageModifier;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            networkPhase.OnValueChanged += OnPhaseChanged;
            networkZombiesAlive.OnValueChanged += UpdateZombieCountUI;
            networkTotalKills.OnValueChanged += UpdateZombieCountUI;

            UpdatePhaseUI(networkPhase.Value);
            UpdateZombieCountUI(0, 0);

            if (IsServer)
                StartCoroutine(StartFirstWave());
        }

        public override void OnNetworkDespawn()
        {
            networkPhase.OnValueChanged -= OnPhaseChanged;
            networkZombiesAlive.OnValueChanged -= UpdateZombieCountUI;
            networkTotalKills.OnValueChanged -= UpdateZombieCountUI;
        }

        private void Update()
        {
            if (!IsServer) return;

            UpdatePacing();
            UpdateSpawning();

            intensity -= Time.deltaTime * 2f;
            intensity = Mathf.Clamp(intensity, 0f, 100f);
        }

        private void UpdatePacing()
        {
            phaseTimer += Time.deltaTime;

            switch (networkPhase.Value)
            {
                case GamePhase.BUILD:
                    if (intensity >= 80f || phaseTimer >= buildDuration)
                        TransitionTo(GamePhase.PEAK);
                    break;

                case GamePhase.PEAK:
                    if (phaseTimer >= peakDuration)
                        TransitionTo(GamePhase.RELAX);
                    break;

                case GamePhase.RELAX:
                    if (networkZombiesAlive.Value <= 2 || phaseTimer >= relaxDuration)
                        TransitionTo(GamePhase.BUILD);
                    break;
            }
        }

        private void TransitionTo(GamePhase newPhase)
        {
            networkPhase.Value = newPhase;
            phaseTimer = 0f;

            if (showDebugLogs)
                Debug.Log($"[AIDirector] Phase → {newPhase}");
        }

        private IEnumerator StartFirstWave()
        {
            yield return new WaitForSeconds(3f);
            TransitionTo(GamePhase.BUILD);
        }

        private void UpdateSpawning()
        {
            if (networkPhase.Value == GamePhase.RELAX) return;
            if (networkZombiesAlive.Value >= maxZombiesAlive) return;

            spawnTimer += Time.deltaTime;
            float interval = GetSpawnInterval();

            if (spawnTimer >= interval)
            {
                spawnTimer = 0f;
                SpawnZombie();
            }
        }

        private float GetSpawnInterval()
        {
            float interval = baseSpawnInterval;
            if (networkPhase.Value == GamePhase.PEAK) interval *= 0.5f;

            int playerCount = PlayerProfiler.Instance?.PlayerCount ?? 1;
            interval /= (1f + (playerCount - 1) * 0.3f);

            return Mathf.Max(interval, spawnIntervalMin);
        }

        private void SpawnZombie()
        {
            if (networkPhase.Value == GamePhase.PEAK && enableSpecialInfected && Random.value < specialSpawnChance)
            {
                if (TrySpawnSpecial()) return;
            }

            GameObject zombie = null;

            if (useSmartSpawning && TeamAnalyzer.Instance != null)
            {
                var isolated = TeamAnalyzer.Instance.GetMostIsolatedPlayer();
                if (isolated != null && Random.value < 0.4f)
                    zombie = ZombieFactory.Instance.SpawnZombieBehindPlayer(isolated.playerIndex, hpModifier, speedModifier, damageModifier);
                else
                    zombie = ZombieFactory.Instance.SpawnZombieAtSmartPosition(hpModifier, speedModifier, damageModifier);
            }
            else
            {
                zombie = ZombieFactory.Instance.SpawnZombieAtRandomPoint(hpModifier, speedModifier, damageModifier);
            }

            if (zombie != null)
            {
                networkZombiesAlive.Value++;
                intensity += 5f;
            }
        }

        private bool TrySpawnSpecial()
        {
            if (SpecialInfectedRegistry.Instance == null || !SpecialInfectedRegistry.Instance.CanSpawnSpecial())
                return false;

            Vector3 pos = InfluenceMapManager.Instance != null
                ? InfluenceMapManager.Instance.GetBestSpawnPosition()
                : ZombieRegistry.Instance.GetSpawnPosition();

            GameObject special = SpecialInfectedRegistry.Instance.SpawnSpecial(pos);

            if (special != null)
            {
                var netObj = special.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null && !netObj.IsSpawned)
                    netObj.Spawn(true);

                networkZombiesAlive.Value++;
                intensity += 15f;

                ShowAnnouncementClientRpc("SPECIAL INCOMING!");
                return true;
            }

            return false;
        }

        public void OnZombieDied()
        {
            if (!IsServer) return;

            networkZombiesAlive.Value = Mathf.Max(0, networkZombiesAlive.Value - 1);
            networkTotalKills.Value++;
            intensity -= 3f;

            UpdateLearningModifiers();
        }

        private void UpdateLearningModifiers()
        {
            if (PlayerProfiler.Instance == null) return;

            PlayerProfile carry = PlayerProfiler.Instance.GetCarryPlayer();
            if (carry == null) return;

            float performanceScore =
                carry.headshotRatio * 0.4f
                + Mathf.Min(carry.totalKills / 100f, 1f) * 0.3f
                + (carry.avgReactionTime > 0 ? Mathf.Min(1f / carry.avgReactionTime, 1f) * 0.3f : 0f);

            if (performanceScore > 0.3f)
            {
                hpModifier     = Mathf.Min(hpModifier     + learningRate,        maxHPModifier);
                speedModifier  = Mathf.Min(speedModifier  + learningRate * 0.5f, maxSpeedModifier);
                damageModifier = Mathf.Min(damageModifier + learningRate,         maxDamageModifier);
            }

            if (carry.isCamping && carry.campingDuration > 15f)
                speedModifier = Mathf.Min(speedModifier + learningRate * 2f, maxSpeedModifier);

            if (carry.headshotRatio > 0.5f)
                hpModifier = Mathf.Min(hpModifier + learningRate * 2f, maxHPModifier);
        }

        private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            UpdatePhaseUI(newPhase);

            if (newPhase == GamePhase.PEAK)  ShowAnnouncement("INCOMING HORDE!");
            else if (newPhase == GamePhase.RELAX) ShowAnnouncement("CLEAR!");
        }

        private void UpdatePhaseUI(GamePhase phase)
        {
            if (phaseText != null)
                phaseText.text = $"Phase: {phase}";
        }

        private void UpdateZombieCountUI(int previousValue, int newValue)
        {
            if (zombieCountText != null)
                zombieCountText.text = $"Zombies: {networkZombiesAlive.Value} | Kills: {networkTotalKills.Value}";
        }

        private void ShowAnnouncement(string message)
        {
            if (announcementText != null)
                announcementText.text = message;

            if (waveAnnouncementPanel != null)
            {
                waveAnnouncementPanel.SetActive(true);
                StartCoroutine(HideAnnouncementAfterDelay(2f));
            }
        }

        [ClientRpc]
        private void ShowAnnouncementClientRpc(string message)
        {
            ShowAnnouncement(message);
        }

        private IEnumerator HideAnnouncementAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (waveAnnouncementPanel != null)
                waveAnnouncementPanel.SetActive(false);
        }
    }
}