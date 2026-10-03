using NUnit.Framework;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace FPS.Tests
{
    public sealed class CampaignRulesTests
    {
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
            Assert.That(shipping.secondaryChoices[1], Is.EqualTo("AN LAC / 02:40"));

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
            var parsed = CampaignContentAuthoring.Parse(source);
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignContentCatalog>(CampaignContentAuthoring.CatalogPath);
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
