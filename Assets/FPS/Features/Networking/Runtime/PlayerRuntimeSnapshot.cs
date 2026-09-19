using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public enum PlayerLifeState : byte
    {
        Alive,
        Downed,
        Dead,
        Spectating
    }

    public struct WeaponRuntimeSnapshot : INetworkSerializable, IEquatable<WeaponRuntimeSnapshot>
    {
        public byte slotIndex;
        public FixedString64Bytes definitionId;
        public int magazineAmmo;
        public int reserveAmmo;
        public double nextAllowedFireTime;
        public double reloadAmmoCommitTime;
        public double reloadCompleteTime;
        public WeaponReloadTimeline reloadTimeline;
        public double equipCompleteTime;
        public ushort lastAcceptedFireSequence;
        public bool hasAcceptedFireSequence;

        public bool Equals(WeaponRuntimeSnapshot other)
        {
            return slotIndex == other.slotIndex
                && definitionId.Equals(other.definitionId)
                && magazineAmmo == other.magazineAmmo
                && reserveAmmo == other.reserveAmmo
                && nextAllowedFireTime.Equals(other.nextAllowedFireTime)
                && reloadAmmoCommitTime.Equals(other.reloadAmmoCommitTime)
                && reloadCompleteTime.Equals(other.reloadCompleteTime)
                && reloadTimeline.Equals(other.reloadTimeline)
                && equipCompleteTime.Equals(other.equipCompleteTime)
                && lastAcceptedFireSequence == other.lastAcceptedFireSequence
                && hasAcceptedFireSequence == other.hasAcceptedFireSequence;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref slotIndex);
            serializer.SerializeValue(ref definitionId);
            serializer.SerializeValue(ref magazineAmmo);
            serializer.SerializeValue(ref reserveAmmo);
            serializer.SerializeValue(ref nextAllowedFireTime);
            serializer.SerializeValue(ref reloadAmmoCommitTime);
            serializer.SerializeValue(ref reloadCompleteTime);
            serializer.SerializeValue(ref reloadTimeline);
            serializer.SerializeValue(ref equipCompleteTime);
            serializer.SerializeValue(ref lastAcceptedFireSequence);
            serializer.SerializeValue(ref hasAcceptedFireSequence);
        }
    }

    public struct PlayerRuntimeSnapshot : INetworkSerializable
    {
        public ushort schemaVersion;
        public uint revision;
        public SessionPlayerId sessionPlayerId;
        public FixedString64Bytes roleId;
        public FixedString64Bytes sceneName;
        public int serverTick;
        public Vector3 position;
        public Quaternion rotation;
        public float health;
        public float infection;
        public PlayerLifeState lifeState;
        public double lifeStateDeadline;
        public byte equippedWeaponSlot;
        public PrimaryWeaponId primaryWeaponId;
        public WeaponRuntimeSnapshot weaponSlot0;
        public WeaponRuntimeSnapshot weaponSlot1;
        public ushort inventorySchemaVersion;
        public byte medicineCount;
        public byte survivalInventoryVersion;
        public byte fragCount, incendiaryCount, medkitCount, antidoteCount;
        public ThrowableKind selectedThrowable;
        // 0 outside campaign; 1..3 identify the chapter containing the saved pose.
        public byte campaignChapter;

        public static PlayerRuntimeSnapshot CreateDefault(SessionPlayerId playerId, Vector3 position, Quaternion rotation)
        {
            return new PlayerRuntimeSnapshot
            {
                schemaVersion = NetworkProtocol.SnapshotSchemaVersion,
                sessionPlayerId = playerId,
                position = position,
                rotation = rotation,
                health = 100f,
                infection = 0f,
                lifeState = PlayerLifeState.Alive,
                inventorySchemaVersion = 5,
                survivalInventoryVersion = 1,
                fragCount = 3, incendiaryCount = 3, medkitCount = 2, antidoteCount = 2,
                medicineCount = 0
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref schemaVersion);
            serializer.SerializeValue(ref revision);
            serializer.SerializeValue(ref sessionPlayerId);
            serializer.SerializeValue(ref roleId);
            serializer.SerializeValue(ref sceneName);
            serializer.SerializeValue(ref serverTick);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref rotation);
            serializer.SerializeValue(ref health);
            serializer.SerializeValue(ref infection);
            serializer.SerializeValue(ref lifeState);
            serializer.SerializeValue(ref lifeStateDeadline);
            serializer.SerializeValue(ref equippedWeaponSlot);
            serializer.SerializeValue(ref primaryWeaponId);
            serializer.SerializeValue(ref weaponSlot0);
            serializer.SerializeValue(ref weaponSlot1);
            serializer.SerializeValue(ref inventorySchemaVersion);
            serializer.SerializeValue(ref medicineCount);
            serializer.SerializeValue(ref campaignChapter);
            serializer.SerializeValue(ref survivalInventoryVersion);
            serializer.SerializeValue(ref fragCount);
            serializer.SerializeValue(ref incendiaryCount);
            serializer.SerializeValue(ref medkitCount);
            serializer.SerializeValue(ref antidoteCount);
            serializer.SerializeValue(ref selectedThrowable);
        }
    }
}
