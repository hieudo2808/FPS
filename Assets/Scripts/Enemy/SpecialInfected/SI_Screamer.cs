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

        // Public properties for BT nodes
        public bool IsScreaming => isScreaming;
        public bool IsAbilityReady => abilityReady;

        protected override void Start()
        {
            base.Start();
            specialType = SpecialType.Screamer;
            allowedInSoloMode = true;
        }

        // Override to disable base class auto-ability trigger (BT controls this)
        protected override void Update()
        {
            // Intentionally empty - BT handles ability triggering
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
            
            // Stop moving
            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = true;
            
            // Play scream animation
            if (animator != null)
                animator.SetTrigger(ScreamTrigger);
            
            // Play scream sound
            if (screamSound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFXSound(screamSound, screamVolume);
            }
            
            Debug.Log("[Screamer] SCREAMING! Calling horde!");
            
            // Wait for scream animation
            yield return new WaitForSeconds(screamDuration);
            
            // Spawn zombies around screamer
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
            
            // Resume movement
            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = false;
            
            isScreaming = false;
        }
    }
}
