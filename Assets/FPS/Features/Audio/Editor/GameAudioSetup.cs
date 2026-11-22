using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace FPS.EditorTools
{
    /// <summary>
    /// Builds the game's audio wiring from the clips under Audio/Content: mixer buses, import settings,
    /// SoundEvent assets, the GameAudioSettings resource, and the weapon / enemy / player references.
    /// Idempotent: run it again after adding or renaming clips.
    /// </summary>
    public static class GameAudioSetup
    {
        public const string Root = "Assets/FPS/Features/Audio/Content";
        public const string EventsFolder = Root + "/Events";
        public const string MixerPath = Root + "/SFX/Sound.mixer";
        public const string SettingsPath = Root + "/Resources/GameAudioSettings.asset";
        private const string WeaponsContent = "Assets/FPS/Features/Weapons/Content";
        private const string EnemiesContent = "Assets/FPS/Features/Characters/Content/Enemies";
        private const string PlayersContent = "Assets/FPS/Features/Characters/Content/Players";

        public static readonly string[] SfxBuses = { "Ambience", "Weapons", "Enemies", "Player", "World", "UI" };
        private static readonly string[] MaleCharacters = { "Brimstone", "Gekko" };
        private static readonly string[] PlayerCharacters = { "Brimstone", "Clove", "Gekko", "Sage" };

        [MenuItem("Tools/FPS/Audio/Build Player Voices")]
        public static void BuildPlayerVoices()
        {
            AssetDatabase.SaveAssets();
            string imports = ApplyImportSettings(Root + "/Player/Voice");
            string players = AssignPlayers(BuildEvents(playersOnly: true));
            AssetDatabase.SaveAssets();
            Debug.Log($"[GameAudioSetup] player voices: {imports} {players}");
        }

        [MenuItem("Tools/FPS/Audio/Build All")]
        public static void BuildAllMenu() => Debug.Log(BuildAll());

        public static string BuildAll()
        {
            var log = new StringBuilder("[GameAudioSetup]");
            log.Append(' ').Append(ApplyImportSettings());
            log.Append(' ').Append(BuildMixer());
            AssetDatabase.SaveAssets();
            var events = BuildEvents();
            log.Append($" events={events.Count}");
            log.Append(' ').Append(BuildSettings());
            log.Append(' ').Append(AssignWeapons(events));
            log.Append(' ').Append(AssignEnemies(events));
            log.Append(' ').Append(AssignPlayers(events));
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ---------------------------------------------------------------- import settings

        public static string ApplyImportSettings(string folder = Root)
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/SFX/")) continue; // legacy clips keep their settings
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;
                bool streamed = path.Contains("/Music/") || path.Contains("/Ambience/");
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                var wanted = settings;
                wanted.loadType = streamed ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                wanted.compressionFormat = AudioCompressionFormat.Vorbis;
                wanted.quality = streamed ? 0.6f : 0.7f;
                wanted.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                bool mono = !streamed && !Regex.IsMatch(path, @"_fire_\d+\.wav$");
                if (Same(settings, wanted) && importer.forceToMono == mono && importer.loadInBackground == streamed)
                    continue;
                importer.defaultSampleSettings = wanted;
                importer.forceToMono = mono;
                importer.loadInBackground = streamed;
                importer.SaveAndReimport();
                changed++;
            }
            return $"imports={changed}";
        }

        private static bool Same(AudioImporterSampleSettings a, AudioImporterSampleSettings b)
            => a.loadType == b.loadType && a.compressionFormat == b.compressionFormat
               && Mathf.Approximately(a.quality, b.quality) && a.sampleRateSetting == b.sampleRateSetting;

        // ---------------------------------------------------------------- mixer

        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Master ← Music, SFX ← {Ambience, Weapons, Enemies, Player, World, UI}; Music volume exposed as "music".</summary>
        public static string BuildMixer()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null) return "mixer=MISSING";
            AudioMixerGroup master = FindGroup(mixer, "Master");
            AudioMixerGroup sfx = FindGroup(mixer, "SFX") ?? EnsureGroup(mixer, "SFX", master);
            int created = 0;
            if (FindGroup(mixer, "Music") == null) { EnsureGroup(mixer, "Music", master); created++; }
            foreach (string bus in SfxBuses)
                if (FindGroup(mixer, bus) == null) { EnsureGroup(mixer, bus, sfx); created++; }
            bool exposed = Expose(mixer, FindGroup(mixer, "Music"), "music");
            EditorUtility.SetDirty(mixer);
            return $"mixerGroups+={created} musicParam={(exposed ? "ok" : "MISSING")}";
        }

        public static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
            => mixer.FindMatchingGroups(string.Empty).FirstOrDefault(g => g != null && g.name == name);

        private static AudioMixerGroup EnsureGroup(AudioMixer mixer, string name, AudioMixerGroup parent)
        {
            Type controller = mixer.GetType();
            MethodInfo create = controller.GetMethod("CreateNewGroup", Any);
            object[] args = create.GetParameters().Length == 2 ? new object[] { name, false } : new object[] { name };
            var group = (AudioMixerGroup)create.Invoke(mixer, args);
            controller.GetMethod("AddChildToParent", Any).Invoke(mixer, new object[] { group, parent });
            controller.GetMethod("AddGroupToCurrentView", Any)?.Invoke(mixer, new object[] { group });
            EditorUtility.SetDirty(group);
            return group;
        }

        private static bool Expose(AudioMixer mixer, AudioMixerGroup group, string parameter)
        {
            if (group == null) return false;
            Type controller = mixer.GetType();
            PropertyInfo exposedProperty = controller.GetProperty("exposedParameters", Any);
            object volumeGuid = group.GetType().GetMethod("GetGUIDForVolume", Any).Invoke(group, null);

            var exposed = (Array)exposedProperty.GetValue(mixer);
            foreach (object entry in exposed)
            {
                Type entryType = entry.GetType();
                if (Equals(entryType.GetField("guid").GetValue(entry), volumeGuid))
                    return (string)entryType.GetField("name").GetValue(entry) == parameter || Rename(mixer, volumeGuid, parameter);
                if ((string)entryType.GetField("name").GetValue(entry) == parameter) return false; // name taken
            }

            Type pathType = controller.Assembly.GetType("UnityEditor.Audio.AudioGroupParameterPath");
            object path = Activator.CreateInstance(pathType, Any, null, new[] { group, volumeGuid }, null);
            controller.GetMethod("AddExposedParameter", Any).Invoke(mixer, new[] { path });
            return Rename(mixer, volumeGuid, parameter);
        }

        private static bool Rename(AudioMixer mixer, object guid, string parameter)
        {
            PropertyInfo exposedProperty = mixer.GetType().GetProperty("exposedParameters", Any);
            var exposed = (Array)exposedProperty.GetValue(mixer);
            for (int i = 0; i < exposed.Length; i++)
            {
                object entry = exposed.GetValue(i);
                if (!Equals(entry.GetType().GetField("guid").GetValue(entry), guid)) continue;
                entry.GetType().GetField("name").SetValue(entry, parameter);
                exposed.SetValue(entry, i);
                exposedProperty.SetValue(mixer, exposed);
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- sound events

        private sealed class Spec
        {
            public string name, folder, prefix, farPrefix, remotePrefix;
            public string[] named;
            public AudioBus bus;
            public float volume = 1f, jitter = 1f, min = 2f, max = 40f, distantFrom = 35f, spatial = 1f, cooldown;
            public Vector2 pitch = new(0.96f, 1.04f);
            public int voices = 4, priority = 128;
            public bool loop;
        }

        private static IEnumerable<Spec> Specs()
        {
            // Guns: one fire event per gun from WeaponFoley.json (near / third-person / distant takes in <gun>/Fire).
            foreach (GunFoley gun in LoadFoley().guns)
            {
                string lower = gun.gun.ToLowerInvariant();
                yield return new Spec
                {
                    name = $"Weapon_{gun.gun}_Fire", folder = $"Weapons/{gun.gun}/Fire", prefix = $"{lower}_fire_",
                    remotePrefix = $"{lower}_fire_tp_", farPrefix = $"{lower}_fire_far_", bus = AudioBus.Weapons,
                    volume = gun.fireVolume, pitch = new Vector2(0.97f, 1.03f), min = gun.fireMin, max = gun.fireMax,
                    voices = gun.fireVoices, priority = 40, jitter = 0.8f,
                };
                foreach (var (suffix, cues) in new[] { ("ReloadStart", gun.shellStart), ("ShellIn", gun.shellInsert), ("ReloadEnd", gun.shellEnd) })
                    if (cues != null && cues.Length > 0)
                        yield return new Spec
                        {
                            name = $"Weapon_{gun.gun}_{suffix}", folder = $"Weapons/{gun.gun}/Handling",
                            named = cues.Select(c => c.clip).ToArray(), bus = AudioBus.Weapons,
                            volume = gun.foleyVolume * (cues[0].v > 0f ? cues[0].v : 1f), pitch = new Vector2(0.97f, 1.03f),
                            min = FoleyMin, max = FoleyMax, voices = 3, priority = 80, jitter = 0.5f,
                        };
            }

            yield return new Spec
            {
                name = "Weapon_DryFire", folder = "Weapons/Shared", prefix = "dry_fire_", bus = AudioBus.Weapons, volume = 0.55f,
                pitch = new Vector2(1.4f, 1.6f), min = FoleyMin, max = FoleyMax, voices = 3, priority = 80, spatial = 0f,
            };

            Spec Zombie(string role, float volume, float max, float cooldown) => new()
            {
                name = $"Zombie_{role}", folder = "Enemies/Zombie", prefix = $"zombie_{role.ToLowerInvariant()}_",
                bus = AudioBus.Enemies, volume = volume, pitch = new Vector2(0.9f, 1.1f), min = 2f, max = max,
                voices = 4, cooldown = cooldown, jitter = 2f,
            };
            yield return Zombie("Idle", 0.7f, 25f, 0.35f);
            yield return Zombie("Attack", 0.9f, 30f, 0f);
            yield return Zombie("Hurt", 0.8f, 30f, 0.08f);
            yield return Zombie("Death", 0.9f, 35f, 0f);

            Spec Special(string who, Vector2 pitch, float max) => new()
            {
                name = $"{who}_Hurt", folder = "Enemies/Special", prefix = $"{who.ToLowerInvariant()}_hurt_",
                bus = AudioBus.Enemies, volume = 1f, pitch = pitch, min = 4f, max = max, voices = 2, jitter = 1.5f,
            };
            yield return Special("Tank", new Vector2(0.68f, 0.78f), 60f);
            yield return Special("Infector", new Vector2(0.85f, 1.0f), 40f);
            yield return Special("Screamer", new Vector2(1.05f, 1.2f), 40f);

            yield return new Spec
            {
                name = "Player_Footstep", folder = "Player/Footsteps", prefix = "step_concrete_", bus = AudioBus.Player,
                volume = 0.6f, pitch = new Vector2(0.92f, 1.08f), min = 1.5f, max = 22f, voices = 8, jitter = 2f,
            };
            yield return new Spec
            {
                name = "Player_Land", folder = "Player/Footsteps", prefix = "land_", bus = AudioBus.Player,
                volume = 0.8f, pitch = new Vector2(0.92f, 1.05f), min = 1.5f, max = 22f, voices = 3,
            };
            yield return new Spec
            {
                name = "Player_DownedBreath", folder = "Player/Voice", prefix = "breathing_downed_loop", bus = AudioBus.Player,
                volume = 0.55f, pitch = new Vector2(1f, 1f), min = 1.5f, max = 14f, voices = 4, jitter = 0f, loop = true,
            };
            foreach (string gender in new[] { "Male", "Female" })
            foreach (var (line, volume, max) in new[] { ("Hurt", 0.85f, 30f), ("Downed", 1f, 45f), ("Death", 1f, 45f) })
            {
                string prefix = $"{gender.ToLowerInvariant()}_{line.ToLowerInvariant()}_";
                var spec = new Spec
                {
                    name = $"Voice_{gender}_{line}", folder = $"Player/Voice/{gender}", prefix = prefix, bus = AudioBus.Player,
                    volume = volume, pitch = new Vector2(0.96f, 1.04f), min = 2f, max = max, voices = 4, priority = 60,
                };
                yield return spec;
            }
            foreach (string character in PlayerCharacters)
            foreach (string line in new[] { "Hurt", "Downed", "Death" })
            {
                yield return new Spec
                {
                    name = $"Voice_{character}_{line}", folder = $"Player/Voice/{character}",
                    prefix = $"{character}_{line}_",
                    named = line == "Hurt" ? new[] { $"{character}_HurtLight_01", $"{character}_HurtMedium_01", $"{character}_HurtHeavy_01" }
                        : line == "Downed" ? new[] { $"{character}_HurtHeavy_01" } : null,
                    bus = AudioBus.Player, volume = line == "Hurt" ? 0.85f : 1f,
                    pitch = Vector2.one, jitter = 0f, min = 2f, max = line == "Hurt" ? 30f : 45f,
                    voices = 4, priority = 60,
                };
            }
        }

        public static Dictionary<string, SoundEvent> BuildEvents(bool playersOnly = false)
        {
            EnsureFolder(EventsFolder);
            var events = new Dictionary<string, SoundEvent>();
            foreach (Spec spec in Specs())
            {
                if (playersOnly && spec.bus != AudioBus.Player) continue;
                string path = $"{EventsFolder}/{spec.name}.asset";
                var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(path);
                if (sound == null)
                {
                    sound = ScriptableObject.CreateInstance<SoundEvent>();
                    AssetDatabase.CreateAsset(sound, path);
                }
                sound.bus = spec.bus;
                sound.clips = spec.named != null ? spec.named.Select(n => Clip(spec.folder, n)).ToArray() : Clips(spec.folder, spec.prefix);
                // Male hurt also uses the strain takes; the clip folder keeps them apart by name.
                if (spec.name == "Voice_Male_Hurt") sound.clips = sound.clips.Concat(Clips(spec.folder, "male_strain_")).ToArray();
                sound.remoteClips = spec.remotePrefix != null ? Clips(spec.folder, spec.remotePrefix) : Array.Empty<AudioClip>();
                sound.distantClips = spec.farPrefix != null ? Clips(spec.folder, spec.farPrefix) : Array.Empty<AudioClip>();
                sound.distantFrom = spec.distantFrom;
                sound.volume = spec.volume;
                sound.volumeJitterDb = spec.jitter;
                sound.pitch = spec.pitch;
                sound.spatialBlend = spec.spatial;
                sound.minDistance = spec.min;
                sound.maxDistance = spec.max;
                sound.maxVoices = spec.voices;
                sound.cooldown = spec.cooldown;
                sound.priority = spec.priority;
                sound.loop = spec.loop;
                EditorUtility.SetDirty(sound);
                events[spec.name] = sound;
            }
            // Female voice has fewer long takes: the same calls serve going down and dying.
            if (events.TryGetValue("Voice_Female_Death", out var femaleDeath) && femaleDeath.clips.Length == 0)
                femaleDeath.clips = Clips("Player/Voice/Female", "female_downed_");
            return events;
        }

        /// <summary>One clip by file name (no extension) in Root/folder; a missing clip is an error, not a silent gap.</summary>
        public static AudioClip Clip(string folder, string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/{folder}/{name}.wav");
            if (clip == null) throw new InvalidOperationException($"Missing clip {Root}/{folder}/{name}.wav");
            return clip;
        }

        /// <summary>Clips in Root/folder named prefix + digits (so "x_fire_" excludes "x_fire_far_01").</summary>
        public static AudioClip[] Clips(string folder, string prefix)
        {
            string directory = $"{Root}/{folder}";
            if (!AssetDatabase.IsValidFolder(directory)) return Array.Empty<AudioClip>();
            var pattern = new Regex("^" + Regex.Escape(prefix) + @"(\d+)?$", RegexOptions.IgnoreCase);
            return AssetDatabase.FindAssets("t:AudioClip", new[] { directory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => System.IO.Path.GetDirectoryName(p).Replace('\\', '/') == directory
                            && pattern.IsMatch(System.IO.Path.GetFileNameWithoutExtension(p)))
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .Where(c => c != null)
                .ToArray();
        }

        // ---------------------------------------------------------------- settings resource

        public static string BuildSettings()
        {
            EnsureFolder(Root + "/Resources");
            var settings = AssetDatabase.LoadAssetAtPath<GameAudioSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GameAudioSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            settings.mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            // Track gains level the four tracks to roughly the same loudness (about -24 dBFS RMS).
            settings.calm = Track("Music/calm_horror_loop.mp3", 0.55f);
            settings.build = Track("Music/build_hunter_class_lifeform.mp3", 0.22f);
            settings.combat = Track("Music/combat_fight_looped.wav", 0.18f);
            settings.tank = Track("Music/tank_determined_pursuit_loop.wav", 0.33f);
            settings.ambienceBed = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Ambience/factory_bed_dungeon_ambient.ogg");
            settings.ambienceBedVolume = 0.7f;
            settings.ambienceWind = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/Ambience/wind_woosh_loop.ogg");
            settings.ambienceWindVolume = 0.35f;
            EditorUtility.SetDirty(settings);
            int tracks = new[] { settings.calm, settings.build, settings.combat, settings.tank }.Count(t => t.clip != null);
            return $"musicTracks={tracks} ambience={(settings.ambienceBed != null && settings.ambienceWind != null ? 2 : 0)}";
        }

        private static GameAudioSettings.MusicTrack Track(string relative, float volume) => new()
        {
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/{relative}"),
            volume = volume,
        };

        // ---------------------------------------------------------------- references

        // ---------------------------------------------------------------- weapon foley table

        public const string FoleyPath = "Assets/FPS/Features/Audio/Editor/WeaponFoley.json";
        private const float FoleyMin = 1.5f, FoleyMax = 25f;

        [Serializable] private sealed class FoleyCue { public float t; public string clip; public float v; }

        [Serializable]
        private sealed class GunFoley
        {
            public string gun;
            public float fireVolume = 1f, fireMin = 6f, fireMax = 120f, foleyVolume = 0.6f;
            public int fireVoices = 4;
            public FoleyCue[] equip, fire, reload, shellStart, shellInsert, shellEnd;
        }

        [Serializable] private sealed class FoleyTable { public GunFoley[] guns; }

        private static FoleyTable LoadFoley()
        {
            var table = JsonUtility.FromJson<FoleyTable>(System.IO.File.ReadAllText(FoleyPath));
            if (table?.guns == null || table.guns.Length == 0) throw new InvalidOperationException("No guns in " + FoleyPath);
            return table;
        }

        private static WeaponSoundCue[] Cues(GunFoley gun, FoleyCue[] cues) => (cues ?? Array.Empty<FoleyCue>())
            .OrderBy(c => c.t)
            .Select(c => new WeaponSoundCue(c.t, Clip($"Weapons/{gun.gun}/Handling", c.clip), gun.foleyVolume * (c.v > 0f ? c.v : 1f)))
            .ToArray();

        // ---------------------------------------------------------------- references

        public static string AssignWeapons(Dictionary<string, SoundEvent> e)
        {
            int count = 0, cues = 0;
            foreach (GunFoley gun in LoadFoley().guns)
            {
                var data = AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponsContent}/{gun.gun}/{gun.gun}.asset");
                if (data == null) continue;
                data.fireSound = e[$"Weapon_{gun.gun}_Fire"];
                data.dryFireSound = e["Weapon_DryFire"];
                data.equipCues = Cues(gun, gun.equip);
                data.fireCues = Cues(gun, gun.fire);
                data.reloadCues = data.reloadMode == ReloadMode.PerShell ? Array.Empty<WeaponSoundCue>() : Cues(gun, gun.reload);
                e.TryGetValue($"Weapon_{gun.gun}_ReloadStart", out data.reloadStartSound);
                e.TryGetValue($"Weapon_{gun.gun}_ShellIn", out data.reloadInsertSound);
                e.TryGetValue($"Weapon_{gun.gun}_ReloadEnd", out data.reloadEndSound);
                cues += data.equipCues.Length + data.fireCues.Length + data.reloadCues.Length;
                EditorUtility.SetDirty(data);
                count++;
            }
            return $"weapons={count} foleyCues={cues}";
        }

        public static string AssignEnemies(Dictionary<string, SoundEvent> e)
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { EnemiesContent }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)?.GetComponent<EnemyAI>() != null)
                // Bases before variants, so a variant inherits the voice instead of getting a second one.
                .OrderBy(p => PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(p)) == PrefabAssetType.Variant)
                .ThenBy(p => p, StringComparer.Ordinal).ToList();
            var summary = new List<string>();
            foreach (string path in paths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var enemy = root.GetComponent<EnemyAI>();
                    var voice = root.GetComponent<EnemyVoice>();
                    if (voice == null) voice = root.AddComponent<EnemyVoice>();
                    string kind = enemy is SI_Tank ? "Tank" : enemy is SI_Infector ? "Infector" : enemy is SI_Screamer ? "Screamer" : "Zombie";
                    if (kind == "Zombie") voice.Configure(e["Zombie_Idle"], e["Zombie_Attack"], e["Zombie_Hurt"], e["Zombie_Death"]);
                    else voice.Configure(null, null, e[$"{kind}_Hurt"], null);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    summary.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)}:{kind}");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return $"enemies={summary.Count}[{string.Join(",", summary)}]";
        }

        public static string AssignPlayers(Dictionary<string, SoundEvent> e)
        {
            var male = VoiceSet("Voice_Male", e["Voice_Male_Hurt"], e["Voice_Male_Downed"], e["Voice_Male_Death"]);
            var female = VoiceSet("Voice_Female", e["Voice_Female_Hurt"], e["Voice_Female_Downed"], e["Voice_Female_Death"]);
            var characters = PlayerCharacters.ToDictionary(n => n,
                n => VoiceSet($"Voice_{n}", e[$"Voice_{n}_Hurt"], e[$"Voice_{n}_Downed"], e[$"Voice_{n}_Death"]));
            var summary = new List<string>();
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { PlayersContent }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset.GetComponent<PlayerHealth>() == null) continue;
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var audio = root.GetComponent<PlayerAudio>();
                    if (audio == null) audio = root.AddComponent<PlayerAudio>();
                    bool isMale = MaleCharacters.Any(n => asset.name.StartsWith(n, StringComparison.Ordinal));
                    string character = PlayerCharacters.FirstOrDefault(n => asset.name == n + "Player");
                    var voice = character != null ? characters[character] : isMale ? male : female;
                    audio.Configure(voice, e["Player_Footstep"], e["Player_Land"], e["Player_DownedBreath"]);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    summary.Add($"{asset.name}:{voice.name}");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return $"players={summary.Count}[{string.Join(",", summary)}]";
        }

        private static PlayerVoiceSet VoiceSet(string name, SoundEvent hurt, SoundEvent downed, SoundEvent death)
        {
            string path = $"{EventsFolder}/{name}.asset";
            var set = AssetDatabase.LoadAssetAtPath<PlayerVoiceSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<PlayerVoiceSet>();
                AssetDatabase.CreateAsset(set, path);
            }
            set.hurt = hurt;
            set.downed = downed;
            set.death = death;
            EditorUtility.SetDirty(set);
            return set;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
