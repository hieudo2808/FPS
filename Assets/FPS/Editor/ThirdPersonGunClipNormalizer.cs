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
    /// Creates weapon-specific copies of imported GNTP gun clips whose root
    /// Transform curves match the authored gun prefab. Child translations are
    /// converted into that root's scale space so moving magazines, shells and
    /// other weapon bones keep their original world-space travel. Imported
    /// FBXs and gun prefab Transforms are treated as read-only source data.
    /// </summary>
    public static class ThirdPersonGunClipNormalizer
    {
        private const string MenuRoot =
            "FPS/Third Person/GNTP Gun Clips/";
        private const string OutputRoot =
            "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/NormalizedGunClips";
        private const string NormalizedSuffix = "_AuthoredRoot.anim";

        private static readonly WeaponProfile[] Profiles =
        {
            new WeaponProfile(
                "Vandal",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Vandal/Vandal3P.prefab",
                "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/CloveVandal3P_GNTP.controller",
                normalize: true),
            new WeaponProfile(
                "Vandal",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Vandal/Vandal3P.prefab",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Vandal/Vandal3P_Gun.controller",
                normalize: true),
            new WeaponProfile(
                "Classic",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Classic/Classic3P.prefab",
                "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/CloveClassic3P_GNTP.controller",
                normalize: true),
            new WeaponProfile(
                "Classic",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Classic/Classic3P.prefab",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Classic/Classic3P_Gun.controller",
                normalize: true),
            new WeaponProfile(
                "Operator",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/Operator3P.prefab",
                "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/CloveOperator3P_GNTP.controller",
                normalize: true),
            new WeaponProfile(
                "Operator",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/Operator3P.prefab",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/Operator3P_Gun.controller",
                normalize: true),
            new WeaponProfile(
                "Bucky",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Bucky/Bucky3P.prefab",
                "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/CloveBucky3P_GNTP.controller",
                normalize: true),
            new WeaponProfile(
                "Bucky",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Bucky/Bucky3P.prefab",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Bucky/Bucky3P_Gun.controller",
                normalize: true),
            // No hierarchy-compatible Odin GNTP gun clip exists in the project.
            // Its controller intentionally keeps the authored-scale neutral pose.
            new WeaponProfile(
                "Odin",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Odin/Odin3P.prefab",
                "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/CloveOdin3P_ThirdPersonGun.controller",
                normalize: false)
        };

        [MenuItem(MenuRoot + "Normalize And Apply Production Clips")]
        public static void NormalizeAndApply()
        {
            SaveAllOrThrow("before GNTP gun clip normalization");
            StopAnimationPreview();
            EnsureFolder(OutputRoot);

            foreach (WeaponProfile profile in Profiles.Where(item => item.Normalize))
                NormalizeProfile(profile);

            AssetDatabase.SaveAssets();
            ForceReloadProductionAssets();
            ValidateInternal();
            SaveAllOrThrow("after GNTP gun clip normalization");
            Debug.Log(
                "[ThirdPersonGunClips] Production GNTP clips now preserve each "
                + "gun Animator's authored root position, rotation and scale "
                + "for both Generic Clove and shared Humanoid presentations. "
                + "Source FBXs and prefab Transforms were not modified.");
        }

        [MenuItem(MenuRoot + "Validate Production Clips")]
        public static void Validate()
        {
            SaveAllOrThrow("before GNTP gun clip validation");
            ValidateInternal();
            SaveAllOrThrow("after GNTP gun clip validation");
            Debug.Log("[ThirdPersonGunClips] Production clip validation passed.");
        }

        private static void NormalizeProfile(WeaponProfile profile)
        {
            Animator animator = RequireGunAnimator(profile);
            AnimatorController controller = RequireController(profile.ControllerPath);
            EnsureFolder(profile.OutputFolder);

            AnimationClip[] currentClips = controller.animationClips
                .Where(clip => clip != null)
                .Distinct()
                .ToArray();
            if (currentClips.Length == 0)
                throw new InvalidOperationException(
                    $"{profile.ControllerPath} has no AnimationClip motions.");

            var replacements = new Dictionary<AnimationClip, AnimationClip>();
            foreach (AnimationClip currentClip in currentClips)
            {
                AnimationClip sourceClip = ResolveOriginalSource(currentClip);
                AnimationClip normalized = CreateOrUpdateNormalizedClip(
                    profile,
                    animator.transform,
                    sourceClip);
                replacements.Add(currentClip, normalized);
            }

            foreach (AnimatorControllerLayer layer in controller.layers)
                ReplaceStateMachineMotions(layer.stateMachine, replacements);

            EditorUtility.SetDirty(controller);
        }

        private static AnimationClip CreateOrUpdateNormalizedClip(
            WeaponProfile profile,
            Transform animatorRoot,
            AnimationClip source)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(sourceGuid))
                throw new InvalidOperationException(
                    $"Cannot resolve the source asset for {source?.name ?? "null"}.");

            string destinationPath = profile.OutputFolder + "/"
                + MakeSafeName(source.name) + "__" + sourceGuid + NormalizedSuffix;
            var clean = new AnimationClip
            {
                frameRate = source.frameRate,
                legacy = source.legacy,
                wrapMode = source.wrapMode,
                name = Path.GetFileNameWithoutExtension(destinationPath)
            };
            AnimationUtility.SetAnimationClipSettings(
                clean,
                AnimationUtility.GetAnimationClipSettings(source));
            AnimationUtility.SetAnimationEvents(
                clean,
                AnimationUtility.GetAnimationEvents(source));

            int keptChildTransformCurves = 0;
            int droppedBindings = 0;
            Vector3 childTranslationScale = CalculateChildTranslationScale(
                source,
                animatorRoot.localScale);
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                if (binding.type == typeof(Transform))
                {
                    if (string.IsNullOrEmpty(binding.path))
                        continue;
                    if (animatorRoot.Find(binding.path) == null)
                    {
                        droppedBindings++;
                        continue;
                    }
                    keptChildTransformCurves++;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                if (curve != null
                    && binding.type == typeof(Transform)
                    && !string.IsNullOrEmpty(binding.path)
                    && TryGetLocalPositionAxis(binding.propertyName, out int axis))
                {
                    curve = ScaleCurve(curve, childTranslationScale[axis]);
                }
                if (curve != null)
                    AnimationUtility.SetEditorCurve(clean, binding, curve);
            }

            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetObjectReferenceCurveBindings(source))
            {
                if (!string.IsNullOrEmpty(binding.path)
                    && animatorRoot.Find(binding.path) == null)
                {
                    droppedBindings++;
                    continue;
                }

                ObjectReferenceKeyframe[] keys =
                    AnimationUtility.GetObjectReferenceCurve(source, binding);
                AnimationUtility.SetObjectReferenceCurve(clean, binding, keys);
            }

            float duration = Mathf.Max(
                source.length,
                1f / Mathf.Max(1f, source.frameRate));
            WriteAuthoredRootCurves(clean, animatorRoot, duration);

            AnimationClip destination =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(destinationPath);
            if (destination == null)
            {
                AssetDatabase.CreateAsset(clean, destinationPath);
                destination = clean;
            }
            else
            {
                EditorUtility.CopySerialized(clean, destination);
                UnityEngine.Object.DestroyImmediate(clean);
                EditorUtility.SetDirty(destination);
            }

            Debug.Log(
                $"[ThirdPersonGunClips] {profile.Name}/{source.name}: kept "
                + $"{keptChildTransformCurves} child Transform curves, dropped "
                + $"{droppedBindings} incompatible bindings, child translation "
                + $"scale={childTranslationScale}.");
            return destination;
        }

        private static Vector3 CalculateChildTranslationScale(
            AnimationClip source,
            Vector3 authoredRootScale)
        {
            Vector3 sourceRootScale = ReadConstantRootScale(source);
            var result = Vector3.one;
            for (int axis = 0; axis < 3; axis++)
            {
                if (!IsFinite(authoredRootScale[axis])
                    || Mathf.Abs(authoredRootScale[axis]) < 0.000001f)
                {
                    throw new InvalidOperationException(
                        $"{source.name} cannot target a zero/non-finite authored "
                        + $"Animator root scale on axis {axis}.");
                }

                result[axis] = sourceRootScale[axis] / authoredRootScale[axis];
            }

            if (Mathf.Abs(result.x - result.y) > 0.0001f
                || Mathf.Abs(result.x - result.z) > 0.0001f)
            {
                throw new InvalidOperationException(
                    $"{source.name} requires non-uniform child translation "
                    + $"conversion {result}. Rotated descendant bones cannot be "
                    + "converted safely with a single local-space factor.");
            }

            return Vector3.one * result.x;
        }

        private static Vector3 ReadConstantRootScale(AnimationClip source)
        {
            var result = Vector3.one;
            string[] suffixes = { ".x", ".y", ".z" };
            float[] sampleTimes =
            {
                0f,
                source.length * 0.25f,
                source.length * 0.5f,
                source.length * 0.75f,
                source.length
            };

            for (int axis = 0; axis < suffixes.Length; axis++)
            {
                EditorCurveBinding[] matches = AnimationUtility
                    .GetCurveBindings(source)
                    .Where(binding => binding.type == typeof(Transform)
                        && string.IsNullOrEmpty(binding.path)
                        && binding.propertyName
                            == "m_LocalScale" + suffixes[axis])
                    .ToArray();
                if (matches.Length == 0)
                    continue;
                if (matches.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"{source.name} has duplicate root scale curves for "
                        + suffixes[axis] + ".");
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(
                    source,
                    matches[0]);
                float expected = curve.Evaluate(0f);
                if (!IsFinite(expected) || Mathf.Abs(expected) < 0.000001f)
                {
                    throw new InvalidOperationException(
                        $"{source.name} has a zero/non-finite source root scale "
                        + $"on axis {axis}.");
                }

                foreach (float time in sampleTimes)
                {
                    if (Mathf.Abs(curve.Evaluate(time) - expected) > 0.0001f)
                    {
                        throw new InvalidOperationException(
                            $"{source.name} animates its root scale on axis {axis}; "
                            + "a constant child-translation conversion is unsafe.");
                    }
                }

                result[axis] = expected;
            }

            return result;
        }

        private static bool TryGetLocalPositionAxis(
            string propertyName,
            out int axis)
        {
            switch (propertyName)
            {
                case "m_LocalPosition.x":
                    axis = 0;
                    return true;
                case "m_LocalPosition.y":
                    axis = 1;
                    return true;
                case "m_LocalPosition.z":
                    axis = 2;
                    return true;
                default:
                    axis = -1;
                    return false;
            }
        }

        private static AnimationCurve ScaleCurve(
            AnimationCurve source,
            float scale)
        {
            Keyframe[] keys = source.keys;
            for (int index = 0; index < keys.Length; index++)
            {
                Keyframe key = keys[index];
                key.value *= scale;
                key.inTangent *= scale;
                key.outTangent *= scale;
                keys[index] = key;
            }

            return new AnimationCurve(keys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };
        }

        private static void WriteAuthoredRootCurves(
            AnimationClip clip,
            Transform animatorRoot,
            float duration)
        {
            Vector3 position = animatorRoot.localPosition;
            Quaternion rotation = animatorRoot.localRotation.normalized;
            Vector3 scale = animatorRoot.localScale;

            WriteRootVector(clip, "m_LocalPosition", position, duration);
            WriteRootQuaternion(clip, rotation, duration);
            WriteRootVector(clip, "m_LocalScale", scale, duration);
        }

        private static void WriteRootVector(
            AnimationClip clip,
            string propertyPrefix,
            Vector3 value,
            float duration)
        {
            string[] suffixes = { ".x", ".y", ".z" };
            for (int axis = 0; axis < suffixes.Length; axis++)
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        string.Empty,
                        typeof(Transform),
                        propertyPrefix + suffixes[axis]),
                    AnimationCurve.Constant(0f, duration, value[axis]));
            }
        }

        private static void WriteRootQuaternion(
            AnimationClip clip,
            Quaternion value,
            float duration)
        {
            string[] properties =
            {
                "m_LocalRotation.x",
                "m_LocalRotation.y",
                "m_LocalRotation.z",
                "m_LocalRotation.w"
            };
            for (int axis = 0; axis < properties.Length; axis++)
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        string.Empty,
                        typeof(Transform),
                        properties[axis]),
                    AnimationCurve.Constant(0f, duration, value[axis]));
            }
        }

        private static void ReplaceStateMachineMotions(
            AnimatorStateMachine machine,
            IReadOnlyDictionary<AnimationClip, AnimationClip> replacements)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                Motion replacement = ReplaceMotion(child.state.motion, replacements);
                if (replacement == child.state.motion)
                    continue;

                child.state.motion = replacement;
                EditorUtility.SetDirty(child.state);
            }

            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
                ReplaceStateMachineMotions(child.stateMachine, replacements);
        }

        private static Motion ReplaceMotion(
            Motion motion,
            IReadOnlyDictionary<AnimationClip, AnimationClip> replacements)
        {
            if (motion is AnimationClip clip)
                return replacements.TryGetValue(clip, out AnimationClip replacement)
                    ? replacement
                    : motion;

            if (!(motion is BlendTree tree))
                return motion;

            ChildMotion[] children = tree.children;
            bool changed = false;
            for (int index = 0; index < children.Length; index++)
            {
                Motion replacement = ReplaceMotion(children[index].motion, replacements);
                if (replacement == children[index].motion)
                    continue;

                children[index].motion = replacement;
                changed = true;
            }

            if (changed)
            {
                tree.children = children;
                EditorUtility.SetDirty(tree);
            }
            return tree;
        }

        private static AnimationClip ResolveOriginalSource(AnimationClip clip)
        {
            string currentPath = AssetDatabase.GetAssetPath(clip);
            if (!currentPath.StartsWith(OutputRoot + "/", StringComparison.Ordinal))
                return clip;

            string fileName = Path.GetFileName(currentPath);
            int separator = fileName.LastIndexOf("__", StringComparison.Ordinal);
            int suffix = fileName.LastIndexOf(NormalizedSuffix, StringComparison.Ordinal);
            if (separator < 0 || suffix <= separator + 2)
                throw new InvalidOperationException(
                    $"Normalized clip name does not contain its source GUID: {currentPath}");

            string sourceGuid = fileName.Substring(
                separator + 2,
                suffix - separator - 2);
            string sourcePath = AssetDatabase.GUIDToAssetPath(sourceGuid);
            AnimationClip source = LoadClipByName(sourcePath, clip.name.Substring(0, separator));
            if (source == null)
                throw new InvalidOperationException(
                    $"Cannot restore source {sourceGuid} for {currentPath}.");
            return source;
        }

        private static AnimationClip LoadClipByName(string assetPath, string safeName)
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .Where(item => !item.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToArray();
            if (clips.Length == 1)
                return clips[0];

            return clips.SingleOrDefault(item =>
                string.Equals(MakeSafeName(item.name), safeName, StringComparison.Ordinal));
        }

        private static void ValidateInternal()
        {
            foreach (WeaponProfile profile in Profiles)
            {
                Animator animator = RequireGunAnimator(profile);
                AnimatorController controller = RequireController(profile.ControllerPath);
                AnimationClip[] clips = controller.animationClips
                    .Where(clip => clip != null)
                    .Distinct()
                    .ToArray();
                if (clips.Length == 0)
                    throw new InvalidOperationException(
                        $"{profile.ControllerPath} has no AnimationClip motions.");

                foreach (AnimationClip clip in clips)
                {
                    string path = AssetDatabase.GetAssetPath(clip);
                    if (profile.Normalize
                        && !path.StartsWith(profile.OutputFolder + "/", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"{profile.ControllerPath} still references unnormalized motion {path}.");
                    }

                    ValidateClipBindings(profile, animator.transform, clip);
                    if (profile.Normalize)
                    {
                        ValidateAuthoredRoot(animator.transform, clip);
                        ValidateChildTranslations(
                            animator.transform,
                            ResolveOriginalSource(clip),
                            clip);
                    }
                    else
                        ValidateRootScale(animator.transform.localScale, clip, profile.Name);
                }

            }
        }

        private static void ValidateClipBindings(
            WeaponProfile profile,
            Transform animatorRoot,
            AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type == typeof(Transform)
                    && !string.IsNullOrEmpty(binding.path)
                    && animatorRoot.Find(binding.path) == null)
                {
                    throw new InvalidOperationException(
                        $"{profile.Name}/{clip.name} targets missing Transform path "
                        + binding.path + ".");
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                    continue;
                foreach (Keyframe key in curve.keys)
                {
                    if (!IsFinite(key.time) || !IsFinite(key.value))
                        throw new InvalidOperationException(
                            $"{profile.Name}/{clip.name}/{binding.propertyName} "
                            + "contains a non-finite key.");
                }
            }

            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (!string.IsNullOrEmpty(binding.path)
                    && animatorRoot.Find(binding.path) == null)
                {
                    throw new InvalidOperationException(
                        $"{profile.Name}/{clip.name} targets missing object path "
                        + binding.path + ".");
                }
            }
        }

        private static void ValidateAuthoredRoot(
            Transform animatorRoot,
            AnimationClip clip)
        {
            float[] sampleTimes =
            {
                0f,
                clip.length * 0.25f,
                clip.length * 0.5f,
                clip.length * 0.75f,
                clip.length
            };
            ValidateRootVector(
                clip,
                "m_LocalPosition",
                animatorRoot.localPosition,
                sampleTimes);
            ValidateRootScale(animatorRoot.localScale, clip, animatorRoot.name);
            ValidateRootRotation(clip, animatorRoot.localRotation, sampleTimes);
        }

        private static void ValidateChildTranslations(
            Transform animatorRoot,
            AnimationClip source,
            AnimationClip normalized)
        {
            Vector3 scale = CalculateChildTranslationScale(
                source,
                animatorRoot.localScale);
            EditorCurveBinding[] normalizedBindings =
                AnimationUtility.GetCurveBindings(normalized);
            foreach (EditorCurveBinding sourceBinding in
                     AnimationUtility.GetCurveBindings(source))
            {
                if (sourceBinding.type != typeof(Transform)
                    || string.IsNullOrEmpty(sourceBinding.path)
                    || !TryGetLocalPositionAxis(
                        sourceBinding.propertyName,
                        out int axis)
                    || animatorRoot.Find(sourceBinding.path) == null)
                {
                    continue;
                }

                EditorCurveBinding[] matches = normalizedBindings
                    .Where(binding => binding.type == sourceBinding.type
                        && binding.path == sourceBinding.path
                        && binding.propertyName == sourceBinding.propertyName)
                    .ToArray();
                if (matches.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"{normalized.name} must contain exactly one converted "
                        + $"{sourceBinding.path}/{sourceBinding.propertyName} curve.");
                }

                AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(
                    source,
                    sourceBinding);
                AnimationCurve normalizedCurve = AnimationUtility.GetEditorCurve(
                    normalized,
                    matches[0]);
                IEnumerable<float> sampleTimes = sourceCurve.keys
                    .Select(key => key.time)
                    .Concat(new[]
                    {
                        0f,
                        source.length * 0.25f,
                        source.length * 0.5f,
                        source.length * 0.75f,
                        source.length
                    })
                    .Distinct();
                foreach (float time in sampleTimes)
                {
                    float expected = sourceCurve.Evaluate(time) * scale[axis];
                    float actual = normalizedCurve.Evaluate(time);
                    if (!IsFinite(actual)
                        || Mathf.Abs(actual - expected) > 0.00001f)
                    {
                        throw new InvalidOperationException(
                            $"{normalized.name}/{sourceBinding.path}/"
                            + $"{sourceBinding.propertyName} evaluated to {actual}, "
                            + $"expected {expected} at {time:F4}s.");
                    }
                }
            }
        }

        private static void ValidateRootScale(
            Vector3 expected,
            AnimationClip clip,
            string context)
        {
            float[] sampleTimes =
            {
                0f,
                clip.length * 0.25f,
                clip.length * 0.5f,
                clip.length * 0.75f,
                clip.length
            };
            ValidateRootVector(clip, "m_LocalScale", expected, sampleTimes, context);
        }

        private static void ValidateRootVector(
            AnimationClip clip,
            string prefix,
            Vector3 expected,
            IReadOnlyList<float> sampleTimes,
            string context = null)
        {
            string[] suffixes = { ".x", ".y", ".z" };
            for (int axis = 0; axis < suffixes.Length; axis++)
            {
                AnimationCurve curve = RequireRootCurve(
                    clip,
                    prefix + suffixes[axis]);
                foreach (float time in sampleTimes)
                {
                    float value = curve.Evaluate(time);
                    if (!IsFinite(value)
                        || Mathf.Abs(value - expected[axis]) > 0.0001f)
                    {
                        throw new InvalidOperationException(
                            $"{context ?? clip.name}/{clip.name}: {prefix}{suffixes[axis]} "
                            + $"evaluated to {value}, expected {expected[axis]}.");
                    }
                }
            }
        }

        private static void ValidateRootRotation(
            AnimationClip clip,
            Quaternion expected,
            IReadOnlyList<float> sampleTimes)
        {
            string[] properties =
            {
                "m_LocalRotation.x",
                "m_LocalRotation.y",
                "m_LocalRotation.z",
                "m_LocalRotation.w"
            };
            AnimationCurve[] curves = properties
                .Select(property => RequireRootCurve(clip, property))
                .ToArray();
            foreach (float time in sampleTimes)
            {
                var sampled = new Quaternion(
                    curves[0].Evaluate(time),
                    curves[1].Evaluate(time),
                    curves[2].Evaluate(time),
                    curves[3].Evaluate(time));
                if (!IsFinite(sampled.x)
                    || !IsFinite(sampled.y)
                    || !IsFinite(sampled.z)
                    || !IsFinite(sampled.w)
                    || Quaternion.Angle(expected, sampled) > 0.01f)
                {
                    throw new InvalidOperationException(
                        $"{clip.name} root rotation does not match the authored gun prefab.");
                }
            }
        }

        private static AnimationCurve RequireRootCurve(
            AnimationClip clip,
            string propertyName)
        {
            EditorCurveBinding[] matches = AnimationUtility.GetCurveBindings(clip)
                .Where(binding => binding.type == typeof(Transform)
                    && string.IsNullOrEmpty(binding.path)
                    && binding.propertyName == propertyName)
                .ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException(
                    $"{clip.name} must contain exactly one root {propertyName} curve.");
            return AnimationUtility.GetEditorCurve(clip, matches[0]);
        }

        private static Animator RequireGunAnimator(WeaponProfile profile)
        {
            GameObject gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                profile.PrefabPath);
            if (gunPrefab == null)
                throw new InvalidOperationException(
                    $"Missing authored gun prefab {profile.PrefabPath}.");

            Animator[] animators = gunPrefab
                .GetComponentsInChildren<Animator>(true);
            if (animators.Length != 1)
                throw new InvalidOperationException(
                    $"{profile.PrefabPath} must contain exactly one gun Animator; "
                    + $"found {animators.Length}.");
            return animators[0];
        }

        private static AnimatorController RequireController(string path)
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            return controller != null
                ? controller
                : throw new InvalidOperationException(
                    $"Missing AnimatorController {path}.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                throw new InvalidOperationException($"Invalid asset folder {path}.");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void ForceReloadProductionAssets()
        {
            string[] normalizedPaths = AssetDatabase
                .FindAssets("t:AnimationClip", new[] { OutputRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(
                    ".anim",
                    StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToArray();
            foreach (string path in normalizedPaths)
            {
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate);
            }

            foreach (string controllerPath in Profiles
                         .Where(profile => profile.Normalize)
                         .Select(profile => profile.ControllerPath)
                         .Distinct())
            {
                AssetDatabase.ImportAsset(
                    controllerPath,
                    ImportAssetOptions.ForceUpdate);
            }

            AssetDatabase.SaveAssets();
        }

        private static void SaveAllOrThrow(string phase)
        {
            if (!EditorSceneManager.SaveOpenScenes())
                throw new InvalidOperationException(
                    $"Open scenes could not be saved {phase}.");
            AssetDatabase.SaveAssets();
        }

        private static void StopAnimationPreview()
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static string MakeSafeName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(value
                .Select(character => invalid.Contains(character) ? '_' : character)
                .ToArray());
        }

        private sealed class WeaponProfile
        {
            public WeaponProfile(
                string name,
                string prefabPath,
                string controllerPath,
                bool normalize)
            {
                Name = name;
                PrefabPath = prefabPath;
                ControllerPath = controllerPath;
                Normalize = normalize;
            }

            public string Name { get; }
            public string PrefabPath { get; }
            public string ControllerPath { get; }
            public bool Normalize { get; }
            public string OutputFolder => OutputRoot + "/" + Name;
        }
    }
}
