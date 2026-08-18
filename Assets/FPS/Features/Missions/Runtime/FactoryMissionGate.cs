using UnityEngine;

namespace FPS
{
    public enum FactoryGateUnlockMode : byte
    {
        MissionStateAtLeast,
        ObjectivesCompleted
    }

    [DisallowMultipleComponent]
    public sealed class FactoryMissionGate : MonoBehaviour
    {
        [SerializeField] private FactoryMissionController controller;
        [SerializeField] private FactoryGateUnlockMode unlockMode = FactoryGateUnlockMode.MissionStateAtLeast;
        [SerializeField] private FactoryMissionState opensAtState = FactoryMissionState.SampleUnlocked;
        [SerializeField] private FactoryObjectiveFlags requiredObjectives = FactoryObjectiveFlags.None;
        [SerializeField] private Vector3 openLocalOffset = new(0f, 5f, 0f);
        [SerializeField, Min(0.1f)] private float moveSpeed = 3f;

        private Vector3 closedLocalPosition;
        private Vector3 targetLocalPosition;
        private bool subscribed;

        public FactoryGateUnlockMode UnlockMode => unlockMode;
        public FactoryMissionState OpensAtState => opensAtState;
        public FactoryObjectiveFlags RequiredObjectives => requiredObjectives;

        private void Awake()
        {
            closedLocalPosition = transform.localPosition;
            targetLocalPosition = closedLocalPosition;
        }

        private void OnEnable()
        {
            ResolveAndSubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (!subscribed)
                ResolveAndSubscribe();

            if ((transform.localPosition - targetLocalPosition).sqrMagnitude <= 0.0001f)
                return;

            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition,
                targetLocalPosition,
                moveSpeed * Time.deltaTime);
        }

        public void Configure(FactoryMissionState openState, Vector3 localOffset, float speed = 3f)
        {
            unlockMode = FactoryGateUnlockMode.MissionStateAtLeast;
            opensAtState = openState;
            requiredObjectives = FactoryObjectiveFlags.None;
            openLocalOffset = localOffset;
            moveSpeed = Mathf.Max(0.1f, speed);
            ApplyCurrentState();
        }

        public void Configure(
            FactoryMissionController missionController,
            FactoryMissionState openState,
            Vector3 localOffset,
            float speed = 3f)
        {
            RebindController(missionController);
            Configure(openState, localOffset, speed);
        }

        public void ConfigureObjectives(
            FactoryMissionController missionController,
            FactoryObjectiveFlags objectives,
            Vector3 localOffset,
            float speed = 3f)
        {
            RebindController(missionController);
            unlockMode = FactoryGateUnlockMode.ObjectivesCompleted;
            requiredObjectives = objectives;
            openLocalOffset = localOffset;
            moveSpeed = Mathf.Max(0.1f, speed);
            ApplyCurrentState();
        }

        private void HandleMissionStateChanged(FactoryMissionState previous, FactoryMissionState next)
        {
            ApplyCurrentState();
        }

        private void HandleObjectivesChanged(
            FactoryObjectiveFlags previous,
            FactoryObjectiveFlags next)
        {
            ApplyCurrentState();
        }

        private void ResolveAndSubscribe()
        {
            if (controller == null)
                controller = FactoryMissionController.Instance;
            if (controller == null || subscribed)
                return;

            controller.MissionStateChanged += HandleMissionStateChanged;
            controller.CompletedObjectivesChanged += HandleObjectivesChanged;
            subscribed = true;
            ApplyCurrentState();
        }

        private void RebindController(FactoryMissionController missionController)
        {
            if (controller == missionController)
                return;

            Unsubscribe();
            controller = missionController;
            if (isActiveAndEnabled)
                ResolveAndSubscribe();
        }

        private void Unsubscribe()
        {
            if (controller != null && subscribed)
            {
                controller.MissionStateChanged -= HandleMissionStateChanged;
                controller.CompletedObjectivesChanged -= HandleObjectivesChanged;
            }

            subscribed = false;
        }

        private void ApplyCurrentState()
        {
            if (controller == null)
                return;

            bool shouldOpen = controller.State is not FactoryMissionState.Failed
                && unlockMode switch
                {
                    FactoryGateUnlockMode.ObjectivesCompleted => requiredObjectives != FactoryObjectiveFlags.None
                        && (controller.CompletedObjectives & requiredObjectives) == requiredObjectives,
                    _ => (byte)controller.State >= (byte)opensAtState
                };
            targetLocalPosition = shouldOpen
                ? closedLocalPosition + openLocalOffset
                : closedLocalPosition;
        }
    }
}
