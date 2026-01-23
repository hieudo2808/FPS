using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace FPS
{
    public class SI_Screamer : SpecialInfectedBase
    {
        [Header("Screamer Settings")]
        [SerializeField] private int zombiesToSpawn = 5;
        [SerializeField] private float screamDuration = 2f;
        
        [Header("Audio")]
        [SerializeField] private AudioClip screamSound;
        [SerializeField] private float screamVolume = 1f;
        
        private bool isScreaming = false;
        private static readonly int ScreamTrigger = Animator.StringToHash("Scream");

        public bool IsScreaming => isScreaming;
        public bool IsAbilityReady => abilityReady;

        protected override void Start()
        {
            base.Start();
            specialType = SpecialType.Screamer;
            allowedInSoloMode = true;
        }

        protected override void Update()
        {

        }

        public override void UseAbility()
        {
            if (!isScreaming)
            {
                StartCoroutine(ScreamRoutine());
                lastAbilityTime = Time.time; // Reset cooldown for BT
            }
        }

        private IEnumerator ScreamRoutine()
        {
            isScreaming = true;
            
            if (agent != null && agent.isOnNavMesh)
            
            if (animator != null)
            
            if (screamSound != null && AudioManager.Instance != null)
            
            Debug.Log("[Screamer] SCREAMING! Calling horde!");
            
            yield return new WaitForSeconds(screamDuration);
            
            if (ZombieFactory.Instance != null)
            {
                for (int i = 0; i < zombiesToSpawn; i++)
                {
                    Vector3 offset = Random.insideUnitSphere * 8f;
                    offset.y = 0;
                    Vector3 spawnPos = transform.position + offset;
                    
                    if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                    {
                        ZombieFactory.Instance.SpawnZombie(hit.position, Quaternion.identity);
                    }
                }
                
                Debug.Log($"[Screamer] Called {zombiesToSpawn} zombies!");
            }
            
            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = false;
            
            isScreaming = false;
        }
    }
}
