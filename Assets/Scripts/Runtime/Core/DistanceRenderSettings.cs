using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace FPS
{
    public enum DistanceRenderBucket
    {
        Near,
        Mid,
        Far,
        Culled
    }

    [Serializable]
    public struct LayerCullDistance
    {
        public string layerName;
        public float distance;

        public LayerCullDistance(string layerName, float distance)
        {
            this.layerName = layerName;
            this.distance = distance;
        }
    }

    [CreateAssetMenu(fileName = "DistanceRenderSettings", menuName = "FPS/Rendering/Distance Render Settings")]
    public class DistanceRenderSettings : ScriptableObject
    {
        [Header("Distance Buckets")]
        [SerializeField] private float nearDistance = 25f;
        [SerializeField] private float midDistance = 60f;
        [SerializeField] private float farDistance = 100f;
        [SerializeField] private float hysteresis = 5f;
        [SerializeField] private float updateInterval = 0.25f;

        [Header("Bucket Behavior")]
        [SerializeField] private ShadowCastingMode midShadowCasting = ShadowCastingMode.Off;
        [SerializeField] private ShadowCastingMode farShadowCasting = ShadowCastingMode.Off;
        [SerializeField] private AnimatorCullingMode farAnimatorCulling = AnimatorCullingMode.CullUpdateTransforms;
        [SerializeField] private AnimatorCullingMode culledAnimatorCulling = AnimatorCullingMode.CullCompletely;

        [Header("Camera Layer Cull Distances")]
        [SerializeField] private LayerCullDistance[] layerCullDistances =
        {
            new LayerCullDistance("SmallProps", 80f),
            new LayerCullDistance("VFX", 60f),
            new LayerCullDistance("EnemyVisual", 100f)
        };

        public float NearDistance => nearDistance;
        public float MidDistance => midDistance;
        public float FarDistance => farDistance;
        public float Hysteresis => hysteresis;
        public float UpdateInterval => Mathf.Max(0.02f, updateInterval);
        public LayerCullDistance[] LayerCullDistances => layerCullDistances;

        public bool IsValid =>
            nearDistance > 0f
            && midDistance > nearDistance
            && farDistance > midDistance
            && hysteresis >= 0f
            && updateInterval > 0f;

        public DistanceRenderBucket EvaluateBucket(float distance, DistanceRenderBucket currentBucket)
        {
            distance = Mathf.Max(0f, distance);

            if (IsWithinExpandedBucket(distance, currentBucket))
                return currentBucket;

            if (distance <= nearDistance)
                return DistanceRenderBucket.Near;

            if (distance <= midDistance)
                return DistanceRenderBucket.Mid;

            if (distance <= farDistance)
                return DistanceRenderBucket.Far;

            return DistanceRenderBucket.Culled;
        }

        public ShadowCastingMode GetShadowCastingMode(DistanceRenderBucket bucket, ShadowCastingMode original)
        {
            switch (bucket)
            {
                case DistanceRenderBucket.Mid:
                    return midShadowCasting;
                case DistanceRenderBucket.Far:
                case DistanceRenderBucket.Culled:
                    return farShadowCasting;
                default:
                    return original;
            }
        }

        public AnimatorCullingMode GetAnimatorCullingMode(DistanceRenderBucket bucket, AnimatorCullingMode original)
        {
            switch (bucket)
            {
                case DistanceRenderBucket.Far:
                    return farAnimatorCulling;
                case DistanceRenderBucket.Culled:
                    return culledAnimatorCulling;
                default:
                    return original;
            }
        }

        public bool ShouldRender(DistanceRenderBucket bucket)
        {
            return bucket != DistanceRenderBucket.Culled;
        }

        public bool ShouldPlayParticles(DistanceRenderBucket bucket)
        {
            return bucket == DistanceRenderBucket.Near || bucket == DistanceRenderBucket.Mid;
        }

        private bool IsWithinExpandedBucket(float distance, DistanceRenderBucket bucket)
        {
            float min;
            float max;

            switch (bucket)
            {
                case DistanceRenderBucket.Near:
                    min = 0f;
                    max = nearDistance;
                    break;
                case DistanceRenderBucket.Mid:
                    min = nearDistance;
                    max = midDistance;
                    break;
                case DistanceRenderBucket.Far:
                    min = midDistance;
                    max = farDistance;
                    break;
                case DistanceRenderBucket.Culled:
                    min = farDistance;
                    max = float.PositiveInfinity;
                    break;
                default:
                    return false;
            }

            return distance >= Mathf.Max(0f, min - hysteresis) && distance <= max + hysteresis;
        }
    }
}
