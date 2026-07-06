using UnityEngine;

namespace FPS
{
    public enum HitZone
    {
        Body,
        Head
    }

    public class EnemyHitbox : MonoBehaviour
    {
        [SerializeField] private HitZone hitZone = HitZone.Body;

        public bool IsHeadshot => hitZone == HitZone.Head;
    }
}
