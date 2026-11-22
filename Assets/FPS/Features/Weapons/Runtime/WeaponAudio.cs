using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// One player's weapon sounds on every peer. The owner hears its own gun in 2D; everyone else hears it
    /// in 3D from the shooter (distant takes far away). Foley cues follow the weapon animations: equip cues from
    /// the equip start, fire cues (pump, bolt) after each shot, and reload cues on the replicated reload timeline,
    /// so a magazine goes in when the server commits the ammo and shells click in one by one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponAudio : MonoBehaviour
    {
        private enum CueKind { Equip, Fire, Reload }

        private struct Cue
        {
            public float at;
            public SoundEvent sound;
            public AudioClip clip;
            public float volume;
            public CueKind kind;
        }

        private const float FoleyMinDistance = 1.5f;
        private const float FoleyMaxDistance = 25f;

        private readonly List<Cue> cues = new();
        private NetworkObject network;
        private WeaponReloadTimeline scheduledReload;
        private WeaponData scheduledData;
        private bool reloadScheduled;
        private float nextDryFire;

        public int FiredCount { get; private set; }
        public int ReloadCuesPlayed { get; private set; }
        public int ReloadsScheduled { get; private set; }
        public int EquipCuesPlayed { get; private set; }

        private void Awake() => network = GetComponent<NetworkObject>();
        private void OnDisable() { cues.Clear(); reloadScheduled = false; }

        private bool IsLocal => network == null || !network.IsSpawned || network.IsOwner;
        private Vector3 Muzzle => transform.position + Vector3.up * 1.45f;

        public void Fire(WeaponData data)
        {
            if (data == null || !GameAudio.CanPlay) return;
            FiredCount++;
            if (data.fireSound != null) Emit(data.fireSound);
            else if (data.shootSound != null)
                GameAudio.PlayClip(data.shootSound, AudioBus.Weapons, Muzzle, 1f, 6f, 90f, transform, IsLocal);
            if (data.fireCues == null || data.fireCues.Length == 0) return;
            cues.RemoveAll(cue => cue.kind == CueKind.Fire);
            foreach (var cue in data.fireCues) Schedule(cue, 1f, 0f, CueKind.Fire);
        }

        /// <summary>A weapon is drawn: earlier foley stops and the equip cues play from elapsedSeconds into the clip.</summary>
        public void Equip(WeaponData data, float elapsedSeconds)
        {
            cues.RemoveAll(cue => cue.kind != CueKind.Reload);
            if (data == null || data.equipCues == null || !GameAudio.CanPlay) return;
            foreach (var cue in data.equipCues) Schedule(cue, 1f, Mathf.Max(0f, elapsedSeconds), CueKind.Equip);
        }

        public void DryFire(WeaponData data)
        {
            if (data == null || data.dryFireSound == null || !IsLocal || Time.time < nextDryFire) return;
            nextDryFire = Time.time + 0.3f;
            GameAudio.PlayLocal(data.dryFireSound);
        }

        /// <summary>
        /// Feed the replicated reload state whenever it arrives; each reload is scheduled once, cues already
        /// in the past (late join, packet delay) are skipped, and an ended reload drops what is left.
        /// </summary>
        public void SyncReload(WeaponData data, bool reloading, WeaponReloadTimeline timeline, double serverNow)
        {
            if (!reloading || data == null || !timeline.IsValid)
            {
                if (!reloadScheduled) return;
                // A reload that ran its course keeps its last cues (a client sees the end a little early);
                // one that ended before its deadline was interrupted, so its remaining cues are dropped.
                bool finished = scheduledData != null
                    && serverNow + 0.5 >= scheduledReload.GameplayCompleteTime(scheduledData);
                if (finished) reloadScheduled = false;
                else CancelReload();
                return;
            }
            if (reloadScheduled && scheduledReload.Equals(timeline)) return;
            CancelReload();
            reloadScheduled = true;
            scheduledReload = timeline;
            scheduledData = data;
            ReloadsScheduled++;

            float elapsed = (float)(serverNow - timeline.startedAt);
            float multiplier = timeline.timingMultiplier;
            if (data.reloadMode != ReloadMode.PerShell && data.reloadCues != null && data.reloadCues.Length > 0)
            {
                // The reload clip plays at its own speed (stretched by the multiplier), so cue times scale with it.
                foreach (var cue in data.reloadCues) Schedule(cue, Mathf.Max(0.01f, multiplier), elapsed, CueKind.Reload);
                return;
            }
            if (data.reloadStartSound == null && data.reloadInsertSound == null && data.reloadEndSound == null)
            {
                if (data.reloadSound != null && elapsed < 0.3f && GameAudio.CanPlay)
                    GameAudio.PlayClip(data.reloadSound, AudioBus.Weapons, Muzzle, 1f, 3f, 25f, transform, IsLocal);
                return;
            }

            if (data.reloadMode == ReloadMode.PerShell)
            {
                At(0.02f * multiplier, data.reloadStartSound, elapsed);
                for (int k = 0; k < timeline.rounds; k++)
                    At(AudioRules.ShellInsertCue(data.PerShellOpeningDuration, data.PerShellInterval, k, multiplier),
                        data.reloadInsertSound, elapsed);
                At(AudioRules.ShellReloadEndCue(data.PerShellOpeningDuration, data.PerShellInterval,
                    data.PerShellClosingDuration, timeline.rounds, multiplier), data.reloadEndSound, elapsed);
            }
            else
            {
                AudioRules.MagazineReloadCues(data.ReloadAmmoCommitDuration, data.ReloadDuration, multiplier,
                    out float start, out float insert, out float end);
                At(start, data.reloadStartSound, elapsed);
                At(insert, data.reloadInsertSound, elapsed);
                At(end, data.reloadEndSound, elapsed);
            }
        }

        private void At(float offset, SoundEvent sound, float elapsed)
        {
            if (sound == null || offset < elapsed - 0.1f) return;
            cues.Add(new Cue { at = Time.time + Mathf.Max(0f, offset - elapsed), sound = sound, kind = CueKind.Reload });
        }

        private void Schedule(WeaponSoundCue cue, float timeScale, float elapsed, CueKind kind)
        {
            float offset = cue.time * timeScale;
            if (cue.clip == null || cue.volume <= 0f || offset < elapsed - 0.1f) return;
            cues.Add(new Cue
            {
                at = Time.time + Mathf.Max(0f, offset - elapsed), clip = cue.clip, volume = cue.volume, kind = kind
            });
        }

        public void CancelReload()
        {
            cues.RemoveAll(cue => cue.kind == CueKind.Reload);
            reloadScheduled = false;
        }

        private void Update()
        {
            if (cues.Count == 0) return;
            float now = Time.time;
            for (int i = cues.Count - 1; i >= 0; i--)
            {
                if (cues[i].at > now) continue;
                Cue cue = cues[i];
                cues.RemoveAt(i);
                if (cue.sound != null) Emit(cue.sound);
                else GameAudio.PlayClip(cue.clip, AudioBus.Weapons, Muzzle, cue.volume, FoleyMinDistance, FoleyMaxDistance,
                    transform, IsLocal);
                if (cue.kind == CueKind.Reload) ReloadCuesPlayed++;
                else if (cue.kind == CueKind.Equip) EquipCuesPlayed++;
            }
        }

        private void Emit(SoundEvent sound)
        {
            if (IsLocal) GameAudio.PlayLocal(sound);
            else GameAudio.Play(sound, Muzzle, 1f, transform);
        }
    }
}
