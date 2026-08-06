using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Server-owned mapping between the replicated character id and the prefab
    /// that is allowed to be spawned. Clients only ever send the enum value.
    /// </summary>
    [CreateAssetMenu(menuName = "FPS/Characters/Player Prefab Catalog", fileName = "PlayerPrefabCatalog")]
    public sealed class PlayerPrefabCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public PlayerCharacterId id;
            public string displayName;
            public GameObject prefab;
        }

        [SerializeField] private List<Entry> entries = new();
        public IReadOnlyList<Entry> Entries => entries;

        public bool TryGetPrefab(PlayerCharacterId id, out GameObject prefab)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry != null && entry.id == id && entry.prefab != null)
                {
                    prefab = entry.prefab;
                    return true;
                }
            }

            prefab = null;
            return false;
        }

        public string GetDisplayName(PlayerCharacterId id)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry != null && entry.id == id)
                    return string.IsNullOrWhiteSpace(entry.displayName) ? id.ToString() : entry.displayName;
            }

            return id.ToString();
        }

        public bool IsComplete(out string error)
        {
            HashSet<PlayerCharacterId> seen = new();
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry == null)
                {
                    error = $"Entry {i} is null.";
                    return false;
                }
                if (!seen.Add(entry.id))
                {
                    error = $"Duplicate character id {entry.id}.";
                    return false;
                }
                if (entry.prefab == null)
                {
                    error = $"Character {entry.id} has no prefab.";
                    return false;
                }

                NetworkObject networkObject = entry.prefab.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    error = $"Character {entry.id} prefab has no NetworkObject.";
                    return false;
                }

                NetworkObject[] nested = entry.prefab.GetComponentsInChildren<NetworkObject>(true);
                if (nested.Length != 1 || nested[0] != networkObject)
                {
                    error = $"Character {entry.id} prefab contains a nested NetworkObject.";
                    return false;
                }
            }

            foreach (PlayerCharacterId id in Enum.GetValues(typeof(PlayerCharacterId)))
            {
                if (!seen.Contains(id))
                {
                    error = $"Missing character id {id}.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            if (!IsComplete(out string error))
                Debug.LogWarning($"{name}: {error}", this);
        }
    }
}
