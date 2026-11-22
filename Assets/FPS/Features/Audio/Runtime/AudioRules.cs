using UnityEngine;

namespace FPS
{
    /// <summary>The music state the game is in; ordered by intensity.</summary>
    public enum MusicCue { None, Calm, Build, Combat, Tank }

    /// <summary>Pure audio decisions, kept free of Unity objects so they can be tested directly.</summary>
    public static class AudioRules
    {
        /// <summary>Random take that never repeats the previous one when there is a choice.</summary>
        public static int PickIndex(int count, int previous, float random01)
        {
            if (count <= 0) return -1;
            if (count == 1) return 0;
            if (previous < 0 || previous >= count)
                return Mathf.Min(count - 1, Mathf.FloorToInt(Mathf.Clamp01(random01) * count));
            int pick = Mathf.Min(count - 2, Mathf.FloorToInt(Mathf.Clamp01(random01) * (count - 1)));
            return previous >= 0 && pick >= previous ? pick + 1 : pick;
        }

        public static float JitterDb(float volume, float jitterDb, float random11)
            => volume * Mathf.Pow(10f, Mathf.Clamp(random11, -1f, 1f) * jitterDb / 20f);

        /// <summary>Director pacing to music: quiet build-up keeps the calm bed, combat and peak share the fight track.</summary>
        public static MusicCue CueFor(DirectorPhase phase, bool tankAlive)
        {
            if (tankAlive) return MusicCue.Tank;
            switch (phase)
            {
                case DirectorPhase.BuildUp: return MusicCue.Build;
                case DirectorPhase.Combat:
                case DirectorPhase.Peak: return MusicCue.Combat;
                default: return MusicCue.Calm;
            }
        }

        /// <summary>
        /// Rising intensity switches at once; falling intensity only after the lower cue has been asked for
        /// continuously for holdSeconds, so the music does not flap when the director hovers at a boundary.
        /// </summary>
        public static MusicCue Settle(MusicCue playing, MusicCue wanted, float wantedForSeconds, float holdSeconds)
        {
            if (wanted == playing) return playing;
            if (playing == MusicCue.None || wanted == MusicCue.None || wanted > playing) return wanted;
            return wantedForSeconds >= holdSeconds ? wanted : playing;
        }

        /// <summary>Metres between footsteps: short strides when walking, long when sprinting.</summary>
        public static float StrideLength(float speed)
            => Mathf.Lerp(1.5f, 2.4f, Mathf.InverseLerp(2f, 7f, speed));

        public static float FootstepVolume(float speed)
            => Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(1f, 6f, speed));

        /// <summary>0..1 settings slider to mixer decibels (squared so the whole travel is audible).</summary>
        public static float SliderToDb(float slider01)
        {
            float gain = Mathf.Clamp01(slider01);
            return gain <= 0.0001f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(gain * gain));
        }

        /// <summary>
        /// Seconds after reload start for each sound cue of a magazine reload. The magazine goes in just before
        /// the server commits the ammo, but never later than 70% of the reload: some weapons commit at the very end.
        /// </summary>
        public static void MagazineReloadCues(float commitSeconds, float durationSeconds, float multiplier,
            out float start, out float insert, out float end)
        {
            multiplier = Mathf.Max(1f, multiplier);
            float magazineIn = Mathf.Max(0.25f, Mathf.Min(commitSeconds - 0.12f, durationSeconds * 0.7f));
            start = 0.05f * multiplier;
            insert = magazineIn * multiplier;
            end = Mathf.Max(magazineIn + 0.15f, durationSeconds - 0.35f) * multiplier;
        }

        /// <summary>Seconds after reload start at which the k-th shell (0-based) is pushed in.</summary>
        public static float ShellInsertCue(float opening, float interval, int k, float multiplier)
            => (opening + k * interval + interval * 0.55f) * Mathf.Max(1f, multiplier);

        public static float ShellReloadEndCue(float opening, float interval, float closing, int rounds, float multiplier)
            => (opening + Mathf.Max(0, rounds) * interval + closing * 0.35f) * Mathf.Max(1f, multiplier);
    }
}
