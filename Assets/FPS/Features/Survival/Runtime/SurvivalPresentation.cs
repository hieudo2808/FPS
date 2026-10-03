using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    /// <summary>Procedural item pose layered after the existing weapon animator; authored transforms are restored.</summary>
    [DefaultExecutionOrder(30000)]
    public sealed class SurvivalPresentation : NetworkBehaviour
    {
        [SerializeField] private SurvivalArmPose firstPersonLeft = new();
        [SerializeField] private SurvivalArmPose firstPersonRight = new();
        [SerializeField] private SurvivalArmPose thirdPersonLeft = new();
        [SerializeField] private SurvivalArmPose thirdPersonRight = new();
        private SurvivalInventory inventory;
        private Transform arms;
        private Transform cameraTransform;
        private Vector3 appliedShake;
        private float shakeUntil, throwUntil;
        private GameObject item;
        private int itemIndex = -1;
        private AudioSource treatmentAudio;
        private bool wasUsing;
        private Renderer[] weaponRenderers;
        private bool[] previousForceHidden;
        public override void OnNetworkSpawn()
        {
            inventory = GetComponent<SurvivalInventory>();
            var visibility = GetComponent<PlayerVisibilityController>();
            if (IsOwner)
            {
                arms = visibility?.FirstPersonArms?.transform;
                cameraTransform = GetComponent<MouseMovement>()?.BodyCam?.transform;
                SurvivalEffects.Blast += OnBlast;
            }
            treatmentAudio = gameObject.AddComponent<AudioSource>();
            treatmentAudio.playOnAwake = false;
            treatmentAudio.loop = true;
            treatmentAudio.spatialBlend = IsOwner ? 0f : 1f;
            treatmentAudio.volume = .2f;
            treatmentAudio.maxDistance = 8f;
            treatmentAudio.clip = SurvivalCatalog.Load()?.treatmentClip;
            var renderers = new System.Collections.Generic.HashSet<Renderer>();
            void AddWeaponRenderers(GameObject weapon)
            {
                if (weapon == null) return;
                foreach (var renderer in weapon.GetComponentsInChildren<Renderer>(true)) renderers.Add(renderer);
            }
            if (arms != null)
                foreach (var weapon in arms.GetComponentsInChildren<Weapon>(true)) AddWeaponRenderers(weapon.gameObject);
            if (!IsOwner && visibility != null)
            {
                if (visibility.ThirdPersonWeaponSlots != null)
                    foreach (var weapon in visibility.ThirdPersonWeaponSlots) AddWeaponRenderers(weapon);
                // Primary pickups share slot zero but have separate third-person models.
                if (visibility.ThirdPersonWeaponPresentations != null)
                    foreach (var presentation in visibility.ThirdPersonWeaponPresentations) AddWeaponRenderers(presentation?.WeaponObject);
            }
            weaponRenderers = new Renderer[renderers.Count];
            renderers.CopyTo(weaponRenderers);
            previousForceHidden = new bool[weaponRenderers.Length];
            for (int i = 0; i < weaponRenderers.Length; i++) previousForceHidden[i] = weaponRenderers[i].forceRenderingOff;
        }
        public override void OnNetworkDespawn()
        {
            SurvivalEffects.Blast -= OnBlast;
            RestoreShake();
            RestorePose();
            HideWeapons(false);
            if (item != null) Destroy(item);
            if (treatmentAudio != null) Destroy(treatmentAudio);
        }
        private void OnDisable()
        {
            RestoreShake(); RestorePose(); HideWeapons(false);
            if (item != null) item.SetActive(false);
            if (treatmentAudio != null) treatmentAudio.Stop();
        }
        private void RestoreShake() { if (cameraTransform != null) cameraTransform.localPosition -= appliedShake; appliedShake = Vector3.zero; }
        private void RestorePose()
        { firstPersonLeft.Restore(); firstPersonRight.Restore(); thirdPersonLeft.Restore(); thirdPersonRight.Restore(); }
        private void HideWeapons(bool hidden)
        {
            if (weaponRenderers == null) return;
            for (int i = 0; i < weaponRenderers.Length; i++)
                if (weaponRenderers[i] != null) weaponRenderers[i].forceRenderingOff = hidden || previousForceHidden[i];
        }
        private void Update() { RestoreShake(); RestorePose(); }
        private void OnBlast(Vector3 point)
        {
            if (cameraTransform != null && Vector3.Distance(cameraTransform.position, point) < 9f) shakeUntil = Time.time + .16f;
        }
        public void PlayThrow()
        {
            throwUntil = Time.time + .3f;
            SurvivalEffects.PlayOneShot(SurvivalCatalog.Load()?.throwClip, transform.position, .3f);
        }
        private void LateUpdate()
        {
            if (!IsSpawned || inventory == null) return;
            bool usingItem = inventory.IsUsingItem;
            HideWeapons(inventory.IsBusy || Time.time < throwUntil);
            if (usingItem != wasUsing)
            {
                if (usingItem && treatmentAudio.clip != null) treatmentAudio.Play(); else treatmentAudio.Stop();
                wasUsing = usingItem;
            }
            if (usingItem)
            {
                int index = inventory.ActiveConsumable == ConsumableKind.Medkit ? 2 : 3;
                var catalog = SurvivalCatalog.Load();
                if (index != itemIndex && catalog != null && catalog.itemVisuals[index] != null)
                {
                    if (item != null) Destroy(item);
                    item = Instantiate(catalog.itemVisuals[index], IsOwner && cameraTransform != null ? cameraTransform : transform);
                    int layer = IsOwner && arms != null ? arms.gameObject.layer : gameObject.layer;
                    foreach (var t in item.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                    itemIndex = index;
                }
                if (item != null)
                {
                    item.SetActive(true);
                    float progress = inventory.UseProgress;
                    float enter = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress * 8f));
                    // Keep the grip inside the narrower first-person weapon camera's frustum.
                    item.transform.localPosition = IsOwner ? new Vector3(.18f, Mathf.Lerp(-.4f, -.12f, enter) + Mathf.Sin(progress * 22f) * .01f, .6f) : new Vector3(.28f, 1.05f, .3f);
                    item.transform.localRotation = Quaternion.Euler(10f + Mathf.Sin(progress * 20f) * 5f, -25f, -10f);
                    var left = IsOwner ? firstPersonLeft : thirdPersonLeft;
                    var right = IsOwner ? firstPersonRight : thirdPersonRight;
                    Transform basis = IsOwner && cameraTransform != null ? cameraTransform : transform;
                    Vector3 rightGrip = item.transform.position + basis.right * .065f - basis.up * .025f;
                    Vector3 leftGrip = item.transform.position - basis.right * .08f - basis.up * .045f;
                    if (index == 3) leftGrip -= basis.right * .04f;
                    right.Apply(rightGrip, basis.right - basis.up, enter);
                    left.Apply(leftGrip, -basis.right - basis.up, enter);
                }
            }
            else if (item != null) item.SetActive(false);
            if (!usingItem && inventory.ThrowDefinition == null && Time.time < throwUntil)
            {
                float phase = 1f - (throwUntil - Time.time) / .3f;
                Transform basis = IsOwner && cameraTransform != null ? cameraTransform : transform;
                Vector3 origin = IsOwner ? basis.position : transform.position + Vector3.up * 1.3f;
                var right = IsOwner ? firstPersonRight : thirdPersonRight;
                right.Apply(origin + basis.right * .22f - basis.up * .13f + basis.forward * (.28f + phase * .42f), basis.right - basis.up, Mathf.Sin(phase * Mathf.PI));
            }
            if (IsOwner && cameraTransform != null && Time.time < shakeUntil)
            {
                appliedShake = new Vector3(Mathf.Sin(Time.time * 97f), Mathf.Cos(Time.time * 113f), 0) * .006f;
                cameraTransform.localPosition += appliedShake;
            }
        }
    }
}
