using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace FPS
{
    /// <summary>Handle to a playing voice; stays safe to use after the voice ends or is reused.</summary>
    public readonly struct AudioVoice
    {
        public readonly int Slot;
        public readonly int Id;
        public AudioVoice(int slot, int id) { Slot = slot; Id = id; }
        public bool IsValid => Id != 0;
    }

    /// <summary>What a started voice is playing, for diagnostics and capture tools.</summary>
    public readonly struct AudioStartInfo
    {
        public readonly int Id;
        public readonly SoundEvent Sound;
        public readonly AudioClip Clip;
        public readonly AudioBus Bus;
        public readonly Vector3 Position;
        public readonly bool Local;
        public readonly float Volume, Pitch, SpatialBlend, MinDistance, MaxDistance;
        public readonly bool Loop;

        public AudioStartInfo(int id, SoundEvent sound, AudioClip clip, AudioBus bus, Vector3 position, bool local,
            float volume, float pitch, float spatialBlend, float minDistance, float maxDistance, bool loop)
        {
            Id = id; Sound = sound; Clip = clip; Bus = bus; Position = position; Local = local;
            Volume = volume; Pitch = pitch; SpatialBlend = spatialBlend; MinDistance = minDistance; MaxDistance = maxDistance; Loop = loop;
        }
    }

    /// <summary>
    /// Client-side sound playback: pooled AudioSources routed through the project mixer, take variation,
    /// voice limits, distance culling and near/distant takes. Every peer plays what it sees replicated;
    /// nothing here touches gameplay state.
    /// </summary>
    public static class GameAudio
    {
        private static GameAudioHost host;
        private static readonly Dictionary<AudioBus, AudioMixerGroup> groups = new();
        private static AudioMixer groupsMixer;

        public static int PlayedCount { get; private set; }
        public static readonly int[] PlayedPerBus = new int[System.Enum.GetValues(typeof(AudioBus)).Length];
        public static string LastEventName { get; private set; }
        private static readonly Dictionary<string, int> counts = new();

        /// <summary>How many voices of the named SoundEvent this peer has started.</summary>
        public static int CountOf(string eventName) => counts.TryGetValue(eventName, out int n) ? n : 0;

        /// <summary>"name=count;..." for every event played on this peer (diagnostics).</summary>
        public static string CountsSummary()
        {
            var parts = new List<string>();
            foreach (var pair in counts) parts.Add(pair.Key + "=" + pair.Value);
            parts.Sort(System.StringComparer.Ordinal);
            return string.Join(";", parts);
        }

        public static MusicCue CurrentMusic => GameMusic.CurrentCue;
        public static MusicCue WantedMusic => GameMusic.WantedCue;
        public static bool AmbiencePlaying => GameMusic.AmbiencePlaying;
        /// <summary>Raised for every voice actually started: event (null for a raw clip), position, local 2D.</summary>
        public static event System.Action<SoundEvent, Vector3, bool> Played;
        /// <summary>Every voice started on this peer (pooled voices and enemy one-shots), with what it plays.</summary>
        public static event System.Action<AudioStartInfo> VoiceStarted;
        /// <summary>A pooled voice ended (finished, faded, stolen or stopped); the id matches VoiceStarted.</summary>
        public static event System.Action<int> VoiceEnded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            host = null;
            groups.Clear();
            groupsMixer = null;
            PlayedCount = 0;
            counts.Clear();
            System.Array.Clear(PlayedPerBus, 0, PlayedPerBus.Length);
            LastEventName = null;
            Played = null;
            VoiceStarted = null;
            VoiceEnded = null;
        }

        // Music and ambience follow the director from the first gameplay frame, whichever scene starts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (CanPlay && GameAudioSettings.Load() != null) EnsureRunning();
        }

        /// <summary>No sound in batch mode (dedicated server, CI).</summary>
        public static bool CanPlay => Application.isPlaying && !Application.isBatchMode;

        internal static GameAudioHost Host
        {
            get
            {
                if (host == null && CanPlay)
                    host = new GameObject("Game Audio").AddComponent<GameAudioHost>();
                return host;
            }
        }

        /// <summary>Makes sure the music director and voice pool exist (they create themselves on first use).</summary>
        public static void EnsureRunning() => _ = Host;

        public static AudioMixerGroup Group(AudioBus bus)
        {
            var settings = GameAudioSettings.Load();
            AudioMixer mixer = settings != null ? settings.mixer : null;
            if (mixer == null) return null;
            if (groupsMixer != mixer) { groups.Clear(); groupsMixer = mixer; }
            if (groups.TryGetValue(bus, out var group) && group != null) return group;
            string name = bus.ToString();
            group = null;
            foreach (var candidate in mixer.FindMatchingGroups(string.Empty))
                if (candidate != null && candidate.name == name) { group = candidate; break; }
            if (group == null && bus != AudioBus.Music)
                foreach (var candidate in mixer.FindMatchingGroups(string.Empty))
                    if (candidate != null && candidate.name == "SFX") { group = candidate; break; }
            groups[bus] = group;
            return group;
        }

        /// <summary>3D sound at a point, optionally following a transform.</summary>
        public static AudioVoice Play(SoundEvent sound, Vector3 position, float volumeScale = 1f, Transform follow = null)
            => sound != null && CanPlay ? Host.Play(sound, null, sound.bus, position, follow, false, volumeScale, 0f, 0f) : default;

        /// <summary>Sound heard by the local player as their own (2D, no falloff).</summary>
        public static AudioVoice PlayLocal(SoundEvent sound, float volumeScale = 1f)
            => sound != null && CanPlay ? Host.Play(sound, null, sound.bus, Vector3.zero, null, true, volumeScale, 0f, 0f) : default;

        /// <summary>A bare clip (legacy fields) on a bus.</summary>
        public static AudioVoice PlayClip(AudioClip clip, AudioBus bus, Vector3 position, float volume = 1f,
            float minDistance = 2f, float maxDistance = 35f, Transform follow = null, bool local = false)
            => clip != null && CanPlay ? Host.Play(null, clip, bus, position, follow, local, volume, minDistance, maxDistance) : default;

        public static void Stop(AudioVoice voice, float fadeSeconds = 0.15f)
        {
            if (voice.IsValid && host != null) host.Stop(voice, fadeSeconds);
        }

        public static bool IsPlaying(AudioVoice voice) => voice.IsValid && host != null && host.IsPlaying(voice);

        public static bool TryGetListenerPosition(out Vector3 position)
        {
            var listener = host != null ? host.Listener : null;
            position = listener != null ? listener.transform.position : default;
            return listener != null;
        }

        internal static void ReportStart(in AudioStartInfo info) => VoiceStarted?.Invoke(info);
        internal static void ReportEnd(int id) => VoiceEnded?.Invoke(id);

        /// <summary>A one-shot played outside the pool (an enemy's own AudioSource), reported for diagnostics.</summary>
        public static void ReportExternal(AudioClip clip, AudioBus bus, Vector3 position, float volume, float pitch,
            float minDistance, float maxDistance)
        {
            if (clip == null || VoiceStarted == null) return;
            VoiceStarted.Invoke(new AudioStartInfo(0, null, clip, bus, position, false, volume, pitch, 1f, minDistance, maxDistance, false));
        }

        internal static void Report(SoundEvent sound, AudioClip clip, AudioBus bus, Vector3 position, bool local)
        {
            PlayedCount++;
            PlayedPerBus[(int)bus]++;
            // A bare clip (weapon foley) is counted under its clip name.
            LastEventName = sound != null ? sound.name : clip != null ? clip.name : "clip";
            counts[LastEventName] = CountOf(LastEventName) + 1;
            Played?.Invoke(sound, position, local);
        }
    }
}
