using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    /// <summary>Procedural item pose layered after the existing weapon animator; authored transforms are restored.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class SurvivalPresentation : NetworkBehaviour
    {
        private SurvivalInventory inventory;
        private Transform arms;
        private Transform cameraTransform;
        private Vector3 armsRest;
        private Vector3 appliedShake;
        private float shakeUntil, throwUntil;
        private bool armsOffsetApplied;
        private GameObject item;
        private int itemIndex = -1;
        private AudioSource treatmentAudio;
        private bool wasUsing;
        public override void OnNetworkSpawn()
        {
            inventory = GetComponent<SurvivalInventory>();
            if (IsOwner)
            {
                arms = GetComponent<PlayerVisibilityController>()?.FirstPersonArms?.transform;
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
        }
        public override void OnNetworkDespawn()
        {
            SurvivalEffects.Blast -= OnBlast;
            RestoreArms();
            RestoreShake();
            if (item != null) Destroy(item);
            if (treatmentAudio != null) Destroy(treatmentAudio);
        }
        private void OnDisable() { RestoreArms(); RestoreShake(); if (treatmentAudio != null) treatmentAudio.Stop(); }
        private void RestoreArms() { if (armsOffsetApplied && arms != null) arms.localPosition = armsRest; armsOffsetApplied = false; }
        private void RestoreShake() { if (cameraTransform != null) cameraTransform.localPosition -= appliedShake; appliedShake = Vector3.zero; }
        private void Update() { RestoreArms(); RestoreShake(); }
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
                    itemIndex = index;
                }
                if (item != null)
                {
                    item.SetActive(true);
                    float progress = inventory.UseProgress;
                    float enter = Mathf.Clamp01(progress * 8f);
                    item.transform.localPosition = IsOwner ? new Vector3(.22f, Mathf.Lerp(-.55f, -.25f, enter) + Mathf.Sin(progress * 22f) * .015f, .48f) : new Vector3(.28f, 1.05f, .3f);
                    item.transform.localRotation = Quaternion.Euler(10f + Mathf.Sin(progress * 20f) * 5f, -25f, -10f);
                }
            }
            else if (item != null) item.SetActive(false);
            if (IsOwner && arms != null && (usingItem || Time.time < throwUntil))
            {
                armsRest = arms.localPosition;
                armsOffsetApplied = true;
                arms.localPosition += new Vector3(0, usingItem ? -.14f : -.06f, -.04f);
            }
            if (IsOwner && cameraTransform != null && Time.time < shakeUntil)
            {
                appliedShake = new Vector3(Mathf.Sin(Time.time * 97f), Mathf.Cos(Time.time * 113f), 0) * .006f;
                cameraTransform.localPosition += appliedShake;
            }
        }
    }
}
