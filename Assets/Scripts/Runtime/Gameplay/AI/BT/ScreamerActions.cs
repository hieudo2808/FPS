using UniBT;
using UnityEngine;

namespace FPS.BT
{
    [System.Obsolete("Legacy authoring only. Runtime Screamer behavior is owned by FPS.SI_Screamer.")]
    [System.Serializable]
    public class ChasePlayer : Action
    {
        protected override Status OnUpdate() => Status.Failure;
    }

    [System.Obsolete("Legacy authoring only. Runtime Screamer behavior is owned by FPS.SI_Screamer.")]
    [System.Serializable]
    public class PerformScream : Action
    {
        protected override Status OnUpdate()
        {
            gameObject.GetComponent<SI_Screamer>()?.UseAbility();
            return Status.Success;
        }
    }

    [System.Obsolete("Legacy authoring only. Runtime Screamer behavior is owned by FPS.SI_Screamer.")]
    [System.Serializable]
    public class FleeFromPlayer : Action
    {
        protected override Status OnUpdate() => Status.Failure;
    }

    [System.Obsolete("Legacy authoring only. Runtime Screamer behavior is owned by FPS.SI_Screamer.")]
    [System.Serializable]
    public class AttackPlayer : Action
    {
        protected override Status OnUpdate() => Status.Failure;
    }
}
