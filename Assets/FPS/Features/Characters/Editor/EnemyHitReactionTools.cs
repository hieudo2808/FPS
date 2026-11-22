using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FPS.EditorTools
{
    /// <summary>
    /// One-click, idempotent setup for enemy hit reactions: brings the Unity Particle Pack blood spray
    /// into the project's own content folder (GUIDs preserved) and puts a presenter on every enemy prefab.
    /// </summary>
    public static class EnemyHitReactionTools
    {
        private const string SourceFolder = "Assets/ThirdParty/UnityTechnologies/EffectExamples/Blood";
        private const string SourcePrefab = SourceFolder + "/Prefabs/BloodSprayEffect.prefab";
        private const string TargetFolder = "Assets/FPS/Features/Characters/Content/Effects/Blood";
        private const string TargetPrefab = TargetFolder + "/Prefabs/BloodSprayEffect.prefab";

        [MenuItem("Tools/FPS/Hit Reaction/1 Move Blood Spray Into Project")]
        public static void MoveBlood()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefab) != null)
            {
                Debug.Log("[HitReaction] Blood spray already at " + TargetPrefab);
                return;
            }
            string[] dependencies = AssetDatabase.GetDependencies(SourcePrefab, true)
                .Where(path => path.StartsWith(SourceFolder + "/")).ToArray();
            var log = new StringBuilder("[HitReaction] Moved:");
            foreach (string path in dependencies)
            {
                string relative = path.Substring(SourceFolder.Length + 1);
                string target = TargetFolder + "/" + relative;
                EnsureFolder(Path.GetDirectoryName(target).Replace('\\', '/'));
                string error = AssetDatabase.MoveAsset(path, target);
                if (!string.IsNullOrEmpty(error)) { Debug.LogError($"[HitReaction] {path} -> {target}: {error}"); return; }
                log.Append(Environment.NewLine).Append(path).Append(" -> ").Append(target);
            }
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        [MenuItem("Tools/FPS/Hit Reaction/2 Add Presenter To Enemy Prefabs")]
        public static void AddPresenters()
        {
            var blood = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefab);
            if (blood == null) { Debug.LogError("[HitReaction] Run step 1 first: " + TargetPrefab + " is missing."); return; }
            var log = new StringBuilder("[HitReaction] Presenter:");
            foreach (string path in EnemyPrefabPaths())
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var presenter = root.GetComponent<EnemyHitReactionPresenter>();
                    bool added = presenter == null;
                    if (added) presenter = root.AddComponent<EnemyHitReactionPresenter>();
                    var serialized = new SerializedObject(presenter);
                    serialized.FindProperty("bloodPrefab").objectReferenceValue = blood;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    int segments = root.GetComponentsInChildren<HitboxSegment>(true).Length;
                    log.Append(Environment.NewLine).Append(added ? "added " : "updated ").Append(path)
                        .Append(" segments=").Append(segments);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        [MenuItem("Tools/FPS/Hit Reaction/Report Enemy Rigs")]
        public static void Report()
        {
            var log = new StringBuilder("[HitReaction] Rigs:");
            foreach (string path in EnemyPrefabPaths())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var animator = prefab.GetComponentInChildren<Animator>();
                var skinned = new HashSet<Transform>(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.bones).Where(b => b != null));
                log.Append(Environment.NewLine).Append(path)
                    .Append(" controller=").Append(animator != null && animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "-")
                    .Append(" culling=").Append(animator != null ? animator.cullingMode.ToString() : "-")
                    .Append(" presenter=").Append(prefab.GetComponent<EnemyHitReactionPresenter>() != null);
                foreach (var segment in prefab.GetComponentsInChildren<HitboxSegment>(true))
                {
                    Transform bone = segment.transform;
                    int up = 0;
                    while (bone != null && !skinned.Contains(bone) && up < 4) { bone = bone.parent; up++; }
                    log.Append(Environment.NewLine).Append("   ").Append(segment.Zone).Append(" '").Append(segment.name)
                        .Append("' bone=").Append(bone != null ? bone.name + (up > 0 ? $" (+{up} up)" : "") : "NONE");
                }
            }
            Debug.Log(log.ToString());
        }

        /// <summary>Base enemy prefabs (variants inherit the component from their base).</summary>
        private static IEnumerable<string> EnemyPrefabPaths()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/FPS" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<EnemyAI>() == null) continue;
                if (PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant)
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
                    if (source != null && source.GetComponent<EnemyAI>() != null) continue;
                }
                yield return path;
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
