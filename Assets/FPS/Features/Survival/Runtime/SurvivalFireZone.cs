using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public sealed class SurvivalFireZone : NetworkBehaviour
    {
        private static readonly List<SurvivalFireZone> zones = new();
        private static readonly HashSet<Component> damagedThisTick = new();
        private static NetworkManager tickManager;
        private static long damageTick = -1;
        private readonly NetworkVariable<double> expires = new();
        private ulong attacker;
        private long lastTick = -1;
        private GameObject visual;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { zones.Clear(); damagedThisTick.Clear(); tickManager = null; damageTick = -1; }
        public static void CreateOrExtend(SurvivalFireZone prefab, Vector3 point, ulong owner, NetworkManager manager)
        {
            if (manager == null || !manager.IsServer || prefab == null) return;
            foreach (var zone in zones)
            {
                if (zone == null || !zone.IsSpawned || zone.NetworkManager != manager || !zone.IsServer) continue;
                if ((zone.transform.position - point).sqrMagnitude > 36f || !SurvivalDamage.HasLineOfSight(point, zone.transform.position, null, null)) continue;
                zone.expires.Value = System.Math.Max(zone.expires.Value, manager.ServerTime.Time + 6);
                return;
            }
            var created = Instantiate(prefab, point, Quaternion.identity);
            created.attacker = owner;
            created.expires.Value = manager.ServerTime.Time + 6;
            created.NetworkObject.Spawn();
        }
        public override void OnNetworkSpawn()
        {
            zones.Add(this);
            if (IsClient) visual = SurvivalEffects.BeginFire(transform.position);
        }
        public override void OnNetworkDespawn()
        {
            zones.Remove(this);
            SurvivalEffects.Return(visual);
            visual = null;
        }
        private void Update()
        {
            if (!IsSpawned || !IsServer) return;
            double now = NetworkManager.ServerTime.Time;
            if (now >= expires.Value) { NetworkObject.Despawn(); return; }
            long tick = (long)System.Math.Floor(now / .5);
            if (lastTick == tick) return;
            lastTick = tick;
            if (tickManager != NetworkManager || damageTick != tick)
            { damagedThisTick.Clear(); tickManager = NetworkManager; damageTick = tick; }
            // Shared across throwers: one victim is hit at most once per world tick.
            SurvivalDamage.Apply(transform.position, 3f, attacker, true, damagedThisTick);
        }
    }
}
