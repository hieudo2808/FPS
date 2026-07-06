using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FPS.EditorTools
{
    public static class FPSHandBodyguardAutomation
    {
        private const string BodyguardPath = "Assets/Prefabs/BodyGuards/Meshes/SkelMesh_Bodyguard_02.fbx";
        private const string Body02HandPath = "Assets/Prefabs/BodyGuards/Meshes/Body02Hand.fbx";
        private const string FPSHandPath = "Assets/Art/Animations/AKAnimation/FPSHand.fbx";
        private const string GeneratedRoot = "Assets/Generated/FPSHandBodyguardPrototype";
        private const string Body02HandGeneratedRoot = "Assets/Generated/FPSHandBody02HandPrototype";

        [MenuItem("Tools/FPS/Prototype Bodyguard Hands On FPSHand Rig")]
        public static void Run()
        {
            RunInternal(
                BodyguardPath,
                GeneratedRoot,
                "SkelMesh_Bodyguard_02.copy.fbx",
                "BodyguardHands_On_FPSHandSkeleton.prefab",
                "BodyguardToFPSHandReport.md");
        }

        [MenuItem("Tools/FPS/Prototype Body02Hand On FPSHand Rig")]
        public static void RunBody02Hand()
        {
            RunInternal(
                Body02HandPath,
                Body02HandGeneratedRoot,
                "Body02Hand.copy.fbx",
                "Body02Hand_On_FPSHandSkeleton.prefab",
                "Body02HandToFPSHandReport.md");
        }

        private static void RunInternal(
            string sourceHandPath,
            string generatedRoot,
            string sourceCopyFileName,
            string prefabFileName,
            string reportFileName)
        {
            EnsureFolder("Assets", "Generated");
            EnsureFolder("Assets/Generated", Path.GetFileName(generatedRoot));
            EnsureFolder(generatedRoot, "SourceCopies");
            EnsureFolder(generatedRoot, "Meshes");

            string sourceCopiesRoot = generatedRoot + "/SourceCopies";
            string meshRoot = generatedRoot + "/Meshes";
            string prefabPath = generatedRoot + "/" + prefabFileName;
            string reportPath = generatedRoot + "/" + reportFileName;

            string bodyguardCopyPath = CopyAssetFresh(sourceHandPath, sourceCopiesRoot + "/" + sourceCopyFileName);
            string fpsHandCopyPath = CopyAssetFresh(FPSHandPath, sourceCopiesRoot + "/FPSHand.copy.fbx");
            AssetDatabase.ImportAsset(bodyguardCopyPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(fpsHandCopyPath, ImportAssetOptions.ForceUpdate);

            GameObject bodyguardAsset = AssetDatabase.LoadAssetAtPath<GameObject>(bodyguardCopyPath);
            GameObject fpsHandAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fpsHandCopyPath);

            if (bodyguardAsset == null || fpsHandAsset == null)
            {
                File.WriteAllText(reportPath, "Failed to load copied FBX assets.");
                AssetDatabase.Refresh();
                return;
            }

            GameObject bodyguardInstance = (GameObject)PrefabUtility.InstantiatePrefab(bodyguardAsset);
            GameObject fpsHandInstance = (GameObject)PrefabUtility.InstantiatePrefab(fpsHandAsset);
            bodyguardInstance.name = "Bodyguard_Source_Analysis";
            fpsHandInstance.name = "FPSHand_Target_Prototype";

            try
            {
                var bodyBonesByName = CollectTransforms(bodyguardInstance.transform);
                var targetBonesByName = CollectTransforms(fpsHandInstance.transform);
                var bodyRenderers = bodyguardInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var targetRenderers = fpsHandInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var handCandidates = bodyRenderers.Where(IsHandCandidate).ToArray();

                var report = new StringBuilder();
                AppendHeader(report, bodyguardCopyPath, fpsHandCopyPath, bodyBonesByName, targetBonesByName, bodyRenderers, targetRenderers, handCandidates);

                int createdRendererCount = 0;
                foreach (SkinnedMeshRenderer sourceRenderer in handCandidates)
                {
                    Dictionary<Transform, Transform> resolvedBoneMap = ResolveBoneMap(sourceRenderer.bones, targetBonesByName);
                    int mappedCount = sourceRenderer.bones.Count(bone => bone != null && resolvedBoneMap.ContainsKey(bone));
                    int requiredCount = sourceRenderer.bones.Count(bone => bone != null);
                    float mappedRatio = requiredCount > 0 ? mappedCount / (float)requiredCount : 0f;

                    report.AppendLine($"## Candidate: `{sourceRenderer.name}`");
                    report.AppendLine($"- Mesh: `{sourceRenderer.sharedMesh?.name ?? "<none>"}`");
                    report.AppendLine($"- Bone mapping: {mappedCount}/{requiredCount} exact/normalized/suffix/alias matches ({mappedRatio:P0})");
                    report.AppendLine($"- Root bone: `{sourceRenderer.rootBone?.name ?? "<none>"}`");

                    string[] missingBones = sourceRenderer.bones
                        .Where(bone => bone != null && !resolvedBoneMap.ContainsKey(bone))
                        .Select(bone => bone.name)
                        .Distinct()
                        .OrderBy(name => name)
                        .ToArray();

                    if (missingBones.Length > 0)
                        report.AppendLine($"- Missing target bones: {string.Join(", ", missingBones.Select(name => $"`{name}`"))}");

                    if (requiredCount == 0 || mappedCount != requiredCount)
                    {
                        report.AppendLine("- Result: skipped prefab binding because resolved bone coverage is incomplete.");
                        report.AppendLine();
                        continue;
                    }

                    CreateBoundRenderer(fpsHandInstance.transform, sourceRenderer, resolvedBoneMap, meshRoot, createdRendererCount);
                    createdRendererCount++;
                    report.AppendLine("- Result: copied mesh and rebound renderer to FPSHand transforms by resolved bone names.");
                    report.AppendLine();
                }

                if (createdRendererCount > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(fpsHandInstance, prefabPath);
                    report.AppendLine($"Created prototype prefab: `{prefabPath}`");
                    report.AppendLine();
                    report.AppendLine("Important: this is only an automatic bind-name prototype. If bind poses/rest poses differ, fix skin weights in Blender.");
                }
                else
                {
                    report.AppendLine("No prototype prefab was created because no Bodyguard hand renderer had complete exact-name bone coverage on FPSHand.");
                }

                File.WriteAllText(reportPath, report.ToString());
            }
            finally
            {
                Object.DestroyImmediate(bodyguardInstance);
                Object.DestroyImmediate(fpsHandInstance);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[FPSHandBodyguardAutomation] Wrote report to {reportPath}");
        }

        private static void CreateBoundRenderer(
            Transform targetRoot,
            SkinnedMeshRenderer sourceRenderer,
            IReadOnlyDictionary<Transform, Transform> resolvedBoneMap,
            string meshRoot,
            int rendererIndex)
        {
            var rendererGo = new GameObject($"{sourceRenderer.name}_BodyguardPrototype");
            rendererGo.transform.SetParent(targetRoot, false);

            Mesh meshCopy = Object.Instantiate(sourceRenderer.sharedMesh);
            meshCopy.name = $"{sourceRenderer.sharedMesh.name}_BodyguardPrototype";
            string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{meshRoot}/{meshCopy.name}_{rendererIndex}.asset");
            AssetDatabase.CreateAsset(meshCopy, meshPath);

            var targetRenderer = rendererGo.AddComponent<SkinnedMeshRenderer>();
            targetRenderer.sharedMesh = meshCopy;
            targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            targetRenderer.bones = sourceRenderer.bones
                .Select(bone => resolvedBoneMap[bone])
                .ToArray();
            targetRenderer.rootBone = sourceRenderer.rootBone != null && resolvedBoneMap.TryGetValue(sourceRenderer.rootBone, out Transform mappedRoot)
                ? mappedRoot
                : targetRenderer.bones.FirstOrDefault();
            targetRenderer.updateWhenOffscreen = true;
            targetRenderer.localBounds = sourceRenderer.localBounds;
        }

        private static bool IsHandCandidate(SkinnedMeshRenderer renderer)
        {
            string rendererText = $"{renderer.name} {renderer.sharedMesh?.name}".ToLowerInvariant();
            if (rendererText.Contains("hand") || rendererText.Contains("arm"))
                return true;

            return renderer.bones.Any(bone =>
                bone != null &&
                (bone.name.Contains("Hand") || bone.name.Contains("ForeArm") || bone.name.Contains("Finger")));
        }

        private static Dictionary<string, Transform> CollectTransforms(Transform root)
        {
            var result = new Dictionary<string, Transform>();
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (!result.ContainsKey(transform.name))
                    result.Add(transform.name, transform);
            }

            return result;
        }

        private static void AppendHeader(
            StringBuilder report,
            string bodyguardCopyPath,
            string fpsHandCopyPath,
            IReadOnlyDictionary<string, Transform> bodyBonesByName,
            IReadOnlyDictionary<string, Transform> targetBonesByName,
            SkinnedMeshRenderer[] bodyRenderers,
            SkinnedMeshRenderer[] targetRenderers,
            SkinnedMeshRenderer[] handCandidates)
        {
            string[] exactMatches = bodyBonesByName.Keys
                .Where(targetBonesByName.ContainsKey)
                .OrderBy(name => name)
                .ToArray();

            report.AppendLine("# Bodyguard To FPSHand Automation Report");
            report.AppendLine();
            report.AppendLine("This report was generated by `FPSHandBodyguardAutomation`.");
            report.AppendLine();
            report.AppendLine("## Inputs");
            report.AppendLine($"- Bodyguard copy: `{bodyguardCopyPath}`");
            report.AppendLine($"- FPSHand copy: `{fpsHandCopyPath}`");
            report.AppendLine();
            report.AppendLine("## Summary");
            report.AppendLine($"- Bodyguard transforms: {bodyBonesByName.Count}");
            report.AppendLine($"- FPSHand transforms: {targetBonesByName.Count}");
            report.AppendLine($"- Exact-name transform matches: {exactMatches.Length}");
            report.AppendLine($"- Bodyguard skinned renderers: {bodyRenderers.Length}");
            report.AppendLine($"- FPSHand skinned renderers: {targetRenderers.Length}");
            report.AppendLine($"- Bodyguard hand candidates: {handCandidates.Length}");
            report.AppendLine();
            report.AppendLine("## Exact Transform Matches");
            report.AppendLine(exactMatches.Length == 0
                ? "- None"
                : string.Join(", ", exactMatches.Select(name => $"`{name}`")));
            report.AppendLine();
            report.AppendLine("## FPSHand Target Transforms");
            report.AppendLine(string.Join(", ", targetBonesByName.Keys.OrderBy(name => name).Select(name => $"`{name}`")));
            report.AppendLine();
            report.AppendLine("## Bodyguard Skinned Renderers");

            foreach (SkinnedMeshRenderer renderer in bodyRenderers)
            {
                report.AppendLine($"- `{renderer.name}` mesh=`{renderer.sharedMesh?.name ?? "<none>"}` bones={renderer.bones.Length}");
            }

            report.AppendLine();
        }

        private static string CopyAssetFresh(string sourcePath, string targetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(targetPath) != null)
                AssetDatabase.DeleteAsset(targetPath);

            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                throw new IOException($"Failed to copy {sourcePath} to {targetPath}");

            return targetPath;
        }

        private static Dictionary<Transform, Transform> ResolveBoneMap(
            IEnumerable<Transform> sourceBones,
            IReadOnlyDictionary<string, Transform> targetBonesByName)
        {
            var result = new Dictionary<Transform, Transform>();
            var normalizedTargets = targetBonesByName.Values
                .GroupBy(transform => NormalizeBoneName(transform.name))
                .ToDictionary(group => group.Key, group => group.ToArray());

            foreach (Transform sourceBone in sourceBones.Where(bone => bone != null).Distinct())
            {
                if (targetBonesByName.TryGetValue(sourceBone.name, out Transform exactTarget))
                {
                    result[sourceBone] = exactTarget;
                    continue;
                }

                string normalizedSource = NormalizeBoneName(sourceBone.name);
                if (normalizedTargets.TryGetValue(normalizedSource, out Transform[] normalizedMatches) && normalizedMatches.Length == 1)
                {
                    result[sourceBone] = normalizedMatches[0];
                    continue;
                }

                Transform[] suffixMatches = targetBonesByName.Values
                    .Where(target => NormalizeBoneName(target.name).EndsWith(normalizedSource))
                    .ToArray();

                if (suffixMatches.Length == 1)
                {
                    result[sourceBone] = suffixMatches[0];
                    continue;
                }

                if (TryGetFPSHandAlias(sourceBone.name, out string aliasName) &&
                    targetBonesByName.TryGetValue(aliasName, out Transform aliasTarget))
                {
                    result[sourceBone] = aliasTarget;
                }
            }

            return result;
        }

        private static bool TryGetFPSHandAlias(string sourceBoneName, out string aliasName)
        {
            aliasName = null;
            string sidePrefix;
            string localName;

            if (sourceBoneName.StartsWith("Left"))
            {
                sidePrefix = "L_";
                localName = sourceBoneName.Substring("Left".Length);
            }
            else if (sourceBoneName.StartsWith("Right"))
            {
                sidePrefix = "R_";
                localName = sourceBoneName.Substring("Right".Length);
            }
            else
            {
                return false;
            }

            if (localName == "ForeArm")
            {
                aliasName = sidePrefix + "Elbow";
                return true;
            }

            if (localName == "Hand")
            {
                aliasName = sidePrefix + "Hand";
                return true;
            }

            if (!localName.StartsWith("Hand"))
                return false;

            string fingerName = localName.Substring("Hand".Length);
            string[] fingers = { "Index", "Middle", "Ring", "Pinky", "Thumb" };
            foreach (string finger in fingers)
            {
                if (!fingerName.StartsWith(finger))
                    continue;

                string numberText = fingerName.Substring(finger.Length);
                if (!int.TryParse(numberText, out int sourceIndex))
                    return false;

                if (finger == "Thumb")
                {
                    aliasName = sourceIndex >= 4
                        ? sidePrefix + "Thumb3_end"
                        : sidePrefix + "Thumb" + sourceIndex;
                    return true;
                }

                int targetIndex = Mathf.Clamp(sourceIndex - 1, 0, 3);
                aliasName = sidePrefix + finger + targetIndex;
                return true;
            }

            return false;
        }

        private static string NormalizeBoneName(string boneName)
        {
            var builder = new StringBuilder(boneName.Length);
            foreach (char c in boneName)
            {
                if (char.IsLetterOrDigit(c))
                    builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
