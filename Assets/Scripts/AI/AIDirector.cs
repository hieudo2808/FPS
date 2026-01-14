using System.Collections;
using UnityEngine;
using TMPro;

namespace FPS
{
    public enum GamePhase { BUILD, PEAK, RELAX }

    public class AIDirector : MonoBehaviour
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
        [SerializeField] private bool showDebugLogs = true;
        
        // State
        private GamePhase currentPhase = GamePhase.BUILD;
        private float phaseTimer;
        private float intensity;
        private int zombiesAlive;
        private int totalKills;
        private float spawnTimer;
        
        // Learning modifiers
        private float hpModifier = 1f;
        private float speedModifier = 1f;
        private float damageModifier = 1f;
        
        // Properties
        public GamePhase CurrentPhase => currentPhase;
        public float Intensity => intensity;
        public int ZombiesAlive => zombiesAlive;
        public int TotalKills => totalKills;
        public float HPModifier => hpModifier;
        public float SpeedModifier => speedModifier;
        public float DamageModifier => damageModifier;

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

        private void Start()
        {
            // Start with BUILD phase after short delay
            StartCoroutine(StartFirstWave());
        }

        private IEnumerator StartFirstWave()
        {
            yield return new WaitForSeconds(3f);
            TransitionTo(GamePhase.BUILD);
        }

        private void Update()
        {
            UpdatePacing();
            UpdateSpawning();
            UpdateUI();
            
            // Intensity decay
            intensity -= Time.deltaTime * 2f;
            intensity = Mathf.Clamp(intensity, 0f, 100f);
        }

        private void UpdatePacing()
        {
            phaseTimer += Time.deltaTime;
            
            switch (currentPhase)
            {
                case GamePhase.BUILD:
                    if (intensity >= 80f || phaseTimer >= buildDuration)
                    {
                        TransitionTo(GamePhase.PEAK);
                    }
                    break;
                    
                case GamePhase.PEAK:
                    if (phaseTimer >= peakDuration)
                    {
                        TransitionTo(GamePhase.RELAX);
                    }
                    break;
                    
                case GamePhase.RELAX:
                    if (zombiesAlive <= 2 || phaseTimer >= relaxDuration)
                    {
                        TransitionTo(GamePhase.BUILD);
                    }
                    break;
            }
        }

        private void TransitionTo(GamePhase newPhase)
        {
            currentPhase = newPhase;
            phaseTimer = 0f;
            
            if (showDebugLogs)
                Debug.Log($"[AIDirector] Phase changed to: {newPhase}");
            
            // Show announcement
            if (newPhase == GamePhase.PEAK)
            {
                ShowAnnouncement("INCOMING HORDE!");
            }
            else if (newPhase == GamePhase.RELAX)
            {
                ShowAnnouncement("CLEAR!");
            }
        }

        private void UpdateSpawning()
        {
            if (currentPhase == GamePhase.RELAX) return;
            if (zombiesAlive >= maxZombiesAlive) return;
            
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
            
            // Faster spawning during PEAK
            if (currentPhase == GamePhase.PEAK)
            {
                interval *= 0.5f;
            }
            
            // Scale with player count
            int playerCount = PlayerProfiler.Instance?.PlayerCount ?? 1;
            interval /= (1f + (playerCount - 1) * 0.3f);
            
            return Mathf.Max(interval, spawnIntervalMin);
        }

        private void SpawnZombie()
        {
            if (ZombieFactory.Instance == null || ZombieRegistry.Instance == null)
            {
                Debug.LogError("[AIDirector] ZombieFactory or ZombieRegistry not found!");
                return;
            }
            
            // Try spawn special during PEAK
            if (currentPhase == GamePhase.PEAK && enableSpecialInfected && Random.value < specialSpawnChance)
            {
                if (TrySpawnSpecial())
                    return;
            }
            
            GameObject zombie;
            
            // Smart spawning: target isolated players or spawn behind team
            if (useSmartSpawning && TeamAnalyzer.Instance != null)
            {
                var isolated = TeamAnalyzer.Instance.GetMostIsolatedPlayer();
                if (isolated != null && Random.value < 0.4f)
                {
                    // Spawn near isolated player
                    zombie = ZombieFactory.Instance.SpawnZombieBehindPlayer(
                        isolated.playerIndex,
                        hpModifier, speedModifier, damageModifier
                    );
                }
                else
                {
                    // Smart spawn using influence map
                    zombie = ZombieFactory.Instance.SpawnZombieAtSmartPosition(
                        hpModifier, speedModifier, damageModifier
                    );
                }
            }
            else
            {
                zombie = ZombieFactory.Instance.SpawnZombieAtRandomPoint(
                    hpModifier, speedModifier, damageModifier
                );
            }
            
            if (zombie != null)
            {
                zombiesAlive++;
                intensity += 5f;
                
                // Subscribe to death event
                EnemyHealth health = zombie.GetComponent<EnemyHealth>();
                if (health != null)
                {
                    StartCoroutine(WaitForDeath(zombie, health));
                }
            }
        }

