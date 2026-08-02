using UnityEngine;

namespace FPS
{
    [CreateAssetMenu(fileName = "New Recoil Pattern", menuName = "FPS/Recoil Pattern")]
    public class RecoilPattern : ScriptableObject
    {
        [Tooltip("Each element represents the kick for a consecutive shot. X is upward pitch, Y is horizontal yaw.")]
        public Vector2[] shots;

        [Tooltip("How fast the gun snaps to the new recoil angle (higher = faster kick).")]
        public float snappiness = 20f;

        [Tooltip("How fast the camera returns to the original position when stopped firing.")]
        public float returnSpeed = 5f;
        
        [Tooltip("Time without firing before the recoil pattern completely resets to shot index 0.")]
        public float resetCooldown = 0.25f;
    }
}
