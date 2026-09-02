using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FPS.EditorTools
{
    /// <summary>
    /// Runs a short, non-destructive preview of the authored Clove 3P body and
    /// gun animation pairs. All sampled properties are owned by AnimationMode
    /// and are restored when the preview finishes or is stopped.
    /// </summary>
    [InitializeOnLoad]
    public static class ThirdPersonWeaponPreviewRunner
    {
        private const string ClovePrefabPath =
            "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab";
        private const double SecondsPerAction = 0.7d;
        private const float TransformVectorTolerance = 0.0001f;
        private const float TransformRotationTolerance = 0.01f;
        private const string StatusKey = "FPS.3PPreview.Status";
        private const string CompletedStepsKey = "FPS.3PPreview.CompletedSteps";
        private const string FailureKey = "FPS.3PPreview.Failure";

        private static readonly PreviewStep[] PreviewSteps =
        {
            new("Vandal", "Hold", "Vandal Hold", "Idle"),
            new("Vandal", "Equip", "Vandal Equip", "Equip"),
            new("Vandal", "Fire", "Vandal Fire", "Fire"),
            new("Vandal", "Reload", "Vandal Reload", "Reload"),

            new("Classic", "Hold", "Classic Hold", "Idle"),
            new("Classic", "Equip", "Classic Equip", "Equip"),
            new("Classic", "Fire", "Classic Fire", "Fire"),
            new("Classic", "Reload", "Classic Reload", "Reload"),

            new("Operator", "Hold", "Operator Hold", "Idle"),
            new("Operator", "Aim", "Operator Aim", "Idle"),
            new("Operator", "Equip", "Operator Equip", "Equip"),
            new("Operator", "Fire", "Operator Fire", "Fire"),
            new("Operator", "Reload", "Operator Reload", "Reload"),

            new("Bucky", "Hold", "Bucky Hold", "Idle"),
            new("Bucky", "Equip", "Bucky Equip", "Equip"),
            new("Bucky", "Fire", "Bucky Fire", "Fire"),
            new("Bucky", "Reload", "Bucky Reload", "Reload"),

            new("Odin", "Hold", "Odin Hold", "Idle"),
            new("Odin", "Equip", "Odin Equip", "Equip"),
            new("Odin", "Fire", "Odin Fire", "Fire"),
            new("Odin", "Reload", "Odin Reload", "Reload")
        };

        private static PreviewSession session;

        static ThirdPersonWeaponPreviewRunner()
        {
            AssemblyReloadEvents.beforeAssemblyReload += StopBeforeAssemblyReload;
            EditorApplication.quitting += StopBeforeAssemblyReload;
        }

        [MenuItem("FPS/Animation/Preview/Run All Clove 3P Weapons %#g")]
        public static void RunAll()
        {
            StopInternal(saveAfterStop: false, logCompletion: false);
            SaveGate();

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.assetPath != ClovePrefabPath)
            {
                Debug.LogError(
                    $"[3P Preview] Open {ClovePrefabPath} in Prefab Mode first. "
                    + "The preview did not change the current scene or prefab.");
                return;
            }

            GameObject root = stage.prefabContentsRoot;
            PlayerVisibilityController visibility =
                root.GetComponent<PlayerVisibilityController>();
            PlayerMovement movement = root.GetComponent<PlayerMovement>();
            Animator body = movement != null ? movement.CharacterAnimation : null;
            if (!TryValidateSetup(visibility, body, out string validationError))
            {
                Debug.LogError("[3P Preview] " + validationError);
                return;
            }

            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();

            session = new PreviewSession(
                stage,
                root,
                visibility,
                body,
                Selection.activeObject);
            UnityEditor.SessionState.SetString(StatusKey, "Running");
            UnityEditor.SessionState.SetInt(CompletedStepsKey, 0);
            UnityEditor.SessionState.EraseString(FailureKey);

            try
            {
                AnimationMode.StartAnimationMode();
                session.StartStep(0);
                EditorApplication.update += Update;
                Debug.Log(
                    $"[3P Preview] Started {PreviewSteps.Length} paired body/gun "
                    + $"actions ({SecondsPerAction:0.0}s each). "
                    + "Use FPS/Animation/Preview/Stop to stop immediately.");
            }
            catch (Exception exception)
            {
                RecordFailure(exception);
                Debug.LogException(exception);
                StopInternal(saveAfterStop: true, logCompletion: false);
            }
        }

        [MenuItem("FPS/Animation/Preview/Stop %#h")]
        public static void Stop()
        {
            if (session != null
                && UnityEditor.SessionState.GetString(StatusKey, "") == "Running")
            {
                UnityEditor.SessionState.SetString(StatusKey, "Stopped");
            }
            StopInternal(saveAfterStop: true, logCompletion: true);
        }

        [MenuItem("FPS/Animation/Preview/Stop %#h", true)]
        private static bool ValidateStop()
        {
            return session != null;
        }

        private static void Update()
        {
            if (session == null)
                return;

            try
            {
                if (!session.IsStageStillValid())
                {
                    Debug.LogWarning(
                        "[3P Preview] Prefab Stage changed; preview was stopped and restored.");
                    UnityEditor.SessionState.SetString(StatusKey, "Stopped");
                    StopInternal(saveAfterStop: true, logCompletion: false);
                    return;
                }

                double elapsed = EditorApplication.timeSinceStartup
                    - session.StepStartedAt;
                float normalizedTime = Mathf.Clamp01(
                    (float)(elapsed / SecondsPerAction));
                session.SampleCurrentStep(normalizedTime);

                if (elapsed < SecondsPerAction)
                    return;

                int nextStep = session.StepIndex + 1;
                session.MarkCurrentStepComplete();
                if (nextStep >= PreviewSteps.Length)
                {
                    UnityEditor.SessionState.SetString(StatusKey, "Passed");
                    StopInternal(saveAfterStop: true, logCompletion: true);
                    return;
                }

                session.StartStep(nextStep);
            }
            catch (Exception exception)
            {
                RecordFailure(exception);
                Debug.LogException(exception);
                StopInternal(saveAfterStop: true, logCompletion: false);
            }
        }

        private static void StopBeforeAssemblyReload()
        {
            if (session != null)
                UnityEditor.SessionState.SetString(StatusKey, "Interrupted");
            StopInternal(saveAfterStop: true, logCompletion: false);
        }

        private static void StopInternal(bool saveAfterStop, bool logCompletion)
        {
            EditorApplication.update -= Update;
            PreviewSession previousSession = session;
            session = null;

            try
            {
                if (AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();

                previousSession?.RestoreEditorState();
            }
            finally
            {
                SceneView.RepaintAll();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                if (saveAfterStop)
                    SaveGate();
            }

            if (logCompletion && previousSession != null)
            {
                bool clean = previousSession.Stage == null
                    || !previousSession.Stage.scene.IsValid()
                    || !previousSession.Stage.scene.isDirty;
                string status = UnityEditor.SessionState.GetString(StatusKey, "Unknown");
                int completed = UnityEditor.SessionState.GetInt(CompletedStepsKey, 0);
                Debug.Log(
                    $"[3P Preview] {status}: {completed}/{PreviewSteps.Length} "
                    + "paired actions. Sampled poses were restored; "
                    + $"prefab stage clean={clean}.");
            }
        }

        private static void RecordFailure(Exception exception)
        {
            UnityEditor.SessionState.SetString(StatusKey, "Failed");
            UnityEditor.SessionState.SetString(FailureKey, exception.Message);
        }

        private static bool TryValidateSetup(
            PlayerVisibilityController visibility,
            Animator body,
            out string error)
        {
            if (visibility == null)
            {
                error = "ClovePlayer has no PlayerVisibilityController.";
                return false;
            }

            if (body == null)
            {
                error = "ClovePlayer has no authored 3P Body Animator reference.";
                return false;
            }

            ThirdPersonWeaponPresentation[] presentations =
                visibility.ThirdPersonWeaponPresentations;
            if (presentations == null)
            {
                error = "Third-person weapon presentations are missing.";
                return false;
            }

            foreach (string weaponName in PreviewSteps
                         .Select(step => step.WeaponName)
                         .Distinct())
            {
                ThirdPersonWeaponPresentation presentation = presentations
                    .SingleOrDefault(item => item?.WeaponData != null
                        && item.WeaponData.name == weaponName);
                if (presentation == null
                    || presentation.WeaponObject == null
                    || presentation.CharacterController == null)
                {
                    error = $"{weaponName} presentation is incomplete.";
                    return false;
                }

                Animator[] gunAnimators = presentation.WeaponObject
                    .GetComponentsInChildren<Animator>(true);
                if (gunAnimators.Length != 1
                    || gunAnimators[0].runtimeAnimatorController == null)
                {
                    error = $"{weaponName} must have exactly one authored gun Animator/controller.";
                    return false;
                }

                foreach (PreviewStep step in PreviewSteps.Where(item =>
                             item.WeaponName == weaponName))
                {
                    if (!HasState(presentation.CharacterController, step.BodyState))
                    {
                        error = $"{weaponName} body state '{step.BodyState}' is missing.";
                        return false;
                    }

                    if (!HasState(
                            gunAnimators[0].runtimeAnimatorController,
                            step.GunState))
                    {
                        error = $"{weaponName} gun state '{step.GunState}' is missing.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private static bool HasState(
            RuntimeAnimatorController controller,
            string stateName)
        {
            if (controller is not UnityEditor.Animations.AnimatorController authored)
                return false;

            return authored.layers.Any(layer =>
                layer.stateMachine.states.Any(child => child.state.name == stateName));
        }

        private static int FindStateLayer(Animator animator, string stateName)
        {
            int hash = Animator.StringToHash(stateName);
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                if (animator.HasState(layer, hash))
                    return layer;
            }

            return -1;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x)
                && float.IsFinite(value.y)
                && float.IsFinite(value.z);
        }

        private static void SaveGate()
        {
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        private sealed class PreviewSession
        {
            private readonly GameObject root;
            private readonly PlayerVisibilityController visibility;
            private readonly Animator body;
            private readonly Object originalSelection;
            private readonly AnimatorState bodyOriginalState;
            private readonly Dictionary<GameObject, bool> weaponActiveStates;
            private readonly Dictionary<Renderer, bool> rendererEnabledStates;
            private readonly Dictionary<Animator, AnimatorState> animatorStates;
            private readonly Dictionary<Transform, TransformState> transformStates;
            private readonly Dictionary<string, ThirdPersonWeaponPresentation>
                presentations;

            private Animator currentGun;
            private TransformState currentGunRootState;

            public PreviewSession(
                PrefabStage stage,
                GameObject root,
                PlayerVisibilityController visibility,
                Animator body,
                Object originalSelection)
            {
                Stage = stage;
                this.root = root;
                this.visibility = visibility;
                this.body = body;
                this.originalSelection = originalSelection;
                bodyOriginalState = new AnimatorState(body);
                presentations = visibility.ThirdPersonWeaponPresentations
                    .ToDictionary(item => item.WeaponData.name);
                weaponActiveStates = presentations.Values.ToDictionary(
                    item => item.WeaponObject,
                    item => item.WeaponObject.activeSelf);
                rendererEnabledStates = root
                    .GetComponentsInChildren<Renderer>(true)
                    .ToDictionary(item => item, item => item.enabled);
                animatorStates = root
                    .GetComponentsInChildren<Animator>(true)
                    .ToDictionary(item => item, item => new AnimatorState(item));
                transformStates = root
                    .GetComponentsInChildren<Transform>(true)
                    .ToDictionary(item => item, item => new TransformState(item));
            }

            public PrefabStage Stage { get; }
            public int StepIndex { get; private set; }
            public double StepStartedAt { get; private set; }

            public bool IsStageStillValid()
            {
                return Stage != null
                    && Stage.scene.IsValid()
                    && PrefabStageUtility.GetCurrentPrefabStage() == Stage
                    && Stage.prefabContentsRoot == root;
            }

            public void MarkCurrentStepComplete()
            {
                UnityEditor.SessionState.SetInt(CompletedStepsKey, StepIndex + 1);
            }

            public void StartStep(int index)
            {
                StepIndex = index;
                StepStartedAt = EditorApplication.timeSinceStartup;
                PreviewStep step = PreviewSteps[index];
                ThirdPersonWeaponPresentation presentation =
                    presentations[step.WeaponName];

                foreach (ThirdPersonWeaponPresentation candidate in presentations.Values)
                    candidate.WeaponObject.SetActive(candidate == presentation);

                body.enabled = true;
                body.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                body.runtimeAnimatorController = presentation.CharacterController;
                switch (presentation.CharacterRigMode)
                {
                    case ThirdPersonCharacterRigMode.AuthoredAvatar:
                        body.avatar = presentation.CharacterAvatar;
                        break;
                    case ThirdPersonCharacterRigMode.GenericPathBound:
                        body.avatar = null;
                        break;
                }

                body.Rebind();
                body.Update(0f);

                currentGun = presentation.WeaponObject
                    .GetComponentsInChildren<Animator>(true)
                    .Single();
                currentGun.enabled = true;
                currentGun.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                currentGun.Rebind();
                currentGun.Update(0f);
                // Body profile rebinding can establish a weapon-specific
                // parent pose before the gun Animator is sampled. Guard the
                // gun animation against changing that established root; the
                // session-wide snapshot remains authoritative for final
                // restoration.
                currentGunRootState = new TransformState(currentGun.transform);

                foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = true;
                foreach (Renderer renderer in presentation.WeaponObject
                             .GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = true;

                Selection.activeGameObject = presentation.WeaponObject;
                Debug.Log(
                    $"[3P Preview] {index + 1:00}/{PreviewSteps.Length:00} "
                    + $"{step.WeaponName} / {step.ActionName} "
                    + $"(Body='{step.BodyState}', Gun='{step.GunState}')");
                SampleCurrentStep(0f);
            }

            public void SampleCurrentStep(float normalizedTime)
            {
                PreviewStep step = PreviewSteps[StepIndex];
                int bodyActionLayer = FindStateLayer(body, step.BodyState);
                int gunActionLayer = FindStateLayer(currentGun, step.GunState);
                if (bodyActionLayer < 0 || gunActionLayer < 0)
                {
                    throw new InvalidOperationException(
                        $"Missing runtime preview state for {step.WeaponName}/{step.ActionName}.");
                }

                AnimationMode.BeginSampling();
                try
                {
                    PlayIfPresent(body, "Locomotion", 0f);
                    PlayIfPresent(body, step.WeaponName + " Hold", 0f);
                    PlayIfPresent(body, "Locomotion Add", 0f);
                    PlayIfPresent(body, "Zero", 0f);
                    PlayIfPresent(body, step.WeaponName + " Finger Hold", 0f);

                    body.SetLayerWeight(bodyActionLayer, 1f);
                    body.Play(
                        Animator.StringToHash(step.BodyState),
                        bodyActionLayer,
                        normalizedTime);
                    currentGun.SetLayerWeight(gunActionLayer, 1f);
                    currentGun.Play(
                        Animator.StringToHash(step.GunState),
                        gunActionLayer,
                        normalizedTime);
                    body.Update(0f);
                    currentGun.Update(0f);
                }
                finally
                {
                    AnimationMode.EndSampling();
                }

                ValidateGunRoot(step);
                SceneView.RepaintAll();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }

            public void RestoreEditorState()
            {
                // Animator.Rebind cannot restore inactive weapon descendants.
                // Temporarily activate every presentation, restore Animator
                // configuration, then restore every captured Transform after
                // Rebind has finished. Active states are restored last.
                foreach (GameObject weapon in weaponActiveStates.Keys)
                {
                    if (weapon != null)
                        weapon.SetActive(true);
                }

                foreach ((Animator animator, AnimatorState state) in animatorStates)
                {
                    if (animator != null)
                        state.Restore(animator);
                }

                if (body != null)
                    bodyOriginalState.Restore(body);

                foreach ((Transform transform, TransformState state) in
                         transformStates)
                {
                    if (transform != null)
                        state.Restore(transform);
                }

                foreach ((Renderer renderer, bool enabled) in rendererEnabledStates)
                {
                    if (renderer != null)
                        renderer.enabled = enabled;
                }

                foreach ((GameObject weapon, bool active) in weaponActiveStates)
                {
                    if (weapon != null)
                        weapon.SetActive(active);
                }

                foreach ((Transform transform, TransformState state) in
                         transformStates)
                {
                    if (transform != null
                        && !state.ApproximatelyMatches(
                            transform,
                            TransformVectorTolerance,
                            TransformRotationTolerance))
                    {
                        throw new InvalidOperationException(
                            $"Preview restoration failed for '{transform.name}'.");
                    }
                }

                if (originalSelection != null)
                    Selection.activeObject = originalSelection;
            }

            private void ValidateGunRoot(PreviewStep step)
            {
                Transform transform = currentGun.transform;
                Vector3 scale = transform.localScale;
                if (!IsFinite(transform.localPosition)
                    || !IsFinite(scale)
                    || scale.x <= 0f
                    || scale.y <= 0f
                    || scale.z <= 0f)
                {
                    throw new InvalidOperationException(
                        $"{step.WeaponName}/{step.ActionName} produced a non-finite "
                        + "or collapsed gun root transform.");
                }

                if (!currentGunRootState.ApproximatelyMatches(
                        transform,
                        TransformVectorTolerance,
                        TransformRotationTolerance))
                {
                    throw new InvalidOperationException(
                        $"{step.WeaponName}/{step.ActionName} changed the authored "
                        + "gun Animator-root local transform. Preview stopped to "
                        + "avoid hiding a GNTP root-curve regression.");
                }
            }

            private static void PlayIfPresent(
                Animator animator,
                string stateName,
                float normalizedTime)
            {
                int layer = FindStateLayer(animator, stateName);
                if (layer < 0)
                    return;

                animator.SetLayerWeight(layer, 1f);
                animator.Play(
                    Animator.StringToHash(stateName),
                    layer,
                    normalizedTime);
            }
        }

        private readonly struct PreviewStep
        {
            public PreviewStep(
                string weaponName,
                string actionName,
                string bodyState,
                string gunState)
            {
                WeaponName = weaponName;
                ActionName = actionName;
                BodyState = bodyState;
                GunState = gunState;
            }

            public string WeaponName { get; }
            public string ActionName { get; }
            public string BodyState { get; }
            public string GunState { get; }
        }

        private readonly struct TransformState
        {
            private readonly Vector3 localPosition;
            private readonly Quaternion localRotation;
            private readonly Vector3 localScale;

            public TransformState(Transform transform)
            {
                localPosition = transform.localPosition;
                localRotation = transform.localRotation;
                localScale = transform.localScale;
            }

            public bool ApproximatelyMatches(
                Transform transform,
                float vectorTolerance,
                float rotationTolerance)
            {
                return Vector3.Distance(localPosition, transform.localPosition)
                        <= vectorTolerance
                    && Quaternion.Angle(localRotation, transform.localRotation)
                        <= rotationTolerance
                    && Vector3.Distance(localScale, transform.localScale)
                        <= vectorTolerance;
            }

            public void Restore(Transform transform)
            {
                transform.localPosition = localPosition;
                transform.localRotation = localRotation;
                transform.localScale = localScale;
            }
        }

        private readonly struct AnimatorState
        {
            private readonly bool enabled;
            private readonly AnimatorCullingMode cullingMode;
            private readonly RuntimeAnimatorController controller;
            private readonly Avatar avatar;
            private readonly float[] layerWeights;
            private readonly bool capturedLayerWeights;

            public AnimatorState(Animator animator)
            {
                enabled = animator.enabled;
                cullingMode = animator.cullingMode;
                controller = animator.runtimeAnimatorController;
                avatar = animator.avatar;
                capturedLayerWeights = animator.enabled
                    && animator.gameObject.activeInHierarchy
                    && controller != null;
                layerWeights = capturedLayerWeights
                    ? Enumerable.Range(0, animator.layerCount)
                        .Select(animator.GetLayerWeight)
                        .ToArray()
                    : Array.Empty<float>();
            }

            public void Restore(Animator animator)
            {
                animator.runtimeAnimatorController = controller;
                animator.avatar = avatar;
                animator.cullingMode = cullingMode;
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
                for (int layer = 0;
                     capturedLayerWeights
                     && layer < layerWeights.Length
                     && layer < animator.layerCount;
                     layer++)
                {
                    animator.SetLayerWeight(layer, layerWeights[layer]);
                }

                animator.enabled = enabled;
            }
        }
    }
}
