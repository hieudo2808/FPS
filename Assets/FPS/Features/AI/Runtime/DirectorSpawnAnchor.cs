using System;
using UnityEngine;

namespace FPS
{
    [Flags]
    public enum DirectorSpawnAnchorType : byte
    {
        None = 0,
        Ambient = 1 << 0,
        Common = 1 << 1,
        Horde = 1 << 2,
        Special = 1 << 3,
        Finale = 1 << 4
    }

    [DisallowMultipleComponent]
    public sealed class DirectorSpawnAnchor : MonoBehaviour
    {
        [SerializeField] private DirectorSpawnAnchorType anchorTypes =
            DirectorSpawnAnchorType.Ambient | DirectorSpawnAnchorType.Common;
        [SerializeField] private DirectorZone zone;
        [SerializeField, Min(0.01f)] private float selectionWeight = 1f;
        [SerializeField] private Vector3 spawnOffset = Vector3.zero;

        public DirectorSpawnAnchorType AnchorTypes => anchorTypes;
        public DirectorZone Zone => zone;
        public float SelectionWeight => Mathf.Max(0.01f, selectionWeight);
        public Vector3 SpawnPosition => transform.TransformPoint(spawnOffset);

        public void Configure(
            DirectorSpawnAnchorType types,
            DirectorZone owningZone,
            float weight = 1f)
        {
            anchorTypes = types;
            zone = owningZone;
            selectionWeight = Mathf.Max(0.01f, weight);
        }
    }
}
