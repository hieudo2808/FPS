using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace FPS.Tests
{
    /// <summary>The audio wiring produced by Tools/FPS/Audio/Build All is complete.</summary>
    public sealed class AudioContentTests
    {
        const string Root = "Assets/FPS/Features/Audio/Content";
        static readonly string[] Weapons = { "Classic", "Vandal", "Odin", "Warden", "Operator", "Bucky" };

        [Test]
        public void MixerHasEveryBusAndTheMusicAndSfxSliders()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(Root + "/SFX/Sound.mixer");
            Assert.That(mixer, Is.Not.Null);
            var names = mixer.FindMatchingGroups(string.Empty).Select(g => g.name).ToList();
            foreach (AudioBus bus in System.Enum.GetValues(typeof(AudioBus)))
                Assert.That(names, Does.Contain(bus.ToString()), $"mixer group {bus}");
            Assert.That(mixer.FindMatchingGroups("Master/Music").Length, Is.EqualTo(1), "Music sits under Master, beside SFX");
            Assert.That(mixer.FindMatchingGroups("Master/SFX/Weapons").Length, Is.EqualTo(1), "Weapons follow the SFX slider");
            var exposed = (System.Array)mixer.GetType().GetProperty("exposedParameters").GetValue(mixer);
            var parameters = exposed.Cast<object>().Select(p => (string)p.GetType().GetField("name").GetValue(p)).ToList();
            Assert.That(parameters, Does.Contain("music").And.Contain("sfx"));
        }

        [Test]
        public void SettingsResourceDrivesFourMusicCuesAndAmbience()
        {
            var settings = Resources.Load<GameAudioSettings>(GameAudioSettings.ResourceName);
            Assert.That(settings, Is.Not.Null, "Resources/GameAudioSettings");
            Assert.That(settings.mixer, Is.Not.Null);
            foreach (MusicCue cue in new[] { MusicCue.Calm, MusicCue.Build, MusicCue.Combat, MusicCue.Tank })
            {
                var track = settings.Track(cue);
                Assert.That(track.clip, Is.Not.Null, cue.ToString());
                Assert.That(track.volume, Is.InRange(0.05f, 1f), cue.ToString());
                Assert.That(ImporterOf(track.clip).defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming), $"{cue} streams");
            }
            Assert.That(settings.ambienceBed, Is.Not.Null);
            Assert.That(settings.ambienceWind, Is.Not.Null);
            Assert.That(settings.IsMenuScene("MainMenu") && settings.IsMenuScene("LobbyScene") && !settings.IsMenuScene("GameScene"));
        }

        [Test]
        public void EverySixWeaponHasShotsAndFoleyOnItsAnimations()
        {
            foreach (string name in Weapons)
            {
                var data = AssetDatabase.LoadAssetAtPath<WeaponData>($"Assets/FPS/Features/Weapons/Content/{name}/{name}.asset");
                Assert.That(data, Is.Not.Null, name);
                Assert.That(data.fireSound, Is.Not.Null, name);
                Assert.That(data.fireSound.clips.Length, Is.GreaterThanOrEqualTo(1), $"{name}: near takes");
                Assert.That(data.fireSound.distantClips.Length, Is.GreaterThanOrEqualTo(1), $"{name}: distant takes");
                Assert.That(data.fireSound.bus, Is.EqualTo(AudioBus.Weapons));
                Assert.That(data.dryFireSound, Is.Not.Null, $"{name}: dry fire");
                Assert.That(data.fireSound.clips.Concat(data.fireSound.distantClips).All(c => c != null && c.length > 0.2f), name);
                Assert.That(data.equipCues.Length, Is.GreaterThanOrEqualTo(3), $"{name}: equip foley");
                Assert.That(data.equipCues.Concat(data.fireCues).Concat(data.reloadCues).All(c => c.clip != null && c.volume > 0f), name);
                Assert.That(data.equipCues.All(c => c.time < data.EquipAnimationDuration), $"{name}: equip cues inside the equip clip");
                if (data.reloadMode == ReloadMode.PerShell)
                    Assert.That(data.reloadInsertSound != null && data.reloadInsertSound.HasClips, $"{name}: shell in");
                else
                {
                    Assert.That(data.reloadCues.Length, Is.GreaterThanOrEqualTo(4), $"{name}: reload foley");
                    Assert.That(data.reloadCues.All(c => c.time < data.ReloadAnimationDuration), $"{name}: reload cues inside the reload clip");
                    // A magazine is seated by the time the server commits the ammo.
                    Assert.That(data.reloadCues.Any(c => c.time <= data.ReloadAmmoCommitDuration + 0.1f), $"{name}: a cue before the ammo commit");
                }
            }
            var bucky = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/FPS/Features/Weapons/Content/Bucky/Bucky.asset");
            Assert.That(bucky.reloadMode, Is.EqualTo(ReloadMode.PerShell));
            Assert.That(bucky.reloadInsertSound.clips.Length, Is.EqualTo(2), "Two shell-in takes alternate");
            Assert.That(bucky.fireCues.Length, Is.EqualTo(2), "Bucky pumps after a shot");
            var op = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/FPS/Features/Weapons/Content/Operator/Operator.asset");
            Assert.That(op.fireCues.Length, Is.EqualTo(4), "Operator works the bolt after a shot");
            var warden = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/FPS/Features/Weapons/Content/Warden/Warden.asset");
            Assert.That(warden.fireSound.remoteClips.Length, Is.EqualTo(6), "Warden has third-person takes for other players");
        }

        [Test]
        public void EnemyPrefabsSpeakAndPlayerPrefabsHaveBodyAudio()
        {
            int zombies = 0, specials = 0;
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/FPS/Features/Characters/Content/Enemies" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var enemy = prefab.GetComponent<EnemyAI>();
                if (enemy == null) continue;
                var voices = prefab.GetComponents<EnemyVoice>();
                Assert.That(voices.Length, Is.EqualTo(1), $"{prefab.name}: exactly one voice");
                Assert.That(voices[0].Hurt, Is.Not.Null, $"{prefab.name}: pain");
                if (enemy is SI_Tank || enemy is SI_Infector || enemy is SI_Screamer) { specials++; continue; }
                Assert.That(voices[0].Idle != null && voices[0].Attack != null && voices[0].Death != null, $"{prefab.name}: full zombie voice");
                zombies++;
            }
            Assert.That(zombies, Is.GreaterThanOrEqualTo(4));
            Assert.That(specials, Is.EqualTo(3));

            int players = 0;
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/FPS/Features/Characters/Content/Players" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponent<PlayerHealth>() == null) continue;
                var audio = prefab.GetComponent<PlayerAudio>();
                Assert.That(audio, Is.Not.Null, prefab.name);
                Assert.That(audio.Voice, Is.Not.Null, prefab.name);
                string character = prefab.name.Replace("Player", "");
                Assert.That(audio.Voice.hurt.clips.Select(c => c.name), Is.EquivalentTo(new[]
                {
                    character + "_HurtLight_01", character + "_HurtMedium_01", character + "_HurtHeavy_01"
                }), prefab.name);
                Assert.That(audio.Voice.downed.HasClips && audio.Voice.death.HasClips, prefab.name);
                Assert.That(audio.Voice.name, Is.EqualTo("Voice_" + character), prefab.name);
                Assert.That(audio.Voice.downed.clips.Single().name, Is.EqualTo(character + "_HurtHeavy_01"), "Downed reuses this character's heavy effort");
                Assert.That(audio.Voice.death.clips.Length, Is.EqualTo(character == "Brimstone" ? 2 : 1));
                Assert.That(audio.Voice.death.clips.Select(c => c.name), Is.All.StartsWith(character + "_Death_"));
                foreach (var sound in new[] { audio.Voice.hurt, audio.Voice.downed, audio.Voice.death })
                {
                    Assert.That(sound.bus, Is.EqualTo(AudioBus.Player));
                    Assert.That(sound.cooldown, Is.Zero, "Cooldown belongs to each player");
                    Assert.That(sound.pitch, Is.EqualTo(Vector2.one), "Preserve the recorded character voice");
                    foreach (var clip in sound.clips)
                    {
                        Assert.That(AssetDatabase.GetAssetPath(clip), Does.StartWith($"{Root}/Player/Voice/{character}/"));
                        Assert.That(clip.channels, Is.EqualTo(1));
                        Assert.That(clip.frequency, Is.EqualTo(48000));
                        Assert.That(ImporterOf(clip).defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
                    }
                }
                if (character == "Sage") Assert.That(audio.Voice.death.clips.Single().length, Is.InRange(0.28f, 0.29f), "The revised Sage death take");
                players++;
            }
            Assert.That(players, Is.EqualTo(4));
        }

        [Test]
        public void EveryEventHasClipsAndEveryClipFolderHasASourceNote()
        {
            var events = AssetDatabase.FindAssets("t:SoundEvent", new[] { Root + "/Events" })
                .Select(g => AssetDatabase.LoadAssetAtPath<SoundEvent>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            Assert.That(events.Count, Is.GreaterThanOrEqualTo(25));
            foreach (var sound in events)
            {
                Assert.That(sound.HasClips, sound.name);
                Assert.That(sound.clips.All(c => c != null), sound.name);
                Assert.That(sound.maxDistance, Is.GreaterThan(sound.minDistance), sound.name);
            }
            foreach (string folder in new[] { "Weapons", "Enemies", "Player", "Music", "Ambience" })
            {
                string note = Path.Combine(Root, folder, "SOURCE.md");
                Assert.That(File.Exists(note), note);
                Assert.That(File.ReadAllText(note), Does.Contain("CC0"), note);
            }
        }

        static AudioImporter ImporterOf(AudioClip clip) => (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
    }
}
