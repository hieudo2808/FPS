using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FPS.Editor
{
    /// <summary>
    /// Authors Clove's remaining firearm presentations as direct-transform
    /// Generic profiles. Source FBXs and the imported Clove model stay intact.
    /// </summary>
    public static class GenericPathBoundWeaponSetup
    {
        private const string MenuRoot = "FPS/Third Person/Generic Path-Bound Weapons/";
        private const string ClovePrefabPath = "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab";
        private const string SharedFolder = "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/GenericPathBoundShared";
        private const string CoreFolder = "Assets/FPS/Features/Characters/Animation/Content/3P";
        private const string UpperMaskPath = SharedFolder + "/CloveGenericUpperBody.mask";
        private const string UpperNoFingersMaskPath = SharedFolder + "/CloveGenericUpperBodyNoFingers.mask";
        private const string ModelRootName = "CS_Smonk_S0_Skelmesh.ao";
        private const string SkeletonRootPath = "Skeleton";
        private const string UpperBodyRootPath = "Skeleton/Root/Splitter/Spine1";
        private const string WeaponRootPath = "Skeleton/Root/Splitter/MasterWeaponAim";
        private const string WeaponMasterPath = WeaponRootPath + "/MasterWeapon/R_WeaponMaster";
        private const string RightHandPath = UpperBodyRootPath + "/Spine2/Spine3/Spine4/R_Clavicle/R_Shoulder/R_Elbow/R_Hand";
        private const string LeftHandPath = UpperBodyRootPath + "/Spine2/Spine3/Spine4/L_Clavicle/L_Shoulder/L_Elbow/L_Hand";

        private static readonly CommonMotion[] CommonMotions =
        {
            Common("Idle", "TP_Core_Idle_LB.fbx", MotionRegion.FullSkeleton),
            Common("Walk", "TP_Core_WalkN_LB.fbx", MotionRegion.FullSkeleton),
            Common("Run", "TP_Core_RunN_LB.fbx", MotionRegion.FullSkeleton),
            Common("Jump", "TP_Core_Jump_LB.fbx", MotionRegion.FullSkeleton),
            Common("Fall", "TP_Core_Falling_LB.fbx", MotionRegion.FullSkeleton),
            Common("Land", "TP_Core_JumpLand_LB.fbx", MotionRegion.FullSkeleton),
            Common("WalkAdd", "TP_Core_WalkAddN_UB.fbx", MotionRegion.UpperBody),
            Common("RunAdd", "TP_Core_RunAddN_UB.fbx", MotionRegion.UpperBody),
            Common("JumpAdd", "TP_Core_JumpAdd_UB.fbx", MotionRegion.UpperBody),
            Common("LandAdd", "TP_Core_JumpLandAdd_UB.fbx", MotionRegion.UpperBody)
        };

        private static readonly WeaponProfile[] Profiles =
        {
            new WeaponProfile("Vandal", "Vandal_3P", "TP_Core_AK_S0_IdlePose_UB.fbx", "TP_Core_AK_S0_Reload_UB.fbx"),
            new WeaponProfile("Classic", "Classic_3P", "TP_Core_BasePistol_S0_IdlePose_UB.fbx", "TP_Core_BasePistol_S0_Reload_UB.fbx"),
            new WeaponProfile("Odin", "Odin_3P", "TP_Core_HMG_S0_IdlePose_UB.fbx", "TP_Core_HMG_S0_Reload_UB.fbx"),
            new WeaponProfile("Bucky", "Bucky_3P", "TP_Core_PumpShotgun_S0_IdlePose_UB.fbx", "TP_Core_PumpShotgun_S0_Reload_UB.fbx")
        };

        [MenuItem(MenuRoot + "Build And Apply Remaining Clove Weapons")]
        public static void BuildAndApply()
        {
            SaveAllOrThrow("before Generic weapon authoring");
            StopAnimationPreview();
            EnsureFolder(SharedFolder);
            foreach (WeaponProfile profile in Profiles)
                EnsureFolder(profile.ProductionFolder);

            GameObject root = PrefabUtility.LoadPrefabContents(ClovePrefabPath);
            try
            {
                Transform body = RequireDescendant(root.transform, "Body");
                Transform modelRoot = RequireDescendant(body, ModelRootName);
                PlayerVisibilityController visibility = RequireVisibility(root);
                Func<string, bool> includeUpperBody =
                    BuildUpperBodyMaskPredicate(body, visibility);
                AvatarMask upperMask = BuildTransformMask(
                    UpperMaskPath,
                    body,
                    includeUpperBody);
                AvatarMask noFingersMask = BuildTransformMask(
                    UpperNoFingersMaskPath,
                    body,
                    path => includeUpperBody(path) && !IsFingerPath(path));
                var controllers = new Dictionary<string, AnimatorController>(StringComparer.Ordinal);

                foreach (WeaponProfile profile in Profiles)
                {
                    ThirdPersonWeaponPresentation presentation = RequirePresentation(visibility, profile.Name);
                    ProfileBuild result = BuildProfile(profile, presentation.CharacterController, body, modelRoot, upperMask, noFingersMask);
                    AuthorWeaponMount(root, modelRoot, profile, result.HoldClip);
                    controllers.Add(profile.Name, result.Controller);
                }

                // The first authoring pass can move weapons from an old hand
                // mount into R_WeaponMaster. Rebuild masks from the final
                // hierarchy so they never retain the pre-migration paths.
                includeUpperBody = BuildUpperBodyMaskPredicate(body, visibility);
                upperMask = BuildTransformMask(
                    UpperMaskPath,
                    body,
                    includeUpperBody);
                noFingersMask = BuildTransformMask(
                    UpperNoFingersMaskPath,
                    body,
                    path => includeUpperBody(path) && !IsFingerPath(path));
                foreach (AnimatorController controller in controllers.Values)
                    ConfigureGenericLayers(controller, upperMask, noFingersMask);

                AuthorPresentations(visibility, controllers);
                PrefabUtility.SaveAsPrefabAsset(root, ClovePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            ThirdPersonWeaponAnimationSyncSetup.ApplyToAuthoredControllers();
            SaveAllOrThrow("after Generic weapon authoring");
            Validate();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(ClovePrefabPath);
            Debug.Log("[GenericPathBoundWeapons] Authored Vandal, Classic, Odin and Bucky with Avatar=None, direct body/hand/MasterWeapon curves and R_WeaponMaster mounts. Source FBXs remain unchanged; no runtime IK is used.");
        }

        [MenuItem(MenuRoot + "Validate Authored Remaining Clove Weapons")]
        public static void Validate()
        {
            SaveAllOrThrow("before Generic weapon validation");
            GameObject root = PrefabUtility.LoadPrefabContents(ClovePrefabPath);
            try
            {
                Transform body = RequireDescendant(root.transform, "Body");
                Transform modelRoot = RequireDescendant(body, ModelRootName);
                Transform master = RequireChild(modelRoot, WeaponMasterPath);
                PlayerVisibilityController visibility = RequireVisibility(root);
                foreach (WeaponProfile profile in Profiles)
                {
                    ThirdPersonWeaponPresentation presentation = RequirePresentation(visibility, profile.Name);
                    AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(profile.ControllerPath);
                    if (controller == null)
                        throw new InvalidOperationException($"Missing generated {profile.Name} controller.");
                    if (presentation.CharacterRigMode != ThirdPersonCharacterRigMode.GenericPathBound
                        || presentation.CharacterAvatar != null
                        || presentation.CharacterController != controller
                        || presentation.UseLeftHandIK
                        || presentation.AnimationDrivenLeftHandIK)
                        throw new InvalidOperationException($"{profile.Name} is not the authored Generic/no-IK profile.");

                    Transform weapon = RequireDescendant(root.transform, profile.ObjectName);
                    if (weapon.parent != master)
                        throw new InvalidOperationException($"{profile.ObjectName} is not below R_WeaponMaster.");
                    ValidateController(body, controller, profile);
                    ValidateControllerEvaluation(root, profile, controller, weapon.localPosition, weapon.localRotation, weapon.localScale);
                }
                Debug.Log("[GenericPathBoundWeapons] Validation passed for Vandal, Classic, Odin and Bucky.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            ThirdPersonWeaponAnimationSyncSetup.ValidateAuthoredControllers();
            SaveAllOrThrow("after Generic weapon validation");
        }

        [MenuItem(MenuRoot + "Repair Movement Additives And Masks")]
        public static void RepairMovementAdditivesAndMasks()
        {
            SaveAllOrThrow("before Generic movement additive repair");
            StopAnimationPreview();
            EnsureFolder(SharedFolder);

            GameObject root = PrefabUtility.LoadPrefabContents(ClovePrefabPath);
            try
            {
                Transform body = RequireDescendant(root.transform, "Body");
                Transform modelRoot = RequireDescendant(body, ModelRootName);
                PlayerVisibilityController visibility = RequireVisibility(root);

                foreach (CommonMotion motion in CommonMotions.Where(
                             motion => motion.Label.EndsWith(
                                 "Add",
                                 StringComparison.Ordinal)))
                {
                    BuildPathBoundClip(
                        motion.Label,
                        motion.SourcePath,
                        motion.Region,
                        SharedFolder,
                        body,
                        modelRoot);
                }

                Func<string, bool> includeUpperBody =
                    BuildUpperBodyMaskPredicate(body, visibility);
                AvatarMask upperMask = BuildTransformMask(
                    UpperMaskPath,
                    body,
                    includeUpperBody);
                AvatarMask noFingersMask = BuildTransformMask(
                    UpperNoFingersMaskPath,
                    body,
                    path => includeUpperBody(path) && !IsFingerPath(path));

                foreach (WeaponProfile profile in Profiles)
                {
                    AnimatorController controller =
                        AssetDatabase.LoadAssetAtPath<AnimatorController>(
                            profile.ControllerPath);
                    if (controller == null)
                    {
                        throw new InvalidOperationException(
                            $"Missing generated {profile.Name} controller.");
                    }

                    ConfigureGenericLayers(
                        controller,
                        upperMask,
                        noFingersMask);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            SaveAllOrThrow("after Generic movement additive repair");
            Validate();
            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<GameObject>(ClovePrefabPath);
            Debug.Log(
                "[GenericPathBoundWeapons] Repaired movement additive clips "
                + "and rebuilt upper-body masks from the final weapon hierarchy.");
        }

        private static ProfileBuild BuildProfile(WeaponProfile profile, RuntimeAnimatorController presentationController, Transform body, Transform modelRoot, AvatarMask upperMask, AvatarMask noFingersMask)
        {
            AnimatorController sourceController = ResolveBaseController(presentationController);
            var sourceClips = new HashSet<AnimationClip>();
            foreach (AnimatorControllerLayer layer in sourceController.layers)
                CollectStateMachineClips(layer.stateMachine, sourceClips);
            AnimationClip holdSource = RequireClipByFileName(sourceClips, profile.HoldFileName);
            AnimationClip reloadSource = RequireClipByFileName(sourceClips, profile.ReloadFileName);
            var replacements = new Dictionary<AnimationClip, AnimationClip>();

            foreach (AnimationClip sourceClip in sourceClips)
            {
                string sourcePath = AssetDatabase.GetAssetPath(sourceClip);
                if (!sourcePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    continue;
                CommonMotion common = CommonMotions.FirstOrDefault(motion => string.Equals(motion.SourcePath, sourcePath, StringComparison.Ordinal));
                AnimationClip generated = common != null
                    ? BuildPathBoundClip(common.Label, common.SourcePath, common.Region, SharedFolder, body, modelRoot)
                    : BuildPathBoundClip(MakeSafeName(Path.GetFileNameWithoutExtension(sourcePath)), sourcePath, MotionRegion.UpperBody, profile.ProductionFolder, body, modelRoot);
                replacements.Add(sourceClip, generated);
            }

            if (!replacements.TryGetValue(holdSource, out AnimationClip holdClip)
                || !replacements.TryGetValue(reloadSource, out AnimationClip reloadClip))
                throw new InvalidOperationException($"{profile.Name} hold/reload clips were not converted.");
            ValidateCompleteGripCurves(profile.Name + " Hold", holdClip);
            ValidateCompleteGripCurves(profile.Name + " Reload", reloadClip);

            AnimatorController controller = CloneController(sourceController, profile.ControllerPath);
            foreach (AnimatorControllerLayer layer in controller.layers)
                ReplaceStateMachineMotions(layer.stateMachine, replacements);
            ConfigureGenericLayers(controller, upperMask, noFingersMask);
            EditorUtility.SetDirty(controller);
            return new ProfileBuild(controller, holdClip);
        }

        private static AnimationClip BuildPathBoundClip(string label, string sourcePath, MotionRegion region, string folder, Transform body, Transform targetModelRoot)
        {
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            if (string.IsNullOrEmpty(sourceGuid))
                throw new InvalidOperationException($"Missing source asset {sourcePath}.");
            string suffix = sourceGuid.Substring(0, 8);
            string genericPath = folder + "/Source_" + label + "_" + suffix + ".fbx";
            CopySource(sourcePath, genericPath);
            ModelImporter importer = AssetImporter.GetAtPath(genericPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException($"No ModelImporter exists for {genericPath}.");
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
                throw new InvalidOperationException($"{genericPath} did not import as Generic.");
            GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(genericPath);
            GameObject source = UnityEngine.Object.Instantiate(sourceAsset);
            source.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                string[] sourcePaths = AnimationUtility.GetCurveBindings(sourceClip)
                    .Where(binding => binding.type == typeof(Transform))
                    .Select(binding => binding.path)
                    .Distinct(StringComparer.Ordinal)
                    .Where(path => IsIncludedPath(path, region))
                    .Where(path => source.transform.Find(path) != null && targetModelRoot.Find(path) != null)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                if (sourcePaths.Length == 0)
                    throw new InvalidOperationException($"{sourcePath} has no compatible direct Transform curves.");

                string destinationPath = folder + "/" + label + "_" + suffix + "_PathBound.anim";
                AnimationClip destination = AssetDatabase.LoadAssetAtPath<AnimationClip>(destinationPath);
                if (destination == null)
                {
                    destination = new AnimationClip();
                    AssetDatabase.CreateAsset(destination, destinationPath);
                }
                var clean = new AnimationClip { frameRate = sourceClip.frameRate, wrapMode = sourceClip.wrapMode, name = Path.GetFileNameWithoutExtension(destinationPath) };
                EditorUtility.CopySerialized(clean, destination);
                UnityEngine.Object.DestroyImmediate(clean);
                AnimationClipSettings settings =
                    AnimationUtility.GetAnimationClipSettings(sourceClip);
                if (settings.hasAdditiveReferencePose)
                {
                    // Curve paths below are rebound from Skeleton/... to
                    // CS_Smonk.../Skeleton/.... The imported FBX reference
                    // clip still targets the old paths, so retaining it makes
                    // an Additive Animator layer add an incompatible full pose.
                    // With no external reference Unity evaluates the generated
                    // clip relative to its own first frame, which has the same
                    // target hierarchy and matches the source reference frame.
                    settings.hasAdditiveReferencePose = false;
                    settings.additiveReferencePoseClip = null;
                    settings.additiveReferencePoseTime = 0f;
                }
                AnimationUtility.SetAnimationClipSettings(destination, settings);
                AnimationUtility.SetAnimationEvents(destination, AnimationUtility.GetAnimationEvents(sourceClip));

                float translationScale = Mathf.Max(Mathf.Abs(source.transform.lossyScale.x), 0.000001f)
                    / Mathf.Max(Mathf.Abs(targetModelRoot.lossyScale.x), 0.000001f);
                var curves = sourcePaths.ToDictionary(
                    path => path,
                    path => new PathCurveSet(ModelRootName + "/" + path, RequireChild(source.transform, path), translationScale),
                    StringComparer.Ordinal);
                int frameCount = Mathf.Max(1, Mathf.CeilToInt(sourceClip.length * sourceClip.frameRate));
                for (int frame = 0; frame <= frameCount; frame++)
                {
                    float time = Mathf.Min(sourceClip.length, frame / sourceClip.frameRate);
                    sourceClip.SampleAnimation(source, time);
                    foreach (PathCurveSet set in curves.Values)
                        set.Sample(time);
                }
                foreach (PathCurveSet set in curves.Values)
                    set.WriteTo(destination);
                destination.EnsureQuaternionContinuity();
                EditorUtility.SetDirty(destination);
                return destination;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static void CopySource(string sourcePath, string destinationPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath) == null)
            {
                if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                    throw new InvalidOperationException($"Could not copy Generic source from {sourcePath}.");
                return;
            }
            FileUtil.ReplaceFile(sourcePath, destinationPath);
            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static AnimatorController CloneController(AnimatorController source, string destinationPath)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(destinationPath) == null)
            {
                if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                    throw new InvalidOperationException($"Could not clone controller {sourcePath}.");
            }
            else
            {
                FileUtil.ReplaceFile(sourcePath, destinationPath);
                AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(destinationPath);
        }

        private static void ReplaceStateMachineMotions(AnimatorStateMachine machine, IReadOnlyDictionary<AnimationClip, AnimationClip> replacements)
        {
            foreach (ChildAnimatorState child in machine.states)
                child.state.motion = ReplaceMotion(child.state.motion, replacements);
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
                ReplaceStateMachineMotions(child.stateMachine, replacements);
        }

        private static Motion ReplaceMotion(Motion motion, IReadOnlyDictionary<AnimationClip, AnimationClip> replacements)
        {
            if (motion is AnimationClip clip)
                return replacements.TryGetValue(clip, out AnimationClip replacement) ? replacement : motion;
            if (motion is BlendTree tree)
            {
                ChildMotion[] children = tree.children;
                for (int index = 0; index < children.Length; index++)
                    children[index].motion = ReplaceMotion(children[index].motion, replacements);
                tree.children = children;
                EditorUtility.SetDirty(tree);
            }
            return motion;
        }

        private static void ConfigureGenericLayers(AnimatorController controller, AvatarMask upperMask, AvatarMask noFingersMask)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            if (layers.Length < 5)
                throw new InvalidOperationException($"{controller.name} no longer has five authored layers.");
            layers[1].avatarMask = upperMask;
            layers[2].avatarMask = noFingersMask;
            layers[3].avatarMask = noFingersMask;
            layers[4].defaultWeight = 0f;
            controller.layers = layers;
        }

        private static AvatarMask BuildTransformMask(string path, Transform body, Func<string, bool> include)
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }
            for (AvatarMaskBodyPart part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, false);
            var paths = new HashSet<string>(StringComparer.Ordinal) { string.Empty };
            foreach (Transform transform in body.GetComponentsInChildren<Transform>(true))
            {
                string relative = AnimationUtility.CalculateTransformPath(transform, body);
                if (include(relative))
                    AddPathAndAncestors(paths, relative);
            }
            string[] ordered = paths.OrderBy(value => value.Count(character => character == '/')).ThenBy(value => value, StringComparer.Ordinal).ToArray();
            mask.transformCount = ordered.Length;
            for (int index = 0; index < ordered.Length; index++)
            {
                mask.SetTransformPath(index, ordered[index]);
                mask.SetTransformActive(index, true);
            }
            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static void AuthorWeaponMount(GameObject root, Transform modelRoot, WeaponProfile profile, AnimationClip holdClip)
        {
            Transform weapon = RequireDescendant(root.transform, profile.ObjectName);
            MountPose pose = CalculateMountPose(root, profile, holdClip, weapon.localScale);
            weapon.SetParent(RequireChild(modelRoot, WeaponMasterPath), false);
            weapon.localPosition = pose.Position;
            weapon.localRotation = pose.Rotation;
            weapon.localScale = pose.Scale;
        }

        private static MountPose CalculateMountPose(GameObject sourceRoot, WeaponProfile profile, AnimationClip holdClip, Vector3 authoredScale)
        {
            GameObject measurement = UnityEngine.Object.Instantiate(sourceRoot);
            measurement.hideFlags = HideFlags.HideAndDontSave;
            measurement.SetActive(true);
            try
            {
                Transform body = RequireDescendant(measurement.transform, "Body");
                Transform model = RequireDescendant(body, ModelRootName);
                Transform weapon = RequireDescendant(measurement.transform, profile.ObjectName);
                Transform rightHand = RequireChild(model, RightHandPath);
                Transform leftHand = RequireChild(model, LeftHandPath);
                Transform trigger = RequireDescendant(weapon, "Trigger");
                Transform leftTarget = RequireDescendant(weapon, "Left_Hand_Target");
                weapon.SetParent(RequireChild(model, WeaponMasterPath), false);
                weapon.localPosition = Vector3.zero;
                weapon.localRotation = Quaternion.Euler(0f, 270f, 0f);
                weapon.localScale = authoredScale;
                holdClip.SampleAnimation(body.gameObject, 0f);
                Vector3 weaponAxis = leftTarget.position - trigger.position;
                Vector3 handAxis = leftHand.position - rightHand.position;
                if (weaponAxis.sqrMagnitude < 0.000001f || handAxis.sqrMagnitude < 0.000001f)
                    throw new InvalidOperationException($"{profile.Name} grip anchors are degenerate.");
                weapon.rotation = Quaternion.FromToRotation(weaponAxis, handAxis) * weapon.rotation;
                weapon.position += (rightHand.position + leftHand.position) * 0.5f - (trigger.position + leftTarget.position) * 0.5f;
                return new MountPose(weapon.localPosition, weapon.localRotation, weapon.localScale);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(measurement);
            }
        }

        private static void AuthorPresentations(PlayerVisibilityController visibility, IReadOnlyDictionary<string, AnimatorController> controllers)
        {
            var serialized = new SerializedObject(visibility);
            SerializedProperty presentations = serialized.FindProperty("thirdPersonWeaponPresentations");
            for (int index = 0; index < presentations.arraySize; index++)
            {
                SerializedProperty entry = presentations.GetArrayElementAtIndex(index);
                WeaponData data = entry.FindPropertyRelative("weaponData").objectReferenceValue as WeaponData;
                if (data == null || !controllers.TryGetValue(data.name, out AnimatorController controller))
                    continue;
                entry.FindPropertyRelative("characterController").objectReferenceValue = controller;
                entry.FindPropertyRelative("characterRigMode").enumValueIndex = (int)ThirdPersonCharacterRigMode.GenericPathBound;
                entry.FindPropertyRelative("characterAvatar").objectReferenceValue = null;
                entry.FindPropertyRelative("useLeftHandIK").boolValue = false;
                entry.FindPropertyRelative("animationDrivenLeftHandIK").boolValue = false;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visibility);
        }

        private static void ValidateController(Transform body, AnimatorController controller, WeaponProfile profile)
        {
            var clips = new HashSet<AnimationClip>();
            foreach (AnimatorControllerLayer layer in controller.layers)
                CollectStateMachineClips(layer.stateMachine, clips);
            ValidateCompleteGripCurves(profile.Name + " Hold", RequireGeneratedClip(clips, profile.HoldFileName));
            ValidateCompleteGripCurves(profile.Name + " Reload", RequireGeneratedClip(clips, profile.ReloadFileName));
            foreach (AnimationClip clip in clips.Where(IsGeneratedClip))
            {
                if (clip.humanMotion)
                    throw new InvalidOperationException($"{profile.Name} generated clip {clip.name} is Humanoid.");

                AnimationClipSettings settings =
                    AnimationUtility.GetAnimationClipSettings(clip);
                if (settings.hasAdditiveReferencePose
                    && settings.additiveReferencePoseClip != null)
                {
                    var referenceBindings = new HashSet<string>(
                        AnimationUtility.GetCurveBindings(
                                settings.additiveReferencePoseClip)
                            .Select(binding =>
                                binding.path + "\n" + binding.propertyName),
                        StringComparer.Ordinal);
                    bool hasCompatibleReference = AnimationUtility
                        .GetCurveBindings(clip)
                        .Any(binding => referenceBindings.Contains(
                            binding.path + "\n" + binding.propertyName));
                    if (!hasCompatibleReference)
                    {
                        throw new InvalidOperationException(
                            $"{clip.name} keeps an additive reference pose "
                            + "whose Transform paths do not match the "
                            + "generated path-bound clip.");
                    }
                }

                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip).Where(binding => binding.type == typeof(Transform)))
                {
                    if (body.Find(binding.path) == null)
                        throw new InvalidOperationException($"{clip.name} targets missing path {binding.path}.");
                    if (binding.propertyName.StartsWith("m_LocalScale", StringComparison.Ordinal))
                        throw new InvalidOperationException($"{clip.name} must not animate Transform scale.");
                }
            }
        }

        private static void ValidateControllerEvaluation(GameObject prefabRoot, WeaponProfile profile, AnimatorController controller, Vector3 expectedPosition, Quaternion expectedRotation, Vector3 expectedScale)
        {
            GameObject sample = UnityEngine.Object.Instantiate(prefabRoot);
            sample.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                sample.SetActive(true);
                Transform body = RequireDescendant(sample.transform, "Body");
                Transform model = RequireDescendant(body, ModelRootName);
                Transform weapon = RequireDescendant(sample.transform, profile.ObjectName);
                weapon.gameObject.SetActive(true);
                Animator gunAnimator =
                    weapon.GetComponentInChildren<Animator>(true);
                if (gunAnimator == null)
                {
                    throw new InvalidOperationException(
                        $"{profile.Name} has no dedicated gun Animator.");
                }
                gunAnimator.enabled = true;
                gunAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                gunAnimator.Rebind();
                gunAnimator.Update(0.0001f);
                Transform rightHand = RequireChild(model, RightHandPath);
                Transform leftHand = RequireChild(model, LeftHandPath);
                Transform trigger = RequireDescendant(weapon, "Trigger");
                Transform leftTarget = RequireDescendant(weapon, "Left_Hand_Target");
                Animator animator = body.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.avatar = null;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
                if (weapon.parent != RequireChild(model, WeaponMasterPath)
                    || Vector3.Distance(weapon.localPosition, expectedPosition) > 0.00001f
                    || Quaternion.Angle(weapon.localRotation, expectedRotation) > 0.01f
                    || Vector3.Distance(weapon.localScale, expectedScale) > 0.00001f)
                    throw new InvalidOperationException($"{profile.Name} mount changed during evaluation setup.");

                AnimationClip hold = RequireGeneratedClip(controller.animationClips, profile.HoldFileName);
                AnimationClip reload = RequireGeneratedClip(controller.animationClips, profile.ReloadFileName);
                EvaluateState(animator, FindStateByMotion(controller, hold), 0f);
                AssertGrip(profile.Name + " Hold", rightHand, trigger, leftHand, leftTarget, true, true);

                animator.SetBool("Grounded", true);
                animator.SetBool("FreeFall", false);
                animator.SetFloat("Speed", 3f);
                animator.Play("Locomotion", 0, 0.25f);
                animator.Play(profile.Name + " Hold", 1, 0f);
                animator.Play("Locomotion Add", 2, 0.25f);
                animator.Update(0.0001f);
                AssertGrip(
                    profile.Name + " Walk",
                    rightHand,
                    trigger,
                    leftHand,
                    leftTarget,
                    true,
                    true,
                    0.35f);

                EvaluateState(animator, FindStateByMotion(controller, reload), 0.55f);
                // Reload clips intentionally let one or both hands leave their
                // resting grip (notably Odin's charging/reload reach). Their
                // authored paths are validated above; here only reject invalid
                // transforms rather than forcing a false continuous grip.
                AssertGrip(profile.Name + " Reload", rightHand, trigger, leftHand, leftTarget, false, false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sample);
            }
        }

        private static StateLocation FindStateByMotion(AnimatorController controller, AnimationClip clip)
        {
            for (int layer = 0; layer < controller.layers.Length; layer++)
            {
                AnimatorState state = FindStateByMotion(controller.layers[layer].stateMachine, clip);
                if (state != null)
                    return new StateLocation(layer, state.name);
            }
            throw new InvalidOperationException($"No state uses generated clip {clip.name}.");
        }

        private static AnimatorState FindStateByMotion(AnimatorStateMachine machine, AnimationClip clip)
        {
            foreach (ChildAnimatorState child in machine.states)
                if (MotionContains(child.state.motion, clip))
                    return child.state;
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                AnimatorState found = FindStateByMotion(child.stateMachine, clip);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static bool MotionContains(Motion motion, AnimationClip clip)
        {
            if (motion == clip)
                return true;
            return motion is BlendTree tree && tree.children.Any(child => MotionContains(child.motion, clip));
        }

        private static void EvaluateState(Animator animator, StateLocation state, float normalizedTime)
        {
            animator.Play(state.Name, state.Layer, normalizedTime);
            animator.Update(0.0001f);
            if (!animator.GetCurrentAnimatorStateInfo(state.Layer).IsName(state.Name))
                throw new InvalidOperationException($"Animator could not evaluate state {state.Name}.");
        }

        private static void AssertGrip(
            string label,
            Transform rightHand,
            Transform trigger,
            Transform leftHand,
            Transform leftTarget,
            bool requireRight,
            bool requireLeft,
            float maximum = 0.30f)
        {
            float right = Vector3.Distance(rightHand.position, trigger.position);
            float left = Vector3.Distance(leftHand.position, leftTarget.position);
            float handGap = Vector3.Distance(
                rightHand.position,
                leftHand.position);
            if (!float.IsFinite(right)
                || !float.IsFinite(left)
                || !float.IsFinite(handGap)
                || (requireRight && right > maximum)
                || (requireLeft && left > maximum)
                // Odin's HMG grips are authored farther apart than the rifle
                // profiles. The broken additive path tears the hands by more
                // than three metres, so one metre still rejects that failure
                // without rejecting the valid HMG stance.
                || ((requireRight || requireLeft) && handGap > 1.0f))
            {
                throw new InvalidOperationException(
                    $"{label} presentation is disconnected "
                    + $"(right={right:F3}, left={left:F3}, "
                    + $"handGap={handGap:F3}, requireRight={requireRight}, "
                    + $"requireLeft={requireLeft}).");
            }
            Debug.Log(
                $"[GenericPathBoundWeapons] {label}: "
                + $"right={right * 100f:F2}cm, "
                + $"left={left * 100f:F2}cm, "
                + $"handGap={handGap * 100f:F2}cm.");
        }

        private static void ValidateCompleteGripCurves(string label, AnimationClip clip)
        {
            string prefix = ModelRootName + "/";
            string[] paths = AnimationUtility.GetCurveBindings(clip).Where(binding => binding.type == typeof(Transform)).Select(binding => binding.path).Distinct(StringComparer.Ordinal).ToArray();
            bool right = paths.Any(path => path == prefix + RightHandPath || path.StartsWith(prefix + RightHandPath + "/", StringComparison.Ordinal));
            bool left = paths.Any(path => path == prefix + LeftHandPath || path.StartsWith(prefix + LeftHandPath + "/", StringComparison.Ordinal));
            bool weapon = paths.Any(path => path == prefix + WeaponRootPath || path.StartsWith(prefix + WeaponRootPath + "/", StringComparison.Ordinal));
            if (!right || !left || !weapon)
                throw new InvalidOperationException($"{label} lacks direct grip curves (right={right}, left={left}, weapon={weapon}).");
        }

        private static AnimationClip RequireClipByFileName(IEnumerable<AnimationClip> clips, string fileName)
        {
            AnimationClip clip = clips.SingleOrDefault(candidate => string.Equals(Path.GetFileName(AssetDatabase.GetAssetPath(candidate)), fileName, StringComparison.Ordinal));
            return clip != null ? clip : throw new InvalidOperationException($"Controller does not reference {fileName}.");
        }

        private static AnimationClip RequireGeneratedClip(IEnumerable<AnimationClip> clips, string sourceFileName)
        {
            string label = MakeSafeName(Path.GetFileNameWithoutExtension(sourceFileName));
            AnimationClip clip = clips
                .Where(candidate => IsGeneratedClip(candidate)
                    && candidate.name.StartsWith(label + "_", StringComparison.Ordinal))
                .Distinct()
                .SingleOrDefault();
            return clip != null ? clip : throw new InvalidOperationException($"Controller does not reference generated {sourceFileName}.");
        }

        private static bool IsGeneratedClip(AnimationClip clip) => AssetDatabase.GetAssetPath(clip).EndsWith("_PathBound.anim", StringComparison.Ordinal);

        private static AnimatorController ResolveBaseController(RuntimeAnimatorController controller)
        {
            RuntimeAnimatorController current = controller;
            while (current is AnimatorOverrideController overrides)
                current = overrides.runtimeAnimatorController;
            return current as AnimatorController ?? throw new InvalidOperationException($"Unsupported controller type {current?.GetType().FullName ?? "null"}.");
        }

        private static void CollectStateMachineClips(AnimatorStateMachine machine, ISet<AnimationClip> clips)
        {
            foreach (ChildAnimatorState child in machine.states)
                CollectMotionClips(child.state.motion, clips);
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
                CollectStateMachineClips(child.stateMachine, clips);
        }

        private static void CollectMotionClips(Motion motion, ISet<AnimationClip> clips)
        {
            if (motion is AnimationClip clip)
                clips.Add(clip);
            else if (motion is BlendTree tree)
                foreach (ChildMotion child in tree.children)
                    CollectMotionClips(child.motion, clips);
        }

        private static AnimationClip LoadImportedClip(string path)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__", StringComparison.Ordinal));
            return clip != null ? clip : throw new InvalidOperationException($"No imported clip exists at {path}.");
        }

        private static PlayerVisibilityController RequireVisibility(GameObject root) => root.GetComponent<PlayerVisibilityController>() ?? throw new InvalidOperationException("Clove has no PlayerVisibilityController.");

        private static ThirdPersonWeaponPresentation RequirePresentation(PlayerVisibilityController visibility, string name)
        {
            ThirdPersonWeaponPresentation result = visibility.ThirdPersonWeaponPresentations.SingleOrDefault(candidate => candidate?.WeaponData != null && candidate.WeaponData.name == name);
            return result ?? throw new InvalidOperationException($"Clove has no {name} presentation.");
        }

        private static bool IsIncludedPath(string path, MotionRegion region)
        {
            if (region == MotionRegion.FullSkeleton)
                return path == SkeletonRootPath || path.StartsWith(SkeletonRootPath + "/", StringComparison.Ordinal);
            return path == UpperBodyRootPath || path.StartsWith(UpperBodyRootPath + "/", StringComparison.Ordinal)
                || path == WeaponRootPath || path.StartsWith(WeaponRootPath + "/", StringComparison.Ordinal);
        }

        private static bool IsTargetUpperBodyPath(string path)
        {
            string prefix = ModelRootName + "/";
            return path.StartsWith(prefix, StringComparison.Ordinal) && IsIncludedPath(path.Substring(prefix.Length), MotionRegion.UpperBody);
        }

        private static Func<string, bool> BuildUpperBodyMaskPredicate(
            Transform body,
            PlayerVisibilityController visibility)
        {
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

        private static bool IsFingerPath(string path) => path.IndexOf("_Thumb", StringComparison.Ordinal) >= 0
            || path.IndexOf("_Index", StringComparison.Ordinal) >= 0
            || path.IndexOf("_Middle", StringComparison.Ordinal) >= 0
            || path.IndexOf("_Ring", StringComparison.Ordinal) >= 0
            || path.IndexOf("_Pinky", StringComparison.Ordinal) >= 0;

        private static void AddPathAndAncestors(ISet<string> paths, string path)
        {
            while (!string.IsNullOrEmpty(path))
            {
                paths.Add(path);
                int separator = path.LastIndexOf('/');
                path = separator >= 0 ? path.Substring(0, separator) : string.Empty;
            }
        }

        private static Transform RequireDescendant(Transform root, string name)
        {
            Transform result = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(transform => transform.name == name);
            return result != null ? result : throw new InvalidOperationException($"Missing {name} below {root.name}.");
        }

        private static Transform RequireChild(Transform parent, string path)
        {
            Transform result = parent.Find(path);
            return result != null ? result : throw new InvalidOperationException($"Missing {path} below {parent.name}.");
        }

        private static void EnsureFolder(string folder)
        {
            string current = "Assets";
            foreach (string segment in folder.Substring("Assets/".Length).Split('/'))
            {
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }

        private static void StopAnimationPreview()
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
        }

        private static void SaveAllOrThrow(string phase)
        {
            if (!EditorSceneManager.SaveOpenScenes())
                throw new InvalidOperationException($"Open scenes could not be saved {phase}.");
            AssetDatabase.SaveAssets();
        }

        private static string MakeSafeName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(character => invalid.Contains(character) || character == '|' ? '_' : character).ToArray());
        }

        private static CommonMotion Common(string label, string file, MotionRegion region) => new CommonMotion(label, CoreFolder + "/" + file, region);

        private enum MotionRegion { FullSkeleton, UpperBody }

        private sealed class CommonMotion
        {
            public CommonMotion(string label, string sourcePath, MotionRegion region) { Label = label; SourcePath = sourcePath; Region = region; }
            public string Label { get; }
            public string SourcePath { get; }
            public MotionRegion Region { get; }
        }

        private sealed class WeaponProfile
        {
            public WeaponProfile(string name, string objectName, string hold, string reload) { Name = name; ObjectName = objectName; HoldFileName = hold; ReloadFileName = reload; }
            public string Name { get; }
            public string ObjectName { get; }
            public string HoldFileName { get; }
            public string ReloadFileName { get; }
            public string ProductionFolder => "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/" + Name + "/S0/3P/Anims/Clove_GenericPathBound";
            public string ControllerPath => ProductionFolder + "/Clove" + Name + "_GenericPathBound.controller";
        }

        private readonly struct ProfileBuild
        {
            public ProfileBuild(AnimatorController controller, AnimationClip holdClip) { Controller = controller; HoldClip = holdClip; }
            public AnimatorController Controller { get; }
            public AnimationClip HoldClip { get; }
        }

        private readonly struct MountPose
        {
            public MountPose(Vector3 position, Quaternion rotation, Vector3 scale) { Position = position; Rotation = rotation; Scale = scale; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 Scale { get; }
        }

        private readonly struct StateLocation
        {
            public StateLocation(int layer, string name) { Layer = layer; Name = name; }
            public int Layer { get; }
            public string Name { get; }
        }

        private sealed class PathCurveSet
        {
            private readonly string path;
            private readonly Transform source;
            private readonly float translationScale;
            private readonly AnimationCurve px = new AnimationCurve(), py = new AnimationCurve(), pz = new AnimationCurve();
            private readonly AnimationCurve rx = new AnimationCurve(), ry = new AnimationCurve(), rz = new AnimationCurve(), rw = new AnimationCurve();
            private Quaternion previousRotation;
            private bool hasPreviousRotation;

            public PathCurveSet(string path, Transform source, float translationScale) { this.path = path; this.source = source; this.translationScale = translationScale; }

            public void Sample(float time)
            {
                Vector3 position = source.localPosition * translationScale;
                Quaternion rotation = source.localRotation;
                if (hasPreviousRotation && Quaternion.Dot(previousRotation, rotation) < 0f)
                    rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                previousRotation = rotation;
                hasPreviousRotation = true;
                Add(px, time, position.x); Add(py, time, position.y); Add(pz, time, position.z);
                Add(rx, time, rotation.x); Add(ry, time, rotation.y); Add(rz, time, rotation.z); Add(rw, time, rotation.w);
            }

            public void WriteTo(AnimationClip clip)
            {
                Set(clip, "m_LocalPosition.x", px); Set(clip, "m_LocalPosition.y", py); Set(clip, "m_LocalPosition.z", pz);
                Set(clip, "m_LocalRotation.x", rx); Set(clip, "m_LocalRotation.y", ry); Set(clip, "m_LocalRotation.z", rz); Set(clip, "m_LocalRotation.w", rw);
            }

            private void Set(AnimationClip clip, string property, AnimationCurve curve)
            {
                for (int index = 0; index < curve.length; index++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
            }

            private static void Add(AnimationCurve curve, float time, float value) => curve.AddKey(new Keyframe(time, value));
        }
    }
}
