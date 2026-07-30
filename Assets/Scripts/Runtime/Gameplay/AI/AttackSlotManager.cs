using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    public enum EnemyAssignmentMode
    {
        Attacker,
        Flanker,
        Pressure,
        Reserve
    }

    public class AttackSlotManager : MonoBehaviour
    {
        public static AttackSlotManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int slotsPerPlayer = 8;
        [SerializeField] private float slotRadius = 2f;
        [SerializeField] private float waitRadius = 4.5f;
        [SerializeField] private float slotTimeout = 5f;
        [SerializeField] private float navMeshSampleRange = 1.0f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = false;

        private Dictionary<int, AttackSlot[]> playerSlots = new Dictionary<int, AttackSlot[]>();
        private Dictionary<EnemyAI, SlotAssignment> zombieAssignments = new Dictionary<EnemyAI, SlotAssignment>();
        private readonly List<EnemyAI> _deadZombieCache = new List<EnemyAI>();
        private float _lastCleanupCheck = -999f;
        private float _lastSlotPositionUpdate = -999f;
        private float _lastTimeoutCheck = -999f;
        [SerializeField] private float cleanupInterval = 0.5f;
        [SerializeField] private float slotPositionUpdateInterval = 0.1f;
        [SerializeField] private float timeoutCheckInterval = 0.25f;

        public class AttackSlot
        {
            public int slotIndex;
            public Vector3 localOffset;
            public EnemyAI occupant;
            public float claimTime;
            public bool IsFree => occupant == null;
        }

        public class SlotAssignment
        {
            public int playerIndex;
            public int slotIndex;
            public int waitIndex;
            public EnemyAssignmentMode mode;
            public bool isAttacker => mode == EnemyAssignmentMode.Attacker;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            float now = Time.time;

            if (now - _lastCleanupCheck >= cleanupInterval)
            {
                _lastCleanupCheck = now;
                CleanupDeadZombies();
            }

            if (now - _lastSlotPositionUpdate >= slotPositionUpdateInterval)
            {
                _lastSlotPositionUpdate = now;
                UpdateSlotPositions();
            }

            if (now - _lastTimeoutCheck >= timeoutCheckInterval)
            {
                _lastTimeoutCheck = now;
                CheckSlotTimeouts();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos || !Application.isPlaying || PlayerProfiler.Instance == null)
                return;

            Gizmos.color = Color.red;
            foreach (var kvp in playerSlots)
            {
                PlayerProfile profile = PlayerProfiler.Instance.GetProfile(kvp.Key);
                if (profile?.playerTransform == null)
                    continue;

                foreach (var slot in kvp.Value)
                {
                    Vector3 worldPos = profile.playerTransform.position + slot.localOffset;
                    Gizmos.DrawWireSphere(worldPos, 0.25f);
                }
            }
        }

        private void CreateSlotsForPlayer(int playerIndex)
        {
            if (playerSlots.ContainsKey(playerIndex)) return;

            AttackSlot[] slots = new AttackSlot[slotsPerPlayer];
            float angleStep = 360f / slotsPerPlayer;

            for (int i = 0; i < slotsPerPlayer; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(
                    Mathf.Sin(angle) * slotRadius,
                    0f,
                    Mathf.Cos(angle) * slotRadius
                );

                slots[i] = new AttackSlot
                {
                    slotIndex = i,
                    localOffset = offset,
                    occupant = null,
                    claimTime = 0f
                };
            }

            playerSlots[playerIndex] = slots;
        }

        public bool RequestSlot(EnemyAI zombie, int targetPlayerIndex)
        {
            if (!playerSlots.ContainsKey(targetPlayerIndex))
                CreateSlotsForPlayer(targetPlayerIndex);

            if (zombieAssignments.ContainsKey(zombie))
                return zombieAssignments[zombie].isAttacker;

            AttackSlot[] slots = playerSlots[targetPlayerIndex];
            int activeSlotCount = GetActiveAttackSlotCount();

            PlayerProfile profile = PlayerProfiler.Instance?.GetProfile(targetPlayerIndex);
            if (profile?.playerTransform == null) return false;

            Vector3 playerPos = profile.playerTransform.position;
            Vector3 zombiePos = zombie.transform.position;
            Vector3 toZombie = (zombiePos - playerPos).normalized;

            AttackSlot bestSlot = null;
            float bestDot = -1f;

            foreach (var slot in slots)
            {
                if (slot.slotIndex >= activeSlotCount) continue;
                if (!slot.IsFree) continue;

                float dot = Vector3.Dot(toZombie, slot.localOffset.normalized);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestSlot = slot;
                }
            }

            if (bestSlot != null)
            {
                bestSlot.occupant = zombie;
                bestSlot.claimTime = Time.time;

                zombieAssignments[zombie] = new SlotAssignment
                {
                    playerIndex = targetPlayerIndex,
                    slotIndex = bestSlot.slotIndex,
                    waitIndex = -1,
                    mode = EnemyAssignmentMode.Attacker
                };

                zombie.NotifyAttackSlotChanged();
                return true;
            }

            int waitIndex = GetNextWaitIndex(targetPlayerIndex);
            zombieAssignments[zombie] = new SlotAssignment
            {
                playerIndex = targetPlayerIndex,
                slotIndex = -1,
                waitIndex = waitIndex,
                mode = GetWaitModeForIndex(waitIndex)
            };

            zombie.NotifyAttackSlotChanged();
            return false;
        }

        public void ReleaseSlot(EnemyAI zombie)
        {
            if (!zombieAssignments.TryGetValue(zombie, out SlotAssignment assignment))
                return;

            if (assignment.slotIndex >= 0 && playerSlots.ContainsKey(assignment.playerIndex))
            {
                var slot = playerSlots[assignment.playerIndex][assignment.slotIndex];
                if (slot.occupant == zombie)
                    slot.occupant = null;
            }

            zombieAssignments.Remove(zombie);
            zombie.NotifyAttackSlotChanged();
        }

        public Vector3 GetSlotWorldPosition(EnemyAI zombie, Transform fallbackTarget)
        {
            return GetDestinationFor(zombie, -1, fallbackTarget);
        }

        public Vector3 GetDestinationFor(EnemyAI zombie, int targetPlayerIndex, Transform fallbackTarget)
        {
            Vector3 fallback = fallbackTarget != null
                ? fallbackTarget.position
                : zombie.transform.position;

            if (!zombieAssignments.TryGetValue(zombie, out SlotAssignment assignment))
            {
                if (targetPlayerIndex >= 0)
                    RequestSlot(zombie, targetPlayerIndex);

                if (!zombieAssignments.TryGetValue(zombie, out assignment))
                    return fallback;
            }

            PlayerProfile profile = PlayerProfiler.Instance?.GetProfile(assignment.playerIndex);
            if (profile?.playerTransform == null)
                return fallback;

            if (assignment.slotIndex < 0)
                return GetNonAttackerDestination(assignment, profile.playerTransform, zombie.transform.position);

            return GetAttackerDestination(assignment, profile.playerTransform, zombie.transform.position);
        }

        private Vector3 GetAttackerDestination(SlotAssignment assignment, Transform playerTransform, Vector3 fallback)
        {
            if (!playerSlots.ContainsKey(assignment.playerIndex))
                return GetNonAttackerDestination(assignment, playerTransform, fallback);

            if (assignment.slotIndex < 0 || assignment.slotIndex >= playerSlots[assignment.playerIndex].Length)
                return GetNonAttackerDestination(assignment, playerTransform, fallback);

            var slot = playerSlots[assignment.playerIndex][assignment.slotIndex];
            Vector3 targetPos = playerTransform.position + slot.localOffset;

            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit hit, navMeshSampleRange, UnityEngine.AI.NavMesh.AllAreas))
                return hit.position;

            if (IsUsableDestination(targetPos, playerTransform.position))
                return targetPos;

            return GetNonAttackerDestination(assignment, playerTransform, fallback);
        }

        private Vector3 GetNonAttackerDestination(SlotAssignment assignment, Transform playerTransform, Vector3 fallback)
        {
            return GetWaitWorldPosition(assignment, playerTransform, fallback);
        }

