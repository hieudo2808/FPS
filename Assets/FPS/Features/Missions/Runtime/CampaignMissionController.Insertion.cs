using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    /// <summary>Frozen server roster. Slots survive disconnects so a remaining actor never switches ropes.</summary>
    public struct CampaignInsertionParticipant : INetworkSerializable, IEquatable<CampaignInsertionParticipant>
    {
        public ulong clientId, playerId;
        public PlayerCharacterId character;
        public Vector3 arrival;
        public Quaternion rotation;
        public bool connected;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref playerId);
            serializer.SerializeValue(ref character);
            serializer.SerializeValue(ref arrival);
            serializer.SerializeValue(ref rotation);
            serializer.SerializeValue(ref connected);
        }

        public bool Equals(CampaignInsertionParticipant other) => clientId == other.clientId && playerId == other.playerId
            && character == other.character && arrival == other.arrival && rotation == other.rotation && connected == other.connected;
    }

    public sealed partial class CampaignMissionController
    {
        private NetworkList<CampaignInsertionParticipant> insertionRoster;
        private readonly NetworkVariable<bool> insertionReady = new();
        public bool InsertionReady => insertionReady.Value;
        public int InsertionPartySize => insertionRoster?.Count ?? 0;
        public CampaignInsertionParticipant InsertionParticipant(int slot) => insertionRoster[slot];

        private bool PrepareInsertionParty()
        {
            if (insertionRoster.Count == 0)
            {
                // Playing can reach the host before all player prefabs and owner input acknowledgements do.
                var clients = NetworkManager.ConnectedClientsList;
                if (clients.Count == 0 || clients.Any(c => c.PlayerObject == null)) return false;
                var people = Players.OrderBy(p => p.OwnerClientId).ToArray();
                if (people.Length != clients.Count || people.Any(p => !p.IsInputReady)) return false;
                if (people.Length > 4) throw new InvalidOperationException("Insertion supports at most four players.");
                var catalog = Resources.Load<PlayerPrefabCatalog>("PlayerPrefabCatalog");
                var entries = new CampaignInsertionParticipant[people.Length];
                for (int slot = 0; slot < people.Length; slot++)
                {
                    var player = people[slot];
                    // Resolve the actual spawned prefab, including standalone sessions without lobby state.
                    var source = catalog.Entries.FirstOrDefault(e => e.prefab != null
                        && e.prefab.GetComponent<NetworkObject>().PrefabIdHash == player.NetworkObject.PrefabIdHash);
                    if (source == null) throw new InvalidOperationException("Insertion character is absent from PlayerPrefabCatalog.");
                    entries[slot] = new CampaignInsertionParticipant
                    {
                        clientId = player.OwnerClientId, playerId = player.StablePlayerId.Value,
                        character = source.id, connected = true,
                        arrival = CampaignInsertionLayout.Arrival(chapters[0].arrivals, people.Length, slot),
                        rotation = chapters[0].arrivals[slot].rotation
                    };
                }
                for (int slot = 0; slot < people.Length; slot++)
                {
                    insertionRoster.Add(entries[slot]);
                    clientPlayers[people[slot].OwnerClientId] = people[slot].StablePlayerId.Value;
                    people[slot].RelocateCampaign(entries[slot].arrival, entries[slot].rotation);
                }
                return false;
            }
            if (!Players.Any(p => IsInsertionParticipant(p.StablePlayerId.Value))) return false;
            if (Players.Any(p => IsInsertionParticipant(p.StablePlayerId.Value) && (!p.IsInputReady || p.CampaignTransferPending))) return false;
            insertionReady.Value = true;
            return true;
        }

        private bool IsInsertionParticipant(ulong id)
        {
            for (int i = 0; i < InsertionPartySize; i++) if (insertionRoster[i].playerId == id) return true;
            return false;
        }

        private void DisconnectInsertionParticipant(ulong client)
        {
            for (int i = 0; i < InsertionPartySize; i++)
            {
                var entry = insertionRoster[i];
                if (entry.clientId != client) continue;
                entry.connected = false;
                insertionRoster[i] = entry;
            }
        }

        private void ReconnectInsertionParticipant(PlayerHealth player)
        {
            if (state.phase != CampaignPhase.Insertion) return;
            for (int i = 0; i < InsertionPartySize; i++)
            {
                var entry = insertionRoster[i];
                if (entry.playerId != player.StablePlayerId.Value || entry.connected) continue;
                entry.clientId = player.OwnerClientId;
                entry.connected = true;
                insertionRoster[i] = entry;
                player.RelocateCampaign(entry.arrival, entry.rotation);
                return;
            }
        }
    }

    public static class CampaignInsertionLayout
    {
        /// <summary>Preserve marker spacing, recenter the occupied slots on the painted landing cross.</summary>
        public static Vector3 Arrival(Transform[] markers, int count, int slot)
        {
            Vector3 center = Vector3.zero, occupiedCenter = Vector3.zero;
            foreach (var marker in markers) center += marker.position / markers.Length;
            for (int i = 0; i < count; i++) occupiedCenter += markers[i].position / count;
            return markers[slot].position + center - occupiedCenter;
        }
    }
}
