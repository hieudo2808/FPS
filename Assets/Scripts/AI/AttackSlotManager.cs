using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    public class AttackSlotManager : MonoBehaviour
    {
        public static AttackSlotManager Instance { get; private set; }
        
        [Header("Settings")]
        [SerializeField] private int slotsPerPlayer = 8;
        [SerializeField] private float slotRadius = 2f;
        [SerializeField] private float slotTimeout = 5f;
        [SerializeField] private float navMeshSampleRange = 1.0f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = false;
        
        // Slot data per player
        private Dictionary<int, AttackSlot[]> playerSlots = new Dictionary<int, AttackSlot[]>();
        private Dictionary<EnemyAI, SlotAssignment> zombieAssignments = new Dictionary<EnemyAI, SlotAssignment>();

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
            public bool isAttacker; // true = attacking, false = waiting
        }

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        private void Start()
        {
            InitializeSlots();
        }

        private void Update()
        {
            CleanupDeadZombies();
            CheckSlotTimeouts();
            UpdateSlotPositions();
        }

        private void InitializeSlots()
        {
            if (PlayerProfiler.Instance == null) return;
            
            for (int p = 0; p < PlayerProfiler.Instance.PlayerCount; p++)
            {
                CreateSlotsForPlayer(p);
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
            // Ensure slots exist for this player
            if (!playerSlots.ContainsKey(targetPlayerIndex))
            {
                CreateSlotsForPlayer(targetPlayerIndex);
            }
            
            // Already has assignment?
            if (zombieAssignments.ContainsKey(zombie))
            {
                return zombieAssignments[zombie].isAttacker;
            }
            
            AttackSlot[] slots = playerSlots[targetPlayerIndex];
            PlayerProfile profile = PlayerProfiler.Instance?.GetProfile(targetPlayerIndex);
            if (profile?.playerTransform == null) return false;
            
            Vector3 playerPos = profile.playerTransform.position;
            Vector3 zombiePos = zombie.transform.position;
            Vector3 toZombie = (zombiePos - playerPos).normalized;
            
            // Find best free slot (closest to zombie's approach direction)
            AttackSlot bestSlot = null;
            float bestDot = -1f;
            
            foreach (var slot in slots)
            {
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
                // Claim slot
                bestSlot.occupant = zombie;
                bestSlot.claimTime = Time.time;
                
                zombieAssignments[zombie] = new SlotAssignment
                {
                    playerIndex = targetPlayerIndex,
                    slotIndex = bestSlot.slotIndex,
                    isAttacker = true
                };
                
                return true;
            }
            
            // No free slot - become waiter
            zombieAssignments[zombie] = new SlotAssignment
            {
                playerIndex = targetPlayerIndex,
                slotIndex = -1,
                isAttacker = false
            };
            
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
                {
                    slot.occupant = null;
                }
            }
            
            zombieAssignments.Remove(zombie);
        }

        public Vector3 GetSlotWorldPosition(EnemyAI zombie, Transform fallbackTarget)
        {
            if (!zombieAssignments.TryGetValue(zombie, out SlotAssignment assignment))
            {
                return fallbackTarget != null ? fallbackTarget.position : zombie.transform.position;
            }

            if (assignment.slotIndex < 0) 
            {
                return fallbackTarget != null ? fallbackTarget.position : zombie.transform.position;
            }
            
            PlayerProfile profile = PlayerProfiler.Instance?.GetProfile(assignment.playerIndex);
            
            if (profile?.playerTransform == null) 
                return fallbackTarget != null ? fallbackTarget.position : zombie.transform.position;

            if (!playerSlots.ContainsKey(assignment.playerIndex))
                return profile.playerTransform.position;

            var slot = playerSlots[assignment.playerIndex][assignment.slotIndex];
            
            Vector3 targetPos = profile.playerTransform.position + slot.localOffset;

            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out hit, navMeshSampleRange, UnityEngine.AI.NavMesh.AllAreas))
            {
                return hit.position;
            }

            return profile.playerTransform.position;
        }

        public bool IsAttacker(EnemyAI zombie)
        {
            if (zombieAssignments.TryGetValue(zombie, out SlotAssignment assignment))
                return assignment.isAttacker;
            return false;
        }

        public int GetZombiesTargeting(int playerIndex)
        {
            int count = 0;
            foreach (var kvp in zombieAssignments)
            {
                if (kvp.Value.playerIndex == playerIndex)
                    count++;
            }
            return count;
        }

        private void CleanupDeadZombies()
        {
            List<EnemyAI> toRemove = new List<EnemyAI>();
            
            foreach (var kvp in zombieAssignments)
            {
                if (kvp.Key == null)
                    toRemove.Add(kvp.Key);
            }
            
            foreach (var zombie in toRemove)
            {
                ReleaseSlot(zombie);
            }
        }

        private void CheckSlotTimeouts()
        {
            foreach (var kvp in playerSlots)
            {
                foreach (var slot in kvp.Value)
                {
                    if (!slot.IsFree && Time.time - slot.claimTime > slotTimeout)
                    {
                        // Timeout - release slot
                        if (slot.occupant != null)
                        {
                            zombieAssignments.Remove(slot.occupant);
                        }
                        slot.occupant = null;
                    }
                }
            }
        }

        private void UpdateSlotPositions()
        {
            // Promote waiters to attackers when slots become free
            foreach (var kvp in zombieAssignments)
            {
                if (!kvp.Value.isAttacker && kvp.Key != null)
                {
                    // Try to get a slot
                    if (RequestSlotForWaiter(kvp.Key, kvp.Value.playerIndex))
                    {
                        kvp.Value.isAttacker = true;
                    }
                }
            }
        }

        private bool RequestSlotForWaiter(EnemyAI zombie, int playerIndex)
        {
            if (!playerSlots.ContainsKey(playerIndex)) return false;
            
            foreach (var slot in playerSlots[playerIndex])
            {
                if (slot.IsFree)
                {
                    slot.occupant = zombie;
                    slot.claimTime = Time.time;
                    
                    if (zombieAssignments.ContainsKey(zombie))
                    {
                        zombieAssignments[zombie].slotIndex = slot.slotIndex;
                        zombieAssignments[zombie].isAttacker = true;
                    }
                    return true;
                }
            }
            return false;
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || PlayerProfiler.Instance == null) return;
            
            foreach (var kvp in playerSlots)
            {
                var profile = PlayerProfiler.Instance.GetProfile(kvp.Key);
                if (profile?.playerTransform == null) continue;
                
                foreach (var slot in kvp.Value)
                {
                    Vector3 pos = profile.playerTransform.position + slot.localOffset;
                    Gizmos.color = slot.IsFree ? Color.green : Color.red;
                    Gizmos.DrawWireSphere(pos, 0.3f);
                }
            }
        }
    }
}
