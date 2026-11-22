using UnityEngine;

namespace FPS
{
    /// <summary>A character's voice: pain, going down and dying.</summary>
    [CreateAssetMenu(menuName = "FPS/Audio/Player Voice Set")]
    public sealed class PlayerVoiceSet : ScriptableObject
    {
        public SoundEvent hurt;
        public SoundEvent downed;
        public SoundEvent death;
    }
}
