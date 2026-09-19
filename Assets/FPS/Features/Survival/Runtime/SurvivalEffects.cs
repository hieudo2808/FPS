using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    /// <summary>Scene-local pools: effects outlive the projectile and are reused, including audio sources.</summary>
    public sealed class SurvivalEffects : MonoBehaviour
    {
        private sealed class Entry
        {
            public GameObject source, instance;
            public ParticleSystem[] particles;
            public AudioSource audio;
            public double deadline;
            public bool active;
        }
        private readonly List<Entry> entries = new();
        private static SurvivalEffects instance;
        public static event System.Action<Vector3> Blast;
        private static SurvivalEffects Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("Survival Effects Pool").AddComponent<SurvivalEffects>();
                return instance;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; Blast = null; }
        private void OnDestroy() { if (instance == this) instance = null; }
        private Entry Rent(GameObject source, Vector3 point, Quaternion rotation, float seconds, AudioClip clip = null, float volume = .6f)
        {
            Entry entry = null;
            foreach (var candidate in entries)
                if (!candidate.active && candidate.source == source) { entry = candidate; break; }
            if (entry == null)
            {
                GameObject go = source != null ? Instantiate(source, transform) : new GameObject("Survival Audio");
                go.transform.SetParent(transform, false);
                entry = new Entry { source = source, instance = go, particles = go.GetComponentsInChildren<ParticleSystem>(true) };
                entry.audio = go.GetComponent<AudioSource>() ?? go.AddComponent<AudioSource>();
                entry.audio.playOnAwake = false;
                entry.audio.spatialBlend = 1f;
                entry.audio.minDistance = 2f;
                entry.audio.maxDistance = 35f;
                entry.audio.rolloffMode = AudioRolloffMode.Linear;
                entries.Add(entry);
            }
            entry.instance.transform.SetPositionAndRotation(point, rotation);
            entry.deadline = Time.timeAsDouble + seconds;
            entry.active = true;
            entry.instance.SetActive(true);
            foreach (var particle in entry.particles) { particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); particle.Play(true); }
            entry.audio.Stop();
            entry.audio.clip = clip;
            entry.audio.volume = volume;
            entry.audio.loop = float.IsPositiveInfinity(seconds);
            if (clip != null) entry.audio.Play();
            return entry;
        }
        private void Release(Entry entry)
        {
            if (!entry.active) return;
            entry.audio.Stop();
            foreach (var particle in entry.particles) particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            entry.instance.SetActive(false);
            entry.active = false;
        }
        private void Update()
        {
            foreach (var entry in entries)
                if (entry.active && Time.timeAsDouble >= entry.deadline) Release(entry);
        }
        public static void Return(GameObject visual)
        {
            if (instance == null || visual == null) return;
            foreach (var entry in instance.entries)
                if (entry.instance == visual) { instance.Release(entry); return; }
        }
        public static void PlayOneShot(AudioClip clip, Vector3 point, float volume = .6f)
        { if (clip != null) Instance.Rent(null, point, Quaternion.identity, clip.length + .1f, clip, volume); }
        public static GameObject BeginFire(Vector3 point)
        {
            var catalog = SurvivalCatalog.Load();
            return catalog != null ? Instance.Rent(catalog.fireEffect, point, Quaternion.identity, float.PositiveInfinity, catalog.fireClip, .35f).instance : null;
        }
        public static void Explosion(Vector3 point, Vector3 normal, ThrowableKind kind)
        {
            var catalog = SurvivalCatalog.Load();
            if (catalog == null) return;
            if (kind == ThrowableKind.Frag)
                Instance.Rent(catalog.fragEffect, point, Quaternion.identity, 3f, catalog.explosionClip, .8f);
            else PlayOneShot(catalog.explosionClip, point, .35f);
            if (catalog.scorchEffect != null && Physics.Raycast(point, -normal, out var hit, .5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                Instance.Rent(catalog.scorchEffect, hit.point + hit.normal * .015f, Quaternion.LookRotation(hit.normal), 12f);
            Blast?.Invoke(point);
        }
        public static void Medical(Vector3 point, ConsumableKind kind)
        {
            var catalog = SurvivalCatalog.Load();
            if (catalog != null) Instance.Rent(catalog.medicalEffect, point, Quaternion.identity, 1.5f, catalog.completionClip, .25f);
        }
    }
}
