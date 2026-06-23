using UnityEngine;
using UnityEngine.AI;
using UniBT;

namespace FPS.BT
{
    [System.Serializable]
    public class ChasePlayer : Action
    {
        [SerializeField] private float rotationSpeed = 5f;
        
        private NavMeshAgent agent;
        private Transform player;
        private Animator animator;
        private SI_Screamer screamer;

        protected override Status OnUpdate()
        {
            if (agent == null) agent = gameObject.GetComponent<NavMeshAgent>();
            if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (animator == null) animator = gameObject.GetComponent<Animator>();
            if (screamer == null) screamer = gameObject.GetComponent<SI_Screamer>();
            
            if (agent == null || player == null) return Status.Failure;
            if (!agent.isOnNavMesh) return Status.Failure;
            
            if (screamer != null && screamer.IsScreaming)
            {
                agent.isStopped = true;
                if (animator != null) animator.SetFloat("Speed", 0f);
                return Status.Running;
            }

            if (animator != null)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName("Attack") || stateInfo.IsName("Death"))
                {
                    agent.isStopped = true;
                    return Status.Running;
                }
            }

            agent.isStopped = false;
            agent.SetDestination(player.position);
            
            Vector3 direction = (player.position - gameObject.transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                gameObject.transform.rotation = Quaternion.Slerp(
                    gameObject.transform.rotation, 
                    lookRotation, 
                    Time.deltaTime * rotationSpeed
                );
            }
            
            if (animator != null)
            {
                float speed = agent.velocity.magnitude;
                animator.SetFloat("Speed", speed);
            }
            
            return Status.Running;
        }
    }

    [System.Serializable]
    public class PerformScream : Action
    {
        private SI_Screamer screamer;

        protected override Status OnUpdate()
        {
            if (screamer == null) screamer = gameObject.GetComponent<SI_Screamer>();
            if (screamer == null) return Status.Failure;

            if (screamer.IsScreaming)
            {
                return Status.Running;
            }

            screamer.UseAbility();
            return Status.Success;
        }
    }

    [System.Serializable]
    public class FleeFromPlayer : Action
    {
        [SerializeField] private float fleeDistance = 15f;
        
        private NavMeshAgent agent;
        private Transform player;

        protected override Status OnUpdate()
        {
            if (agent == null) agent = gameObject.GetComponent<NavMeshAgent>();
            if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
            
            if (agent == null || player == null) return Status.Failure;
            
            if (!agent.isOnNavMesh) return Status.Failure;

            Vector3 fleeDir = (gameObject.transform.position - player.position).normalized;
            Vector3 fleePos = gameObject.transform.position + fleeDir * fleeDistance;

            if (NavMesh.SamplePosition(fleePos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
                return Status.Running;
            }

            return Status.Failure;
        }
    }

    [System.Serializable]
    public class AttackPlayer : Action
    {
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private string attackTrigger = "Attack";
        
        private float lastAttackTime;
        private Animator animator;
        private Transform player;
        private NavMeshAgent agent;
        private EnemyAI enemyAI;

        protected override Status OnUpdate()
        {
            if (animator == null) animator = gameObject.GetComponent<Animator>();
            if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (agent == null) agent = gameObject.GetComponent<NavMeshAgent>();
            if (enemyAI == null) enemyAI = gameObject.GetComponent<EnemyAI>();
            
            if (player == null) return Status.Failure;

            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = true;

            Vector3 dir = (player.position - gameObject.transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
                gameObject.transform.rotation = Quaternion.LookRotation(dir);

            if (Time.time - lastAttackTime >= attackCooldown)
            {
                lastAttackTime = Time.time;
                
                if (animator != null)
                {
                    try { animator.SetTrigger(attackTrigger); } catch { }
                }

                float damage = enemyAI != null ? enemyAI.AttackDamage : 10f;
                player.GetComponent<PlayerHealth>()?.TakeDamage(damage);
            }

            return Status.Running;
        }
    }

    [System.Serializable]
    public class StopMoving : Action
    {
        private NavMeshAgent agent;

        protected override Status OnUpdate()
        {
            if (agent == null) agent = gameObject.GetComponent<NavMeshAgent>();
            
            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = true;

            return Status.Success;
        }
    }
}
