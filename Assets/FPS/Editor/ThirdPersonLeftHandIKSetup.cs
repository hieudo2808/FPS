using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace FPS.Editor
{
    public static class ThirdPersonLeftHandIKSetup
    {
        private const string MenuPath = "FPS/Third Person/Setup Left Hand IK";
        private const string RigRootName = "ThirdPersonWeaponRig";
        private const string ConstraintName = "LeftHandIK";
        private const string HintName = "LeftElbowHint";
        private const string ProxyName = "LeftHandTargetProxy";
        private const string TargetName = "Left_Hand_Target";
        private const string ClovePrefabPath =
            "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab";
        private const string VandalSocketName = "Left_Hand_TargetSocket";
        private const string VandalWeaponPrefabPath =
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Vandal/Vandal3P.prefab";
        private const string OperatorWeaponPrefabPath =
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/Operator3P.prefab";
        private const string OdinWeaponPrefabPath =
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Odin/Odin3P.prefab";
        private const string ClassicWeaponPrefabPath =
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Classic/Classic3P.prefab";
        private const string CloveReloadStateName = "Vandal Reload";
        private const string CloveAnimationFolder =
            "Assets/FPS/Features/Characters/Animation/Content/3P/Clove";
        private const string CloveBodyModelPath =
            "Assets/FPS/Features/Characters/Content/Players/Clove/Models/Body.fbx";
        private const string CanonicalTPCoreAvatarModelPath =
            "Assets/FPS/Features/Characters/Animation/Content/3P/TP_Core_IdlePose.fbx";
        private const string CloveTPCoreAvatarPath =
            CloveAnimationFolder + "/CloveTPCoreAvatar.asset";
        private const string CloveReloadClipPath =
            CloveAnimationFolder + "/CloveVandalReloadLeftHandIK.anim";
        private const string CloveOverrideControllerPath =
            CloveAnimationFolder + "/CloveThirdPerson.overrideController";
        private const string CloveGunReloadClipPath =
            CloveAnimationFolder + "/CloveVandalGunReloadLeftHandTarget.anim";
        private const string CloveGunOverrideControllerPath =
            CloveAnimationFolder + "/CloveVandal3P_Gun.overrideController";
        private const string VandalGunReloadStateName = "Reload";
        private const string Vandal3PName = "Vandal_3P";
        private const string Operator3PName = "Operator_3P";
        private const string OperatorReloadStateName = "Operator Reload";
        private const string OperatorHoldStateName = "Operator Hold";
        private const string OperatorAimStateName = "Operator Aim";
        private const string OperatorFireStateName = "Operator Fire";
        private const string OperatorGunReloadStateName = "Reload";
        private const string OperatorGunFireStateName = "Fire";
        private const string OperatorAimingParameterName = "Aiming";
        private const string OperatorDirectTargetName =
            "Left_Hand_Target_end";
        private const string CloveOperatorReloadClipPath =
            CloveAnimationFolder + "/CloveOperatorReloadLeftHandIK.anim";
        private const string CloveOperatorOverrideControllerPath =
            CloveAnimationFolder + "/CloveOperatorThirdPerson.overrideController";
        private const string CloveOperatorGunReloadClipPath =
            CloveAnimationFolder + "/CloveOperatorGunReloadLeftHandTarget.anim";
        private const string CloveOperatorGunOverrideControllerPath =
            CloveAnimationFolder + "/CloveOperator3P_Gun.overrideController";
        private const string OperatorAimClipPath =
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/"
            + "S0/3P/Anims/TP_Core_Boltsniper_S0_AltIdlePose_UB.fbx";
        private const string CloveOperatorAimPoseClipPath =
            CloveAnimationFolder + "/CloveOperatorAimPose.anim";
        private const float OperatorAimEyeRelief = 0.22f;
        private const string Classic3PName = "Classic_3P";
        private const string ClassicReloadStateName = "Classic Reload";
        private const string ClassicGunReloadStateName = "Reload";
        private const string ClassicDirectTargetName =
            "Left_Hand_TargetSocket";
        private const string CloveClassicReloadClipPath =
            CloveAnimationFolder + "/CloveClassicReloadLeftHandIK.anim";
        private const string CloveClassicOverrideControllerPath =
            CloveAnimationFolder + "/CloveClassicThirdPerson.overrideController";
        private const string CloveClassicGunReloadClipPath =
            CloveAnimationFolder + "/CloveClassicGunReloadLeftHandTarget.anim";
        private const string CloveClassicGunOverrideControllerPath =
            CloveAnimationFolder + "/CloveClassic3P_Gun.overrideController";
        private const string Odin3PName = "Odin_3P";
        private const string OdinReloadStateName = "Odin Reload";
        private const string OdinGunReloadStateName = "Reload";
        private const string OdinDirectTargetName = "Left_Hand_TargetSocket";
        private const string CloveOdinGunReloadClipPath =
            CloveAnimationFolder + "/CloveOdinGunReloadLeftHandTarget.anim";
        private const string ObsoleteCloveOdinAimPoseClipPath =
            CloveAnimationFolder + "/CloveOdinAimPose.anim";
        private const string LegacyCloveOdinOverrideControllerPath =
            CloveAnimationFolder + "/CloveOdinThirdPerson.overrideController";
        private const string CloveOdinBodyControllerPath =
            CloveAnimationFolder + "/CloveOdin3P_Body.controller";
        private const string CloveOdinGunControllerPath =
            CloveAnimationFolder + "/CloveOdin3P_ThirdPersonGun.controller";
        private const string CloveOdinStaticPoseClipPath =
            CloveAnimationFolder + "/CloveOdin3P_StaticPose.anim";
        private const string OdinBodyControllerPath =
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Odin/"
            + "Odin3P_Body.controller";
        private const string OdinGunControllerPath =
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Odin/"
            + "Odin3P_Gun.controller";
        private const string OdinAimStateName = "Odin Aim";
        private const string OdinAimingParameterName = "Aiming";
        private const string MagazineMainName = "Magazine_Main";
        private const string ConstraintAnimationPath =
            "ThirdPersonWeaponRig/LeftHandIK";

        private readonly struct WeaponGripSetup
        {
            public WeaponGripSetup(
                string prefabPath,
                Vector3 positionOffset,
                Vector3 rotationOffset)
            {
                PrefabPath = prefabPath;
                PositionOffset = positionOffset;
                RotationOffset = rotationOffset;
            }

            public string PrefabPath { get; }
            public Vector3 PositionOffset { get; }
            public Vector3 RotationOffset { get; }
        }

        private static readonly WeaponGripSetup[] WeaponGripSetups =
        {
            new WeaponGripSetup(
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Vandal/Vandal3P.prefab",
                new Vector3(0f, -0.001119f, -0.043897f),
                new Vector3(288.598f, 136.874f, 357.895f)),
            new WeaponGripSetup(
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Classic/Classic3P.prefab",
                new Vector3(-0.022876f, 0.027583f, -0.064806f),
                new Vector3(329.159f, 68.925f, 351.729f)),
            new WeaponGripSetup(
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/Operator3P.prefab",
                new Vector3(-0.082836f, 0.071174f, 0.130563f),
                new Vector3(298.771f, 316.784f, 116.588f)),
            new WeaponGripSetup(
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Odin/Odin3P.prefab",
                new Vector3(0.062502f, 0.058313f, 0.016069f),
                new Vector3(28.332f, 81.883f, 75.387f)),
            new WeaponGripSetup(
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Bucky/Bucky3P.prefab",
                new Vector3(-0.020734f, -0.003783f, -0.096840f),
                new Vector3(310.085f, 326.892f, 115.489f))
        };

        private static readonly string[] PlayerPrefabPaths =
        {
            "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Sage/SagePlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Gekko/GekkoPlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Brimstone/BrimstonePlayer.prefab"
        };

        [MenuItem(MenuPath)]
        public static void SetupAllPlayerPrefabs()
        {
            if (!EditorSceneManager.SaveOpenScenes())
                throw new InvalidOperationException(
                    "Open scenes could not be saved before IK setup.");

            foreach (WeaponGripSetup setup in WeaponGripSetups)
                SetupWeaponGrip(setup);

            var configured = new List<string>(PlayerPrefabPaths.Length);
            foreach (string prefabPath in PlayerPrefabPaths)
            {
                SetupPlayerPrefab(prefabPath);
                configured.Add(prefabPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[ThirdPersonIK] Configured calibrated left-hand rig for: "
                + string.Join(", ", configured));
        }

        [MenuItem("FPS/Third Person/Configure Clove Reload Weight Clip")]
        public static void ConfigureCloveReloadWeightClip()
        {
            if (!EditorSceneManager.SaveOpenScenes())
            {
                throw new InvalidOperationException(
                    "Open scenes could not be saved before the Clove IK setup.");
            }
            AssetDatabase.SaveAssets();

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool useLiveStage = stage != null && stage.assetPath == ClovePrefabPath;
            GameObject root = useLiveStage
                ? stage.prefabContentsRoot
                : PrefabUtility.LoadPrefabContents(ClovePrefabPath);
            try
            {
                ConfigureClovePrefabRoot(root);
                PrefabUtility.SaveAsPrefabAsset(root, ClovePrefabPath);
            }
            finally
            {
                if (!useLiveStage)
                    PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                ClovePrefabPath,
                ImportAssetOptions.ForceUpdate);
            Debug.Log(
                "[ThirdPersonIK] Clove now uses an AnimationClip curve for "
                + "LeftHandIK.m_Weight. Existing socket transforms were not modified.");
        }

        [MenuItem("FPS/Third Person/Configure Clove Operator Reload IK")]
        public static void ConfigureCloveOperatorReloadIK()
        {
            if (!EditorSceneManager.SaveOpenScenes())
            {
                throw new InvalidOperationException(
                    "Open scenes could not be saved before the Clove Operator IK setup.");
            }
            AssetDatabase.SaveAssets();

            ConfigureSharedDirectIKTarget(
                VandalWeaponPrefabPath,
                VandalSocketName,
                false);
            ConfigureSharedDirectIKTarget(
                OperatorWeaponPrefabPath,
                OperatorDirectTargetName,
                true);
            AssetDatabase.SaveAssets();

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool useLiveStage = stage != null && stage.assetPath == ClovePrefabPath;
            GameObject root = useLiveStage
                ? stage.prefabContentsRoot
                : PrefabUtility.LoadPrefabContents(ClovePrefabPath);
            try
            {
                ConfigureCloveOperatorPrefabRoot(root);
                PrefabUtility.SaveAsPrefabAsset(root, ClovePrefabPath);
            }
            finally
            {
                if (!useLiveStage)
                    PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                ClovePrefabPath,
                ImportAssetOptions.ForceUpdate);
            Debug.Log(
                "[ThirdPersonIK] Clove Operator now uses the existing "
                + "Left_Hand_Target_end for direct IK and animation-only "
                + "Magazine_Main contact curves.");
        }

        [MenuItem("FPS/Third Person/Configure Clove Classic Reload IK")]
        public static void ConfigureCloveClassicReloadIK()
        {
            if (!EditorSceneManager.SaveOpenScenes())
            {
                throw new InvalidOperationException(
                    "Open scenes could not be saved before the Clove Classic IK setup.");
            }
            AssetDatabase.SaveAssets();

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool useLiveStage = stage != null && stage.assetPath == ClovePrefabPath;
            GameObject root = useLiveStage
                ? stage.prefabContentsRoot
                : PrefabUtility.LoadPrefabContents(ClovePrefabPath);
            try
            {
                ConfigureCloveClassicPrefabRoot(root);
                PrefabUtility.SaveAsPrefabAsset(root, ClovePrefabPath);
            }
            finally
            {
                if (!useLiveStage)
                    PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                ClovePrefabPath,
                ImportAssetOptions.ForceUpdate);
            Debug.Log(
                "[ThirdPersonIK] Clove Classic now uses its existing "
                + "Left_Hand_TargetSocket, Classic-specific IK weight curves, "
                + "and animation-only Magazine_Main contact curves.");
        }

        [MenuItem("FPS/Third Person/Prepare Clove Odin Rig")]
        public static void ConfigureCloveOdinRig()
        {
            if (!EditorSceneManager.SaveOpenScenes())
            {
                throw new InvalidOperationException(
                    "Open scenes could not be saved before the Clove Odin setup.");
            }
            AssetDatabase.SaveAssets();

            // Never persist an Animation Window preview pose into the nested
            // Clove Body model. This must happen before reading the live stage.
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();

            Avatar cloveTPCoreAvatar = CreateOrUpdateCloveTPCoreAvatar();

            ConfigureSharedDirectIKTarget(
                OdinWeaponPrefabPath,
                OdinDirectTargetName,
                true);
            AssetDatabase.SaveAssets();

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool useLiveStage = stage != null && stage.assetPath == ClovePrefabPath;
            GameObject root = useLiveStage
                ? stage.prefabContentsRoot
                : PrefabUtility.LoadPrefabContents(ClovePrefabPath);
            try
            {
                RepairCloveBodyAvatarAndPose(root, cloveTPCoreAvatar);
                ConfigureCloveOdinPrefabRoot(root);
                PrefabUtility.SaveAsPrefabAsset(root, ClovePrefabPath);
            }
            finally
            {
                if (!useLiveStage)
                    PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                ClovePrefabPath,
                ImportAssetOptions.ForceUpdate);
            Debug.Log(
                "[ThirdPersonIK] Clove is ready for Odin authoring with the "
                + "existing Left_Hand_TargetSocket and the canonical humanoid "
                + "equip/reload clips.");
        }

        [MenuItem("FPS/Third Person/Repair Clove TP Core Avatar")]
        public static void RepairCloveTPCoreAvatar()
        {
            if (!EditorSceneManager.SaveOpenScenes())
            {
                throw new InvalidOperationException(
                    "Open scenes could not be saved before the Clove Avatar repair.");
            }
            AssetDatabase.SaveAssets();

            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();

            Avatar avatar = CreateOrUpdateCloveTPCoreAvatar();
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool useLiveStage = stage != null && stage.assetPath == ClovePrefabPath;
            GameObject root = useLiveStage
                ? stage.prefabContentsRoot
                : PrefabUtility.LoadPrefabContents(ClovePrefabPath);
            int removedPoseOverrides;
            try
            {
                removedPoseOverrides = RepairCloveBodyAvatarAndPose(root, avatar);
                PrefabUtility.SaveAsPrefabAsset(root, ClovePrefabPath);
            }
            finally
            {
                if (!useLiveStage)
                    PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                ClovePrefabPath,
                ImportAssetOptions.ForceUpdate);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[ThirdPersonIK] Assigned the hierarchy-compatible TP Core Avatar "
                + $"and removed {removedPoseOverrides} sampled Body pose overrides.");
        }

        private static void ConfigureClovePrefabRoot(GameObject root)
        {
            Transform body = root.transform.Find("Body");
            Animator animator = body != null ? body.GetComponent<Animator>() : null;
            ThirdPersonLeftHandIK controller =
                root.GetComponent<ThirdPersonLeftHandIK>();
            TwoBoneIKConstraint constraint =
                root.GetComponentInChildren<TwoBoneIKConstraint>(true);
            if (animator == null || controller == null || constraint == null)
            {
                throw new InvalidOperationException(
                    "Clove is missing its Body Animator or left-hand IK components.");
            }

            Transform socket = constraint.data.target;
            if (socket == null || socket.name != VandalSocketName)
            {
                throw new InvalidOperationException(
                    $"The existing IK target must remain {VandalSocketName}; "
                    + "the setup will not replace or move it.");
            }

            Vector3 socketPosition = socket.localPosition;
            Quaternion socketRotation = socket.localRotation;
            Vector3 socketScale = socket.localScale;
            Animator gunAnimator = socket.GetComponentInParent<Animator>();
            Transform vandal = FindAncestor(socket, Vandal3PName);
            Transform magazine = gunAnimator != null
                ? FindDescendant(gunAnimator.transform, MagazineMainName)
                : null;
            if (gunAnimator == null
                || gunAnimator == animator
                || vandal == null
                || magazine == null)
            {
                throw new InvalidOperationException(
                    "Clove Vandal is missing its nested gun Animator, Vandal_3P root, "
                    + "or Magazine_Main bone.");
            }

            AnimatorController baseController = GetBaseAnimatorController(animator);
            AnimatorState reloadState = FindState(
                baseController,
                CloveReloadStateName);
            AnimationClip sourceClip = reloadState?.motion as AnimationClip;
            if (sourceClip == null)
            {
                throw new InvalidOperationException(
                    $"State '{CloveReloadStateName}' has no AnimationClip motion.");
            }

            AnimatorController gunBaseController =
                GetBaseAnimatorController(gunAnimator);
            AnimatorState gunReloadState = FindState(
                gunBaseController,
                VandalGunReloadStateName);
            AnimationClip gunSourceClip = gunReloadState?.motion as AnimationClip;
            if (gunSourceClip == null)
            {
                throw new InvalidOperationException(
                    $"State '{VandalGunReloadStateName}' has no AnimationClip motion.");
            }

            SynchronizeGunReloadStateToBody(
                reloadState,
                sourceClip,
                gunReloadState,
                gunSourceClip);
            EditorUtility.SetDirty(gunBaseController);
            ValidateSynchronizedReloadStates(
                reloadState,
                sourceClip,
                gunReloadState,
                gunSourceClip);

            EnsureAssetFolder(CloveAnimationFolder);
            AnimatorOverrideController overrideController =
                CreateOrUpdateOverrideController(
                    CloveOverrideControllerPath,
                    baseController,
                    sourceClip,
                    null);
            animator.runtimeAnimatorController = overrideController;
            gunAnimator.runtimeAnimatorController = gunBaseController;
            ConfigurePresentationController(
                root,
                vandal,
                overrideController,
                "Vandal",
                animationDrivenLeftHandIK: false,
                useLeftHandIK: false);
            // The canonical TP Core Vandal clips own both arms throughout the
            // weapon flow. Preserve the shared rig for weapons such as Classic
            // and Operator, but opt Vandal out of it entirely.
            if (vandal.gameObject.activeInHierarchy)
            {
                controller.ConfigureRigEnabled(false);
                controller.ConfigureAnimationDrivenWeight(false);
            }

            // Defensive restore: the setup never owns the authored socket pose.
            socket.localPosition = socketPosition;
            socket.localRotation = socketRotation;
            socket.localScale = socketScale;
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(gunAnimator);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureCloveOperatorPrefabRoot(GameObject root)
        {
            Transform body = root.transform.Find("Body");
            Animator bodyAnimator = body != null
                ? body.GetComponent<Animator>()
                : null;
            ThirdPersonLeftHandIK controller =
                root.GetComponent<ThirdPersonLeftHandIK>();
            if (bodyAnimator == null || controller == null)
            {
                throw new InvalidOperationException(
                    "Clove is missing its Body Animator or left-hand IK controller.");
            }

            ThirdPersonWeaponPresentation vandalPresentation =
                FindPresentation(root, "Vandal");
            ThirdPersonWeaponGrip vandalGrip = vandalPresentation.WeaponObject
                .GetComponentInChildren<ThirdPersonWeaponGrip>(true);
            Transform vandalSocket = vandalGrip != null
                ? FindDescendant(vandalGrip.LeftHandTarget, VandalSocketName)
                : null;
            if (vandalGrip == null || vandalSocket == null)
            {
                throw new InvalidOperationException(
                    "Clove Vandal is missing its existing direct IK socket.");
            }
            vandalGrip.ConfigureDirectIKTarget(vandalSocket);

            ThirdPersonWeaponPresentation operatorPresentation =
                FindPresentation(root, "Operator");
            GameObject operatorObject = operatorPresentation.WeaponObject;
            Transform operatorRoot = operatorObject != null
                ? FindDescendant(operatorObject.transform, Operator3PName)
                : null;
            if (operatorRoot == null && operatorObject != null
                && operatorObject.name == Operator3PName)
            {
                operatorRoot = operatorObject.transform;
            }

            Animator gunAnimator = operatorObject != null
                ? operatorObject.GetComponentInChildren<Animator>(true)
                : null;
            ThirdPersonWeaponGrip operatorGrip = operatorObject != null
                ? operatorObject.GetComponentInChildren<ThirdPersonWeaponGrip>(true)
                : null;
            Transform operatorTarget = operatorGrip != null
                ? operatorGrip.LeftHandTarget
                : null;
            Transform operatorSocket = operatorTarget != null
                ? FindDescendant(operatorTarget, OperatorDirectTargetName)
                : null;
            Transform magazine = gunAnimator != null
                ? FindDescendant(gunAnimator.transform, MagazineMainName)
                : null;
            if (operatorRoot == null
                || gunAnimator == null
                || operatorGrip == null
                || operatorTarget == null
                || operatorSocket == null
                || magazine == null)
            {
                throw new InvalidOperationException(
                    "Clove Operator is missing its gun Animator, grip anchor, "
                    + "Left_Hand_Target_end, or Magazine_Main bone.");
            }

            Transform previousDirectTarget = operatorGrip.DirectIKTarget;
            if (previousDirectTarget != null
                && previousDirectTarget != operatorSocket)
            {
                throw new InvalidOperationException(
                    "Clove Operator already references a different direct IK target.");
            }

            // Initialize the existing end bone once from the calibrated grip.
            // Later reruns preserve any manual tuning made to this transform.
            if (previousDirectTarget == null)
            {
                operatorSocket.localPosition = DivideByLossyScale(
                    operatorGrip.HandPositionOffset,
                    operatorTarget.lossyScale);
                operatorSocket.localRotation = operatorGrip.HandRotationOffset;
                operatorSocket.localScale = Vector3.one;
            }
            operatorGrip.ConfigureDirectIKTarget(operatorSocket);

            Vector3 socketPosition = operatorSocket.localPosition;
            Quaternion socketRotation = operatorSocket.localRotation;
            Vector3 socketScale = operatorSocket.localScale;
            Vector3 operatorPosition = operatorRoot.localPosition;
            Quaternion operatorRotation = operatorRoot.localRotation;
            Vector3 operatorScale = operatorRoot.localScale;

            AnimatorController bodyBaseController =
                GetBaseAnimatorController(operatorPresentation.CharacterController);
            AnimatorController gunBaseController =
                GetBaseAnimatorController(gunAnimator);
            EnsureAssetFolder(CloveAnimationFolder);
            AnimationClip operatorAimClip = CreateOrUpdateOperatorAimPose(
                root,
                LoadImportedAnimationClip(OperatorAimClipPath));
            ConfigureOperatorAimControllers(
                bodyBaseController,
                gunBaseController,
                operatorAimClip);

            AnimatorState bodyReloadState = FindState(
                bodyBaseController,
                OperatorReloadStateName);
            AnimationClip bodySourceClip =
                bodyReloadState?.motion as AnimationClip;

            AnimatorState gunReloadState = FindState(
                gunBaseController,
                OperatorGunReloadStateName);
            AnimationClip gunSourceClip = gunReloadState?.motion as AnimationClip;
            if (bodySourceClip == null || gunSourceClip == null)
            {
                throw new InvalidOperationException(
                    "Clove Operator reload states are missing source AnimationClips.");
            }

            ValidateSynchronizedReloadStates(
                bodyReloadState,
                bodySourceClip,
                gunReloadState,
                gunSourceClip);

            AnimationClip bodyReloadClip =
                CreateOrUpdateOperatorReloadClip(bodySourceClip);
            AnimatorOverrideController bodyOverrideController =
                CreateOrUpdateOverrideController(
                    CloveOperatorOverrideControllerPath,
                    bodyBaseController,
                    bodySourceClip,
                    bodyReloadClip);
            gunAnimator.runtimeAnimatorController = gunBaseController;
            ConfigurePresentationController(
                root,
                operatorRoot,
                bodyOverrideController,
                "Operator");
            controller.ConfigureAnimationDrivenWeight(true);

            // Defensive restore: generated clips/controllers never own either
            // transform authored by the user in Prefab Mode.
            operatorRoot.localPosition = operatorPosition;
            operatorRoot.localRotation = operatorRotation;
            operatorRoot.localScale = operatorScale;
            operatorSocket.localPosition = socketPosition;
            operatorSocket.localRotation = socketRotation;
            operatorSocket.localScale = socketScale;
            RecordNestedPrefabOverride(operatorRoot);
            RecordNestedPrefabOverride(operatorSocket);
            EditorUtility.SetDirty(operatorRoot);
            EditorUtility.SetDirty(operatorSocket);
            EditorUtility.SetDirty(vandalGrip);
            EditorUtility.SetDirty(operatorGrip);
            EditorUtility.SetDirty(gunAnimator);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureCloveClassicPrefabRoot(GameObject root)
        {
            Transform body = root.transform.Find("Body");
            Animator bodyAnimator = body != null
                ? body.GetComponent<Animator>()
                : null;
            ThirdPersonLeftHandIK controller =
                root.GetComponent<ThirdPersonLeftHandIK>();
            TwoBoneIKConstraint constraint =
                root.GetComponentInChildren<TwoBoneIKConstraint>(true);
            if (bodyAnimator == null || controller == null || constraint == null)
            {
                throw new InvalidOperationException(
                    "Clove is missing its Body Animator or left-hand IK components.");
            }

            ThirdPersonWeaponPresentation classicPresentation =
                FindPresentation(root, "Classic");
            GameObject classicObject = classicPresentation.WeaponObject;
            Transform classicRoot = classicObject != null
                ? FindDescendant(classicObject.transform, Classic3PName)
                : null;
            if (classicRoot == null
                && classicObject != null
                && classicObject.name == Classic3PName)
            {
                classicRoot = classicObject.transform;
            }

            Animator gunAnimator = classicObject != null
                ? classicObject.GetComponentInChildren<Animator>(true)
                : null;
            ThirdPersonWeaponGrip classicGrip = classicObject != null
                ? classicObject.GetComponentInChildren<ThirdPersonWeaponGrip>(true)
                : null;
            Transform classicTarget = classicGrip != null
                ? classicGrip.LeftHandTarget
                : null;
            Transform classicSocket = classicTarget != null
                ? FindDescendant(classicTarget, ClassicDirectTargetName)
                : null;
            Transform magazine = gunAnimator != null
                ? FindDescendant(gunAnimator.transform, MagazineMainName)
                : null;
            if (classicRoot == null
                || gunAnimator == null
                || classicGrip == null
                || classicTarget == null
                || classicSocket == null
                || magazine == null)
            {
                throw new InvalidOperationException(
                    "Clove Classic is missing its gun Animator, grip anchor, "
                    + "Left_Hand_TargetSocket, or Magazine_Main bone.");
            }

            Transform previousDirectTarget = classicGrip.DirectIKTarget;
            if (previousDirectTarget != null
                && previousDirectTarget != classicSocket)
            {
                throw new InvalidOperationException(
                    "Clove Classic already references a different direct IK target.");
            }
            classicGrip.ConfigureDirectIKTarget(classicSocket);

            Vector3 weaponPosition = classicRoot.localPosition;
            Quaternion weaponRotation = classicRoot.localRotation;
            Vector3 weaponScale = classicRoot.localScale;
            Vector3 socketPosition = classicSocket.localPosition;
            Quaternion socketRotation = classicSocket.localRotation;
            Vector3 socketScale = classicSocket.localScale;

            AnimatorController bodyBaseController =
                GetBaseAnimatorController(classicPresentation.CharacterController);
            AnimatorState bodyReloadState = FindState(
                bodyBaseController,
                ClassicReloadStateName);
            AnimationClip bodySourceClip =
                bodyReloadState?.motion as AnimationClip;

            AnimatorController gunBaseController =
                GetBaseAnimatorController(gunAnimator);
            AnimatorState gunReloadState = FindState(
                gunBaseController,
                ClassicGunReloadStateName);
            AnimationClip gunSourceClip = gunReloadState?.motion as AnimationClip;
            if (bodySourceClip == null || gunSourceClip == null)
            {
                throw new InvalidOperationException(
                    "Clove Classic reload states are missing source AnimationClips.");
            }

            ValidateSynchronizedReloadStates(
                bodyReloadState,
                bodySourceClip,
                gunReloadState,
                gunSourceClip);

            EnsureAssetFolder(CloveAnimationFolder);
            AnimationClip bodyReloadClip =
                CreateOrUpdateClassicReloadClip(bodySourceClip);
            AnimatorOverrideController bodyOverrideController =
                CreateOrUpdateOverrideController(
                    CloveClassicOverrideControllerPath,
                    bodyBaseController,
                    bodySourceClip,
                    bodyReloadClip);
            gunAnimator.runtimeAnimatorController = gunBaseController;
            ConfigurePresentationController(
                root,
                classicRoot,
                bodyOverrideController,
                "Classic");
            if (classicObject.activeInHierarchy)
                bodyAnimator.runtimeAnimatorController = bodyOverrideController;
            controller.ConfigureAnimationDrivenWeight(true);

            // The generated assets own only animation curves. The user's
            // authored Classic placement and support-hand socket remain source
            // data and must survive every setup rerun unchanged.
            classicRoot.localPosition = weaponPosition;
            classicRoot.localRotation = weaponRotation;
            classicRoot.localScale = weaponScale;
            classicSocket.localPosition = socketPosition;
            classicSocket.localRotation = socketRotation;
            classicSocket.localScale = socketScale;

            TwoBoneIKConstraintData constraintData = constraint.data;
            constraintData.target = classicSocket;
            constraintData.maintainTargetPositionOffset = false;
            constraintData.maintainTargetRotationOffset = false;
            constraint.data = constraintData;

            RecordNestedPrefabOverride(classicRoot);
            RecordNestedPrefabOverride(classicSocket);
            RecordNestedPrefabOverride(classicGrip);
            RecordNestedPrefabOverride(gunAnimator);
            RecordNestedPrefabOverride(bodyAnimator);
            RecordNestedPrefabOverride(constraint);
            EditorUtility.SetDirty(classicGrip);
            EditorUtility.SetDirty(gunAnimator);
            EditorUtility.SetDirty(bodyAnimator);
            EditorUtility.SetDirty(constraint);
            EditorUtility.SetDirty(controller);
        }

        private static Avatar CreateOrUpdateCloveTPCoreAvatar()
        {
            GameObject bodyModel = AssetDatabase.LoadAssetAtPath<GameObject>(
                CloveBodyModelPath);
            Avatar cloveAvatar = AssetDatabase.LoadAllAssetsAtPath(CloveBodyModelPath)
                .OfType<Avatar>()
                .FirstOrDefault();
            Avatar canonicalAvatar = AssetDatabase
                .LoadAllAssetsAtPath(CanonicalTPCoreAvatarModelPath)
                .OfType<Avatar>()
                .FirstOrDefault();
            if (bodyModel == null
                || cloveAvatar == null
                || !cloveAvatar.isValid
                || canonicalAvatar == null
                || !canonicalAvatar.isValid)
            {
                throw new InvalidOperationException(
                    "Clove Body or the canonical TP Core Humanoid Avatar is invalid.");
            }

            HumanDescription description = cloveAvatar.humanDescription;
            Dictionary<string, SkeletonBone> canonicalSkeleton = canonicalAvatar
                .humanDescription
                .skeleton
                .ToDictionary(bone => bone.name, bone => bone);
            var fingerBoneNames = new HashSet<string>(
                description.human
                    .Where(bone => IsHumanoidFingerName(bone.humanName))
                    .Select(bone => bone.boneName));
            SkeletonBone[] adjustedSkeleton = description.skeleton;
            int replacedFingerRotations = 0;
            for (int i = 0; i < adjustedSkeleton.Length; i++)
            {
                SkeletonBone targetBone = adjustedSkeleton[i];
                if (!fingerBoneNames.Contains(targetBone.name)
                    || !canonicalSkeleton.TryGetValue(
                        targetBone.name,
                        out SkeletonBone canonicalBone))
                {
                    continue;
                }

                // Clove's authored torso and arm calibration already retargets
                // the TP Core upper-body motion correctly. Replacing those
                // rotations with source-local values changes the right-hand
                // weapon space and can throw the Odin away from the character.
                // Only the Humanoid finger axes differ enough to twist distal
                // joints, so keep every non-finger bone exactly as authored.
                targetBone.rotation = canonicalBone.rotation;
                adjustedSkeleton[i] = targetBone;
                replacedFingerRotations++;
            }
            description.skeleton = adjustedSkeleton;

            GameObject temporaryBody = null;
            Avatar generatedAvatar = null;
            try
            {
                temporaryBody = UnityEngine.Object.Instantiate(bodyModel);
                temporaryBody.name = "__CloveTPCoreAvatarBuild__";
                temporaryBody.hideFlags = HideFlags.HideAndDontSave;
                generatedAvatar = AvatarBuilder.BuildHumanAvatar(
                    temporaryBody,
                    description);
                if (generatedAvatar == null
                    || !generatedAvatar.isValid
                    || !generatedAvatar.isHuman)
                {
                    throw new InvalidOperationException(
                        "The hierarchy-compatible Clove TP Core Avatar could not be built.");
                }

                generatedAvatar.name = "CloveTPCoreAvatar";
                EnsureAssetFolder(CloveAnimationFolder);
                Avatar existingAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(
                    CloveTPCoreAvatarPath);
                if (existingAvatar == null)
                {
                    AssetDatabase.CreateAsset(
                        generatedAvatar,
                        CloveTPCoreAvatarPath);
                    generatedAvatar = null;
                }
                else
                {
                    EditorUtility.CopySerialized(generatedAvatar, existingAvatar);
                    existingAvatar.name = "CloveTPCoreAvatar";
                    EditorUtility.SetDirty(existingAvatar);
                }

                AssetDatabase.SaveAssets();
                Avatar savedAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(
                    CloveTPCoreAvatarPath);
                if (savedAvatar == null
                    || !savedAvatar.isValid
                    || !savedAvatar.isHuman)
                {
                    throw new InvalidOperationException(
                        "The saved Clove TP Core Avatar asset is invalid.");
                }

                Debug.Log(
                    "[ThirdPersonIK] Built CloveTPCoreAvatar with "
                    + $"{replacedFingerRotations} canonical TP Core finger rotations; "
                    + "Clove torso and arm calibration was preserved.");
                return savedAvatar;
            }
            finally
            {
                if (generatedAvatar != null)
                    UnityEngine.Object.DestroyImmediate(generatedAvatar);
                if (temporaryBody != null)
                    UnityEngine.Object.DestroyImmediate(temporaryBody);
            }
        }

        private static bool IsHumanoidFingerName(string humanName)
        {
            if (string.IsNullOrEmpty(humanName))
                return false;

            return humanName.IndexOf("Thumb", StringComparison.Ordinal) >= 0
                || humanName.IndexOf("Index", StringComparison.Ordinal) >= 0
                || humanName.IndexOf("Middle", StringComparison.Ordinal) >= 0
                || humanName.IndexOf("Ring", StringComparison.Ordinal) >= 0
                || humanName.IndexOf("Little", StringComparison.Ordinal) >= 0;
        }

        private static int RepairCloveBodyAvatarAndPose(
            GameObject root,
            Avatar avatar)
        {
            Transform body = root.transform.Find("Body");
            Animator animator = body != null ? body.GetComponent<Animator>() : null;
            if (animator == null || avatar == null || !avatar.isValid)
            {
                throw new InvalidOperationException(
                    "Clove is missing its Body Animator or compatible Avatar.");
            }

            GameObject nestedBody = PrefabUtility.GetNearestPrefabInstanceRoot(
                animator.gameObject);
            Animator sourceAnimator = PrefabUtility.GetCorrespondingObjectFromSource(
                animator);
            Transform sourceSkeleton = sourceAnimator != null
                ? sourceAnimator.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform => transform.name == "Skeleton")
                : null;
            if (nestedBody == null || sourceSkeleton == null)
            {
                throw new InvalidOperationException(
                    "Clove Body is not a valid nested model prefab instance.");
            }

            int removedPoseOverrides = RemoveSampledSkeletonPoseModifications(
                nestedBody,
                sourceSkeleton,
                removeAnimatorWarning: true);
            animator.avatar = avatar;
            var serializedAnimator = new SerializedObject(animator);
            SerializedProperty warning = serializedAnimator.FindProperty(
                "m_WarningMessage");
            if (warning != null)
            {
                warning.stringValue = string.Empty;
                serializedAnimator.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);

            // Assigning a Humanoid Avatar can evaluate the model immediately in
            // Prefab Mode. Unity may then add a complete set of bone transform
            // overrides after the first cleanup, even though AnimationMode is
            // already stopped. Remove that second wave after the Avatar has
            // been assigned so only the source FBX rest pose is authored.
            removedPoseOverrides += RemoveSampledSkeletonPoseModifications(
                nestedBody,
                sourceSkeleton,
                removeAnimatorWarning: false);
            EditorUtility.SetDirty(animator);
            return removedPoseOverrides;
        }

        private static int RemoveSampledSkeletonPoseModifications(
            GameObject nestedBody,
            Transform sourceSkeleton,
            bool removeAnimatorWarning)
        {
            PropertyModification[] modifications =
                PrefabUtility.GetPropertyModifications(nestedBody)
                ?? Array.Empty<PropertyModification>();
            int removedPoseOverrides = 0;
            var keptModifications = new List<PropertyModification>(
                modifications.Length);
            foreach (PropertyModification modification in modifications)
            {
                if (IsSampledSkeletonPoseModification(
                        modification,
                        sourceSkeleton))
                {
                    removedPoseOverrides++;
                    continue;
                }

                if (removeAnimatorWarning
                    && modification != null
                    && modification.target is Animator
                    && modification.propertyPath == "m_WarningMessage")
                {
                    continue;
                }

                keptModifications.Add(modification);
            }

            PrefabUtility.SetPropertyModifications(
                nestedBody,
                keptModifications.ToArray());
            return removedPoseOverrides;
        }

        private static bool IsSampledSkeletonPoseModification(
            PropertyModification modification,
            Transform sourceSkeleton)
        {
            if (modification == null
                || !(modification.target is Transform target)
                || (target != sourceSkeleton && !target.IsChildOf(sourceSkeleton)))
            {
                return false;
            }

            string propertyPath = modification.propertyPath;
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

        private static void ConfigureCloveOdinPrefabRoot(GameObject root)
        {
            Transform body = root.transform.Find("Body");
            Animator bodyAnimator = body != null
                ? body.GetComponent<Animator>()
                : null;
            ThirdPersonLeftHandIK controller =
                root.GetComponent<ThirdPersonLeftHandIK>();
            TwoBoneIKConstraint constraint =
                root.GetComponentInChildren<TwoBoneIKConstraint>(true);
            if (bodyAnimator == null || controller == null || constraint == null)
            {
                throw new InvalidOperationException(
                    "Clove is missing its Body Animator or left-hand IK components.");
            }

            ThirdPersonWeaponPresentation odinPresentation =
                FindPresentation(root, "Odin");
            GameObject odinObject = odinPresentation.WeaponObject;
            Transform odinRoot = odinObject != null
                ? FindDescendant(odinObject.transform, Odin3PName)
                : null;
            if (odinRoot == null
                && odinObject != null
                && odinObject.name == Odin3PName)
            {
                odinRoot = odinObject.transform;
            }

            Animator gunAnimator = odinObject != null
                ? odinObject.GetComponentInChildren<Animator>(true)
                : null;
            ThirdPersonWeaponGrip odinGrip = odinObject != null
                ? odinObject.GetComponentInChildren<ThirdPersonWeaponGrip>(true)
                : null;
            Transform odinTarget = odinGrip != null
                ? odinGrip.LeftHandTarget
                : null;
            Transform odinSocket = odinTarget != null
                ? FindDescendant(odinTarget, OdinDirectTargetName)
                : null;
            if (odinRoot == null
                || gunAnimator == null
                || odinGrip == null
                || odinTarget == null
                || odinSocket == null)
            {
                throw new InvalidOperationException(
                    "Clove Odin is missing its gun Animator, grip anchor, "
                    + "or Left_Hand_TargetSocket.");
            }

            Transform previousDirectTarget = odinGrip.DirectIKTarget;
            if (previousDirectTarget != null && previousDirectTarget != odinSocket)
            {
                throw new InvalidOperationException(
                    "Clove Odin already references a different direct IK target.");
            }
            if (previousDirectTarget == null
                && odinSocket.localPosition.sqrMagnitude < 0.0000000001f)
            {
                odinSocket.localPosition = DivideByLossyScale(
                    odinGrip.HandPositionOffset,
                    odinTarget.lossyScale);
                odinSocket.localRotation = odinGrip.HandRotationOffset;
                odinSocket.localScale = Vector3.one;
            }
            odinGrip.ConfigureDirectIKTarget(odinSocket);

            Vector3 weaponPosition = odinRoot.localPosition;
            Quaternion weaponRotation = odinRoot.localRotation;
            Vector3 weaponScale = odinRoot.localScale;
            Vector3 socketPosition = odinSocket.localPosition;
            Quaternion socketRotation = odinSocket.localRotation;
            Vector3 socketScale = odinSocket.localScale;

            AnimatorController bodyBaseController =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    OdinBodyControllerPath);
            AnimatorState bodyReloadState = FindState(
                bodyBaseController,
                OdinReloadStateName);
            AnimatorState bodyEquipState = FindState(
                bodyBaseController,
                "Odin Equip");
            AnimationClip bodyReloadSourceClip =
                bodyReloadState?.motion as AnimationClip;
            AnimationClip bodyEquipSourceClip =
                bodyEquipState?.motion as AnimationClip;
            AnimatorController gunBaseController =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    OdinGunControllerPath);
            AnimationClip gunStaticPose =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    CloveOdinStaticPoseClipPath);
            if (bodyReloadSourceClip == null
                || bodyEquipSourceClip == null
                || gunBaseController == null
                || gunStaticPose == null)
            {
                throw new InvalidOperationException(
                    "Clove Odin is missing its body clips, gun controller, "
                    + "or 3P static gun pose.");
            }

            EnsureAssetFolder(CloveAnimationFolder);
            NormalizeOdinStaticGunMotion(
                gunStaticPose,
                gunAnimator.transform.localScale);
            AnimatorController bodyController = CreateOrUpdateOdinBodyController(
                bodyBaseController,
                bodyEquipSourceClip,
                bodyReloadSourceClip);
            ClearLegacyOdinIKOverrides();
            AnimatorController gunController = CreateOrUpdateOdinGunController(
                gunBaseController,
                gunStaticPose);
            ConfigurePresentationController(
                root,
                odinRoot,
                bodyController,
                "Odin",
                animationDrivenLeftHandIK: false,
                useLeftHandIK: false);
            SetActiveThirdPersonWeapon(root, "Odin");
            bodyAnimator.runtimeAnimatorController = bodyController;
            gunAnimator.runtimeAnimatorController = gunController;
            controller.ConfigureRigEnabled(false);
            controller.ConfigureAnimationDrivenWeight(false);

            odinRoot.localPosition = weaponPosition;
            odinRoot.localRotation = weaponRotation;
            odinRoot.localScale = weaponScale;
            odinSocket.localPosition = socketPosition;
            odinSocket.localRotation = socketRotation;
            odinSocket.localScale = socketScale;

            TwoBoneIKConstraintData constraintData = constraint.data;
            constraintData.target = odinSocket;
            constraintData.maintainTargetPositionOffset = false;
            constraintData.maintainTargetRotationOffset = false;
            constraint.data = constraintData;

            RecordNestedPrefabOverride(odinRoot);
            RecordNestedPrefabOverride(odinSocket);
            RecordNestedPrefabOverride(odinGrip);
            RecordNestedPrefabOverride(bodyAnimator);
            RecordNestedPrefabOverride(gunAnimator);
            RecordNestedPrefabOverride(constraint);
            EditorUtility.SetDirty(odinGrip);
            EditorUtility.SetDirty(bodyAnimator);
            EditorUtility.SetDirty(gunAnimator);
            EditorUtility.SetDirty(constraint);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureOperatorAimControllers(
            AnimatorController bodyController,
            AnimatorController gunController,
            AnimationClip operatorAimClip)
        {
            EnsureBoolParameter(bodyController, OperatorAimingParameterName);
            EnsureBoolParameter(gunController, OperatorAimingParameterName);

            AnimatorStateMachine bodyStateMachine = GetLayerStateMachine(
                bodyController,
                "Upper Body Gun Pose");
            AnimatorState holdState = FindDirectState(
                bodyStateMachine,
                OperatorHoldStateName);
            AnimatorState hipFireState = FindDirectState(
                bodyStateMachine,
                OperatorFireStateName);
            if (holdState == null || hipFireState == null)
            {
                throw new InvalidOperationException(
                    "Operator upper-body layer is missing its Hold or Fire state.");
            }

            AnimatorState aimState = GetOrAddDirectState(
                bodyStateMachine,
                OperatorAimStateName,
                new Vector3(480f, -20f));
            aimState.motion = operatorAimClip;
            aimState.speed = 1f;
            aimState.writeDefaultValues = holdState.writeDefaultValues;

            RemoveTransitionsTo(holdState, aimState);
            RemoveAllTransitions(aimState);
            RemoveDirectState(bodyStateMachine, "Operator Fire Zoomed");
            RemoveAnyStateTransitionsForParameter(bodyStateMachine, "Fire");

            ConfigureImmediateBoolTransition(
                holdState.AddTransition(aimState),
                OperatorAimingParameterName,
                true,
                0.12f);
            ConfigureImmediateBoolTransition(
                aimState.AddTransition(holdState),
                OperatorAimingParameterName,
                false,
                0.12f);

            AnimatorStateTransition hipFireTransition =
                bodyStateMachine.AddAnyStateTransition(hipFireState);
            ConfigureTriggeredTransition(
                hipFireTransition,
                "Fire",
                canTransitionToSelf: true);

            AnimatorStateMachine gunStateMachine = GetLayerStateMachine(
                gunController,
                "Base Layer");
            AnimatorState gunIdleState = FindDirectState(gunStateMachine, "Idle");
            AnimatorState gunHipFireState = FindDirectState(
                gunStateMachine,
                OperatorGunFireStateName);
            if (gunIdleState == null || gunHipFireState == null)
            {
                throw new InvalidOperationException(
                    "Operator gun layer is missing its Idle or Fire state.");
            }

            RemoveDirectState(gunStateMachine, "Fire Zoomed");
            RemoveAnyStateTransitionsForParameter(gunStateMachine, "Fire");

            AnimatorStateTransition gunHipFireTransition =
                gunStateMachine.AddAnyStateTransition(gunHipFireState);
            ConfigureTriggeredTransition(
                gunHipFireTransition,
                "Fire",
                canTransitionToSelf: true);

            EditorUtility.SetDirty(bodyController);
            EditorUtility.SetDirty(gunController);
            EditorUtility.SetDirty(aimState);
        }

        private static AnimatorController CreateOrUpdateOdinBodyController(
            AnimatorController sourceController,
            AnimationClip sourceEquipClip,
            AnimationClip sourceReloadClip)
        {
            AnimatorController controller = LoadOrCreateControllerCopy(
                sourceController,
                OdinBodyControllerPath,
                CloveOdinBodyControllerPath);
            AnimatorState reloadState = FindState(
                controller,
                OdinReloadStateName);
            AnimatorState equipState = FindState(controller, "Odin Equip");
            if (equipState == null || reloadState == null)
            {
                throw new InvalidOperationException(
                    "The full Clove Odin body controller has no equip or reload state.");
            }
            // Preserve Odin's canonical humanoid 3P animation. The previous
            // copies added Animation Rigging weight curves and masked part of
            // the authored support-hand movement during equip and reload.
            equipState.motion = sourceEquipClip;
            reloadState.motion = sourceReloadClip;

            AnimatorStateMachine stateMachine = GetLayerStateMachine(
                controller,
                "Upper Body Gun Pose");
            RemoveDirectState(stateMachine, OdinAimStateName);
            RemoveControllerParameter(controller, OdinAimingParameterName);

            // ADS is intentionally Operator-only. Remove the stale Odin pose
            // generated by earlier revisions so it cannot be wired back in by
            // accident or mistaken for a supported Odin state.
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    ObsoleteCloveOdinAimPoseClipPath) != null)
            {
                AssetDatabase.DeleteAsset(ObsoleteCloveOdinAimPoseClipPath);
            }

            EditorUtility.SetDirty(equipState);
            EditorUtility.SetDirty(reloadState);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ClearLegacyOdinIKOverrides()
        {
            AnimatorOverrideController legacyController =
                AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                    LegacyCloveOdinOverrideControllerPath);
            if (legacyController == null)
                return;

            var overrides =
                new List<KeyValuePair<AnimationClip, AnimationClip>>();
            legacyController.GetOverrides(overrides);
            bool changed = false;
            for (int i = 0; i < overrides.Count; i++)
            {
                AnimationClip overrideClip = overrides[i].Value;
                if (overrideClip == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(overrideClip);
                if (!path.EndsWith("CloveOdinEquipLeftHandIK.anim", StringComparison.Ordinal)
                    && !path.EndsWith("CloveOdinReloadLeftHandIK.anim", StringComparison.Ordinal))
                {
                    continue;
                }

                overrides[i] =
                    new KeyValuePair<AnimationClip, AnimationClip>(
                        overrides[i].Key,
                        null);
                changed = true;
            }

            if (!changed)
                return;

            legacyController.ApplyOverrides(overrides);
            EditorUtility.SetDirty(legacyController);
        }

        private static AnimatorController CreateOrUpdateOdinGunController(
            AnimatorController sourceController,
            AnimationClip staticPose)
        {
            AnimatorController controller = LoadOrCreateControllerCopy(
                sourceController,
                OdinGunControllerPath,
                CloveOdinGunControllerPath);
            foreach (string stateName in new[]
            {
                "Idle",
                "Equip",
                "Reload",
                "Fire",
                "Inspect"
            })
            {
                AnimatorState state = FindState(controller, stateName);
                if (state == null)
                {
                    throw new InvalidOperationException(
                        $"The full Clove Odin gun controller has no "
                        + $"'{stateName}' state.");
                }
                state.motion = staticPose;
                EditorUtility.SetDirty(state);
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void NormalizeOdinStaticGunMotion(
            AnimationClip clip,
            Vector3 animatorRootScale)
        {
            float duration = Mathf.Max(1f, clip.length);
            int removedCurves = 0;
            foreach (EditorCurveBinding binding in AnimationUtility
                         .GetCurveBindings(clip)
                         .Where(binding => binding.type == typeof(Transform))
                         .ToArray())
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
                removedCurves++;
            }

            foreach (EditorCurveBinding binding in AnimationUtility
                         .GetObjectReferenceCurveBindings(clip)
                         .Where(binding => binding.type == typeof(Transform))
                         .ToArray())
            {
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                removedCurves++;
            }

            // The Odin package has no compatible GNTP gun motion. Keep a
            // one-second, transform-neutral motion so its replicated gun state
            // machine retains meaningful exit times while the authored prefab
            // pose remains untouched. The only curves preserve the .ao
            // Animator's own scale (100 in this asset); all weapon-bone
            // position/rotation curves are deliberately absent.
            string[] scaleProperties =
            {
                "m_LocalScale.x",
                "m_LocalScale.y",
                "m_LocalScale.z"
            };
            for (int i = 0; i < scaleProperties.Length; i++)
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        string.Empty,
                        typeof(Transform),
                        scaleProperties[i]),
                    AnimationCurve.Constant(
                        0f,
                        duration,
                        animatorRootScale[i]));
            }

            EditorUtility.SetDirty(clip);
            Debug.Log(
                $"[ThirdPersonIK] Replaced {removedCurves} incompatible Odin gun "
                + "Transform curves with a neutral authored-scale motion.");
        }

        private static AnimatorController LoadOrCreateControllerCopy(
            AnimatorController sourceController,
            string canonicalSourcePath,
            string destinationPath)
        {
            if (sourceController == null)
            {
                throw new InvalidOperationException(
                    "Cannot create a full Odin controller without a source.");
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourceController);
            AnimatorController destination =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    destinationPath);
            if (destination != null)
                return destination;

            if (sourcePath == destinationPath)
                return sourceController;
            if (sourcePath != canonicalSourcePath)
            {
                AnimatorController canonical =
                    AssetDatabase.LoadAssetAtPath<AnimatorController>(
                        canonicalSourcePath);
                if (canonical == null)
                {
                    throw new InvalidOperationException(
                        $"Canonical Odin controller was not found at "
                        + $"{canonicalSourcePath}.");
                }
                sourceController = canonical;
                sourcePath = canonicalSourcePath;
            }

            if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
            {
                throw new InvalidOperationException(
                    $"Could not copy {sourcePath} to {destinationPath}.");
            }
            AssetDatabase.ImportAsset(
                destinationPath,
                ImportAssetOptions.ForceSynchronousImport);
            destination = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                destinationPath);
            if (destination == null)
            {
                throw new InvalidOperationException(
                    $"Copied Odin controller could not be loaded from "
                    + $"{destinationPath}.");
            }
            return destination;
        }

        private static void EnsureBoolParameter(
            AnimatorController controller,
            string parameterName)
        {
            AnimatorControllerParameter existing = controller.parameters
                .FirstOrDefault(parameter => parameter.name == parameterName);
            if (existing != null)
            {
                if (existing.type != AnimatorControllerParameterType.Bool)
                {
                    throw new InvalidOperationException(
                        $"Animator parameter '{parameterName}' must be a bool.");
                }
                return;
            }

            controller.AddParameter(
                parameterName,
                AnimatorControllerParameterType.Bool);
        }

        private static AnimatorStateMachine GetLayerStateMachine(
            AnimatorController controller,
            string layerName)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name == layerName)
                    return layer.stateMachine;
            }

            throw new InvalidOperationException(
                $"Animator layer '{layerName}' was not found in {controller.name}.");
        }

        private static AnimatorState FindDirectState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state.name == stateName)
                    return child.state;
            }

            return null;
        }

        private static AnimatorState GetOrAddDirectState(
            AnimatorStateMachine stateMachine,
            string stateName,
            Vector3 position)
        {
            return FindDirectState(stateMachine, stateName)
                ?? stateMachine.AddState(stateName, position);
        }

        private static AnimationClip LoadImportedAnimationClip(string assetPath)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => !candidate.name.StartsWith(
                    "__preview__",
                    StringComparison.Ordinal));
            if (clip == null)
            {
                throw new InvalidOperationException(
                    $"No imported AnimationClip was found at {assetPath}.");
            }

            return clip;
        }

        private static AnimationClip CreateOrUpdateOperatorAimPose(
            GameObject sourceRoot,
            AnimationClip sourceClip)
        {
            return CreateOrUpdateWeaponAimPose(
                sourceRoot,
                sourceClip,
                Operator3PName,
                CloveOperatorAimPoseClipPath,
                "CloveOperatorAimPose",
                OperatorAimEyeRelief,
                "Operator");
        }

        private static AnimationClip CreateOrUpdateWeaponAimPose(
            GameObject sourceRoot,
            AnimationClip sourceClip,
            string weaponRootName,
            string destinationPath,
            string destinationName,
            float targetEyeRelief,
            string weaponLabel)
        {
            if (sourceRoot == null || sourceClip == null || !sourceClip.humanMotion)
            {
                throw new InvalidOperationException(
                    $"Clove {weaponLabel} ADS requires a valid Humanoid source pose.");
            }

            UnityEngine.SceneManagement.Scene previewScene =
                EditorSceneManager.NewPreviewScene();
            GameObject sampleRoot = null;
            HumanPoseHandler poseHandler = null;
            try
            {
                sampleRoot = UnityEngine.Object.Instantiate(sourceRoot);
                sampleRoot.name = destinationName + "Sample";
                sampleRoot.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                    sampleRoot,
                    previewScene);
                sampleRoot.SetActive(true);

                Animator animator = sampleRoot.transform.Find("Body")?
                    .GetComponent<Animator>();
                if (animator == null
                    || animator.avatar == null
                    || !animator.avatar.isHuman)
                {
                    throw new InvalidOperationException(
                        $"Clove {weaponLabel} ADS sample has no valid "
                        + "Humanoid Animator.");
                }

                animator.Rebind();
                sourceClip.SampleAnimation(animator.gameObject, 0f);

                Transform rightEye = animator.GetBoneTransform(
                    HumanBodyBones.RightEye);
                Transform upperArm = animator.GetBoneTransform(
                    HumanBodyBones.RightUpperArm);
                Transform lowerArm = animator.GetBoneTransform(
                    HumanBodyBones.RightLowerArm);
                Transform rightHand = animator.GetBoneTransform(
                    HumanBodyBones.RightHand);
                Transform weaponRoot = FindDescendant(
                    sampleRoot.transform,
                    weaponRootName);
                Transform scopeTarget = FindDescendant(
                    weaponRoot,
                    "ScopeTarget");
                Transform muzzle = FindDescendant(weaponRoot, "Muzzle");
                if (rightEye == null
                    || upperArm == null
                    || lowerArm == null
                    || rightHand == null
                    || weaponRoot == null
                    || scopeTarget == null
                    || muzzle == null)
                {
                    throw new InvalidOperationException(
                        $"Clove {weaponLabel} ADS sample is missing its right arm, "
                        + "RightEye, ScopeTarget, or Muzzle.");
                }
                if (!weaponRoot.IsChildOf(rightHand))
                {
                    throw new InvalidOperationException(
                        $"{weaponRootName} must remain parented below Clove's right hand "
                        + "for the baked ADS pose to move the weapon rigidly.");
                }

                poseHandler = new HumanPoseHandler(
                    animator.avatar,
                    animator.transform);
                var sourcePose = new HumanPose();
                poseHandler.GetHumanPose(ref sourcePose);

                Vector3 sightAxis = muzzle.position - scopeTarget.position;
                if (sightAxis.sqrMagnitude < 0.000001f)
                {
                    throw new InvalidOperationException(
                        $"{weaponLabel} ScopeTarget and Muzzle do not define "
                        + "a sight axis.");
                }
                sightAxis.Normalize();
                Vector3 desiredScopePosition = rightEye.position
                    + sightAxis * targetEyeRelief;
                Vector3 handCorrection = desiredScopePosition
                    - scopeTarget.position;
                SolveTwoBoneArm(
                    upperArm,
                    lowerArm,
                    rightHand,
                    rightHand.position + handCorrection);

                float sightLineMiss = DistanceFromPointToLine(
                    rightEye.position,
                    scopeTarget.position,
                    muzzle.position);
                float eyeRelief = Vector3.Dot(
                    scopeTarget.position - rightEye.position,
                    (muzzle.position - scopeTarget.position).normalized);
                if (sightLineMiss > 0.005f
                    || Mathf.Abs(eyeRelief - targetEyeRelief) > 0.005f)
                {
                    throw new InvalidOperationException(
                        $"Clove {weaponLabel} ADS solve did not converge "
                        + $"(line miss {sightLineMiss:F4}m, "
                        + $"eye relief {eyeRelief:F4}m)." );
                }

                var solvedPose = new HumanPose();
                poseHandler.GetHumanPose(ref solvedPose);
                AnimationClip clip = CopyAnimationClipToAsset(
                    sourceClip,
                    destinationPath,
                    destinationName);

                float curveEnd = Mathf.Max(
                    sourceClip.length,
                    1f / Mathf.Max(1f, sourceClip.frameRate));
                int changedMuscles = 0;
                for (int i = 0; i < HumanTrait.MuscleCount; i++)
                {
                    string muscleName = HumanTrait.MuscleName[i];
                    if (!IsRightArmMuscle(muscleName)
                        || Mathf.Abs(solvedPose.muscles[i]
                            - sourcePose.muscles[i]) < 0.0001f)
                    {
                        continue;
                    }

                    AnimationUtility.SetEditorCurve(
                        clip,
                        EditorCurveBinding.FloatCurve(
                            string.Empty,
                            typeof(Animator),
                            muscleName),
                        AnimationCurve.Constant(
                            0f,
                            curveEnd,
                            solvedPose.muscles[i]));
                    changedMuscles++;
                }

                if (changedMuscles == 0)
                {
                    throw new InvalidOperationException(
                        $"Clove {weaponLabel} ADS solve produced no "
                        + "right-arm muscles.");
                }

                EditorUtility.SetDirty(clip);
                Debug.Log(
                    $"[ThirdPersonIK] Baked Clove {weaponLabel} ADS with "
                    + $"{changedMuscles} right-arm muscles, "
                    + $"{eyeRelief:F3}m eye relief and "
                    + $"{sightLineMiss:F4}m sight-line miss.");
                return clip;
            }
            finally
            {
                poseHandler?.Dispose();
                if (sampleRoot != null)
                    UnityEngine.Object.DestroyImmediate(sampleRoot);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static AnimationClip CopyAnimationClipToAsset(
            AnimationClip source,
            string assetPath,
            string clipName)
        {
            AnimationClip destination =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (destination == null)
            {
                destination = new AnimationClip();
                AssetDatabase.CreateAsset(destination, assetPath);
            }
            else
            {
                destination.ClearCurves();
            }

            destination.name = clipName;
            destination.frameRate = source.frameRate;
            destination.wrapMode = source.wrapMode;
            destination.legacy = source.legacy;
            AnimationUtility.SetAnimationClipSettings(
                destination,
                AnimationUtility.GetAnimationClipSettings(source));

            foreach (EditorCurveBinding binding in
                AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(
                    source,
                    binding);
                var curveCopy = new AnimationCurve(sourceCurve.keys)
                {
                    preWrapMode = sourceCurve.preWrapMode,
                    postWrapMode = sourceCurve.postWrapMode
                };
                AnimationUtility.SetEditorCurve(destination, binding, curveCopy);
            }

            foreach (EditorCurveBinding binding in
                AnimationUtility.GetObjectReferenceCurveBindings(source))
            {
                AnimationUtility.SetObjectReferenceCurve(
                    destination,
                    binding,
                    AnimationUtility.GetObjectReferenceCurve(source, binding));
            }
            AnimationUtility.SetAnimationEvents(
                destination,
                AnimationUtility.GetAnimationEvents(source));
            return destination;
        }

        private static void SolveTwoBoneArm(
            Transform upperArm,
            Transform lowerArm,
            Transform hand,
            Vector3 targetPosition)
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
                    $"Clove Operator ADS target is outside the right arm's "
                    + $"reachable range ({targetDistance:F4}m not in "
                    + $"[{minimumReach:F4}, {maximumReach:F4}]m)." );
            }

            Vector3 targetDirection = targetVector / targetDistance;
            Vector3 bendDirection = elbowPosition - shoulderPosition;
            bendDirection -= Vector3.Dot(bendDirection, targetDirection)
                * targetDirection;
            if (bendDirection.sqrMagnitude < 0.000001f)
            {
                bendDirection = Vector3.Cross(
                    targetDirection,
                    upperArm.forward);
            }
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

        private static bool IsRightArmMuscle(string muscleName)
        {
            return muscleName.StartsWith("Right Arm ", StringComparison.Ordinal)
                || muscleName.StartsWith(
                    "Right Forearm ",
                    StringComparison.Ordinal)
                || muscleName.StartsWith(
                    "Right Hand ",
                    StringComparison.Ordinal);
        }

        private static float DistanceFromPointToLine(
            Vector3 point,
            Vector3 lineStart,
            Vector3 lineEnd)
        {
            Vector3 line = lineEnd - lineStart;
            if (line.sqrMagnitude < 0.000001f)
                return float.PositiveInfinity;
            return Vector3.Cross(point - lineStart, line.normalized).magnitude;
        }

        private static void RemoveTransitionsTo(
            AnimatorState source,
            AnimatorState destination)
        {
            foreach (AnimatorStateTransition transition in source.transitions.ToArray())
            {
                if (transition.destinationState == destination)
                    source.RemoveTransition(transition);
            }
        }

        private static void RemoveAllTransitions(AnimatorState state)
        {
            foreach (AnimatorStateTransition transition in state.transitions.ToArray())
                state.RemoveTransition(transition);
        }

        private static void RemoveDirectState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            AnimatorState state = FindDirectState(stateMachine, stateName);
            if (state == null)
                return;

            foreach (ChildAnimatorState child in stateMachine.states)
                RemoveTransitionsTo(child.state, state);
            foreach (AnimatorStateTransition transition in
                stateMachine.anyStateTransitions.ToArray())
            {
                if (transition.destinationState == state)
                    stateMachine.RemoveAnyStateTransition(transition);
            }
            RemoveAllTransitions(state);
            stateMachine.RemoveState(state);
        }

        private static void RemoveControllerParameter(
            AnimatorController controller,
            string parameterName)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = parameters.Length - 1; i >= 0; i--)
            {
                if (parameters[i].name == parameterName)
                    controller.RemoveParameter(i);
            }
        }

        private static void RemoveAnyStateTransitionsForParameter(
            AnimatorStateMachine stateMachine,
            string parameterName)
        {
            foreach (AnimatorStateTransition transition in
                stateMachine.anyStateTransitions.ToArray())
            {
                if (transition.conditions.Any(condition =>
                    condition.parameter == parameterName))
                {
                    stateMachine.RemoveAnyStateTransition(transition);
                }
            }
        }

        private static void ConfigureImmediateBoolTransition(
            AnimatorStateTransition transition,
            string parameterName,
            bool expected,
            float durationSeconds)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = durationSeconds;
            transition.AddCondition(
                expected ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                0f,
                parameterName);
        }

        private static void ConfigureTriggeredTransition(
            AnimatorStateTransition transition,
            string parameterName,
            bool canTransitionToSelf)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
            transition.canTransitionToSelf = canTransitionToSelf;
            transition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                parameterName);
        }

        private static void ConfigurePresentationController(
            GameObject root,
            Transform weaponRoot,
            RuntimeAnimatorController characterController,
            string weaponName,
            bool animationDrivenLeftHandIK = true,
            bool useLeftHandIK = true)
        {
            PlayerVisibilityController visibility =
                root.GetComponent<PlayerVisibilityController>();
            if (visibility == null)
            {
                throw new InvalidOperationException(
                    "Clove is missing PlayerVisibilityController.");
            }

            var serializedVisibility = new SerializedObject(visibility);
            SerializedProperty presentations = serializedVisibility.FindProperty(
                "thirdPersonWeaponPresentations");
            if (presentations == null || !presentations.isArray)
            {
                throw new InvalidOperationException(
                    "Clove third-person weapon presentations could not be serialized.");
            }

            bool configured = false;
            for (int i = 0; i < presentations.arraySize; i++)
            {
                SerializedProperty presentation =
                    presentations.GetArrayElementAtIndex(i);
                SerializedProperty weaponObjectProperty =
                    presentation.FindPropertyRelative("weaponObject");
                GameObject weaponObject =
                    weaponObjectProperty?.objectReferenceValue as GameObject;
                if (weaponObject == null
                    || (weaponObject.transform != weaponRoot
                        && !weaponRoot.IsChildOf(weaponObject.transform)))
                {
                    continue;
                }

                SerializedProperty controllerProperty =
                    presentation.FindPropertyRelative("characterController");
                if (controllerProperty == null)
                {
                    throw new InvalidOperationException(
                        $"{weaponName} presentation has no characterController field.");
                }

                controllerProperty.objectReferenceValue = characterController;
                SerializedProperty useRigProperty =
                    presentation.FindPropertyRelative("useLeftHandIK");
                if (useRigProperty == null)
                {
                    throw new InvalidOperationException(
                        $"{weaponName} presentation has no useLeftHandIK field.");
                }

                useRigProperty.boolValue = useLeftHandIK;
                SerializedProperty weightModeProperty =
                    presentation.FindPropertyRelative(
                        "animationDrivenLeftHandIK");
                if (weightModeProperty == null)
                {
                    throw new InvalidOperationException(
                        $"{weaponName} presentation has no "
                        + "animationDrivenLeftHandIK field.");
                }

                weightModeProperty.boolValue = animationDrivenLeftHandIK;
                configured = true;
                break;
            }

            if (!configured)
            {
                throw new InvalidOperationException(
                    $"Clove {weaponName} third-person presentation was not found.");
            }

            serializedVisibility.ApplyModifiedPropertiesWithoutUndo();
            RecordNestedPrefabOverride(visibility);
            EditorUtility.SetDirty(visibility);
        }

        private static void SetActiveThirdPersonWeapon(
            GameObject root,
            string weaponName)
        {
            PlayerVisibilityController visibility =
                root.GetComponent<PlayerVisibilityController>();
            if (visibility == null
                || visibility.ThirdPersonWeaponPresentations == null)
            {
                throw new InvalidOperationException(
                    "Clove has no third-person weapon presentations.");
            }

            bool found = false;
            foreach (ThirdPersonWeaponPresentation presentation in
                visibility.ThirdPersonWeaponPresentations)
            {
                if (presentation?.WeaponObject == null)
                    continue;

                bool shouldBeActive = presentation.WeaponData != null
                    && presentation.WeaponData.name == weaponName;
                presentation.WeaponObject.SetActive(shouldBeActive);
                RecordNestedPrefabOverride(presentation.WeaponObject);
                EditorUtility.SetDirty(presentation.WeaponObject);
                found |= shouldBeActive;
            }

            if (!found)
            {
                throw new InvalidOperationException(
                    $"Clove {weaponName} presentation was not found.");
            }
        }

        private static ThirdPersonWeaponPresentation FindPresentation(
            GameObject root,
            string weaponName)
        {
            PlayerVisibilityController visibility =
                root.GetComponent<PlayerVisibilityController>();
            ThirdPersonWeaponPresentation presentation = visibility?
                .ThirdPersonWeaponPresentations?
                .FirstOrDefault(candidate =>
                    candidate?.WeaponData != null
                    && candidate.WeaponData.name == weaponName);
            if (presentation?.WeaponObject == null)
            {
                throw new InvalidOperationException(
                    $"Clove {weaponName} third-person presentation was not found.");
            }

            return presentation;
        }

        private static AnimatorController GetBaseAnimatorController(Animator animator)
        {
            return GetBaseAnimatorController(animator.runtimeAnimatorController);
        }

        private static AnimatorController GetBaseAnimatorController(
            RuntimeAnimatorController runtimeController)
        {
            if (runtimeController is AnimatorOverrideController currentOverride)
                runtimeController = currentOverride.runtimeAnimatorController;

            if (runtimeController is AnimatorController controller)
                return controller;

            throw new InvalidOperationException(
                "Clove Body Animator does not use an AnimatorController.");
        }

        private static AnimatorState FindState(
            AnimatorController controller,
            string stateName)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                AnimatorState state = FindState(layer.stateMachine, stateName);
                if (state != null)
                    return state;
            }

            return null;
        }

        private static AnimatorState FindState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state.name == stateName)
                    return child.state;
            }

            foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
            {
                AnimatorState state = FindState(child.stateMachine, stateName);
                if (state != null)
                    return state;
            }

            return null;
        }

        private static AnimationClip CreateOrUpdateReloadClip(
            AnimationClip sourceClip)
        {
            AnimationClip clip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(CloveReloadClipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, CloveReloadClipPath);
            }

            EditorUtility.CopySerialized(sourceClip, clip);
            clip.name = "CloveVandalReloadLeftHandIK";

            float frameRate = Mathf.Max(1f, sourceClip.frameRate);
            float lastFrame = Mathf.Round(sourceClip.length * frameRate);
            AnimationCurve weightCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(FrameToTime(7f, frameRate, lastFrame), 1f),
                new Keyframe(FrameToTime(8f, frameRate, lastFrame), 0f),
                new Keyframe(FrameToTime(40f, frameRate, lastFrame), 0f),
                new Keyframe(FrameToTime(44f, frameRate, lastFrame), 1f),
                new Keyframe(FrameToTime(69f, frameRate, lastFrame), 1f),
                new Keyframe(FrameToTime(71f, frameRate, lastFrame), 0f),
                new Keyframe(FrameToTime(72f, frameRate, lastFrame), 0f),
                new Keyframe(FrameToTime(76f, frameRate, lastFrame), 1f),
                new Keyframe(sourceClip.length, 1f));
            SetLinearTangents(weightCurve);

            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(
                ConstraintAnimationPath,
                typeof(TwoBoneIKConstraint),
                "m_Weight");
            AnimationUtility.SetEditorCurve(clip, binding, weightCurve);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip CreateOrUpdateOperatorReloadClip(
            AnimationClip sourceClip)
        {
            AnimationClip clip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    CloveOperatorReloadClipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, CloveOperatorReloadClipPath);
            }

            EditorUtility.CopySerialized(sourceClip, clip);
            clip.name = "CloveOperatorReloadLeftHandIK";

            float frameRate = Mathf.Max(1f, sourceClip.frameRate);
            float lastFrame = Mathf.Round(sourceClip.length * frameRate);
            AnimationCurve weightCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(FrameToTime(6f, frameRate, lastFrame), 1f),
                new Keyframe(FrameToTime(8f, frameRate, lastFrame), 0f),
                new Keyframe(FrameToTime(49f, frameRate, lastFrame), 0f),
                new Keyframe(FrameToTime(53f, frameRate, lastFrame), 1f),
                new Keyframe(FrameToTime(69f, frameRate, lastFrame), 1f),
                new Keyframe(FrameToTime(71f, frameRate, lastFrame), 0f),
                new Keyframe(FrameToTime(132f, frameRate, lastFrame), 0f),
                new Keyframe(FrameToTime(136f, frameRate, lastFrame), 1f),
                new Keyframe(sourceClip.length, 1f));
            SetLinearTangents(weightCurve);

            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(
                ConstraintAnimationPath,
                typeof(TwoBoneIKConstraint),
                "m_Weight");
            AnimationUtility.SetEditorCurve(clip, binding, weightCurve);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip CreateOrUpdateClassicReloadClip(
            AnimationClip sourceClip)
        {
            AnimationClip clip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    CloveClassicReloadClipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, CloveClassicReloadClipPath);
            }

            EditorUtility.CopySerialized(sourceClip, clip);
            clip.name = "CloveClassicReloadLeftHandIK";

            float frameRate = Mathf.Max(1f, sourceClip.frameRate);
            float lastFrame = Mathf.Round(sourceClip.length * frameRate);
            AnimationCurve weightCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(FrameToTime(1f, frameRate, lastFrame), 1f),
                new Keyframe(FrameToTime(3f, frameRate, lastFrame), 0f),
                new Keyframe(FrameToTime(18f, frameRate, lastFrame), 0f),
                new Keyframe(FrameToTime(20f, frameRate, lastFrame), 1f),
                new Keyframe(FrameToTime(26f, frameRate, lastFrame), 1f),
                new Keyframe(FrameToTime(28f, frameRate, lastFrame), 0f),
                new Keyframe(FrameToTime(54f, frameRate, lastFrame), 0f),
                new Keyframe(FrameToTime(56f, frameRate, lastFrame), 1f),
                new Keyframe(sourceClip.length, 1f));
            SetLinearTangents(weightCurve);

            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(
                ConstraintAnimationPath,
                typeof(TwoBoneIKConstraint),
                "m_Weight");
            AnimationUtility.SetEditorCurve(clip, binding, weightCurve);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip CreateOrUpdateGunReloadClip(
            GameObject root,
            Animator bodyAnimator,
            AnimationClip bodySourceClip,
            Animator gunAnimator,
            AnimationClip gunSourceClip,
            Transform vandal,
            Transform magazine,
            Transform socket,
            Vector3 authoredSocketPosition,
            Quaternion authoredSocketRotation)
        {
            AnimationClip clip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(CloveGunReloadClipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, CloveGunReloadClipPath);
            }

            EditorUtility.CopySerialized(gunSourceClip, clip);
            clip.name = "CloveVandalGunReloadLeftHandTarget";

            string bodyPath = AnimationUtility.CalculateTransformPath(
                bodyAnimator.transform,
                root.transform);
            string gunPath = AnimationUtility.CalculateTransformPath(
                gunAnimator.transform,
                root.transform);
            string vandalPath = AnimationUtility.CalculateTransformPath(
                vandal,
                root.transform);
            string magazinePath = AnimationUtility.CalculateTransformPath(
                magazine,
                root.transform);
            string socketPath = AnimationUtility.CalculateTransformPath(
                socket,
                root.transform);
            string socketAnimationPath = AnimationUtility.CalculateTransformPath(
                socket,
                gunAnimator.transform);

            GameObject sampleRoot = UnityEngine.Object.Instantiate(root);
            sampleRoot.name = root.name + "_ReloadTargetSample";
            sampleRoot.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Animator sampleBody = sampleRoot.transform.Find(bodyPath)
                    ?.GetComponent<Animator>();
                Animator sampleGun = sampleRoot.transform.Find(gunPath)
                    ?.GetComponent<Animator>();
                Transform sampleVandal = sampleRoot.transform.Find(vandalPath);
                Transform sampleMagazine = sampleRoot.transform.Find(magazinePath);
                Transform sampleSocket = sampleRoot.transform.Find(socketPath);
                Transform sampleHand = sampleBody != null
                    ? sampleBody.GetBoneTransform(HumanBodyBones.LeftHand)
                    : null;
                if (sampleBody == null
                    || sampleGun == null
                    || sampleVandal == null
                    || sampleMagazine == null
                    || sampleSocket == null
                    || sampleSocket.parent == null
                    || sampleHand == null)
                {
                    throw new InvalidOperationException(
                        "Could not create the isolated Clove reload sampling hierarchy.");
                }

                var positionKeys = new List<Keyframe>[3]
                {
                    new List<Keyframe>(),
                    new List<Keyframe>(),
                    new List<Keyframe>()
                };
                var rotationKeys = new List<Keyframe>[4]
                {
                    new List<Keyframe>(),
                    new List<Keyframe>(),
                    new List<Keyframe>(),
                    new List<Keyframe>()
                };
                Quaternion previousRotation = authoredSocketRotation;
                float bodyLastFrame = Mathf.Max(
                    1f,
                    Mathf.Round(bodySourceClip.length * bodySourceClip.frameRate));

                const float gripReferenceFrame = 65f;
                float gripReferenceNormalizedTime = Mathf.Clamp01(
                    gripReferenceFrame / bodyLastFrame);
                sampleSocket.localPosition = authoredSocketPosition;
                sampleSocket.localRotation = authoredSocketRotation;
                bodySourceClip.SampleAnimation(
                    sampleBody.gameObject,
                    bodySourceClip.length * gripReferenceNormalizedTime);
                gunSourceClip.SampleAnimation(
                    sampleGun.gameObject,
                    gunSourceClip.length * gripReferenceNormalizedTime);
                Vector3 magazineGripPosition =
                    sampleMagazine.InverseTransformPoint(sampleHand.position);
                Quaternion magazineGripRotation = Quaternion.Inverse(
                    sampleMagazine.rotation) * sampleHand.rotation;

                void AddTargetKey(float bodyFrame, bool useAuthoredPose)
                {
                    float normalizedTime = Mathf.Clamp01(bodyFrame / bodyLastFrame);
                    float gunTime = normalizedTime * gunSourceClip.length;
                    Vector3 localPosition = authoredSocketPosition;
                    Quaternion localRotation = authoredSocketRotation;

                    if (!useAuthoredPose)
                    {
                        sampleSocket.localPosition = authoredSocketPosition;
                        sampleSocket.localRotation = authoredSocketRotation;
                        gunSourceClip.SampleAnimation(
                            sampleGun.gameObject,
                            gunSourceClip.length * normalizedTime);

                        // The body and gun clips have different hand/magazine paths.
                        // Hold the natural frame-65 hand pose rigidly relative to
                        // Magazine_Main through the carry/insertion interval.
                        Vector3 desiredWorldPosition = sampleMagazine.TransformPoint(
                            magazineGripPosition);
                        Quaternion desiredWorldRotation =
                            sampleMagazine.rotation * magazineGripRotation;
                        localPosition = sampleSocket.parent.InverseTransformPoint(
                            desiredWorldPosition);
                        localRotation = Quaternion.Inverse(
                            sampleSocket.parent.rotation) * desiredWorldRotation;
                    }

                    if (Quaternion.Dot(previousRotation, localRotation) < 0f)
                    {
                        localRotation = new Quaternion(
                            -localRotation.x,
                            -localRotation.y,
                            -localRotation.z,
                            -localRotation.w);
                    }
                    previousRotation = localRotation;

                    positionKeys[0].Add(new Keyframe(gunTime, localPosition.x));
                    positionKeys[1].Add(new Keyframe(gunTime, localPosition.y));
                    positionKeys[2].Add(new Keyframe(gunTime, localPosition.z));
                    rotationKeys[0].Add(new Keyframe(gunTime, localRotation.x));
                    rotationKeys[1].Add(new Keyframe(gunTime, localRotation.y));
                    rotationKeys[2].Add(new Keyframe(gunTime, localRotation.z));
                    rotationKeys[3].Add(new Keyframe(gunTime, localRotation.w));
                }

                AddTargetKey(0f, true);
                AddTargetKey(38f, true);
                for (int frame = 40; frame <= 71; frame++)
                    AddTargetKey(frame, false);
                AddTargetKey(72f, true);

                AddTargetKey(bodyLastFrame, true);

                SetTransformCurves(
                    clip,
                    socketAnimationPath,
                    positionKeys,
                    rotationKeys);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sampleRoot);
            }

            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip CreateOrUpdateOperatorGunReloadClip(
            GameObject root,
            Animator bodyAnimator,
            AnimationClip bodySourceClip,
            Animator gunAnimator,
            AnimationClip gunSourceClip,
            Transform operatorRoot,
            Transform magazine,
            Transform socket,
            Vector3 authoredSocketPosition,
            Quaternion authoredSocketRotation)
        {
            AnimationClip clip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    CloveOperatorGunReloadClipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(
                    clip,
                    CloveOperatorGunReloadClipPath);
            }

            EditorUtility.CopySerialized(gunSourceClip, clip);
            clip.name = "CloveOperatorGunReloadLeftHandTarget";

            string bodyPath = AnimationUtility.CalculateTransformPath(
                bodyAnimator.transform,
                root.transform);
            string gunPath = AnimationUtility.CalculateTransformPath(
                gunAnimator.transform,
                root.transform);
            string operatorPath = AnimationUtility.CalculateTransformPath(
                operatorRoot,
                root.transform);
            string magazinePath = AnimationUtility.CalculateTransformPath(
                magazine,
                root.transform);
            string socketPath = AnimationUtility.CalculateTransformPath(
                socket,
                root.transform);
            string socketAnimationPath = AnimationUtility.CalculateTransformPath(
                socket,
                gunAnimator.transform);

            GameObject sampleRoot = UnityEngine.Object.Instantiate(root);
            sampleRoot.name = root.name + "_OperatorReloadTargetSample";
            sampleRoot.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Animator sampleBody = sampleRoot.transform.Find(bodyPath)
                    ?.GetComponent<Animator>();
                Animator sampleGun = sampleRoot.transform.Find(gunPath)
                    ?.GetComponent<Animator>();
                Transform sampleOperator =
                    sampleRoot.transform.Find(operatorPath);
                Transform sampleMagazine =
                    sampleRoot.transform.Find(magazinePath);
                Transform sampleSocket =
                    sampleRoot.transform.Find(socketPath);
                Transform sampleHand = sampleBody != null
                    ? sampleBody.GetBoneTransform(HumanBodyBones.LeftHand)
                    : null;
                if (sampleBody == null
                    || sampleGun == null
                    || sampleOperator == null
                    || sampleMagazine == null
                    || sampleSocket == null
                    || sampleSocket.parent == null
                    || sampleHand == null)
                {
                    throw new InvalidOperationException(
                        "Could not create the isolated Operator reload sampling hierarchy.");
                }

                var positionKeys = new List<Keyframe>[3]
                {
                    new List<Keyframe>(),
                    new List<Keyframe>(),
                    new List<Keyframe>()
                };
                var rotationKeys = new List<Keyframe>[4]
                {
                    new List<Keyframe>(),
                    new List<Keyframe>(),
                    new List<Keyframe>(),
                    new List<Keyframe>()
                };
                Quaternion previousRotation = authoredSocketRotation;
                float bodyLastFrame = Mathf.Max(
                    1f,
                    Mathf.Round(
                        bodySourceClip.length * bodySourceClip.frameRate));

                const float gripReferenceFrame = 58f;
                float gripReferenceNormalizedTime = Mathf.Clamp01(
                    gripReferenceFrame / bodyLastFrame);
                sampleSocket.localPosition = authoredSocketPosition;
                sampleSocket.localRotation = authoredSocketRotation;
                bodySourceClip.SampleAnimation(
                    sampleBody.gameObject,
                    bodySourceClip.length * gripReferenceNormalizedTime);
                gunSourceClip.SampleAnimation(
                    sampleGun.gameObject,
                    gunSourceClip.length * gripReferenceNormalizedTime);
                Vector3 magazineGripPosition =
                    sampleMagazine.InverseTransformPoint(sampleHand.position);
                Quaternion magazineGripRotation = Quaternion.Inverse(
                    sampleMagazine.rotation) * sampleHand.rotation;

                void AddTargetKey(float bodyFrame, bool useAuthoredPose)
                {
                    float normalizedTime = Mathf.Clamp01(
                        bodyFrame / bodyLastFrame);
                    float gunTime = normalizedTime * gunSourceClip.length;
                    Vector3 localPosition = authoredSocketPosition;
                    Quaternion localRotation = authoredSocketRotation;

                    if (!useAuthoredPose)
                    {
                        sampleSocket.localPosition = authoredSocketPosition;
                        sampleSocket.localRotation = authoredSocketRotation;
                        gunSourceClip.SampleAnimation(
                            sampleGun.gameObject,
                            gunSourceClip.length * normalizedTime);

                        Vector3 desiredWorldPosition =
                            sampleMagazine.TransformPoint(magazineGripPosition);
                        Quaternion desiredWorldRotation =
                            sampleMagazine.rotation * magazineGripRotation;
                        localPosition = sampleSocket.parent.InverseTransformPoint(
                            desiredWorldPosition);
                        localRotation = Quaternion.Inverse(
                            sampleSocket.parent.rotation) * desiredWorldRotation;
                    }

                    if (Quaternion.Dot(previousRotation, localRotation) < 0f)
                    {
                        localRotation = new Quaternion(
                            -localRotation.x,
                            -localRotation.y,
                            -localRotation.z,
                            -localRotation.w);
                    }
                    previousRotation = localRotation;

                    positionKeys[0].Add(new Keyframe(gunTime, localPosition.x));
                    positionKeys[1].Add(new Keyframe(gunTime, localPosition.y));
                    positionKeys[2].Add(new Keyframe(gunTime, localPosition.z));
                    rotationKeys[0].Add(new Keyframe(gunTime, localRotation.x));
                    rotationKeys[1].Add(new Keyframe(gunTime, localRotation.y));
                    rotationKeys[2].Add(new Keyframe(gunTime, localRotation.z));
                    rotationKeys[3].Add(new Keyframe(gunTime, localRotation.w));
                }

                AddTargetKey(0f, true);
                AddTargetKey(49f, true);
                for (int frame = 50; frame <= 71; frame++)
                    AddTargetKey(frame, false);
                AddTargetKey(72f, true);
                AddTargetKey(bodyLastFrame, true);

                SetTransformCurves(
                    clip,
                    socketAnimationPath,
                    positionKeys,
                    rotationKeys);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sampleRoot);
            }

            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip CreateOrUpdateClassicGunReloadClip(
            GameObject root,
            Animator bodyAnimator,
            AnimationClip bodySourceClip,
            Animator gunAnimator,
            AnimationClip gunSourceClip,
            Transform classicRoot,
            Transform magazine,
            Transform socket,
            Vector3 authoredSocketPosition,
            Quaternion authoredSocketRotation)
        {
            AnimationClip clip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    CloveClassicGunReloadClipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(
                    clip,
                    CloveClassicGunReloadClipPath);
            }

            EditorUtility.CopySerialized(gunSourceClip, clip);
            clip.name = "CloveClassicGunReloadLeftHandTarget";

            string bodyPath = AnimationUtility.CalculateTransformPath(
                bodyAnimator.transform,
                root.transform);
            string gunPath = AnimationUtility.CalculateTransformPath(
                gunAnimator.transform,
                root.transform);
            string classicPath = AnimationUtility.CalculateTransformPath(
                classicRoot,
                root.transform);
            string magazinePath = AnimationUtility.CalculateTransformPath(
                magazine,
                root.transform);
            string socketPath = AnimationUtility.CalculateTransformPath(
                socket,
                root.transform);
            string socketAnimationPath = AnimationUtility.CalculateTransformPath(
                socket,
                gunAnimator.transform);

            GameObject sampleRoot = UnityEngine.Object.Instantiate(root);
            sampleRoot.name = root.name + "_ClassicReloadTargetSample";
            sampleRoot.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Animator sampleBody = sampleRoot.transform.Find(bodyPath)
                    ?.GetComponent<Animator>();
                Animator sampleGun = sampleRoot.transform.Find(gunPath)
                    ?.GetComponent<Animator>();
                Transform sampleClassic =
                    sampleRoot.transform.Find(classicPath);
                Transform sampleMagazine =
                    sampleRoot.transform.Find(magazinePath);
                Transform sampleSocket =
                    sampleRoot.transform.Find(socketPath);
                Transform sampleHand = sampleBody != null
                    ? sampleBody.GetBoneTransform(HumanBodyBones.LeftHand)
                    : null;
                if (sampleBody == null
                    || sampleGun == null
                    || sampleClassic == null
                    || sampleMagazine == null
                    || sampleSocket == null
                    || sampleSocket.parent == null
                    || sampleHand == null)
                {
                    throw new InvalidOperationException(
                        "Could not create the isolated Classic reload sampling hierarchy.");
                }

                var positionKeys = new List<Keyframe>[3]
                {
                    new List<Keyframe>(),
                    new List<Keyframe>(),
                    new List<Keyframe>()
                };
                var rotationKeys = new List<Keyframe>[4]
                {
                    new List<Keyframe>(),
                    new List<Keyframe>(),
                    new List<Keyframe>(),
                    new List<Keyframe>()
                };
                Quaternion previousRotation = authoredSocketRotation;
                float bodyLastFrame = Mathf.Max(
                    1f,
                    Mathf.Round(
                        bodySourceClip.length * bodySourceClip.frameRate));

                // At body frame 22 the support hand is firmly carrying the
                // moving magazine toward the pistol. Capture that natural hand
                // pose relative to Magazine_Main, then preserve the contact
                // through insertion without altering the authored idle socket.
                const float gripReferenceFrame = 22f;
                float gripReferenceNormalizedTime = Mathf.Clamp01(
                    gripReferenceFrame / bodyLastFrame);
                sampleSocket.localPosition = authoredSocketPosition;
                sampleSocket.localRotation = authoredSocketRotation;
                bodySourceClip.SampleAnimation(
                    sampleBody.gameObject,
                    bodySourceClip.length * gripReferenceNormalizedTime);
                gunSourceClip.SampleAnimation(
                    sampleGun.gameObject,
                    gunSourceClip.length * gripReferenceNormalizedTime);
                Vector3 magazineGripPosition =
                    sampleMagazine.InverseTransformPoint(sampleHand.position);
                Quaternion magazineGripRotation = Quaternion.Inverse(
                    sampleMagazine.rotation) * sampleHand.rotation;

                void AddTargetKey(float bodyFrame, bool useAuthoredPose)
                {
                    float normalizedTime = Mathf.Clamp01(
                        bodyFrame / bodyLastFrame);
                    float gunTime = normalizedTime * gunSourceClip.length;
                    Vector3 localPosition = authoredSocketPosition;
                    Quaternion localRotation = authoredSocketRotation;

                    if (!useAuthoredPose)
                    {
                        sampleSocket.localPosition = authoredSocketPosition;
                        sampleSocket.localRotation = authoredSocketRotation;
                        gunSourceClip.SampleAnimation(
                            sampleGun.gameObject,
                            gunSourceClip.length * normalizedTime);

                        Vector3 desiredWorldPosition =
                            sampleMagazine.TransformPoint(magazineGripPosition);
                        Quaternion desiredWorldRotation =
                            sampleMagazine.rotation * magazineGripRotation;
                        localPosition = sampleSocket.parent.InverseTransformPoint(
                            desiredWorldPosition);
                        localRotation = Quaternion.Inverse(
                            sampleSocket.parent.rotation) * desiredWorldRotation;
                    }

                    if (Quaternion.Dot(previousRotation, localRotation) < 0f)
                    {
                        localRotation = new Quaternion(
                            -localRotation.x,
                            -localRotation.y,
                            -localRotation.z,
                            -localRotation.w);
                    }
                    previousRotation = localRotation;

                    positionKeys[0].Add(new Keyframe(gunTime, localPosition.x));
                    positionKeys[1].Add(new Keyframe(gunTime, localPosition.y));
                    positionKeys[2].Add(new Keyframe(gunTime, localPosition.z));
                    rotationKeys[0].Add(new Keyframe(gunTime, localRotation.x));
                    rotationKeys[1].Add(new Keyframe(gunTime, localRotation.y));
                    rotationKeys[2].Add(new Keyframe(gunTime, localRotation.z));
                    rotationKeys[3].Add(new Keyframe(gunTime, localRotation.w));
                }

                AddTargetKey(0f, true);
                AddTargetKey(18f, true);
                for (int frame = 19; frame <= 27; frame++)
                    AddTargetKey(frame, false);
                AddTargetKey(28f, true);
                AddTargetKey(bodyLastFrame, true);

                SetTransformCurves(
                    clip,
                    socketAnimationPath,
                    positionKeys,
                    rotationKeys);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sampleRoot);
            }

            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip CreateOrUpdateOdinGunReloadClip(
            GameObject root,
            Animator bodyAnimator,
            AnimationClip bodySourceClip,
            Animator gunAnimator,
            AnimationClip gunSourceClip,
            Transform odinRoot,
            Transform magazine,
            Transform socket,
            Vector3 authoredSocketPosition,
            Quaternion authoredSocketRotation)
        {
            if (root == null
                || bodyAnimator == null
                || bodySourceClip == null
                || gunAnimator == null
                || gunSourceClip == null
                || odinRoot == null
                || magazine == null
                || socket == null)
            {
                throw new InvalidOperationException(
                    "Odin reload target generation requires the complete "
                    + "body, weapon, magazine, and socket hierarchy.");
            }

            AnimationClip clip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    CloveOdinGunReloadClipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, CloveOdinGunReloadClipPath);
            }

            EditorUtility.CopySerialized(gunSourceClip, clip);
            clip.name = "CloveOdinGunReloadLeftHandTarget";

            string bodyPath = AnimationUtility.CalculateTransformPath(
                bodyAnimator.transform,
                root.transform);
            string gunPath = AnimationUtility.CalculateTransformPath(
                gunAnimator.transform,
                root.transform);
            string odinPath = AnimationUtility.CalculateTransformPath(
                odinRoot,
                root.transform);
            string magazinePath = AnimationUtility.CalculateTransformPath(
                magazine,
                root.transform);
            string socketPath = AnimationUtility.CalculateTransformPath(
                socket,
                root.transform);
            string socketAnimationPath = AnimationUtility.CalculateTransformPath(
                socket,
                gunAnimator.transform);

            GameObject sampleRoot = UnityEngine.Object.Instantiate(root);
            sampleRoot.name = root.name + "_OdinReloadTargetSample";
            sampleRoot.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Animator sampleBody = sampleRoot.transform.Find(bodyPath)
                    ?.GetComponent<Animator>();
                Animator sampleGun = sampleRoot.transform.Find(gunPath)
                    ?.GetComponent<Animator>();
                Transform sampleOdin = sampleRoot.transform.Find(odinPath);
                Transform sampleMagazine = sampleRoot.transform.Find(magazinePath);
                Transform sampleSocket = sampleRoot.transform.Find(socketPath);
                Transform sampleHand = sampleBody != null
                    ? sampleBody.GetBoneTransform(HumanBodyBones.LeftHand)
                    : null;
                Transform sampleShoulder = sampleBody != null
                    ? sampleBody.GetBoneTransform(HumanBodyBones.LeftUpperArm)
                    : null;
                Transform sampleElbow = sampleBody != null
                    ? sampleBody.GetBoneTransform(HumanBodyBones.LeftLowerArm)
                    : null;
                Renderer sampleMagazineRenderer = sampleMagazine != null
                    ? sampleMagazine.GetComponentInChildren<Renderer>(true)
                    : null;
                MeshFilter sampleMagazineMeshFilter = sampleMagazineRenderer != null
                    ? sampleMagazineRenderer.GetComponent<MeshFilter>()
                    : null;
                if (sampleBody == null
                    || sampleGun == null
                    || sampleOdin == null
                    || sampleMagazine == null
                    || sampleMagazineRenderer == null
                    || sampleMagazineMeshFilter == null
                    || sampleMagazineMeshFilter.sharedMesh == null
                    || sampleSocket == null
                    || sampleSocket.parent == null
                    || sampleHand == null
                    || sampleShoulder == null
                    || sampleElbow == null)
                {
                    throw new InvalidOperationException(
                        "Could not create the isolated Odin reload sampling "
                        + "hierarchy.");
                }

                var positionKeys = new List<Keyframe>[3]
                {
                    new List<Keyframe>(),
                    new List<Keyframe>(),
                    new List<Keyframe>()
                };
                var rotationKeys = new List<Keyframe>[4]
                {
                    new List<Keyframe>(),
                    new List<Keyframe>(),
                    new List<Keyframe>(),
                    new List<Keyframe>()
                };
                Quaternion previousRotation = authoredSocketRotation;
                float bodyLastFrame = Mathf.Max(
                    1f,
                    Mathf.Round(
                        bodySourceClip.length * bodySourceClip.frameRate));

                AnimatorController sampleBodyController =
                    AssetDatabase.LoadAssetAtPath<AnimatorController>(
                        OdinBodyControllerPath);
                AnimatorState sampleReloadState = FindState(
                    sampleBodyController,
                    OdinReloadStateName);
                int sampleReloadLayer = -1;
                for (int i = 0; i < sampleBodyController.layers.Length; i++)
                {
                    if (sampleBodyController.layers[i].name
                        == "Upper Body Gun Pose")
                    {
                        sampleReloadLayer = i;
                        break;
                    }
                }
                RigBuilder sampleRigBuilder = sampleBody.GetComponent<RigBuilder>();
                if (sampleReloadState == null
                    || sampleReloadLayer < 0
                    || sampleRigBuilder == null)
                {
                    throw new InvalidOperationException(
                        "Could not initialize the composed Odin reload pose for "
                        + "target baking.");
                }
                sampleRigBuilder.Clear();
                sampleRigBuilder.enabled = false;
                sampleBody.runtimeAnimatorController = sampleBodyController;
                sampleBody.Rebind();
                sampleBody.Update(0f);

                // The Humanoid-retargeted hand can be far from Odin's belt box,
                // and some belt-feed points are beyond Clove's physical arm
                // length with the authored idle weapon transform. For every
                // contact frame, aim toward the visible magazine surface but
                // clamp the wrist target to 97% of the actual two-bone reach.
                // This prevents both empty-air insertion and an unreachable IK
                // target, while retaining the reload clip's wrist rotation.
                const float wristClearance = 0.065f;
                const float reachRatio = 0.97f;

                void AddTargetKey(float bodyFrame, bool useAuthoredPose)
                {
                    float normalizedTime = Mathf.Clamp01(
                        bodyFrame / bodyLastFrame);
                    float gunTime = normalizedTime * gunSourceClip.length;
                    Vector3 localPosition = authoredSocketPosition;
                    Quaternion localRotation = authoredSocketRotation;

                    if (!useAuthoredPose)
                    {
                        sampleSocket.localPosition = authoredSocketPosition;
                        sampleSocket.localRotation = authoredSocketRotation;
                        sampleBody.Play(
                            sampleReloadState.nameHash,
                            sampleReloadLayer,
                            normalizedTime);
                        sampleBody.Update(0.001f);
                        gunSourceClip.SampleAnimation(
                            sampleGun.gameObject,
                            gunSourceClip.length * normalizedTime);

                        Bounds magazineBounds =
                            sampleMagazineMeshFilter.sharedMesh.bounds;
                        Vector3 shoulderInMagazineMesh =
                            sampleMagazineRenderer.transform.InverseTransformPoint(
                                sampleShoulder.position);
                        Vector3 magazineSurface =
                            sampleMagazineRenderer.transform.TransformPoint(
                                magazineBounds.ClosestPoint(
                                    shoulderInMagazineMesh));
                        Vector3 magazineCenter =
                            sampleMagazineRenderer.transform.TransformPoint(
                                magazineBounds.center);
                        Vector3 magazineOutward = sampleShoulder.position
                            - magazineCenter;
                        if (magazineOutward.sqrMagnitude < 0.000001f)
                        {
                            magazineOutward = sampleHand.position
                                - magazineCenter;
                        }
                        Vector3 desiredWorldPosition = magazineSurface
                            + magazineOutward.normalized * wristClearance;
                        float armLength = Vector3.Distance(
                                sampleShoulder.position,
                                sampleElbow.position)
                            + Vector3.Distance(
                                sampleElbow.position,
                                sampleHand.position);
                        Vector3 shoulderToTarget = desiredWorldPosition
                            - sampleShoulder.position;
                        float maximumReach = armLength * reachRatio;
                        if (shoulderToTarget.magnitude > maximumReach)
                        {
                            desiredWorldPosition = sampleShoulder.position
                                + shoulderToTarget.normalized * maximumReach;
                        }
                        Quaternion desiredWorldRotation = sampleHand.rotation;
                        localPosition = sampleSocket.parent.InverseTransformPoint(
                            desiredWorldPosition);
                        localRotation = Quaternion.Inverse(
                            sampleSocket.parent.rotation) * desiredWorldRotation;
                    }

                    if (Quaternion.Dot(previousRotation, localRotation) < 0f)
                    {
                        localRotation = new Quaternion(
                            -localRotation.x,
                            -localRotation.y,
                            -localRotation.z,
                            -localRotation.w);
                    }
                    previousRotation = localRotation;

                    positionKeys[0].Add(new Keyframe(gunTime, localPosition.x));
                    positionKeys[1].Add(new Keyframe(gunTime, localPosition.y));
                    positionKeys[2].Add(new Keyframe(gunTime, localPosition.z));
                    rotationKeys[0].Add(new Keyframe(gunTime, localRotation.x));
                    rotationKeys[1].Add(new Keyframe(gunTime, localRotation.y));
                    rotationKeys[2].Add(new Keyframe(gunTime, localRotation.z));
                    rotationKeys[3].Add(new Keyframe(gunTime, localRotation.w));
                }

                AddTargetKey(0f, true);
                AddTargetKey(15f, true);
                for (int frame = 16; frame <= 33; frame++)
                    AddTargetKey(frame, false);
                AddTargetKey(34f, true);
                AddTargetKey(bodyLastFrame, true);

                SetTransformCurves(
                    clip,
                    socketAnimationPath,
                    positionKeys,
                    rotationKeys);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sampleRoot);
            }

            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void SetTransformCurves(
            AnimationClip clip,
            string path,
            IReadOnlyList<List<Keyframe>> positionKeys,
            IReadOnlyList<List<Keyframe>> rotationKeys)
        {
            string[] positionProperties =
            {
                "m_LocalPosition.x",
                "m_LocalPosition.y",
                "m_LocalPosition.z"
            };
            string[] rotationProperties =
            {
                "m_LocalRotation.x",
                "m_LocalRotation.y",
                "m_LocalRotation.z",
                "m_LocalRotation.w"
            };

            for (int i = 0; i < positionProperties.Length; i++)
            {
                var curve = new AnimationCurve(positionKeys[i].ToArray());
                SetLinearTangents(curve);
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        path,
                        typeof(Transform),
                        positionProperties[i]),
                    curve);
            }

            for (int i = 0; i < rotationProperties.Length; i++)
            {
                var curve = new AnimationCurve(rotationKeys[i].ToArray());
                SetLinearTangents(curve);
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        path,
                        typeof(Transform),
                        rotationProperties[i]),
                    curve);
            }

            clip.EnsureQuaternionContinuity();
        }

        private static void SetLinearTangents(AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(
                    curve,
                    i,
                    AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(
                    curve,
                    i,
                    AnimationUtility.TangentMode.Linear);
            }
        }

        private static void SynchronizeGunReloadStateToBody(
            AnimatorState bodyState,
            AnimationClip bodyClip,
            AnimatorState gunState,
            AnimationClip gunClip)
        {
            if (bodyState.speedParameterActive || gunState.speedParameterActive)
            {
                throw new InvalidOperationException(
                    "Clove reload alignment requires fixed body and gun state speeds.");
            }

            float bodySpeed = Mathf.Abs(bodyState.speed);
            if (bodySpeed <= Mathf.Epsilon || bodyClip.length <= Mathf.Epsilon)
            {
                throw new InvalidOperationException(
                    "Clove body reload state must have a positive effective duration.");
            }

            float bodyDuration = bodyClip.length / bodySpeed;
            float direction = gunState.speed < 0f ? -1f : 1f;
            gunState.speed = direction * gunClip.length / bodyDuration;
            EditorUtility.SetDirty(gunState);
        }

        private static void ValidateSynchronizedReloadStates(
            AnimatorState bodyState,
            AnimationClip bodyClip,
            AnimatorState gunState,
            AnimationClip gunClip)
        {
            if (bodyState.speedParameterActive || gunState.speedParameterActive)
            {
                throw new InvalidOperationException(
                    "Clove reload alignment requires fixed body and gun state speeds.");
            }

            float bodyDuration = bodyClip.length / Mathf.Abs(bodyState.speed);
            float gunDuration = gunClip.length / Mathf.Abs(gunState.speed);
            if (Mathf.Abs(bodyDuration - gunDuration) > 0.01f)
            {
                throw new InvalidOperationException(
                    $"Body and gun reload states are not synchronized "
                    + $"({bodyDuration:F4}s vs {gunDuration:F4}s).");
            }
        }

        private static float FrameToTime(
            float frame,
            float frameRate,
            float lastFrame)
        {
            return Mathf.Min(frame, lastFrame) / frameRate;
        }

        private static Vector3 DivideByLossyScale(
            Vector3 worldUnitOffset,
            Vector3 lossyScale)
        {
            return new Vector3(
                SafeDivide(worldUnitOffset.x, lossyScale.x),
                SafeDivide(worldUnitOffset.y, lossyScale.y),
                SafeDivide(worldUnitOffset.z, lossyScale.z));
        }

        private static float SafeDivide(float value, float scale)
        {
            return Mathf.Abs(scale) > 0.00001f ? value / scale : 0f;
        }

        private static AnimatorOverrideController CreateOrUpdateOverrideController(
            string assetPath,
            AnimatorController baseController,
            AnimationClip sourceClip,
            AnimationClip reloadClip)
        {
            AnimatorOverrideController overrideController =
                AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                    assetPath);
            if (overrideController == null)
            {
                overrideController = new AnimatorOverrideController(baseController);
                AssetDatabase.CreateAsset(
                    overrideController,
                    assetPath);
            }
            else
            {
                overrideController.runtimeAnimatorController = baseController;
            }

            var overrides =
                new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);
            int index = overrides.FindIndex(pair => pair.Key == sourceClip);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    $"Reload clip '{sourceClip.name}' is not present in "
                    + $"base controller '{baseController.name}'.");
            }

            overrides[index] =
                new KeyValuePair<AnimationClip, AnimationClip>(sourceClip, reloadClip);
            overrideController.ApplyOverrides(overrides);
            EditorUtility.SetDirty(overrideController);
            return overrideController;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            int separator = folderPath.LastIndexOf('/');
            string parent = folderPath.Substring(0, separator);
            string leaf = folderPath.Substring(separator + 1);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void RecordNestedPrefabOverride(UnityEngine.Object target)
        {
            if (target != null && PrefabUtility.IsPartOfPrefabInstance(target))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
        }

        private static void ConfigureSharedDirectIKTarget(
            string prefabPath,
            string directTargetName,
            bool initializeFromGripOffsets)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ThirdPersonWeaponGrip grip =
                    root.GetComponent<ThirdPersonWeaponGrip>();
                Transform directTarget = grip?.LeftHandTarget != null
                    ? FindDescendant(
                        grip.LeftHandTarget,
                        directTargetName)
                    : null;
                if (grip == null || directTarget == null)
                {
                    throw new InvalidOperationException(
                        $"Direct IK target '{directTargetName}' was not found "
                        + $"in {prefabPath}.");
                }

                if (grip.DirectIKTarget != null
                    && grip.DirectIKTarget != directTarget)
                {
                    throw new InvalidOperationException(
                        $"{prefabPath} already references a different "
                        + "direct IK target.");
                }

                if (grip.DirectIKTarget == null)
                {
                    if (initializeFromGripOffsets)
                    {
                        directTarget.localPosition = DivideByLossyScale(
                            grip.HandPositionOffset,
                            grip.LeftHandTarget.lossyScale);
                        directTarget.localRotation = grip.HandRotationOffset;
                        directTarget.localScale = Vector3.one;
                        EditorUtility.SetDirty(directTarget);
                    }

                    grip.ConfigureDirectIKTarget(directTarget);
                    EditorUtility.SetDirty(grip);
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetupWeaponGrip(WeaponGripSetup setup)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(setup.PrefabPath);
            try
            {
                Transform target = FindDescendant(root.transform, TargetName);
                if (target == null)
                {
                    throw new InvalidOperationException(
                        $"No {TargetName} found in {setup.PrefabPath}");
                }

                ThirdPersonWeaponGrip grip =
                    root.GetComponent<ThirdPersonWeaponGrip>();
                if (grip == null)
                    grip = root.AddComponent<ThirdPersonWeaponGrip>();

                grip.Configure(
                    target,
                    setup.PositionOffset,
                    setup.RotationOffset);
                EditorUtility.SetDirty(grip);
                PrefabUtility.SaveAsPrefabAsset(root, setup.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetupPlayerPrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                WeaponManager weaponManager = root.GetComponent<WeaponManager>();
                PlayerVisibilityController visibility =
                    root.GetComponent<PlayerVisibilityController>();
                if (weaponManager == null || visibility == null)
                {
                    throw new InvalidOperationException(
                        $"Missing player presentation components: {prefabPath}");
                }

                Animator animator = weaponManager.CharacterAnimation;
                if (animator == null
                    || animator.avatar == null
                    || !animator.avatar.isHuman)
                {
                    throw new InvalidOperationException(
                        $"Missing valid humanoid character Animator: {prefabPath}");
                }

                Transform upperArm =
                    animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform lowerArm =
                    animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                Transform hand =
                    animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Transform chest =
                    animator.GetBoneTransform(HumanBodyBones.UpperChest)
                    ?? animator.GetBoneTransform(HumanBodyBones.Chest);
                if (upperArm == null
                    || lowerArm == null
                    || hand == null
                    || chest == null)
                {
                    throw new InvalidOperationException(
                        $"Incomplete humanoid left-arm mapping: {prefabPath}");
                }

                RigBuilder builder = animator.GetComponent<RigBuilder>();
                if (builder == null)
                    builder = animator.gameObject.AddComponent<RigBuilder>();

                Transform rigRoot =
                    GetOrCreateDirectChild(animator.transform, RigRootName);
                Rig rig = rigRoot.GetComponent<Rig>();
                if (rig == null)
                    rig = rigRoot.gameObject.AddComponent<Rig>();
                rig.weight = 1f;

                Transform constraintTransform =
                    GetOrCreateDirectChild(rigRoot, ConstraintName);
                TwoBoneIKConstraint constraint =
                    constraintTransform.GetComponent<TwoBoneIKConstraint>();
                if (constraint == null)
                {
                    constraint = constraintTransform.gameObject
                        .AddComponent<TwoBoneIKConstraint>();
                }

                Transform hint = GetOrCreateDirectChild(rigRoot, HintName);
                Vector3 outward = lowerArm.position - chest.position;
                if (outward.sqrMagnitude < 0.0001f)
                    outward = -animator.transform.right;
                hint.position = lowerArm.position
                    + outward.normalized * Mathf.Max(
                        0.2f,
                        Vector3.Distance(upperArm.position, lowerArm.position));
                hint.rotation = lowerArm.rotation;

                Transform proxy = FindDirectChild(rigRoot, ProxyName)
                    ?? FindDirectChild(root.transform, ProxyName);
                if (proxy == null)
                    proxy = new GameObject(ProxyName).transform;
                proxy.SetParent(root.transform, false);
                proxy.position = hand.position;
                proxy.rotation = hand.rotation;

                TwoBoneIKConstraintData data = constraint.data;
                data.root = upperArm;
                data.mid = lowerArm;
                data.tip = hand;
                data.target = proxy;
                data.hint = hint;
                data.targetPositionWeight = 1f;
                data.targetRotationWeight = 1f;
                data.hintWeight = 1f;
                data.maintainTargetPositionOffset = false;
                data.maintainTargetRotationOffset = false;
                constraint.data = data;
                constraint.weight = 1f;

                if (!builder.layers.Any(layer => layer.rig == rig))
                    builder.layers.Add(new RigLayer(rig));

                ThirdPersonLeftHandIK controller =
                    root.GetComponent<ThirdPersonLeftHandIK>();
                if (controller == null)
                    controller = root.AddComponent<ThirdPersonLeftHandIK>();
                controller.Configure(
                    animator,
                    builder,
                    rig,
                    constraint,
                    proxy);

                EditorUtility.SetDirty(builder);
                EditorUtility.SetDirty(rig);
                EditorUtility.SetDirty(constraint);
                EditorUtility.SetDirty(controller);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform GetOrCreateDirectChild(
            Transform parent,
            string childName)
        {
            Transform child = FindDirectChild(parent, childName);
            if (child != null)
                return child;

            var childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Transform FindDirectChild(
            Transform parent,
            string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                    return child;
            }

            return null;
        }

        private static Transform FindDescendant(
            Transform root,
            string exactName)
        {
            if (root == null)
                return null;
            if (root.name == exactName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match =
                    FindDescendant(root.GetChild(i), exactName);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static Transform FindAncestor(
            Transform child,
            string exactName)
        {
            Transform current = child;
            while (current != null)
            {
                if (current.name == exactName)
                    return current;
                current = current.parent;
            }

            return null;
        }
    }

}
