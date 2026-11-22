using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

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

        // The field journal keeps its existing short transmission summaries.
        private static string Format(CampaignDialogueDefinition line) => line == null || string.IsNullOrWhiteSpace(line.english)
            ? string.Empty : string.IsNullOrWhiteSpace(line.speaker) ? line.english : line.speaker + ": " + line.english;

        public static bool TryRecord(CampaignDialogueDefinition line, double now, Dictionary<CampaignDialogueId, double> history)
        {
            if (line == null || line.id == CampaignDialogueId.None || line.cues is not { Length: > 0 }) return false;
            if (history.TryGetValue(line.id, out double last) && (line.oncePerRun || now - last < line.cooldown)) return false;
            history[line.id] = now;
            return true;
        }

        public static (CampaignRadioVoice first, CampaignRadioVoice second) Cast(CampaignDialogueDefinition scene,
            IReadOnlyList<CampaignRadioVoice> available, CampaignRadioVoice subject = CampaignRadioVoice.None)
        {
            var first = subject;
            if (first == CampaignRadioVoice.None)
            {
                first = available.Contains(scene.firstVoice) ? scene.firstVoice
                    : available.FirstOrDefault(voice => voice != scene.secondVoice);
                if (first == CampaignRadioVoice.None && available.Count > 0) first = available[0];
            }
            if (scene.secondVoice == CampaignRadioVoice.None) return (first, CampaignRadioVoice.None);
            var second = scene.secondVoice != first && available.Contains(scene.secondVoice) ? scene.secondVoice
                : available.FirstOrDefault(voice => voice != first);
            return (first, second);
        }

        public static bool IsAlert(CampaignDialogueId id) => id is CampaignDialogueId.OperatorDowned or CampaignDialogueId.Revived
            or CampaignDialogueId.TankWarning or CampaignDialogueId.ScreamerWarning or CampaignDialogueId.InfectorWarning;

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

        public static string InsertionBriefing
        {
            get
            {
                string text = Catalog(CampaignDialogueId.InsertionBriefing);
                return string.Join("\n", text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Take(2));
            }
        }
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
    }

    public sealed class CampaignRadioPlayback
    {
        private const float AlertLifetime = 8f;
        private readonly Queue<Call> story = new();
        private readonly Queue<Call> alerts = new();
        private string text = string.Empty;
        private string interrupted;
        private float until, remaining;
        private bool showingAlert;

        private sealed class Call
        {
            public readonly CampaignDialogueId id;
            public readonly ulong subject;
            public readonly CampaignChapter chapter;
            private readonly Queue<(string text, AudioClip clip)> cues = new();
            public bool HasStarted { get; private set; }
            public float expires;
            public int Count => cues.Count;

            public Call(CampaignDialogueDefinition scene, CampaignDialogueDefinition continuation,
                CampaignRadioVoice first, CampaignRadioVoice second, ulong subject, CampaignChapter chapter, float expires)
            {
                id = scene.id;
                this.subject = subject;
                this.chapter = chapter;
                this.expires = expires;
                Add(scene, first, second);
                if (continuation != null) Add(continuation, first, second);
            }

            private void Add(CampaignDialogueDefinition scene, CampaignRadioVoice first, CampaignRadioVoice second)
            {
                if (scene.CuesFor(second) is not { Length: > 0 } lines) return;
                foreach (var line in lines)
                    if (line != null && !string.IsNullOrWhiteSpace(line.english))
                        cues.Enqueue((line.Format(first, second), line == lines[0] ? scene.audio : null));
            }

            public (string text, AudioClip clip) Take()
            {
                HasStarted = true;
                return cues.Dequeue();
            }

            public bool IsOutdated(CampaignChapter currentChapter, CampaignPhase phase)
            {
                if (phase == CampaignPhase.Completed) return id != CampaignDialogueId.Completed;
                if (id == CampaignDialogueId.Boarding && phase == CampaignPhase.Transitioning && currentChapter == chapter) return true;
                // The spoken descent can outlast the six-second ride. Let it finish before B2's greeting.
                return (byte)chapter < (byte)currentChapter && !(HasStarted && id == CampaignDialogueId.AsylumTransition);
            }
        }

        public bool HasEnding => story.Any(call => call.id == CampaignDialogueId.Completed);

        public void Enqueue(CampaignDialogueDefinition scene, CampaignDialogueDefinition continuation,
            CampaignRadioVoice first, CampaignRadioVoice second, ulong subject, CampaignChapter chapter, float now)
        {
            bool alert = CampaignDialogue.IsAlert(scene.id);
            if (scene.id == CampaignDialogueId.Revived)
                foreach (var call in alerts)
                    if (call.id == CampaignDialogueId.OperatorDowned && call.subject == subject)
                        call.expires = now;
            var next = new Call(scene, continuation, first, second, subject, chapter, now + AlertLifetime);
            if (next.Count > 0) (alert ? alerts : story).Enqueue(next);
        }

        public bool Advance(float now, CampaignChapter chapter, CampaignPhase phase, out string caption, out AudioClip clip)
        {
            caption = string.Empty;
            clip = null;
            while (alerts.Count > 0 && (alerts.Peek().expires <= now || alerts.Peek().Count == 0))
                alerts.Dequeue();
            bool hasAlert = alerts.Count > 0;
            if (now < until)
            {
                if (showingAlert || !hasAlert) return false;
                interrupted = text;
                remaining = until - now;
            }

            while (story.Count > 0 && (story.Peek().IsOutdated(chapter, phase)
                || (interrupted == null && story.Peek().Count == 0)))
            {
                story.Dequeue();
                interrupted = null;
            }
            if (hasAlert) (caption, clip) = alerts.Peek().Take();
            else if (interrupted != null)
            {
                caption = text = interrupted;
                interrupted = null;
                showingAlert = false;
                until = now + Mathf.Max(1.2f, remaining);
                return true;
            }
            else
            {
                if (story.Count > 0) (caption, clip) = story.Peek().Take();
            }

            if (caption.Length == 0 && text.Length == 0) return false;
            showingAlert = hasAlert;
            text = caption;
            until = caption.Length == 0 ? now : now + Mathf.Max(1.6f, .25f + caption.Length / 19f, clip != null ? clip.length : 0f);
            return true;
        }

        public void Clear()
        {
            story.Clear(); alerts.Clear();
            text = string.Empty; interrupted = null; until = 0; showingAlert = false;
        }
    }
}
