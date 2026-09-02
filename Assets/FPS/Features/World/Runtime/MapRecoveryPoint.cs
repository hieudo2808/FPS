using UnityEngine;

namespace FPS
{
    [DisallowMultipleComponent]
    public sealed class MapRecoveryPoint : MonoBehaviour
    {
        [SerializeField] private string pointId = "Recovery";
        [SerializeField, Min(0.5f)] private float clearanceRadius = 1.25f;

        public string PointId => pointId;
        public float ClearanceRadius => clearanceRadius;

        public void Configure(string id, float radius = 1.25f)
        {
            pointId = string.IsNullOrWhiteSpace(id) ? name : id;
            clearanceRadius = Mathf.Max(0.5f, radius);
        }
    }
}
