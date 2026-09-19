using UnityEngine;

namespace FPS
{
    [DisallowMultipleComponent]
    public sealed class CampaignChapterRoot : MonoBehaviour
    {
        public CampaignChapter chapter;
        public Transform environment;
        public Transform[] arrivals;
        public BoxCollider preparationArea;
        public BoxCollider boardingArea;
        public CampaignDoor entryDoor;
        public CampaignDoor exitDoor;
        public DirectorSpawnAnchor[] anchors;
        public MapRecoveryPoint[] recoveries;
        public CampaignInteractable[] objectives;
        public float spawnMinimumDistance = 12;
        public float spawnMaximumDistance = 95;
        public bool Contains(BoxCollider volume, Vector3 point)
        {
            if (volume == null) return false;
            Vector3 local = volume.transform.InverseTransformPoint(point) - volume.center;
            Vector3 half = volume.size * .5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }
    }
}
