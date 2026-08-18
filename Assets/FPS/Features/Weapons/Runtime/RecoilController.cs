using UnityEngine;
using Unity.Netcode;

namespace FPS
{
    public class RecoilController : NetworkBehaviour
    {
        private RecoilPattern currentPattern;
        
        private int currentShotIndex;
        private uint spraySequence;
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
                if (currentShotIndex != 0)
                {
                    currentShotIndex = 0;
                    spraySequence++;
                }
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

            if (currentPattern != pattern)
            {
                currentShotIndex = 0;
                spraySequence++;
            }
            currentPattern = pattern;

            if (pattern.shots == null || pattern.shots.Length == 0) return;

            Vector2 shotRecoil = pattern.GetShot(currentShotIndex, spraySequence);
            targetRecoil += shotRecoil;
            
            currentShotIndex++;
            timeSinceLastShot = 0f;
        }
    }
}
