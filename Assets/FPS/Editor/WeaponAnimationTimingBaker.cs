using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FPS.Editor
{
    public static class WeaponAnimationTimingBaker
    {
        private const string FpControllerPath =
            "Assets/FPS/Features/Weapons/Content/FirstPerson/FPAnim.controller";

        private readonly struct WeaponSpec
        {
            public readonly string Name;
            public readonly string DataPath;
            public readonly string GunControllerPath;

            public WeaponSpec(string name, string dataPath, string gunControllerPath)
            {
                Name = name;
                DataPath = dataPath;
                GunControllerPath = gunControllerPath;
            }
        }

        private static readonly WeaponSpec[] Specs =
        {
            new("Vandal", "Assets/FPS/Features/Weapons/Content/Vandal/Vandal.asset",
                "Assets/FPS/Features/Weapons/Content/Vandal/Animation/VandalAnim.controller"),
            new("Classic", "Assets/FPS/Features/Weapons/Content/Classic/Classic.asset",
                "Assets/FPS/Features/Weapons/Content/Classic/Animations/ClassicAnim.controller"),
            new("Operator", "Assets/FPS/Features/Weapons/Content/Operator/Operator.asset",
                "Assets/FPS/Features/Weapons/Content/Operator/Animation/Operator.controller"),
            new("Odin", "Assets/FPS/Features/Weapons/Content/Odin/Odin.asset",
                "Assets/FPS/Features/Weapons/Content/Odin/Animation/OdinAnim.controller"),
            new("Bucky", "Assets/FPS/Features/Weapons/Content/Bucky/Bucky.asset",
                "Assets/FPS/Features/Weapons/Content/Bucky/Animations/BuckyAnim.controller")
        };

        [MenuItem("FPS/Weapons/Bake Animation Timings")]
        public static void BakeFromMenu() => BakeAll(saveAssets: true);

        public static void BakeAll(bool saveAssets)
        {
            AnimatorController fpController = AssetDatabase.LoadAssetAtPath<AnimatorController>(FpControllerPath);
            if (fpController == null)
                throw new InvalidOperationException($"Missing first-person controller: {FpControllerPath}");

            foreach (WeaponSpec spec in Specs)
                Bake(spec, fpController);

            if (saveAssets)
                AssetDatabase.SaveAssets();
        }

        internal static bool TryGetGunState(
            WeaponData data,
            string stateName,
            out AnimatorController controller,
            out AnimatorState state)
        {
            controller = null;
            state = null;
            if (data == null || string.IsNullOrWhiteSpace(stateName))
                return false;

            if (!TryGetSpec(data, out WeaponSpec matchingSpec))
                return false;

            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                matchingSpec.GunControllerPath);
            if (controller == null || controller.layers.Length == 0)
                return false;

            state = controller.layers[0].stateMachine.states
                .Select(item => item.state)
                .FirstOrDefault(candidate => candidate.name == stateName);
            return state?.motion is AnimationClip;
        }

        internal static bool TryGetFirstPersonState(
            WeaponData data,
            string stateName,
            out AnimatorController controller,
            out AnimatorState state)
        {
            controller = null;
            state = null;
            if (data == null || string.IsNullOrWhiteSpace(stateName) ||
                !TryGetSpec(data, out WeaponSpec matchingSpec))
                return false;

            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(FpControllerPath);
            AnimatorControllerLayer layer = controller?.layers
                .FirstOrDefault(candidate => candidate.name == matchingSpec.Name);
            if (layer == null)
                return false;

            state = layer.stateMachine.states
                .Select(item => item.state)
                .FirstOrDefault(candidate => candidate.name == stateName);
            return state?.motion is AnimationClip;
        }

        private static bool TryGetSpec(WeaponData data, out WeaponSpec matchingSpec)
        {
            string dataPath = AssetDatabase.GetAssetPath(data);
            foreach (WeaponSpec spec in Specs)
            {
                if (!string.Equals(spec.DataPath, dataPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                matchingSpec = spec;
                return true;
            }

            matchingSpec = default;
            return false;
        }

        internal static bool IsTimingControllerPath(string path)
        {
            return string.Equals(path, FpControllerPath, StringComparison.OrdinalIgnoreCase)
                || Specs.Any(spec => string.Equals(
                    path, spec.GunControllerPath, StringComparison.OrdinalIgnoreCase));
        }

        private static void Bake(WeaponSpec spec, AnimatorController fpController)
        {
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(spec.DataPath);
            AnimatorController gun = AssetDatabase.LoadAssetAtPath<AnimatorController>(spec.GunControllerPath);
            if (data == null || gun == null)
                throw new InvalidOperationException($"Missing timing asset for {spec.Name}.");

            AnimatorControllerLayer fpLayer = fpController.layers.FirstOrDefault(layer => layer.name == spec.Name);
            if (fpLayer == null)
                throw new InvalidOperationException($"FPAnim layer '{spec.Name}' is missing.");

            SynchronizeSharedStateSpeeds(spec.Name, fpController, fpLayer.stateMachine,
                gun.layers[0].stateMachine);

            AnimatorState fpEquip = FindState(fpLayer.stateMachine, "Equip", spec.Name);
            AnimatorState fpFire = FindState(fpLayer.stateMachine, "Fire", spec.Name);
            AnimatorState fpReload = FindState(fpLayer.stateMachine, "Reload", spec.Name);
            AnimatorState gunEquip = FindState(gun.layers[0].stateMachine, "Equip", spec.Name);
            AnimatorState gunFire = FindState(gun.layers[0].stateMachine, "Fire", spec.Name);
            AnimatorState gunReload = FindState(gun.layers[0].stateMachine, "Reload", spec.Name);

            float equipDuration = MatchingDuration(spec.Name, "Equip", fpEquip, gunEquip);
            float reloadDuration = MatchingDuration(spec.Name, "Reload", fpReload, gunReload);
            float fireInterval;
            if (data.restartFireAnimationPerShot)
            {
                fireInterval = MatchingDuration(spec.Name, "Fire", fpFire, gunFire);
            }
            else
            {
                AnimationClip fireClip = RequireClip(gunFire, spec.Name, "Fire");
                int fireLastFrame = Mathf.RoundToInt(fireClip.length * fireClip.frameRate);
                ValidateFrameRange(
                    data.fireLoopStartFrame,
                    data.fireLoopEndFrame,
                    fireLastFrame,
                    spec.Name,
                    "fire");
                fireInterval = (data.fireLoopEndFrame - data.fireLoopStartFrame)
                    / (fireClip.frameRate * Mathf.Max(0.0001f, gunFire.speed));
            }
            AnimationClip reloadClip = RequireClip(gunReload, spec.Name, "Reload");
            float secondsPerSourceFrame = 1f / (reloadClip.frameRate * Mathf.Max(0.0001f, gunReload.speed));
            int lastFrame = Mathf.RoundToInt(reloadClip.length * reloadClip.frameRate);

            float ammoCommitDuration;
            float opening = 0f;
            float interval = 0f;
            float closing = 0f;
            if (data.reloadMode == ReloadMode.PerShell)
            {
                ValidateFrameRange(data.reloadLoopStartFrame, data.reloadLoopEndFrame, lastFrame, spec.Name, "reload");
                opening = data.reloadLoopStartFrame * secondsPerSourceFrame;
                interval = (data.reloadLoopEndFrame - data.reloadLoopStartFrame) * secondsPerSourceFrame;
                closing = (lastFrame - data.reloadLoopEndFrame) * secondsPerSourceFrame;
                ammoCommitDuration = opening + interval;
            }
            else
            {
                if (data.reloadAmmoCommitFrame < 0 || data.reloadAmmoCommitFrame > lastFrame)
                    throw new InvalidOperationException(
                        $"{spec.Name} Reload Ammo Commit Frame must be between 0 and {lastFrame}.");
                ammoCommitDuration = data.reloadAmmoCommitFrame * secondsPerSourceFrame;
            }

            data.ApplyBakedAnimationTimings(
                equipDuration, reloadDuration, ammoCommitDuration, opening, interval, closing);
            data.ApplyBakedFireInterval(fireInterval);
            EditorUtility.SetDirty(data);

            if (!data.restartFireAnimationPerShot)
            {
                bool controllerChanged = false;
                foreach (AnimatorStateTransition transition in gunFire.transitions.ToArray())
                {
                    gunFire.RemoveTransition(transition);
                    controllerChanged = true;
                }
                if (fpFire != null)
                {
                    foreach (AnimatorStateTransition transition in fpFire.transitions.ToArray())
                    {
                        fpFire.RemoveTransition(transition);
                        controllerChanged = true;
                    }
                }
                if (controllerChanged)
                {
                    EditorUtility.SetDirty(gun);
                    EditorUtility.SetDirty(fpController);
                }
            }
        }

        private static void SynchronizeSharedStateSpeeds(
            string weaponName,
            AnimatorController fpController,
            AnimatorStateMachine handsMachine,
            AnimatorStateMachine gunMachine)
        {
            bool changed = false;
            foreach (string stateName in new[] { "Idle", "Equip", "Fire", "Reload", "Inspect" })
            {
                AnimatorState hands = handsMachine.states.Select(item => item.state)
                    .FirstOrDefault(state => state.name == stateName);
                AnimatorState gun = gunMachine.states.Select(item => item.state)
                    .FirstOrDefault(state => state.name == stateName);
                if (hands?.motion is not AnimationClip handsClip || gun?.motion is not AnimationClip)
                    continue;

                float targetSpeed;
                if (weaponName == "Odin" && stateName == "Fire")
                {
                    // Continuous Odin Fire is sampled from the gun Animator in LateUpdate.
                    targetSpeed = 0f;
                }
                else
                {
                    float gunDuration = EffectiveDuration(gun, weaponName, stateName);
                    targetSpeed = handsClip.length / Mathf.Max(0.0001f, gunDuration);
                }

                if (Mathf.Abs(hands.speed - targetSpeed) <= 0.0001f)
                    continue;

                hands.speed = targetSpeed;
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(fpController);
        }

        private static float MatchingDuration(
            string weaponName, string stateName, AnimatorState hands, AnimatorState gun)
        {
            float handsDuration = EffectiveDuration(hands, weaponName, stateName);
            float gunDuration = EffectiveDuration(gun, weaponName, stateName);
            if (Mathf.Abs(handsDuration - gunDuration) > 0.01f)
                throw new InvalidOperationException(
                    $"{weaponName}/{stateName} timing mismatch: hands={handsDuration:F4}s, gun={gunDuration:F4}s.");
            return gunDuration;
        }

        private static float EffectiveDuration(AnimatorState state, string weaponName, string stateName)
        {
            AnimationClip clip = RequireClip(state, weaponName, stateName);
            if (state.speed <= 0f)
                throw new InvalidOperationException($"{weaponName}/{stateName} state speed must be greater than zero.");
            return clip.length / state.speed;
        }

        private static AnimationClip RequireClip(AnimatorState state, string weaponName, string stateName)
        {
            return state.motion as AnimationClip
                ?? throw new InvalidOperationException($"{weaponName}/{stateName} must use one AnimationClip.");
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name, string weaponName)
        {
            return machine.states.Select(item => item.state).FirstOrDefault(state => state.name == name)
                ?? throw new InvalidOperationException($"{weaponName}/{name} state is missing.");
        }

        private static void ValidateFrameRange(
            int start,
            int end,
            int lastFrame,
            string weaponName,
            string action)
        {
            if (start < 0 || end <= start || end > lastFrame)
                throw new InvalidOperationException(
                    $"{weaponName} {action} loop must satisfy 0 <= start < end <= {lastFrame}; got {start}..{end}.");
        }
    }

    internal sealed class WeaponAnimationTimingPostprocessor : AssetPostprocessor
    {
        private static bool bakeQueued;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (bakeQueued || !importedAssets.Any(WeaponAnimationTimingBaker.IsTimingControllerPath))
                return;

            bakeQueued = true;
            EditorApplication.delayCall += () =>
            {
                bakeQueued = false;
                if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                try
                {
                    WeaponAnimationTimingBaker.BakeAll(saveAssets: true);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[WeaponAnimationTimingBaker] {exception.Message}");
                }
            };
        }
    }
}
