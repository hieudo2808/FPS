using System.Collections.Generic;
using UnityEngine;

namespace UniBT
{
    public abstract class Composite : NodeBehavior
    {
        [SerializeReference]
        private List<NodeBehavior> children = new List<NodeBehavior>();
        
        public List<NodeBehavior> Children => children;

        protected sealed override void OnRun()
        {
            foreach (var child in children)
            {
                if (child == null)
                {
                    Debug.LogWarning($"BehaviorTree on {gameObject.name} has a missing child node in {GetType().Name}.");
                    continue;
                }

                child.Run(gameObject);
            }
        }
        
        public sealed override void Awake()
        {
            OnAwake();
            foreach (var child in children)
            {
                child?.Awake();
            }
        }

        protected virtual void OnAwake()
        {
        }

        public sealed override void Start()
        {
            OnStart();
            foreach (var child in children)
            {
                child?.Start();
            }
        }
        
        protected virtual void OnStart()
        {
        }

        public sealed override void PreUpdate()
        {
            foreach (var child in children)
            {
                child?.PreUpdate();
            }
        }
        
        public sealed override void PostUpdate()
        {
            foreach (var child in children)
            {
                child?.PostUpdate();
            }
        }

#if UNITY_EDITOR
        public void AddChild(NodeBehavior child)
        {
            children.Add(child);
        }
#endif
        
    }
}
