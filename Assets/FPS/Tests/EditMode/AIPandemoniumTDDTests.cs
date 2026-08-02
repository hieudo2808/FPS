using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using UniBT;

namespace FPS.Tests
{
    public class AIPandemoniumTDDTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in createdObjects)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }

            ResetSingleton(typeof(PlayerProfiler));
            ResetSingleton(typeof(AttackSlotManager));
            ResetSingleton(typeof(AIDirector));
            ResetSingleton(typeof(InfluenceMapManager));
            ResetSingleton(typeof(ZombieRegistry));
            ResetSingleton(typeof(SpecialInfectedRegistry));
            ResetSceneSingleton(typeof(ZombieFactory));
            ResetSceneSingleton(typeof(RubberBandingSystem));
        }

        [Test]
        public void AIDirector_OfflineSpawnEvent_IncrementsZombiesAlive()
        {
            var directorGo = Create("AIDirector");
            var director = directorGo.AddComponent<AIDirector>();
            InvokePrivate(director, "Awake");

            var factoryGo = Create("ZombieFactory");
            var factory = factoryGo.AddComponent<ZombieFactory>();
            InvokePrivate(factory, "Awake");

            InvokeIfExists(director, "OnEnable");

            var prefab = Create("ZombiePrefab");
            var registryGo = Create("ZombieRegistry");
            var registry = registryGo.AddComponent<ZombieRegistry>();
            InvokePrivate(registry, "Awake");
            registry.AddZombieType(new ZombieData
            {
                displayName = "Test Zombie",
                prefab = prefab,
                baseHP = 10f,
                baseSpeed = 1f,
                baseDamage = 1f,
                attackRate = 1f,
                spawnWeight = 1
            });

            GameObject spawned = factory.SpawnZombie(new Vector3(30f, 0f, 0f), Quaternion.identity);
            if (spawned != null)
                createdObjects.Add(spawned);

            Assert.NotNull(spawned, "Test setup should spawn a zombie through the real factory path.");
            Assert.AreEqual(1, director.ZombiesAlive,
                "Offline/local director mode must count factory spawn events even when OnNetworkSpawn never runs.");
        }

        [Test]
        public void DamageInfo_AddsAttributedDamageWithoutRemovingLegacyDamage()
        {
            Assert.True(typeof(IDamageable).IsAssignableFrom(typeof(IAttributedDamageable)),
                "Attributed damage must remain compatible with legacy IDamageable callers.");

            var method = typeof(IAttributedDamageable).GetMethod("TakeDamage", new[] { typeof(DamageInfo) });
            Assert.NotNull(method, "IAttributedDamageable must accept DamageInfo for attacker/headshot/reaction attribution.");

            Assert.True(typeof(IAttributedDamageable).IsAssignableFrom(typeof(EnemyHealth)),
                "EnemyHealth must accept attributed damage so deaths can update PlayerProfiler kill stats.");
        }

        [Test]
        public void PlayerCombatTelemetry_ClampsAmmoAndExpiresStaleSamples()
        {
            var telemetryGo = Create("Telemetry");
            var telemetry = telemetryGo.AddComponent<PlayerCombatTelemetry>();

            telemetry.ApplyWeaponState(false, 90, 30, 10.0);

            Assert.AreEqual(1f, telemetry.AmmoPercent, 0.001f, "Ammo percent must be clamped to 1.");
            Assert.False(telemetry.IsReloading);
            Assert.True(telemetry.IsFresh(11.0), "Fresh telemetry should be usable by AI target scoring.");
            Assert.False(telemetry.IsFresh(20.0), "Stale telemetry must not influence AI target scoring.");
        }

        [Test]
        public void PlayerCombatTelemetry_DoesNotExposeClientReportedWeaponRpc()
        {
            var method = typeof(PlayerCombatTelemetry).GetMethod(
                "ReportWeaponStateServerRpc",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.Null(method,
                "Ammo/reload telemetry must be sourced from WeaponFireHandler server state, never a client report RPC.");
        }

        [Test]
        public void DifficultyManager_PandemoniumExposesSmartAIKnobs()
        {
            var managerGo = Create("DifficultyManager");
            var manager = managerGo.AddComponent<DifficultyManager>();

            DifficultyStats stats = manager.GetStats(DifficultyLevel.Pandemonium);

            Assert.AreEqual(6, stats.maxConcurrentAttackers, "Pandemonium should pressure harder but keep an attacker cap.");
            Assert.Less(stats.spawnIntervalMultiplier, 1f, "Pandemonium should spawn faster through explicit difficulty stats.");
            Assert.Greater(stats.maxAliveMultiplier, 1f, "Pandemonium should allow a larger horde budget.");
            Assert.Greater(stats.specialSpawnChance, 0f, "Pandemonium should increase special infected pressure.");
        }

        [Test]
        public void InfluenceMap_FairSpawnRejectsCloseVisibleAndNearBehindPoints()
        {
            var profiler = CreateProfilerWithSinglePlayer(Vector3.zero, Vector3.forward);
            var mapGo = Create("InfluenceMap");
            var map = mapGo.AddComponent<InfluenceMapManager>();

            Assert.False(map.IsFairSpawnPoint(new Vector3(0f, 0f, 10f)), "Too-close points must never be fair.");
            Assert.False(map.IsFairSpawnPoint(new Vector3(0f, 0f, 40f)), "Near points inside the player's view cone must be rejected.");
            Assert.False(map.IsFairSpawnPoint(new Vector3(0f, 0f, -12f)), "Near behind-player ambush points must be rejected.");
            Assert.False(map.IsFairSpawnPoint(Vector3.zero), "Zero fallback must never be fair while players exist.");
            Assert.True(map.IsFairSpawnPoint(new Vector3(0f, 0f, -35f)), "A far off-screen pressure point can be fair.");

            Assert.NotNull(profiler);
        }

        [Test]
        public void AttackSlotManager_OverflowZombieGetsWaitPositionInsteadOfPlayerCenter()
        {
            CreateProfilerWithSinglePlayer(Vector3.zero, Vector3.forward);

            var managerGo = Create("AttackSlotManager");
            var manager = managerGo.AddComponent<AttackSlotManager>();
            InvokePrivate(manager, "Awake");
            SetPrivateField(manager, "slotsPerPlayer", 1);

            var firstZombie = Create("FirstZombie").AddComponent<EnemyAI>();
            firstZombie.transform.position = new Vector3(2f, 0f, 0f);
            var secondZombie = Create("SecondZombie").AddComponent<EnemyAI>();
            secondZombie.transform.position = new Vector3(-2f, 0f, 0f);

            Assert.True(manager.RequestSlot(firstZombie, 0));
            Assert.False(manager.RequestSlot(secondZombie, 0), "Second zombie should become a waiter when the attacker cap is full.");

            Vector3 destination = manager.GetDestinationFor(secondZombie, 0, null);

            Assert.Greater(Vector3.Distance(destination, Vector3.zero), 2.1f,
                "Overflow zombies must wait/orbit outside the attack slot ring instead of dogpiling the player center.");
        }

        [Test]
        public void AttackSlotManager_AttackerSlotNavMeshFail_DoesNotReturnPlayerCenter()
        {
            CreateProfilerWithSinglePlayer(Vector3.zero, Vector3.forward);

            var managerGo = Create("AttackSlotManager");
            var manager = managerGo.AddComponent<AttackSlotManager>();
            InvokePrivate(manager, "Awake");
            SetPrivateField(manager, "slotsPerPlayer", 1);
            SetPrivateField(manager, "navMeshSampleRange", 0f);

            var attacker = Create("Attacker").AddComponent<EnemyAI>();
            attacker.transform.position = new Vector3(2f, 0f, 0f);

            Assert.True(manager.RequestSlot(attacker, 0), "First zombie should receive the active attack slot.");

            Vector3 destination = manager.GetDestinationFor(attacker, 0, null);

            Assert.Greater(Vector3.Distance(destination, Vector3.zero), 0.5f,
                "An assigned attacker must not fall back to the player center when NavMesh sampling fails.");
        }

        [Test]
        public void AttackSlotManager_ExposesCoordinationModes()
        {
            Assert.NotNull(typeof(EnemyAssignmentMode), "Enemy assignment modes must be explicit for horde coordination.");
            Assert.NotNull(typeof(AttackSlotManager).GetMethod("GetDestinationFor", BindingFlags.Instance | BindingFlags.Public),
                "EnemyAI should ask AttackSlotManager for every coordinated chase destination.");
        }

        [Test]
        public void ZombieFactory_FairPressurePlayerIndexUsesNearFairBand()
        {
            Assert.Null(typeof(ZombieFactory).GetMethod("SpawnZombieBehindPlayer", BindingFlags.Instance | BindingFlags.Public),
                "No public API should request literal behind-player spawning.");
            Assert.NotNull(typeof(ZombieFactory).GetMethod("SpawnZombieAtFairPressurePosition", new[] { typeof(int), typeof(float), typeof(float), typeof(float) }),
                "ZombieFactory should expose fair pressure spawning instead of behind-player spawning.");

            CreateProfilerWithSinglePlayer(Vector3.zero, Vector3.forward);

            var mapGo = Create("InfluenceMap");
            var map = mapGo.AddComponent<InfluenceMapManager>();
            InvokePrivate(map, "Awake");
            Vector3 fairPressurePoint = new Vector3(0f, 0f, -35f);
            SetPrivateField(map, "cachedNavMeshPoints", new List<Vector3>
            {
                new Vector3(0f, 0f, -20f),
                fairPressurePoint,
                new Vector3(0f, 0f, -80f)
            });

            var prefab = Create("ZombiePrefab");
            var registryGo = Create("ZombieRegistry");
            var registry = registryGo.AddComponent<ZombieRegistry>();
            InvokePrivate(registry, "Awake");
            registry.AddZombieType(new ZombieData { displayName = "Test Zombie", prefab = prefab, spawnWeight = 1 });

            var factoryGo = Create("ZombieFactory");
            var factory = factoryGo.AddComponent<ZombieFactory>();
            InvokePrivate(factory, "Awake");

            GameObject spawned = factory.SpawnZombieAtFairPressurePosition(0);
            if (spawned != null)
                createdObjects.Add(spawned);

            Assert.NotNull(spawned, "A fair near-player pressure candidate should be spawnable.");
            Assert.AreEqual(fairPressurePoint, spawned.transform.position,
                "Player-index fair pressure spawn should use the near fair band instead of falling back to a far/global point.");
        }

        [Test]
        public void AIDirector_SkipsSpecialSpawnWhenNoFairPositionExists()
        {
            CreateProfilerWithSinglePlayer(Vector3.zero, Vector3.forward);

            var directorGo = Create("AIDirector");
            var director = directorGo.AddComponent<AIDirector>();
            InvokePrivate(director, "Awake");

            var method = typeof(AIDirector).GetMethod("TryGetSpecialSpawnPosition", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, "AIDirector must centralize special spawn position policy.");

            object[] args = { Vector3.zero };
            bool result = (bool)method.Invoke(director, args);

            Assert.False(result, "Special spawn must skip when players exist and no fair spawn provider is available.");
            Assert.AreEqual(Vector3.zero, (Vector3)args[0], "Failed special spawn lookup must not leak a fallback position.");
        }

        [Test]
        public void SpecialRegistry_RegisterFrameworkOnlySpecial_DoesNotMarkPlayable()
        {
            var registryGo = Create("SpecialRegistry");
            var registry = registryGo.AddComponent<SpecialInfectedRegistry>();
            InvokePrivate(registry, "Awake");

            var screamerPrefab = Create("ScreamerPrefab");
            screamerPrefab.AddComponent<SI_Screamer>();

            registry.RegisterSpecialPrefab(SpecialType.Screamer, screamerPrefab);

            Assert.False(registry.HasImplementedSpecial(),
                "RegisterSpecialPrefab should register framework data only; it must not make a special live-playable.");
        }

        [Test]
        public void SpecialData_LegacyImplementedFlagDoesNotOverrideFrameworkState()
        {
            var data = new SpecialInfectedData
            {
                type = SpecialType.Screamer,
                implementationState = SpecialImplementationState.FrameworkOnly,
                isImplemented = true
            };

            Assert.False(data.IsPlayable,
                "implementationState must be the only live-spawn contract; legacy isImplemented data must not promote framework code.");
        }

        [Test]
        public void SpecialRegistry_RegisterPlayableScreamerCanSpawn()
        {
            var registryGo = Create("SpecialRegistry");
            var registry = registryGo.AddComponent<SpecialInfectedRegistry>();
            InvokePrivate(registry, "Awake");

            var screamerPrefab = Create("ScreamerPrefab");
            screamerPrefab.AddComponent<SI_Screamer>();

            var registerPlayable = typeof(SpecialInfectedRegistry).GetMethod(
                "RegisterPlayableSpecialPrefab",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(registerPlayable,
                "Registry needs an explicit API for promoting framework specials to live-playable specials.");

            bool registered = (bool)registerPlayable.Invoke(registry, new object[] { SpecialType.Screamer, screamerPrefab });

            Assert.True(registered, "Screamer with a real brain should be accepted as playable.");
            Assert.True(registry.HasImplementedSpecial(), "Playable Screamer should be eligible for director spawning.");
        }

        [Test]
        public void SpecialRegistry_RegisterPlayableRejectsFrameworkOnlySpecials()
        {
            var registryGo = Create("SpecialRegistry");
            var registry = registryGo.AddComponent<SpecialInfectedRegistry>();
            InvokePrivate(registry, "Awake");

            var registerPlayable = typeof(SpecialInfectedRegistry).GetMethod(
                "RegisterPlayableSpecialPrefab",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(registerPlayable,
                "Registry needs an explicit API so framework-only specials cannot accidentally go live.");

            var stalkerPrefab = Create("StalkerPrefab");
            stalkerPrefab.AddComponent<SI_Stalker>();
            var spitterPrefab = Create("SpitterPrefab");
            spitterPrefab.AddComponent<SI_Spitter>();
            var tankPrefab = Create("TankPrefab");
            tankPrefab.AddComponent<SI_Tank>();

            Assert.False((bool)registerPlayable.Invoke(registry, new object[] { SpecialType.Stalker, stalkerPrefab }),
                "Stalker is framework-only in this pass and must not be promoted to playable.");
            Assert.False((bool)registerPlayable.Invoke(registry, new object[] { SpecialType.Spitter, spitterPrefab }),
                "Spitter is framework-only in this pass and must not be promoted to playable.");
            Assert.False((bool)registerPlayable.Invoke(registry, new object[] { SpecialType.Tank, tankPrefab }),
                "Tank is framework-only in this pass and must not be promoted to playable.");
            Assert.False(registry.HasImplementedSpecial(), "Stub special abilities must not satisfy live spawn checks.");
        }

        [Test]
        public void FrameworkOnlySpecials_DoNotAutoActivateDeferredAbilities()
        {
            var player = Create("Player");
            player.tag = "Player";
            player.transform.position = Vector3.zero;

            AssertFrameworkOnlyAbilityStaysInactive<SI_Stalker>("Stalker");
            AssertFrameworkOnlyAbilityStaysInactive<SI_Spitter>("Spitter");
            AssertFrameworkOnlyAbilityStaysInactive<SI_Tank>("Tank");
        }

        [Test]
        public void PlayerPrefab_HasCombatTelemetryForAIProfiler()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Features/Characters/Content/Players/Player/Player.prefab");
            Assert.NotNull(prefab, "Player prefab should be loadable.");
            Assert.NotNull(prefab.GetComponent<PlayerCombatTelemetry>(),
                "Player prefab must include PlayerCombatTelemetry so PlayerProfiler can read reload/ammo state.");
        }

        [Test]
        public void ScreamerPrefab_DisablesUniBTAutoBrain()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Features/Characters/Content/Enemies/Screamer/Prefabs/BookHeadMonster_withBlood.prefab");
            Assert.NotNull(prefab, "Screamer prefab should be loadable.");

            var behaviorTree = prefab.GetComponent<BehaviorTree>();
            Assert.NotNull(behaviorTree, "The UniBT asset may remain for authoring/history.");
            Assert.False(behaviorTree.enabled, "Screamer must not run a second auto-ticking client-side brain at runtime.");
        }

        [Test]
        public void RubberBanding_TeleportPositionUsesFairInfluencePoint()
        {
            CreateProfilerWithSinglePlayer(Vector3.zero, Vector3.forward);

            var mapGo = Create("InfluenceMap");
            var map = mapGo.AddComponent<InfluenceMapManager>();
            InvokePrivate(map, "Awake");
            InvokePrivate(map, "InitializeGrid");
            Vector3 fairPoint = new Vector3(0f, 0f, -35f);
            SetPrivateField(map, "cachedNavMeshPoints", new List<Vector3>
            {
                new Vector3(0f, 0f, 40f),
                new Vector3(0f, 0f, -12f),
                fairPoint
            });

            var rubberBandGo = Create("RubberBanding");
            var rubberBand = rubberBandGo.AddComponent<RubberBandingSystem>();
            InvokePrivate(rubberBand, "Awake");

            Vector3 teleportPosition = (Vector3)InvokePrivateWithResult(rubberBand, "GetTeleportPosition");

            Assert.AreEqual(fairPoint, teleportPosition,
                "Rubber-banding teleport must use a fair influence-map point, not visible or near-behind fallback positions.");
        }

        [Test]
        public void ZombieFactory_FairPressurePreferredPositionRejectsUnfairPoint()
        {
            CreateProfilerWithSinglePlayer(Vector3.zero, Vector3.forward);

            var mapGo = Create("InfluenceMap");
            var map = mapGo.AddComponent<InfluenceMapManager>();
            InvokePrivate(map, "Awake");
            InvokePrivate(map, "InitializeGrid");
            Vector3 fairPoint = new Vector3(0f, 0f, -35f);
            SetPrivateField(map, "cachedNavMeshPoints", new List<Vector3> { fairPoint });

            var prefab = Create("ZombiePrefab");
            var registryGo = Create("ZombieRegistry");
            var registry = registryGo.AddComponent<ZombieRegistry>();
            InvokePrivate(registry, "Awake");
            registry.AddZombieType(new ZombieData { displayName = "Test Zombie", prefab = prefab, spawnWeight = 1 });

            var factoryGo = Create("ZombieFactory");
            var factory = factoryGo.AddComponent<ZombieFactory>();
            InvokePrivate(factory, "Awake");

            GameObject spawned = factory.SpawnZombieAtFairPressurePosition(new Vector3(0f, 0f, -12f), Quaternion.identity);
            if (spawned != null)
                createdObjects.Add(spawned);

            Assert.NotNull(spawned, "Factory should recover from an unfair preferred point by choosing a fair pressure point.");
            Assert.AreEqual(fairPoint, spawned.transform.position,
                "Unfair preferred spawn positions must not be used raw.");
        }

        private PlayerProfiler CreateProfilerWithSinglePlayer(Vector3 position, Vector3 lookDirection)
        {
            var player = Create("Player");
            player.tag = "Player";
            player.transform.position = position;
            player.transform.forward = lookDirection.normalized;

            var profilerGo = Create("PlayerProfiler");
            var profiler = profilerGo.AddComponent<PlayerProfiler>();
            InvokePrivate(profiler, "Awake");

            var profiles = new List<PlayerProfile>
            {
                new PlayerProfile
                {
                    playerTransform = player.transform,
                    playerIndex = 0,
                    lookDirection = lookDirection.normalized,
                    currentHealth = 100f
                }
            };

            var field = typeof(PlayerProfiler).GetField("playerProfiles", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, "PlayerProfiler should keep profile storage.");
            field.SetValue(profiler, profiles);

            return profiler;
        }

        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            createdObjects.Add(go);
            return go;
        }

        private void AssertFrameworkOnlyAbilityStaysInactive<TSpecial>(string label)
            where TSpecial : SpecialInfectedBase
        {
            var special = Create($"{label}FrameworkOnly").AddComponent<TSpecial>();
            special.transform.position = Vector3.zero;

            bool canUseAbility = (bool)InvokePrivateWithResult(special, "CanUseAbility");
            Assert.False(canUseAbility,
                $"{label} is framework-only in this pass; it must not auto-activate deferred ability code.");
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = FindMethod(target.GetType(), methodName);
            Assert.NotNull(method, $"Missing method {methodName} on {target.GetType().Name}");
            method.Invoke(target, null);
        }

        private static bool InvokeIfExists(object target, string methodName)
        {
            var method = FindMethod(target.GetType(), methodName);
            if (method == null)
                return false;

            method.Invoke(target, null);
            return true;
        }

        private static object InvokePrivateWithResult(object target, string methodName)
        {
            var method = FindMethod(target.GetType(), methodName);
            Assert.NotNull(method, $"Missing method {methodName} on {target.GetType().Name}");
            return method.Invoke(target, null);
        }

        private static MethodInfo FindMethod(System.Type type, string methodName)
        {
            while (type != null)
            {
                var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (method != null)
                    return method;

                type = type.BaseType;
            }

            return null;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field {fieldName} on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static void ResetSingleton(System.Type type)
        {
            var prop = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite)
                prop.SetValue(null, null);
        }

        private static void ResetSceneSingleton(System.Type singletonType)
        {
            var baseType = typeof(SceneSingleton<>).MakeGenericType(singletonType);
            var field = baseType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }
    }
}
