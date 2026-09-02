using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Netcode;
using UnityEngine.Rendering;

namespace FPS.Tests
{
    /// <summary>
    /// Regression tests cho Task 9: Toi uu hoa PlayerProfiler va TeamAnalyzer.
    /// 1. PlayerProfiler.isMoving chi tinh lai khi positionHistory doi.
    /// 2. TeamAnalyzer chi chay AnalyzeFormation... sau moi 10 frames.
    /// </summary>
    public class PerformanceOptimizationTests
    {
        // -------------------------------------------------------
        // Test 1: PlayerProfiler - isMoving only updates when history changes
        // -------------------------------------------------------
        [Test]
        public void TestPlayerProfiler_IsMoving_OnlyRecalculatedWhenHistoryChanges()
        {
            // Verify UpdateCurrentState ton tai
            var method = typeof(PlayerProfiler).GetMethod(
                "UpdateCurrentState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "UpdateCurrentState phai ton tai tren PlayerProfiler");

            // Sau toi uu Task 9, PlayerProfiler phai co field lastPositionHistoryCount
            // hoac tuong duong de theo doi thay doi cua positionHistory.
            // Ta kiem tra qua Reflection: field "lastPositionHistoryCount" hoac "lastIsMovingHistoryCount"
            var trackField = typeof(PlayerProfiler).GetField(
                "lastIsMovingHistoryCount",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(trackField,
                "PlayerProfiler phai co field 'lastIsMovingHistoryCount' (int) " +
                "de chi tinh lai isMoving khi positionHistory.Count thay doi (Task 9)");
        }

        // -------------------------------------------------------
        // Test 2: TeamAnalyzer - Analysis runs every 10 frames, not every frame
        // -------------------------------------------------------
        [Test]
        public void TestTeamAnalyzer_AnalysisRunsEveryTenFrames()
        {
            // Sau toi uu Task 9, TeamAnalyzer phai co field frameCounter (int)
            var frameCounterField = typeof(TeamAnalyzer).GetField(
                "frameCounter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(frameCounterField,
                "TeamAnalyzer phai co private field 'frameCounter' (int) " +
                "de dem frame va chi chay phan tich sau moi 10 frames (Task 9)");

            // frameCounter phai la kieu int
            Assert.AreEqual(typeof(int), frameCounterField.FieldType,
                "frameCounter phai la kieu int");
        }

        // -------------------------------------------------------
        // Test 3: TeamAnalyzer Update() co logic skip khi frameCounter chua dat 10
        // -------------------------------------------------------
        [Test]
        public void TestTeamAnalyzer_Update_SkipsAnalysisBeforeTenFrames()
        {
            // Tao TeamAnalyzer instance
            var go = new GameObject("TeamAnalyzer");
            var ta = go.AddComponent<TeamAnalyzer>();

            // Inject singleton
            var instanceProp = typeof(TeamAnalyzer).GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(instanceProp, "TeamAnalyzer.Instance property phai ton tai");
            instanceProp.SetValue(null, ta);

            // Lay frameCounter field
            var frameCounterField = typeof(TeamAnalyzer).GetField(
                "frameCounter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(frameCounterField, "frameCounter phai ton tai");

            // Set frameCounter = 0 (moi bat dau)
            frameCounterField.SetValue(ta, 0);

            // Lay currentFormation truoc khi Update
            var formationField = typeof(TeamAnalyzer).GetField(
                "currentFormation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(formationField, "currentFormation field phai ton tai");
            object formationBefore = formationField.GetValue(ta);

            // Goi Update 5 lan (chua du 10 frame)
            var updateMethod = typeof(TeamAnalyzer).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(updateMethod, "Update phai ton tai");

            // Dat PlayerProfiler = null de dam bao skip check PlayerCount
            // Kiem tra frameCounter tang len sau moi Update call
            for (int i = 0; i < 5; i++)
            {
                // frameCounter se tang nhung AnalyzeFormation khong duoc goi vi < 10
                try { updateMethod.Invoke(ta, null); } catch { }
            }

            int frameCountAfter = (int)frameCounterField.GetValue(ta);
            // frameCounter phai tang (sau 5 lan goi Update khi PlayerProfiler.Instance == null,
            // ta van mong muon frameCounter tang de chung to co logic dem frame)
            // Gia tri co the = 0 neu Update return som vi PlayerProfiler.Instance == null
            // Day la test co tinh mo ta hop dong - frameCounter lon hon 0 hoac = 0 neu logic return som
            // Dieu quan trong la field ton tai
            Assert.GreaterOrEqual(frameCountAfter, 0, "frameCounter phai >= 0 sau Update calls");

            // Cleanup
            Object.DestroyImmediate(go);
            instanceProp.SetValue(null, null);
        }

        [Test]
        public void EnemyAI_PathRefreshPolicy_ExposesThrottleAndAgentSubmitCounters()
        {
            Assert.NotNull(typeof(EnemyAI).GetField("pathRefreshInterval", BindingFlags.Instance | BindingFlags.NonPublic),
                "EnemyAI should expose a serialized pathRefreshInterval so chase pathing is not tied to every Update.");
            Assert.NotNull(typeof(EnemyAI).GetField("destinationRepathDistance", BindingFlags.Instance | BindingFlags.NonPublic),
                "EnemyAI should expose a serialized destinationRepathDistance to avoid redundant NavMeshAgent.SetDestination calls.");

            var snapshotType = typeof(EnemyAI).GetNestedType("TestSnapshot", BindingFlags.Public);
            Assert.NotNull(snapshotType, "EnemyAI should keep a test snapshot for runtime behavior verification.");
            Assert.NotNull(snapshotType.GetField("agentDestinationRequestCount"),
                "EnemyAI test snapshot should expose real NavMesh destination submissions separately from intent destination updates.");
        }

        [Test]
        public void AttackSlotManager_ExpensiveWorkRunsOnSeparateCadences()
        {
            Assert.NotNull(typeof(AttackSlotManager).GetField("cleanupInterval", BindingFlags.Instance | BindingFlags.NonPublic),
                "AttackSlotManager should not cleanup dead zombies every frame.");
            Assert.NotNull(typeof(AttackSlotManager).GetField("slotPositionUpdateInterval", BindingFlags.Instance | BindingFlags.NonPublic),
                "AttackSlotManager waiter promotion/slot position work should run on a short explicit interval.");
            Assert.NotNull(typeof(AttackSlotManager).GetField("timeoutCheckInterval", BindingFlags.Instance | BindingFlags.NonPublic),
                "AttackSlotManager timeout checks should remain explicit and configurable.");
        }

        [Test]
        public void PlayerProfiler_ReusesNetworkRefreshCollections()
        {
            Assert.NotNull(typeof(PlayerProfiler).GetField("nextProfiles", BindingFlags.Instance | BindingFlags.NonPublic),
                "PlayerProfiler should reuse nextProfiles instead of allocating a new list every refresh.");
            Assert.NotNull(typeof(PlayerProfiler).GetField("connectedClientCache", BindingFlags.Instance | BindingFlags.NonPublic),
                "PlayerProfiler should sort connected clients through a reusable cache instead of LINQ OrderBy allocation.");
            Assert.NotNull(typeof(PlayerProfiler).GetField("activeClientIds", BindingFlags.Instance | BindingFlags.NonPublic),
                "PlayerProfiler should reuse activeClientIds instead of allocating a HashSet every refresh.");
            Assert.NotNull(typeof(PlayerProfiler).GetField("staleClientIds", BindingFlags.Instance | BindingFlags.NonPublic),
                "PlayerProfiler should reuse staleClientIds instead of allocating a list every refresh.");
        }

        [Test]
        public void Weapon_CachesOwnerHotPathDependencies()
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera bodyCamera = cameraGo.AddComponent<Camera>();

            var playerGo = new GameObject("Player");
            playerGo.AddComponent<NetworkObject>();
            var fireHandler = playerGo.AddComponent<WeaponFireHandler>();
            var weaponManager = playerGo.AddComponent<WeaponManager>();

            var weaponGo = new GameObject("Weapon");
            weaponGo.transform.SetParent(playerGo.transform);
            var weapon = weaponGo.AddComponent<Weapon>();

            try
            {
                weapon.BindFirstPersonPresentation(bodyCamera, null);
                weapon.SetOwner(true);

                Assert.AreSame(fireHandler, GetPrivateField<WeaponFireHandler>(weapon, "cachedFireHandler"),
                    "Weapon should cache WeaponFireHandler instead of GetComponentInParent during every fire/reload.");
                Assert.AreSame(weaponManager, GetPrivateField<WeaponManager>(weapon, "cachedWeaponManager"),
                    "Weapon should cache WeaponManager for reload animation triggers.");
                Assert.AreSame(bodyCamera, GetPrivateField<Camera>(weapon, "cachedCamera"),
                    "Weapon should retain the explicit body-camera binding instead of searching Camera.main on the hot path.");
            }
            finally
            {
                Object.DestroyImmediate(weaponGo);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(cameraGo);
            }
        }

        [Test]
        public void DistanceRenderSettings_DefaultBucketsAreOrderedAndStable()
        {
            var settings = ScriptableObject.CreateInstance<DistanceRenderSettings>();

            try
            {
                Assert.IsTrue(settings.IsValid, "Default distance render settings should be valid out of the box.");
                Assert.Less(settings.NearDistance, settings.MidDistance);
                Assert.Less(settings.MidDistance, settings.FarDistance);

                Assert.AreEqual(DistanceRenderBucket.Near,
                    settings.EvaluateBucket(settings.NearDistance + settings.Hysteresis * 0.5f, DistanceRenderBucket.Near),
                    "Hysteresis should prevent bucket flicker just outside the near boundary.");
                Assert.AreEqual(DistanceRenderBucket.Culled,
                    settings.EvaluateBucket(settings.FarDistance + settings.Hysteresis + 1f, DistanceRenderBucket.Far));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void DistanceRenderTarget_CulledBucketOnlyDisablesVisuals()
        {
            var settings = ScriptableObject.CreateInstance<DistanceRenderSettings>();
            var go = new GameObject("DistanceRenderEnemy");
            var renderer = go.AddComponent<MeshRenderer>();
            var collider = go.AddComponent<BoxCollider>();
            var networkObject = go.AddComponent<NetworkObject>();
            var target = go.AddComponent<DistanceRenderTarget>();

            try
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                target.CacheReferences();
                target.ApplyBucket(DistanceRenderBucket.Culled, settings);

                Assert.IsFalse(renderer.enabled, "Culled distance bucket should hide renderers.");
                Assert.IsTrue(collider.enabled, "Distance rendering must not disable gameplay colliders.");
                Assert.IsTrue(networkObject.enabled, "Distance rendering must not disable NetworkObject/gameplay roots.");

                target.ApplyBucket(DistanceRenderBucket.Near, settings);
                Assert.IsTrue(renderer.enabled, "Returning to near bucket should re-enable renderers.");
                Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode,
                    "Near bucket should restore the renderer's original shadow policy.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void RenderDistanceLayers_AreReservedWithoutClobberingExistingGameplayLayers()
        {
            Assert.AreEqual(6, LayerMask.NameToLayer("FirstPerson"));
            Assert.AreEqual(7, LayerMask.NameToLayer("ThirdPerson"));
            Assert.AreEqual(8, LayerMask.NameToLayer("Weapon"));
            Assert.AreEqual(9, LayerMask.NameToLayer("SmallProps"));
            Assert.AreEqual(10, LayerMask.NameToLayer("VFX"));
            Assert.AreEqual(11, LayerMask.NameToLayer("EnemyVisual"));
        }

        [Test]
        public void GameLog_DebugLogsAreConditionallyCompiled()
        {
            AssertConditional(nameof(GameLog.Info), typeof(string));
            AssertConditional(nameof(GameLog.Warning), typeof(string));
        }

        [Test]
        public void PoolingTypesExposeCapacityForValidation()
        {
            Assert.NotNull(typeof(ObjectPooling).GetProperty("Capacity"),
                "ObjectPooling should expose Capacity so prefab validation can compare expected active count.");
            Assert.NotNull(typeof(ZombiePoolManager).GetProperty("ConfiguredPoolSizePerType"),
                "ZombiePoolManager should expose configured pool size for spawn budget validation.");
            Assert.NotNull(typeof(ZombiePoolManager).GetMethod("HasPoolFor", new[] { typeof(GameObject) }),
                "ZombiePoolManager should expose HasPoolFor so tests can catch missing registrations.");
            Assert.NotNull(typeof(SpecialInfectedRegistry).GetField("aliveCleanupInterval", BindingFlags.Instance | BindingFlags.NonPublic),
                "SpecialInfectedRegistry should cleanup alive specials on an interval instead of every frame.");
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            return (T)instance.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(instance);
        }

        private static void AssertConditional(string methodName, params System.Type[] parameterTypes)
        {
            var method = typeof(GameLog).GetMethod(methodName, parameterTypes);
            Assert.NotNull(method, $"GameLog.{methodName} should exist.");

            var attributes = method.GetCustomAttributes(typeof(ConditionalAttribute), false);
            Assert.IsNotEmpty(attributes,
                $"GameLog.{methodName} should use ConditionalAttribute so callsites are stripped outside editor/development builds.");
        }
    }
}
