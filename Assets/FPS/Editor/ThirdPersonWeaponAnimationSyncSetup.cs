using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FPS.Editor
{
    /// <summary>
    /// Keeps the authored third-person character and gun controllers on one
    /// gameplay clock.  The character and gun remain separate Animators, but
    /// their action states use the same effective duration and completion edge.
    /// </summary>
    public static class ThirdPersonWeaponAnimationSyncSetup
    {
        private const string MenuRoot = "FPS/Third Person/Animation Sync/";
        private static readonly string[] PlayerPrefabPaths =
        {
            "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Sage/SagePlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Gekko/GekkoPlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Brimstone/BrimstonePlayer.prefab"
        };
        private const string UpperBodyLayer = "Upper Body Gun Pose";
        private const string GunLayer = "Base Layer";
        private const float CompletionBlendSeconds = 0.05f;
        private const float AdditiveFireBlendSeconds = 0.04f;

        [MenuItem(MenuRoot + "Repair All Authored Weapon Controllers")]
        public static void RepairAll()
        {
            SaveAllOrThrow("before third-person animation synchronization");
            ApplyToAuthoredControllers();
            SaveAllOrThrow("after third-person animation synchronization");
            ValidateAuthoredControllers();
            Debug.Log(
                "[3PAnimationSync] Reload and Equip now wait for shared "
                + "completion signals. Fire playback is duration-matched for "
                + "all five weapons on every authored player prefab.");
        }

        [MenuItem(MenuRoot + "Validate All Authored Weapon Controllers")]
        public static void Validate()
        {
            SaveAllOrThrow("before third-person animation sync validation");
            ValidateAuthoredControllers();
            SaveAllOrThrow("after third-person animation sync validation");
            Debug.Log("[3PAnimationSync] Validation passed for all authored player weapons.");
        }

        internal static void ApplyToAuthoredControllers()
        {
            foreach (string prefabPath in PlayerPrefabPaths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    PlayerVisibilityController visibility =
                        root.GetComponent<PlayerVisibilityController>();
                    if (visibility == null)
                    {
                        throw new InvalidOperationException(
                            $"{prefabPath} has no PlayerVisibilityController.");
                    }

                    foreach (ThirdPersonWeaponPresentation presentation in
                             visibility.ThirdPersonWeaponPresentations)
                    {
                        if (presentation?.WeaponData == null
                            || presentation.WeaponObject == null)
                        {
                            continue;
                        }

                        ConfigurePresentation(presentation);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        internal static void ValidateAuthoredControllers()
        {
            foreach (string prefabPath in PlayerPrefabPaths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    PlayerVisibilityController visibility =
                        root.GetComponent<PlayerVisibilityController>();
                    if (visibility == null)
                    {
                        throw new InvalidOperationException(
                            $"{prefabPath} has no PlayerVisibilityController.");
                    }

                    foreach (ThirdPersonWeaponPresentation presentation in
                             visibility.ThirdPersonWeaponPresentations)
                    {
                        if (presentation?.WeaponData == null
                            || presentation.WeaponObject == null)
                        {
                            continue;
                        }

                        ValidatePresentation(presentation);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ConfigurePresentation(
            ThirdPersonWeaponPresentation presentation)
        {
            WeaponData data = presentation.WeaponData;
            string weapon = data.name;
            AnimatorController body = RequireController(
                presentation.CharacterController,
                weapon + " Body");
            Animator gunAnimator = presentation.WeaponObject
                .GetComponentInChildren<Animator>(true);
            AnimatorController gun = RequireController(
                gunAnimator != null
                    ? gunAnimator.runtimeAnimatorController
                    : null,
                weapon + " gun");

            ConfigureCompletionAction(
                body,
                UpperBodyLayer,
                weapon + " Reload",
                weapon + " Hold",
                "ReloadComplete",
                "ReloadPlaybackSpeed",
                data.ReloadDuration);
            ConfigureCompletionAction(
                gun,
                GunLayer,
                "Reload",
                "Idle",
                "ReloadComplete",
                "ReloadPlaybackSpeed",
                data.ReloadDuration);

            ConfigureCompletionAction(
                body,
                UpperBodyLayer,
                weapon + " Equip",
                weapon + " Hold",
                "EquipComplete",
                "EquipPlaybackSpeed",
                data.EquipDuration);
            ConfigureCompletionAction(
                gun,
                GunLayer,
                "Equip",
                "Idle",
                "EquipComplete",
                "EquipPlaybackSpeed",
                data.EquipDuration);

            StateLocation bodyFire = RequireState(body, weapon + " Fire");
            bool additiveFire = body.layers[bodyFire.Layer].name.IndexOf(
                    "Fire Additive",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            float fireBlend = additiveFire ? AdditiveFireBlendSeconds : 0f;
            ConfigureTimedFire(
                body,
                bodyFire,
                data.FireInterval,
                fireBlend);
            ConfigureTimedFire(
                gun,
                RequireState(gun, "Fire"),
                data.FireInterval,
                fireBlend);

            EditorUtility.SetDirty(body);
            EditorUtility.SetDirty(gun);
        }

        private static void ConfigureCompletionAction(
            AnimatorController controller,
            string layerName,
            string stateName,
            string destinationName,
            string completionParameter,
            string playbackParameter,
            float duration)
        {
            EnsureParameter(
                controller,
                completionParameter,
                AnimatorControllerParameterType.Trigger,
                0f);
            EnsureParameter(
                controller,
                playbackParameter,
                AnimatorControllerParameterType.Float,
                1f);

            AnimatorState state = RequireDirectState(
                controller,
                layerName,
                stateName);
            AnimatorState destination = RequireDirectState(
                controller,
                layerName,
                destinationName);
            AnimationClip clip = RequireClip(state, controller.name + "/" + stateName);
            state.speed = clip.length / Mathf.Max(0.0001f, duration);
            state.speedParameter = playbackParameter;
            state.speedParameterActive = true;

            AnimatorStateTransition transition = state.transitions
                .Single(candidate => candidate.destinationState == destination);
            foreach (AnimatorCondition condition in transition.conditions)
                transition.RemoveCondition(condition);
            transition.hasExitTime = false;
            transition.exitTime = 0f;
            transition.hasFixedDuration = true;
            transition.duration = CompletionBlendSeconds;
            transition.offset = 0f;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.orderedInterruption = true;
            transition.canTransitionToSelf = false;
            transition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                completionParameter);

            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(transition);
        }

        private static void ConfigureTimedFire(
            AnimatorController controller,
            StateLocation location,
            float duration,
            float blendSeconds)
        {
            AnimatorState state = location.State;
            AnimationClip clip = RequireClip(
                state,
                controller.name + "/" + state.name);
            state.speed = clip.length / Mathf.Max(0.0001f, duration);
            state.speedParameterActive = false;

            // Some legacy third-person Odin controllers kept Fire latched
            // forever because the first-person continuous-fire policy had
            // leaked into the 3P controller.  Third person is driven once per
            // accepted shot, so author the same timed Fire -> Idle edge used
            // by the other gun presentations.
            if (state.transitions.Length == 0)
            {
                AnimatorState idle = RequireDirectState(
                    controller,
                    controller.layers[location.Layer].name,
                    "Idle");
                state.AddTransition(idle);
            }

            if (state.transitions.Length != 1)
            {
                throw new InvalidOperationException(
                    $"{controller.name}/{state.name} must have exactly one exit transition.");
            }

            AnimatorStateTransition transition = state.transitions[0];
            foreach (AnimatorCondition condition in transition.conditions)
                transition.RemoveCondition(condition);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.hasFixedDuration = true;
            transition.duration = blendSeconds;
            transition.offset = 0f;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.orderedInterruption = true;
            transition.canTransitionToSelf = false;

            ConfigureFireEntryForRestart(controller, location, state);

            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(transition);
        }

        private static void ConfigureFireEntryForRestart(
            AnimatorController controller,
            StateLocation location,
            AnimatorState state)
        {
            AnimatorStateMachine machine = controller.layers[location.Layer]
                .stateMachine;
            foreach (AnimatorStateTransition entry in machine.anyStateTransitions
                         .Where(candidate => candidate.destinationState == state))
            {
                entry.canTransitionToSelf = true;
                EditorUtility.SetDirty(entry);
            }
        }

        private static void ValidatePresentation(
            ThirdPersonWeaponPresentation presentation)
        {
            WeaponData data = presentation.WeaponData;
            string weapon = data.name;
            AnimatorController body = RequireController(
                presentation.CharacterController,
                weapon + " Body");
            Animator gunAnimator = presentation.WeaponObject
                .GetComponentInChildren<Animator>(true);
            AnimatorController gun = RequireController(
                gunAnimator != null
                    ? gunAnimator.runtimeAnimatorController
                    : null,
                weapon + " gun");

            ValidateCompletionAction(
                body,
                UpperBodyLayer,
                weapon + " Reload",
                weapon + " Hold",
                "ReloadComplete",
                "ReloadPlaybackSpeed",
                data.ReloadDuration);
            ValidateCompletionAction(
                gun,
                GunLayer,
                "Reload",
                "Idle",
                "ReloadComplete",
                "ReloadPlaybackSpeed",
                data.ReloadDuration);
            ValidateCompletionAction(
                body,
                UpperBodyLayer,
                weapon + " Equip",
                weapon + " Hold",
                "EquipComplete",
                "EquipPlaybackSpeed",
                data.EquipDuration);
            ValidateCompletionAction(
                gun,
                GunLayer,
                "Equip",
                "Idle",
                "EquipComplete",
                "EquipPlaybackSpeed",
                data.EquipDuration);

            ValidateFireDuration(
                body,
                RequireState(body, weapon + " Fire"),
                data.FireInterval);
            ValidateFireDuration(
                gun,
                RequireState(gun, "Fire"),
                data.FireInterval);
        }

        private static void ValidateCompletionAction(
            AnimatorController controller,
            string layerName,
            string stateName,
            string destinationName,
            string completionParameter,
            string playbackParameter,
            float expectedDuration)
        {
            RequireParameter(
                controller,
                completionParameter,
                AnimatorControllerParameterType.Trigger);
            RequireParameter(
                controller,
                playbackParameter,
                AnimatorControllerParameterType.Float);
            AnimatorState state = RequireDirectState(
                controller,
                layerName,
                stateName);
            AnimatorState destination = RequireDirectState(
                controller,
                layerName,
                destinationName);
            AnimationClip clip = RequireClip(state, controller.name + "/" + stateName);
            AssertDuration(controller, state, clip, expectedDuration);
            if (!state.speedParameterActive
                || state.speedParameter != playbackParameter)
            {
                throw new InvalidOperationException(
                    $"{controller.name}/{stateName} does not use {playbackParameter}.");
            }

            AnimatorStateTransition transition = state.transitions
                .Single(candidate => candidate.destinationState == destination);
            if (transition.hasExitTime
                || !transition.hasFixedDuration
                || Mathf.Abs(transition.duration - CompletionBlendSeconds) > 0.0001f
                || transition.conditions.Length != 1
                || transition.conditions[0].parameter != completionParameter
                || transition.conditions[0].mode != AnimatorConditionMode.If)
            {
                throw new InvalidOperationException(
                    $"{controller.name}/{stateName} does not wait for {completionParameter}.");
            }
        }

        private static void ValidateFireDuration(
            AnimatorController controller,
            StateLocation location,
            float expectedDuration)
        {
            AnimatorState state = location.State;
            AnimationClip clip = RequireClip(state, controller.name + "/" + state.name);
            AssertDuration(controller, state, clip, expectedDuration);
            if (state.speedParameterActive
                || state.transitions.Length != 1
                || !state.transitions[0].hasExitTime
                || Mathf.Abs(state.transitions[0].exitTime - 1f) > 0.0001f)
            {
                throw new InvalidOperationException(
                    $"{controller.name}/{state.name} does not use the synchronized fire policy.");
            }
        }

        private static void AssertDuration(
            AnimatorController controller,
            AnimatorState state,
            AnimationClip clip,
            float expectedDuration)
        {
            float effective = clip.length / Mathf.Max(0.0001f, state.speed);
            if (Mathf.Abs(effective - expectedDuration) > 0.002f)
            {
                throw new InvalidOperationException(
                    $"{controller.name}/{state.name} lasts {effective:F4}s; "
                    + $"expected {expectedDuration:F4}s.");
            }
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type,
            float defaultFloat)
        {
            AnimatorControllerParameter parameter = controller.parameters
                .SingleOrDefault(candidate => candidate.name == name);
            bool wrongType = parameter != null && parameter.type != type;
            bool wrongFloatDefault = parameter != null
                && type == AnimatorControllerParameterType.Float
                && Mathf.Abs(parameter.defaultFloat - defaultFloat) > 0.0001f;
            if (wrongType || wrongFloatDefault)
            {
                controller.RemoveParameter(parameter);
                parameter = null;
            }

            if (parameter == null)
            {
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = name,
                    type = type,
                    defaultFloat = defaultFloat
                });
            }
        }

        private static void RequireParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            if (!controller.parameters.Any(parameter =>
                    parameter.name == name && parameter.type == type))
            {
                throw new InvalidOperationException(
                    $"{controller.name} has no {type} parameter {name}.");
            }
        }

        private static AnimatorController RequireController(
            RuntimeAnimatorController runtimeController,
            string label)
        {
            RuntimeAnimatorController current = runtimeController;
            while (current is AnimatorOverrideController overrides)
                current = overrides.runtimeAnimatorController;
            return current as AnimatorController
                ?? throw new InvalidOperationException(
                    $"{label} has no editable AnimatorController.");
        }

        private static AnimatorState RequireDirectState(
            AnimatorController controller,
            string layerName,
            string stateName)
        {
            AnimatorControllerLayer layer = controller.layers.Single(candidate =>
                candidate.name == layerName);
            return layer.stateMachine.states
                .Select(child => child.state)
                .Single(state => state.name == stateName);
        }

        private static StateLocation RequireState(
            AnimatorController controller,
            string stateName)
        {
            StateLocation[] matches = controller.layers
                .SelectMany((layer, index) => layer.stateMachine.states
                    .Where(child => child.state.name == stateName)
                    .Select(child => new StateLocation(index, child.state)))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"{controller.name} has {matches.Length} direct states named {stateName}.");
            }
            return matches[0];
        }

        private static AnimationClip RequireClip(AnimatorState state, string label)
        {
            return state.motion as AnimationClip
                ?? throw new InvalidOperationException(
                    $"{label} does not use a direct AnimationClip.");
        }

        private static void SaveAllOrThrow(string phase)
        {
            if (!EditorSceneManager.SaveOpenScenes())
                throw new InvalidOperationException(
                    $"Open scenes could not be saved {phase}.");
            AssetDatabase.SaveAssets();
        }

        private readonly struct StateLocation
        {
            public StateLocation(int layer, AnimatorState state)
            {
                Layer = layer;
                State = state;
            }

            public int Layer { get; }
            public AnimatorState State { get; }
        }
    }
}
