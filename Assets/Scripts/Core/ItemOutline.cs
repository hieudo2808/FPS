using UnityEngine;

namespace FPS
{
    public class ItemOutline : MonoBehaviour
    {
        [Tooltip("The outline/glow material to add when highlighted. " +
                 "Use any outline shader (e.g. Standard with inverted normals, URP outline, etc.)")]
        [SerializeField] private Material outlineMaterial;

        [Tooltip("Optional: pulse the outline alpha for a breathing glow effect.")]
        [SerializeField] private bool pulseEffect = true;

        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseMinAlpha = 0.4f;
        [SerializeField] private float pulseMaxAlpha = 1f;

        private Renderer[] renderers;
        private Material[][] originalMaterials;
        private bool isOutlineActive = false;

        private MaterialPropertyBlock propertyBlock;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int OutlineColorProperty = Shader.PropertyToID("_OutlineColor");

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            originalMaterials = new Material[renderers.Length][];

            for (int i = 0; i < renderers.Length; i++)
                originalMaterials[i] = renderers[i].materials;

            propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (!isOutlineActive || !pulseEffect || outlineMaterial == null) return;

            float alpha = Mathf.Lerp(
                pulseMinAlpha,
                pulseMaxAlpha,
                (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f
            );

            foreach (var r in renderers)
            {
                r.GetPropertyBlock(propertyBlock);
                Color col = outlineMaterial.HasProperty(OutlineColorProperty)
                    ? outlineMaterial.GetColor(OutlineColorProperty)
                    : outlineMaterial.color;

                col.a = alpha;

                if (outlineMaterial.HasProperty(OutlineColorProperty))
                    propertyBlock.SetColor(OutlineColorProperty, col);
                else
                    propertyBlock.SetColor(ColorProperty, col);

                r.SetPropertyBlock(propertyBlock);
            }
        }

        public void ShowOutline()
        {
            if (isOutlineActive || outlineMaterial == null) return;
            isOutlineActive = true;

            for (int i = 0; i < renderers.Length; i++)
            {
                var mats = renderers[i].materials;
                var newMats = new Material[mats.Length + 1];

                for (int j = 0; j < mats.Length; j++)
                    newMats[j] = mats[j];

                newMats[newMats.Length - 1] = outlineMaterial;
                renderers[i].materials = newMats;
            }
        }

        public void HideOutline()
        {
            if (!isOutlineActive) return;
            isOutlineActive = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].materials = originalMaterials[i];
                renderers[i].SetPropertyBlock(null);
            }
        }

        private void OnDisable()
        {
            HideOutline();
        }
    }
}