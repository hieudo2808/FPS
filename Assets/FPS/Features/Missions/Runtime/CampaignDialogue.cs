using System;
using System.Collections.Generic;
using System.Text;

namespace FPS
{
    /// <summary>Catalog-backed text and deterministic selection rules; no second dialogue runner.</summary>
    public static class CampaignDialogue
    {
        private static string Catalog(CampaignDialogueId id)
        {
            CampaignDialogueDefinition line = CampaignMissionController.Instance?.Dialogue(id);
            return Format(line);
        }

        public static string Format(CampaignDialogueDefinition line) => line == null || string.IsNullOrWhiteSpace(line.english)
            ? string.Empty : string.IsNullOrWhiteSpace(line.speaker) ? line.english : line.speaker + ": " + line.english;

        public static bool TryRecord(CampaignDialogueDefinition line, double now, Dictionary<CampaignDialogueId, double> history)
        {
            if (line == null || line.id == CampaignDialogueId.None || string.IsNullOrWhiteSpace(line.english)) return false;
            if (history.TryGetValue(line.id, out double last) && (line.oncePerRun || now - last < line.cooldown)) return false;
            history[line.id] = now;
            return true;
        }

        public static CampaignDialogueDefinition TakeNext(List<CampaignDialogueDefinition> pending)
        {
            if (pending.Count == 0) return null;
            int best = 0;
            for (int i = 1; i < pending.Count; i++)
                if (pending[i].priority > pending[best].priority) best = i;
            var next = pending[best];
            pending.RemoveAt(best);
            return next;
        }

        public static CampaignDialogueId ForObjective(CampaignObjectiveId id) => id switch
        {
            CampaignObjectiveId.FactoryGenerator => CampaignDialogueId.FactoryGenerator,
            CampaignObjectiveId.FactoryShipping => CampaignDialogueId.FactoryShipping,
            CampaignObjectiveId.FactoryCase => CampaignDialogueId.FactoryCase,
            CampaignObjectiveId.AsylumAccess => CampaignDialogueId.AsylumAccess,
            CampaignObjectiveId.AsylumPower => CampaignDialogueId.AsylumPower,
            CampaignObjectiveId.AsylumPatient => CampaignDialogueId.AsylumPatient,
            CampaignObjectiveId.AsylumTransfer => CampaignDialogueId.AsylumTransfer,
            CampaignObjectiveId.LabPower => CampaignDialogueId.LabPower,
            CampaignObjectiveId.LabArchive => CampaignDialogueId.LabArchive,
            CampaignObjectiveId.LabCase => CampaignDialogueId.LabCase,
            _ => CampaignDialogueId.None
        };

        public static string InsertionBriefing => Catalog(CampaignDialogueId.InsertionBriefing);
        public static string ObjectiveLine(CampaignObjectiveId id) => Catalog(ForObjective(id));
        public static string ChapterLine(CampaignChapter chapter) => Catalog(chapter switch
        {
            CampaignChapter.Factory => CampaignDialogueId.FactoryChapter,
            CampaignChapter.Asylum => CampaignDialogueId.AsylumChapter,
            _ => CampaignDialogueId.LaboratoryChapter
        });

        public static string Journal(CampaignState state)
        {
            var text = new StringBuilder("\n\nRECORDED TRANSMISSIONS\n");
            text.AppendLine(InsertionBriefing);
            for (int i = 0; i <= (int)state.chapter; i++) text.AppendLine(ChapterLine((CampaignChapter)i));
            foreach (CampaignObjectiveId id in Enum.GetValues(typeof(CampaignObjectiveId)))
                if (state.Has(id))
                {
                    string line = ObjectiveLine(id);
                    if (line.Length > 0) text.AppendLine(line);
                }
            if (state.evidenceTransmitted)
                text.AppendLine(Catalog(CampaignDialogueId.EvidenceTransmitted));
            return text.ToString();
        }

        public static string AccessInstructions(CampaignState state, CampaignObjectiveId objective)
        {
            var text = new StringBuilder();
            void Step(CampaignObjectiveId id, string description) => text.AppendLine((state.Has(id) ? "[DONE] " : "[ ] ") + description);
            if (objective == CampaignObjectiveId.FactoryRoute)
            {
                Step(CampaignObjectiveId.FactoryGenerator, "Restore cold-store power: isolate, backup, generator.");
                Step(CampaignObjectiveId.FactoryShipping, "Verify the AL-04 manifest in logistics.");
                Step(CampaignObjectiveId.FactoryCase, "Recover the T9-17 evidence case.");
                Step(CampaignObjectiveId.FactoryRoute, "Activate the service-route control cabinet.");
                text.Append("Interlock cycle: 60 seconds. The squad must hold the staging zone for 5 seconds.");
            }
            else if (objective == CampaignObjectiveId.AsylumLift)
            {
                Step(CampaignObjectiveId.AsylumAccess, "Read the security desk log.");
                Step(CampaignObjectiveId.AsylumFuse, "Recover the service fuse from ground-floor storage.");
                Step(CampaignObjectiveId.AsylumPower, "Install the fuse and power the lower service panel.");
                Step(CampaignObjectiveId.AsylumPatient, "Confirm P046 in the upper records room.");
                Step(CampaignObjectiveId.AsylumTransfer, "Cross-check the mortuary transfer and take the B2 card.");
                Step(CampaignObjectiveId.AsylumLift, "Scan the card at the freight-lift panel.");
                text.Append("Lift cycle: 35 seconds. Hold the cabin for 5 seconds before descent.");
            }
            else text.Append("Complete the current objective in the team inventory.");
            return text.ToString();
        }
    }
}
