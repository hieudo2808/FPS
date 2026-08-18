using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public enum PrimaryWeaponId : byte
    {
        Vandal,
        Operator,
        Odin,
        Bucky
    }

    [Serializable]
    public sealed class PrimaryWeaponCandidate
    {
        [SerializeField] private PrimaryWeaponId id;
        [SerializeField] private GameObject weaponObject;

        public PrimaryWeaponId Id => id;
        public GameObject WeaponObject => weaponObject;
    }

    public class WeaponManager : NetworkBehaviour
    {
        private const int PrimarySlotIndex = 0;

        [Tooltip("Owned weapon slots only. Sage starts with Vandal in slot 0 and Classic in slot 1.")]
        [SerializeField] private List<GameObject> weapons = new List<GameObject>();
        [Tooltip("Preconfigured primary replacements. These are not owned slots and must remain inactive until selected by the server.")]
        [SerializeField] private List<PrimaryWeaponCandidate> primaryWeaponCandidates = new List<PrimaryWeaponCandidate>();
        [SerializeField] private Animator characterAnimation;
        [Tooltip("Animator on the first-person model that owns Skeleton. Bound to every weapon slot at runtime.")]
        [SerializeField] private Animator firstPersonArmsAnimator;
        [SerializeField] private int maxWeaponSlots = 2;

        private NetworkVariable<int> networkedWeaponIndex = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
        );
        private readonly NetworkVariable<PrimaryWeaponId> networkedPrimaryWeapon = new(
            PrimaryWeaponId.Vandal,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private bool networkCallbacksRegistered;

        public static WeaponManager LocalInstance { get; private set; }

        /// <summary>
        /// Raised after the networked weapon index has been applied locally.
        /// Presentation systems use this to update first/third-person visuals
        /// without polling the network variable every frame.
        /// </summary>
        public event Action<int> WeaponIndexChanged;

        public int WeaponCount => weapons != null ? weapons.Count : 0;
        public int CurrentWeaponIndex => WeaponCount > 0
            ? Mathf.Clamp(networkedWeaponIndex.Value, 0, WeaponCount - 1)
            : 0;
        public GameObject CurrentWeapon => WeaponCount > 0
            ? weapons[Mathf.Clamp(networkedWeaponIndex.Value, 0, WeaponCount - 1)]
            : null;
        public GameObject UnusedWeapon => WeaponCount > 1
            ? weapons[(networkedWeaponIndex.Value + 1) % WeaponCount]
            : null;
        public Animator CharacterAnimation => characterAnimation;
        public Animator FirstPersonArmsAnimator => firstPersonArmsAnimator;
        public PrimaryWeaponId ActivePrimaryWeaponId => networkedPrimaryWeapon.Value;
        public int PrimaryCandidateCount => primaryWeaponCandidates != null ? primaryWeaponCandidates.Count : 0;

        public override void OnNetworkSpawn()
        {
            if (IsOwner) LocalInstance = this;

            networkedWeaponIndex.OnValueChanged += OnWeaponChanged;
            networkedPrimaryWeapon.OnValueChanged += OnPrimaryWeaponChanged;
            networkCallbacksRegistered = true;

            #region agent log
            int nullWeaponSlots = 0;
            if (weapons != null)
            {
                for (int i = 0; i < weapons.Count; i++)
                    if (weapons[i] == null)
                        nullWeaponSlots++;
            }
            GameLog.Info(() => $"[WeaponManager][dbg] OnNetworkSpawn owner={IsOwner} weaponCount={WeaponCount} nullSlots={nullWeaponSlots} currentIndex={networkedWeaponIndex.Value}");
            #region agent log
            GameLog.DebugSession("initial", "W1", "WeaponManager.cs:45", "weapon configuration at network spawn", $"{{\"owner\":{(IsOwner ? "true" : "false")},\"count\":{WeaponCount},\"nullSlots\":{nullWeaponSlots},\"index\":{networkedWeaponIndex.Value}}}");
            #endregion
            #endregion

            ApplyPrimaryWeapon(networkedPrimaryWeapon.Value);
            BindFirstPersonPresentation();
            ApplyOwnerStateToWeapons();
            UpdateWeaponVisibility(networkedWeaponIndex.Value);
            CurrentWeapon?.GetComponent<Weapon>()?.PrepareFirstPersonPresentation(false);
            ReportCurrentWeaponTelemetry();
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && LocalInstance == this)
                LocalInstance = null;

            networkedWeaponIndex.OnValueChanged -= OnWeaponChanged;
            networkedPrimaryWeapon.OnValueChanged -= OnPrimaryWeaponChanged;
            networkCallbacksRegistered = false;
        }

        private void ApplyOwnerStateToWeapons()
        {
            foreach (GameObject weaponObj in EnumerateConfiguredWeapons())
            {
                if (weaponObj == null)
                    continue;

                var weapon = weaponObj.GetComponent<Weapon>();
                if (weapon != null)
                {
                    weapon.BindFirstPersonPresentation(GetComponent<MouseMovement>()?.BodyCam, firstPersonArmsAnimator);
                    weapon.SetOwner(IsOwner);
                }
            }
        }

        private void BindFirstPersonPresentation()
        {
            Camera bodyCamera = GetComponent<MouseMovement>()?.BodyCam;
            foreach (GameObject weaponObject in EnumerateConfiguredWeapons())
                weaponObject?.GetComponent<Weapon>()?.BindFirstPersonPresentation(bodyCamera, firstPersonArmsAnimator);
        }

        /// <summary>
        /// Replaces the owned primary weapon after a server-validated pickup.
        /// Candidate objects are authored on the player prefab but do not become
        /// inventory slots until the server selects one here.
        /// </summary>
        public bool TryReplacePrimaryWeaponServer(PrimaryWeaponId weaponId)
        {
            if (IsSpawned && !IsServer)
                return false;
            if (!TryGetPrimaryCandidate(weaponId, out GameObject candidate))
                return false;
            if (candidate == null || candidate.GetComponent<Weapon>()?.Data == null)
                return false;
            if (networkedPrimaryWeapon.Value == weaponId && GetWeapon(PrimarySlotIndex) == candidate)
                return true;

            PrimaryWeaponId previous = networkedPrimaryWeapon.Value;
            networkedPrimaryWeapon.Value = weaponId;
            if (!networkCallbacksRegistered)
                OnPrimaryWeaponChanged(previous, weaponId);
            return true;
        }

        public void RestorePrimaryWeaponServer(PrimaryWeaponId weaponId)
        {
            if (IsSpawned && !IsServer)
                return;

            if (!TryReplacePrimaryWeaponServer(weaponId))
                TryReplacePrimaryWeaponServer(PrimaryWeaponId.Vandal);
        }

        public bool TryGetPrimaryCandidate(PrimaryWeaponId weaponId, out GameObject weaponObject)
        {
            if (primaryWeaponCandidates != null)
            {
                foreach (PrimaryWeaponCandidate candidate in primaryWeaponCandidates)
                {
                    if (candidate != null && candidate.Id == weaponId)
                    {
                        weaponObject = candidate.WeaponObject;
                        return weaponObject != null;
                    }
                }
            }

            weaponObject = null;
            return false;
        }

        private void OnPrimaryWeaponChanged(PrimaryWeaponId previous, PrimaryWeaponId current)
        {
            if (!ApplyPrimaryWeapon(current))
                return;

            bool primaryIsEquipped = CurrentWeaponIndex == PrimarySlotIndex;
            // The selected object must be active before the authoritative equip
            // deadline is published; otherwise Weapon ignores the presentation
            // update and only the gun controller's Entry state is visible.
            UpdateWeaponVisibility(CurrentWeaponIndex);
            if (IsServer)
                GetComponent<WeaponFireHandler>()?.HandleServerPrimaryWeaponReplaced(primaryIsEquipped);
            if (primaryIsEquipped)
                CurrentWeapon?.GetComponent<Weapon>()?.PrepareFirstPersonPresentation(false);
            ReportCurrentWeaponTelemetry();
        }

        private bool ApplyPrimaryWeapon(PrimaryWeaponId weaponId)
        {
            if (!TryGetPrimaryCandidate(weaponId, out GameObject selected))
                return false;
            if (weapons == null)
                weapons = new List<GameObject>();
            while (weapons.Count <= PrimarySlotIndex)
                weapons.Add(null);

            weapons[PrimarySlotIndex] = selected;
            Weapon weapon = selected.GetComponent<Weapon>();
            weapon?.BindFirstPersonPresentation(GetComponent<MouseMovement>()?.BodyCam, firstPersonArmsAnimator);
            weapon?.SetOwner(IsOwner);
            return true;
        }

        private IEnumerable<GameObject> EnumerateConfiguredWeapons()
        {
            var seen = new HashSet<GameObject>();
            if (weapons != null)
            {
                foreach (GameObject weapon in weapons)
                    if (weapon != null && seen.Add(weapon))
                        yield return weapon;
            }
            if (primaryWeaponCandidates == null)
                yield break;
            foreach (PrimaryWeaponCandidate candidate in primaryWeaponCandidates)
            {
                GameObject weapon = candidate?.WeaponObject;
                if (weapon != null && seen.Add(weapon))
                    yield return weapon;
            }
        }

        [ServerRpc]
        public void RequestSwitchWeaponServerRpc(ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (WeaponCount == 0) return;
            int oldSlot = CurrentWeaponIndex;
            int newSlot = (oldSlot + 1) % WeaponCount;
            if (newSlot == oldSlot) return;
            networkedWeaponIndex.Value = newSlot;
            GetComponent<WeaponFireHandler>()?.HandleServerWeaponSwitched(oldSlot, newSlot);
        }

        [ServerRpc]
        public void RequestEquipWeaponServerRpc(int slotIndex, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (WeaponCount == 0) return;

            int clampedSlot = Mathf.Clamp(slotIndex, 0, WeaponCount - 1);
            int oldSlot = CurrentWeaponIndex;
            if (clampedSlot == oldSlot) return;
            networkedWeaponIndex.Value = clampedSlot;
            GetComponent<WeaponFireHandler>()?.HandleServerWeaponSwitched(oldSlot, clampedSlot);
        }

        public void SetEquippedWeaponServer(int slotIndex)
        {
            ApplyEquippedWeaponServer(slotIndex, true, true);
        }

        public void RestoreEquippedWeaponServer(int slotIndex)
        {
            ApplyEquippedWeaponServer(slotIndex, false, true);
        }

        private void ApplyEquippedWeaponServer(int slotIndex, bool beginEquip, bool force)
        {
            // Networked clients may not change the authoritative slot. An
            // unspawned manager is also used by editor/offline setup code, so
            // allow that local path to exercise the same visibility/event flow.
            if ((!IsServer && IsSpawned) || WeaponCount == 0)
                return;

            int clampedSlot = Mathf.Clamp(slotIndex, 0, WeaponCount - 1);
            int oldSlot = CurrentWeaponIndex;
            if (!force && oldSlot == clampedSlot)
                return;

            networkedWeaponIndex.Value = clampedSlot;
            if (!IsSpawned)
                OnWeaponChanged(oldSlot, clampedSlot);
            else
                GetComponent<WeaponFireHandler>()?.HandleServerWeaponSwitched(oldSlot, clampedSlot, beginEquip);
        }

        private void OnWeaponChanged(int oldIndex, int newIndex)
        {
            UpdateWeaponVisibility(newIndex);
            CurrentWeapon?.GetComponent<Weapon>()?.PrepareFirstPersonPresentation(false);
            WeaponIndexChanged?.Invoke(CurrentWeaponIndex);
            ReportCurrentWeaponTelemetry();
        }

        private void UpdateWeaponVisibility(int index)
        {
            if (weapons == null) return;

            if (primaryWeaponCandidates != null)
            {
                foreach (PrimaryWeaponCandidate candidate in primaryWeaponCandidates)
                    if (candidate?.WeaponObject != null)
                        candidate.WeaponObject.SetActive(false);
            }

            #region agent log
            int nullWeaponSlots = 0;
            for (int i = 0; i < weapons.Count; i++)
                if (weapons[i] == null)
                    nullWeaponSlots++;
            GameLog.Info(() => $"[WeaponManager][dbg] UpdateWeaponVisibility index={index} count={weapons.Count} nullSlots={nullWeaponSlots}");
            #region agent log
            GameLog.DebugSession("initial", "W1", "WeaponManager.cs:113", "weapon visibility update", $"{{\"index\":{index},\"count\":{weapons.Count},\"nullSlots\":{nullWeaponSlots}}}");
            #endregion
            #endregion

            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] == null)
                    continue;

                weapons[i].SetActive(i == index);
            }

            if (IsOwner && HUDManager.HasInstance)
                HUDManager.Instance.UpdateWeaponUI();
        }

        public void AddWeapon(GameObject newWeapon)
        {
            if (weapons == null)
                weapons = new List<GameObject>();

            if (weapons.Count < maxWeaponSlots)
                weapons.Add(newWeapon);
        }



        /// <summary>
        /// Thêm đạn cho vũ khí hiện tại — tránh chain call qua CurrentWeapon.
        /// </summary>
        public void AddAmmoToCurrentWeapon(int amount)
        {
            if (IsServer)
                GetComponent<WeaponFireHandler>()?.AddReserveAmmoServer(amount);

            AddAmmoToCurrentWeaponLocalOnly(amount);
        }

        public void AddAmmoToCurrentWeaponLocalOnly(int amount)
        {
            var weapon = CurrentWeapon?.GetComponent<Weapon>();
            weapon?.AddReserveAmmo(amount);
        }

        public void ReportCurrentWeaponTelemetry()
        {
            var weapon = CurrentWeapon?.GetComponent<Weapon>();
            weapon?.ReportCombatTelemetry();
        }

        public Weapon GetWeapon(int slotIndex)
        {
            if (weapons == null || slotIndex < 0 || slotIndex >= weapons.Count)
                return null;

            return weapons[slotIndex] != null ? weapons[slotIndex].GetComponent<Weapon>() : null;
        }

        public void ApplyAuthoritativeWeaponState(WeaponOwnerState state)
        {
            Weapon weapon = GetWeapon(state.slotIndex);
            weapon?.SetLocalAmmoState(state.magazineAmmo, state.reserveAmmo, state.isReloading);
            weapon?.ApplyAuthoritativePresentation(state.equipCompleteTime, state.isReloading);

            if (IsOwner && HUDManager.HasInstance)
                HUDManager.Instance.UpdateAmmoInfo();
        }

        public void ApplyPresentationState(WeaponPresentationState state)
        {
            GetWeapon(state.slotIndex)?.ApplyAuthoritativePresentation(state.equipCompleteTime, state.isReloading);
        }

        public bool TryInspectCurrentWeapon()
        {
            Weapon weapon = CurrentWeapon != null ? CurrentWeapon.GetComponent<Weapon>() : null;
            return weapon != null && weapon.TryPlayInspect();
        }

        /// <summary>
        /// Trigger animation trên character — tránh expose CharacterAnimation.
        /// </summary>
        public void TriggerAnimation(string triggerName)
        {
            // The FPS hand/weapon animators are driven by Weapon. This optional
            // reference is only for legacy third-person rigs; destroyed Unity
            // objects can still compare non-null through C#'s null-conditional
            // operator, so use Unity's overloaded comparison explicitly.
            if (characterAnimation != null && characterAnimation.isActiveAndEnabled)
                characterAnimation.SetTrigger(triggerName);
        }
    }
}
