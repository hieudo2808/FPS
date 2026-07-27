using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class ZombieNetworkPoolHandler : INetworkPrefabInstanceHandler
    {
        private readonly GameObject prefab;
        private readonly ZombiePoolManager pool;

        public ZombieNetworkPoolHandler(GameObject prefab, ZombiePoolManager pool)
        {
            this.prefab = prefab;
            this.pool = pool;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            GameObject obj = pool.GetFromPoolOnly(prefab, position, rotation);

            if (obj == null)
            {
                GameLog.Warning(() => $"[ZombiePoolHandler] Pool returned null for '{prefab.name}', falling back to Instantiate.");
                obj = Object.Instantiate(prefab, position, rotation);
            }

            return obj.GetComponent<NetworkObject>();
        }

        public void Destroy(NetworkObject networkObject)
        {
            if (networkObject == null) return;
            pool.ReturnToPoolInternal(networkObject.gameObject);
        }
    }
}
