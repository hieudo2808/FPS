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
        Bucky,
        Warden
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

        [Tooltip("Owned weapon slots only. Slot 0 is the primary weapon and slot 1 is the sidearm.")]
        [SerializeField] private List<GameObject> weapons = new List<GameObject>();
        [Tooltip("Primary candidate selected by the server when this player spawns.")]
        [SerializeField] private PrimaryWeaponId defaultPrimaryWeapon = PrimaryWeaponId.Vandal;
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
        private bool primaryWeaponSelectionPrepared;
        private bool thirdPersonReloadActive;
        private double thirdPersonReloadCompleteTime = -1d;
        private WeaponReloadTimeline thirdPersonReloadTimeline;
        private bool thirdPersonEquipCompletionPending;
        private double thirdPersonEquipCompleteTime = -1d;
        private double thirdPersonEquipStartedAt = -1d;
        private double lastAuthoritativeEquipCompleteTime = -1d;
        private PlayerHealth playerHealth;

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
            if (characterAnimation == null)
                characterAnimation = GetComponent<PlayerMovement>()?.CharacterAnimation;

            if (IsOwner) LocalInstance = this;

            // The server chooses the prefab's loadout; clients use the replicated
            // value so a late join never replaces an already selected primary.
            if (IsServer && !primaryWeaponSelectionPrepared
                && TryGetPrimaryCandidate(defaultPrimaryWeapon, out GameObject initialPrimary)
                && initialPrimary.GetComponent<Weapon>()?.Data != null)
            {
                networkedPrimaryWeapon.Value = defaultPrimaryWeapon;
            }

            playerHealth = GetComponent<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.CombatAvailabilityChanged += HandleCombatAvailabilityChanged;

            networkedWeaponIndex.OnValueChanged += OnWeaponChanged;
            networkedPrimaryWeapon.OnValueChanged += OnPrimaryWeaponChanged;
            networkCallbacksRegistered = true;

            ApplyPrimaryWeapon(networkedPrimaryWeapon.Value);
            BindFirstPersonPresentation();
            ApplyOwnerStateToWeapons();
            HandleCombatAvailabilityChanged(playerHealth != null && playerHealth.CanUseCombat);
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
            if (playerHealth != null)
                playerHealth.CombatAvailabilityChanged -= HandleCombatAvailabilityChanged;
            networkCallbacksRegistered = false;
        }

        private void Update()
        {
            if (thirdPersonReloadActive && thirdPersonReloadTimeline.IsValid)
            {
                WeaponData data = GetWeapon(CurrentWeaponIndex)?.Data;
                float normalized = thirdPersonReloadTimeline.NormalizedTime(data, GetPresentationTime(), true);
                SetAnimatorFloatIfPresent(characterAnimation, "ReloadNormalizedTime", normalized);
                GetComponent<PlayerVisibilityController>()?
                    .SetThirdPersonWeaponAnimationFloat("ReloadNormalizedTime", normalized);
                if (GetPresentationTime() >= thirdPersonReloadTimeline.PresentationCompleteTime(data))
                    TriggerAnimation("ReloadComplete");
            }
            if (thirdPersonEquipCompletionPending)
            {
                WeaponData data = GetWeapon(CurrentWeaponIndex)?.Data;
                float normalized = Mathf.Clamp01((float)((GetPresentationTime() - thirdPersonEquipStartedAt)
                    / Math.Max(.0001f, data != null ? data.EquipAnimationDuration : 0f)));
                SetAnimatorFloatIfPresent(characterAnimation, "EquipNormalizedTime", normalized);
                GetComponent<PlayerVisibilityController>()?
                    .SetThirdPersonWeaponAnimationFloat("EquipNormalizedTime", normalized);
            }
            if (!thirdPersonEquipCompletionPending
                || thirdPersonEquipCompleteTime < 0d
                || GetPresentationTime() < thirdPersonEquipCompleteTime)
            {
                return;
            }

            TriggerAnimation("EquipComplete");
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
            // Snapshot restoration may run in a sibling's OnNetworkSpawn before
            // this manager. Preserve that selection instead of applying defaults.
            primaryWeaponSelectionPrepared = true;
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

            // Replacing the primary object does not change its slot index, so
            // OnWeaponChanged is not raised.  Clear any Reload/Equip state from
            // the previous controller before binding and driving the new one.
            ResetThirdPersonActionState();
            bool primaryIsEquipped = CurrentWeaponIndex == PrimarySlotIndex;
            // The selected object must be active before the authoritative equip
            // deadline is published; otherwise Weapon ignores the presentation
            // update and only the gun controller's Entry state is visible.
            UpdateWeaponVisibility(CurrentWeaponIndex);
            GetComponent<PlayerVisibilityController>()?
                .RefreshWeaponPresentation(CurrentWeaponIndex);
            if (primaryIsEquipped)
                CurrentWeapon?.GetComponent<Weapon>()?.PrepareFirstPersonPresentation(false);
            // Reset the old pose before publishing Equip: the host applies the
            // NetworkVariable callback synchronously during this publication.
            if (IsServer)
                GetComponent<WeaponFireHandler>()?.HandleServerPrimaryWeaponReplaced(primaryIsEquipped);
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
            if (!CanUseCombat()) return;
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
            if (!CanUseCombat()) return;
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
            ResetThirdPersonActionState();
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
            if (IsSpawned)
            {
                if (IsServer) GetComponent<WeaponFireHandler>()?.AddReserveAmmoServer(amount);
            }
            else AddAmmoToCurrentWeaponLocalOnly(amount);
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
            SetReloadPresentationTimeline(state.reloadTimeline);
            ApplyThirdPersonActionTiming(
                state.isReloading,
                state.reloadCompleteTime,
                state.equipCompleteTime);
            Weapon weapon = GetWeapon(state.slotIndex);
            weapon?.SetLocalAmmoState(state.magazineAmmo, state.reserveAmmo, state.isReloading);
            weapon?.ApplyAuthoritativePresentation(
                state.equipCompleteTime, state.isReloading, state.reloadCompleteTime, state.reloadTimeline);

            if (IsOwner && HUDManager.HasInstance)
                HUDManager.Instance.UpdateAmmoInfo();
        }

        public void ApplyPresentationState(
            WeaponPresentationState previous,
            WeaponPresentationState current)
        {
            SetReloadPresentationTimeline(current.reloadTimeline);
            ApplyThirdPersonActionTiming(
                current.isReloading,
                current.reloadCompleteTime,
                current.equipCompleteTime);
            GetWeapon(current.slotIndex)?.ApplyAuthoritativePresentation(
                current.equipCompleteTime,
                current.isReloading,
                current.reloadCompleteTime, current.reloadTimeline);

            // A remote client's first-person weapon may be inactive, in which case
            // Weapon.ApplyAuthoritativePresentation intentionally skips its local
            // presentation path. Drive the third-person body and gun directly from
            // the replicated equip edge so their authored animation is independent
            // of first-person visibility. If Weapon already emitted the trigger,
            // resetting it again here is harmless because both calls are synchronous
            // and the Animator cannot evaluate between them.
            bool equipStarted = System.Math.Abs(
                    previous.equipCompleteTime - current.equipCompleteTime)
                > 0.0001
                && current.equipCompleteTime >= 0d
                && thirdPersonEquipCompletionPending;
            if (equipStarted)
                TriggerAnimation("Equip");

            // Fire is presented by FireEffectsClientRpc. Reload has no separate
            // RPC, so detect its authoritative rising edge here as well.
            if (current.isReloading && (!previous.isReloading
                || !previous.reloadTimeline.Equals(current.reloadTimeline)))
                TriggerAnimation("Reload");
            else if (previous.isReloading && !current.isReloading)
                CompleteReloadPresentation();
        }

        public bool TryInspectCurrentWeapon()
        {
            if (!CanUseCombat())
                return false;

            Weapon weapon = CurrentWeapon != null ? CurrentWeapon.GetComponent<Weapon>() : null;
            return weapon != null && weapon.TryPlayInspect();
        }

        private bool CanUseCombat()
        {
            if (playerHealth == null)
                playerHealth = GetComponent<PlayerHealth>();
            return playerHealth != null && playerHealth.CanUseCombat && GetComponent<SurvivalInventory>()?.IsBusy != true;
        }

        private void HandleCombatAvailabilityChanged(bool canUseCombat)
        {
            if (!canUseCombat)
                ResetThirdPersonActionState();

            foreach (GameObject weaponObject in EnumerateConfiguredWeapons())
                weaponObject?.GetComponent<Weapon>()?.SetCombatAvailability(canUseCombat);
        }

        /// <summary>
        /// Trigger animation trên character — tránh expose CharacterAnimation.
        /// </summary>
        public void TriggerAnimation(string triggerName)
        {
            if (string.IsNullOrWhiteSpace(triggerName))
                return;

            if (triggerName == "Fire" || triggerName == "Inspect")
                CompleteActionsForAcceptedShot();

            UpdateThirdPersonActionState(triggerName);

            // The FPS hand/weapon animators are driven by Weapon. This optional
            // reference is only for legacy third-person rigs; destroyed Unity
            // objects can still compare non-null through C#'s null-conditional
            // operator, so use Unity's overloaded comparison explicitly.
            TriggerIfPresent(characterAnimation, triggerName);
            GetComponent<PlayerVisibilityController>()?
                .TriggerThirdPersonWeaponAnimation(triggerName);
        }

        public void ConfigureThirdPersonActionDuration(
            string actionName,
            float duration)
        {
            WeaponData data = CurrentWeapon != null
                ? CurrentWeapon.GetComponent<Weapon>()?.Data
                : null;
            if (data == null || duration <= 0f)
                return;

            float authoredDuration = actionName == "Reload"
                ? data.ReloadAnimationDuration
                : actionName == "Equip"
                    ? data.EquipAnimationDuration
                    : 0f;
            if (authoredDuration <= 0f)
                return;

            SetThirdPersonActionPlaybackSpeed(
                actionName,
                Mathf.Clamp(authoredDuration / duration, 0.05f, 20f));
        }

        public void SetReloadPresentationTimeline(WeaponReloadTimeline timeline)
        {
            WeaponData data = GetWeapon(CurrentWeaponIndex)?.Data;
            if (timeline.IsValid || !thirdPersonReloadTimeline.ShouldContinueAfterGameplay(data, GetPresentationTime()))
                thirdPersonReloadTimeline = timeline;
        }

        private void CompleteReloadPresentation()
        {
            WeaponData data = GetWeapon(CurrentWeaponIndex)?.Data;
            if (!thirdPersonReloadTimeline.ShouldContinueAfterGameplay(data, GetPresentationTime()))
                TriggerAnimation("ReloadComplete");
        }

        public void CompleteActionsForAcceptedShot()
        {
            // The reliable shot RPC may arrive before this tick's NetworkVariable
            // delta. A server-accepted Bucky shot has already cancelled reload.
            if (thirdPersonReloadActive) TriggerAnimation("ReloadComplete");
            if (thirdPersonEquipCompletionPending) TriggerAnimation("EquipComplete");
        }

        private void ApplyThirdPersonActionTiming(
            bool isReloading,
            double reloadCompleteTime,
            double equipCompleteTime)
        {
            double now = GetPresentationTime();
            WeaponData data = GetWeapon(CurrentWeaponIndex)?.Data;
            if (isReloading
                && reloadCompleteTime > now
                && (!thirdPersonReloadActive
                    || System.Math.Abs(
                        thirdPersonReloadCompleteTime - reloadCompleteTime)
                        > 0.0001d))
            {
                SetThirdPersonActionPlaybackSpeed("Reload",
                    thirdPersonReloadTimeline.IsValid ? 1f / thirdPersonReloadTimeline.timingMultiplier : 1f);
                thirdPersonReloadCompleteTime = reloadCompleteTime;
            }

            if (equipCompleteTime >= 0d && data != null
                && equipCompleteTime - data.EquipDuration + data.EquipAnimationDuration > now
                && System.Math.Abs(lastAuthoritativeEquipCompleteTime - equipCompleteTime) > .0001d)
            {
                lastAuthoritativeEquipCompleteTime = equipCompleteTime;
                thirdPersonEquipStartedAt = equipCompleteTime - data.EquipDuration;
                thirdPersonEquipCompleteTime = thirdPersonEquipStartedAt + data.EquipAnimationDuration;
                SetThirdPersonActionPlaybackSpeed("Equip", 1f);
                thirdPersonEquipCompletionPending = true;
            }
        }

        private void SetThirdPersonActionPlaybackSpeed(
            string actionName,
            float playbackSpeed)
        {
            string parameterName = actionName + "PlaybackSpeed";
            SetAnimatorFloatIfPresent(
                characterAnimation,
                parameterName,
                playbackSpeed);
            GetComponent<PlayerVisibilityController>()?
                .SetThirdPersonWeaponAnimationFloat(
                    parameterName,
                    playbackSpeed);
        }

        private void UpdateThirdPersonActionState(string triggerName)
        {
            switch (triggerName)
            {
                case "Reload":
                    thirdPersonReloadActive = true;
                    thirdPersonEquipCompletionPending = false;
                    thirdPersonEquipCompleteTime = -1d;
                    break;

                case "ReloadComplete":
                    thirdPersonReloadActive = false;
                    thirdPersonReloadTimeline = default;
                    thirdPersonReloadCompleteTime = -1d;
                    break;

                case "Equip":
                    thirdPersonReloadActive = false;
                    thirdPersonReloadTimeline = default;
                    thirdPersonReloadCompleteTime = -1d;
                    if (!thirdPersonEquipCompletionPending)
                    {
                        WeaponData data = CurrentWeapon != null
                            ? CurrentWeapon.GetComponent<Weapon>()?.Data
                            : null;
                        if (data != null)
                        {
                            thirdPersonEquipStartedAt = GetPresentationTime();
                            thirdPersonEquipCompleteTime =
                                thirdPersonEquipStartedAt + data.EquipAnimationDuration;
                            thirdPersonEquipCompletionPending = true;
                        }
                    }
                    break;

                case "EquipComplete":
                    thirdPersonEquipCompletionPending = false;
                    thirdPersonEquipCompleteTime = -1d;
                    break;
            }
        }

        private void ResetThirdPersonActionState()
        {
            thirdPersonReloadTimeline = default;
            thirdPersonReloadActive = false;
            thirdPersonReloadCompleteTime = -1d;
            thirdPersonEquipCompletionPending = false;
            thirdPersonEquipCompleteTime = -1d;
            thirdPersonEquipStartedAt = -1d;
            lastAuthoritativeEquipCompleteTime = -1d;
        }

        private double GetPresentationTime()
        {
            return NetworkManager != null && NetworkManager.IsListening
                ? NetworkManager.ServerTime.Time
                : Time.timeAsDouble;
        }

        private static void TriggerIfPresent(Animator animator, string triggerName)
        {
            if (animator == null || !animator.isActiveAndEnabled
                || animator.runtimeAnimatorController == null
                || string.IsNullOrWhiteSpace(triggerName))
                return;

            // Fire uses a separate Additive layer on most 3P body controllers.
            // A reload/equip action owns the complete authored upper-body pose,
            // so leaving Fire active would blend recoil over the action and can
            // visibly twist the neck/arms even though each clip previews well
            // in isolation. Clear both a queued trigger and the active layer
            // before starting the mutually-exclusive action.
            if (triggerName == "Reload" || triggerName == "Equip")
                CancelFirePresentation(animator);

            if (triggerName == "Reload")
                ResetTriggerIfPresent(animator, "ReloadComplete");
            if (triggerName == "Equip")
                ResetTriggerIfPresent(animator, "EquipComplete");

            int parameterHash = Animator.StringToHash(triggerName);
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash != parameterHash
                    || parameter.type != AnimatorControllerParameterType.Trigger)
                    continue;

                animator.ResetTrigger(parameterHash);
                animator.SetTrigger(parameterHash);
                return;
            }
        }

        private static void ResetTriggerIfPresent(
            Animator animator,
            string triggerName)
        {
            int triggerHash = Animator.StringToHash(triggerName);
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash != triggerHash
                    || parameter.type != AnimatorControllerParameterType.Trigger)
                    continue;

                animator.ResetTrigger(triggerHash);
                return;
            }
        }

        private static void SetAnimatorFloatIfPresent(
            Animator animator,
            string parameterName,
            float value)
        {
            if (animator == null || !animator.isActiveAndEnabled
                || animator.runtimeAnimatorController == null)
                return;

            int parameterHash = Animator.StringToHash(parameterName);
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == parameterHash
                    && parameter.type == AnimatorControllerParameterType.Float)
                {
                    animator.SetFloat(parameterHash, value);
                    return;
                }
            }
        }

        private static void CancelFirePresentation(Animator animator)
        {
            int fireHash = Animator.StringToHash("Fire");
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == fireHash
                    && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.ResetTrigger(fireHash);
                    break;
                }
            }

            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                if (animator.GetLayerName(layer).IndexOf(
                        "Fire Additive",
                        System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                animator.Play("Zero", layer, 0f);
            }
        }
    }
}
