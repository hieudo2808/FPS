using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using UniBT;

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
            DisableBehaviorTreeBrain();
        }

        public override void UseAbility()
        {
            if (!CanRunServerLogic()) return;

            if (!isScreaming)
            {
                StartCoroutine(ScreamRoutine());
                lastAbilityTime = Time.time;
            }
        }

        private IEnumerator ScreamRoutine()
        {
            isScreaming = true;
            
            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = true;
            
            if (animator != null)
                animator.SetTrigger(ScreamTrigger);
            
            if (screamSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(screamSound, screamVolume);
            
            yield return new WaitForSeconds(screamDuration);
            
            if (ZombieFactory.Instance != null)
            {
                for (int i = 0; i < zombiesToSpawn; i++)
                {
                    Vector3 offset = UnityEngine.Random.insideUnitSphere * 8f;
                    offset.y = 0;
                    Vector3 spawnPos = transform.position + offset;

                    if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                    {
                        ZombieFactory.Instance.SpawnZombieAtFairPressurePosition(hit.position, Quaternion.identity);
                    }
                }
            }
            
            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = false;
            
            isScreaming = false;
        }

        private void DisableBehaviorTreeBrain()
        {
            BehaviorTree behaviorTree = GetComponent<BehaviorTree>();
            if (behaviorTree != null)
                behaviorTree.enabled = false;
        }
    }
}
