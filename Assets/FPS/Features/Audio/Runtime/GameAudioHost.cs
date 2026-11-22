using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    /// <summary>The pooled voices behind GameAudio; lives across scenes and hosts the music director.</summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(1000)]
    internal sealed class GameAudioHost : MonoBehaviour
    {
        private sealed class Slot
        {
            public AudioSource source;
            public SoundEvent sound;
            public Transform follow;
            public bool following;
            public int id;
            public float started;
            public float fadePerSecond;
        }

        private const int InitialSlots = 16;
        private const int MaxSlots = 48;
        private readonly List<Slot> slots = new();
        private readonly Dictionary<SoundEvent, int> lastTake = new();
        private readonly Dictionary<SoundEvent, float> lastPlay = new();
        private int nextId = 1;
        private AudioListener listener;
        private float nextListenerSearch;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            for (int i = 0; i < InitialSlots; i++) slots.Add(CreateSlot());
            gameObject.AddComponent<GameMusic>();
        }

        private Slot CreateSlot()
        {
            var go = new GameObject("Voice");
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            return new Slot { source = source };
        }

        internal AudioListener Listener
        {
            get
            {
                if ((listener == null || !listener.isActiveAndEnabled) && Time.unscaledTime >= nextListenerSearch)
                {
                    nextListenerSearch = Time.unscaledTime + 0.5f;
                    listener = null;
                    foreach (var candidate in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                        if (candidate.isActiveAndEnabled) { listener = candidate; break; }
                }
                return listener != null && listener.isActiveAndEnabled ? listener : null;
            }
        }

        internal AudioVoice Play(SoundEvent sound, AudioClip clip, AudioBus bus, Vector3 position, Transform follow,
            bool local, float volumeScale, float minDistance, float maxDistance)
        {
            if (follow != null) position = follow.position;
            float now = Time.unscaledTime;
            bool spatial = !local && (sound == null || sound.spatialBlend > 0f);
            float distance = 0f;
            if (spatial && Listener != null) distance = Vector3.Distance(position, Listener.transform.position);

            float volume = Mathf.Max(0f, volumeScale);
            float pitch = 1f;
            bool loop = false;
            int priority = 128;
            if (sound != null)
            {
                if (!sound.loop && spatial && distance > sound.maxDistance) return default;
                if (sound.cooldown > 0f && lastPlay.TryGetValue(sound, out float last) && now - last < sound.cooldown)
                    return default;
                AudioClip[] takes = !local && sound.distantClips != null && sound.distantClips.Length > 0
                    && distance >= sound.distantFrom ? sound.distantClips
                    : !local && sound.remoteClips != null && sound.remoteClips.Length > 0 ? sound.remoteClips
                    : sound.clips;
                if (takes == null || takes.Length == 0) return default;
                if (!lastTake.TryGetValue(sound, out int previous)) previous = -1;
                int index = AudioRules.PickIndex(takes.Length, takes == sound.clips ? previous : -1, Random.value);
                if (takes == sound.clips) lastTake[sound] = index;
                clip = takes[index];
                if (clip == null) return default;
                volume = AudioRules.JitterDb(sound.volume * volume, sound.volumeJitterDb, Random.Range(-1f, 1f));
                pitch = Random.Range(Mathf.Min(sound.pitch.x, sound.pitch.y), Mathf.Max(sound.pitch.x, sound.pitch.y));
                loop = sound.loop;
                priority = sound.priority;
                minDistance = sound.minDistance;
                maxDistance = sound.maxDistance;
                LimitVoices(sound);
                lastPlay[sound] = now;
            }
            else if (spatial && distance > maxDistance * 1.25f)
            {
                return default;
            }

            int slotIndex = FreeSlot();
            Slot slot = slots[slotIndex];
            AudioSource source = slot.source;
            source.Stop();
            source.clip = clip;
            source.outputAudioMixerGroup = GameAudio.Group(bus);
            source.volume = Mathf.Clamp01(volume);
            source.pitch = pitch;
            source.loop = loop;
            source.priority = priority;
            source.spatialBlend = local ? 0f : (sound != null ? sound.spatialBlend : 1f);
            source.minDistance = Mathf.Max(0.01f, minDistance);
            source.maxDistance = Mathf.Max(source.minDistance + 0.01f, maxDistance);
            source.transform.position = position;
            slot.sound = sound;
            slot.follow = follow;
            slot.following = follow != null;
            slot.id = nextId++;
            if (nextId == int.MaxValue) nextId = 1;
            slot.started = now;
            slot.fadePerSecond = 0f;
            source.Play();
            GameAudio.Report(sound, clip, bus, position, local);
            GameAudio.ReportStart(new AudioStartInfo(slot.id, sound, clip, bus, position, local, source.volume, pitch,
                source.spatialBlend, source.minDistance, source.maxDistance, loop));
            return new AudioVoice(slotIndex, slot.id);
        }

        // A stolen voice fades out this fast instead of stopping dead, so long gunshot tails do not click.
        private const float StealFadeSeconds = 0.06f;

        private void LimitVoices(SoundEvent sound)
        {
            int count = 0;
            Slot oldest = null;
            foreach (var slot in slots)
            {
                if (slot.id == 0 || slot.sound != sound || slot.fadePerSecond > 0f) continue;
                count++;
                if (oldest == null || slot.started < oldest.started) oldest = slot;
            }
            if (count >= Mathf.Max(1, sound.maxVoices) && oldest != null)
                oldest.fadePerSecond = Mathf.Max(oldest.source.volume, 0.0001f) / StealFadeSeconds;
        }

        private int FreeSlot()
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].id == 0) return i;
            if (slots.Count < MaxSlots)
            {
                slots.Add(CreateSlot());
                return slots.Count - 1;
            }
            int steal = -1;
            for (int i = 0; i < slots.Count; i++)
                if (!slots[i].source.loop && (steal < 0 || slots[i].started < slots[steal].started)) steal = i;
            if (steal < 0) steal = 0;
            Release(slots[steal]);
            return steal;
        }

        internal void Stop(AudioVoice voice, float fadeSeconds)
        {
            if (voice.Slot < 0 || voice.Slot >= slots.Count) return;
            Slot slot = slots[voice.Slot];
            if (slot.id != voice.Id) return;
            if (fadeSeconds <= 0f) Release(slot);
            else slot.fadePerSecond = Mathf.Max(slot.source.volume, 0.0001f) / fadeSeconds;
        }

        internal bool IsPlaying(AudioVoice voice)
            => voice.Slot >= 0 && voice.Slot < slots.Count && slots[voice.Slot].id == voice.Id;

        private static void Release(Slot slot)
        {
            if (slot.id != 0) GameAudio.ReportEnd(slot.id);
            slot.source.Stop();
            slot.source.clip = null;
            slot.source.loop = false;
            slot.sound = null;
            slot.follow = null;
            slot.following = false;
            slot.id = 0;
            slot.fadePerSecond = 0f;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var slot in slots)
            {
                if (slot.id == 0) continue;
                AudioSource source = slot.source;
                if (slot.following)
                {
                    if (slot.follow != null) source.transform.position = slot.follow.position;
                    else if (source.loop) { Release(slot); continue; }
                    else slot.following = false;
                }
                if (slot.fadePerSecond > 0f)
                {
                    source.volume -= slot.fadePerSecond * dt;
                    if (source.volume <= 0.0001f) { Release(slot); continue; }
                }
                if (!source.isPlaying && !source.loop && AudioSettingsAllowRelease(source)) Release(slot);
            }
        }

        // A voice that has not started yet (clip still loading) is not finished.
        private static bool AudioSettingsAllowRelease(AudioSource source)
            => source.clip == null || source.clip.loadState != AudioDataLoadState.Loading;
    }
}
