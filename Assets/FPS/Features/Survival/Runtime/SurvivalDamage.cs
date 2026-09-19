using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    public static class SurvivalDamage
    {
        public static bool HasLineOfSight(Vector3 origin, Vector3 destination, Transform ignore, Transform target)
        {
            Vector3 direction = destination - origin;
            if (direction.sqrMagnitude < .0001f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction.normalized, direction.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (ignore != null && (hit.transform == ignore || hit.transform.IsChildOf(ignore))) continue;
                if (target != null && (hit.transform == target || hit.transform.IsChildOf(target))) continue;
                return false;
            }
            return true;
        }
        public static void Apply(Vector3 center, float radius, ulong attacker, bool fire, HashSet<Component> damaged = null)
        {
            var seen = damaged ?? new HashSet<Component>();
            foreach (Collider hit in Physics.OverlapSphere(center, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                Component target = hit.GetComponentInParent<PlayerHealth>();
                if (target == null) target = hit.GetComponentInParent<EnemyHealth>();
                if (target == null || seen.Contains(target)) continue;
                if (target is PlayerHealth p && (!p.IsServer || p.IsDead)) continue;
                if (target is EnemyHealth e && (!e.IsServer || e.IsDead)) continue;
                Vector3 point = hit.ClosestPoint(center);
                if (!HasLineOfSight(center, hit.bounds.center, null, target.transform)) continue;
                seen.Add(target);
                float damage = fire ? (target is PlayerHealth ? 4f : 12f) : SurvivalRules.FragDamage(Vector3.Distance(center, point));
                if (target is PlayerHealth player) player.TakeDamage(damage);
                else ((EnemyHealth)target).TakeDamage(new DamageInfo(damage, attacker, -1, point, false, 0, fire ? DamageType.Environment : DamageType.Explosion));
            }
        }
    }
}
