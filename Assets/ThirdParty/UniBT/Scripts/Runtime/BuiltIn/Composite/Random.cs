using System;

namespace UniBT
{
    [Serializable]
    public class Random : Composite
    {
        private NodeBehavior runningNode;

        protected override Status OnUpdate()
        {
            // update running node if previous status is Running.
            if (runningNode != null)
            {
                return HandleStatus(runningNode.Update(), runningNode);
            }

            if (Children.Count == 0)
            {
                return Status.Failure;
            }

            var start = UnityEngine.Random.Range(0, Children.Count);
            for (var offset = 0; offset < Children.Count; offset++)
            {
                var target = Children[(start + offset) % Children.Count];
                if (target == null)
                {
                    continue;
                }

                return HandleStatus(target.Update(), target);
            }

            return Status.Failure;
        }

        private Status HandleStatus(Status status, NodeBehavior updated)
        {
            runningNode = status == Status.Running ? updated : null;
            return status;
        }

        public override void Abort()
        {
            if (runningNode != null)
            {
                runningNode.Abort();
                runningNode = null;
            }
        }
    }
}
