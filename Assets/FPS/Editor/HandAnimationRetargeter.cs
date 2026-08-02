using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FPS.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS.Editor
{
    public static class HandAnimationRetargeter
    {
        private const string ProfilePath = "Assets/FPS/Config/HandAnimationRetargetProfile.asset";
        private const string DefaultSourceModelPath = "Assets/FPS/Features/Weapons/Content/AKM/Animations/FP_Classic.fbx";
        private const string DefaultBaseControllerPath = "Assets/FPS/Features/Weapons/Content/AKM/Animations/New Animator Controller.controller";
        private const string DefaultTestScenePath = "Assets/FPS/Scenes/TestScene.unity";
        private const string OutputRoot = "Assets/FPS/Generated/Animations/HandAnimations/AllSources";
        private const string TestRootName = "__HandAnimationVerification";
        private const string BatchVerificationSessionKey = "FPS.HandAnimationRetargeter.BatchVerification";

        static HandAnimationRetargeter()
        {
            EditorApplication.update += PollBatchVerification;
        }

        [MenuItem("FPS/Animation/Build Hand Retargeted Animations")]
        public static void BuildHandRetargetedAnimations()
        {
            Build(false);
        }

        [MenuItem("FPS/Animation/Build Hand Animations And Prepare TestScene")]
        public static void BuildAndPrepareTestScene()
        {
            Build(true);
        }

        [MenuItem("FPS/Animation/Build Hand Animations And Verify TestScene")]
        public static void BuildAndVerifyTestScene()
        {
            Build(true);
            EditorPrefs.SetBool(BatchVerificationSessionKey, true);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("FPS/Animation/Select Hand Retarget Profile")]
        public static void SelectHandRetargetProfile()
        {
            Selection.activeObject = LoadOrCreateProfile();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        private static void Build(bool prepareTestScene)
        {
            EnsureFolder("Assets/FPS/Generated");
            EnsureFolder("Assets/FPS/Generated/Animations");
            EnsureFolder("Assets/FPS/Generated/Animations/HandAnimations");
            EnsureFolder(OutputRoot);

            HandAnimationRetargetProfile profile = LoadOrCreateProfile();
            ValidateProfile(profile, prepareTestScene);

            AnimatorController baseController = profile.baseController;
            IReadOnlyList<SourceAnimationSet> sourceSets = LoadSourceAnimationSets(profile);
            if (sourceSets.Count == 0 || baseController == null)
            {
                throw new InvalidOperationException("The source FBX list or base Animator Controller in the Hand Retarget Profile could not be loaded.");
            }

            var sourceClipNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (SourceAnimationSet sourceSet in sourceSets)
            {
                foreach (AnimationClip clip in sourceSet.Clips)
                {
                    if (!sourceClipNames.Add(clip.name))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate animation clip name '{clip.name}' exists in more than one source FBX. " +
                            "Rename the clip or keep only one source copy in the profile.");
                    }
                }
            }

            var controllerStates = GetControllerStateNames(baseController, sourceClipNames);
            if (prepareTestScene && controllerStates.Count == 0)
            {
                throw new InvalidOperationException(
                    "The base Animator Controller has no state using a clip from the configured source FBX files. " +
                    "The verification scene cannot select a source animation state.");
            }
            var targetResults = new List<TargetBuildResult>();
            foreach (HandAnimationRetargetProfile.Target profileTarget in profile.targets)
            {
                TargetDefinition target = TargetDefinition.Create(profileTarget);
                targetResults.Add(BuildTarget(target, sourceSets, baseController));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (prepareTestScene)
            {
                PrepareTestScene(targetResults, controllerStates, AssetDatabase.GetAssetPath(profile.testScene));
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Built retargeted animations for {targetResults.Count} hands from {sourceSets.Count} source FBX files. " +
                $"Source clips: {sourceClipNames.Count}.");
        }

        private static HandAnimationRetargetProfile LoadOrCreateProfile()
        {
            HandAnimationRetargetProfile profile = AssetDatabase.LoadAssetAtPath<HandAnimationRetargetProfile>(ProfilePath);
            if (profile != null)
            {
                if (profile.sourceModels == null)
                {
                    profile.sourceModels = new List<GameObject>();
                }

                if (profile.sourceModels.Count == 0)
                {
                    profile.sourceModels.AddRange(GetDefaultSourceModels());
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"Migrated {ProfilePath} to the multi-source animation profile.", profile);
                }

                return profile;
            }

            EnsureFolder("Assets/FPS/Config");
            profile = ScriptableObject.CreateInstance<HandAnimationRetargetProfile>();
            profile.sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultSourceModelPath);
            profile.sourceModels = GetDefaultSourceModels();
            profile.baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(DefaultBaseControllerPath);
            profile.testScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(DefaultTestScenePath);
            profile.targets.Add(CreateDefaultTarget(
                "Brimstone",
                "Assets/FPS/Features/Characters/Content/Players/Brimstone/Models/Hand.fbx",
                "Assets/FPS/Features/Characters/Content/Players/Brimstone/Brimstone.prefab"));
            profile.targets.Add(CreateDefaultTarget(
                "Gekko",
                "Assets/FPS/Features/Characters/Content/Players/Gekko/Models/Hand.fbx",
                "Assets/FPS/Features/Characters/Content/Players/Gekko/Gekko.prefab"));
            profile.targets.Add(CreateDefaultTarget(
                "Sage",
                "Assets/FPS/Features/Characters/Content/Players/Sage/Models/Hand.fbx",
                "Assets/FPS/Features/Characters/Content/Players/Sage/Sage.prefab"));

            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created {ProfilePath}. Add future hands to this asset instead of editing the retargeter code.", profile);
            return profile;
        }

        private static List<GameObject> GetDefaultSourceModels()
        {
            string[] paths =
            {
                "Assets/FPS/Features/Weapons/Content/AKM/Animations/FP_Classic.fbx",
                "Assets/FPS/Features/Weapons/Content/AKM/Animations/FP_Vandal.fbx",
                "Assets/FPS/Features/Weapons/Content/AKM/Animations/GN_Vandal.fbx",
                "Assets/FPS/Features/Weapons/Content/AKM/Animations/GN_Classic.fbx"
            };

            return paths
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(model => model != null)
                .ToList();
        }

        private static IReadOnlyList<SourceAnimationSet> LoadSourceAnimationSets(HandAnimationRetargetProfile profile)
        {
            var models = new List<GameObject>();
            if (profile.sourceModels != null)
            {
                models.AddRange(profile.sourceModels.Where(model => model != null));
            }

            if (models.Count == 0 && profile.sourceModel != null)
            {
                models.Add(profile.sourceModel);
            }

            return models
                .GroupBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
                .Select(group =>
                {
                    string path = group.Key;
                    AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path)
                        .OfType<AnimationClip>()
                        .Where(clip => clip != null && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    if (clips.Length == 0)
                    {
                        throw new InvalidOperationException($"No animation clips were found in {path}.");
                    }

                    return new SourceAnimationSet(group.First(), path, clips);
                })
                .ToList();
        }

        private static HandAnimationRetargetProfile.Target CreateDefaultTarget(
            string characterName,
            string modelPath,
            string prefabPath)
        {
            return new HandAnimationRetargetProfile.Target
            {
                characterName = characterName,
                handModel = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath),
                probeBoneName = "L_Hand"
            };
        }

        private static void ValidateProfile(HandAnimationRetargetProfile profile, bool prepareTestScene)
        {
            if (profile == null)
            {
                throw new InvalidOperationException("Hand Animation Retarget Profile is missing.");
            }

            if ((profile.sourceModels == null || profile.sourceModels.Count == 0) && profile.sourceModel == null)
            {
                throw new InvalidOperationException($"Assign at least one source FBX in {ProfilePath}.");
            }

            if (profile.baseController == null)
            {
                throw new InvalidOperationException($"Assign the base Animator Controller in {ProfilePath}.");
            }

            if (profile.targets == null || profile.targets.Count == 0)
            {
                throw new InvalidOperationException($"Add at least one hand target to {ProfilePath}.");
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < profile.targets.Count; i++)
            {
                HandAnimationRetargetProfile.Target target = profile.targets[i];
                if (target == null || string.IsNullOrWhiteSpace(target.characterName))
                {
                    throw new InvalidOperationException($"Target {i + 1} in {ProfilePath} has no character name.");
                }

                if (!names.Add(target.characterName))
                {
                    throw new InvalidOperationException($"Duplicate character name '{target.characterName}' in {ProfilePath}.");
                }

                if (target.handModel == null || target.prefab == null)
                {
                    throw new InvalidOperationException($"Target '{target.characterName}' must have both Hand Model and Prefab assigned.");
                }
            }

            if (prepareTestScene && profile.testScene == null)
            {
                throw new InvalidOperationException($"Assign the verification TestScene in {ProfilePath}.");
            }
        }

        private static TargetBuildResult BuildTarget(
            TargetDefinition target,
            IReadOnlyList<SourceAnimationSet> sourceSets,
            AnimatorController baseController)
        {
            GameObject targetModel = AssetDatabase.LoadAssetAtPath<GameObject>(target.ModelPath);
            if (targetModel == null)
            {
                throw new InvalidOperationException($"Target model not found: {target.ModelPath}");
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(target.PrefabPath);
            try
            {
                Transform targetRuntimeRoot = FindTargetRoot(prefabContents, targetModel);
                if (targetRuntimeRoot == null)
                {
                    throw new InvalidOperationException(
                        $"Could not locate the '{targetModel.transform.name}' hand model hierarchy in {target.PrefabPath}.");
                }

                string safeTargetName = SanitizeAssetName(target.Name);
                string targetFolder = $"{OutputRoot}/{safeTargetName}";
                EnsureFolder(targetFolder);

                var bakedBySourceName = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
                var allUnmappedPaths = new List<string>();
                foreach (SourceAnimationSet sourceSet in sourceSets)
                {
                    GameObject sourceInstance = InstantiateHidden(sourceSet.Model);
                    try
                    {
                        TransformMap transformMap = TransformMap.Create(sourceInstance.transform, targetRuntimeRoot);
                        ValidateAnimatedBindings(
                            $"{target.Name}/{Path.GetFileNameWithoutExtension(sourceSet.Path)}",
                            sourceSet.Clips,
                            transformMap);

                        if (transformMap.UnmappedPaths.Count > 0)
                        {
                            allUnmappedPaths.AddRange(transformMap.UnmappedPaths);
                            string sample = string.Join(", ", transformMap.UnmappedPaths.Take(8));
                            Debug.LogWarning(
                                $"[{target.Name}] {transformMap.UnmappedPaths.Count} transforms from {sourceSet.Path} have no target match. " +
                                $"They are not animated by the selected clips or need an explicit mapping. Sample: {sample}",
                                targetModel);
                        }

                        foreach (AnimationClip sourceClip in sourceSet.Clips)
                        {
                            AnimationClip bakedClip = BakeClip(sourceClip, transformMap, targetFolder, safeTargetName);
                            bakedBySourceName[sourceClip.name] = bakedClip;
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(sourceInstance);
                    }
                }

                string controllerPath = $"{targetFolder}/{safeTargetName}_AllSources.overrideController";
                AnimatorOverrideController overrideController = CreateOrUpdateOverrideController(
                    controllerPath,
                    baseController,
                    bakedBySourceName);
                AssignControllerToPrefab(target.PrefabPath, targetModel, overrideController);

                return new TargetBuildResult(target, overrideController, bakedBySourceName, allUnmappedPaths);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static void ValidateAnimatedBindings(
            string targetName,
            IReadOnlyList<AnimationClip> sourceClips,
            TransformMap transformMap)
        {
            var animatedPaths = new HashSet<string>(StringComparer.Ordinal);
            var skippedNonTransformPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (AnimationClip sourceClip in sourceClips)
            {
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(sourceClip))
                {
                    if (transformMap.ContainsSourcePath(binding.path))
                    {
                        animatedPaths.Add(binding.path);
                    }
                    else
                    {
                        skippedNonTransformPaths.Add($"{binding.path} ({binding.type.Name}, source path not in model)");
                    }
                }

                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(sourceClip))
                {
                    if (!transformMap.TryMapPath(binding.path, out _))
                    {
                        skippedNonTransformPaths.Add($"{binding.path} ({binding.type.Name})");
                    }
                }
            }

            if (skippedNonTransformPaths.Count > 0)
            {
                string skippedSample = string.Join(", ", skippedNonTransformPaths.Take(8));
                Debug.LogWarning(
                    $"[{targetName}] skipped {skippedNonTransformPaths.Count} non-transform bindings that are not present on the target Hand. " +
                    $"Sample: {skippedSample}");
            }

            var unmappedAnimatedPaths = animatedPaths
                .Where(path => !transformMap.TryMapPath(path, out _))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (unmappedAnimatedPaths.Length == 0)
            {
                return;
            }

            string sample = string.Join(", ", unmappedAnimatedPaths.Take(12));
            throw new InvalidOperationException(
                $"[{targetName}] cannot retarget {unmappedAnimatedPaths.Length} animated binding paths. " +
                $"No curves were baked for these paths. Sample: {sample}");
        }

        private static AnimationClip BakeClip(
            AnimationClip sourceClip,
            TransformMap transformMap,
            string targetFolder,
            string targetName)
        {
            string assetName = SanitizeAssetName($"{targetName}_{sourceClip.name}");
            string assetPath = $"{targetFolder}/{assetName}.anim";
            AnimationClip bakedClip = new AnimationClip
            {
                name = assetName,
                frameRate = sourceClip.frameRate,
                legacy = false,
                wrapMode = sourceClip.wrapMode
            };

            foreach (EditorCurveBinding sourceBinding in AnimationUtility.GetCurveBindings(sourceClip))
            {
                if (!transformMap.TryMapPath(sourceBinding.path, out string targetPath))
                {
                    continue;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, sourceBinding);
                EditorCurveBinding targetBinding = new EditorCurveBinding
                {
                    path = targetPath,
                    propertyName = sourceBinding.propertyName,
                    type = sourceBinding.type
                };
                AnimationUtility.SetEditorCurve(bakedClip, targetBinding, curve);
            }

            foreach (EditorCurveBinding sourceBinding in AnimationUtility.GetObjectReferenceCurveBindings(sourceClip))
            {
                if (!transformMap.TryMapPath(sourceBinding.path, out string targetPath))
                {
                    continue;
                }

                ObjectReferenceKeyframe[] curve = AnimationUtility.GetObjectReferenceCurve(sourceClip, sourceBinding);
                EditorCurveBinding targetBinding = new EditorCurveBinding
                {
                    path = targetPath,
                    propertyName = sourceBinding.propertyName,
                    type = sourceBinding.type
                };
                AnimationUtility.SetObjectReferenceCurve(bakedClip, targetBinding, curve);
            }

            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(sourceClip);
            AnimationUtility.SetAnimationEvents(bakedClip, events);

            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(bakedClip, assetPath);
                return bakedClip;
            }

            EditorUtility.CopySerialized(bakedClip, existing);
            UnityEngine.Object.DestroyImmediate(bakedClip);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static AnimatorOverrideController CreateOrUpdateOverrideController(
            string assetPath,
            AnimatorController baseController,
            IReadOnlyDictionary<string, AnimationClip> bakedBySourceName)
        {
            AnimatorOverrideController controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(assetPath);
            if (controller == null)
            {
                controller = new AnimatorOverrideController(baseController);
                AssetDatabase.CreateAsset(controller, assetPath);
            }
            else
            {
                controller.runtimeAnimatorController = baseController;
            }

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (AnimationClip sourceClip in controller.animationClips)
            {
                if (sourceClip != null && bakedBySourceName.TryGetValue(sourceClip.name, out AnimationClip bakedClip))
                {
                    overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(sourceClip, bakedClip));
                }
            }

            controller.ApplyOverrides(overrides);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AssignControllerToPrefab(
            string prefabPath,
            GameObject targetModel,
            AnimatorOverrideController controller)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform handRoot = FindTargetRoot(prefabRoot, targetModel);
                if (handRoot == null)
                {
                    throw new InvalidOperationException(
                        $"Could not locate the '{targetModel.transform.name}' hand model hierarchy in {prefabPath}.");
                }

                Animator handAnimator = handRoot.GetComponent<Animator>();
                if (handAnimator == null)
                {
                    handAnimator = handRoot.gameObject.AddComponent<Animator>();
                }

                handAnimator.runtimeAnimatorController = controller;
                handAnimator.avatar = null;
                EditorUtility.SetDirty(handAnimator);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void PrepareTestScene(
            IReadOnlyList<TargetBuildResult> results,
            IReadOnlyList<string> controllerStates,
            string testScenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(testScenePath, OpenSceneMode.Single);
            GameObject oldRoot = GameObject.Find(TestRootName);
            if (oldRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(oldRoot);
            }

            GameObject testRoot = new GameObject(TestRootName);
            testRoot.transform.position = Vector3.zero;
            HandAnimationVerification verification = testRoot.AddComponent<HandAnimationVerification>();

            SerializedObject serializedVerification = new SerializedObject(verification);
            SerializedProperty entriesProperty = serializedVerification.FindProperty("entries");
            entriesProperty.arraySize = results.Count;

            for (int i = 0; i < results.Count; i++)
            {
                TargetBuildResult result = results[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.Target.PrefabPath);
                GameObject targetModel = AssetDatabase.LoadAssetAtPath<GameObject>(result.Target.ModelPath);
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance == null)
                {
                    throw new InvalidOperationException($"Could not instantiate {result.Target.PrefabPath} into TestScene.");
                }

                instance.name = $"{result.Target.Name}_HandAnimationTest";
                instance.transform.SetParent(testRoot.transform, false);
                instance.transform.localPosition = new Vector3((i - 1) * 2.5f, 0f, 0f);
                instance.transform.localRotation = Quaternion.identity;

                Transform handRoot = FindTargetRoot(instance, targetModel);
                Animator animator = handRoot == null ? null : handRoot.GetComponent<Animator>();
                if (animator == null)
                {
                    throw new InvalidOperationException(
                        $"No hand Animator found after instantiating {result.Target.PrefabPath}. " +
                        "The retargeted controller must be assigned to the Hand model hierarchy.");
                }

                SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("label").stringValue = result.Target.Name;
                entry.FindPropertyRelative("animator").objectReferenceValue = animator;
                entry.FindPropertyRelative("stateName").stringValue = controllerStates.Count > 0
                    ? controllerStates[0]
                    : string.Empty;
                SerializedProperty stateNamesProperty = entry.FindPropertyRelative("stateNames");
                stateNamesProperty.arraySize = controllerStates.Count;
                for (int stateIndex = 0; stateIndex < controllerStates.Count; stateIndex++)
                {
                    stateNamesProperty.GetArrayElementAtIndex(stateIndex).stringValue = controllerStates[stateIndex];
                }

                entry.FindPropertyRelative("expectedClipPrefix").stringValue =
                    $"{SanitizeAssetName(result.Target.Name)}_";
                entry.FindPropertyRelative("probeBoneName").stringValue = result.Target.ProbeBoneName;
            }

            serializedVerification.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = testRoot;
        }

        private static IReadOnlyList<string> GetControllerStateNames(
            AnimatorController controller,
            ISet<string> sourceClipNames)
        {
            var names = new List<string>();
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                foreach (ChildAnimatorState childState in layer.stateMachine.states)
                {
                    if (childState.state != null && MotionUsesSourceClip(childState.state.motion, sourceClipNames))
                    {
                        names.Add($"{layer.name}.{childState.state.name}");
                    }
                }
            }

            names.Sort((left, right) =>
                GetVerificationStatePriority(left).CompareTo(GetVerificationStatePriority(right)));
            return names;
        }

        private static int GetVerificationStatePriority(string stateName)
        {
            if (stateName.IndexOf("_Fire", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 0;
            }

            if (stateName.IndexOf("_Reload", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 1;
            }

            if (stateName.IndexOf("_Inspect", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 2;
            }

            if (stateName.IndexOf("_Equip", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 3;
            }

            return 4;
        }

        private static bool MotionUsesSourceClip(Motion motion, ISet<string> sourceClipNames)
        {
            if (motion is AnimationClip clip)
            {
                return sourceClipNames.Contains(clip.name);
            }

            if (motion is BlendTree blendTree)
            {
                foreach (ChildMotion childMotion in blendTree.children)
                {
                    if (MotionUsesSourceClip(childMotion.motion, sourceClipNames))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void PollBatchVerification()
        {
            if (!EditorPrefs.GetBool(BatchVerificationSessionKey, false) || !Application.isPlaying)
            {
                return;
            }

            HandAnimationVerification verification = UnityEngine.Object.FindAnyObjectByType<HandAnimationVerification>();
            if (verification == null || !verification.IsComplete)
            {
                return;
            }

            bool passed = verification.Passed;
            EditorPrefs.DeleteKey(BatchVerificationSessionKey);
            Debug.Log(passed
                ? "Hand animation PlayMode verification passed for every configured hand."
                : "Hand animation PlayMode verification failed for one or more configured hands.");

            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }

        private static GameObject InstantiateHidden(GameObject prefab)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            return instance;
        }

        private static Transform FindTargetRoot(GameObject prefabRoot, GameObject targetModel)
        {
            if (prefabRoot == null || targetModel == null)
            {
                return null;
            }

            string targetRootKey = CanonicalName(targetModel.transform.name);
            HashSet<string> targetHierarchy = BuildCanonicalPathSet(targetModel.transform);
            Transform best = null;
            int bestScore = -1;

            foreach (Transform candidate in prefabRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!string.Equals(CanonicalName(candidate.name), targetRootKey, StringComparison.Ordinal))
                {
                    continue;
                }

                HashSet<string> candidateHierarchy = BuildCanonicalPathSet(candidate);
                int score = targetHierarchy.Intersect(candidateHierarchy).Count();
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        private static HashSet<string> BuildCanonicalPathSet(Transform root)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                string path = RelativeTransformPath(root, transform);
                if (path.Length > 0)
                {
                    paths.Add(CanonicalPath(path));
                }
            }

            return paths;
        }

        private static string RelativeTransformPath(Transform root, Transform transform)
        {
            if (transform == root)
            {
                return string.Empty;
            }

            var segments = new Stack<string>();
            Transform current = transform;
            while (current != null && current != root)
            {
                segments.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", segments.ToArray());
        }

        private static string CanonicalPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            return string.Join("/", path.Split('/').Select(CanonicalName));
        }

        private static string CanonicalName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(name.Length);
            foreach (char character in name)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }

        private static string SanitizeAssetName(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_');
            }

            return builder.ToString();
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private sealed class SourceAnimationSet
        {
            public readonly GameObject Model;
            public readonly string Path;
            public readonly AnimationClip[] Clips;

            public SourceAnimationSet(GameObject model, string path, AnimationClip[] clips)
            {
                Model = model;
                Path = path;
                Clips = clips;
            }
        }

        private sealed class TargetDefinition
        {
            public readonly string Name;
            public readonly string ModelPath;
            public readonly string PrefabPath;
            public readonly string ProbeBoneName;

            private TargetDefinition(string name, string modelPath, string prefabPath, string probeBoneName)
            {
                Name = name;
                ModelPath = modelPath;
                PrefabPath = prefabPath;
                ProbeBoneName = string.IsNullOrWhiteSpace(probeBoneName) ? "L_Hand" : probeBoneName;
            }

            public static TargetDefinition Create(HandAnimationRetargetProfile.Target target)
            {
                return new TargetDefinition(
                    target.characterName.Trim(),
                    AssetDatabase.GetAssetPath(target.handModel),
                    AssetDatabase.GetAssetPath(target.prefab),
                    target.probeBoneName);
            }
        }

        private sealed class TargetBuildResult
        {
            public readonly TargetDefinition Target;
            public readonly AnimatorOverrideController Controller;
            public readonly IReadOnlyDictionary<string, AnimationClip> BakedClips;
            public readonly IReadOnlyList<string> UnmappedPaths;

            public TargetBuildResult(
                TargetDefinition target,
                AnimatorOverrideController controller,
                IReadOnlyDictionary<string, AnimationClip> bakedClips,
                IReadOnlyList<string> unmappedPaths)
            {
                Target = target;
                Controller = controller;
                BakedClips = bakedClips;
                UnmappedPaths = unmappedPaths;
            }
        }

        private sealed class TransformMap
        {
            private readonly Dictionary<string, string> _pathMap;
            private readonly Dictionary<string, string> _uniqueLeafMap;
            public readonly IReadOnlyList<string> UnmappedPaths;

            private TransformMap(
                Dictionary<string, string> pathMap,
                Dictionary<string, string> uniqueLeafMap,
                List<string> unmappedPaths)
            {
                _pathMap = pathMap;
                _uniqueLeafMap = uniqueLeafMap;
                UnmappedPaths = unmappedPaths;
            }

            public static TransformMap Create(Transform sourceRoot, Transform targetRoot)
            {
                var targetByName = new Dictionary<string, List<Transform>>(StringComparer.OrdinalIgnoreCase);
                var targetByCanonicalPath = new Dictionary<string, Transform>(StringComparer.Ordinal);
                foreach (Transform targetTransform in targetRoot.GetComponentsInChildren<Transform>(true))
                {
                    string key = CanonicalName(targetTransform.name);
                    if (!targetByName.TryGetValue(key, out List<Transform> candidates))
                    {
                        candidates = new List<Transform>();
                        targetByName.Add(key, candidates);
                    }

                    candidates.Add(targetTransform);
                    string canonicalPath = CanonicalPath(RelativePath(targetRoot, targetTransform));
                    if (canonicalPath.Length > 0 && !targetByCanonicalPath.ContainsKey(canonicalPath))
                    {
                        targetByCanonicalPath.Add(canonicalPath, targetTransform);
                    }
                }

                var pathMap = new Dictionary<string, string>(StringComparer.Ordinal);
                var unmapped = new List<string>();
                foreach (Transform sourceTransform in sourceRoot.GetComponentsInChildren<Transform>(true))
                {
                    string sourcePath = RelativePath(sourceRoot, sourceTransform);
                    if (sourcePath.Length == 0)
                    {
                        pathMap[sourcePath] = string.Empty;
                        continue;
                    }

                    Transform targetTransform = targetRoot.Find(sourcePath);
                    if (targetTransform == null && targetByCanonicalPath.TryGetValue(CanonicalPath(sourcePath), out Transform canonicalMatch))
                    {
                        targetTransform = canonicalMatch;
                    }

                    if (targetTransform == null)
                    {
                        string key = CanonicalName(sourceTransform.name);
                        if (targetByName.TryGetValue(key, out List<Transform> candidates))
                        {
                            targetTransform = SelectBestCandidate(sourceTransform, candidates, sourceRoot, targetRoot);
                        }
                    }

                    if (targetTransform == null)
                    {
                        unmapped.Add(sourcePath);
                        continue;
                    }

                    pathMap[sourcePath] = RelativePath(targetRoot, targetTransform);
                }

                var uniqueLeafMap = new Dictionary<string, string>(StringComparer.Ordinal);
                var ambiguousLeaves = new HashSet<string>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> pair in pathMap)
                {
                    string leaf = CanonicalName(pair.Key.Split('/').LastOrDefault() ?? string.Empty);
                    if (leaf.Length == 0)
                    {
                        continue;
                    }

                    if (uniqueLeafMap.ContainsKey(leaf))
                    {
                        uniqueLeafMap.Remove(leaf);
                        ambiguousLeaves.Add(leaf);
                    }
                    else if (!ambiguousLeaves.Contains(leaf))
                    {
                        uniqueLeafMap.Add(leaf, pair.Value);
                    }
                }

                return new TransformMap(pathMap, uniqueLeafMap, unmapped);
            }

            public bool TryMapPath(string sourcePath, out string targetPath)
            {
                if (_pathMap.TryGetValue(sourcePath, out targetPath))
                {
                    return true;
                }

                string sourceLeaf = sourcePath.Split('/').LastOrDefault();
                if (!string.IsNullOrEmpty(sourceLeaf))
                {
                    string key = CanonicalName(sourceLeaf);
                    if (_uniqueLeafMap.TryGetValue(key, out targetPath))
                    {
                        return true;
                    }
                }

                targetPath = null;
                return false;
            }

            public bool ContainsSourcePath(string sourcePath)
            {
                return _pathMap.ContainsKey(sourcePath);
            }

            private static Transform SelectBestCandidate(
                Transform sourceTransform,
                IReadOnlyList<Transform> candidates,
                Transform sourceRoot,
                Transform targetRoot)
            {
                string sourcePath = RelativePath(sourceRoot, sourceTransform);
                string[] sourceSegments = sourcePath.Split('/');
                Transform best = null;
                int bestScore = int.MinValue;
                foreach (Transform candidate in candidates)
                {
                    string[] targetSegments = RelativePath(targetRoot, candidate).Split('/');
                    int score = 0;
                    int sourceIndex = sourceSegments.Length - 1;
                    int targetIndex = targetSegments.Length - 1;
                    while (sourceIndex >= 0 && targetIndex >= 0 &&
                           CanonicalName(sourceSegments[sourceIndex]) == CanonicalName(targetSegments[targetIndex]))
                    {
                        score++;
                        sourceIndex--;
                        targetIndex--;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                    }
                }

                return best;
            }

            private static string CanonicalPath(string path)
            {
                if (string.IsNullOrEmpty(path))
                {
                    return string.Empty;
                }

                return string.Join("/", path.Split('/').Select(CanonicalName));
            }

            private static string RelativePath(Transform root, Transform transform)
            {
                if (transform == root)
                {
                    return string.Empty;
                }

                var segments = new Stack<string>();
                Transform current = transform;
                while (current != null && current != root)
                {
                    segments.Push(current.name);
                    current = current.parent;
                }

                return string.Join("/", segments.ToArray());
            }

            private static string CanonicalName(string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return string.Empty;
                }

                var builder = new StringBuilder(name.Length);
                foreach (char character in name)
                {
                    if (char.IsLetterOrDigit(character))
                    {
                        builder.Append(char.ToLowerInvariant(character));
                    }
                }

                return builder.ToString();
            }
        }
    }
}
