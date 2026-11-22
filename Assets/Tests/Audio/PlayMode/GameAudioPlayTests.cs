using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    /// <summary>The voice pool and the footstep pacing, without networking.</summary>
    public sealed class GameAudioPlayTests
    {
        GameObject listener, ground, walker;
        SoundEvent sound;
        PlayerVoiceSet voice;

        static AudioClip Tone(string name, float seconds, float hz)
        {
            int rate = 44100, n = Mathf.CeilToInt(seconds * rate);
            var clip = AudioClip.Create(name, n, 1, rate, false);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = 0.2f * Mathf.Sin(2 * Mathf.PI * hz * i / rate);
            clip.SetData(data, 0);
            return clip;
        }

        [SetUp]
        public void SetUp()
        {
            listener = new GameObject("Listener", typeof(AudioListener));
            sound = ScriptableObject.CreateInstance<SoundEvent>();
            sound.name = "Test_Tone";
            sound.clips = new[] { Tone("near_a", 2f, 440f), Tone("near_b", 2f, 550f), Tone("near_c", 2f, 660f) };
            sound.distantClips = new[] { Tone("far_a", 2f, 220f) };
            sound.distantFrom = 30f;
            sound.minDistance = 2f;
            sound.maxDistance = 80f;
            sound.maxVoices = 2;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { listener, ground, walker }) if (go != null) Object.Destroy(go);
            Object.Destroy(sound);
            if (voice != null) Object.Destroy(voice);
        }

        static AudioSource SourcePlaying(AudioClip clip)
            => Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).FirstOrDefault(s => s.isPlaying && s.clip == clip);

        [UnityTest]
        public IEnumerator HurtIgnoresInitialHealthHealingCooldownAndLethalDamage()
        {
            walker = new GameObject("Hurt test");
            var audio = walker.AddComponent<PlayerAudio>();
            voice = ScriptableObject.CreateInstance<PlayerVoiceSet>();
            voice.hurt = sound;
            audio.Configure(voice, null, null, null);
            var changed = typeof(PlayerAudio).GetMethod("OnHealthChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            void Health(float value) => changed.Invoke(audio, new object[] { value, 100f });
            int before = GameAudio.CountOf(sound.name);
            Health(100f);
            Health(110f);
            Health(109f);
            Assert.That(GameAudio.CountOf(sound.name), Is.EqualTo(before), "Spawn, healing and tiny damage are silent");
            Health(99f);
            Assert.That(GameAudio.CountOf(sound.name), Is.EqualTo(before + 1));
            Health(89f);
            Assert.That(GameAudio.CountOf(sound.name), Is.EqualTo(before + 1), "Per-player cooldown");
            yield return new WaitForSeconds(0.85f);
            Health(79f);
            Assert.That(GameAudio.CountOf(sound.name), Is.EqualTo(before + 2));
            yield return new WaitForSeconds(0.85f);
            Health(0f);
            Assert.That(GameAudio.CountOf(sound.name), Is.EqualTo(before + 2), "Lethal damage leaves room for Downed/Death");
        }

        [UnityTest]
        public IEnumerator PoolLimitsVoicesPicksDistantTakesAndCullsBeyondRange()
        {
            yield return null; // listener registered
            int before = GameAudio.CountOf(sound.name);
            var first = GameAudio.Play(sound, new Vector3(0, 0, 5));
            var second = GameAudio.Play(sound, new Vector3(0, 0, 5));
            yield return null;
            Assert.That(GameAudio.IsPlaying(first) && GameAudio.IsPlaying(second));
            var third = GameAudio.Play(sound, new Vector3(0, 0, 5));
            Assert.That(GameAudio.IsPlaying(third), Is.True);
            // The oldest voice is stolen at the limit: it fades out over a few frames instead of clicking off.
            float until = Time.realtimeSinceStartup + 0.3f;
            while (GameAudio.IsPlaying(first) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(GameAudio.IsPlaying(first), Is.False, "The oldest voice of an event is stolen at its limit");
            Assert.That(GameAudio.IsPlaying(second) && GameAudio.IsPlaying(third), Is.True, "Only the oldest voice is stolen");
            Assert.That(GameAudio.CountOf(sound.name), Is.EqualTo(before + 3));

            var far = GameAudio.Play(sound, new Vector3(0, 0, 50));
            yield return null;
            Assert.That(far.IsValid);
            Assert.That(SourcePlaying(sound.distantClips[0]), Is.Not.Null, "A listener 50 m away hears the distant take");
            var source = SourcePlaying(sound.distantClips[0]);
            Assert.That(source.spatialBlend, Is.EqualTo(1f));
            Assert.That(source.outputAudioMixerGroup, Is.Not.Null.And.Property("name").EqualTo("World"), "Routed through the mixer");

            Assert.That(GameAudio.Play(sound, new Vector3(0, 0, 500)).IsValid, Is.False, "Beyond max distance nothing plays");
            var local = GameAudio.PlayLocal(sound);
            yield return null;
            Assert.That(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                .Any(s => s.isPlaying && sound.clips.Contains(s.clip) && s.spatialBlend == 0f), "The owner's own sound is 2D");
            GameAudio.Stop(local, 0f);
            GameAudio.Stop(second, 0f);
            GameAudio.Stop(third, 0f);
            GameAudio.Stop(far, 0f);
        }

        [UnityTest]
        public IEnumerator FootstepsFollowStrideAndALandingFollowsAFall()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube); // test fixture only
            ground.transform.localScale = new Vector3(200f, 1f, 200f);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            walker = new GameObject("Walker");
            var audio = walker.AddComponent<PlayerAudio>();
            audio.Configure(null, sound, sound, null);
            sound.maxVoices = 8;
            yield return null;

            // Walk 3 s at 4 m/s: ~12 m with ~1.9 m strides.
            float t = 0f;
            while (t < 3f)
            {
                yield return null;
                t += Time.deltaTime;
                walker.transform.position += Vector3.forward * 4f * Time.deltaTime;
            }
            Debug.Log($"[GameAudioPlayTests] steps={audio.StepsPlayed} over {walker.transform.position.z:0.0} m");
            Assert.That(audio.StepsPlayed, Is.InRange(4, 8));

            int steps = audio.StepsPlayed;
            walker.transform.position += Vector3.up * 3f; // airborne
            float air = 0f;
            while (air < 0.6f) { yield return null; air += Time.deltaTime; }
            Assert.That(audio.StepsPlayed, Is.EqualTo(steps), "No steps in the air");
            walker.transform.position -= Vector3.up * 3f;
            yield return null;
            yield return null;
            Assert.That(audio.LandingsPlayed, Is.EqualTo(1));
        }
    }
}