#if UNITY_INCLUDE_TESTS
        public bool IsAttacker(EnemyAI zombie)
        {
            if (zombieAssignments.TryGetValue(zombie, out SlotAssignment assignment))
                return assignment.isAttacker;
            return false;
        }

        public bool TryGetAssignmentMode(EnemyAI zombie, out EnemyAssignmentMode mode)
        {
            if (zombieAssignments.TryGetValue(zombie, out SlotAssignment assignment))
            {
                mode = assignment.mode;
                return true;
            }

            mode = EnemyAssignmentMode.Reserve;
            return false;
        }

        public bool HasAssignment(EnemyAI zombie)
        {
            return zombieAssignments.ContainsKey(zombie);
        }
#endif

        public int GetZombiesTargeting(int playerIndex)
        {
            int count = 0;
            foreach (var kvp in zombieAssignments)
                if (kvp.Value.playerIndex == playerIndex)
                    count++;
            return count;
        }

        private void CleanupDeadZombies()
        {
            _deadZombieCache.Clear();

            foreach (var kvp in zombieAssignments)
                if (!IsZombieAlive(kvp.Key))
                    _deadZombieCache.Add(kvp.Key);

            foreach (var zombie in _deadZombieCache)
                ReleaseSlot(zombie);
        }

        private void CheckSlotTimeouts()
        {
            foreach (var kvp in playerSlots)
            {
                foreach (var slot in kvp.Value)
                {
                    if (!slot.IsFree && Time.time - slot.claimTime > slotTimeout)
                    {
                        if (slot.occupant != null && IsZombieAlive(slot.occupant))
                        {
                            slot.claimTime = Time.time;
                        }
                        else if (slot.occupant != null)
                        {
                            zombieAssignments.Remove(slot.occupant);
                            slot.occupant = null;
                        }
                    }
                }
            }
        }

        private void UpdateSlotPositions()
        {
            foreach (var kvp in zombieAssignments)
            {
                if (!kvp.Value.isAttacker && kvp.Key != null)
                {
                    RequestSlotForWaiter(kvp.Key, kvp.Value.playerIndex);
                }
            }
        }

        private bool RequestSlotForWaiter(EnemyAI zombie, int playerIndex)
        {
            if (!playerSlots.ContainsKey(playerIndex)) return false;

            int activeSlotCount = GetActiveAttackSlotCount();
            foreach (var slot in playerSlots[playerIndex])
            {
                if (slot.slotIndex >= activeSlotCount) continue;
                if (slot.IsFree)
                {
                    slot.occupant = zombie;
                    slot.claimTime = Time.time;

                    if (zombieAssignments.ContainsKey(zombie))
                    {
                        zombieAssignments[zombie].slotIndex = slot.slotIndex;
                        zombieAssignments[zombie].waitIndex = -1;
                        zombieAssignments[zombie].mode = EnemyAssignmentMode.Attacker;
                    }

                    zombie.NotifyAttackSlotChanged();
                    return true;
                }
            }
            return false;
        }

        private Vector3 GetWaitWorldPosition(SlotAssignment assignment, Transform playerTransform, Vector3 fallback)
        {
            if (playerTransform == null) return fallback;

            int waitIndex = assignment.waitIndex >= 0 ? assignment.waitIndex : 0;
            float radius = GetRadiusForMode(assignment.mode);
            float angleOffset = GetAngleOffsetForMode(assignment.mode);
            float angle = (angleOffset + waitIndex * 137.5f) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Sin(angle) * radius,
                0f,
                Mathf.Cos(angle) * radius
            );

            Vector3 targetPos = playerTransform.position + offset;
            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit hit, navMeshSampleRange, UnityEngine.AI.NavMesh.AllAreas))
                return hit.position;

            return targetPos;
        }

        private EnemyAssignmentMode GetWaitModeForIndex(int waitIndex)
        {
            switch (waitIndex % 3)
            {
                case 0: return EnemyAssignmentMode.Flanker;
                case 1: return EnemyAssignmentMode.Pressure;
                default: return EnemyAssignmentMode.Reserve;
            }
        }

        private float GetRadiusForMode(EnemyAssignmentMode mode)
        {
            switch (mode)
            {
                case EnemyAssignmentMode.Flanker:
                    return waitRadius;
                case EnemyAssignmentMode.Pressure:
                    return waitRadius + 1.5f;
                case EnemyAssignmentMode.Reserve:
                    return waitRadius + 3f;
                default:
                    return slotRadius;
            }
        }

        private float GetAngleOffsetForMode(EnemyAssignmentMode mode)
        {
            switch (mode)
            {
                case EnemyAssignmentMode.Flanker: return 70f;
                case EnemyAssignmentMode.Pressure: return 160f;
                case EnemyAssignmentMode.Reserve: return 250f;
                default: return 0f;
            }
        }

        private int GetNextWaitIndex(int playerIndex)
        {
            int count = 0;
            foreach (var assignment in zombieAssignments.Values)
            {
                if (assignment.playerIndex == playerIndex && !assignment.isAttacker)
                    count++;
            }

            return count;
        }

        private int GetActiveAttackSlotCount()
        {
            int configured = slotsPerPlayer;
            if (DifficultyManager.Instance != null)
                configured = DifficultyManager.Instance.GetCurrentStats().maxConcurrentAttackers;

            return Mathf.Clamp(configured, 1, slotsPerPlayer);
        }

        private static bool IsZombieAlive(EnemyAI zombie)
        {
            if (zombie == null || !zombie.gameObject.activeInHierarchy)
                return false;

            EnemyHealth health = zombie.GetComponent<EnemyHealth>();
            return health == null || !health.IsDead;
        }

        private bool IsUsableDestination(Vector3 destination, Vector3 playerPosition)
        {
            if (float.IsNaN(destination.x) || float.IsNaN(destination.y) || float.IsNaN(destination.z))
                return false;

            if (float.IsInfinity(destination.x) || float.IsInfinity(destination.y) || float.IsInfinity(destination.z))
                return false;

            return (destination - playerPosition).sqrMagnitude > (slotRadius * 0.5f) * (slotRadius * 0.5f);
        }
    }
}
