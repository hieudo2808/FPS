using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FPS.Editor
{
    /// <summary>
    /// Authors Operator as a path-bound Generic animation profile while leaving
    /// the imported Clove model and all source animation FBXs untouched.
    /// Other weapon presentations are left untouched.
    /// </summary>
    public static class OperatorGenericThirdPersonSetup
    {
        private const string MenuRoot =
            "FPS/Third Person/Operator Generic Path-Bound/";
        private const string ClovePrefabPath =
            "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab";
        private const string BaseControllerPath =
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/Operator3P_Body.controller";
        private const string BaseGunControllerPath =
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/Operator3P_Gun.controller";
        private const string CloveGunControllerPath =
            "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/CloveOperator3P_GNTP.controller";
        private const string ReloadCompleteParameter = "ReloadComplete";
        private const string CoreAnimationFolder =
            "Assets/FPS/Features/Characters/Animation/Content/3P";
        private const string OperatorAnimationFolder =
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/S0/3P/Anims";
        private const string ProductionFolder =
            OperatorAnimationFolder + "/Clove_GenericPathBound";
        private const string GunFirePath =
            OperatorAnimationFolder + "/GNTP_Core_Boltsniper_S0_Fire.fbx";
        private const string CloveAimPosePath =
            CoreAnimationFolder + "/Clove/CloveOperatorAimPose.anim";
        private const string ControllerPath =
            ProductionFolder + "/CloveOperator_GenericPathBound.controller";
        private const string UpperBodyMaskPath =
            ProductionFolder + "/CloveOperator_GenericUpperBody.mask";
        private const string UpperBodyNoFingersMaskPath =
            ProductionFolder + "/CloveOperator_GenericUpperBodyNoFingers.mask";
        private const string ModelRootName = "CS_Smonk_S0_Skelmesh.ao";
        private const string SkeletonRootPath = "Skeleton";
        private const string UpperBodyRootPath =
            "Skeleton/Root/Splitter/Spine1";
        private const string WeaponRootPath =
            "Skeleton/Root/Splitter/MasterWeaponAim";
        private const string WeaponMasterPath =
            WeaponRootPath + "/MasterWeapon/R_WeaponMaster";
        private const string RightHandPath =
            UpperBodyRootPath + "/Spine2/Spine3/Spine4/R_Clavicle/"
            + "R_Shoulder/R_Elbow/R_Hand";
        private const string LeftHandPath =
            UpperBodyRootPath + "/Spine2/Spine3/Spine4/L_Clavicle/"
            + "L_Shoulder/L_Elbow/L_Hand";
        private const string RightEyePath =
            UpperBodyRootPath + "/Spine2/Spine3/Spine4/Neck/Head/R_Eyeball";
        private const float OperatorAimMinimumEyeRelief = 0.12f;
        private const float OperatorAimMaximumEyeRelief = 0.32f;
        private const float OperatorAimMaximumSightLineMiss = 0.03f;
        private const float OperatorAimMinimumForwardAlignment = 0.9f;
        private const float OperatorAimEyeRelief = 0.22f;

        private static readonly MotionDefinition[] Motions =
        {
            Core("Idle", "TP_Core_Idle_LB.fbx", MotionRegion.FullSkeleton),
            Core("Walk", "TP_Core_WalkN_LB.fbx", MotionRegion.FullSkeleton),
            Core("Run", "TP_Core_RunN_LB.fbx", MotionRegion.FullSkeleton),
            Core("Jump", "TP_Core_Jump_LB.fbx", MotionRegion.FullSkeleton),
            Core("Fall", "TP_Core_Falling_LB.fbx", MotionRegion.FullSkeleton),
            Core("Land", "TP_Core_JumpLand_LB.fbx", MotionRegion.FullSkeleton),
            Operator("Hold", "TP_Core_Boltsniper_S0_IdlePose_UB.fbx"),
            Operator("Equip", "TP_Core_Boltsniper_S0_Equip_UB.fbx"),
            Operator("Reload", "TP_Core_Boltsniper_S0_Reload_UB.fbx"),
            Operator("Fire", "TP_Core_Boltsniper_S0_Fire_UB.fbx"),
            // AimN means the neutral/forward ADS state in this eight-way set.
            // Its raw Generic transform data is not directly compatible with
            // Clove's authored hierarchy, so start from the verified Hold pose
            // and bake an eye-aligned ADS correction below.
            Operator("Aim", "TP_Core_Boltsniper_S0_IdlePose_UB.fbx"),
            Core("WalkAdd", "TP_Core_WalkAddN_UB.fbx", MotionRegion.UpperBody),
            Core("RunAdd", "TP_Core_RunAddN_UB.fbx", MotionRegion.UpperBody),
            Core("JumpAdd", "TP_Core_JumpAdd_UB.fbx", MotionRegion.UpperBody),
            Core("LandAdd", "TP_Core_JumpLandAdd_UB.fbx", MotionRegion.UpperBody)
        };

        [MenuItem(MenuRoot + "Build And Apply To Clove Prefab")]
        public static void BuildAndApply()
        {
            SaveAllOrThrow("before Operator Generic authoring");
            StopAnimationPreview();
            EnsureFolder(ProductionFolder);
            NormalizeUnifiedFireFlow(
                RequireController(BaseControllerPath),
                "Upper Body Gun Pose",
                "Operator Fire",
                "Operator Fire Zoomed");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(
                ClovePrefabPath);

            GameObject root = PrefabUtility.LoadPrefabContents(ClovePrefabPath);
            try
            {
                Transform body = RequireDescendant(root.transform, "Body");
                Transform modelRoot = RequireDescendant(body, ModelRootName);
                string[] originalSkeletonPose = CaptureSkeletonPoseOverrides(body);
                var generated = new Dictionary<string, AnimationClip>(
                    StringComparer.Ordinal);

                foreach (MotionDefinition motion in Motions)
                {
                    AnimationClip clip = BuildPathBoundClip(
                        motion,
                        body,
                        modelRoot);
                    generated.Add(motion.Label, clip);
                }

                AnimatorController controller = BuildController(
                    body,
                    generated);
                AuthorOperatorGunControllers();
                AuthorPrefab(root, body, modelRoot, controller);
                BakeOperatorAdsPose(root, generated["Aim"]);
                AssertSkeletonPoseUnchanged(body, originalSkeletonPose);
                PrefabUtility.SaveAsPrefabAsset(root, ClovePrefabPath);
                DeleteObsoleteProductionAssets();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            ThirdPersonWeaponAnimationSyncSetup.ApplyToAuthoredControllers();
            SaveAllOrThrow("after Operator Generic authoring");
            Validate();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(
                ClovePrefabPath);
            Debug.Log(
                "[OperatorGeneric] Production profile authored. Operator uses "
                + "Avatar=None with direct body/hand/weapon curves. Other weapon "
                + "presentations remain untouched. No runtime IK or "
                + "runtime-created component is involved.");
        }

        [MenuItem(MenuRoot + "Validate Authored Production Setup")]
        public static void Validate()
        {
            SaveAllOrThrow("before Operator Generic validation");
            GameObject root = PrefabUtility.LoadPrefabContents(ClovePrefabPath);
            try
            {
                Transform body = RequireDescendant(root.transform, "Body");
                Transform modelRoot = RequireDescendant(body, ModelRootName);
                Transform master = RequireChild(modelRoot, WeaponMasterPath);
                Transform weapon = RequireDescendant(root.transform, "Operator_3P");
                Animator animator = body.GetComponent<Animator>();
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    ControllerPath);

                if (animator == null)
                    throw new InvalidOperationException("Clove Body has no Animator.");
                if (animator.avatar != null)
                    throw new InvalidOperationException(
                        "The authored Operator preview must use Avatar=None.");
                if (animator.runtimeAnimatorController != controller)
                    throw new InvalidOperationException(
                        "Clove Body does not use the Generic Operator controller.");
                if (weapon.parent != master)
                    throw new InvalidOperationException(
                        "Operator_3P is not authored below R_WeaponMaster.");
                MountPose expectedMount = CalculateOperatorMountPose(
                    root,
                    Motions.First(motion => motion.Label == "Hold").LoadGenerated());
                if (Vector3.Distance(
                        weapon.localPosition,
                        expectedMount.Position) > 0.00001f
                    || Quaternion.Angle(
                        weapon.localRotation,
                        expectedMount.Rotation) > 0.01f
                    || Vector3.Distance(
                        weapon.localScale,
                        expectedMount.Scale) > 0.00001f)
                {
                    throw new InvalidOperationException(
                        "Operator_3P does not use the verified two-hand mount fit.");
                }

                ValidatePresentations(root, controller);
                ValidateController(body, controller);
                ValidateOperatorGunControllers(root);
                ValidateSerializedSkeletonPose(body);
                ValidateControllerEvaluation(root, body, modelRoot, animator);

                Debug.Log(
                    "[OperatorGeneric] Validation passed: fixed authored profile, "
                    + "direct Generic transform curves for locomotion/actions, "
                    + "Operator under R_WeaponMaster, original assets unchanged, "
                    + "eye-aligned two-hand ADS and one regular Fire path.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            SaveAllOrThrow("after Operator Generic validation");
        }

        [MenuItem(MenuRoot + "Repair Movement Additives")]
        public static void RepairMovementAdditives()
        {
            SaveAllOrThrow("before Operator movement additive repair");
            StopAnimationPreview();
            EnsureFolder(ProductionFolder);

            GameObject root = PrefabUtility.LoadPrefabContents(ClovePrefabPath);
            try
            {
                Transform body = RequireDescendant(root.transform, "Body");
                Transform modelRoot = RequireDescendant(body, ModelRootName);
                foreach (MotionDefinition motion in Motions.Where(
                             motion => motion.Label.EndsWith(
                                 "Add",
                                 StringComparison.Ordinal)))
                {
                    BuildPathBoundClip(motion, body, modelRoot);
                }

                Func<string, bool> includeUpperBody =
                    BuildUpperBodyMaskPredicate(body);
                AvatarMask upperBody = BuildTransformMask(
                    UpperBodyMaskPath,
                    body,
                    includeUpperBody);
                AvatarMask upperBodyNoFingers = BuildTransformMask(
                    UpperBodyNoFingersMaskPath,
                    body,
                    path => includeUpperBody(path) && !IsFingerPath(path));
                AnimatorController controller = RequireController(ControllerPath);
                AnimatorControllerLayer[] layers = controller.layers;
                if (layers.Length < 5)
                {
                    throw new InvalidOperationException(
                        "Operator controller no longer has its five authored layers.");
                }
                layers[1].avatarMask = upperBody;
                layers[2].avatarMask = upperBodyNoFingers;
                layers[3].avatarMask = upperBodyNoFingers;
                controller.layers = layers;
                EditorUtility.SetDirty(controller);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            SaveAllOrThrow("after Operator movement additive repair");
            Debug.Log(
                "[OperatorGeneric] Repaired movement additive clips without "
                + "external FBX reference poses.");
        }

        [MenuItem(MenuRoot + "Repair Reload Completion Transitions")]
        public static void RepairReloadCompletionTransitions()
        {
            SaveAllOrThrow("before Operator reload transition repair");
            ThirdPersonWeaponAnimationSyncSetup.ApplyToAuthoredControllers();
            SaveAllOrThrow("after Operator reload transition repair");
            Debug.Log(
                "[OperatorGeneric] All authored 3P actions were synchronized; "
                + "Operator Reload remains driven by ReloadComplete.");
        }

        [MenuItem(MenuRoot + "Open Clove Prefab")]
        public static void OpenClovePrefab()
        {
            SaveAllOrThrow("before opening the authored Clove prefab");
            StopAnimationPreview();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                ClovePrefabPath);
            AssetDatabase.OpenAsset(prefab);
        }

        private static AnimationClip BuildPathBoundClip(
            MotionDefinition motion,
            Transform body,
            Transform targetModelRoot)
        {
            string genericPath = ProductionFolder + "/"
                + motion.Label + "_Source_Generic.fbx";
            GameObject genericSource =
                AssetDatabase.LoadAssetAtPath<GameObject>(genericPath);
            if (genericSource != null)
            {
                AnimationClip expectedSource = LoadImportedClip(motion.SourcePath);
                AnimationClip currentSource = LoadImportedClip(genericPath);
                if (!string.Equals(
                        expectedSource.name,
                        currentSource.name,
                        StringComparison.Ordinal))
                {
                    if (!AssetDatabase.DeleteAsset(genericPath))
                    {
                        throw new InvalidOperationException(
                            $"Could not replace stale Generic source {genericPath}.");
                    }
                    genericSource = null;
                }
            }

            if (genericSource == null)
            {
                if (!AssetDatabase.CopyAsset(motion.SourcePath, genericPath))
                {
                    throw new InvalidOperationException(
                        $"Could not copy Generic source from {motion.SourcePath}.");
                }
            }

            ModelImporter importer = AssetImporter.GetAtPath(genericPath)
                as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException(
                    $"No ModelImporter exists for {genericPath}.");
            if (importer.animationType != ModelImporterAnimationType.Generic
                || importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar
                || importer.optimizeGameObjects)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                importer.optimizeGameObjects = false;
                importer.SaveAndReimport();
            }

            AnimationClip sourceClip = LoadImportedClip(genericPath);
            if (sourceClip.humanMotion)
                throw new InvalidOperationException(
                    $"{genericPath} did not import as Generic.");
            GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
                genericPath);
            GameObject source = UnityEngine.Object.Instantiate(sourceAsset);
            source.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                string[] sourcePaths = AnimationUtility
                    .GetCurveBindings(sourceClip)
                    .Where(binding => binding.type == typeof(Transform))
                    .Select(binding => binding.path)
                    .Distinct(StringComparer.Ordinal)
                    .Where(path => IsIncludedPath(path, motion.Region))
                    .Where(path => source.transform.Find(path) != null)
                    .Where(path => targetModelRoot.Find(path) != null)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                if (sourcePaths.Length == 0)
                    throw new InvalidOperationException(
                        $"{motion.Label} has no compatible direct Transform curves.");

                string destinationPath = ProductionFolder + "/Operator_"
                    + motion.Label + "_PathBound.anim";
                AnimationClip destination = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    destinationPath);
                if (destination == null)
                {
                    destination = new AnimationClip();
                    AssetDatabase.CreateAsset(destination, destinationPath);
                }

                var clean = new AnimationClip
                {
                    frameRate = sourceClip.frameRate,
                    wrapMode = sourceClip.wrapMode,
                    name = Path.GetFileNameWithoutExtension(destinationPath)
                };
                EditorUtility.CopySerialized(clean, destination);
                UnityEngine.Object.DestroyImmediate(clean);
                AnimationClipSettings settings =
                    AnimationUtility.GetAnimationClipSettings(sourceClip);
                if (settings.hasAdditiveReferencePose)
                {
                    settings.hasAdditiveReferencePose = false;
                    settings.additiveReferencePoseClip = null;
                    settings.additiveReferencePoseTime = 0f;
                }
                AnimationUtility.SetAnimationClipSettings(destination, settings);

                float translationScale = Mathf.Max(
                        Mathf.Abs(source.transform.lossyScale.x),
                        0.000001f)
                    / Mathf.Max(
                        Mathf.Abs(targetModelRoot.lossyScale.x),
                        0.000001f);
                var curves = sourcePaths.ToDictionary(
                    path => path,
                    path => new PathCurveSet(
                        ModelRootName + "/" + path,
                        RequireChild(source.transform, path),
                        translationScale),
                    StringComparer.Ordinal);
                int frameCount = Mathf.Max(
                    1,
                    Mathf.CeilToInt(sourceClip.length * sourceClip.frameRate));
                for (int frame = 0; frame <= frameCount; frame++)
                {
                    float time = Mathf.Min(
                        sourceClip.length,
                        frame / sourceClip.frameRate);
                    sourceClip.SampleAnimation(source, time);
                    foreach (PathCurveSet pathCurves in curves.Values)
                        pathCurves.Sample(time);
                }

                foreach (PathCurveSet pathCurves in curves.Values)
                    pathCurves.WriteTo(destination);
                destination.EnsureQuaternionContinuity();
                EditorUtility.SetDirty(destination);
                return destination;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static AnimatorController BuildController(
            Transform body,
            IReadOnlyDictionary<string, AnimationClip> generatedByLabel)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                == null)
            {
                if (!AssetDatabase.CopyAsset(BaseControllerPath, ControllerPath))
                    throw new InvalidOperationException(
                        "Could not clone the Operator controller.");
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                ControllerPath);
            var sourceToGenerated = Motions
                .Where(motion => motion.Label != "Aim")
                .ToDictionary(
                    motion => motion.SourcePath,
                    motion => generatedByLabel[motion.Label],
                    StringComparer.Ordinal);
            sourceToGenerated[CloveAimPosePath] = generatedByLabel["Aim"];

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                    child.state.motion = ReplaceMotion(
                        child.state.motion,
                        sourceToGenerated);
            }

            NormalizeUnifiedFireFlow(
                controller,
                "Upper Body Gun Pose",
                "Operator Fire",
                "Operator Fire Zoomed");

            Func<string, bool> includeUpperBody =
                BuildUpperBodyMaskPredicate(body);
            AvatarMask upperBody = BuildTransformMask(
                UpperBodyMaskPath,
                body,
                includeUpperBody);
            AvatarMask upperBodyNoFingers = BuildTransformMask(
                UpperBodyNoFingersMaskPath,
                body,
                path => includeUpperBody(path) && !IsFingerPath(path));

            AnimatorControllerLayer[] layers = controller.layers;
            if (layers.Length < 5)
                throw new InvalidOperationException(
                    "Operator controller no longer has its five authored layers.");
            layers[1].avatarMask = upperBody;
            layers[2].avatarMask = upperBodyNoFingers;
            layers[3].avatarMask = upperBodyNoFingers;
            // Direct action clips own their original finger curves. The old
            // Humanoid-only finger layer would otherwise overwrite reload/equip.
            layers[4].defaultWeight = 0f;
            controller.layers = layers;
            ConfigureReloadCompletionTransition(
                controller,
                "Upper Body Gun Pose",
                "Operator Reload",
                "Operator Hold");
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static Motion ReplaceMotion(
            Motion motion,
            IReadOnlyDictionary<string, AnimationClip> replacements)
        {
            if (motion is AnimationClip clip)
            {
                string path = AssetDatabase.GetAssetPath(clip);
                return replacements.TryGetValue(path, out AnimationClip replacement)
                    ? replacement
                    : motion;
            }

            if (motion is BlendTree tree)
            {
                ChildMotion[] children = tree.children;
                for (int index = 0; index < children.Length; index++)
                    children[index].motion = ReplaceMotion(
                        children[index].motion,
                        replacements);
                tree.children = children;
                EditorUtility.SetDirty(tree);
            }

            return motion;
        }

        private static void AuthorOperatorGunControllers()
        {
            AnimationClip regularFire = LoadImportedClip(GunFirePath);
            AnimatorState bodyReload = RequireDirectState(
                RequireController(ControllerPath),
                "Upper Body Gun Pose",
                "Operator Reload");
            float bodyReloadDuration = GetEffectiveStateDuration(
                bodyReload,
                "Operator body Reload");
            foreach (string path in new[]
                     {
                         BaseGunControllerPath,
                         CloveGunControllerPath
                     })
            {
                AnimatorController controller = RequireController(path);
                NormalizeUnifiedFireFlow(
                    controller,
                    "Base Layer",
                    "Fire",
                    "Fire Zoomed");
                AnimatorState fire = RequireDirectState(
                    controller,
                    "Base Layer",
                    "Fire");
                fire.motion = regularFire;
                AnimatorState reload = RequireDirectState(
                    controller,
                    "Base Layer",
                    "Reload");
                reload.speed = GetStateSpeedForDuration(
                    reload,
                    bodyReloadDuration,
                    $"{controller.name} gun Reload");
                reload.speedParameterActive = false;
                ConfigureReloadCompletionTransition(
                    controller,
                    "Base Layer",
                    "Reload",
                    "Idle");
                EditorUtility.SetDirty(fire);
                EditorUtility.SetDirty(reload);
                EditorUtility.SetDirty(controller);
            }
        }

        private static void ConfigureReloadCompletionTransition(
            AnimatorController controller,
            string layerName,
            string reloadStateName,
            string destinationStateName)
        {
            AnimatorControllerParameter existing = controller.parameters
                .SingleOrDefault(parameter =>
                    parameter.name == ReloadCompleteParameter);
            if (existing != null
                && existing.type != AnimatorControllerParameterType.Trigger)
            {
                controller.RemoveParameter(existing);
                existing = null;
            }
            if (existing == null)
            {
                controller.AddParameter(
                    ReloadCompleteParameter,
                    AnimatorControllerParameterType.Trigger);
            }

            AnimatorState reload = RequireDirectState(
                controller,
                layerName,
                reloadStateName);
            AnimatorState destination = RequireDirectState(
                controller,
                layerName,
                destinationStateName);
            AnimatorStateTransition transition = reload.transitions
                .Single(candidate => candidate.destinationState == destination);

            foreach (AnimatorCondition condition in transition.conditions.ToArray())
                transition.RemoveCondition(condition);
            transition.hasExitTime = false;
            transition.exitTime = 0f;
            transition.hasFixedDuration = true;
            transition.duration = 0.05f;
            transition.offset = 0f;
            transition.interruptionSource =
                TransitionInterruptionSource.None;
            transition.orderedInterruption = true;
            transition.canTransitionToSelf = false;
            transition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                ReloadCompleteParameter);
            EditorUtility.SetDirty(transition);
            EditorUtility.SetDirty(reload);
            EditorUtility.SetDirty(controller);
        }

        private static void ValidateOperatorGunControllers(GameObject root)
        {
            AnimationClip expected = LoadImportedClip(GunFirePath);
            float bodyReloadDuration = GetEffectiveStateDuration(
                RequireDirectState(
                    RequireController(ControllerPath),
                    "Upper Body Gun Pose",
                    "Operator Reload"),
                "Operator body Reload");
            foreach (string path in new[]
                     {
                         BaseGunControllerPath,
                         CloveGunControllerPath
                     })
            {
                AnimatorController controller = RequireController(path);
                ValidateUnifiedFireFlow(
                    controller,
                    "Base Layer",
                    "Fire",
                    "Fire Zoomed");
                if (RequireDirectState(controller, "Base Layer", "Fire").motion
                    != expected)
                {
                    throw new InvalidOperationException(
                        $"{controller.name}/Fire does not use the regular GNTP "
                        + "Operator fire motion.");
                }

                AnimatorState reload = RequireDirectState(
                    controller,
                    "Base Layer",
                    "Reload");
                float gunReloadDuration = GetEffectiveStateDuration(
                    reload,
                    $"{controller.name} gun Reload");
                if (reload.speedParameterActive
                    || Mathf.Abs(gunReloadDuration - bodyReloadDuration) > 0.001f)
                {
                    throw new InvalidOperationException(
                        $"{controller.name}/Reload is not synchronized with the "
                        + "Operator body Reload "
                        + $"(body={bodyReloadDuration:F4}s, "
                        + $"gun={gunReloadDuration:F4}s)." );
                }
            }

            Animator activeGun = RequireDescendant(root.transform, "Operator_3P")
                .GetComponentInChildren<Animator>(true);
            AnimatorController cloveController =
                RequireController(CloveGunControllerPath);
            if (activeGun == null
                || activeGun.runtimeAnimatorController != cloveController)
            {
                throw new InvalidOperationException(
                    "Clove Operator prefab does not author the cleaned GNTP gun "
                    + "controller directly on its weapon Animator.");
            }
        }

        private static float GetEffectiveStateDuration(
            AnimatorState state,
            string label)
        {
            if (state?.motion == null
                || !float.IsFinite(state.motion.averageDuration)
                || state.motion.averageDuration <= 0f
                || !float.IsFinite(state.speed)
                || state.speed <= 0f)
            {
                throw new InvalidOperationException(
                    $"{label} requires a finite positive motion duration and speed.");
            }

            return state.motion.averageDuration / state.speed;
        }

        private static float GetStateSpeedForDuration(
            AnimatorState state,
            float targetDuration,
            string label)
        {
            if (state?.motion == null
                || !float.IsFinite(state.motion.averageDuration)
                || state.motion.averageDuration <= 0f
                || !float.IsFinite(targetDuration)
                || targetDuration <= 0f)
            {
                throw new InvalidOperationException(
                    $"{label} cannot be synchronized to an invalid duration.");
            }

            return state.motion.averageDuration / targetDuration;
        }

        private static void NormalizeUnifiedFireFlow(
            AnimatorController controller,
            string layerName,
            string fireStateName,
            string obsoleteZoomedStateName)
        {
            AnimatorStateMachine machine = GetLayerStateMachine(
                controller,
                layerName);
            AnimatorState fire = FindDirectState(machine, fireStateName)
                ?? throw new InvalidOperationException(
                    $"{controller.name}/{layerName} has no state {fireStateName}.");
            AnimatorState zoomed = FindDirectState(
                machine,
                obsoleteZoomedStateName);

            foreach (AnimatorStateTransition transition in
                     machine.anyStateTransitions.ToArray())
            {
                if (transition.destinationState == zoomed)
                {
                    machine.RemoveAnyStateTransition(transition);
                    continue;
                }
                if (transition.destinationState == fire)
                    RemoveCondition(transition, "Aiming");
            }

            foreach (ChildAnimatorState child in machine.states)
            {
                foreach (AnimatorStateTransition transition in
                         child.state.transitions.ToArray())
                {
                    if (transition.destinationState == zoomed)
                    {
                        child.state.RemoveTransition(transition);
                        continue;
                    }
                    if (transition.destinationState == fire)
                        RemoveCondition(transition, "Aiming");
                }
            }

            if (zoomed != null)
            {
                foreach (AnimatorStateTransition transition in
                         zoomed.transitions.ToArray())
                {
                    zoomed.RemoveTransition(transition);
                }
                machine.RemoveState(zoomed);
            }

            ValidateUnifiedFireFlow(
                controller,
                layerName,
                fireStateName,
                obsoleteZoomedStateName);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
        }

        private static void ValidateUnifiedFireFlow(
            AnimatorController controller,
            string layerName,
            string fireStateName,
            string obsoleteZoomedStateName)
        {
            AnimatorStateMachine machine = GetLayerStateMachine(
                controller,
                layerName);
            if (FindDirectState(machine, obsoleteZoomedStateName) != null)
            {
                throw new InvalidOperationException(
                    $"{controller.name}/{layerName} still contains "
                    + $"{obsoleteZoomedStateName}.");
            }

            AnimatorState fire = FindDirectState(machine, fireStateName)
                ?? throw new InvalidOperationException(
                    $"{controller.name}/{layerName} has no state {fireStateName}.");
            AnimatorStateTransition[] incoming = machine.anyStateTransitions
                .Where(transition => transition.destinationState == fire)
                .Concat(machine.states.SelectMany(child =>
                    child.state.transitions.Where(transition =>
                        transition.destinationState == fire)))
                .ToArray();
            if (incoming.Length == 0
                || incoming.Any(transition =>
                    !transition.conditions.Any(condition =>
                        condition.parameter == "Fire")
                    || transition.conditions.Any(condition =>
                        condition.parameter == "Aiming")))
            {
                throw new InvalidOperationException(
                    $"{controller.name}/{fireStateName} is not driven solely "
                    + "by the Fire trigger for both hip-fire and ADS.");
            }
        }

        private static AnimatorStateMachine GetLayerStateMachine(
            AnimatorController controller,
            string layerName)
        {
            AnimatorControllerLayer layer = controller.layers.SingleOrDefault(
                candidate => candidate.name == layerName);
            return layer?.stateMachine
                ?? throw new InvalidOperationException(
                    $"{controller.name} has no layer {layerName}.");
        }

        private static AnimatorState FindDirectState(
            AnimatorStateMachine machine,
            string stateName)
        {
            return machine.states
                .Select(child => child.state)
                .SingleOrDefault(candidate => candidate.name == stateName);
        }

        private static void RemoveCondition(
            AnimatorStateTransition transition,
            string parameterName)
        {
            foreach (AnimatorCondition condition in transition.conditions
                         .Where(condition => condition.parameter == parameterName)
                         .ToArray())
            {
                transition.RemoveCondition(condition);
            }
            EditorUtility.SetDirty(transition);
        }

        private static void DeleteObsoleteProductionAssets()
        {
            foreach (string path in new[]
                     {
                         ProductionFolder + "/Operator_FireZoomed_PathBound.anim",
                         ProductionFolder + "/FireZoomed_Source_Generic.fbx"
                     })
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null
                    && !AssetDatabase.DeleteAsset(path))
                {
                    throw new InvalidOperationException(
                        $"Could not delete obsolete production asset {path}.");
                }
            }
        }

        private static AnimatorController RequireController(string path)
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            return controller != null
                ? controller
                : throw new InvalidOperationException(
                    $"Missing Animator Controller {path}.");
        }

        private static AnimatorState RequireDirectState(
            AnimatorController controller,
            string layerName,
            string stateName)
        {
            AnimatorControllerLayer layer = controller.layers.SingleOrDefault(
                candidate => candidate.name == layerName);
            if (layer == null)
            {
                throw new InvalidOperationException(
                    $"{controller.name} has no layer {layerName}.");
            }

            AnimatorState state = layer.stateMachine.states
                .Select(child => child.state)
                .SingleOrDefault(candidate => candidate.name == stateName);
            return state != null
                ? state
                : throw new InvalidOperationException(
                    $"{controller.name}/{layerName} has no state {stateName}.");
        }

        private static AvatarMask BuildTransformMask(
            string assetPath,
            Transform body,
            Func<string, bool> include)
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(assetPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, assetPath);
            }

            for (AvatarMaskBodyPart part = AvatarMaskBodyPart.Root;
                 part < AvatarMaskBodyPart.LastBodyPart;
                 part++)
            {
                mask.SetHumanoidBodyPartActive(part, false);
            }

            var paths = new HashSet<string>(StringComparer.Ordinal)
            {
                string.Empty
            };
            foreach (Transform transform in body.GetComponentsInChildren<Transform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(transform, body);
                if (!include(path))
                    continue;
                AddPathAndAncestors(paths, path);
            }

            string[] ordered = paths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
            mask.transformCount = ordered.Length;
            for (int index = 0; index < ordered.Length; index++)
            {
                mask.SetTransformPath(index, ordered[index]);
                mask.SetTransformActive(index, true);
            }
            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static void AuthorPrefab(
            GameObject root,
            Transform body,
            Transform modelRoot,
            RuntimeAnimatorController controller)
        {
            Animator animator = body.GetComponent<Animator>();
            if (animator == null)
                throw new InvalidOperationException("Clove Body has no Animator.");

            AnimationClip holdClip = Motions
                .First(motion => motion.Label == "Hold")
                .LoadGenerated();
            MountPose mountPose = CalculateOperatorMountPose(root, holdClip);
            Transform master = RequireChild(modelRoot, WeaponMasterPath);
            Transform weapon = RequireDescendant(root.transform, "Operator_3P");
            weapon.SetParent(master, false);
            weapon.localPosition = mountPose.Position;
            weapon.localRotation = mountPose.Rotation;
            weapon.localScale = mountPose.Scale;
            Animator gunAnimator = weapon.GetComponentInChildren<Animator>(true);
            if (gunAnimator == null)
                throw new InvalidOperationException("Operator_3P has no Animator.");
            gunAnimator.runtimeAnimatorController =
                RequireController(CloveGunControllerPath);
            EditorUtility.SetDirty(gunAnimator);

            PlayerVisibilityController visibility =
                root.GetComponent<PlayerVisibilityController>();
            if (visibility == null)
                throw new InvalidOperationException(
                    "Clove prefab has no PlayerVisibilityController.");
            var serializedVisibility = new SerializedObject(visibility);
            SerializedProperty presentations = serializedVisibility.FindProperty(
                "thirdPersonWeaponPresentations");
            for (int index = 0; index < presentations.arraySize; index++)
            {
                SerializedProperty entry = presentations.GetArrayElementAtIndex(index);
                WeaponData data = entry.FindPropertyRelative("weaponData")
                    .objectReferenceValue as WeaponData;
                bool isOperator = data != null && data.name == "Operator";
                if (isOperator)
                {
                    entry.FindPropertyRelative("characterRigMode").enumValueIndex =
                        (int)ThirdPersonCharacterRigMode.GenericPathBound;
                    entry.FindPropertyRelative("characterAvatar")
                        .objectReferenceValue = null;
                    entry.FindPropertyRelative("characterController")
                        .objectReferenceValue = controller;
                    entry.FindPropertyRelative("useLeftHandIK").boolValue = false;
                    entry.FindPropertyRelative("animationDrivenLeftHandIK")
                        .boolValue = false;
                }
            }
            serializedVisibility.ApplyModifiedPropertiesWithoutUndo();

            animator.avatar = null;
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(visibility);
        }

        private static void ValidatePresentations(
            GameObject root,
            RuntimeAnimatorController controller)
        {
            PlayerVisibilityController visibility =
                root.GetComponent<PlayerVisibilityController>();
            ThirdPersonWeaponPresentation[] presentations =
                visibility?.ThirdPersonWeaponPresentations;
            if (presentations == null || presentations.Length == 0)
                throw new InvalidOperationException(
                    "Clove has no authored third-person weapon presentations.");

            foreach (ThirdPersonWeaponPresentation presentation in presentations)
            {
                bool isOperator = presentation.WeaponData != null
                    && presentation.WeaponData.name == "Operator";
                if (isOperator)
                {
                    if (presentation.CharacterRigMode
                            != ThirdPersonCharacterRigMode.GenericPathBound
                        || presentation.CharacterAvatar != null
                        || presentation.CharacterController != controller
                        || presentation.UseLeftHandIK
                        || presentation.AnimationDrivenLeftHandIK)
                    {
                        throw new InvalidOperationException(
                            "Operator presentation is not the authored Generic/no-IK profile.");
                    }
                }
            }
        }

        private static string[] CaptureSkeletonPoseOverrides(Transform body)
        {
            return GetSkeletonPoseOverrides(body)
                .Select(modification =>
                    modification.target.GetEntityId().ToString()
                    + "|" + modification.propertyPath
                    + "|" + modification.value
                    + "|" + (modification.objectReference != null
                        ? modification.objectReference.GetEntityId().ToString()
                        : "0"))
                .OrderBy(signature => signature, StringComparer.Ordinal)
                .ToArray();
        }

        private static void AssertSkeletonPoseUnchanged(
            Transform body,
            IReadOnlyList<string> originalSignatures)
        {
            string[] currentSignatures = CaptureSkeletonPoseOverrides(body);
            if (!originalSignatures.SequenceEqual(currentSignatures))
            {
                throw new InvalidOperationException(
                    "Operator authoring changed existing skeleton Transform "
                    + "overrides. The prefab was not saved; preserve the user's "
                    + "authored pose and inspect the builder.");
            }
        }

        private static void ValidateSerializedSkeletonPose(Transform body)
        {
            PropertyModification[] modifications =
                GetSkeletonPoseOverrides(body);
            foreach (PropertyModification modification in modifications)
            {
                if (!float.TryParse(
                        modification.value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float value)
                    || !float.IsFinite(value))
                {
                    throw new InvalidOperationException(
                        $"Clove skeleton override {modification.propertyPath} "
                        + $"contains invalid value '{modification.value}'.");
                }
            }

            Debug.Log(
                $"[OperatorGeneric] Preserved {modifications.Length} existing "
                + "skeleton Transform overrides without modification.");
        }

        private static PropertyModification[] GetSkeletonPoseOverrides(
            Transform body)
        {
            GameObject nestedBody = PrefabUtility.GetNearestPrefabInstanceRoot(
                body.gameObject);
            Animator sourceAnimator = PrefabUtility.GetCorrespondingObjectFromSource(
                body.GetComponent<Animator>());
            if (nestedBody == null || sourceAnimator == null)
            {
                throw new InvalidOperationException(
                    "Clove Body is not an authored nested model prefab instance.");
            }

            Transform sourceSkeleton = sourceAnimator
                .GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == SkeletonRootPath);
            return (PrefabUtility.GetPropertyModifications(nestedBody)
                    ?? Array.Empty<PropertyModification>())
                .Where(modification =>
                    modification?.target is Transform target
                    && (target == sourceSkeleton || target.IsChildOf(sourceSkeleton))
                    && IsTransformProperty(modification.propertyPath))
                .ToArray();
        }

        private static bool IsTransformProperty(string propertyPath)
        {
            return propertyPath.StartsWith(
                       "m_LocalRotation.",
                       StringComparison.Ordinal)
                || propertyPath.StartsWith(
                    "m_LocalPosition.",
                    StringComparison.Ordinal)
                || propertyPath.StartsWith(
                    "m_LocalScale.",
                    StringComparison.Ordinal)
                || propertyPath.StartsWith(
                    "m_LocalEulerAnglesHint.",
                    StringComparison.Ordinal);
        }

        private static void ValidateController(
            Transform body,
            AnimatorController controller)
        {
            ValidateUnifiedFireFlow(
                controller,
                "Upper Body Gun Pose",
                "Operator Fire",
                "Operator Fire Zoomed");
            var clips = new HashSet<AnimationClip>();
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                    CollectClips(child.state.motion, clips);
            }

            foreach (MotionDefinition motion in Motions)
            {
                AnimationClip clip = motion.LoadGenerated();
                if (!clips.Contains(clip))
                    throw new InvalidOperationException(
                        $"Controller does not reference generated {motion.Label}.");
                if (clip.humanMotion)
                    throw new InvalidOperationException(
                        $"Generated {motion.Label} is still Humanoid.");
                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                if (!bindings.Any(binding => binding.type == typeof(Transform)))
                    throw new InvalidOperationException(
                        $"Generated {motion.Label} has no direct Transform curves.");
                foreach (EditorCurveBinding binding in bindings.Where(
                             binding => binding.type == typeof(Transform)))
                {
                    if (body.Find(binding.path) == null)
                    {
                        throw new InvalidOperationException(
                            $"{motion.Label} targets missing path {binding.path}.");
                    }
                }
            }
        }

        private static void ValidateControllerEvaluation(
            GameObject prefabRoot,
            Transform body,
            Transform modelRoot,
            Animator sourceAnimator)
        {
            GameObject sample = UnityEngine.Object.Instantiate(prefabRoot);
            sample.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                sample.SetActive(true);
                Transform sampleBody = RequireDescendant(sample.transform, "Body");
                Transform sampleModel = RequireDescendant(sampleBody, ModelRootName);
                Transform rightHand = RequireChild(sampleModel, RightHandPath);
                Transform leftHand = RequireChild(sampleModel, LeftHandPath);
                Transform rightEye = RequireChild(sampleModel, RightEyePath);
                Transform master = RequireChild(sampleModel, WeaponMasterPath);
                Animator animator = sampleBody.GetComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);

                Transform weapon = RequireDescendant(sample.transform, "Operator_3P");
                Transform trigger = RequireDescendant(weapon, "Trigger");
                Transform leftTarget = RequireDescendant(
                    weapon,
                    "Left_Hand_Target");
                Transform scopeTarget = RequireDescendant(weapon, "ScopeTarget");
                Transform muzzle = RequireDescendant(weapon, "Muzzle");
                ValidateState(animator, "Operator Hold", 0f, master, rightHand, leftHand);
                AssertHandAlignment(
                    "Operator Hold",
                    rightHand,
                    trigger,
                    leftHand,
                    leftTarget,
                    0.05f);
                Vector3 holdMasterPosition = master.position;
                Quaternion holdMasterRotation = master.rotation;
                Vector3 holdRightHandPosition = rightHand.position;
                Vector3 holdLeftHandPosition = leftHand.position;
                ValidateState(animator, "Operator Aim", 0f, master, rightHand, leftHand);
                AssertHandAlignment(
                    "Operator Aim",
                    rightHand,
                    trigger,
                    leftHand,
                    leftTarget,
                    0.05f);
                bool aimDiffersFromHold =
                    Vector3.Distance(holdMasterPosition, master.position) > 0.01f
                    || Quaternion.Angle(holdMasterRotation, master.rotation) > 1f
                    || Vector3.Distance(
                        holdRightHandPosition,
                        rightHand.position) > 0.01f
                    || Vector3.Distance(
                        holdLeftHandPosition,
                        leftHand.position) > 0.01f;
                if (!aimDiffersFromHold)
                {
                    throw new InvalidOperationException(
                        "Operator Aim still evaluates as the hip-fire Hold pose; "
                        + "AimN must raise the rifle into ADS.");
                }
                ValidateOperatorAdsGeometry(
                    sampleBody,
                    rightEye,
                    scopeTarget,
                    muzzle);
                ValidateState(animator, "Operator Equip", 0.35f, master, rightHand, leftHand);
                ValidateState(animator, "Operator Reload", 0.55f, master, rightHand, leftHand);
                AssertHandAlignment(
                    "Operator Reload",
                    rightHand,
                    trigger,
                    leftHand,
                    leftTarget,
                    0.06f);
                ValidateState(animator, "Operator Fire", 0.4f, master, rightHand, leftHand);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sample);
            }
        }

        private static MountPose CalculateOperatorMountPose(
            GameObject sourceRoot,
            AnimationClip holdClip)
        {
            GameObject measurement = UnityEngine.Object.Instantiate(sourceRoot);
            measurement.hideFlags = HideFlags.HideAndDontSave;
            measurement.SetActive(true);
            try
            {
                Transform body = RequireDescendant(measurement.transform, "Body");
                Transform modelRoot = RequireDescendant(body, ModelRootName);
                Transform master = RequireChild(modelRoot, WeaponMasterPath);
                Transform weapon = RequireDescendant(
                    measurement.transform,
                    "Operator_3P");
                Transform rightHand = RequireChild(modelRoot, RightHandPath);
                Transform leftHand = RequireChild(modelRoot, LeftHandPath);
                Transform trigger = RequireDescendant(weapon, "Trigger");
                Transform leftTarget = RequireDescendant(
                    weapon,
                    "Left_Hand_Target");

                // Start from the shared 1P model-axis convention, then solve a
                // single editor-authored rigid offset that best matches both
                // grip anchors in the original Generic Hold pose. This changes
                // only Operator_3P below R_WeaponMaster; no source transform,
                // mesh, Avatar, or AnimationClip is modified.
                weapon.SetParent(master, false);
                weapon.localPosition = Vector3.zero;
                weapon.localRotation = Quaternion.Euler(0f, 270f, 0f);
                weapon.localScale = Vector3.one * 0.01f;
                holdClip.SampleAnimation(body.gameObject, 0f);

                Quaternion axisFit = Quaternion.FromToRotation(
                    leftTarget.position - trigger.position,
                    leftHand.position - rightHand.position);
                weapon.rotation = axisFit * weapon.rotation;
                weapon.position += (rightHand.position + leftHand.position) * 0.5f
                    - (trigger.position + leftTarget.position) * 0.5f;

                return new MountPose(
                    weapon.localPosition,
                    weapon.localRotation,
                    weapon.localScale);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(measurement);
            }
        }

        private static void BakeOperatorAdsPose(
            GameObject prefabRoot,
            AnimationClip aimClip)
        {
            GameObject sample = UnityEngine.Object.Instantiate(prefabRoot);
            sample.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                sample.SetActive(true);
                Transform body = RequireDescendant(sample.transform, "Body");
                Transform modelRoot = RequireDescendant(body, ModelRootName);
                Transform weapon = RequireDescendant(sample.transform, "Operator_3P");
                Transform weaponMaster = RequireChild(modelRoot, WeaponMasterPath);
                Transform rightEye = RequireChild(modelRoot, RightEyePath);
                Transform rightHand = RequireChild(modelRoot, RightHandPath);
                Transform leftHand = RequireChild(modelRoot, LeftHandPath);
                Transform rightLowerArm = rightHand.parent;
                Transform rightUpperArm = rightLowerArm.parent;
                Transform leftLowerArm = leftHand.parent;
                Transform leftUpperArm = leftLowerArm.parent;
                Transform scopeTarget = RequireDescendant(weapon, "ScopeTarget");
                Transform muzzle = RequireDescendant(weapon, "Muzzle");
                Transform trigger = RequireDescendant(weapon, "Trigger");
                Transform leftTarget = RequireDescendant(
                    weapon,
                    "Left_Hand_Target");

                aimClip.SampleAnimation(body.gameObject, 0f);
                Vector3 sightVector = muzzle.position - scopeTarget.position;
                if (sightVector.sqrMagnitude < 0.000001f)
                {
                    throw new InvalidOperationException(
                        "Operator ScopeTarget and Muzzle do not define a sight axis.");
                }

                // AimN is the forward ADS pose: align the rigid weapon sight
                // axis with character forward, then place the scope behind the
                // muzzle at the authored eye-relief distance. All changes are
                // baked into the generated clip; the prefab/model rest pose is
                // never edited.
                Vector3 bodyForward = body.forward.normalized;
                weaponMaster.rotation = Quaternion.FromToRotation(
                    sightVector.normalized,
                    bodyForward) * weaponMaster.rotation;
                weaponMaster.position += rightEye.position
                    + bodyForward * OperatorAimEyeRelief
                    - scopeTarget.position;

                SolveTwoBoneArm(
                    rightUpperArm,
                    rightLowerArm,
                    rightHand,
                    trigger.position,
                    "right");
                SolveTwoBoneArm(
                    leftUpperArm,
                    leftLowerArm,
                    leftHand,
                    leftTarget.position,
                    "left");

                float curveEnd = Mathf.Max(
                    aimClip.length,
                    1f / Mathf.Max(1f, aimClip.frameRate));
                WriteConstantLocalPosition(
                    aimClip,
                    body,
                    weaponMaster,
                    curveEnd);
                foreach (Transform solved in new[]
                         {
                             rightUpperArm,
                             rightLowerArm,
                             rightHand,
                             leftUpperArm,
                             leftLowerArm,
                             leftHand,
                             weaponMaster
                         })
                {
                    WriteConstantLocalRotation(
                        aimClip,
                        body,
                        solved,
                        curveEnd);
                }

                aimClip.EnsureQuaternionContinuity();
                EditorUtility.SetDirty(aimClip);
                ValidateOperatorAdsGeometry(
                    body,
                    rightEye,
                    scopeTarget,
                    muzzle);
                AssertHandAlignment(
                    "Baked Operator Aim",
                    rightHand,
                    trigger,
                    leftHand,
                    leftTarget,
                    0.005f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sample);
            }
        }

        private static void SolveTwoBoneArm(
            Transform upperArm,
            Transform lowerArm,
            Transform hand,
            Vector3 targetPosition,
            string side)
        {
            Vector3 shoulderPosition = upperArm.position;
            Vector3 elbowPosition = lowerArm.position;
            Vector3 handPosition = hand.position;
            Quaternion handRotation = hand.rotation;
            float upperLength = Vector3.Distance(
                shoulderPosition,
                elbowPosition);
            float lowerLength = Vector3.Distance(elbowPosition, handPosition);
            Vector3 targetVector = targetPosition - shoulderPosition;
            float targetDistance = targetVector.magnitude;
            float minimumReach = Mathf.Abs(upperLength - lowerLength) + 0.0001f;
            float maximumReach = upperLength + lowerLength - 0.0001f;
            if (targetDistance < minimumReach || targetDistance > maximumReach)
            {
                throw new InvalidOperationException(
                    $"Operator ADS target is outside the {side} arm's reachable "
                    + $"range ({targetDistance:F4}m not in "
                    + $"[{minimumReach:F4}, {maximumReach:F4}]m).");
            }

            Vector3 targetDirection = targetVector / targetDistance;
            Vector3 bendDirection = elbowPosition - shoulderPosition;
            bendDirection -= Vector3.Dot(bendDirection, targetDirection)
                * targetDirection;
            if (bendDirection.sqrMagnitude < 0.000001f)
                bendDirection = Vector3.Cross(targetDirection, upperArm.forward);
            if (bendDirection.sqrMagnitude < 0.000001f)
                bendDirection = Vector3.Cross(targetDirection, Vector3.up);
            bendDirection.Normalize();

            float shoulderCosine = Mathf.Clamp(
                (upperLength * upperLength
                    + targetDistance * targetDistance
                    - lowerLength * lowerLength)
                / (2f * upperLength * targetDistance),
                -1f,
                1f);
            float shoulderSine = Mathf.Sqrt(
                Mathf.Max(0f, 1f - shoulderCosine * shoulderCosine));
            Vector3 desiredElbow = shoulderPosition
                + targetDirection * (shoulderCosine * upperLength)
                + bendDirection * (shoulderSine * upperLength);

            upperArm.rotation = Quaternion.FromToRotation(
                lowerArm.position - upperArm.position,
                desiredElbow - upperArm.position) * upperArm.rotation;
            lowerArm.rotation = Quaternion.FromToRotation(
                hand.position - lowerArm.position,
                targetPosition - lowerArm.position) * lowerArm.rotation;
            hand.rotation = handRotation;
        }

        private static void WriteConstantLocalPosition(
            AnimationClip clip,
            Transform root,
            Transform target,
            float curveEnd)
        {
            string path = AnimationUtility.CalculateTransformPath(target, root);
            Vector3 value = target.localPosition;
            SetConstantTransformCurve(
                clip,
                path,
                "m_LocalPosition.x",
                value.x,
                curveEnd);
            SetConstantTransformCurve(
                clip,
                path,
                "m_LocalPosition.y",
                value.y,
                curveEnd);
            SetConstantTransformCurve(
                clip,
                path,
                "m_LocalPosition.z",
                value.z,
                curveEnd);
        }

        private static void WriteConstantLocalRotation(
            AnimationClip clip,
            Transform root,
            Transform target,
            float curveEnd)
        {
            string path = AnimationUtility.CalculateTransformPath(target, root);
            Quaternion value = target.localRotation;
            SetConstantTransformCurve(
                clip,
                path,
                "m_LocalRotation.x",
                value.x,
                curveEnd);
            SetConstantTransformCurve(
                clip,
                path,
                "m_LocalRotation.y",
                value.y,
                curveEnd);
            SetConstantTransformCurve(
                clip,
                path,
                "m_LocalRotation.z",
                value.z,
                curveEnd);
            SetConstantTransformCurve(
                clip,
                path,
                "m_LocalRotation.w",
                value.w,
                curveEnd);
        }

        private static void SetConstantTransformCurve(
            AnimationClip clip,
            string path,
            string property,
            float value,
            float curveEnd)
        {
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    path,
                    typeof(Transform),
                    property),
                AnimationCurve.Constant(0f, curveEnd, value));
        }

        private static void AssertHandAlignment(
            string state,
            Transform rightHand,
            Transform trigger,
            Transform leftHand,
            Transform leftTarget,
            float maximumDistance)
        {
            float rightDistance = Vector3.Distance(
                rightHand.position,
                trigger.position);
            float leftDistance = Vector3.Distance(
                leftHand.position,
                leftTarget.position);
            if (rightDistance > maximumDistance || leftDistance > maximumDistance)
            {
                throw new InvalidOperationException(
                    $"{state} grip alignment is outside {maximumDistance:F3}m "
                    + $"(right={rightDistance:F3}, left={leftDistance:F3}).");
            }
        }

        private static void ValidateOperatorAdsGeometry(
            Transform body,
            Transform rightEye,
            Transform scopeTarget,
            Transform muzzle)
        {
            Vector3 sightVector = muzzle.position - scopeTarget.position;
            if (sightVector.sqrMagnitude < 0.000001f)
            {
                throw new InvalidOperationException(
                    "Operator ScopeTarget and Muzzle do not define a sight axis.");
            }

            Vector3 sightAxis = sightVector.normalized;
            float sightLineMiss = Vector3.Cross(
                rightEye.position - scopeTarget.position,
                sightAxis).magnitude;
            float eyeRelief = Vector3.Dot(
                scopeTarget.position - rightEye.position,
                sightAxis);
            float forwardAlignment = Vector3.Dot(sightAxis, body.forward);
            if (sightLineMiss > OperatorAimMaximumSightLineMiss
                || eyeRelief < OperatorAimMinimumEyeRelief
                || eyeRelief > OperatorAimMaximumEyeRelief
                || forwardAlignment < OperatorAimMinimumForwardAlignment)
            {
                throw new InvalidOperationException(
                    "Operator Aim is not a valid eye-level ADS pose "
                    + $"(line miss={sightLineMiss:F3}m, "
                    + $"eye relief={eyeRelief:F3}m, "
                    + $"forward alignment={forwardAlignment:F3}).");
            }
        }

        private static void ValidateState(
            Animator animator,
            string state,
            float normalizedTime,
            params Transform[] transforms)
        {
            animator.Play(state, 1, normalizedTime);
            animator.Update(0.0001f);
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(1);
            if (!info.IsName(state))
                throw new InvalidOperationException(
                    $"Animator could not evaluate state {state}.");
            foreach (Transform transform in transforms)
            {
                if (!IsFinite(transform.position)
                    || !IsFinite(transform.rotation)
                    || !IsFinite(transform.localScale))
                {
                    throw new InvalidOperationException(
                        $"{state} produced an invalid pose at {transform.name}.");
                }
            }
        }

        private static void CollectClips(Motion motion, ISet<AnimationClip> clips)
        {
            if (motion is AnimationClip clip)
            {
                clips.Add(clip);
                return;
            }
            if (motion is BlendTree tree)
            {
                foreach (ChildMotion child in tree.children)
                    CollectClips(child.motion, clips);
            }
        }

        private static bool IsIncludedPath(string path, MotionRegion region)
        {
            if (region == MotionRegion.FullSkeleton)
                return path == SkeletonRootPath
                    || path.StartsWith(SkeletonRootPath + "/", StringComparison.Ordinal);
            return path == UpperBodyRootPath
                || path.StartsWith(UpperBodyRootPath + "/", StringComparison.Ordinal)
                || path == WeaponRootPath
                || path.StartsWith(WeaponRootPath + "/", StringComparison.Ordinal);
        }

        private static bool IsTargetUpperBodyPath(string path)
        {
            string prefix = ModelRootName + "/";
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            return IsIncludedPath(path.Substring(prefix.Length), MotionRegion.UpperBody);
        }

        private static Func<string, bool> BuildUpperBodyMaskPredicate(
            Transform body)
        {
            PlayerVisibilityController visibility =
                body.root.GetComponent<PlayerVisibilityController>();
            if (visibility == null)
            {
                throw new InvalidOperationException(
                    "Clove has no PlayerVisibilityController for mask authoring.");
            }

            var presentationNames = new HashSet<string>(visibility
                .ThirdPersonWeaponPresentations
                .Where(presentation =>
                    presentation?.WeaponObject != null)
                .Select(presentation => presentation.WeaponObject.name),
                StringComparer.Ordinal);
            Transform weaponMaster = RequireChild(
                RequireDescendant(body, ModelRootName),
                WeaponMasterPath);
            string[] presentationRoots = Enumerable
                .Range(0, weaponMaster.childCount)
                .Select(weaponMaster.GetChild)
                .Where(child =>
                    presentationNames.Contains(child.name)
                    || child.GetComponentInChildren<Animator>(true) != null)
                .Select(child => AnimationUtility.CalculateTransformPath(
                    child,
                    body))
                .ToArray();

            return path => IsTargetUpperBodyPath(path)
                && !presentationRoots.Any(root =>
                    path == root
                    || path.StartsWith(root + "/", StringComparison.Ordinal));
        }

        private static bool IsFingerPath(string path)
        {
            return path.IndexOf("_Thumb", StringComparison.Ordinal) >= 0
                || path.IndexOf("_Index", StringComparison.Ordinal) >= 0
                || path.IndexOf("_Middle", StringComparison.Ordinal) >= 0
                || path.IndexOf("_Ring", StringComparison.Ordinal) >= 0
                || path.IndexOf("_Pinky", StringComparison.Ordinal) >= 0;
        }

        private static void AddPathAndAncestors(ISet<string> paths, string path)
        {
            while (!string.IsNullOrEmpty(path))
            {
                paths.Add(path);
                int separator = path.LastIndexOf('/');
                path = separator >= 0 ? path.Substring(0, separator) : string.Empty;
            }
            paths.Add(string.Empty);
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

        private static void EnsureFolder(string folder)
        {
            string current = "Assets";
            foreach (string segment in folder.Split('/').Skip(1))
            {
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }

        private static Transform RequireDescendant(Transform root, string name)
        {
            Transform result = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == name);
            return result != null
                ? result
                : throw new InvalidOperationException(
                    $"Missing authored object {name} below {root.name}.");
        }

        private static Transform RequireChild(Transform parent, string path)
        {
            Transform child = parent.Find(path);
            return child != null
                ? child
                : throw new InvalidOperationException(
                    $"Missing authored path {path} below {parent.name}.");
        }

        private static void StopAnimationPreview()
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
        }

        private static void SaveAllOrThrow(string phase)
        {
            if (!EditorSceneManager.SaveOpenScenes())
                throw new InvalidOperationException(
                    $"Open scenes could not be saved {phase}.");
            AssetDatabase.SaveAssets();
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

        private static MotionDefinition Core(
            string label,
            string fileName,
            MotionRegion region)
        {
            return new MotionDefinition(
                label,
                CoreAnimationFolder + "/" + fileName,
                region);
        }

        private static MotionDefinition Operator(string label, string fileName)
        {
            return new MotionDefinition(
                label,
                OperatorAnimationFolder + "/" + fileName,
                MotionRegion.UpperBody);
        }

        private enum MotionRegion
        {
            FullSkeleton,
            UpperBody
        }

        private readonly struct MotionDefinition
        {
            public MotionDefinition(
                string label,
                string sourcePath,
                MotionRegion region)
            {
                Label = label;
                SourcePath = sourcePath;
                Region = region;
            }

            public string Label { get; }
            public string SourcePath { get; }
            public MotionRegion Region { get; }

            public AnimationClip LoadGenerated()
            {
                string path = ProductionFolder + "/Operator_"
                    + Label + "_PathBound.anim";
                return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            }
        }

        private readonly struct MountPose
        {
            public MountPose(
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
        }

        private sealed class PathCurveSet
        {
            private readonly string path;
            private readonly Transform source;
            private readonly float translationScale;
            private readonly AnimationCurve px = new AnimationCurve();
            private readonly AnimationCurve py = new AnimationCurve();
            private readonly AnimationCurve pz = new AnimationCurve();
            private readonly AnimationCurve rx = new AnimationCurve();
            private readonly AnimationCurve ry = new AnimationCurve();
            private readonly AnimationCurve rz = new AnimationCurve();
            private readonly AnimationCurve rw = new AnimationCurve();
            private Quaternion previousRotation;
            private bool hasPreviousRotation;

            public PathCurveSet(
                string path,
                Transform source,
                float translationScale)
            {
                this.path = path;
                this.source = source;
                this.translationScale = translationScale;
            }

            public void Sample(float time)
            {
                Vector3 position = source.localPosition * translationScale;
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

                Add(px, time, position.x);
                Add(py, time, position.y);
                Add(pz, time, position.z);
                Add(rx, time, rotation.x);
                Add(ry, time, rotation.y);
                Add(rz, time, rotation.z);
                Add(rw, time, rotation.w);
            }

            public void WriteTo(AnimationClip clip)
            {
                Set(clip, "m_LocalPosition.x", px);
                Set(clip, "m_LocalPosition.y", py);
                Set(clip, "m_LocalPosition.z", pz);
                Set(clip, "m_LocalRotation.x", rx);
                Set(clip, "m_LocalRotation.y", ry);
                Set(clip, "m_LocalRotation.z", rz);
                Set(clip, "m_LocalRotation.w", rw);
            }

            private void Set(
                AnimationClip clip,
                string property,
                AnimationCurve curve)
            {
                SetLinearTangents(curve);
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        path,
                        typeof(Transform),
                        property),
                    curve);
            }

            private static void Add(
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
