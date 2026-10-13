using UnityEngine;

namespace FPS
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class FactoryPitFillLighting : MonoBehaviour
    {
        public MeshRenderer referenceRoad;

        void OnEnable() => RestoreLighting();
        void Start() => RestoreLighting();

        void RestoreLighting()
        {
            var target = GetComponent<MeshRenderer>();
            if (referenceRoad == null || target.lightmapIndex >= 0) return;
            // ponytail: Borrow adjacent-road GI until this fill gets its own baked lighting.
            target.lightmapIndex = referenceRoad.lightmapIndex;
            target.lightmapScaleOffset = referenceRoad.lightmapScaleOffset;
        }
    }
}
