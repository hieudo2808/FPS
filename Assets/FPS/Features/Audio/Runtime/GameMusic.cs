using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS
{
    /// <summary>
    /// Gameplay music and ambience. Every peer reads the replicated director phase and whether a Tank is
    /// alive, then crossfades between the calm / build / combat / Tank tracks. Calmer cues wait a few
    /// seconds before stepping down; long tracks resume where they left off.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class GameMusic : MonoBehaviour
    {
        public static MusicCue CurrentCue { get; private set; }
        public static MusicCue WantedCue { get; private set; }
        public static bool AmbiencePlaying { get; private set; }

        private readonly AudioSource[] music = new AudioSource[2];
        private readonly float[] musicTarget = new float[2];
        private readonly Dictionary<MusicCue, float> resumeAt = new();
        private AudioSource bed, wind;
        private int active;
        private MusicCue playing = MusicCue.None;
        private MusicCue wanted = MusicCue.None;
        private float wantedSince;
        private float nextTankScan;
        private bool tankAlive;
        private float ambienceLevel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CurrentCue = MusicCue.None;
            WantedCue = MusicCue.None;
            AmbiencePlaying = false;
        }

        private void Awake()
        {
            for (int i = 0; i < music.Length; i++) music[i] = CreateSource("Music " + i);
            bed = CreateSource("Ambience Bed");
            wind = CreateSource("Ambience Wind");
        }

        private AudioSource CreateSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = 0;
            source.volume = 0f;
            return source;
        }

        private void Update()
        {
            var settings = GameAudioSettings.Load();
            if (settings == null) return;

            music[0].outputAudioMixerGroup = music[1].outputAudioMixerGroup = GameAudio.Group(AudioBus.Music);
            bed.outputAudioMixerGroup = wind.outputAudioMixerGroup = GameAudio.Group(AudioBus.Ambience);

            AIDirector director = AIDirector.Instance;
            bool gameplay = director != null && !settings.IsMenuScene(SceneManager.GetActiveScene().name);
            if (gameplay && Time.unscaledTime >= nextTankScan)
            {
                nextTankScan = Time.unscaledTime + 0.5f;
                tankAlive = AnyTankAlive();
            }

            MusicCue want = gameplay ? AudioRules.CueFor(director.AdaptivePhase, tankAlive) : MusicCue.None;
            if (want != wanted) { wanted = want; wantedSince = Time.unscaledTime; }
            WantedCue = wanted;
            MusicCue next = AudioRules.Settle(playing, wanted, Time.unscaledTime - wantedSince, settings.stepDownHoldSeconds);
            if (next != playing) SwitchTo(next, settings);

            float step = Time.unscaledDeltaTime / Mathf.Max(0.1f, settings.crossfadeSeconds);
            for (int i = 0; i < music.Length; i++)
            {
                AudioSource source = music[i];
                source.volume = Mathf.MoveTowards(source.volume, musicTarget[i], step);
                if (source.isPlaying && musicTarget[i] <= 0f && source.volume <= 0.0001f) source.Stop();
            }

            UpdateAmbience(settings, gameplay);
        }

        private void SwitchTo(MusicCue cue, GameAudioSettings settings)
        {
            AudioSource old = music[active];
            if (playing != MusicCue.None && playing != MusicCue.Tank && old.isPlaying && old.clip != null)
                resumeAt[playing] = old.time;
            musicTarget[active] = 0f;
            playing = cue;
            CurrentCue = cue;
            if (cue == MusicCue.None) return;

            GameAudioSettings.MusicTrack track = settings.Track(cue);
            if (track.clip == null) return;
            active = 1 - active;
            AudioSource source = music[active];
            source.Stop();
            source.clip = track.clip;
            source.volume = 0f;
            source.time = resumeAt.TryGetValue(cue, out float time) && time < track.clip.length - 1f ? time : 0f;
            source.Play();
            musicTarget[active] = track.volume;
        }

        private void UpdateAmbience(GameAudioSettings settings, bool gameplay)
        {
            float target = gameplay ? 1f : 0f;
            ambienceLevel = Mathf.MoveTowards(ambienceLevel, target,
                Time.unscaledDeltaTime / Mathf.Max(0.1f, settings.ambienceFadeSeconds));
            Drive(bed, settings.ambienceBed, settings.ambienceBedVolume * ambienceLevel, gameplay);
            Drive(wind, settings.ambienceWind, settings.ambienceWindVolume * ambienceLevel, gameplay);
            AmbiencePlaying = bed.isPlaying || wind.isPlaying;
        }

        private static void Drive(AudioSource source, AudioClip clip, float volume, bool wanted)
        {
            if (clip == null) return;
            if (wanted && !source.isPlaying)
            {
                source.clip = clip;
                source.time = Random.Range(0f, Mathf.Max(0f, clip.length - 1f));
                source.Play();
            }
            source.volume = volume;
            if (!wanted && volume <= 0.0001f && source.isPlaying) source.Stop();
        }

        private static bool AnyTankAlive()
        {
            foreach (var tank in FindObjectsByType<SI_Tank>(FindObjectsSortMode.None))
                if (tank != null && tank.isActiveAndEnabled && !tank.PresentsDead) return true;
            return false;
        }
    }
}
