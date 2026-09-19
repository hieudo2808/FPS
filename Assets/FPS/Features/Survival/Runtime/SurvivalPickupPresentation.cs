using UnityEngine;

namespace FPS
{
    public sealed class SurvivalPickupPresentation : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Light glow;
        [SerializeField] private Material outlineMaterial;
        private Renderer[] outlineRenderers;
        private Vector3 rest;
        private bool highlighted;
        private bool available = true;
        private void Awake()
        {
            if (visual == null) return;
            rest = visual.localPosition;
            if (outlineMaterial == null) return;
            var meshes = visual.GetComponentsInChildren<MeshFilter>();
            var shells = new System.Collections.Generic.List<Renderer>();
            foreach (var mesh in meshes)
            {
                if (mesh.sharedMesh == null) continue;
                var shell = new GameObject("InteractionOutline", typeof(MeshFilter), typeof(MeshRenderer));
                shell.transform.SetParent(mesh.transform, false);
                shell.layer = mesh.gameObject.layer;
                shell.GetComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                var renderer = shell.GetComponent<MeshRenderer>();
                var materials = new Material[mesh.sharedMesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = outlineMaterial;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;
                shells.Add(renderer);
            }
            outlineRenderers = shells.ToArray();
        }
        public void Highlight(bool value)
        {
            highlighted = value && available;
            if (outlineRenderers != null)
                foreach (var renderer in outlineRenderers) renderer.enabled = highlighted;
        }
        public void SetAvailable(bool value)
        {
            available = value;
            if (!available) Highlight(false);
            if (glow != null) glow.enabled = available;
        }
        private void OnDisable() => Highlight(false);
        private void Update()
        {
            if (visual != null)
            {
                visual.localPosition = rest + Vector3.up * (Mathf.Sin(Time.time * 1.5f) * .025f);
                visual.Rotate(Vector3.up, Time.deltaTime * 12f, Space.Self);
            }
            if (glow != null) glow.intensity = highlighted ? 1.2f : .25f;
        }
    }
}
