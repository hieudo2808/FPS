using UnityEngine;

namespace FPS
{
    /// <summary>Mixer bus a sound plays through; each maps to an AudioMixer group of the same name.</summary>
    public enum AudioBus { Music, Ambience, Weapons, Enemies, Player, World, UI }

    /// <summary>
    /// One game sound: a pool of takes with per-play pitch/volume variation, a bus, 3D falloff and a
    /// voice limit. Distant takes replace the near ones when the listener is far enough away.
    /// </summary>
    [CreateAssetMenu(menuName = "FPS/Audio/Sound Event")]
    public sealed class SoundEvent : ScriptableObject
    {
        public AudioBus bus = AudioBus.World;
        public AudioClip[] clips = System.Array.Empty<AudioClip>();
        [Tooltip("Optional: played instead of clips for other players' sounds (3D), e.g. third-person gunshot takes.")]
        public AudioClip[] remoteClips = System.Array.Empty<AudioClip>();
        [Tooltip("Played instead of clips when the listener is at least distantFrom metres away.")]
        public AudioClip[] distantClips = System.Array.Empty<AudioClip>();
        [Min(0f)] public float distantFrom = 35f;

        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random volume offset per play, in decibels (+/-).")]
        [Range(0f, 6f)] public float volumeJitterDb = 1f;
        [Tooltip("Random pitch range per play (1 = unchanged).")]
        public Vector2 pitch = new(0.96f, 1.04f);

        [Header("3D")]
        [Range(0f, 1f)] public float spatialBlend = 1f;
        [Min(0.01f)] public float minDistance = 2f;
        [Min(0.02f)] public float maxDistance = 40f;

        [Header("Limits")]
        [Min(1)] public int maxVoices = 4;
        [Tooltip("Minimum seconds between two plays of this event anywhere.")]
        [Min(0f)] public float cooldown;
        [Range(0, 256)] public int priority = 128;
        public bool loop;

        public bool HasClips => clips != null && clips.Length > 0;
    }
}
