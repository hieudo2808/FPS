using UnityEngine;

namespace FPS
{
    [CreateAssetMenu(menuName = "FPS/Survival/Grenade Throw")]
    public sealed class GrenadeThrowDefinition : ScriptableObject
    {
        public const string LayerName = "Grenade Throw";
        public const string StateName = "Throw";
        public AnimationClip firstPersonClip;
        public AnimationClip thirdPersonClip;
        [Min(.05f)] public float releaseTime = 1f / 3f;
        [Min(.1f)] public float duration = 1.25f;
        [Min(.01f)] public float blendIn = .08f;
        [Min(.01f)] public float blendOut = .15f;
        public Vector3 releaseOffset = new(.3f, 0f, .72f);

        public bool IsValid => firstPersonClip != null && thirdPersonClip != null
            && float.IsFinite(releaseTime) && float.IsFinite(duration)
            && releaseTime > 0f && duration > releaseTime;
    }
}
