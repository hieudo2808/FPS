using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;

namespace FPS
{
    [Serializable]
    public sealed class CampaignWeaponSave
    {
        public byte slot;
        public string definition;
        public int magazine;
        public int reserve;
        public static CampaignWeaponSave Capture(WeaponRuntimeSnapshot s) => new() { slot = s.slotIndex, definition = s.definitionId.ToString(), magazine = s.magazineAmmo, reserve = s.reserveAmmo };
        public WeaponRuntimeSnapshot Restore() => new() { slotIndex = slot, definitionId = new FixedString64Bytes(definition ?? ""), magazineAmmo = magazine, reserveAmmo = reserve, nextAllowedFireTime = -1, reloadAmmoCommitTime = -1, reloadCompleteTime = -1, equipCompleteTime = -1 };
    }
    [Serializable]
    public sealed class CampaignPlayerSave
    {
        public ulong playerId;
        public Vector3 position;
        public Quaternion rotation;
        public float health;
        public float infection;
        public PlayerLifeState lifeState;
        public byte equipped;
        public byte medicine;
        public byte survivalInventoryVersion;
        public byte fragCount, incendiaryCount, medkitCount, antidoteCount;
        public ThrowableKind selectedThrowable;
        public byte chapter;
        public double lifeStateDeadline;
        public PrimaryWeaponId primary;
        public CampaignWeaponSave first;
        public CampaignWeaponSave second;
        public static CampaignPlayerSave Capture(PlayerHealth p)
        {
            return Capture(p.CaptureRuntimeSnapshot());
        }
        public static CampaignPlayerSave Capture(PlayerRuntimeSnapshot s)
        {
            return new() { playerId = s.sessionPlayerId.Value, position = s.position, rotation = s.rotation,
                health = s.health, infection = s.infection, lifeState = s.lifeState, equipped = s.equippedWeaponSlot,
                lifeStateDeadline = s.lifeStateDeadline,
                survivalInventoryVersion = s.survivalInventoryVersion, fragCount = s.fragCount,
                incendiaryCount = s.incendiaryCount, medkitCount = s.medkitCount, antidoteCount = s.antidoteCount,
                selectedThrowable = s.selectedThrowable,
                primary = s.primaryWeaponId, medicine = s.medicineCount, chapter = s.campaignChapter, first = CampaignWeaponSave.Capture(s.weaponSlot0), second = CampaignWeaponSave.Capture(s.weaponSlot1) };
        }
        public PlayerRuntimeSnapshot Restore(SessionPlayerId id)
        {
            var s = PlayerRuntimeSnapshot.CreateDefault(id, position, rotation);
            s.sceneName = new FixedString64Bytes("GameScene"); s.health = health; s.infection = infection;
            s.lifeState = lifeState; s.primaryWeaponId = primary; s.equippedWeaponSlot = equipped;
            s.weaponSlot0 = first.Restore(); s.weaponSlot1 = second.Restore(); s.medicineCount = medicine;
            s.campaignChapter = chapter;
            if (survivalInventoryVersion > 0)
            {
                s.fragCount = fragCount; s.incendiaryCount = incendiaryCount;
                s.medkitCount = medkitCount; s.antidoteCount = antidoteCount;
                s.selectedThrowable = selectedThrowable;
            }
            s.lifeStateDeadline = lifeStateDeadline;
            return s;
        }
    }
    [Serializable]
    public sealed class CampaignCheckpoint
    {
        public int version = 1;
        public CampaignState state;
        public List<CampaignPlayerSave> players = new();
        public List<string> claimedSupplies = new();
        public string savedUtc;
        public static string SavePath
        {
            get
            {
                string directory=Application.persistentDataPath;
                return Path.Combine(directory,"cold-ledger-campaign-v1.json");
            }
        }

        public void Save()
        {
            Validate();
            savedUtc = DateTime.UtcNow.ToString("O");
            string path = SavePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(this, true));
            // Replace is atomic on the local filesystem; keep one previous valid checkpoint.
            if (File.Exists(path)) File.Replace(temp, path, path + ".previous");
            else File.Move(temp, path);
        }
        public static bool TryLoad(out CampaignCheckpoint checkpoint, out string error)
        {
            checkpoint = null; error = "";
            foreach (string path in new[] { SavePath, SavePath + ".previous" })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    checkpoint = JsonUtility.FromJson<CampaignCheckpoint>(File.ReadAllText(path));
                    if (checkpoint == null) throw new InvalidDataException("Checkpoint trống.");
                    checkpoint.Validate();
                    return true;
                }
                catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
                { error = e.Message; checkpoint = null; }
            }
            if (string.IsNullOrEmpty(error)) error = "Chưa có checkpoint đã lưu.";
            return false;
        }

        public void Validate()
        {
            if (version != 1 || state == null || state.version != 1 || players == null || players.Count is < 1 or > 4
                || !Enum.IsDefined(typeof(CampaignChapter), state.chapter) || state.phase != CampaignPhase.Exploring
                || state.checkpoint < 0 || state.checkpoint > 5 || state.checkpoint / 2 != (int)state.chapter
                || claimedSupplies == null || claimedSupplies.Count > 128)
                throw new InvalidDataException("Checkpoint không tương thích hoặc thiếu dữ liệu.");
            var ids = new HashSet<ulong>();
            foreach (var p in players)
            {
                if (p == null || !ids.Add(p.playerId) || p.first == null || p.second == null
                    || !float.IsFinite(p.health) || p.health < 0 || !float.IsFinite(p.infection)
                    || !Finite(p.position) || !Finite(new Vector3(p.rotation.x, p.rotation.y, p.rotation.z)) || !float.IsFinite(p.rotation.w)
                    || !Enum.IsDefined(typeof(PlayerLifeState), p.lifeState) || p.lifeState == PlayerLifeState.Downed
                    || p.fragCount > 3 || p.incendiaryCount > 3 || p.medkitCount > 2 || p.antidoteCount > 2
                    || (byte)p.selectedThrowable > 1 || p.survivalInventoryVersion > 1
                    || p.medicine > 2 || p.equipped > 1 || !ValidWeapon(p.first) || !ValidWeapon(p.second))
                    throw new InvalidDataException("Snapshot player không hợp lệ.");
            }
            foreach (string id in claimedSupplies)
                if (string.IsNullOrWhiteSpace(id) || System.Text.Encoding.UTF8.GetByteCount(id) > 61)
                    throw new InvalidDataException("Mã tiếp tế không hợp lệ.");
        }
        private static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        private static bool ValidWeapon(CampaignWeaponSave w) => w.magazine >= 0 && w.reserve >= 0
            && w.slot <= 1 && System.Text.Encoding.UTF8.GetByteCount(w.definition ?? "") <= 61;
    }
}
