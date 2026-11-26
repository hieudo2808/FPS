using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using static FPS.CampaignRadioVoice;

namespace FPS.Tests
{
    public sealed class CampaignRadioTests
    {
        private const string Catalog = "Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset";
        private static CampaignContentCatalog Load() => AssetDatabase.LoadAssetAtPath<CampaignContentCatalog>(Catalog);

        [Test]
        public void CatalogHasAllScenesAndFitsBothLanguagesForEveryCast()
        {
            var catalog = Load();
            Assert.That(catalog.dialogue.Select(scene => (int)scene.id), Is.EquivalentTo(Enumerable.Range(1, 28)));
            Assert.That(catalog.dialogue.Where(scene => scene.soloCues.Length > 0).Select(scene => (int)scene.id),
                Is.EquivalentTo(new[] { 5, 8, 12, 15, 19, 24, 25, 26, 27, 28 }));
            Assert.That(catalog.dialogue.Sum(scene => scene.cues.Length + scene.soloCues.Length), Is.EqualTo(132));
            var operators = new[] { Mason, Doc, Vega, Raven };
            foreach (var scene in catalog.dialogue)
            foreach (var first in operators)
            foreach (var second in operators.Where(v => v != first).Prepend(None))
            foreach (var cue in scene.CuesFor(second))
            foreach (bool vietnamese in new[] { false, true })
            {
                var body = vietnamese ? cue.vietnamese : cue.english;
                Assert.That(body, Is.Not.Null.And.Not.Empty);
                string caption = cue.Format(first, second, vietnamese);
                Assert.That(caption, Does.Not.Contain("{").And.Not.Contain(";"));
                string[] rows = caption.Split('\n');
                Assert.That(rows.Length, Is.InRange(1, 2), caption);
                Assert.That(rows.Max(row => row.Length), Is.LessThanOrEqualTo(40), caption);
            }
        }

        [Test]
        public void FallbackCastKeepsAnAvailablePreferredReplyAndReplacesNamesInBodies()
        {
            var catalog = Load();
            var power = catalog.FindDialogue(CampaignDialogueId.FactoryGenerator);
            Assert.That(CampaignDialogue.Cast(power, new[] { Raven, Mason }), Is.EqualTo((Mason, Raven)));
            Assert.That(CampaignDialogue.Cast(power, new[] { Raven, Raven }), Is.EqualTo((Raven, None)));
            Assert.That(power.CuesFor(None)[0].Format(Raven, None), Does.StartWith("Raven: Command, cold store"));
            var records = catalog.FindDialogue(CampaignDialogueId.AsylumPower);
            Assert.That(records.CuesFor(None)[1].Format(Doc, None), Does.StartWith("Command: Watch the intake time"));
            var door = catalog.FindDialogue(CampaignDialogueId.FactoryEncounter);
            Assert.That(door.cues[0].Format(Doc, Raven), Does.StartWith("Doc: I hit the service release."));
            var down = catalog.FindDialogue(CampaignDialogueId.OperatorDowned);
            Assert.That(CampaignDialogue.Cast(down, new[] { Vega, Doc }, Doc), Is.EqualTo((Doc, Vega)));
            Assert.That(CampaignDialogue.Cast(down, new[] { Doc }, Doc), Is.EqualTo((Doc, None)));
        }

        [Test]
        public void AlertInterruptsStoryAndReviveVoidsOnlyTheSameOperatorsOldReply()
        {
            var catalog = Load();
            var radio = new CampaignRadioPlayback();
            void Add(CampaignDialogueId id, ulong subject, float now) => radio.Enqueue(catalog.FindDialogue(id), null,
                Doc, Mason, subject, CampaignChapter.Factory, now);
            string At(float now)
            {
                Assert.That(radio.Advance(now, CampaignChapter.Factory, CampaignPhase.Exploring, out var value, out _), Is.True);
                return value;
            }

            Add(CampaignDialogueId.FactoryGenerator, 0, 0);
            string interrupted = At(0);
            Add(CampaignDialogueId.OperatorDowned, 12, .1f);
            Assert.That(At(.1f), Does.StartWith("Doc: Vanguard, I'm down!"));
            Add(CampaignDialogueId.Revived, 12, 1);
            Assert.That(At(4), Is.EqualTo("Doc: Command, back on my feet."));
            Assert.That(At(12), Is.EqualTo(interrupted), "Expired alert replies must not carry into the resumed scene");
            radio.Clear();
            Assert.That(radio.Advance(12, CampaignChapter.Factory, CampaignPhase.Exploring, out _, out _), Is.False);
        }

        [Test]
        public void SuppressionAndRebuttalKeepOneCallAheadOfLaterStoryMessages()
        {
            var catalog = Load();
            var radio = new CampaignRadioPlayback();
            var suppression = catalog.FindDialogue(CampaignDialogueId.ClientSuppressionOrder);
            var rebuttal = catalog.FindDialogue(CampaignDialogueId.CommandRejectsOrder);
            var received = catalog.FindDialogue(CampaignDialogueId.EvidenceTransmitted);
            radio.Enqueue(suppression, rebuttal, Raven, None, 0, CampaignChapter.Laboratory, 0);
            radio.Enqueue(received, null, Raven, None, 0, CampaignChapter.Laboratory, 1);
            var played = new List<string>();
            for (int time = 0; time < 120; time += 5)
                if (radio.Advance(time, CampaignChapter.Laboratory, CampaignPhase.Encounter, out var text, out _) && text.Length > 0)
                    played.Add(text);
            Assert.That(played.Count, Is.EqualTo(12));
            Assert.That(played[3], Does.StartWith("Client: We track by lot number only."));
            Assert.That(played[4], Does.StartWith("Command: Vanguard, ignore that order."));
            Assert.That(played[8], Does.StartWith("Command: Vanguard, BRT station has"));
        }
    }
}