        private bool TrySpawnSpecial()
        {
            if (SpecialInfectedRegistry.Instance == null)
                return false;
            
            if (!SpecialInfectedRegistry.Instance.CanSpawnSpecial())
                return false;
            
            Vector3 pos;
            if (InfluenceMapManager.Instance != null)
            {
                pos = InfluenceMapManager.Instance.GetBestSpawnPosition();
            }
            else
            {
                pos = ZombieRegistry.Instance.GetSpawnPosition();
            }
            
            GameObject special = SpecialInfectedRegistry.Instance.SpawnSpecial(pos);
            
            if (special != null)
            {
                zombiesAlive++;
                intensity += 15f; // Specials add more intensity
                
                EnemyHealth health = special.GetComponent<EnemyHealth>();
                if (health != null)
                {
                    StartCoroutine(WaitForDeath(special, health));
                }
                
                ShowAnnouncement("SPECIAL INCOMING!");
                return true;
            }
            
            return false;
        }

        private IEnumerator WaitForDeath(GameObject zombie, EnemyHealth health)
        {
            while (zombie != null && !health.IsDead)
            {
                yield return null;
            }
            
            OnZombieDied();
        }

        public void OnZombieDied()
        {
            zombiesAlive = Mathf.Max(0, zombiesAlive - 1);
            totalKills++;
            intensity -= 3f;
            
            // Update learning modifiers
            UpdateLearningModifiers();
        }

        private void UpdateLearningModifiers()
        {
            if (PlayerProfiler.Instance == null) return;
            
            PlayerProfile carry = PlayerProfiler.Instance.GetCarryPlayer();
            if (carry == null) return;
            
            // Performance-based adjustment
            float performanceScore = carry.headshotRatio * 0.4f
                                   + Mathf.Min(carry.totalKills / 100f, 1f) * 0.3f
                                   + (carry.avgReactionTime > 0 ? Mathf.Min(1f / carry.avgReactionTime, 1f) * 0.3f : 0f);
            
            if (performanceScore > 0.3f) // Player doing well
            {
                hpModifier = Mathf.Min(hpModifier + learningRate, maxHPModifier);
                speedModifier = Mathf.Min(speedModifier + learningRate * 0.5f, maxSpeedModifier);
                damageModifier = Mathf.Min(damageModifier + learningRate, maxDamageModifier);
            }
            
            // Counter-play: camping players face faster zombies
            if (carry.isCamping && carry.campingDuration > 15f)
            {
                speedModifier = Mathf.Min(speedModifier + learningRate * 2f, maxSpeedModifier);
            }
            
            // Counter-play: high headshot = more HP
            if (carry.headshotRatio > 0.5f)
            {
                hpModifier = Mathf.Min(hpModifier + learningRate * 2f, maxHPModifier);
            }
            
            if (showDebugLogs && totalKills % 10 == 0)
            {
                Debug.Log($"[AIDirector] Modifiers - HP: {hpModifier:F2}x, Speed: {speedModifier:F2}x, Damage: {damageModifier:F2}x");
            }
        }

        private void UpdateUI()
        {
            if (phaseText != null)
            {
                phaseText.text = $"Phase: {currentPhase}";
            }
            
            if (zombieCountText != null)
            {
                zombieCountText.text = $"Zombies: {zombiesAlive} | Kills: {totalKills}";
            }
        }

        private void ShowAnnouncement(string message)
        {
            if (announcementText != null)
            {
                announcementText.text = message;
            }
            
            if (waveAnnouncementPanel != null)
            {
                waveAnnouncementPanel.SetActive(true);
                StartCoroutine(HideAnnouncementAfterDelay(2f));
            }
        }

        private IEnumerator HideAnnouncementAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (waveAnnouncementPanel != null)
            {
                waveAnnouncementPanel.SetActive(false);
            }
        }
    }
}
