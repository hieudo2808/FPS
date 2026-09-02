using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public sealed class ThirdPersonGunClipNormalizationTests
    {
        private const string OutputRoot =
            "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/NormalizedGunClips";
        private const string ClovePrefabPath =
            "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab";

        private static readonly Profile[] Profiles =
        {
            new Profile("Vandal"),
            new Profile("Classic"),
            new Profile("Operator"),
            new Profile("Bucky")
        };

        [TestCase("Vandal")]
        [TestCase("Classic")]
        [TestCase("Operator")]
        [TestCase("Bucky")]
        public void ProductionController_UsesHierarchySafeAuthoredRootClips(
            string weaponName)
        {
            Profile profile = Profiles.Single(item => item.Name == weaponName);
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    profile.ControllerPath);
            Assert.NotNull(controller, profile.ControllerPath);

            Animator productionAnimator = RequireProductionGunAnimator(profile.Name);
            Animator authoredAnimator = RequireAuthoredGunAnimator(profile);
            AnimationClip[] clips = controller.animationClips
                .Where(clip => clip != null)
                .Distinct()
                .ToArray();
            Assert.IsNotEmpty(clips, profile.ControllerPath);

            foreach (AnimationClip clip in clips)
            {
                string path = AssetDatabase.GetAssetPath(clip);
                StringAssert.StartsWith(profile.OutputFolder + "/", path);
                Assert.False(
                    path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase),
                    $"{profile.ControllerPath} must not run an imported FBX directly.");
                AssertBindingsExist(productionAnimator.transform, clip);
                AssertFiniteCurves(clip);
                AssertAuthoredRoot(authoredAnimator.transform, clip);
            }

            AnimatorState reload = FindState(controller, "Reload");
            Assert.IsInstanceOf<AnimationClip>(reload.motion);
            Assert.True(
                AnimationUtility.GetCurveBindings((AnimationClip)reload.motion)
                    .Any(binding => binding.type == typeof(Transform)
                        && !string.IsNullOrEmpty(binding.path)),
                $"{weaponName} Reload lost all weapon-bone animation.");
        }

        [TestCase("Vandal")]
        [TestCase("Classic")]
        [TestCase("Operator")]
        [TestCase("Bucky")]
        public void ProductionClips_SampleWithoutCollapsingGunRoot(
            string weaponName)
        {
            Profile profile = Profiles.Single(item => item.Name == weaponName);
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    profile.ControllerPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                ClovePrefabPath);
            Animator authoredAnimator = RequireAuthoredGunAnimator(profile);
            Assert.NotNull(controller, profile.ControllerPath);
            Assert.NotNull(prefab, ClovePrefabPath);

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                instance.SetActive(true);
                PlayerVisibilityController visibility =
                    instance.GetComponent<PlayerVisibilityController>();
                ThirdPersonWeaponPresentation presentation = visibility
                    .ThirdPersonWeaponPresentations
                    .Single(item => item.WeaponData.name == weaponName);
                presentation.WeaponObject.SetActive(true);
                Animator animator = presentation.WeaponObject
                    .GetComponentsInChildren<Animator>(true)
                    .Single();
                animator.enabled = false;

                Vector3 authoredPosition = authoredAnimator.transform.localPosition;
                Quaternion authoredRotation = authoredAnimator.transform.localRotation;
                Vector3 authoredScale = authoredAnimator.transform.localScale;
                foreach (AnimationClip clip in controller.animationClips.Distinct())
                {
                    foreach (float time in SampleTimes(clip))
                    {
                        animator.transform.localPosition = authoredPosition;
                        animator.transform.localRotation = authoredRotation;
                        animator.transform.localScale = authoredScale;
                        clip.SampleAnimation(animator.gameObject, time);

                        Assert.That(
                            Vector3.Distance(
                                authoredPosition,
                                animator.transform.localPosition),
                            Is.LessThan(0.0001f),
                            clip.name + " root position");
                        Assert.That(
                            Quaternion.Angle(
                                authoredRotation,
                                animator.transform.localRotation),
                            Is.LessThan(0.01f),
                            clip.name + " root rotation");
                        Assert.That(
                            Vector3.Distance(
                                authoredScale,
                                animator.transform.localScale),
                            Is.LessThan(0.0001f),
                            clip.name + " root scale");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [TestCase("Vandal")]
        [TestCase("Classic")]
        [TestCase("Operator")]
        [TestCase("Bucky")]
        public void ProductionClips_ConvertChildTranslationsForAuthoredRootScale(
            string weaponName)
        {
            Profile profile = Profiles.Single(item => item.Name == weaponName);
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    profile.ControllerPath);
            Animator animator = RequireAuthoredGunAnimator(profile);
            Assert.NotNull(controller, profile.ControllerPath);

            foreach (AnimationClip normalized in controller.animationClips
                         .Where(clip => clip != null)
                         .Distinct())
            {
                AnimationClip source = ResolveOriginalSource(normalized);
                Vector3 sourceRootScale = ReadRootScale(source);
                float factor = sourceRootScale.x / animator.transform.localScale.x;
                Assert.AreEqual(
                    factor,
                    sourceRootScale.y / animator.transform.localScale.y,
                    0.0001f,
                    source.name + " non-uniform Y conversion");
                Assert.AreEqual(
                    factor,
                    sourceRootScale.z / animator.transform.localScale.z,
                    0.0001f,
                    source.name + " non-uniform Z conversion");

                EditorCurveBinding[] normalizedBindings =
                    AnimationUtility.GetCurveBindings(normalized);
                foreach (EditorCurveBinding sourceBinding in
                         AnimationUtility.GetCurveBindings(source))
                {
                    if (sourceBinding.type != typeof(Transform)
                        || string.IsNullOrEmpty(sourceBinding.path)
                        || !sourceBinding.propertyName.StartsWith(
                            "m_LocalPosition.",
                            StringComparison.Ordinal)
                        || animator.transform.Find(sourceBinding.path) == null)
                    {
                        continue;
                    }

                    EditorCurveBinding normalizedBinding = normalizedBindings
                        .Single(binding => binding.type == sourceBinding.type
                            && binding.path == sourceBinding.path
                            && binding.propertyName == sourceBinding.propertyName);
                    AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(
                        source,
                        sourceBinding);
                    AnimationCurve normalizedCurve = AnimationUtility.GetEditorCurve(
                        normalized,
                        normalizedBinding);
                    foreach (float time in sourceCurve.keys
                                 .Select(key => key.time)
                                 .Concat(SampleTimes(source))
                                 .Distinct())
                    {
                        Assert.AreEqual(
                            sourceCurve.Evaluate(time) * factor,
                            normalizedCurve.Evaluate(time),
                            0.00001f,
                            normalized.name + "/" + sourceBinding.path + "/"
                            + sourceBinding.propertyName + $" at {time:F4}s");
                    }
                }
            }
        }

        [Test]
        public void Odin_UsesNeutralStaticPoseAtAuthoredScale()
        {
            var profile = new Profile(
                "Odin",
                "CloveOdin3P_ThirdPersonGun.controller");
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    profile.ControllerPath);
            Assert.NotNull(controller, profile.ControllerPath);

            Animator animator = RequireAuthoredGunAnimator(profile);
            foreach (AnimationClip clip in controller.animationClips.Distinct())
            {
                string path = AssetDatabase.GetAssetPath(clip);
                StringAssert.Contains("CloveOdin3P_StaticPose.anim", path);
                AssertRootVector(
                    clip,
                    "m_LocalScale",
                    animator.transform.localScale);
            }
        }

        private static void AssertBindingsExist(
            Transform animatorRoot,
            AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform)
                    || string.IsNullOrEmpty(binding.path))
                    continue;
                Assert.NotNull(
                    animatorRoot.Find(binding.path),
                    $"{clip.name} targets missing Transform path {binding.path}.");
            }

            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (string.IsNullOrEmpty(binding.path))
                    continue;
                Assert.NotNull(
                    animatorRoot.Find(binding.path),
                    $"{clip.name} targets missing object path {binding.path}.");
            }
        }

        private static void AssertFiniteCurves(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                foreach (Keyframe key in curve.keys)
                {
                    Assert.True(IsFinite(key.time), $"{clip.name}/{binding.propertyName} time");
                    Assert.True(IsFinite(key.value), $"{clip.name}/{binding.propertyName} value");
                }
            }
        }

        private static void AssertAuthoredRoot(
            Transform animatorRoot,
            AnimationClip clip)
        {
            AssertRootVector(
                clip,
                "m_LocalPosition",
                animatorRoot.localPosition);
            AssertRootVector(
                clip,
                "m_LocalScale",
                animatorRoot.localScale);

            string[] rotationProperties =
            {
                "m_LocalRotation.x",
                "m_LocalRotation.y",
                "m_LocalRotation.z",
                "m_LocalRotation.w"
            };
            AnimationCurve[] curves = rotationProperties
                .Select(property => RequireRootCurve(clip, property))
                .ToArray();
            foreach (float time in SampleTimes(clip))
            {
                var sampled = new Quaternion(
                    curves[0].Evaluate(time),
                    curves[1].Evaluate(time),
                    curves[2].Evaluate(time),
                    curves[3].Evaluate(time));
                Assert.That(Quaternion.Angle(animatorRoot.localRotation, sampled),
                    Is.LessThan(0.01f), clip.name + " root rotation");
            }
        }

        private static void AssertRootVector(
            AnimationClip clip,
            string prefix,
            Vector3 expected)
        {
            string[] suffixes = { ".x", ".y", ".z" };
            for (int axis = 0; axis < suffixes.Length; axis++)
            {
                AnimationCurve curve = RequireRootCurve(
                    clip,
                    prefix + suffixes[axis]);
                foreach (float time in SampleTimes(clip))
                {
                    float value = curve.Evaluate(time);
                    Assert.True(IsFinite(value), clip.name + "/" + prefix);
                    Assert.AreEqual(
                        expected[axis],
                        value,
                        0.0001f,
                        clip.name + "/" + prefix + suffixes[axis]);
                }
            }
        }

        private static AnimationCurve RequireRootCurve(
            AnimationClip clip,
            string propertyName)
        {
            EditorCurveBinding binding = AnimationUtility.GetCurveBindings(clip)
                .Single(item => item.type == typeof(Transform)
                    && string.IsNullOrEmpty(item.path)
                    && item.propertyName == propertyName);
            return AnimationUtility.GetEditorCurve(clip, binding);
        }

        private static IEnumerable<float> SampleTimes(AnimationClip clip)
        {
            yield return 0f;
            yield return clip.length * 0.25f;
            yield return clip.length * 0.5f;
            yield return clip.length * 0.75f;
            yield return clip.length;
        }

        private static AnimatorState FindState(
            AnimatorController controller,
            string stateName)
        {
            return controller.layers
                .SelectMany(layer => layer.stateMachine.states)
                .Select(child => child.state)
                .Single(state => state.name == stateName);
        }

        private static AnimationClip ResolveOriginalSource(AnimationClip normalized)
        {
            string fileName = Path.GetFileName(AssetDatabase.GetAssetPath(normalized));
            const string suffixText = "_AuthoredRoot.anim";
            int separator = fileName.LastIndexOf("__", StringComparison.Ordinal);
            int suffix = fileName.LastIndexOf(suffixText, StringComparison.Ordinal);
            Assert.That(separator, Is.GreaterThanOrEqualTo(0), fileName);
            Assert.That(suffix, Is.GreaterThan(separator + 2), fileName);

            string guid = fileName.Substring(
                separator + 2,
                suffix - separator - 2);
            string sourcePath = AssetDatabase.GUIDToAssetPath(guid);
            string safeName = fileName.Substring(0, separator);
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith(
                    "__preview__",
                    StringComparison.Ordinal))
                .ToArray();
            AnimationClip source = clips.Length == 1
                ? clips[0]
                : clips.SingleOrDefault(clip => MakeSafeName(clip.name) == safeName);
            Assert.NotNull(source, sourcePath + "/" + safeName);
            return source;
        }

        private static Vector3 ReadRootScale(AnimationClip clip)
        {
            var result = Vector3.one;
            string[] suffixes = { ".x", ".y", ".z" };
            for (int axis = 0; axis < suffixes.Length; axis++)
            {
                EditorCurveBinding[] matches = AnimationUtility
                    .GetCurveBindings(clip)
                    .Where(binding => binding.type == typeof(Transform)
                        && string.IsNullOrEmpty(binding.path)
                        && binding.propertyName
                            == "m_LocalScale" + suffixes[axis])
                    .ToArray();
                if (matches.Length == 0)
                    continue;
                Assert.AreEqual(1, matches.Length, clip.name + suffixes[axis]);
                result[axis] = AnimationUtility.GetEditorCurve(clip, matches[0])
                    .Evaluate(0f);
            }

            return result;
        }

        private static string MakeSafeName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(value
                .Select(character => invalid.Contains(character) ? '_' : character)
                .ToArray());
        }

        private static Animator RequireProductionGunAnimator(string weaponName)
        {
            GameObject clove = AssetDatabase.LoadAssetAtPath<GameObject>(
                ClovePrefabPath);
            Assert.NotNull(clove, ClovePrefabPath);
            PlayerVisibilityController visibility =
                clove.GetComponent<PlayerVisibilityController>();
            Assert.NotNull(visibility);
            ThirdPersonWeaponPresentation presentation = visibility
                .ThirdPersonWeaponPresentations
                .Single(item => item.WeaponData.name == weaponName);
            Assert.NotNull(presentation.WeaponObject, weaponName);
            return presentation.WeaponObject
                .GetComponentsInChildren<Animator>(true)
                .Single();
        }

        private static Animator RequireAuthoredGunAnimator(Profile profile)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                profile.PrefabPath);
            Assert.NotNull(prefab, profile.PrefabPath);
            Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
            Assert.AreEqual(
                1,
                animators.Length,
                profile.PrefabPath + " authored Animator count");
            return animators[0];
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class Profile
        {
            public Profile(string name, string controllerName = null)
            {
                Name = name;
                ControllerName = controllerName ?? "Clove" + name + "3P_GNTP.controller";
            }

            public string Name { get; }
            public string ControllerName { get; }
            public string ControllerPath =>
                "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/"
                + ControllerName;
            public string PrefabPath =>
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/"
                + Name + "/" + Name + "3P.prefab";
            public string OutputFolder => OutputRoot + "/" + Name;
        }
    }
}
