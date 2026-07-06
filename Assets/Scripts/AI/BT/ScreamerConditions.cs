using UniBT;

namespace FPS.BT
{
    [System.Obsolete("Legacy authoring only. Runtime Screamer behavior is owned by FPS.SI_Screamer.")]
    [System.Serializable]
    public class IsHealthLow : Action
    {
        protected override Status OnUpdate() => Status.Failure;
    }

    [System.Obsolete("Legacy authoring only. Runtime Screamer behavior is owned by FPS.SI_Screamer.")]
    [System.Serializable]
    public class IsPlayerInAttackRange : Action
    {
        protected override Status OnUpdate() => Status.Failure;
    }

    [System.Obsolete("Legacy authoring only. Runtime Screamer behavior is owned by FPS.SI_Screamer.")]
    [System.Serializable]
    public class IsAbilityReady : Action
    {
        protected override Status OnUpdate() => Status.Failure;
    }

    [System.Obsolete("Legacy authoring only. Runtime Screamer behavior is owned by FPS.SI_Screamer.")]
    [System.Serializable]
    public class IsPlayerInRange : Action
    {
        protected override Status OnUpdate() => Status.Failure;
    }
}
