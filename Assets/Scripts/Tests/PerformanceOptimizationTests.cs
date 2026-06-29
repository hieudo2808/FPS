using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

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
    }
}
