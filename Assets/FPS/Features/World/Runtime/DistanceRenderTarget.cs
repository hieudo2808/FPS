using UnityEngine;
using UnityEngine.Rendering;

namespace FPS
{
    public class DistanceRenderTarget : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Animator animator;
        [SerializeField] private ParticleSystem[] particleSystems;
        [SerializeField] private bool includeChildren = true;
        [SerializeField] private bool affectAnimator = true;
        [SerializeField] private bool affectParticles = true;

        private ShadowCastingMode[] originalShadowCastingModes;
        private AnimatorCullingMode originalAnimatorCullingMode;
        private bool hasCachedReferences;
        private DistanceRenderBucket currentBucket = DistanceRenderBucket.Near;

        public DistanceRenderBucket CurrentBucket => currentBucket;

        private void Awake()
        {
            CacheReferences();
        }

        private void Reset()
        {
            CacheReferences();
        }

        public void CacheReferences()
        {
            if (includeChildren)
            {
                if (renderers == null || renderers.Length == 0)
                    renderers = GetComponentsInChildren<Renderer>(true);

                if (particleSystems == null || particleSystems.Length == 0)
                    particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            }

            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            originalShadowCastingModes = new ShadowCastingMode[renderers != null ? renderers.Length : 0];
            for (int i = 0; i < originalShadowCastingModes.Length; i++)
                originalShadowCastingModes[i] = renderers[i] != null ? renderers[i].shadowCastingMode : ShadowCastingMode.Off;

            originalAnimatorCullingMode = animator != null ? animator.cullingMode : AnimatorCullingMode.AlwaysAnimate;
            hasCachedReferences = true;
        }

        public void ApplyBucket(DistanceRenderBucket bucket, DistanceRenderSettings settings)
        {
            if (settings == null)
                return;

            if (!hasCachedReferences)
                CacheReferences();

            currentBucket = bucket;
            bool shouldRender = settings.ShouldRender(bucket);

            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer targetRenderer = renderers[i];
                    if (targetRenderer == null)
                        continue;

                    targetRenderer.enabled = shouldRender;
                    ShadowCastingMode original = i < originalShadowCastingModes.Length
                        ? originalShadowCastingModes[i]
                        : ShadowCastingMode.On;
                    targetRenderer.shadowCastingMode = settings.GetShadowCastingMode(bucket, original);
                }
            }

            if (affectAnimator && animator != null)
                animator.cullingMode = settings.GetAnimatorCullingMode(bucket, originalAnimatorCullingMode);

            if (affectParticles)
                ApplyParticlePolicy(settings.ShouldPlayParticles(bucket));
        }

        private void ApplyParticlePolicy(bool shouldPlay)
        {
            if (particleSystems == null)
                return;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps == null)
                    continue;

                if (!shouldPlay && ps.isPlaying)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }
    }
}
