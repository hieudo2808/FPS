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

        [Header("Adaptive Director (opt-in)")]
        [SerializeField] private bool enableAdaptiveDirector = false;
        [SerializeField] private bool adaptiveObserveOnly = true;
        [SerializeField] private AdaptiveDirectorPolicyAsset adaptiveDirectorPolicy;
        [SerializeField] private AdaptiveDifficultyPolicyAsset adaptiveDifficultyPolicy;

        private NetworkVariable<GamePhase> networkPhase = new NetworkVariable<GamePhase>(
            GamePhase.BUILD, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
        );

        private NetworkVariable<int> networkZombiesAlive = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
        );

        private NetworkVariable<int> networkTotalKills = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
        );

        private NetworkVariable<DirectorPhase> networkAdaptivePhase = new NetworkVariable<DirectorPhase>(
            DirectorPhase.Calm, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<float> networkAdaptiveMultiplier = new NetworkVariable<float>(
            1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private float phaseTimer;
        private float intensity;
        private float spawnTimer;
        private double forcedCrescendoUntil;

        private float hpModifier = 1f;
        private float speedModifier = 1f;
        private float damageModifier = 1f;
        private ZombieFactory subscribedZombieFactory;
        private SpecialInfectedRegistry subscribedSpecialRegistry;
        private GameObject lastCountedSpawnedEnemy;
        private int lastCountedSpawnFrame = -1;
        private GamePhase localPhase = GamePhase.BUILD;
        private int localZombiesAlive;
        private int localTotalKills;
        private DirectorPhase localAdaptivePhase = DirectorPhase.Calm;
        private DirectorStateMachine adaptiveStateMachine;
        private DynamicDifficultyEvaluator adaptiveDifficultyEvaluator;
        private readonly SpawnController adaptiveSpawnController = new SpawnController();
        private DirectorDecision adaptiveDecision;
        private float adaptiveDifficultyMultiplier = 1f;

        private bool HasBoundNetworkState => IsSpawned
            && NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsListening;

        public GamePhase CurrentPhase => HasBoundNetworkState ? networkPhase.Value : localPhase;
        public float Intensity => intensity;
        public int ZombiesAlive => HasBoundNetworkState ? networkZombiesAlive.Value : localZombiesAlive;
        public int TotalKills => HasBoundNetworkState ? networkTotalKills.Value : localTotalKills;
        public float HPModifier => hpModifier;
        public float AdaptiveDifficultyMultiplier => HasBoundNetworkState
            ? networkAdaptiveMultiplier.Value
            : adaptiveDifficultyMultiplier;
        public bool AdaptiveDirectorEnabled => enableAdaptiveDirector;
        public bool AdaptiveObserveOnly => adaptiveObserveOnly;
        public DirectorPhase AdaptivePhase => HasBoundNetworkState
            ? networkAdaptivePhase.Value
            : localAdaptivePhase;
        public DirectorSpawnAnchorType CurrentSpawnAnchorTypes
        {
            get
            {
                if (FactoryMissionController.Instance != null
                    && FactoryMissionController.Instance.State == FactoryMissionState.ExtractionActive)
                {
                    return DirectorSpawnAnchorType.Common
                        | DirectorSpawnAnchorType.Horde
                        | DirectorSpawnAnchorType.Finale;
                }

                if (GetDirectorTime() < forcedCrescendoUntil || CurrentPhase == GamePhase.PEAK)
                    return DirectorSpawnAnchorType.Common | DirectorSpawnAnchorType.Horde;

                return DirectorSpawnAnchorType.Ambient | DirectorSpawnAnchorType.Common;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void SetVerificationMaxZombiesAlive(int count)
        {
            maxZombiesAlive = Mathf.Max(1, count);
        }

        public void ConfigureAdaptiveForVerification(bool enabled, bool observeOnly)
        {
            enableAdaptiveDirector = enabled;
            adaptiveObserveOnly = observeOnly;
            InitializeAdaptiveSystems();
        }
#endif
        public float SpeedModifier => speedModifier;
        public float DamageModifier => damageModifier;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            InitializeAdaptiveSystems();
        }

        private void OnEnable()
        {
            EnsureSpawnEventSubscriptions();
        }

        private void OnDisable()
        {
            UnsubscribeSpawnEvents();
            lastCountedSpawnedEnemy = null;
            lastCountedSpawnFrame = -1;
        }

        public override void OnNetworkSpawn()
        {
            networkPhase.OnValueChanged += OnPhaseChanged;
            networkZombiesAlive.OnValueChanged += UpdateZombieCountUI;
            networkTotalKills.OnValueChanged += UpdateZombieCountUI;
            EnsureSpawnEventSubscriptions();

            UpdatePhaseUI(CurrentPhase);
            UpdateZombieCountUI(0, 0);

            if (CanRunServerLogic() && NetworkMatchStateManager.IsGameplayActive && !enableAdaptiveDirector)
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
            if (!CanRunServerLogic()) return;
            if (!NetworkMatchStateManager.IsGameplayActive) return;

            EnsureSpawnEventSubscriptions();
            if (enableAdaptiveDirector)
            {
                UpdateAdaptiveDirector();
                if (!adaptiveObserveOnly)
                    UpdateAdaptiveSpawning();
                else
                {
                    UpdatePacing();
                    UpdateSpawning();
                }
            }
            else
            {
                UpdatePacing();
                UpdateSpawning();
            }

            intensity -= Time.deltaTime * 2f;
            intensity = Mathf.Clamp(intensity, 0f, 100f);
        }

        private void UpdatePacing()
        {
            phaseTimer += Time.deltaTime;

            switch (CurrentPhase)
            {
                case GamePhase.BUILD:
                    if (intensity >= 80f || phaseTimer >= buildDuration)
                        TransitionTo(GamePhase.PEAK);
                    break;

                case GamePhase.PEAK:
                    if (GetDirectorTime() >= forcedCrescendoUntil && phaseTimer >= peakDuration)
                        TransitionTo(GamePhase.RELAX);
                    break;

                case GamePhase.RELAX:
                    if (ZombiesAlive <= 2 || phaseTimer >= relaxDuration)
                        TransitionTo(GamePhase.BUILD);
                    break;
            }
        }

        private void TransitionTo(GamePhase newPhase)
        {
            SetPhase(newPhase);
            phaseTimer = 0f;

            if (showDebugLogs)
                GameLog.Info(() => $"[AIDirector] Phase -> {newPhase}");
        }

        public void RequestCrescendo(string reason, float minimumDurationSeconds)
        {
            if (!CanRunServerLogic())
                return;

            forcedCrescendoUntil = System.Math.Max(
                forcedCrescendoUntil,
                GetDirectorTime() + Mathf.Max(1f, minimumDurationSeconds));
            intensity = Mathf.Max(intensity, 85f);
            if (CurrentPhase != GamePhase.PEAK)
                TransitionTo(GamePhase.PEAK);

            if (showDebugLogs)
                GameLog.Info(() => $"[AIDirector] Crescendo requested: {reason}");
        }

        private IEnumerator StartFirstWave()
        {
            yield return new WaitForSeconds(3f);
            TransitionTo(GamePhase.BUILD);
        }

        private void UpdateSpawning()
        {
            if (!NetworkMatchStateManager.IsGameplayActive) return;
            if (CurrentPhase == GamePhase.RELAX) return;
            if (ZombiesAlive >= GetMaxZombiesAlive()) return;

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
            if (CurrentPhase == GamePhase.PEAK) interval *= 0.5f;
            interval *= GetDifficultyStats().spawnIntervalMultiplier;

            int playerCount = PlayerProfiler.Instance?.PlayerCount ?? 1;
            interval /= (1f + (playerCount - 1) * 0.3f);

            return Mathf.Max(interval, spawnIntervalMin);
        }

        private void SpawnZombie()
        {
            if (CurrentPhase == GamePhase.PEAK && enableSpecialInfected && Random.value < GetSpecialSpawnChance())
            {
                if (TrySpawnSpecial()) return;
            }

            GameObject zombie = null;

            if (useSmartSpawning)
            {
                zombie = ZombieFactory.Instance.SpawnZombieAtSmartPosition(hpModifier, speedModifier, damageModifier);
            }
            else
            {
                zombie = ZombieFactory.Instance.SpawnZombieAtRandomPoint(hpModifier, speedModifier, damageModifier);
            }

            if (zombie != null)
            {
                intensity += 5f;
            }
        }

        private void UpdateAdaptiveDirector()
        {
            InitializeAdaptiveSystems();
            DirectorStepResult result = adaptiveStateMachine.Advance(
                Time.deltaTime,
                BuildDirectorInput());
            adaptiveDecision = result.Decision;
            SetAdaptiveState(result.Phase, adaptiveDifficultyMultiplier);

            if (result.PhaseChanged)
            {
                SetPhase(MapToLegacyPhase(result.Phase));
                AdaptiveDirectorDiagnostics.Emit(
                    "director_phase_changed",
                    $"phase={result.Phase};legacyPhase={MapToLegacyPhase(result.Phase)}");
                if (showDebugLogs)
                    GameLog.Info(() => $"[AIDirector] Adaptive phase -> {result.Phase}");
            }

            if (!result.EnteredRelax)
                return;

            var metrics = NetworkGameManager.Instance?.AdaptiveMetrics;
            var samples = metrics?.BuildSamples();
            DifficultyLevel difficulty = DifficultyManager.Instance != null
                ? DifficultyManager.Instance.CurrentDifficulty.Value
                : DifficultyLevel.Medium;
            DynamicDifficultyEvaluation evaluation = adaptiveDifficultyEvaluator.Evaluate(
                difficulty,
                samples,
                relaxBoundary: true);
            adaptiveDifficultyMultiplier = evaluation.Multiplier;
            SetAdaptiveState(result.Phase, adaptiveDifficultyMultiplier);
            metrics?.ResetEncounter();
            AdaptiveDirectorDiagnostics.Emit(
                "difficulty_evaluated",
                $"score={evaluation.TeamPerformanceScore:F4};target={evaluation.TargetMultiplier:F4};"
                + $"multiplier={evaluation.Multiplier:F4};evidence={evaluation.HasEvidence};updated={evaluation.Updated}");

            if (showDebugLogs)
            {
                GameLog.Info(() => $"[AIDirector] Adaptive Relax score={evaluation.TeamPerformanceScore:F3}, "
                    + $"target={evaluation.TargetMultiplier:F3}, multiplier={evaluation.Multiplier:F3}, "
                    + $"evidence={evaluation.HasEvidence}");
            }
        }

        private void UpdateAdaptiveSpawning()
        {
            if (!NetworkMatchStateManager.IsGameplayActive || ZombieFactory.Instance == null)
                return;

            DifficultyStats stats = GetDifficultyStats();
            int playerCount = PlayerProfiler.Instance != null ? PlayerProfiler.Instance.PlayerCount : 1;
            SpawnDecision decision = adaptiveSpawnController.Decide(
                adaptiveDecision,
                stats,
                adaptiveDifficultyMultiplier,
                ZombiesAlive,
                maxZombiesAlive,
                playerCount,
                baseSpawnInterval,
                spawnIntervalMin,
                enableSpecialInfected,
                UnityEngine.Random.value);

            if (!decision.CanSpawn)
                return;

            AdaptiveDirectorDiagnostics.Emit(
                "spawn_decision",
                $"phase={adaptiveDecision.Phase};canSpawn={decision.CanSpawn};maxAlive={decision.MaxAlive};"
                + $"interval={decision.IntervalSeconds:F4};dynamicMultiplier={adaptiveDifficultyMultiplier:F4}");

            spawnTimer += Time.deltaTime;
            if (spawnTimer < decision.IntervalSeconds)
                return;

            spawnTimer = 0f;
            AdaptiveDirectorDiagnostics.Emit(
                "special_spawn_roll",
                $"gate={adaptiveDecision.SpecialGateOpen};rollDecision={decision.SpawnSpecial};chance={decision.SpecialChance:F4}");
            if (decision.SpawnSpecial && TrySpawnSpecial())
                return;

            GameObject zombie = useSmartSpawning
                ? ZombieFactory.Instance.SpawnZombieAtSmartPosition(
                    adaptiveDifficultyMultiplier,
                    1f,
                    adaptiveDifficultyMultiplier)
                : ZombieFactory.Instance.SpawnZombieAtRandomPoint(
                    adaptiveDifficultyMultiplier,
                    1f,
                    adaptiveDifficultyMultiplier);
            if (zombie != null)
                intensity += 5f;
        }

        private DirectorInput BuildDirectorInput()
        {
            float weakestHealth01 = 1f;
            float separation01 = 0f;
            float idleSeconds = 0f;
            float recentDownedSeconds = 999f;

            if (PlayerProfiler.Instance != null)
            {
                foreach (PlayerProfile profile in PlayerProfiler.Instance.AllProfiles)
                {
                    if (profile == null || profile.cachedHealth != null && profile.cachedHealth.IsDead)
                        continue;

                    float maxHealth = profile.cachedHealth != null
                        ? Mathf.Max(1f, profile.cachedHealth.MaxHealth)
                        : 100f;
                    weakestHealth01 = Mathf.Min(weakestHealth01,
                        Mathf.Clamp01(profile.currentHealth / maxHealth));
                    separation01 = Mathf.Max(separation01,
                        Mathf.Clamp01(profile.distanceToNearestAlly / 30f));
                    idleSeconds = Mathf.Max(idleSeconds, profile.campingDuration);
                    if (profile.cachedHealth != null && profile.cachedHealth.LifeState == PlayerLifeState.Downed)
                        recentDownedSeconds = 0f;
                }
            }

            return new DirectorInput(
                weakestHealth01,
                separation01,
                idleSeconds,
                recentDownedSeconds,
                ZombiesAlive,
                intensity);
        }

        private void InitializeAdaptiveSystems()
        {
            if (adaptiveStateMachine == null)
            {
                adaptiveStateMachine = new DirectorStateMachine(
                    adaptiveDirectorPolicy != null
                        ? adaptiveDirectorPolicy.ToPolicy()
                        : DirectorPolicy.Default);
                adaptiveDecision = adaptiveStateMachine.GetDecision();
            }

            if (adaptiveDifficultyEvaluator == null)
            {
                adaptiveDifficultyEvaluator = new DynamicDifficultyEvaluator(
                    adaptiveDifficultyPolicy != null
                        ? adaptiveDifficultyPolicy.ToPolicy()
                        : DynamicDifficultyPolicy.Default);
            }
        }

        private static GamePhase MapToLegacyPhase(DirectorPhase phase)
        {
            switch (phase)
            {
                case DirectorPhase.Peak:
                    return GamePhase.PEAK;
                case DirectorPhase.Relax:
                    return GamePhase.RELAX;
                default:
                    return GamePhase.BUILD;
            }
        }

        private bool TrySpawnSpecial()
        {
            if (SpecialInfectedRegistry.Instance == null || !SpecialInfectedRegistry.Instance.CanSpawnSpecial())
                return false;

            if (!TryGetSpecialSpawnPosition(out Vector3 pos))
                return false;

            GameObject special = SpecialInfectedRegistry.Instance.SpawnSpecial(pos);

            if (special != null)
            {
                var netObj = special.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null && !netObj.IsSpawned)
                    netObj.Spawn(true);

                intensity += 15f;

                ShowAnnouncementClientRpc("SPECIAL INCOMING!");
                return true;
            }

            return false;
        }

        private bool TryGetSpecialSpawnPosition(out Vector3 position)
        {
            position = Vector3.zero;

            if (DirectorSpawnService.Instance != null
                && DirectorSpawnService.Instance.TryGetSpawnPosition(
                    DirectorSpawnAnchorType.Special | CurrentSpawnAnchorTypes,
                    out position))
            {
                return true;
            }

            if (InfluenceMapManager.Instance != null)
                return InfluenceMapManager.Instance.TryGetBestSpawnPosition(out position);

            bool hasProfiledPlayers = PlayerProfiler.Instance != null && PlayerProfiler.Instance.PlayerCount > 0;
            if (hasProfiledPlayers || ZombieRegistry.Instance == null)
                return false;

            return ZombieRegistry.Instance.TryGetSpawnPosition(out position);
        }

        public void OnZombieDied()
        {
            if (!CanRunServerLogic()) return;

            SetZombiesAlive(Mathf.Max(0, ZombiesAlive - 1));
            SetTotalKills(TotalKills + 1);
            intensity -= 3f;

            if (!enableAdaptiveDirector || adaptiveObserveOnly)
                UpdateLearningModifiers();
        }

        public void RegisterSpawnedEnemy(GameObject enemy)
        {
            if (!CanRunServerLogic()) return;
            if (enemy == null) return;
            if (enemy == lastCountedSpawnedEnemy && Time.frameCount == lastCountedSpawnFrame)
                return;

            lastCountedSpawnedEnemy = enemy;
            lastCountedSpawnFrame = Time.frameCount;
            SetZombiesAlive(ZombiesAlive + 1);
        }

        private void UpdateLearningModifiers()
        {
            if (PlayerProfiler.Instance == null) return;

            PlayerProfile carry = PlayerProfiler.Instance.GetCarryPlayer();
            if (carry == null) return;

            float performanceScore =
                carry.headshotRatio * 0.4f
                + Mathf.Min(carry.totalKills / 100f, 1f) * 0.3f;

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

        private void EnsureSpawnEventSubscriptions()
        {
            if (subscribedZombieFactory == null && ZombieFactory.HasInstance)
            {
                subscribedZombieFactory = ZombieFactory.Instance;
                subscribedZombieFactory.OnZombieSpawned += RegisterSpawnedEnemy;
            }

            if (subscribedSpecialRegistry == null && SpecialInfectedRegistry.Instance != null)
            {
                subscribedSpecialRegistry = SpecialInfectedRegistry.Instance;
                subscribedSpecialRegistry.OnSpecialSpawned += RegisterSpawnedEnemy;
            }
        }

        private void UnsubscribeSpawnEvents()
        {
            if (subscribedZombieFactory != null)
            {
                subscribedZombieFactory.OnZombieSpawned -= RegisterSpawnedEnemy;
                subscribedZombieFactory = null;
            }

            if (subscribedSpecialRegistry != null)
            {
                subscribedSpecialRegistry.OnSpecialSpawned -= RegisterSpawnedEnemy;
                subscribedSpecialRegistry = null;
            }
        }

        private int GetMaxZombiesAlive()
        {
            return Mathf.Max(1, Mathf.RoundToInt(maxZombiesAlive * GetDifficultyStats().maxAliveMultiplier));
        }

        private float GetSpecialSpawnChance()
        {
            if (DifficultyManager.Instance == null)
                return specialSpawnChance;

            return DifficultyManager.Instance.GetCurrentStats().specialSpawnChance;
        }

        private DifficultyStats GetDifficultyStats()
        {
            return DifficultyManager.Instance != null
                ? DifficultyManager.Instance.GetCurrentStats()
                : new DifficultyStats
                {
                    hpMultiplier = 1f,
                    damageMultiplier = 1f,
                    speedMultiplier = 1f,
                    maxConcurrentAttackers = 3,
                    spawnIntervalMultiplier = 1f,
                    maxAliveMultiplier = 1f,
                    specialSpawnChance = specialSpawnChance,
                    enableRubberBanding = false
                };
        }

        private bool CanRunServerLogic()
        {
            return IsServer || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        }

        private double GetDirectorTime()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                return NetworkManager.Singleton.ServerTime.Time;

            return Time.timeAsDouble;
        }

        private void SetPhase(GamePhase value)
        {
            if (HasBoundNetworkState)
                networkPhase.Value = value;
            else
                localPhase = value;
        }

        private void SetZombiesAlive(int value)
        {
            if (HasBoundNetworkState)
                networkZombiesAlive.Value = value;
            else
                localZombiesAlive = value;
        }

        private void SetTotalKills(int value)
        {
            if (HasBoundNetworkState)
                networkTotalKills.Value = value;
            else
                localTotalKills = value;
        }

        private void SetAdaptiveState(DirectorPhase phase, float multiplier)
        {
            if (HasBoundNetworkState)
            {
                networkAdaptivePhase.Value = phase;
                networkAdaptiveMultiplier.Value = Mathf.Clamp(multiplier, 0.6f, 1.5f);
            }
            else
            {
                localAdaptivePhase = phase;
                adaptiveDifficultyMultiplier = multiplier;
            }
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
                zombieCountText.text = $"Zombies: {ZombiesAlive} | Kills: {TotalKills}";
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
