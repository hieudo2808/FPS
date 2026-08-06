using UnityEngine;
using Unity.Netcode;

namespace FPS
{
    public class RecoilController : NetworkBehaviour
    {
        private RecoilPattern currentPattern;
        
        private int currentShotIndex;
        private float timeSinceLastShot;
        
        private Vector2 currentRecoil;
        private Vector2 targetRecoil;
        
        private MouseMovement mouseMovement;

        private void Start()
        {
            mouseMovement = GetComponentInParent<MouseMovement>();
            if (mouseMovement == null)
            {
                mouseMovement = FindAnyObjectByType<MouseMovement>();
            }
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (currentPattern == null || mouseMovement == null) return;

            timeSinceLastShot += Time.deltaTime;

            if (timeSinceLastShot > currentPattern.resetCooldown)
            {
                currentShotIndex = 0;
                // Exponential decay return when not firing (framerate-independent)
                float returnFactor = 1f - Mathf.Exp(-currentPattern.returnSpeed * Time.deltaTime);
                targetRecoil = Vector2.Lerp(targetRecoil, Vector2.zero, returnFactor);
            }

            // Smoothly interpolate current recoil towards target recoil (framerate-independent)
            float snapFactor = 1f - Mathf.Exp(-currentPattern.snappiness * Time.deltaTime);
            Vector2 smoothRecoil = Vector2.Lerp(currentRecoil, targetRecoil, snapFactor);
            
            // The difference between frames is what we apply to the camera physically
            Vector2 deltaRecoil = smoothRecoil - currentRecoil;
            currentRecoil = smoothRecoil;

            if (Mathf.Abs(deltaRecoil.x) > 0.0001f || Mathf.Abs(deltaRecoil.y) > 0.0001f)
            {
                mouseMovement.ApplyRecoil(deltaRecoil.x, deltaRecoil.y);
            }
        }

        public void Fire(RecoilPattern pattern)
        {
            if (!IsOwner) return;
            
            currentPattern = pattern;
            
            if (pattern.shots == null || pattern.shots.Length == 0) return;
            
            Vector2 shotRecoil;
            if (currentShotIndex < pattern.shots.Length)
            {
                shotRecoil = pattern.shots[currentShotIndex];
            }
            else
            {
                // After reaching the end of the pattern, oscillate horizontal recoil left/right
                Vector2 lastRecoil = pattern.shots[pattern.shots.Length - 1];
                int overflowShots = currentShotIndex - pattern.shots.Length + 1;
                float sideSign = (overflowShots % 2 == 1) ? -1f : 1f;
                shotRecoil = new Vector2(Mathf.Abs(lastRecoil.x) * sideSign, lastRecoil.y * 0.3f);
            }
            
            targetRecoil += shotRecoil;
            
            currentShotIndex++;
            timeSinceLastShot = 0f;
        }
    }
}
