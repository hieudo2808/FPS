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
    /// Creates an isolated Clove prefab used to compare Generic, path-bound
    /// third-person animation with the current Humanoid presentation.
    /// </summary>
    public static class ThirdPersonGenericWeaponDriverExperiment
    {
        private const string MenuRoot = "FPS/Third Person/Operator 1P-Style Experiment/";
        private const string ClovePrefabPath =
            "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab";
        private const string ExperimentPrefabPath =
            "Assets/FPS/Features/Characters/Content/Players/Clove/"
            + "ClovePlayer_Operator1PStyle_Experiment.prefab";
        private const string SourceFolder =
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/S0/3P/Anims";
        private const string ExperimentFolder = SourceFolder + "/Experiment_1PStyle";
        private const string ModelRootName = "CS_Smonk_S0_Skelmesh.ao";
        private const string RightHandRelativePath =
            "Skeleton/Root/Splitter/Spine1/Spine2/Spine3/Spine4/"
            + "R_Clavicle/R_Shoulder/R_Elbow/R_Hand";
        private const string SourceWeaponMasterPath =
            "Skeleton/Root/Splitter/MasterWeaponAim/MasterWeapon/R_WeaponMaster";
        private const string DriverAnimatorName = "OperatorWeaponDriverAnimator";
        private const string WeaponPoseName = "WeaponPose";
        private const string WeaponDeltaName = "WeaponDelta";
        private const string ControllerPath =
            ExperimentFolder + "/CloveOperator_1PStyle.controller";

        private static readonly ClipDefinition[] Clips =
        {
            new ClipDefinition(
                "Hold",
                SourceFolder + "/TP_Core_Boltsniper_S0_IdlePose_UB.fbx",
                ExperimentFolder + "/Operator_Hold_Generic.fbx",
                ExperimentFolder + "/Operator_Hold_Clove.anim",
                true),
            new ClipDefinition(
                "Equip",
                SourceFolder + "/TP_Core_Boltsniper_S0_Equip_UB.fbx",
                ExperimentFolder + "/Operator_Equip_Generic.fbx",
                ExperimentFolder + "/Operator_Equip_Clove.anim",
                false),
            new ClipDefinition(
                "Reload",
                SourceFolder + "/TP_Core_Boltsniper_S0_Reload_UB.fbx",
                ExperimentFolder + "/Operator_Reload_Generic.fbx",
                ExperimentFolder + "/Operator_Reload_Clove.anim",
                false)
        };

        [MenuItem(MenuRoot + "Build And Open Isolated Prefab")]
        public static void BuildAndOpen()
        {
            SaveAllOrThrow("before building the Operator Generic experiment");
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();

            GameObject source = PrefabUtility.LoadPrefabContents(ClovePrefabPath);
            GameObject experiment = null;
            try
            {
                experiment = UnityEngine.Object.Instantiate(source);
                experiment.name = "ClovePlayer_Operator1PStyle_Experiment";
                BuildExperiment(experiment);
                PrefabUtility.SaveAsPrefabAsset(experiment, ExperimentPrefabPath);
            }
            finally
            {
                if (experiment != null)
                    UnityEngine.Object.DestroyImmediate(experiment);
                PrefabUtility.UnloadPrefabContents(source);
            }

            SaveAllOrThrow("after building the Operator Generic experiment");
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ExperimentPrefabPath);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            AssetDatabase.OpenAsset(asset);
            EditorApplication.delayCall += () =>
            {
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
            };

            Debug.Log(
                "[Operator1PStyle] Opened the isolated Generic prefab. "
                + "The canonical ClovePlayer prefab and Humanoid clips were not modified.");
        }

        [MenuItem(MenuRoot + "Validate Isolated Prefab")]
        public static void Validate()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ExperimentPrefabPath);
            try
            {
                Transform body = RequireChild(root.transform, "Body");
                Transform modelRoot = RequireChild(body, ModelRootName);
                Transform rightHand = RequireChild(modelRoot, RightHandRelativePath);
                Transform driverRoot = RequireChild(rightHand, DriverAnimatorName);
                Transform weaponPose = RequireChild(driverRoot, WeaponPoseName);
                Transform weaponDelta = RequireChild(weaponPose, WeaponDeltaName);
                Transform weapon = FindDescendant(root.transform, "Operator_3P");
                Animator bodyAnimator = body.GetComponent<Animator>();
                Animator driverAnimator = driverRoot.GetComponent<Animator>();

                if (weapon == null || weapon.parent != weaponDelta)
                    throw new InvalidOperationException(
                        "Operator_3P is not authored under the path-bound WeaponPose.");
                if (bodyAnimator == null || bodyAnimator.avatar == null)
                    throw new InvalidOperationException(
                        "The Clove Body Humanoid Animator was not preserved.");
                if (driverAnimator == null || driverAnimator.avatar != null
                    || AssetDatabase.GetAssetPath(driverAnimator.runtimeAnimatorController)
                        != ControllerPath)
                {
                    throw new InvalidOperationException(
                        "MasterWeaponAim is not configured as the Generic driver.");
                }

                foreach (ClipDefinition definition in Clips)
                {
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        definition.NormalizedPath);
                    if (clip == null)
                        throw new InvalidOperationException(
                            $"Missing normalized clip: {definition.NormalizedPath}");
                    int driverBindings = AnimationUtility.GetCurveBindings(clip)
                        .Count(binding => binding.path
                            == WeaponPoseName + "/" + WeaponDeltaName);
                    if (driverBindings == 0)
                        throw new InvalidOperationException(
                            $"{clip.name} has no R_WeaponMaster transform curves.");
                }

                Debug.Log(
                    "[Operator1PStyle] Validation passed: Humanoid still owns the "
                    + "Clove Body, Generic owns only the path-bound WeaponPose, "
                    + "and Operator_3P is authored below that pose.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem(MenuRoot + "Preview Reload At 55 Percent")]
        public static void PreviewReloadMidpoint()
        {
            PreviewClip("Reload", 0.55f);
        }

        [MenuItem(MenuRoot + "Preview Hold At Start")]
        public static void PreviewHoldAtStart()
        {
            PreviewClip("Hold", 0f);
        }

        private static void PreviewClip(string label, float normalizedTime)
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
            SaveAllOrThrow($"before previewing the Generic {label} clip");

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.assetPath != ExperimentPrefabPath)
            {
                throw new InvalidOperationException(
                    "Open the isolated Operator 1P-style prefab before previewing.");
            }

            Transform body = RequireChild(stage.prefabContentsRoot.transform, "Body");
            Transform modelRoot = RequireChild(body, ModelRootName);
            Transform rightHand = RequireChild(modelRoot, RightHandRelativePath);
            Transform driverRoot = RequireChild(rightHand, DriverAnimatorName);
            ClipDefinition definition =
                Clips.First(candidate => candidate.Label == label);
            AnimationClip bodyClip = LoadImportedClip(definition.HumanoidPath);
            AnimationClip driverClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                definition.NormalizedPath);
            if (driverClip == null)
                throw new InvalidOperationException(
                    $"The normalized {label} driver clip is missing.");

            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            try
            {
                AnimationMode.SampleAnimationClip(
                    body.gameObject,
                    bodyClip,
                    bodyClip.length * normalizedTime);
                AnimationMode.SampleAnimationClip(
                    driverRoot.gameObject,
                    driverClip,
                    driverClip.length * normalizedTime);
            }
            finally
            {
                AnimationMode.EndSampling();
            }

            Selection.activeGameObject = body.gameObject;
            SceneView.RepaintAll();
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[Operator1PStyle] Previewing {label} at "
                + $"{normalizedTime:P0}. This is an "
                + "AnimationMode preview only; no sampled transforms were saved.");
        }

        private static void BuildExperiment(GameObject root)
        {
            Transform body = RequireChild(root.transform, "Body");
            Transform modelRoot = RequireChild(body, ModelRootName);
            Transform rightHand = RequireChild(modelRoot, RightHandRelativePath);

            var normalizedClips = new Dictionary<string, AnimationClip>(
                StringComparer.Ordinal);
            foreach (ClipDefinition definition in Clips)
            {
                AnimationClip source = LoadImportedClip(definition.GenericPath);
                AnimationClip normalized = BuildNormalizedClip(
                    definition,
                    source);
                normalizedClips.Add(definition.Label, normalized);
            }

            AnimatorController controller = BuildController(normalizedClips);
            Transform driverRoot = rightHand.Find(DriverAnimatorName);
            if (driverRoot == null)
            {
                var driverObject = new GameObject(DriverAnimatorName);
                driverRoot = driverObject.transform;
                driverRoot.SetParent(rightHand, false);
            }
            driverRoot.localPosition = Vector3.zero;
            driverRoot.localRotation = Quaternion.identity;
            driverRoot.localScale = Vector3.one;

            Transform weaponPose = driverRoot.Find(WeaponPoseName);
            if (weaponPose == null)
            {
                var poseObject = new GameObject(WeaponPoseName);
                weaponPose = poseObject.transform;
                weaponPose.SetParent(driverRoot, false);
            }

            Transform weaponDelta = weaponPose.Find(WeaponDeltaName);
            if (weaponDelta == null)
            {
                var deltaObject = new GameObject(WeaponDeltaName);
                weaponDelta = deltaObject.transform;
                weaponDelta.SetParent(weaponPose, false);
            }

            Transform operatorPresentation = FindDescendant(root.transform, "Operator_3P");
            if (operatorPresentation == null)
                throw new InvalidOperationException(
                    "Clove prefab has no authored Operator_3P presentation.");

            // Keep the weapon's authored Clove-space pose as the static baseline.
            // The Generic source FBX uses a different bone basis/unit scale, so its
            // absolute R_WeaponMaster matrix is not a valid pose for this hierarchy.
            // Only the source motion delta is transferred to WeaponDelta below.
            Vector3 baselinePosition = operatorPresentation.localPosition;
            Quaternion baselineRotation = operatorPresentation.localRotation;
            Vector3 baselineScale = operatorPresentation.localScale;
            weaponPose.localPosition = baselinePosition;
            weaponPose.localRotation = baselineRotation;
            weaponPose.localScale = baselineScale;
            weaponDelta.localPosition = Vector3.zero;
            weaponDelta.localRotation = Quaternion.identity;
            weaponDelta.localScale = Vector3.one;

            operatorPresentation.SetParent(weaponDelta, false);
            operatorPresentation.localPosition = Vector3.zero;
            operatorPresentation.localRotation = Quaternion.identity;
            operatorPresentation.localScale = Vector3.one;

            foreach (string weaponName in new[]
                     {
                         "Vandal_3P", "Classic_3P", "Operator_3P", "Odin_3P", "Bucky_3P"
                     })
            {
                Transform presentation = FindDescendant(root.transform, weaponName);
                if (presentation != null)
                    presentation.gameObject.SetActive(weaponName == "Operator_3P");
            }

            Animator bodyAnimator = body.GetComponent<Animator>();
            if (bodyAnimator == null || bodyAnimator.avatar == null)
                throw new InvalidOperationException("Clove Body has no Animator.");

            Animator driverAnimator = driverRoot.GetComponent<Animator>();
            if (driverAnimator == null)
                driverAnimator = driverRoot.gameObject.AddComponent<Animator>();
            driverAnimator.avatar = null;
            driverAnimator.runtimeAnimatorController = controller;
            driverAnimator.applyRootMotion = false;

            ThirdPersonLeftHandIK leftHandIk = root.GetComponent<ThirdPersonLeftHandIK>();
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

        private static AnimationClip BuildNormalizedClip(
            ClipDefinition definition,
            AnimationClip source)
        {
            AnimationClip destination = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                definition.NormalizedPath);
            if (destination == null)
            {
                destination = new AnimationClip();
                AssetDatabase.CreateAsset(destination, definition.NormalizedPath);
            }

            ClearClip(destination);
            destination.name = $"Operator_{definition.Label}_Clove";
            destination.frameRate = source.frameRate;
            destination.wrapMode = source.wrapMode;
            destination.legacy = false;

            GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
                definition.GenericPath);
            if (sourceAsset == null)
                throw new InvalidOperationException(
                    $"No source model exists at {definition.GenericPath}.");

            AnimationClip holdReference = LoadImportedClip(Clips[0].GenericPath);
            GameObject sourceInstance = UnityEngine.Object.Instantiate(sourceAsset);
            sourceInstance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Transform sourceHand = RequireChild(
                    sourceInstance.transform,
                    RightHandRelativePath);
                Transform sourceWeapon = RequireChild(
                    sourceInstance.transform,
                    SourceWeaponMasterPath);

                holdReference.SampleAnimation(sourceInstance, 0f);
                Matrix4x4 reference =
                    sourceHand.worldToLocalMatrix * sourceWeapon.localToWorldMatrix;
                Matrix4x4 inverseReference = reference.inverse;

                string deltaPath = WeaponPoseName + "/" + WeaponDeltaName;
                var positionX = new AnimationCurve();
                var positionY = new AnimationCurve();
                var positionZ = new AnimationCurve();
                var rotationX = new AnimationCurve();
                var rotationY = new AnimationCurve();
                var rotationZ = new AnimationCurve();
                var rotationW = new AnimationCurve();

                int frameCount = Mathf.Max(
                    1,
                    Mathf.CeilToInt(source.length * source.frameRate));
                for (int frame = 0; frame <= frameCount; frame++)
                {
                    float time = Mathf.Min(
                        source.length,
                        frame / source.frameRate);
                    source.SampleAnimation(sourceInstance, time);
                    Matrix4x4 current =
                        sourceHand.worldToLocalMatrix * sourceWeapon.localToWorldMatrix;
                    Matrix4x4 delta = inverseReference * current;
                    Vector3 position = delta.GetColumn(3);
                    Quaternion rotation = delta.rotation;

                    positionX.AddKey(time, position.x);
                    positionY.AddKey(time, position.y);
                    positionZ.AddKey(time, position.z);
                    rotationX.AddKey(time, rotation.x);
                    rotationY.AddKey(time, rotation.y);
                    rotationZ.AddKey(time, rotation.z);
                    rotationW.AddKey(time, rotation.w);
                }

                SetTransformCurve(destination, deltaPath, "m_LocalPosition.x", positionX);
                SetTransformCurve(destination, deltaPath, "m_LocalPosition.y", positionY);
                SetTransformCurve(destination, deltaPath, "m_LocalPosition.z", positionZ);
                SetTransformCurve(destination, deltaPath, "m_LocalRotation.x", rotationX);
                SetTransformCurve(destination, deltaPath, "m_LocalRotation.y", rotationY);
                SetTransformCurve(destination, deltaPath, "m_LocalRotation.z", rotationZ);
                SetTransformCurve(destination, deltaPath, "m_LocalRotation.w", rotationW);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceInstance);
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopTime = definition.Loop;
            AnimationUtility.SetAnimationClipSettings(destination, settings);
            AnimationUtility.SetAnimationEvents(
                destination,
                AnimationUtility.GetAnimationEvents(source));
            destination.EnsureQuaternionContinuity();
            EditorUtility.SetDirty(destination);

            Debug.Log(
                $"[Operator1PStyle] {definition.Label}: baked the source "
                + "R_WeaponMaster pose relative to R_Hand into seven "
                + "path-bound WeaponDelta curves.");
            return destination;
        }

        private static void SetTransformCurve(
            AnimationClip clip,
            string path,
            string propertyName,
            AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(path, typeof(Transform), propertyName),
                curve);
        }

        private static void ClearClip(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, null);
            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            }
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        }

        private static AnimatorController BuildController(
            IReadOnlyDictionary<string, AnimationClip> clips)
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
                controller.RemoveParameter(parameter);
            controller.AddParameter("Equip", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Reload", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in stateMachine.states.ToArray())
                stateMachine.RemoveState(child.state);
            foreach (AnimatorStateTransition transition in
                     stateMachine.anyStateTransitions.ToArray())
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }

            AnimatorState hold = stateMachine.AddState("Hold");
            AnimatorState equip = stateMachine.AddState("Equip");
            AnimatorState reload = stateMachine.AddState("Reload");
            hold.motion = clips["Hold"];
            equip.motion = clips["Equip"];
            reload.motion = clips["Reload"];
            stateMachine.defaultState = hold;

            AddActionTransition(stateMachine, equip, "Equip");
            AddActionTransition(stateMachine, reload, "Reload");
            AddReturnTransition(equip, hold);
            AddReturnTransition(reload, hold);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddActionTransition(
            AnimatorStateMachine stateMachine,
            AnimatorState destination,
            string trigger)
        {
            AnimatorStateTransition transition =
                stateMachine.AddAnyStateTransition(destination);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        private static void AddReturnTransition(
            AnimatorState source,
            AnimatorState destination)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0.05f;
        }

        private static Transform RequireChild(Transform parent, string path)
        {
            Transform child = parent.Find(path);
            return child != null
                ? child
                : throw new InvalidOperationException(
                    $"Missing required authored path '{path}' under '{parent.name}'.");
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == name)
                    return candidate;
            return null;
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
                string normalizedPath,
                bool loop)
            {
                Label = label;
                HumanoidPath = humanoidPath;
                GenericPath = genericPath;
                NormalizedPath = normalizedPath;
                Loop = loop;
            }

            public string Label { get; }
            public string HumanoidPath { get; }
            public string GenericPath { get; }
            public string NormalizedPath { get; }
            public bool Loop { get; }
        }
    }
}
