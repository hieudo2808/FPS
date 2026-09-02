using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FPS.Editor
{
    /// <summary>
    /// Builds an isolated Operator proof that keeps Clove's Humanoid body
    /// animation and bakes only the weapon motion that Humanoid import drops.
    /// The baked motion targets the existing R_WeaponMaster transform that is
    /// already part of Clove's Avatar skeleton; no runtime IK is required.
    /// </summary>
    public static class ThirdPersonHybridWeaponAnimationExperiment
    {
        private const string MenuRoot =
            "FPS/Third Person/Operator Hybrid Animation Experiment/";
        private const string ClovePrefabPath =
            "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab";
        private const string ExperimentPrefabPath =
            "Assets/FPS/Features/Characters/Content/Players/Clove/"
            + "ClovePlayer_OperatorHybrid_Experiment.prefab";
        private const string SourceFolder =
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/S0/3P/Anims";
        private const string ExperimentFolder = SourceFolder + "/Experiment_1PStyle";
        private const string ModelRootName = "CS_Smonk_S0_Skelmesh.ao";
        private const string RightHandRelativePath =
            "Skeleton/Root/Splitter/Spine1/Spine2/Spine3/Spine4/"
            + "R_Clavicle/R_Shoulder/R_Elbow/R_Hand";
        private const string SourceWeaponRelativePath =
            "Skeleton/Root/Splitter/MasterWeaponAim/MasterWeapon/R_WeaponMaster";
        private const string MountBindingPath =
            ModelRootName + "/" + SourceWeaponRelativePath;
        private const string ExperimentBaseControllerPath =
            ExperimentFolder + "/Operator3P_HybridMasked.controller";
        private const string ExperimentMaskPath =
            ExperimentFolder + "/Operator3P_WeaponMount.mask";
        private const string ExperimentControllerPath =
            ExperimentFolder + "/CloveOperator_Hybrid_Experiment.overrideController";

        private static readonly ClipDefinition[] Clips =
        {
            new ClipDefinition(
                "Hold",
                SourceFolder + "/TP_Core_Boltsniper_S0_IdlePose_UB.fbx",
                ExperimentFolder + "/Operator_Hold_Generic.fbx",
                ExperimentFolder + "/Operator_Hold_Hybrid.anim"),
            new ClipDefinition(
                "Equip",
                SourceFolder + "/TP_Core_Boltsniper_S0_Equip_UB.fbx",
                ExperimentFolder + "/Operator_Equip_Generic.fbx",
                ExperimentFolder + "/Operator_Equip_Hybrid.anim"),
            new ClipDefinition(
                "Reload",
                SourceFolder + "/TP_Core_Boltsniper_S0_Reload_UB.fbx",
                ExperimentFolder + "/Operator_Reload_Generic.fbx",
                ExperimentFolder + "/Operator_Reload_Hybrid.anim")
        };

        [MenuItem(MenuRoot + "Build And Open Isolated Prefab")]
        public static void BuildAndOpen()
        {
            SaveAllOrThrow("before building the Operator hybrid experiment");
            StopAnimationPreview();

            GameObject source = PrefabUtility.LoadPrefabContents(ClovePrefabPath);
            GameObject experiment = null;
            try
            {
                experiment = UnityEngine.Object.Instantiate(source);
                experiment.name = "ClovePlayer_OperatorHybrid_Experiment";
                BuildExperiment(experiment);
                PrefabUtility.SaveAsPrefabAsset(experiment, ExperimentPrefabPath);
            }
            finally
            {
                if (experiment != null)
                    UnityEngine.Object.DestroyImmediate(experiment);
                PrefabUtility.UnloadPrefabContents(source);
            }

            SaveAllOrThrow("after building the Operator hybrid experiment");
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                ExperimentPrefabPath);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            AssetDatabase.OpenAsset(asset);
            EditorApplication.delayCall += () =>
            {
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
            };

            Debug.Log(
                "[OperatorHybrid] Opened isolated prefab. One Body Animator "
                + "uses synchronized Humanoid/body and Generic/R_WeaponMaster "
                + "layers. Source model and source clips are unchanged.");
        }

        [MenuItem(MenuRoot + "Validate Isolated Prefab")]
        public static void Validate()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ExperimentPrefabPath);
            try
            {
                Transform body = RequireChild(root.transform, "Body");
                Transform modelRoot = RequireChild(body, ModelRootName);
                Transform mount = RequireChild(modelRoot, SourceWeaponRelativePath);
                Transform weapon = FindDescendant(root.transform, "Operator_3P");
                Animator animator = body.GetComponent<Animator>();

                if (animator == null || animator.avatar == null)
                    throw new InvalidOperationException(
                        "The Clove Body Humanoid Animator/Avatar was not preserved.");
                if (AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)
                    != ExperimentControllerPath)
                {
                    throw new InvalidOperationException(
                        "The Body Animator does not use the isolated hybrid override.");
                }
                if (weapon == null || weapon.parent != mount)
                    throw new InvalidOperationException(
                        "Operator_3P is not authored under R_WeaponMaster.");
                if (!ApproximatelyIdentity(mount))
                    throw new InvalidOperationException(
                        "The authored Operator mount must remain identity at rest.");
                if (FindDescendant(root.transform, "OperatorWeaponDriverAnimator")
                    != null)
                    throw new InvalidOperationException(
                        "The obsolete root-motion weapon driver still exists.");

                ThirdPersonLeftHandIK leftHandIk =
                    root.GetComponent<ThirdPersonLeftHandIK>();
                if (leftHandIk != null && leftHandIk.enabled)
                    throw new InvalidOperationException(
                        "ThirdPersonLeftHandIK must stay disabled in this experiment.");

                ValidateMaskAndController();
                foreach (ClipDefinition definition in Clips)
                    ValidateHybridClip(definition);
                ValidateDirectSampleMotion(root);
                ValidateAnimatorControllerMotion(root);

                Debug.Log(
                    "[OperatorHybrid] Validation passed: source assets untouched; "
                    + "one Body Animator with synced Humanoid/body and Generic/weapon "
                    + "layers; controller-evaluated Hold/Equip/Reload move the weapon; "
                    + "no runtime IK and no secondary Animator.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem(MenuRoot + "Preview Hold At Start")]
        public static void PreviewHoldAtStart()
        {
            PreviewClip("Hold", 0f);
        }

        [MenuItem(MenuRoot + "Preview Equip At 35 Percent")]
        public static void PreviewEquipMidpoint()
        {
            PreviewClip("Equip", 0.35f);
        }

        [MenuItem(MenuRoot + "Preview Reload At 55 Percent")]
        public static void PreviewReloadMidpoint()
        {
            PreviewClip("Reload", 0.55f);
        }

        private static void PreviewClip(string label, float normalizedTime)
        {
            StopAnimationPreview();
            SaveAllOrThrow($"before previewing hybrid {label}");

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.assetPath != ExperimentPrefabPath)
            {
                throw new InvalidOperationException(
                    "Open the isolated Operator hybrid prefab before previewing.");
            }

            Transform body = RequireChild(stage.prefabContentsRoot.transform, "Body");
            AnimationClip clip = LoadHybridClip(label);
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            try
            {
                AnimationMode.SampleAnimationClip(
                    body.gameObject,
                    clip,
                    clip.length * Mathf.Clamp01(normalizedTime));
            }
            finally
            {
                AnimationMode.EndSampling();
            }

            Transform weapon = FindDescendant(
                stage.prefabContentsRoot.transform,
                "Operator_3P");
            Selection.activeTransform = weapon;
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.FrameSelected(false, true);
                sceneView.Repaint();
            }
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[OperatorHybrid] Previewing {label} at {normalizedTime:P0}. "
                + "AnimationMode is active; no sampled transform is saved.");
        }

        private static void BuildExperiment(GameObject root)
        {
            Transform body = RequireChild(root.transform, "Body");
            Transform modelRoot = RequireChild(body, ModelRootName);
            Transform weaponMaster = RequireChild(
                modelRoot,
                SourceWeaponRelativePath);
            Animator bodyAnimator = body.GetComponent<Animator>();
            if (bodyAnimator == null || bodyAnimator.avatar == null)
                throw new InvalidOperationException("Clove Body has no Humanoid Animator.");

            Transform operatorPresentation = FindDescendant(root.transform, "Operator_3P");
            if (operatorPresentation == null)
                throw new InvalidOperationException(
                    "Clove prefab has no authored Operator_3P presentation.");

            operatorPresentation.SetParent(weaponMaster, false);
            // Operator_3P uses the same model/pivot as 1P, so retain the same
            // authored convention below R_WeaponMaster instead of calibrating
            // against the previously incorrect R_Hand-local pose.
            operatorPresentation.localPosition = Vector3.zero;
            operatorPresentation.localRotation = Quaternion.Euler(0f, 270f, 0f);
            operatorPresentation.localScale = Vector3.one * 0.01f;

            var weaponClips = new Dictionary<string, AnimationClip>(
                StringComparer.Ordinal);
            foreach (ClipDefinition definition in Clips)
            {
                AnimationClip human = LoadImportedClip(definition.HumanoidPath);
                AnimationClip generic = LoadImportedClip(definition.GenericPath);
                AnimationClip weaponClip = BuildWeaponMotionClip(
                    definition,
                    human,
                    generic,
                    body);
                weaponClips.Add(definition.Label, weaponClip);
            }

            AnimatorController maskedBase = BuildMaskedBaseController(
                bodyAnimator.runtimeAnimatorController,
                weaponClips);
            AnimatorOverrideController controller = BuildExperimentController(
                bodyAnimator.runtimeAnimatorController,
                maskedBase);
            bodyAnimator.runtimeAnimatorController = controller;

            foreach (string weaponName in new[]
                     {
                         "Vandal_3P", "Classic_3P", "Operator_3P", "Odin_3P",
                         "Bucky_3P"
                     })
            {
                Transform presentation = FindDescendant(root.transform, weaponName);
                if (presentation != null)
                    presentation.gameObject.SetActive(weaponName == "Operator_3P");
            }

            ThirdPersonLeftHandIK leftHandIk =
                root.GetComponent<ThirdPersonLeftHandIK>();
            if (leftHandIk != null)
                leftHandIk.enabled = false;
            foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour.GetType().FullName
                    == "UnityEngine.Animations.Rigging.RigBuilder")
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static AnimationClip BuildWeaponMotionClip(
            ClipDefinition definition,
            AnimationClip human,
            AnimationClip generic,
            Transform targetBody)
        {
            if (!human.humanMotion)
                throw new InvalidOperationException(
                    $"{human.name} must remain a Humanoid clip.");
            if (generic.humanMotion)
                throw new InvalidOperationException(
                    $"{generic.name} must remain the isolated Generic source copy.");

            AnimationClip destination = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                definition.HybridPath);
            if (destination == null)
            {
                destination = new AnimationClip();
                AssetDatabase.CreateAsset(destination, definition.HybridPath);
            }

            var cleanClip = new AnimationClip
            {
                frameRate = human.frameRate,
                wrapMode = human.wrapMode
            };
            EditorUtility.CopySerialized(cleanClip, destination);
            UnityEngine.Object.DestroyImmediate(cleanClip);
            destination.name = System.IO.Path.GetFileNameWithoutExtension(
                definition.HybridPath);
            RemoveAllTransformCurves(destination);
            AnimationUtility.SetAnimationClipSettings(
                destination,
                AnimationUtility.GetAnimationClipSettings(human));

            GameObject genericAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
                definition.GenericPath);
            if (genericAsset == null)
                throw new InvalidOperationException(
                    $"Missing Generic model: {definition.GenericPath}");

            GameObject sourceInstance = UnityEngine.Object.Instantiate(genericAsset);
            sourceInstance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Transform targetModelRoot = RequireChild(
                    targetBody,
                    ModelRootName);
                string[] sourcePaths = AnimationUtility.GetCurveBindings(generic)
                    .Where(binding => binding.type == typeof(Transform))
                    .Select(binding => binding.path)
                    .Distinct(StringComparer.Ordinal)
                    .Where(IsOperatorUpperBodyPath)
                    .Where(path => sourceInstance.transform.Find(path) != null)
                    .Where(path => targetModelRoot.Find(path) != null)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                if (!sourcePaths.Contains(
                        SourceWeaponRelativePath,
                        StringComparer.Ordinal)
                    || !sourcePaths.Contains(
                        RightHandRelativePath,
                        StringComparer.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Generic Operator source is missing R_Hand/R_WeaponMaster.");
                }

                var pathCurves = sourcePaths.ToDictionary(
                    path => path,
                    path => new RetargetedPathCurveSet(
                        ModelRootName + "/" + path,
                        RequireChild(sourceInstance.transform, path),
                        RequireChild(targetModelRoot, path)),
                    StringComparer.Ordinal);
                int frameCount = Mathf.Max(
                    1,
                    Mathf.CeilToInt(human.length * human.frameRate));
                for (int frame = 0; frame <= frameCount; frame++)
                {
                    float humanTime = Mathf.Min(
                        human.length,
                        frame / human.frameRate);
                    float normalizedTime = human.length > Mathf.Epsilon
                        ? humanTime / human.length
                        : 0f;
                    float genericTime = generic.length * normalizedTime;
                    generic.SampleAnimation(sourceInstance, genericTime);
                    foreach (RetargetedPathCurveSet curves in pathCurves.Values)
                        curves.Sample(humanTime);
                }

                foreach (RetargetedPathCurveSet curves in pathCurves.Values)
                    curves.WriteTo(destination);
                destination.EnsureQuaternionContinuity();
                EditorUtility.SetDirty(destination);
                Debug.Log(
                    $"[OperatorHybrid] {definition.Label}: baked "
                    + $"{pathCurves.Count} Generic upper-body/weapon paths "
                    + "against Clove bind transforms; original clips unchanged.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceInstance);
            }

            return destination;
        }

        private static AnimatorController BuildMaskedBaseController(
            RuntimeAnimatorController sourceController,
            IReadOnlyDictionary<string, AnimationClip> weaponClips)
        {
            var currentOverride = sourceController as AnimatorOverrideController;
            RuntimeAnimatorController unwrapped = currentOverride != null
                ? currentOverride.runtimeAnimatorController
                : sourceController;
            string sourcePath = AssetDatabase.GetAssetPath(unwrapped);
            if (string.IsNullOrEmpty(sourcePath))
                throw new InvalidOperationException(
                    "Operator base AnimatorController is not an asset.");

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    ExperimentBaseControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ExperimentBaseControllerPath);
            }
            if (!AssetDatabase.CopyAsset(sourcePath, ExperimentBaseControllerPath))
                throw new InvalidOperationException(
                    "Could not clone the Operator AnimatorController.");

            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    ExperimentBaseControllerPath);
            if (controller == null || controller.layers.Length < 2)
                throw new InvalidOperationException(
                    "Operator controller has no Upper Body Gun Pose layer.");

            AnimatorControllerLayer[] layers = controller.layers;
            int upperBodyIndex = Array.FindIndex(
                layers,
                layer => layer.name == "Upper Body Gun Pose");
            AnimatorControllerLayer upperBody = upperBodyIndex >= 0
                ? layers[upperBodyIndex]
                : null;
            if (upperBody == null || upperBody.avatarMask == null)
                throw new InvalidOperationException(
                    "Upper Body Gun Pose has no source AvatarMask.");

            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                ExperimentMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, ExperimentMaskPath);
            }
            EditorUtility.CopySerialized(upperBody.avatarMask, mask);
            mask.name = System.IO.Path.GetFileNameWithoutExtension(
                ExperimentMaskPath);
            for (int part = 0; part < mask.humanoidBodyPartCount; part++)
            {
                mask.SetHumanoidBodyPartActive(
                    (AvatarMaskBodyPart)part,
                    false);
            }
            string[] maskPaths = GetTransformMaskPaths(weaponClips.Values);
            mask.transformCount = maskPaths.Length;
            for (int index = 0; index < maskPaths.Length; index++)
            {
                mask.SetTransformPath(index, maskPaths[index]);
                mask.SetTransformActive(index, true);
            }
            EditorUtility.SetDirty(mask);

            controller.AddLayer("Operator Generic Upper Body");
            layers = controller.layers;
            int weaponLayerIndex = layers.Length - 1;
            AnimatorControllerLayer weaponLayer = layers[weaponLayerIndex];
            weaponLayer.defaultWeight = 1f;
            weaponLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            weaponLayer.avatarMask = mask;
            weaponLayer.syncedLayerIndex = upperBodyIndex;
            weaponLayer.syncedLayerAffectsTiming = false;

            var stateByName = upperBody.stateMachine.states.ToDictionary(
                child => child.state.name,
                child => child.state,
                StringComparer.Ordinal);
            weaponLayer.SetOverrideMotion(
                stateByName["Operator Hold"],
                weaponClips["Hold"]);
            weaponLayer.SetOverrideMotion(
                stateByName["Operator Equip"],
                weaponClips["Equip"]);
            weaponLayer.SetOverrideMotion(
                stateByName["Operator Reload"],
                weaponClips["Reload"]);
            layers[weaponLayerIndex] = weaponLayer;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimatorOverrideController BuildExperimentController(
            RuntimeAnimatorController sourceController,
            AnimatorController maskedBase)
        {
            var currentOverride = sourceController as AnimatorOverrideController;
            var currentValues = new Dictionary<AnimationClip, AnimationClip>();
            if (currentOverride != null)
            {
                var currentPairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                currentOverride.GetOverrides(currentPairs);
                foreach (KeyValuePair<AnimationClip, AnimationClip> pair in currentPairs)
                    currentValues[pair.Key] = pair.Value;
            }

            AnimatorOverrideController destination =
                AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                    ExperimentControllerPath);
            if (destination == null)
            {
                destination = new AnimatorOverrideController(maskedBase);
                AssetDatabase.CreateAsset(destination, ExperimentControllerPath);
            }
            else
            {
                destination.runtimeAnimatorController = maskedBase;
            }

            var definitionsByHuman = Clips.ToDictionary(
                definition => LoadImportedClip(definition.HumanoidPath),
                definition => definition);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            destination.GetOverrides(overrides);
            var matched = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < overrides.Count; index++)
            {
                AnimationClip key = overrides[index].Key;
                if (definitionsByHuman.TryGetValue(key, out ClipDefinition definition))
                {
                    overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(
                        key,
                        null);
                    matched.Add(definition.Label);
                }
                else if (currentValues.TryGetValue(key, out AnimationClip existing)
                    && existing != null)
                {
                    overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(
                        key,
                        existing);
                }
            }

            string[] missing = Clips
                .Select(definition => definition.Label)
                .Where(label => !matched.Contains(label))
                .ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException(
                    "The Operator controller does not expose override keys for: "
                    + string.Join(", ", missing));

            destination.ApplyOverrides(overrides);
            EditorUtility.SetDirty(destination);
            return destination;
        }

        private static void ValidateMaskAndController()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                ExperimentMaskPath);
            bool hasActiveMount = false;
            if (mask != null)
            {
                for (int index = 0; index < mask.transformCount; index++)
                {
                    if (mask.GetTransformPath(index) == MountBindingPath
                        && mask.GetTransformActive(index))
                    {
                        hasActiveMount = true;
                        break;
                    }
                }
            }
            if (!hasActiveMount)
            {
                throw new InvalidOperationException(
                    "Operator AvatarMask does not admit the authored mount path.");
            }

            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    ExperimentBaseControllerPath);
            if (controller == null)
                throw new InvalidOperationException(
                    "Missing isolated Operator AnimatorController.");
            int upperBodyIndex = Array.FindIndex(
                controller.layers,
                layer => layer.name == "Upper Body Gun Pose");
            AnimatorControllerLayer weaponLayer = controller.layers.FirstOrDefault(
                layer => layer.name == "Operator Generic Upper Body");
            if (upperBodyIndex < 0
                || weaponLayer == null
                || weaponLayer.avatarMask != mask
                || weaponLayer.syncedLayerIndex != upperBodyIndex)
                throw new InvalidOperationException(
                    "Operator Generic Upper Body is not synced/masked to Upper Body Gun Pose.");
        }

        private static void ValidateHybridClip(ClipDefinition definition)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                definition.HybridPath);
            if (clip == null || clip.humanMotion)
                throw new InvalidOperationException(
                    $"{definition.Label} weapon clip must be Generic transform-only.");

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings.Any(binding => binding.type == typeof(Animator)))
                throw new InvalidOperationException(
                    $"{clip.name} must not contain Humanoid muscle curves.");

            EditorCurveBinding[] direct = bindings
                .Where(binding => binding.type == typeof(Transform))
                .ToArray();
            string leftHandPath = ModelRootName + "/"
                + "Skeleton/Root/Splitter/Spine1/Spine2/Spine3/Spine4/"
                + "L_Clavicle/L_Shoulder/L_Elbow/L_Hand";
            if (direct.Length < 100
                || !direct.Any(binding => binding.path == MountBindingPath)
                || !direct.Any(binding => binding.path
                    == ModelRootName + "/" + RightHandRelativePath)
                || !direct.Any(binding => binding.path == leftHandPath))
            {
                throw new InvalidOperationException(
                    $"{clip.name} is missing path-bound hands/weapon animation.");
            }
            if (direct.Any(binding => binding.propertyName.Contains("Scale")))
                throw new InvalidOperationException(
                    $"{clip.name} must not animate weapon scale.");
        }

        private static void ValidateDirectSampleMotion(GameObject prefabRoot)
        {
            foreach (ClipDefinition definition in Clips)
            {
                GameObject sample = UnityEngine.Object.Instantiate(prefabRoot);
                sample.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    Transform body = RequireChild(sample.transform, "Body");
                    Transform modelRoot = RequireChild(body, ModelRootName);
                    Transform hand = RequireChild(modelRoot, RightHandRelativePath);
                    Transform mount = RequireChild(
                        modelRoot,
                        SourceWeaponRelativePath);
                    Transform weapon = FindDescendant(sample.transform, "Operator_3P");
                    AnimationClip clip = LoadHybridClip(definition.Label);
                    AnimationClip human = LoadImportedClip(
                        definition.HumanoidPath);
                    foreach (float normalizedTime in new[] { 0f, 0.35f, 0.55f, 0.95f })
                    {
                        human.SampleAnimation(
                            body.gameObject,
                            human.length * normalizedTime);
                        clip.SampleAnimation(
                            body.gameObject,
                            clip.length * normalizedTime);
                        AssertFinite(mount, definition.Label, normalizedTime);
                        AssertFinite(weapon, definition.Label, normalizedTime);
                        float handDistance = Vector3.Distance(
                            weapon.position,
                            hand.position);
                        if (handDistance > 1f)
                        {
                            throw new InvalidOperationException(
                                $"{definition.Label} {normalizedTime:P0}: Operator "
                                + $"origin is {handDistance:F3}m from R_Hand.");
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(sample);
                }
            }
        }

        private static void ValidateAnimatorControllerMotion(GameObject prefabRoot)
        {
            GameObject sample = UnityEngine.Object.Instantiate(prefabRoot);
            sample.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                sample.SetActive(true);
                Transform body = RequireChild(sample.transform, "Body");
                Transform modelRoot = RequireChild(body, ModelRootName);
                Transform hand = RequireChild(modelRoot, RightHandRelativePath);
                Transform mount = RequireChild(
                    modelRoot,
                    SourceWeaponRelativePath);
                Transform weapon = FindDescendant(sample.transform, "Operator_3P");
                Animator animator = body.GetComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);

                ValidateAnimatorState(
                    animator,
                    mount,
                    weapon,
                    hand,
                    "Operator Equip",
                    LoadHybridClip("Equip"),
                    0.35f);
                ValidateAnimatorState(
                    animator,
                    mount,
                    weapon,
                    hand,
                    "Operator Reload",
                    LoadHybridClip("Reload"),
                    0.55f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sample);
            }
        }

        private static void ValidateAnimatorState(
            Animator animator,
            Transform mount,
            Transform weapon,
            Transform hand,
            string stateName,
            AnimationClip clip,
            float normalizedTime)
        {
            animator.Play(stateName, 1, normalizedTime);
            animator.Update(0.00001f);
            Vector3 expectedPosition = EvaluatePosition(
                clip,
                clip.length * normalizedTime);
            Quaternion expectedRotation = EvaluateRotation(
                clip,
                clip.length * normalizedTime);
            float positionError = Vector3.Distance(
                mount.localPosition,
                expectedPosition);
            float rotationError = Quaternion.Angle(
                mount.localRotation,
                expectedRotation);
            if (positionError > 0.001f || rotationError > 1f)
            {
                throw new InvalidOperationException(
                    $"{stateName}: Animator/AvatarMask blocked or altered the "
                    + $"weapon mount (position error {positionError:F6}, "
                    + $"rotation error {rotationError:F3}).");
            }
            if (Vector3.Distance(weapon.position, hand.position) > 1f)
                throw new InvalidOperationException(
                    $"{stateName}: controller evaluation detached Operator from R_Hand.");
        }

        private static Vector3 EvaluatePosition(AnimationClip clip, float time)
        {
            return new Vector3(
                EvaluateCurve(clip, "m_LocalPosition.x", time),
                EvaluateCurve(clip, "m_LocalPosition.y", time),
                EvaluateCurve(clip, "m_LocalPosition.z", time));
        }

        private static Quaternion EvaluateRotation(AnimationClip clip, float time)
        {
            Quaternion rotation = new Quaternion(
                EvaluateCurve(clip, "m_LocalRotation.x", time),
                EvaluateCurve(clip, "m_LocalRotation.y", time),
                EvaluateCurve(clip, "m_LocalRotation.z", time),
                EvaluateCurve(clip, "m_LocalRotation.w", time));
            return Quaternion.Normalize(rotation);
        }

        private static float EvaluateCurve(
            AnimationClip clip,
            string propertyName,
            float time)
        {
            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(
                MountBindingPath,
                typeof(Transform),
                propertyName);
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null)
                throw new InvalidOperationException(
                    $"{clip.name} is missing {propertyName}.");
            return curve.Evaluate(time);
        }

        private static LocalPose MeasureApprovedWeaponOffset(
            GameObject sourcePrefab,
            AnimationClip holdClip)
        {
            GameObject measurement = UnityEngine.Object.Instantiate(sourcePrefab);
            measurement.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Transform body = RequireChild(measurement.transform, "Body");
                Transform modelRoot = RequireChild(body, ModelRootName);
                Transform weaponMaster = RequireChild(
                    modelRoot,
                    SourceWeaponRelativePath);
                Transform weapon = FindDescendant(
                    measurement.transform,
                    "Operator_3P");
                if (weapon == null)
                    throw new InvalidOperationException(
                        "Clove has no Operator_3P presentation to calibrate.");

                holdClip.SampleAnimation(body.gameObject, 0f);
                Matrix4x4 offset =
                    weaponMaster.worldToLocalMatrix * weapon.localToWorldMatrix;
                return LocalPose.FromMatrix(offset);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(measurement);
            }
        }

        private static Matrix4x4 Basis(Transform transform)
        {
            return Matrix4x4.TRS(
                Vector3.zero,
                transform.rotation,
                transform.lossyScale);
        }

        private static void RemoveAllTransformCurves(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type == typeof(Transform))
                    AnimationUtility.SetEditorCurve(clip, binding, null);
            }
        }

        private static bool IsOperatorUpperBodyPath(string path)
        {
            return path.StartsWith(
                    "Skeleton/Root/Splitter/Spine1",
                    StringComparison.Ordinal)
                || path.StartsWith(
                    "Skeleton/Root/Splitter/MasterWeaponAim",
                    StringComparison.Ordinal);
        }

        private static string[] GetTransformMaskPaths(
            IEnumerable<AnimationClip> clips)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal)
            {
                string.Empty
            };
            foreach (string bindingPath in clips
                         .SelectMany(AnimationUtility.GetCurveBindings)
                         .Where(binding => binding.type == typeof(Transform))
                         .Select(binding => binding.path)
                         .Distinct(StringComparer.Ordinal))
            {
                string[] segments = bindingPath.Split('/');
                string current = string.Empty;
                foreach (string segment in segments)
                {
                    current = string.IsNullOrEmpty(current)
                        ? segment
                        : current + "/" + segment;
                    paths.Add(current);
                }
            }
            return paths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        private static AnimationClip LoadImportedClip(string path)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => !candidate.name.StartsWith(
                    "__preview__",
                    StringComparison.Ordinal));
            return clip != null
                ? clip
                : throw new InvalidOperationException(
                    $"No imported AnimationClip exists at {path}.");
        }

        private static AnimationClip LoadHybridClip(string label)
        {
            ClipDefinition definition = Clips.First(candidate =>
                candidate.Label == label);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                definition.HybridPath);
            return clip != null
                ? clip
                : throw new InvalidOperationException(
                    $"Missing hybrid clip: {definition.HybridPath}");
        }

        private static void StopAnimationPreview()
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
        }

        private static Transform RequireChild(Transform parent, string path)
        {
            Transform child = parent.Find(path);
            return child != null
                ? child
                : throw new InvalidOperationException(
                    $"Missing authored path '{path}' under '{parent.name}'.");
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == name);
        }

        private static bool ApproximatelyIdentity(Transform transform)
        {
            return transform.localPosition.sqrMagnitude < 0.00000001f
                && Quaternion.Angle(transform.localRotation, Quaternion.identity) < 0.01f
                && Vector3.Distance(transform.localScale, Vector3.one) < 0.0001f;
        }

        private static void AssertFinite(
            Transform transform,
            string label,
            float normalizedTime)
        {
            if (transform == null
                || !IsFinite(transform.position)
                || !IsFinite(transform.localScale)
                || !IsFinite(transform.rotation))
            {
                throw new InvalidOperationException(
                    $"{label} {normalizedTime:P0}: invalid transform sample.");
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x)
                && float.IsFinite(value.y)
                && float.IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return float.IsFinite(value.x)
                && float.IsFinite(value.y)
                && float.IsFinite(value.z)
                && float.IsFinite(value.w);
        }

        private static void SaveAllOrThrow(string phase)
        {
            if (!EditorSceneManager.SaveOpenScenes())
                throw new InvalidOperationException(
                    $"Open scenes could not be saved {phase}.");
            AssetDatabase.SaveAssets();
        }

        private readonly struct ClipDefinition
        {
            public ClipDefinition(
                string label,
                string humanoidPath,
                string genericPath,
                string hybridPath)
            {
                Label = label;
                HumanoidPath = humanoidPath;
                GenericPath = genericPath;
                HybridPath = hybridPath;
            }

            public string Label { get; }
            public string HumanoidPath { get; }
            public string GenericPath { get; }
            public string HybridPath { get; }
        }

        private readonly struct LocalPose
        {
            private LocalPose(
                Vector3 position,
                Quaternion rotation,
                Vector3 scale)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 Scale { get; }

            public static LocalPose FromMatrix(Matrix4x4 matrix)
            {
                if (!matrix.ValidTRS())
                    throw new InvalidOperationException(
                        "Approved Operator mount is not valid TRS.");
                return new LocalPose(
                    matrix.GetColumn(3),
                    matrix.rotation,
                    matrix.lossyScale);
            }
        }

        private sealed class RetargetedPathCurveSet
        {
            private readonly string path;
            private readonly Transform source;
            private readonly float unitScale;
            private readonly AnimationCurve positionX = new AnimationCurve();
            private readonly AnimationCurve positionY = new AnimationCurve();
            private readonly AnimationCurve positionZ = new AnimationCurve();
            private readonly AnimationCurve rotationX = new AnimationCurve();
            private readonly AnimationCurve rotationY = new AnimationCurve();
            private readonly AnimationCurve rotationZ = new AnimationCurve();
            private readonly AnimationCurve rotationW = new AnimationCurve();
            private Quaternion previousRotation;
            private bool hasPreviousRotation;

            public RetargetedPathCurveSet(
                string path,
                Transform source,
                Transform target)
            {
                this.path = path;
                this.source = source;
                float sourceWorldScale = Mathf.Max(
                    Mathf.Abs(source.lossyScale.x),
                    0.000001f);
                float targetWorldScale = Mathf.Max(
                    Mathf.Abs(target.lossyScale.x),
                    0.000001f);
                unitScale = sourceWorldScale / targetWorldScale;
            }

            public void Sample(float time)
            {
                // The standalone Generic FBX is already evaluated at the
                // intended animation pose; Clove contains the same path-bound
                // skeleton at 100x root scale. Copy absolute local rotations
                // and convert only translation units (1 -> 0.01).
                Vector3 position = source.localPosition * unitScale;
                Quaternion rotation = source.localRotation;
                if (hasPreviousRotation
                    && Quaternion.Dot(previousRotation, rotation) < 0f)
                {
                    rotation = new Quaternion(
                        -rotation.x,
                        -rotation.y,
                        -rotation.z,
                        -rotation.w);
                }
                previousRotation = rotation;
                hasPreviousRotation = true;

                AddKey(positionX, time, position.x);
                AddKey(positionY, time, position.y);
                AddKey(positionZ, time, position.z);
                AddKey(rotationX, time, rotation.x);
                AddKey(rotationY, time, rotation.y);
                AddKey(rotationZ, time, rotation.z);
                AddKey(rotationW, time, rotation.w);
            }

            public void WriteTo(AnimationClip clip)
            {
                SetCurve(clip, "m_LocalPosition.x", positionX);
                SetCurve(clip, "m_LocalPosition.y", positionY);
                SetCurve(clip, "m_LocalPosition.z", positionZ);
                SetCurve(clip, "m_LocalRotation.x", rotationX);
                SetCurve(clip, "m_LocalRotation.y", rotationY);
                SetCurve(clip, "m_LocalRotation.z", rotationZ);
                SetCurve(clip, "m_LocalRotation.w", rotationW);
            }

            private void SetCurve(
                AnimationClip clip,
                string propertyName,
                AnimationCurve curve)
            {
                SetLinearTangents(curve);
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        path,
                        typeof(Transform),
                        propertyName),
                    curve);
            }

            private static void AddKey(
                AnimationCurve curve,
                float time,
                float value)
            {
                curve.AddKey(new Keyframe(time, value));
            }

            private static void SetLinearTangents(AnimationCurve curve)
            {
                for (int index = 0; index < curve.length; index++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(
                        curve,
                        index,
                        AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(
                        curve,
                        index,
                        AnimationUtility.TangentMode.Linear);
                }
            }
        }
    }
}
