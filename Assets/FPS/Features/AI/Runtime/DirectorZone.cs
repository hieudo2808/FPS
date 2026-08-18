using UnityEngine;

namespace FPS
{
    public enum DirectorZoneKind : byte
    {
        Playable,
        Safe,
        Cutscene,
        NoSpawn
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DirectorZone : MonoBehaviour
    {
        [SerializeField] private DirectorZoneKind zoneKind = DirectorZoneKind.Playable;
        [SerializeField] private string zoneId = "Zone";

        private BoxCollider cachedBounds;

        public DirectorZoneKind ZoneKind => zoneKind;
        public string ZoneId => zoneId;
        public bool AllowsSpawning => zoneKind == DirectorZoneKind.Playable;

        private void Awake()
        {
            CacheBounds();
        }

        public bool Contains(Vector3 worldPosition)
        {
            CacheBounds();
            Vector3 local = transform.InverseTransformPoint(worldPosition) - cachedBounds.center;
            Vector3 half = cachedBounds.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x
                && Mathf.Abs(local.y) <= half.y
                && Mathf.Abs(local.z) <= half.z;
        }

        public void Configure(string id, DirectorZoneKind kind, Vector3 size)
        {
            zoneId = id;
            zoneKind = kind;
            CacheBounds();
            cachedBounds.isTrigger = true;
            cachedBounds.size = size;
        }

        private void CacheBounds()
        {
            if (cachedBounds == null)
                cachedBounds = GetComponent<BoxCollider>();
        }
    }
}
