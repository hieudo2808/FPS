using UnityEngine;

namespace UniBT
{
    public class Root : NodeBehavior
    {
        [SerializeReference]
        private NodeBehavior child;

#if UNITY_EDITOR
        [HideInEditorWindow]
        public System.Action UpdateEditor;
#endif
        public NodeBehavior Child
        {
            get => child;
#if UNITY_EDITOR
            set => child = value;
#endif
        }

        protected sealed override void OnRun()
        {
            if (child == null)
            {
                Debug.LogWarning($"BehaviorTree on {gameObject.name} has no root child.");
                return;
            }

            child.Run(gameObject);
        }

        public override void Awake()
        {
            child?.Awake();
        }

        public override void Start()
        {
           child?.Start();
        }

        public override void PreUpdate()
        {
            child?.PreUpdate();
        }

        protected sealed override Status OnUpdate()
        {
#if UNITY_EDITOR
            UpdateEditor?.Invoke();
#endif
            return child != null ? child.Update() : Status.Failure;
        }
        
        
        public override void PostUpdate()
        {
            child?.PostUpdate();
        }

        public override void Abort()
        {
            child?.Abort();
        }

    }
}
