using UnityEngine;

namespace FPS
{
    /// <summary>Plays authored upper-body layers from the server clock. Never spawns or consumes a grenade.</summary>
    [DefaultExecutionOrder(29000)]
    [RequireComponent(typeof(SurvivalInventory))]
    public sealed class GrenadeThrowAnimation : MonoBehaviour
    {
        [SerializeField] private Animator firstPersonAnimator;
        [SerializeField] private Animator thirdPersonAnimator;
        [SerializeField] private Transform firstPersonHand;
        [SerializeField] private Transform thirdPersonHand;
        [SerializeField] private Vector3 firstPersonGripOffset = new(.05f, -.01f, 0f);
        [SerializeField] private Vector3 thirdPersonGripOffset = new(.05f, -.01f, 0f);
        [SerializeField] private Vector3 gripRotation = new(0f, 0f, 90f);
        [SerializeField] private Vector3 thirdPersonGripRotation = new(0f, 0f, 90f);
        private SurvivalInventory inventory;
        private GameObject heldGrenade;
        private ThrowableKind heldKind;
        private Animator activeAnimator;
        private int activeLayer = -1;
        private uint soundedSequence;
        private bool hasSounded;

        private void Awake() => inventory = GetComponent<SurvivalInventory>();
        private void Update()
        {
            if (inventory == null || !inventory.IsSpawned || !inventory.IsThrowing)
            { ResetPresentation(); return; }
            var definition = inventory.ThrowDefinition;
            var state = inventory.ThrowState;
            Animator animator = inventory.IsOwner ? firstPersonAnimator : thirdPersonAnimator;
            if (animator == null || !animator.isActiveAndEnabled || definition == null) return;
            if (activeAnimator != animator)
            {
                ResetPresentation();
                activeAnimator = animator;
            }
            activeLayer = animator.GetLayerIndex(GrenadeThrowDefinition.LayerName);
            if (activeLayer < 0) return;
            float elapsed = (float)(inventory.ServerNow - state.StartedAt);
            float weight = Mathf.Min(Mathf.Clamp01(elapsed / Mathf.Max(.01f, definition.blendIn)),
                Mathf.Clamp01((definition.duration - elapsed) / Mathf.Max(.01f, definition.blendOut)));
            animator.SetLayerWeight(activeLayer, weight);
            animator.Play(Animator.StringToHash(GrenadeThrowDefinition.LayerName + "." + GrenadeThrowDefinition.StateName),
                activeLayer, Mathf.Clamp01(elapsed / definition.duration));
            if ((!hasSounded || soundedSequence != state.Sequence) && inventory.ServerNow >= state.ReleaseAt)
            {
                soundedSequence = state.Sequence; hasSounded = true;
                SurvivalEffects.PlayOneShot(SurvivalCatalog.Load()?.throwClip, transform.position, .3f);
            }
        }

        private void LateUpdate()
        {
            if (inventory == null || !inventory.IsSpawned || !inventory.IsThrowing
                || inventory.ServerNow >= inventory.ThrowState.ReleaseAt)
            { if (heldGrenade != null) heldGrenade.SetActive(false); return; }
            Transform hand = inventory.IsOwner ? firstPersonHand : thirdPersonHand;
            if (hand == null) return;
            ThrowableKind kind = inventory.ThrowState.Kind;
            if (heldGrenade == null || heldKind != kind)
            {
                if (heldGrenade != null) Destroy(heldGrenade);
                var catalog = SurvivalCatalog.Load();
                int index = (int)kind;
                if (catalog == null || catalog.itemVisuals.Length <= index || catalog.itemVisuals[index] == null) return;
                heldGrenade = Instantiate(catalog.itemVisuals[index], transform);
                heldGrenade.name = "Held grenade";
                heldKind = kind;
                int layer = LayerMask.NameToLayer(inventory.IsOwner ? "FirstPerson" : "ThirdPerson");
                if (layer >= 0) foreach (var child in heldGrenade.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
            }
            heldGrenade.SetActive(true);
            Vector3 grip = inventory.IsOwner ? firstPersonGripOffset : thirdPersonGripOffset;
            Vector3 rotation = inventory.IsOwner ? gripRotation : thirdPersonGripRotation;
            // Imported bones are scaled by 100; offsets are in metres, independent of that import scale.
            heldGrenade.transform.SetPositionAndRotation(hand.position + hand.rotation * grip,
                hand.rotation * Quaternion.Euler(rotation));
        }

        private void ResetPresentation()
        {
            if (activeAnimator != null && activeLayer >= 0 && activeLayer < activeAnimator.layerCount)
                activeAnimator.SetLayerWeight(activeLayer, 0f);
            activeAnimator = null; activeLayer = -1;
            if (heldGrenade != null) heldGrenade.SetActive(false);
        }
        private void OnDisable() => ResetPresentation();
        private void OnDestroy() { ResetPresentation(); if (heldGrenade != null) Destroy(heldGrenade); }
    }
}
