using UnityEngine;
using UniBT;

namespace FPS.BT
{


    [System.Serializable]
    public class IsPlayerInRange : Conditional
    {
        [SerializeField] private float range = 30f;
        
        private Transform player;

        protected override void OnAwake()
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        protected override bool IsUpdatable()
        {
            if (player == null) return false;
            float dist = Vector3.Distance(gameObject.transform.position, player.position);
            return dist <= range;
        }
    }

    [System.Serializable]
    public class IsAbilityReady : Conditional
    {
        private SI_Screamer screamer;

        protected override void OnAwake()
        {
            screamer = gameObject.GetComponent<SI_Screamer>();
        }

        protected override bool IsUpdatable()
        {
            return screamer != null && screamer.IsAbilityReady;
        }
    }

    [System.Serializable]
    public class IsNotScreaming : Conditional
    {
        private SI_Screamer screamer;

        protected override void OnAwake()
        {
            screamer = gameObject.GetComponent<SI_Screamer>();
        }

        protected override bool IsUpdatable()
        {
            return screamer != null && !screamer.IsScreaming;
        }
    }

    [System.Serializable]
    public class IsHealthLow : Conditional
    {
        [SerializeField] private float threshold = 0.3f;
        
        private EnemyHealth health;

        protected override void OnAwake()
        {
            health = gameObject.GetComponent<EnemyHealth>();
        }

        protected override bool IsUpdatable()
        {
            if (health == null) return false;
            return health.CurrentHealth / health.MaxHealth < threshold;
        }
    }

    [System.Serializable]
    public class IsPlayerInAttackRange : Conditional
    {
        [SerializeField] private float attackRange = 2f;
        
        private Transform player;

        protected override void OnAwake()
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        protected override bool IsUpdatable()
        {
            if (player == null) return false;
            float dist = Vector3.Distance(gameObject.transform.position, player.position);
            return dist <= attackRange;
        }
    }
}
