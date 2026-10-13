using System;
using System.Reflection;
using FPS;
using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public class SettingsDifficultyRegressionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        [TestCase(DifficultyLevel.Medium)]
        [TestCase(DifficultyLevel.Pandemonium)]
        public void LobbySubscriptionShowsCurrentDifficultyWithoutSendingAChange(DifficultyLevel difficulty)
        {
            var previous = WaitingRoomManager.Instance;
            var root = new GameObject("Lobby difficulty regression");
            root.SetActive(false);
            var manager = root.AddComponent<WaitingRoomManager>();
            var variable = new NetworkVariable<DifficultyLevel>(difficulty);
            try
            {
                typeof(WaitingRoomManager).GetProperty("Instance").SetValue(null, null);
                Invoke(manager, "Awake");
                Set(manager, "<LobbyDifficulty>k__BackingField", variable);
                var dropdown = root.AddComponent<TMP_Dropdown>();
                dropdown.AddOptions(new System.Collections.Generic.List<string>
                    { "Easy", "Medium", "Hard", "Pandemonium" });
                var ui = root.AddComponent<WaitingRoomUI>();
                Set(ui, "difficultyDropdown", dropdown);
                int changes = 0;
                dropdown.onValueChanged.AddListener(_ => changes++);

                Invoke(ui, "TrySubscribe");
                Assert.That(dropdown.value, Is.EqualTo((int)DifficultyLevel.Easy));
                typeof(NetworkBehaviour).GetProperty("IsSpawned").SetValue(manager, true);
                Invoke(ui, "TrySubscribe");
                Assert.That(dropdown.value, Is.EqualTo((int)difficulty));
                Invoke(ui, "Unsubscribe");
                dropdown.SetValueWithoutNotify(0);
                Invoke(ui, "TrySubscribe"); // Reopening the UI must also read the current value.
                Assert.That(dropdown.value, Is.EqualTo((int)difficulty));
                Assert.That(changes, Is.Zero);
                Invoke(ui, "Unsubscribe");
            }
            finally
            {
                Object.DestroyImmediate(root);
                typeof(WaitingRoomManager).GetProperty("Instance").SetValue(null, previous);
            }
        }

        [TestCase(DifficultyLevel.Easy)]
        [TestCase(DifficultyLevel.Medium)]
        [TestCase(DifficultyLevel.Hard)]
        [TestCase(DifficultyLevel.Pandemonium)]
        public void CatchUpRestoresScaledSpeedAfterRegistrationAndPoolReuse(DifficultyLevel difficulty)
        {
            var previousRubber = SingletonField<RubberBandingSystem>(true).GetValue(null);
            var previousProfiler = PlayerProfiler.Instance;
            var root = new GameObject("Catch-up difficulty regression");
            root.SetActive(false);
            try
            {
                var rubber = root.AddComponent<RubberBandingSystem>();
                SingletonField<RubberBandingSystem>(true).SetValue(null, rubber);
                var profiler = root.AddComponent<PlayerProfiler>();
                typeof(PlayerProfiler).GetProperty("Instance").SetValue(null, profiler);
                var player = new GameObject("Profiled player");
                player.transform.SetParent(root.transform);
                var profile = new PlayerProfile { playerTransform = player.transform, lookDirection = Vector3.forward };
                profiler.AllProfiles.Add(profile);
                var zombie = new GameObject("Scaled zombie");
                zombie.transform.SetParent(root.transform);
                zombie.transform.position = Vector3.back * 20f;
                var agent = zombie.AddComponent<NavMeshAgent>();
                agent.speed = 5f;
                var enemy = zombie.AddComponent<EnemyAI>();
                Set(enemy, "agent", agent);

                rubber.RegisterZombie(enemy); // Network spawn happens before factory scaling.
                float speed = 5f * StaticDifficultyProfiles.Get(difficulty).speedMultiplier;
                enemy.SetStats(speed, 10f, 1f);
                rubber.RegisterZombie(enemy); // Factory/Start registration must stay idempotent.
                Invoke(rubber, "UpdateCatchUpSpeed");
                Assert.That(agent.speed, Is.EqualTo(speed * 1.5f).Within(0.001f));
                rubber.RegisterZombie(enemy); // A boost must never become the baseline.

                enemy.SetStats(speed, 10f, 1f); // Replacing stats while boosted must reset boost state.
                Invoke(rubber, "UpdateCatchUpSpeed");
                Assert.That(agent.speed, Is.EqualTo(speed * 1.5f).Within(0.001f));
                player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                profile.lookDirection = player.transform.forward;
                Invoke(rubber, "UpdateCatchUpSpeed");
                Assert.That(agent.speed, Is.EqualTo(speed).Within(0.001f));

                rubber.UnregisterZombie(enemy);
                enemy.ResetForPool();
                rubber.RegisterZombie(enemy);
                enemy.SetStats(speed * 1.2f, 10f, 1f); // Next spawn has an additional director modifier.
                player.transform.rotation = Quaternion.identity;
                profile.lookDirection = Vector3.forward;
                Invoke(rubber, "UpdateCatchUpSpeed");
                Assert.That(agent.speed, Is.EqualTo(speed * 1.2f * 1.5f).Within(0.001f));
                player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                profile.lookDirection = player.transform.forward;
                Invoke(rubber, "UpdateCatchUpSpeed");
                Assert.That(agent.speed, Is.EqualTo(speed * 1.2f).Within(0.001f));
                rubber.UnregisterZombie(enemy);
            }
            finally
            {
                Object.DestroyImmediate(root);
                SingletonField<RubberBandingSystem>(true).SetValue(null, previousRubber);
                typeof(PlayerProfiler).GetProperty("Instance").SetValue(null, previousProfiler);
            }
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(5)]
        public void ResetDefaultsRestoresStartupQualityInEnginePreferenceAndDropdown(int defaultQuality)
        {
            int oldQuality = QualitySettings.GetQualityLevel();
            int oldAA = QualitySettings.antiAliasing;
            int oldMipmap = QualitySettings.globalTextureMipmapLimit;
            float oldLod = QualitySettings.lodBias;
            float oldDpi = QualitySettings.resolutionScalingFixedDPIFactor;
            float oldBufferWidth = ScalableBufferManager.widthScaleFactor;
            float oldBufferHeight = ScalableBufferManager.heightScaleFactor;
            var oldAniso = QualitySettings.anisotropicFiltering;
            int oldDefault = SettingsManager.DefaultGraphicsQuality;
            bool hadQuality = PlayerPrefs.HasKey("GraphicsQuality");
            int oldPreference = PlayerPrefs.GetInt("GraphicsQuality");
            string[] floatKeys = { "MouseSensitivity", "master", "music", "sfx" };
            bool[] hadFloat = Array.ConvertAll(floatKeys, PlayerPrefs.HasKey);
            float[] floatValues = Array.ConvertAll(floatKeys, key => PlayerPrefs.GetFloat(key));
            var previousSettings = SingletonField<SettingsManager>().GetValue(null);
            var previousAudio = SingletonField<AudioManager>().GetValue(null);
            var previousInput = InputManager.Instance;
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            var cameraFlags = Array.ConvertAll(cameras, c => (c.allowHDR, c.allowMSAA, c.allowDynamicResolution));
            var root = new GameObject("Graphics defaults regression");
            root.SetActive(false);
            try
            {
                QualitySettings.SetQualityLevel(defaultQuality, true);
                typeof(SettingsManager).GetMethod("CaptureDefaultGraphicsQuality", StaticPrivate).Invoke(null, null);
                var settings = root.AddComponent<SettingsManager>();
                Set(settings, "settingsLoaded", true);
                Set(settings, "graphicsQuality", defaultQuality);
                SingletonField<SettingsManager>().SetValue(null, settings);
                SingletonField<AudioManager>().SetValue(null, root.AddComponent<AudioManager>());
                typeof(InputManager).GetProperty("Instance").SetValue(null, null);
                var dropdown = root.AddComponent<TMP_Dropdown>();
                dropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
                var ui = root.AddComponent<SettingsUI>();
                Set(ui, "qualityDropdown", dropdown);
                int changedQuality = (defaultQuality + 1) % QualitySettings.names.Length;
                settings.SetGraphicsQuality(changedQuality);
                dropdown.SetValueWithoutNotify(changedQuality);
                Assert.That(SettingsManager.DefaultGraphicsQuality, Is.EqualTo(defaultQuality));
                int changes = 0;
                dropdown.onValueChanged.AddListener(_ => changes++);

                Invoke(ui, "ResetDefaults");
                Assert.That(settings.GraphicsQuality, Is.EqualTo(defaultQuality));
                Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(defaultQuality));
                Assert.That(PlayerPrefs.GetInt("GraphicsQuality", -1), Is.EqualTo(defaultQuality));
                Assert.That(dropdown.value, Is.EqualTo(defaultQuality));
                Assert.That(changes, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                SingletonField<SettingsManager>().SetValue(null, previousSettings);
                SingletonField<AudioManager>().SetValue(null, previousAudio);
                typeof(InputManager).GetProperty("Instance").SetValue(null, previousInput);
                typeof(SettingsManager).GetProperty("DefaultGraphicsQuality").SetValue(null, oldDefault);
                QualitySettings.SetQualityLevel(oldQuality, true);
                QualitySettings.antiAliasing = oldAA;
                QualitySettings.globalTextureMipmapLimit = oldMipmap;
                QualitySettings.lodBias = oldLod;
                QualitySettings.resolutionScalingFixedDPIFactor = oldDpi;
                QualitySettings.anisotropicFiltering = oldAniso;
                ScalableBufferManager.ResizeBuffers(oldBufferWidth, oldBufferHeight);
                for (int i = 0; i < cameras.Length; i++)
                    if (cameras[i] != null)
                    {
                        cameras[i].allowHDR = cameraFlags[i].Item1;
                        cameras[i].allowMSAA = cameraFlags[i].Item2;
                        cameras[i].allowDynamicResolution = cameraFlags[i].Item3;
                    }
                if (hadQuality) PlayerPrefs.SetInt("GraphicsQuality", oldPreference);
                else PlayerPrefs.DeleteKey("GraphicsQuality");
                for (int i = 0; i < floatKeys.Length; i++)
                    if (hadFloat[i]) PlayerPrefs.SetFloat(floatKeys[i], floatValues[i]);
                    else PlayerPrefs.DeleteKey(floatKeys[i]);
                PlayerPrefs.Save();
            }
        }

        private static FieldInfo SingletonField<T>(bool scene = false) where T : Component =>
            (scene ? typeof(SceneSingleton<T>) : typeof(Singleton<T>)).GetField("instance", StaticPrivate);
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Private).SetValue(target, value);
        private static void Invoke(object target, string name) =>
            target.GetType().GetMethod(name, Private).Invoke(target, null);
    }
}
