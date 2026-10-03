#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FPS
{
    /// <summary>Imports the deliberately small, fixed Markdown format of the English campaign canon.</summary>
    public static class CampaignContentAuthoring
    {
        public const string CanonPath = "Documents/Outbreak-Protocol-Master-Canon.md";
        public const string CatalogPath = "Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset";

        [MenuItem("Tools/FPS/Campaign/Add Asylum Tank Anchors")]
        public static void AddAsylumTankAnchors()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author anchors only in Edit Mode.");
            var controller = UnityEngine.Object.FindFirstObjectByType<CampaignMissionController>();
            if (controller == null || controller.gameObject.scene.path != "Assets/FPS/Scenes/GameScene.unity"
                || controller.gameObject.scene.isDirty) throw new InvalidOperationException("Open a clean GameScene; this operation will not overwrite unsaved work.");
            var chapter = controller.chapters[(int)CampaignChapter.Asylum];
            var sources = new[] { "Finale_West_1", "Finale_East_1" }.Select(name => chapter.anchors.Single(a => a != null && a.name == name)).ToArray();
            var points = new Vector3[sources.Length];
            Physics.SyncTransforms();
            for (int i = 0; i < sources.Length; i++)
                if (!TryFindTankCandidate(sources[i], out points[i]))
                    throw new InvalidOperationException("Tank candidate no longer has clearance; run Audit Tank Arenas again.");
            Undo.RecordObject(chapter, "Add Asylum Tank anchors");
            var anchors = chapter.anchors.ToList();
            for (int i = 0; i < sources.Length; i++)
            {
                string name = i == 0 ? "Tank_WestArena" : "Tank_EastArena";
                if (anchors.Any(a => a != null && a.name == name)) continue;
                var root = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(root, "Add Asylum Tank anchor");
                root.transform.SetParent(sources[i].transform.parent, false);
                root.transform.position = points[i];
                var anchor = Undo.AddComponent<DirectorSpawnAnchor>(root);
                anchor.Configure(DirectorSpawnAnchorType.Special, sources[i].Zone);
                anchors.Add(anchor);
            }
            chapter.anchors = anchors.ToArray();
            EditorUtility.SetDirty(chapter);
            EditorSceneManager.MarkSceneDirty(chapter.gameObject.scene);
            Debug.Log("[CampaignTankAudit] Added/verified two Asylum Tank anchors. Scene is not saved; verify before saving.");
        }

        [MenuItem("Tools/FPS/Campaign/Audit Tank Arenas")]
        public static void AuditTankArenas()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<CampaignMissionController>();
            if (controller == null) throw new InvalidOperationException("Open GameScene before auditing Tank arenas.");
            Physics.SyncTransforms();
            foreach (var chapter in controller.chapters)
            {
                var anchors = chapter.anchors ?? Array.Empty<DirectorSpawnAnchor>();
                var usable = anchors.Where(anchor => anchor != null && anchor.isActiveAndEnabled
                    && DirectorSpawnService.HasTankArenaClearance(anchor.SpawnPosition)).ToArray();
                Debug.Log($"[CampaignTankAudit] {chapter.chapter}: {usable.Length}/{anchors.Length} anchors have static Tank clearance. "
                    + string.Join("; ", usable.Select(anchor => anchor.name + " " + anchor.SpawnPosition))
                    + ". Live distance/LOS/path validation is still required at spawn time.");
                var spawns = UnityEngine.Object.FindFirstObjectByType<DirectorSpawnService>();
                var targetId = chapter.chapter == CampaignChapter.Factory ? CampaignObjectiveId.FactoryCase
                    : chapter.chapter == CampaignChapter.Asylum ? CampaignObjectiveId.AsylumLift : CampaignObjectiveId.LabTransmit;
                var target = chapter.objectives.FirstOrDefault(o => o != null && o.objectiveId == targetId);
                if (spawns != null && target != null && UnityEngine.AI.NavMesh.SamplePosition(target.Point, out var targetFloor, 3, UnityEngine.AI.NavMesh.AllAreas))
                    Debug.Log($"[CampaignTankRoute] {chapter.chapter} to {targetId}: "
                        + usable.Count(a => spawns.HasCampaignSpecialRoute(a.SpawnPosition, targetFloor.position, true))
                        + $"/{usable.Length} capsule-clear routes. Closed doors and live player proximity still matter.");
                if (usable.Length == 0)
                {
                    foreach (var anchor in anchors.Where(a => a != null && a.Zone != null))
                    {
                        bool found = TryFindTankCandidate(anchor, out Vector3 candidate);
                        Debug.Log($"[CampaignTankCandidate] {chapter.chapter}/{anchor.name}: " + (found ? candidate.ToString() : "none within zone and 6m"));
                    }
                }
            }
        }

        private static bool TryFindTankCandidate(DirectorSpawnAnchor anchor, out Vector3 candidate)
        {
            for (int x = -6; x <= 6; x++)
                for (int z = -6; z <= 6; z++)
                {
                    var point = anchor.SpawnPosition + new Vector3(x, 0, z);
                    if (anchor.Zone.Contains(point) && DirectorSpawnService.HasTankArenaClearance(point))
                    { candidate = point; return true; }
                }
            candidate = default;
            return false;
        }

        public sealed class EnglishCanon
        {
            public CampaignFileDefinition[] files;
            public CampaignObjectiveDefinition[] objectives;
            public CampaignDialogueDefinition[] dialogue;
        }

        public static EnglishCanon Parse(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) throw new InvalidDataException("Canon is empty.");
            string text = markdown.Replace("\r\n", "\n");
            var result = new EnglishCanon
            {
                files = Regex.Matches(text, @"^### FILE_(\d+) · (.+) · (FACTORY|ASYLUM|LABORATORY) · (REQUIRED|OPTIONAL)\n\n([\s\S]*?)(?=\n## |\n### |\z)", RegexOptions.Multiline)
                    .Cast<Match>().Select(m => new CampaignFileDefinition
                    {
                        id = (CampaignFileId)byte.Parse(m.Groups[1].Value), title = m.Groups[2].Value,
                        chapter = Enum.Parse<CampaignChapter>(m.Groups[3].Value, true), required = m.Groups[4].Value == "REQUIRED",
                        body = m.Groups[5].Value.Trim()
                    }).ToArray(),
                objectives = Regex.Matches(text, @"^- \*\*(\d+) (.+?)\*\* — (.+?) Hint: (.+)$", RegexOptions.Multiline)
                    .Cast<Match>().Select(ParseObjective).ToArray(),
                dialogue = Regex.Matches(text, @"^- \*\*D(\d+) · (.+?) · (.+?) · P(\d+)\*\* — (.+)$", RegexOptions.Multiline)
                    .Cast<Match>().Select(m => new CampaignDialogueDefinition
                    {
                        id = (CampaignDialogueId)byte.Parse(m.Groups[1].Value), speaker = m.Groups[2].Value,
                        trigger = m.Groups[3].Value, priority = byte.Parse(m.Groups[4].Value), english = m.Groups[5].Value
                    }).ToArray()
            };
            RequireIds(result.files.Select(f => f.id), "Files");
            RequireIds(result.objectives.Select(o => o.id), "Objectives");
            RequireIds(result.dialogue.Select(d => d.id), "Dialogue");
            if (result.files.Count(f => f.required) != 9 || result.files.Length != 21
                || !new[] { 3, 4, 5 }.SequenceEqual(Enum.GetValues(typeof(CampaignChapter)).Cast<CampaignChapter>()
                    .Select(ch => result.files.Count(f => !f.required && f.chapter == ch))))
                throw new InvalidDataException("Expected 9 required files and 3/4/5 optional files.");
            foreach (var file in result.files)
            {
                int words = Regex.Matches(file.body, @"\S+").Count;
                if (words < 60 || words > 120) throw new InvalidDataException($"{file.id}: expected 60–120 English words, got {words}.");
            }
            if (result.dialogue.Any(d => d.priority > 2)) throw new InvalidDataException("Dialogue priorities are 2 critical, 1 warning, 0 bark.");
            return result;
        }

        private static CampaignObjectiveDefinition ParseObjective(Match match)
        {
            string tail = match.Groups[4].Value;
            string[] choices = Array.Empty<string>(), secondary = Array.Empty<string>();
            int at = tail.IndexOf(" Secondary: ", StringComparison.Ordinal);
            if (at >= 0) { secondary = SplitChoices(tail.Substring(at + 12)); tail = tail.Substring(0, at); }
            at = tail.IndexOf(" Choices: ", StringComparison.Ordinal);
            if (at >= 0) { choices = SplitChoices(tail.Substring(at + 10)); tail = tail.Substring(0, at); }
            return new CampaignObjectiveDefinition
            {
                id = (CampaignObjectiveId)byte.Parse(match.Groups[1].Value), title = match.Groups[2].Value,
                instructions = match.Groups[3].Value, hint = tail, choices = choices, secondaryChoices = secondary
            };
        }

        private static string[] SplitChoices(string text) => text.TrimEnd('.').Split(new[] { " | " }, StringSplitOptions.None);

        private static void RequireIds<T>(System.Collections.Generic.IEnumerable<T> values, string label) where T : Enum
        {
            var actual = values.Select(v => Convert.ToInt32(v)).OrderBy(v => v).ToArray();
            var expected = Enum.GetValues(typeof(T)).Cast<T>().Select(v => Convert.ToInt32(v)).Where(v => v != 0).OrderBy(v => v);
            if (!actual.SequenceEqual(expected)) throw new InvalidDataException(label + ": missing, duplicate or unknown stable ID.");
        }

        [MenuItem("Tools/FPS/Campaign/Import English Canon")]
        public static void ImportEnglishCanon()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Import canon only in Edit Mode.");
            var parsed = Parse(File.ReadAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, CanonPath)));
            var catalog = AssetDatabase.LoadAssetAtPath<CampaignContentCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidDataException("Campaign catalog is missing.");
            // Validate every target before editing: do not partially import a malformed manuscript.
            RequireIds(catalog.files.Select(f => f.id), "Catalog Files");
            RequireIds(catalog.objectives.Select(o => o.id), "Catalog Objectives");
            RequireIds(catalog.dialogue.Select(d => d.id), "Catalog Dialogue");
            Undo.RecordObject(catalog, "Import campaign English canon");
            foreach (var file in parsed.files)
            {
                var target = catalog.FindFile(file.id);
                target.title = file.title; target.body = file.body;
                target.summary = file.body; target.chapter = file.chapter; target.required = file.required;
            }
            foreach (var objective in parsed.objectives)
            {
                var target = catalog.FindObjective(objective.id);
                target.title = objective.title; target.instructions = objective.instructions; target.hint = objective.hint;
                target.choices = objective.choices; target.secondaryChoices = objective.secondaryChoices;
            }
            foreach (var line in parsed.dialogue)
            {
                var target = catalog.FindDialogue(line.id);
                target.speaker = line.speaker; target.english = line.english;
                target.priority = line.priority; target.trigger = line.trigger;
            }
            // Icons, clips, Vietnamese translations, objective links and stable IDs remain intact.
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            Debug.Log("Campaign canon imported: 21 files, 20 objectives, 28 dialogue lines. No scene saved.");
        }
    }
}
#endif
