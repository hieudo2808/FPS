using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace FPS.Tests
{
    public sealed class CampaignRulesTests
    {
        [TestCase(CampaignObjectiveId.FactoryShipping, CampaignFileId.FactoryManifest, 1, 1)]
        [TestCase(CampaignObjectiveId.AsylumPatient, CampaignFileId.AsylumPatientRecord, 1, 0)]
        [TestCase(CampaignObjectiveId.AsylumTransfer, CampaignFileId.AsylumMortuaryTransfer, 0, 0)]
        [TestCase(CampaignObjectiveId.LabPower, CampaignFileId.LabPowerRecord, 13, 0)]
        [TestCase(CampaignObjectiveId.LabArchive, CampaignFileId.LabOriginalArchive, 1, 0)]
        public void ComparisonRequiresRecordedSourceBeforeCorrectAnswerCanMutateState(CampaignObjectiveId id, CampaignFileId source, int first, int second)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignContentCatalog>(CampaignContentAuthoring.CatalogPath);
            Assert.That(CampaignRules.ComparisonSource(id), Is.EqualTo(source));
            Assert.That(catalog.FindObjective(id).file, Is.EqualTo(source), "Read stage must use the console's registered catalog source.");
            var state = new CampaignState { chapter = CampaignRules.ChapterOf(id), phase = CampaignPhase.Exploring,
                completed = ulong.MaxValue & ~CampaignState.Bit(id),
                discoveredFiles = CampaignState.FileBit(CampaignFileId.AsylumDeskLog) | CampaignState.FileBit(CampaignFileId.LabIndex) };
            string before = JsonUtility.ToJson(state);
            Assert.That(CampaignRules.CanUse(state, id), Is.EqualTo(CampaignResult.Prerequisite));
            Assert.That(CampaignRules.Availability(state, id).RequiredFileId, Is.EqualTo(source));
            Assert.That(CampaignRules.TryComplete(state, id, first, second), Is.EqualTo(CampaignResult.Prerequisite));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
            Assert.That(CampaignRules.CanReadComparisonSource(state, id), Is.EqualTo(id != CampaignObjectiveId.FactoryShipping));
            Assert.That(CampaignObjectiveSelection.CanTrack(state, id), Is.EqualTo(id != CampaignObjectiveId.FactoryShipping));
            CampaignRules.RecordFile(state, source);
            Assert.That(CampaignRules.CanReadComparisonSource(state, id), Is.False);
            Assert.That(CampaignRules.CanUse(state, id), Is.EqualTo(CampaignResult.Accepted));
            Assert.That(CampaignRules.TryComplete(state, id, first, second), Is.EqualTo(CampaignResult.Accepted));
        }

        [TestCase(CampaignObjectiveId.AsylumPatient, CampaignObjectiveId.AsylumPower, CampaignFileId.AsylumDeskLog)]
        [TestCase(CampaignObjectiveId.LabArchive, CampaignObjectiveId.LabPower, CampaignFileId.LabIndex)]
        public void ComparisonReadStageRetainsPrerequisitesAndRecordedReference(CampaignObjectiveId id, CampaignObjectiveId prerequisite, CampaignFileId reference)
        {
            var state = new CampaignState { chapter = CampaignRules.ChapterOf(id), phase = CampaignPhase.Exploring,
                completed = CampaignState.Bit(prerequisite) | CampaignState.Bit(CampaignObjectiveId.AsylumAccess) };
            Assert.That(CampaignRules.CanReadComparisonSource(state, id), Is.False);
            Assert.That(CampaignRules.Availability(state, id).RequiredFileId, Is.EqualTo(reference));
            CampaignRules.RecordFile(state, reference);
            Assert.That(CampaignRules.CanReadComparisonSource(state, id), Is.True);
            state.completed &= ~CampaignState.Bit(prerequisite);
            Assert.That(CampaignRules.CanReadComparisonSource(state, id), Is.False);
            state.completed |= CampaignState.Bit(prerequisite); state.phase = CampaignPhase.Encounter;
            Assert.That(CampaignRules.CanReadComparisonSource(state, id), Is.False);
            state.phase = CampaignPhase.Exploring; state.chapter = CampaignChapter.Factory;
            Assert.That(CampaignRules.CanReadComparisonSource(state, id), Is.False);
        }

        [Test]
        public void CampaignPresentationKeepsAuthoredBodyWithoutCreatingFloatingText()
        {
            var root = new GameObject("Campaign presentation fixture");
            try
            {
                root.transform.position = new Vector3(2, 4, 6);
                var body = root.AddComponent<BoxCollider>();
                var item = root.AddComponent<CampaignInteractable>();
                item.objectiveId = CampaignObjectiveId.FactoryIsolate; item.interactionBody = body;
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(CampaignInteractable).GetMethod("Start", flags).Invoke(item, null);
                for (int i = 0; i < 3; i++) typeof(CampaignInteractable).GetMethod("Update", flags).Invoke(item, null);
                Assert.That(root.GetComponentsInChildren<Component>(true).Select(c => c.GetType().FullName), Does.Not.Contain("TMPro.TextMeshPro"));
                Assert.That(root.GetComponentsInChildren<Light>(true).Length, Is.EqualTo(1));
                Assert.That(root.transform.position, Is.EqualTo(new Vector3(2, 4, 6)));
                Assert.That(item.TargetPosition, Is.EqualTo(body.bounds.center));
                Assert.That(body.enabled, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void HintClockAdvancesCachedObservationAndResetsWhenFactsEventArrives()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring };
            var clock = new CampaignHintProgress();
            clock.Advance(10, false); Assert.That(clock.Seconds, Is.Zero);
            clock.Observe(state, CampaignObjectiveId.FactoryIsolate);
            clock.Advance(45, false); Assert.That(clock.Seconds, Is.EqualTo(45));
            clock.Advance(10, true); Assert.That(clock.Seconds, Is.EqualTo(45));
            CampaignRules.RecordFile(state, CampaignFileId.FactoryManifest);
            clock.Advance(5, false); Assert.That(clock.Seconds, Is.EqualTo(50), "Timer ticks do not poll mutable campaign state.");
            clock.Observe(state, CampaignObjectiveId.FactoryIsolate); Assert.That(clock.Seconds, Is.Zero);
            state.phase = CampaignPhase.Preparing; clock.Observe(state, CampaignObjectiveId.FactoryIsolate);
            clock.Advance(10, false); Assert.That(clock.Seconds, Is.Zero);
            clock.Observe(null, CampaignObjectiveId.FactoryIsolate);
            clock.Advance(10, false); Assert.That(clock.Seconds, Is.Zero);
        }

        [Test]
        public void FactoryLateShippingErrorCannotReplaceCompletedFeedback()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring, revision = 10,
                completed = CampaignState.Bit(CampaignObjectiveId.FactoryShipping) };
            var buffer = new CampaignFeedbackBuffer(); int delivered = 0;
            buffer.Add(new(CampaignObjectiveId.FactoryShipping, CampaignResult.WrongAnswer, 9,
                mismatch: CampaignShippingGroup.ShipmentSeal), state, _ => delivered++);
            Assert.That(delivered, Is.Zero);
            buffer.Add(new(CampaignObjectiveId.FactoryShipping, CampaignResult.Accepted, 10), state, _ => delivered++);
            buffer.Add(new(CampaignObjectiveId.FactoryShipping, CampaignResult.Accepted, 10), state, _ => delivered++);
            Assert.That(delivered, Is.EqualTo(1));
        }

        [Test]
        public void FactoryPartyGateSoloAfkGraceDownedAndLostEligibility()
        {
            CampaignPartyGateStatus Prep(int living, int inside, int ready, int qualified, int downed = 0, bool grace = false, bool tank = true, float timeout = 1)
                => CampaignRules.EvaluatePartyGate(true, living, inside, ready, qualified, downed, grace, tank, timeout);
            Assert.That(Prep(1, 1, 0, 1).canCountdown, Is.True, "Solo does not need READY.");
            var outside = Prep(1, 0, 0, 0, timeout: 0);
            Assert.That(outside.canCountdown, Is.False, "No teleport or automatic skip outside the zone.");
            Assert.That(outside.reason, Is.EqualTo(CampaignGateWaitReason.EnterVolume));
            Assert.That(Prep(2, 1, 1, 1).canCountdown, Is.False, "AFK still connected before timeout.");
            Assert.That(Prep(2, 1, 1, 1, timeout: 0).canCountdown, Is.True, "Existing override requires one READY in zone.");
            Assert.That(Prep(2, 1, 0, 0, timeout: 0).canCountdown, Is.False, "No qualified READY is not an override.");
            Assert.That(Prep(2, 1, 0, 0, timeout: 0).reason, Is.EqualTo(CampaignGateWaitReason.Ready), "Already inside: ask for READY.");
            Assert.That(Prep(2, 2, 2, 2, grace: true).reason, Is.EqualTo(CampaignGateWaitReason.DisconnectGrace));
            Assert.That(Prep(2, 1, 1, 1, grace: true, timeout: 0).canCountdown, Is.True);
            Assert.That(Prep(2, 1, 1, 1, downed: 1).reason, Is.EqualTo(CampaignGateWaitReason.Revive));
            Assert.That(Prep(0, 0, 0, 0, timeout: 0).canCountdown, Is.False, "Dead/spectating are excluded by server, never start alone.");
            Assert.That(Prep(1, 1, 1, 1, tank: false, timeout: 0).reason, Is.EqualTo(CampaignGateWaitReason.Tank));
            var countdown = Prep(1, 1, 0, 1); countdown.countdownRemaining = 3;
            Assert.That(CampaignRules.PartyGateText(countdown, true), Does.Contain("3s countdown").And.Contain("In zone 1/1"));
            var lost = Prep(1, 0, 0, 0);
            Assert.That(lost.canCountdown, Is.False, "Controller resets countdownSince/boardingSince when this is false.");
            Assert.That(lost.countdownRemaining, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void FactoryBoardingRequiresZoneAndTankBeforeAndAfterTimeout(bool timeout)
        {
            float remaining = timeout ? 0 : 10;
            var gate = CampaignRules.EvaluatePartyGate(false, 2, 1, 0, 1, 0, false, true, remaining);
            Assert.That(gate.canCountdown, Is.EqualTo(timeout));
            Assert.That(gate.overrideActive, Is.EqualTo(timeout));
            gate = CampaignRules.EvaluatePartyGate(false, 2, 0, 0, 0, 0, false, true, remaining);
            Assert.That(gate.canCountdown, Is.False);
            gate = CampaignRules.EvaluatePartyGate(false, 1, 1, 0, 1, 0, false, false, remaining);
            Assert.That(gate.canCountdown, Is.False);
        }

        [Test]
        public void FactoryTankTimeoutRetryAndConfirmedDeathKeepTheTwoBranches()
        {
            var s = new CampaignState { phase = CampaignPhase.Exploring,
                completed = CampaignState.Bit(CampaignObjectiveId.FactoryGenerator) | CampaignState.Bit(CampaignObjectiveId.FactoryShipping),
                discoveredFiles = CampaignState.FileBit(CampaignFileId.FactoryManifest), lastFactoryBranch = CampaignFactoryBranch.Logistics,
                tankStage = CampaignTankStage.Pending };
            var attempt = new CampaignTankAttempt();
            attempt.Tick(0, false); attempt.Tick(9, false);
            Assert.That(attempt.Fallback(20), Is.False);
            attempt.Tick(100, true); Assert.That(attempt.Elapsed, Is.EqualTo(9));
            attempt.Tick(101, false); Assert.That(attempt.Fallback(20), Is.True);
            Assert.That(attempt.Tier(20), Is.EqualTo(1), "Second half of the timeout reverses the anchor order.");
            attempt.Tick(111, false); Assert.That(attempt.TimedOut(20), Is.True);
            Assert.That(attempt.Tier(20), Is.EqualTo(2), "A full timeout relaxes placement instead of failing.");
            Assert.That(s.tankStage, Is.EqualTo(CampaignTankStage.Pending), "Timing out never leaves the pending stage on its own.");
            Assert.That(CampaignRules.CanUse(s, CampaignObjectiveId.FactoryCase), Is.EqualTo(CampaignResult.Prerequisite));
            Assert.That(CampaignRules.TankAllowsExit(s), Is.False);
            attempt.Tick(150, false); Assert.That(attempt.Tier(20), Is.EqualTo(2));
            attempt.Tick(151, false); Assert.That(attempt.Tier(20), Is.EqualTo(3), "Three timeouts without any valid spot ends the wait.");
            ulong completed = s.completed, files = s.discoveredFiles;
            Assert.That(s.completed, Is.EqualTo(completed)); Assert.That(s.discoveredFiles, Is.EqualTo(files));
            Assert.That(s.lastFactoryBranch, Is.EqualTo(CampaignFactoryBranch.Logistics));
            Assert.That(CampaignRules.ConfirmTankDefeat(s, true, true, true), Is.False, "Pending is not a live encounter.");
            s.tankStage = CampaignTankStage.Active;
            Assert.That(CampaignRules.ConfirmTankDefeat(s, true, false, true), Is.False, "Another Tank cannot unlock this encounter.");
            Assert.That(CampaignRules.ConfirmTankDefeat(s, true, true, false), Is.False, "Despawn is not death.");
            Assert.That(CampaignRules.ConfirmTankDefeat(s, false, true, true), Is.False, "Client cannot confirm death.");
            Assert.That(CampaignRules.ConfirmTankDefeat(s, true, true, true), Is.True);
            Assert.That(CampaignRules.CanUse(s, CampaignObjectiveId.FactoryCase), Is.EqualTo(CampaignResult.Accepted));
            Assert.That(CampaignRules.ConfirmTankDefeat(s, true, true, true), Is.False, "Duplicate callback.");

            var unstageable = new CampaignState { phase = CampaignPhase.Exploring, completed = completed, discoveredFiles = files,
                tankStage = CampaignTankStage.Pending, tankWaitReason = CampaignTankWaitReason.NoSafePosition, tankFallback = true };
            CampaignRules.SkipUnstageableTank(unstageable);
            Assert.That(unstageable.tankStage, Is.EqualTo(CampaignTankStage.Resolved));
            Assert.That(unstageable.tankWaitReason, Is.EqualTo(CampaignTankWaitReason.None));
            Assert.That(CampaignRules.ShouldArmTank(unstageable), Is.False, "A skipped encounter must not re-arm.");
            Assert.That(CampaignRules.CanUse(unstageable, CampaignObjectiveId.FactoryCase), Is.EqualTo(CampaignResult.Accepted),
                "A Tank that can never be staged must not lock the case.");
            Assert.That(CampaignRules.TankAllowsExit(unstageable), Is.True);
            Assert.That(unstageable.completed, Is.EqualTo(completed)); Assert.That(unstageable.discoveredFiles, Is.EqualTo(files));
            var asylum = new CampaignState { chapter = CampaignChapter.Asylum, phase = CampaignPhase.Preparing, tankStage = CampaignTankStage.Pending };
            CampaignRules.SkipUnstageableTank(asylum);
            Assert.That(asylum.factoryTankDefeated, Is.False);
            Assert.That(CampaignRules.TankAllowsExit(asylum), Is.True);
        }

        [Test]
        public void FactoryHintsHaveThreeCatalogTiersPauseAndResetOnFactsOrTarget()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring };
            var clock = new CampaignHintProgress();
            var definition = new CampaignObjectiveDefinition { id = CampaignObjectiveId.FactoryBackup, hint = "Read Utilities Procedure.",
                hint2 = "Reopen Utilities Procedure in Files.", hint3 = "Check the backup step in Utilities Procedure." };
            clock.Tick(state, definition.id, 44, false);
            Assert.That(CampaignRules.HintText(state, definition, clock.Seconds), Is.Empty);
            clock.Tick(state, definition.id, 1, false);
            Assert.That(CampaignRules.HintText(state, definition, clock.Seconds), Does.Contain("Requires: complete Power / Isolate").And.Contain(definition.hint));
            clock.Tick(state, definition.id, 100, true);
            Assert.That(clock.Seconds, Is.EqualTo(45));
            clock.Tick(state, definition.id, 45, false);
            Assert.That(CampaignRules.HintText(state, definition, clock.Seconds), Does.EndWith(definition.hint2));
            clock.Tick(state, definition.id, 45, false);
            Assert.That(CampaignRules.HintText(state, definition, clock.Seconds), Does.EndWith(definition.hint3));
            CampaignRules.RecordFile(state, CampaignFileId.FactoryManifest);
            clock.Tick(state, definition.id, 0, false); Assert.That(clock.Seconds, Is.Zero);
            clock.Tick(state, definition.id, 90, false);
            CampaignRules.TryComplete(state, CampaignObjectiveId.FactoryIsolate);
            clock.Tick(state, definition.id, 0, false); Assert.That(clock.Seconds, Is.Zero);
            clock.Tick(state, definition.id, 90, false);
            clock.Tick(state, CampaignObjectiveId.FactoryRoute, 0, false); Assert.That(clock.Seconds, Is.Zero);
            definition.id = CampaignObjectiveId.FactoryRoute;
            Assert.That(CampaignRules.HintText(state, definition, 135), Does.Contain("recover the evidence case"));
            // No distance or CanUse==Accepted input exists: locked and distant objectives retain hints.
        }

        [TestCase(0, 1, CampaignShippingGroup.ShipmentSeal)]
        [TestCase(1, 0, CampaignShippingGroup.DestinationReceivingTime)]
        [TestCase(0, 0, CampaignShippingGroup.ShipmentSeal | CampaignShippingGroup.DestinationReceivingTime)]
        public void FactoryShippingMismatchUsesSubmittedGroupsAndPreservesProgress(int first, int second, CampaignShippingGroup expected)
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring };
            CampaignRules.RecordFile(state, CampaignFileId.FactoryManifest);
            string before = JsonUtility.ToJson(state);
            Assert.That(CampaignRules.TryComplete(state, CampaignObjectiveId.FactoryShipping, first, second), Is.EqualTo(CampaignResult.WrongAnswer));
            var receipt = new CampaignObjectiveFeedback(CampaignObjectiveId.FactoryShipping, CampaignResult.WrongAnswer,
                state.revision, mismatch: CampaignRules.ShippingMismatch(first, second));
            first = second = 1; // Local choices may change while the server reply is in flight.
            Assert.That(receipt.MismatchedGroups, Is.EqualTo(expected));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
            Assert.That(CampaignRules.TryComplete(state, CampaignObjectiveId.FactoryShipping, first, second), Is.EqualTo(CampaignResult.Accepted));
        }

        [TestCase(-1, 1, CampaignShippingGroup.ShipmentSeal)]
        [TestCase(1, -1, CampaignShippingGroup.DestinationReceivingTime)]
        [TestCase(-1, -1, CampaignShippingGroup.ShipmentSeal | CampaignShippingGroup.DestinationReceivingTime)]
        public void FactoryShippingRequiresBothSelectionsAfterRecordedManifest(int first, int second, CampaignShippingGroup expected)
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring };
            Assert.That(CampaignRules.CanOpenShipping(state), Is.False);
            CampaignRules.RecordFile(state, CampaignFileId.FactoryManifest);
            Assert.That(CampaignRules.CanOpenShipping(state), Is.True);
            Assert.That(CampaignRules.ShippingMissing(first, second), Is.EqualTo(expected));
            Assert.That(CampaignRules.TryComplete(state, CampaignObjectiveId.FactoryShipping, first, second), Is.EqualTo(CampaignResult.MissingSelection));
            Assert.That(state.Has(CampaignObjectiveId.FactoryShipping), Is.False);
        }

        [Test]
        public void FactoryShippingPanelCannotOpenEarlyAndStartsUnselectedWithRecordedFacts()
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var root = new GameObject("Shipping panel fixture");
            var catalog = ScriptableObject.CreateInstance<CampaignContentCatalog>();
            var settings = ScriptableObject.CreateInstance<CampaignSettings>();
            try
            {
                var campaign = root.AddComponent<CampaignMissionController>();
                if (CampaignMissionController.Instance != campaign) typeof(CampaignMissionController).GetMethod("Awake", flags).Invoke(campaign, null);
                campaign.State.phase = CampaignPhase.Exploring;
                var existing = AssetDatabase.LoadAssetAtPath<CampaignContentCatalog>(CampaignContentAuthoring.CatalogPath);
                catalog.objectives = existing.objectives; catalog.files = existing.files;
                settings.contentCatalog = catalog; campaign.settings = settings;
                var hud = root.AddComponent<CampaignHUD>();
                if (CampaignHUD.Instance != hud) typeof(CampaignHUD).GetMethod("Awake", flags).Invoke(hud, null);
                var item = root.AddComponent<CampaignInteractable>(); item.objectiveId = CampaignObjectiveId.FactoryShipping;
                hud.Open(item);
                Assert.That(typeof(CampaignHUD).GetField("focused", flags).GetValue(hud), Is.Null);
                CampaignRules.RecordFile(campaign.State, CampaignFileId.FactoryManifest);
                hud.Open(item);
                Assert.That(typeof(CampaignHUD).GetField("focused", flags).GetValue(hud), Is.EqualTo(item));
                Assert.That(typeof(CampaignHUD).GetField("first", flags).GetValue(hud), Is.EqualTo(-1));
                Assert.That(typeof(CampaignHUD).GetField("second", flags).GetValue(hud), Is.EqualTo(-1));
                var body = (UnityEngine.UI.Text)typeof(CampaignHUD).GetField("body", flags).GetValue(hud);
                Assert.That(body.text, Does.Contain(catalog.FindFile(CampaignFileId.FactoryManifest).body));
                var controls = (Transform)typeof(CampaignHUD).GetField("controls", flags).GetValue(hud);
                // Runtime rerenders use deferred Destroy; this fixture remains in Edit Mode.
                foreach (var child in controls.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                Object.DestroyImmediate((GameObject)typeof(CampaignHUD).GetField("confirmation", flags).GetValue(hud));
                hud.ShowObjectiveResult(new(CampaignObjectiveId.FactoryShipping, CampaignResult.WrongAnswer, campaign.State.revision,
                    mismatch: CampaignShippingGroup.ShipmentSeal | CampaignShippingGroup.DestinationReceivingTime));
                Assert.That(controls.GetComponentsInChildren<UnityEngine.UI.Text>().Count(t => t.text.Contains("CHECK THIS GROUP")), Is.GreaterThanOrEqualTo(2));
                foreach (var child in controls.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                Object.DestroyImmediate((GameObject)typeof(CampaignHUD).GetField("confirmation", flags).GetValue(hud));
                hud.CloseDocument();
                Assert.That(typeof(CampaignHUD).GetField("shippingErrors", flags).GetValue(hud), Is.EqualTo(CampaignShippingGroup.None));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(settings); Object.DestroyImmediate(catalog); }
        }

        [TestCase(true)] [TestCase(false)]
        public void FactoryCompletionWaitsForStateAndDeliversOnceInEitherNetworkOrder(bool receiptFirst)
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring, revision = 3 };
            var buffer = new CampaignFeedbackBuffer();
            var receipt = new CampaignObjectiveFeedback(CampaignObjectiveId.FactoryIsolate, CampaignResult.Accepted, 5);
            int delivered = 0;
            void Deliver(CampaignObjectiveFeedback actual)
            {
                Assert.That(state.revision, Is.GreaterThanOrEqualTo(actual.Revision));
                Assert.That(state.Has(actual.Objective), Is.True);
                Assert.That(CampaignRules.CanUse(state, CampaignObjectiveId.FactoryBackup), Is.EqualTo(CampaignResult.Accepted));
                delivered++;
            }
            if (receiptFirst) { buffer.Add(receipt, state, Deliver); Assert.That(delivered, Is.Zero); }
            state.completed |= CampaignState.Bit(CampaignObjectiveId.FactoryIsolate); state.revision = 5;
            buffer.Apply(state, Deliver);
            buffer.Add(receipt, state, Deliver); buffer.Apply(state, Deliver);
            Assert.That(delivered, Is.EqualTo(1));
        }

        [Test]
        public void FactoryCompletionKeepsBothObjectivesAtOneRevisionAndNeverReplaysAfterRestoreOrLateJoin()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring, revision = 8,
                completed = CampaignState.Bit(CampaignObjectiveId.FactoryIsolate) | CampaignState.Bit(CampaignObjectiveId.FactoryShipping) };
            var receipts = new[] { new CampaignObjectiveFeedback(CampaignObjectiveId.FactoryIsolate, CampaignResult.Accepted, 8),
                new CampaignObjectiveFeedback(CampaignObjectiveId.FactoryShipping, CampaignResult.Accepted, 8) };
            var buffer = new CampaignFeedbackBuffer(); var seen = new List<CampaignObjectiveId>();
            foreach (var receipt in receipts) buffer.Add(receipt, state, r => seen.Add(r.Objective));
            Assert.That(seen, Is.EquivalentTo(new[] { CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryShipping }));
            buffer.Reset(8); foreach (var receipt in receipts) buffer.Add(receipt, state, r => seen.Add(r.Objective));
            Assert.That(seen.Count, Is.EqualTo(2), "Late join/restore baseline must not replay old cues.");
            var restored = CampaignRules.RestoreCheckpoint(new CampaignState { revision = 2 }, state.revision);
            Assert.That(restored.revision, Is.EqualTo(8));
            restored.revision++;
            Assert.That(restored.revision, Is.GreaterThan(state.revision));
        }

        [Test]
        public void FactoryCompletionForOtherBranchDoesNotPullTrackingBackToCompletedStep()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring };
            var ids = new[] { CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryBackup, CampaignObjectiveId.FactoryGenerator,
                CampaignObjectiveId.FactoryManifest, CampaignObjectiveId.FactoryShipping };
            CampaignRules.RecordFile(state, CampaignFileId.FactoryManifest);
            CampaignRules.TryComplete(state, CampaignObjectiveId.FactoryShipping, 1, 1);
            var tracked = CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.FactoryIsolate, _ => 1);
            Assert.That(tracked, Is.EqualTo(CampaignObjectiveId.FactoryIsolate));
            CampaignRules.TryComplete(state, CampaignObjectiveId.FactoryIsolate);
            CampaignRules.TryComplete(state, CampaignObjectiveId.FactoryBackup);
            CampaignRules.TryComplete(state, CampaignObjectiveId.FactoryGenerator);
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, tracked, _ => 1), Is.EqualTo(CampaignObjectiveId.None));
            Assert.That(CampaignRules.ShouldArmTank(state), Is.True);
        }

        [Test]
        public void FactorySceneMeasuresBodiesAndNavMeshWithoutChangingAuthoredObjects()
        {
            Assert.That(EditorApplication.ExecuteMenuItem("Tools/FPS/UI/Save Authored Scenes"), Is.True);
            var scene = SceneManager.GetSceneByPath("Assets/FPS/Scenes/GameScene.unity");
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene("Assets/FPS/Scenes/GameScene.unity", OpenSceneMode.Additive);
            try
            {
                Assert.That(scene.isDirty, Is.False);
                var factory = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CampaignChapterRoot>(true))
                    .Single(root => root.chapter == CampaignChapter.Factory);
                var objectives = factory.objectives.Where(o => o != null && !o.documentOnly).ToDictionary(o => o.objectiveId);
                var report = new System.Text.StringBuilder("# B2 — Đo body/NavMesh trong Edit Mode\n\nKhông Play Mode, không di chuyển object/cửa; route theo trạng thái authored đang tải. [SUY LUẬN]\n\n");
                string Identity(Transform t) => t.parent == null ? t.name : Identity(t.parent) + "/" + t.name;
                foreach (var entry in objectives)
                {
                    var item = entry.Value;
                    Assert.That(item.interactionBody, Is.Not.Null, entry.Key.ToString());
                    Assert.That(item.TargetPosition, Is.EqualTo(item.interactionBody.bounds.center));
                    report.AppendLine($"- {entry.Key}: object={Identity(item.transform)}, entity={item.GetEntityId()}, body={Identity(item.interactionBody.transform)}, objective={item.transform.position.ToString("F3")}, bodyCenter={item.interactionBody.bounds.center.ToString("F3")}, deltaObjectiveBody={Vector3.Distance(item.transform.position, item.interactionBody.bounds.center):F3} m, deltaTargetBody={Vector3.Distance(item.TargetPosition, item.interactionBody.bounds.center):F3} m. [SCENE Assets/FPS/Scenes/GameScene.unity / {Identity(item.transform)}/interactionBody]");
                }
                var pairs = new List<(CampaignObjectiveId from, CampaignObjectiveId to)>
                {
                    (CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryBackup), (CampaignObjectiveId.FactoryBackup, CampaignObjectiveId.FactoryGenerator),
                    (CampaignObjectiveId.FactoryManifest, CampaignObjectiveId.FactoryShipping), (CampaignObjectiveId.FactoryGenerator, CampaignObjectiveId.FactoryCase),
                    (CampaignObjectiveId.FactoryShipping, CampaignObjectiveId.FactoryCase), (CampaignObjectiveId.FactoryCase, CampaignObjectiveId.FactoryRoute)
                };
                foreach (var power in new[] { CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryBackup, CampaignObjectiveId.FactoryGenerator })
                    foreach (var logistics in new[] { CampaignObjectiveId.FactoryManifest, CampaignObjectiveId.FactoryShipping })
                    { pairs.Add((power, logistics)); pairs.Add((logistics, power)); }
                foreach (var pair in pairs)
                {
                    var from = objectives[pair.from].TargetPosition;
                    var to = objectives[pair.to].TargetPosition;
                    bool sampledFrom = NavMesh.SamplePosition(from, out var start, 2f, NavMesh.AllAreas);
                    bool sampledTo = NavMesh.SamplePosition(to, out var end, 2f, NavMesh.AllAreas);
                    var path = new NavMeshPath();
                    bool calculated = sampledFrom && sampledTo && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path);
                    float meters = 0;
                    for (int i = 1; i < path.corners.Length; i++) meters += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                    string status = calculated ? path.status.ToString() : "PathInvalid";
                    report.AppendLine($"- {pair.from} → {pair.to}: sampleFrom={sampledFrom} {start.position.ToString("F3")}; sampleTo={sampledTo} {end.position.ToString("F3")}; status={status}; pathSegmentMeters={meters:F3}; walkingMeters={(calculated && path.status == NavMeshPathStatus.PathComplete ? meters.ToString("F3") : "KHÔNG TÌM THẤY route hoàn chỉnh")}; straightMeters={Vector3.Distance(from, to):F3}. [SCENE Assets/FPS/Scenes/GameScene.unity / NavMesh/{pair.from}→{pair.to}]");
                }
                foreach (var door in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CampaignDoor>(true)))
                    report.AppendLine($"- Door {Identity(door.transform)}: rule={door.rule}, IsOpen={door.IsOpen}, IsClosed={door.IsClosed}, blockerEnabled={door.navigationBlocker != null && door.navigationBlocker.enabled}. [SCENE Assets/FPS/Scenes/GameScene.unity / {Identity(door.transform)}]");
                Directory.CreateDirectory("Reports/FactoryImplementationEvidence");
                File.WriteAllText("Reports/FactoryImplementationEvidence/B2-scene-measurements.md", report.ToString());
                Assert.That(scene.isDirty, Is.False, "Measurement must not author scene changes.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [TestCase(false)] [TestCase(true)]
        public void FactoryBranchesCompleteInEitherOrderAndResumeRecordedBranch(bool logisticsFirst)
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring };
            var ids = new[] { CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryBackup, CampaignObjectiveId.FactoryGenerator,
                CampaignObjectiveId.FactoryManifest, CampaignObjectiveId.FactoryShipping, CampaignObjectiveId.FactoryCase };
            Assert.That(CampaignObjectiveSelection.CycleFactoryBranch(state, CampaignObjectiveId.FactoryBackup), Is.EqualTo(CampaignObjectiveId.FactoryManifest));
            Assert.That(CampaignRules.CanSwitchFactoryBranch(state), Is.True);
            for (int branch = 0; branch < 2; branch++)
            {
                bool logistics = branch == 0 ? logisticsFirst : !logisticsFirst;
                if (logistics)
                {
                    CampaignRules.RecordFile(state, CampaignFileId.FactoryManifest);
                    Assert.That(CampaignRules.IsObjectiveComplete(state, CampaignObjectiveId.FactoryManifest), Is.True);
                    Assert.That(CampaignRules.FactoryProgress(state, CampaignFactoryBranch.Logistics), Is.EqualTo(1));
                    CampaignRules.RecordFile(state, CampaignFileId.FactoryManifest);
                    Assert.That(CampaignRules.FactoryProgress(state, CampaignFactoryBranch.Logistics), Is.EqualTo(1));
                    Assert.That(CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.None, _ => 1), Is.EqualTo(CampaignObjectiveId.FactoryShipping));
                    Assert.That(CampaignRules.TryComplete(state, CampaignObjectiveId.FactoryShipping, 1, 1), Is.EqualTo(CampaignResult.Accepted));
                }
                else
                    foreach (var id in ids.Take(3)) Assert.That(CampaignRules.TryComplete(state, id), Is.EqualTo(CampaignResult.Accepted));
                Assert.That(CampaignRules.CanUse(state, CampaignObjectiveId.FactoryCase), Is.EqualTo(CampaignResult.Prerequisite));
            }
            Assert.That(CampaignRules.FactoryProgress(state, CampaignFactoryBranch.Power), Is.EqualTo(3));
            Assert.That(CampaignRules.FactoryProgress(state, CampaignFactoryBranch.Logistics), Is.EqualTo(2));
            Assert.That(CampaignRules.CanSwitchFactoryBranch(state), Is.False);
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.FactoryShipping, _ => 1), Is.EqualTo(CampaignObjectiveId.None));
        }

        [Test]
        public void FactorySelectionKeepsManualChoiceThenContinuesAndUsesStableNearestFallback()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring };
            var ids = new[] { CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryBackup, CampaignObjectiveId.FactoryManifest, CampaignObjectiveId.FactoryShipping };
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.None, _ => 1), Is.EqualTo(CampaignObjectiveId.FactoryIsolate));
            CampaignRules.RecordFile(state, CampaignFileId.FactoryManifest);
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.FactoryIsolate, _ => 0), Is.EqualTo(CampaignObjectiveId.FactoryIsolate));
            CampaignRules.TryComplete(state, CampaignObjectiveId.FactoryIsolate);
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.FactoryIsolate, _ => 0), Is.EqualTo(CampaignObjectiveId.FactoryBackup));
            Assert.That(CampaignObjectiveSelection.CycleFactoryBranch(state, CampaignObjectiveId.FactoryBackup), Is.EqualTo(CampaignObjectiveId.FactoryShipping));
            Assert.That(CampaignRules.FactoryProgress(state, CampaignFactoryBranch.Power), Is.EqualTo(1));
            Assert.That(CampaignRules.FactoryProgress(state, CampaignFactoryBranch.Logistics), Is.EqualTo(1));
        }

        private static CampaignCheckpoint FactoryCheckpoint() => new()
        {
            state = new CampaignState { phase = CampaignPhase.Exploring },
            players = new List<CampaignPlayerSave> { new() { playerId = 1, first = new(), second = new() } }
        };

        [Test]
        public void FactoryTimeoutCheckpointCapturesDownedWithoutBlockingSaveOrChangingLivePlayer()
        {
            Assert.That(CampaignRules.EvaluatePartyGate(true, 2, 1, 1, 1, 1, false, true, 0).canCountdown, Is.True);
            var live = PlayerRuntimeSnapshot.CreateDefault(new SessionPlayerId(2), Vector3.zero, Quaternion.identity);
            live.lifeState = PlayerLifeState.Downed; live.lifeStateDeadline = 123; live.health = 0;
            var checkpoint = FactoryCheckpoint();
            checkpoint.players.Add(CampaignPlayerSave.Capture(live));
            string directory = Path.Combine(Path.GetTempPath(), "FactoryDownedCheckpoint-" + System.Guid.NewGuid());
            string path = Path.Combine(directory, "checkpoint.json");
            try
            {
                checkpoint.Save(path);
                Assert.That(CampaignCheckpoint.TryLoad(path, out var loaded, out var error), Is.True, error);
                var saved = loaded.players.Single(p => p.playerId == 2);
                Assert.That(saved.lifeState, Is.EqualTo(PlayerLifeState.Dead));
                Assert.That(saved.lifeStateDeadline, Is.Zero);
                Assert.That(live.lifeState, Is.EqualTo(PlayerLifeState.Downed));
                Assert.That(live.lifeStateDeadline, Is.EqualTo(123));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Test]
        public void FactoryCheckpointRoundTripsBothBranchesAndFallsBackAfterInvalidMain()
        {
            string directory = Path.Combine(Path.GetTempPath(), "FactoryCheckpoint-" + System.Guid.NewGuid());
            string path = Path.Combine(directory, "checkpoint.json");
            try
            {
                var checkpoint = FactoryCheckpoint();
                foreach (var id in new[] { CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryBackup, CampaignObjectiveId.FactoryGenerator })
                {
                    CampaignRules.TryComplete(checkpoint.state, id);
                    if (id == CampaignObjectiveId.FactoryBackup) CampaignRules.RecordFile(checkpoint.state, CampaignFileId.FactoryManifest);
                    checkpoint.Save(path);
                    Assert.That(CampaignCheckpoint.TryLoad(path, out var loaded, out var error), Is.True, error);
                    Assert.That(loaded.state.completed, Is.EqualTo(checkpoint.state.completed));
                    Assert.That(loaded.state.discoveredFiles, Is.EqualTo(checkpoint.state.discoveredFiles));
                    Assert.That(loaded.state.lastFactoryBranch, Is.EqualTo(checkpoint.state.lastFactoryBranch));
                }
                CampaignRules.TryComplete(checkpoint.state, CampaignObjectiveId.FactoryShipping, 1, 1);
                checkpoint.Save(path); checkpoint.Save(path);
                File.WriteAllText(path, "{\"version\":999}");
                Assert.That(CampaignCheckpoint.TryLoad(path, out var fallback, out _), Is.True);
                Assert.That(fallback.state.completed, Is.EqualTo(checkpoint.state.completed));
                Assert.That(fallback.state.discoveredFiles, Is.EqualTo(checkpoint.state.discoveredFiles));
                File.Delete(path + ".previous");
                Assert.That(CampaignCheckpoint.TryLoad(path, out _, out _), Is.False);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Test]
        public void FactoryCheckpointCurrentSchemaRetainsConfirmedKill()
        {
            string directory = Path.Combine(Path.GetTempPath(), "FactoryKillCheckpoint-" + System.Guid.NewGuid());
            string path = Path.Combine(directory, "checkpoint.json");
            try
            {
                var checkpoint = FactoryCheckpoint();
                checkpoint.state.completed = CampaignState.Bit(CampaignObjectiveId.FactoryGenerator) | CampaignState.Bit(CampaignObjectiveId.FactoryShipping);
                checkpoint.state.discoveredFiles = CampaignState.FileBit(CampaignFileId.FactoryManifest);
                checkpoint.state.tankStage = CampaignTankStage.Active;
                Assert.That(CampaignRules.ConfirmTankDefeat(checkpoint.state, true, true, true), Is.True);
                checkpoint.Save(path);
                Assert.That(CampaignCheckpoint.TryLoad(path, out var loaded, out var error), Is.True, error);
                Assert.That(loaded.state.factoryTankDefeated, Is.True);
                Assert.That(CampaignRules.CanUse(loaded.state, CampaignObjectiveId.FactoryCase), Is.EqualTo(CampaignResult.Accepted));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [TestCase(1, false)] [TestCase(2, false)] [TestCase(2, true)] [TestCase(3, false)] [TestCase(3, true)]
        public void FactoryCheckpointMigratesLegacyWithoutInferringKill(int version, bool hasCase)
        {
            string directory = Path.Combine(Path.GetTempPath(), "FactoryMigration-" + System.Guid.NewGuid());
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "checkpoint.json");
            try
            {
                var old = FactoryCheckpoint(); old.version = version; old.state.version = version == 3 ? 2 : 1;
                old.state.completed = CampaignState.Bit(CampaignObjectiveId.FactoryGenerator) | CampaignState.Bit(CampaignObjectiveId.FactoryShipping);
                if (hasCase) old.state.completed |= CampaignState.Bit(CampaignObjectiveId.FactoryCase) | CampaignState.Bit(CampaignObjectiveId.FactoryRoute);
                old.state.discoveredFiles = CampaignState.FileBit(CampaignFileId.FactoryManifest);
                old.state.factoryTankDefeated = true;
                File.WriteAllText(path, JsonUtility.ToJson(old));
                Assert.That(CampaignCheckpoint.TryLoad(path, out var loaded, out var error), Is.True, error);
                Assert.That(loaded.version, Is.EqualTo(4)); Assert.That(loaded.state.version, Is.EqualTo(3));
                Assert.That(loaded.state.completed, Is.EqualTo(old.state.completed));
                Assert.That(loaded.state.discoveredFiles, Is.EqualTo(old.state.discoveredFiles));
                Assert.That(loaded.state.lastFactoryBranch, Is.EqualTo(CampaignFactoryBranch.None));
                Assert.That(loaded.state.factoryTankDefeated, Is.False, "Case history is retained without fabricating a kill.");
                Assert.That(CampaignRules.ShouldArmTank(loaded.state), Is.EqualTo(!hasCase));
                Assert.That(CampaignRules.TankAllowsExit(loaded.state), Is.EqualTo(hasCase));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void FactoryAvailabilityRejectsShippingBeforeManifestWithoutMutation()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring };
            string before = JsonUtility.ToJson(state);
            var availability = CampaignRules.Availability(state, CampaignObjectiveId.FactoryShipping);
            Assert.That(availability.Result, Is.EqualTo(CampaignRules.CanUse(state, CampaignObjectiveId.FactoryShipping)));
            Assert.That(availability.IsLocked, Is.True);
            Assert.That(availability.Reason, Is.EqualTo("read Delivery Manifest"));
            Assert.That(availability.RequiredObjectiveIds, Is.EqualTo(new[] { CampaignObjectiveId.FactoryManifest }));
            Assert.That(availability.RequiredFileId, Is.EqualTo(CampaignFileId.FactoryManifest));
            Assert.That(CampaignRules.TryComplete(state, CampaignObjectiveId.FactoryShipping, 1, 1), Is.EqualTo(CampaignResult.Prerequisite));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void FactoryCaseReportsOnlyMissingRequirements(int readyMask)
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring, factoryTankDefeated = (readyMask & 4) != 0 };
            if ((readyMask & 1) != 0) state.completed |= CampaignState.Bit(CampaignObjectiveId.FactoryGenerator);
            if ((readyMask & 2) != 0) state.completed |= CampaignState.Bit(CampaignObjectiveId.FactoryShipping);
            var availability = CampaignRules.Availability(state, CampaignObjectiveId.FactoryCase);
            Assert.That(availability.IsLocked, Is.EqualTo(readyMask != 7));
            Assert.That(availability.RequiredObjectiveIds.Contains(CampaignObjectiveId.FactoryGenerator), Is.EqualTo((readyMask & 1) == 0));
            Assert.That(availability.RequiredObjectiveIds.Contains(CampaignObjectiveId.FactoryShipping), Is.EqualTo((readyMask & 2) == 0));
            Assert.That(availability.RequiresTankDefeat, Is.EqualTo((readyMask & 4) == 0));
            Assert.That(availability.Reason.Contains("Power"), Is.EqualTo((readyMask & 1) == 0));
            Assert.That(availability.Reason.Contains("Logistics"), Is.EqualTo((readyMask & 2) == 0));
            Assert.That(availability.Reason.Contains("Tank"), Is.EqualTo((readyMask & 4) == 0));
        }

        [TestCase(CampaignObjectiveId.FactoryBackup, CampaignObjectiveId.FactoryIsolate)]
        [TestCase(CampaignObjectiveId.FactoryGenerator, CampaignObjectiveId.FactoryBackup)]
        [TestCase(CampaignObjectiveId.FactoryRoute, CampaignObjectiveId.FactoryCase)]
        public void FactoryAvailabilitySeparatesMissingPrerequisitesFromRecordedAndInvalidState(CampaignObjectiveId id, CampaignObjectiveId prerequisite)
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring };
            Assert.That(CampaignRules.Availability(state, id).RequiredObjectiveIds, Is.EqualTo(new[] { prerequisite }));
            state.completed |= CampaignState.Bit(prerequisite);
            Assert.That(CampaignRules.Availability(state, id).Result, Is.EqualTo(CampaignResult.Accepted));
            state.completed |= CampaignState.Bit(id);
            Assert.That(CampaignRules.Availability(state, id).IsLocked, Is.False);
            Assert.That(CampaignRules.Availability(state, id).RequiredObjectiveIds, Is.Empty);
            state = new CampaignState { phase = CampaignPhase.Preparing };
            Assert.That(CampaignRules.Availability(state, id).Reason, Is.EqualTo("return to exploration"));
            state.chapter = CampaignChapter.Asylum;
            Assert.That(CampaignRules.Availability(state, id).Result, Is.EqualTo(CampaignResult.WrongChapter));
            Assert.That(CampaignRules.Availability(state, (CampaignObjectiveId)255).Reason, Is.EqualTo("a valid objective"));
        }

        [Test]
        public void FactoryLockedProxyRemainsRaycastInspectableAndHasNoInteractPrompt()
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var root = new GameObject("Factory locked proxy fixture");
            try
            {
                var campaign = root.AddComponent<CampaignMissionController>();
                if (CampaignMissionController.Instance != campaign) typeof(CampaignMissionController).GetMethod("Awake", flags).Invoke(campaign, null);
                campaign.State.phase = CampaignPhase.Exploring;
                var target = root.AddComponent<CampaignInteractable>();
                target.objectiveId = CampaignObjectiveId.FactoryBackup;
                var body = new GameObject("Body", typeof(BoxCollider), typeof(CampaignInteractionProxy));
                body.transform.SetParent(root.transform);
                body.transform.position = new Vector3(1234, 1234, 1236);
                var proxy = body.GetComponent<CampaignInteractionProxy>();
                proxy.target = target;
                var cameraObject = new GameObject("View", typeof(Camera));
                cameraObject.transform.SetParent(root.transform);
                cameraObject.transform.position = new Vector3(1234, 1234, 1234);
                var interaction = root.AddComponent<InteractionManager>();
                typeof(InteractionManager).GetField("playerCamera", flags).SetValue(interaction, cameraObject.GetComponent<Camera>());
                Physics.SyncTransforms();
                typeof(InteractionManager).GetMethod("ScanForInteractable", flags).Invoke(interaction, null);
                Assert.That(typeof(InteractionManager).GetField("currentInteractable", flags).GetValue(interaction), Is.EqualTo(proxy));
                Assert.That(proxy.CanInspect, Is.True);
                Assert.That(proxy.CanInteract, Is.False);
                Assert.That(proxy.GetLockedText(), Does.Contain(CampaignRules.Availability(campaign.State, target.objectiveId).Reason));
                Assert.That(proxy.GetLockedText(), Does.Not.Contain("[F]"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReadingManifestUnlocksShippingAndRetargetsWithinLogistics()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring };
            Assert.That(CampaignRules.CanUse(state, CampaignObjectiveId.FactoryShipping), Is.EqualTo(CampaignResult.Prerequisite));
            state.discoveredFiles = CampaignState.FileBit(CampaignFileId.FactoryManifest);
            Assert.That(CampaignRules.CanUse(state, CampaignObjectiveId.FactoryManifest), Is.EqualTo(CampaignResult.AlreadyDone));
            Assert.That(CampaignObjectiveSelection.CanInspect(state, CampaignObjectiveId.FactoryManifest), Is.False);
            Assert.That(CampaignRules.CanUse(state, CampaignObjectiveId.FactoryShipping), Is.EqualTo(CampaignResult.Accepted));
            var ids = new[] { CampaignObjectiveId.FactoryIsolate, CampaignObjectiveId.FactoryManifest, CampaignObjectiveId.FactoryShipping };
            Assert.That(CampaignObjectiveSelection.Resolve(state, ids, CampaignObjectiveId.FactoryManifest,
                id => id == CampaignObjectiveId.FactoryIsolate ? 1 : 50), Is.EqualTo(CampaignObjectiveId.FactoryShipping));
            state.completed = CampaignState.Bit(CampaignObjectiveId.FactoryIsolate);
            Assert.That(CampaignObjectiveSelection.Resolve(state,
                new[] { CampaignObjectiveId.FactoryBackup, CampaignObjectiveId.FactoryShipping }, CampaignObjectiveId.FactoryIsolate,
                id => id == CampaignObjectiveId.FactoryShipping ? 1 : 50), Is.EqualTo(CampaignObjectiveId.FactoryBackup));
        }

        [Test]
        public void ScriptedTank_ArmsAtLockedBeatsAndOnlyFactoryRequiresKill()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring };
            Assert.That(CampaignRules.ShouldArmTank(state), Is.False);
            state.completed = CampaignState.Bit(CampaignObjectiveId.FactoryGenerator) | CampaignState.Bit(CampaignObjectiveId.FactoryShipping);
            Assert.That(CampaignRules.ShouldArmTank(state), Is.True);
            Assert.That(CampaignRules.CanUse(state, CampaignObjectiveId.FactoryCase), Is.EqualTo(CampaignResult.Prerequisite));
            foreach (var stage in new[] { CampaignTankStage.Pending, CampaignTankStage.Active })
            {
                state.tankStage = stage;
                Assert.That(CampaignRules.TankAllowsExit(state), Is.False);
                Assert.That(CampaignRules.TryComplete(state, CampaignObjectiveId.FactoryCase), Is.EqualTo(CampaignResult.Prerequisite));
            }
            state.factoryTankDefeated = true; state.tankStage = CampaignTankStage.Resolved;
            Assert.That(CampaignRules.TryComplete(state, CampaignObjectiveId.FactoryCase), Is.EqualTo(CampaignResult.Accepted));
            Assert.That(CampaignRules.TankAllowsExit(state), Is.True);

            state = new CampaignState { chapter = CampaignChapter.Asylum, phase = CampaignPhase.Exploring,
                completed = CampaignState.Bit(CampaignObjectiveId.AsylumPatient) };
            Assert.That(CampaignRules.ShouldArmTank(state), Is.False);
            state.completed |= CampaignState.Bit(CampaignObjectiveId.AsylumTransfer);
            Assert.That(CampaignRules.ShouldArmTank(state), Is.True);
            Assert.That(CampaignRules.TankAllowsExit(state), Is.False, "The encounter cannot silently be skipped before spawning.");
            state.tankStage = CampaignTankStage.Active;
            Assert.That(CampaignRules.TankAllowsExit(state), Is.True, "Asylum Tank may be evaded.");

            state = new CampaignState { chapter = CampaignChapter.Laboratory, phase = CampaignPhase.Preparing,
                completed = CampaignState.Bit(CampaignObjectiveId.LabTransmit) };
            Assert.That(CampaignRules.ShouldArmTank(state), Is.False);
            state.phase = CampaignPhase.Encounter;
            Assert.That(CampaignRules.ShouldArmTank(state), Is.True);
            state.tankStage = CampaignTankStage.Active;
            Assert.That(CampaignRules.TankAllowsExit(state), Is.True, "Lab Tank is pressure, not a kill objective.");
            state.tankStage = CampaignTankStage.Dormant; state.phase = CampaignPhase.Failed;
            Assert.That(CampaignRules.ShouldArmTank(state), Is.False);
        }

        [Test]
        public void Dialogue_HonorsOncePerAttemptCooldownAndStablePriority()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignContentCatalog>(CampaignContentAuthoring.CatalogPath);
            var history = new Dictionary<CampaignDialogueId, double>();
            var order = catalog.FindDialogue(CampaignDialogueId.ClientSuppressionOrder);
            var rejection = catalog.FindDialogue(CampaignDialogueId.MiraRejectsOrder);
            var warning = catalog.FindDialogue(CampaignDialogueId.TankWarning);
            Assert.That(CampaignDialogue.TryRecord(order, 100, history), Is.True);
            Assert.That(CampaignDialogue.TryRecord(order, 1000, history), Is.False);
            Assert.That(CampaignDialogue.TryRecord(warning, 100, history), Is.True);
            Assert.That(CampaignDialogue.TryRecord(warning, 100 + warning.cooldown - .01, history), Is.False);
            Assert.That(CampaignDialogue.TryRecord(warning, 100 + warning.cooldown, history), Is.True);
            history.Clear(); // New attempt; the restored state itself emits no historical events.
            Assert.That(CampaignDialogue.TryRecord(order, 2000, history), Is.True);
            Assert.That(CampaignDialogue.TryRecord(null, 2000, history), Is.False);
            Assert.That(CampaignDialogue.TryRecord(new CampaignDialogueDefinition { id = CampaignDialogueId.Completed }, 2000, history), Is.False);

            var queue = new List<CampaignDialogueDefinition> { warning, order, rejection };
            Assert.That(CampaignDialogue.TakeNext(queue), Is.SameAs(order));
            Assert.That(CampaignDialogue.TakeNext(queue), Is.SameAs(rejection), "Equal-priority client/Mira lines retain causal order.");
            Assert.That(CampaignDialogue.TakeNext(queue), Is.SameAs(warning));
            Assert.That(CampaignDialogue.TakeNext(queue), Is.Null);
            Assert.That(CampaignDialogue.Format(order), Does.StartWith("Anonymous Client: "));
            Assert.That(CampaignDialogue.Format(null), Is.Empty);
        }

        [Test]
        public void FileSources_OnlyRequiredCluesHaveConsoleFallback()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignContentCatalog>(CampaignContentAuthoring.CatalogPath);
            var root = new GameObject("Campaign source test");
            try
            {
                var source = root.AddComponent<CampaignInteractable>();
                source.objectiveId = CampaignObjectiveId.FactoryShipping;
                var manifest = catalog.FindFile(CampaignFileId.FactoryManifest);
                var optional = catalog.FindFile(CampaignFileId.OptionalFactory01);
                Assert.That(source.ProvidesFile(manifest, catalog), Is.True);
                Assert.That(source.ProvidesFile(optional, catalog), Is.False);
                Assert.That(source.ProvidesFile(manifest, null), Is.False);
                Assert.That(source.ProvidesFile(null, catalog), Is.False);
                source.fileId = optional.id;
                Assert.That(source.ProvidesFile(optional, catalog), Is.False, "A loose ID on a console cannot grant optional lore.");
                source.documentOnly = true;
                Assert.That(source.ProvidesFile(optional, catalog), Is.True);
                Assert.That(source.ProvidesFile(manifest, catalog), Is.False);

                foreach (var required in catalog.files.Where(file => file.required))
                    Assert.That(catalog.objectives.Any(objective => objective.file == required.id), Is.True, required.id.ToString());
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void FileVisibility_RecordingAndRollbackToggleOnlyPickupAndInteraction()
        {
            var console = new GameObject("Persistent clue console");
            try
            {
                var root = new GameObject("Document");
                root.transform.SetParent(console.transform);
                var source = root.AddComponent<CampaignInteractable>();
                source.documentOnly = true;
                source.interactionBody = root.AddComponent<BoxCollider>();
                var visual = new GameObject("Paper visual");
                visual.transform.SetParent(root.transform);
                source.pickupVisual = visual;
                source.SetRecorded(true);
                Assert.That(console.activeSelf && root.activeSelf, Is.True);
                Assert.That(visual.activeSelf || source.interactionBody.enabled, Is.False);
                source.SetRecorded(false);
                Assert.That(visual.activeSelf && source.interactionBody.enabled, Is.True);
                source.pickupVisual = console; // Misconfigured owner must never disappear.
                source.SetRecorded(true);
                Assert.That(console.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(console); }
        }

        [Test]
        public void FileDiscovery_RequiresBoundReachableSourceAndExploringPhase()
        {
            var state = new CampaignState { chapter = CampaignChapter.Factory, phase = CampaignPhase.Exploring };
            var file = new CampaignFileDefinition { id = CampaignFileId.FactoryProcedure, chapter = CampaignChapter.Factory };

            Assert.That(CampaignRules.CanDiscover(state, file, true, true, true), Is.EqualTo(CampaignResult.Accepted));
            Assert.That(CampaignRules.CanDiscover(state, file, true, false, true), Is.EqualTo(CampaignResult.Prerequisite));
            Assert.That(CampaignRules.CanDiscover(state, file, true, true, false), Is.EqualTo(CampaignResult.OutOfRange));
            Assert.That(CampaignRules.CanDiscover(state, file, false, true, true), Is.EqualTo(CampaignResult.InvalidActor));
        }

        [Test]
        public void FileDiscovery_IsIdempotent()
        {
            var state = new CampaignState { chapter = CampaignChapter.Factory, phase = CampaignPhase.Exploring };
            var file = new CampaignFileDefinition { id = CampaignFileId.FactoryProcedure, chapter = CampaignChapter.Factory };
            state.discoveredFiles = CampaignState.FileBit(file.id);

            Assert.That(CampaignRules.CanDiscover(state, file, true, true, true), Is.EqualTo(CampaignResult.AlreadyDone));
        }

        [Test]
        public void KeyItem_IsOwnedOnlyBetweenAcquireAndConsumeUnlessPersistent()
        {
            var item = new CampaignKeyItemDefinition
            {
                acquiredAfter = CampaignObjectiveId.AsylumFuse,
                consumedAfter = CampaignObjectiveId.AsylumInstall,
                persistAfterUse = false
            };
            var state = new CampaignState();
            Assert.That(item.IsOwned(state), Is.False);
            state.completed = CampaignState.Bit(CampaignObjectiveId.AsylumFuse);
            Assert.That(item.IsOwned(state), Is.True);
            state.completed |= CampaignState.Bit(CampaignObjectiveId.AsylumInstall);
            Assert.That(item.IsOwned(state), Is.False);
            item.persistAfterUse = true;
            Assert.That(item.IsOwned(state), Is.True);
        }

        [Test]
        public void ContentCatalog_ContainsLockedFileAndPuzzleTruth()
        {
            CampaignContentCatalog catalog = AssetDatabase.LoadAssetAtPath<CampaignContentCatalog>(
                "Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.files, Has.Length.EqualTo(21));
            Assert.That(System.Array.FindAll(catalog.files, file => file != null && file.required), Has.Length.EqualTo(9));

            CampaignObjectiveDefinition shipping = catalog.FindObjective(CampaignObjectiveId.FactoryShipping);
            Assert.That(shipping.choices[1], Is.EqualTo("AL-04 / K6"));
            Assert.That(shipping.secondaryChoices[1], Is.EqualTo("NORTHBRIDGE / 02:40"));

            CampaignObjectiveDefinition patient = catalog.FindObjective(CampaignObjectiveId.AsylumPatient);
            Assert.That(patient.choices[1], Does.Contain("P046 / C12 / 02:40"));
            CampaignObjectiveDefinition transfer = catalog.FindObjective(CampaignObjectiveId.AsylumTransfer);
            Assert.That(transfer.choices[0], Does.StartWith("CONTRADICTION"));
            CampaignObjectiveDefinition archive = catalog.FindObjective(CampaignObjectiveId.LabArchive);
            Assert.That(archive.choices[1], Does.StartWith("E-02"));
        }

        [Test]
        public void EnglishCanon_HasCompleteFilesAndMatchesRuntimeCatalog()
        {
            string source = File.ReadAllText(CampaignContentAuthoring.CanonPath);
            Assert.That(source, Does.Not.Contain("An Lac").And.Not.Contain("AN LAC"));
            var parsed = CampaignContentAuthoring.Parse(source);
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignContentCatalog>(CampaignContentAuthoring.CatalogPath);
            Assert.That(JsonUtility.ToJson(catalog), Does.Not.Contain("An Lac").And.Not.Contain("AN LAC"));
            Assert.That(catalog.FindFile(CampaignFileId.FactoryManifest).title, Is.EqualTo("Delivery Manifest"));
            Assert.That(catalog.FindDialogue(CampaignDialogueId.FactoryGenerator).english,
                Is.EqualTo("Cold-store power is restored. Keep the evidence case sealed."));
            Assert.That(catalog.FindDialogue(CampaignDialogueId.Boarding).english,
                Is.EqualTo("The route is ready. Enter the boarding zone and wait for the departure countdown."));
            foreach (var file in parsed.files)
            {
                var runtime = catalog.FindFile(file.id);
                Assert.That(runtime.body, Is.EqualTo(file.body), file.id.ToString());
                Assert.That(runtime.bodyVietnamese, Is.Not.Empty, file.id.ToString());
                Assert.That(runtime.icon, Is.Not.Null, file.id.ToString());
                Assert.That(CampaignState.FileBit(file.id), Is.Not.Zero);
                if (!file.required) Assert.That(runtime.autoDiscoverAfter, Is.EqualTo(CampaignObjectiveId.None));
            }
            foreach (var objective in parsed.objectives)
            {
                var runtime = catalog.FindObjective(objective.id);
                Assert.That(runtime.instructions, Is.EqualTo(objective.instructions), objective.id.ToString());
                Assert.That(runtime.choices, Is.EqualTo(objective.choices), objective.id.ToString());
                Assert.That(runtime.secondaryChoices, Is.EqualTo(objective.secondaryChoices), objective.id.ToString());
                Assert.That(runtime.hint, Is.EqualTo(objective.hint), objective.id.ToString());
                Assert.That(runtime.hint2 ?? "", Is.EqualTo(objective.hint2), objective.id.ToString());
                Assert.That(runtime.hint3 ?? "", Is.EqualTo(objective.hint3), objective.id.ToString());
            }
            foreach (var line in parsed.dialogue)
                Assert.That(catalog.FindDialogue(line.id).english, Is.EqualTo(line.english), line.id.ToString());
            Assert.Throws<InvalidDataException>(() => CampaignContentAuthoring.Parse(source.Replace("### FILE_02", "### FILE_01")));
            Assert.Throws<InvalidDataException>(() => CampaignContentAuthoring.Parse("# Empty manuscript"));
        }

        [Test]
        public void FileDiscovery_RejectsNullUnknownWrongChapterAndWrongPhase()
        {
            var state = new CampaignState { phase = CampaignPhase.Exploring };
            var file = new CampaignFileDefinition { id = CampaignFileId.FactoryProcedure };
            Assert.That(CampaignRules.CanDiscover(null, file, true, true, true), Is.EqualTo(CampaignResult.Prerequisite));
            Assert.That(CampaignRules.CanDiscover(state, null, true, true, true), Is.EqualTo(CampaignResult.Prerequisite));
            file.id = (CampaignFileId)255;
            Assert.That(CampaignRules.CanDiscover(state, file, true, true, true), Is.EqualTo(CampaignResult.Prerequisite));
            Assert.That(CampaignState.FileBit(file.id), Is.Zero);
            file.id = CampaignFileId.FactoryProcedure; state.chapter = CampaignChapter.Asylum;
            Assert.That(CampaignRules.CanDiscover(state, file, true, true, true), Is.EqualTo(CampaignResult.WrongChapter));
            state.chapter = CampaignChapter.Factory; state.phase = CampaignPhase.Encounter;
            Assert.That(CampaignRules.CanDiscover(state, file, true, true, true), Is.EqualTo(CampaignResult.WrongPhase));
        }

        [Test]
        public void PuzzleChoices_MatchAcceptedIndicesAndWrongAnswerDoesNotMutateState()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignContentCatalog>(CampaignContentAuthoring.CatalogPath);
            foreach (var answer in new[] { (CampaignObjectiveId.FactoryShipping, 1, 1), (CampaignObjectiveId.AsylumPatient, 1, 0),
                (CampaignObjectiveId.AsylumTransfer, 0, 0), (CampaignObjectiveId.LabArchive, 1, 0) })
            {
                var state = new CampaignState { chapter = CampaignRules.ChapterOf(answer.Item1), phase = CampaignPhase.Exploring,
                    completed = ulong.MaxValue & ~CampaignState.Bit(answer.Item1) };
                state.discoveredFiles = CampaignState.FileBit(CampaignRules.ComparisonSource(answer.Item1))
                    | CampaignState.FileBit(CampaignFileId.AsylumDeskLog) | CampaignState.FileBit(CampaignFileId.LabIndex);
                var objective = catalog.FindObjective(answer.Item1);
                Assert.That(objective.choices.Length, Is.GreaterThan(answer.Item2));
                string before = JsonUtility.ToJson(state);
                Assert.That(CampaignRules.TryComplete(state, answer.Item1, 99, 99), Is.EqualTo(CampaignResult.WrongAnswer));
                Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
                Assert.That(CampaignRules.TryComplete(state, answer.Item1, answer.Item2, answer.Item3), Is.EqualTo(CampaignResult.Accepted));
            }
            Assert.That(CampaignRules.IsPowerAnswer(13), Is.True);
            Assert.That(CampaignRules.Load(13), Is.EqualTo(6));
        }
    }
}
