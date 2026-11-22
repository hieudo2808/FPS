using System;
using UnityEngine;
using UnityEngine.Audio;

namespace FPS
{
    /// <summary>Project audio wiring: the mixer, the director-driven music cues and the gameplay ambience.</summary>
    [CreateAssetMenu(menuName = "FPS/Audio/Game Audio Settings")]
    public sealed class GameAudioSettings : ScriptableObject
    {
        public const string ResourceName = "GameAudioSettings";

        [Serializable]
        public struct MusicTrack
        {
            public AudioClip clip;
            [Tooltip("Track gain, used to level loud and quiet tracks against each other.")]
            [Range(0f, 1f)] public float volume;
        }

        public AudioMixer mixer;

        [Header("Music by director pacing")]
        public MusicTrack calm;
        public MusicTrack build;
        public MusicTrack combat;
        public MusicTrack tank;
        [Min(0.1f)] public float crossfadeSeconds = 2.5f;
        [Tooltip("Seconds a calmer cue must be wanted before the music steps down.")]
        [Min(0f)] public float stepDownHoldSeconds = 8f;

        [Header("Gameplay ambience")]
        public AudioClip ambienceBed;
        [Range(0f, 1f)] public float ambienceBedVolume = 0.7f;
        public AudioClip ambienceWind;
        [Range(0f, 1f)] public float ambienceWindVolume = 0.35f;
        [Min(0.1f)] public float ambienceFadeSeconds = 4f;

        [Tooltip("Scenes with their own menu music; gameplay music and ambience stay silent there.")]
        public string[] menuScenes = { "MainMenu", "LobbyScene" };

        public MusicTrack Track(MusicCue cue)
        {
            switch (cue)
            {
                case MusicCue.Calm: return calm;
                case MusicCue.Build: return build;
                case MusicCue.Combat: return combat;
                case MusicCue.Tank: return tank;
                default: return default;
            }
        }

        public bool IsMenuScene(string sceneName) => Array.IndexOf(menuScenes, sceneName) >= 0;

        private static GameAudioSettings cached;
        public static GameAudioSettings Load() => cached != null ? cached : cached = Resources.Load<GameAudioSettings>(ResourceName);
    }
}
