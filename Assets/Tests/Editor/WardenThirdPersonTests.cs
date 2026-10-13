using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FPS.Tests
{
    public sealed class WardenThirdPersonTests
    {
        [TestCase("Brimstone")]
        [TestCase("Clove")]
        [TestCase("Gekko")]
        [TestCase("Sage")]
        public void WardenUsesItsOwnBodyAndGunClocksWithoutASpareMagazine(string character)
        {
            var root = PrefabUtility.LoadPrefabContents(
                $"Assets/FPS/Features/Characters/Content/Players/{character}/{character}Player.prefab");
            try
            {
                var manager = root.GetComponent<WeaponManager>();
                var visibility = root.GetComponent<PlayerVisibilityController>();
                Assert.That(manager.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Warden), Is.True);
                visibility.SetupVisibility(false);
                var presentation = visibility.ThirdPersonWeaponPresentations.Single(p => p.WeaponData.weaponName == "Warden");
                var body = manager.CharacterAnimation;
                var gun = presentation.WeaponObject.GetComponentInChildren<Animator>(true);
                Assert.That(presentation.WeaponObject.activeInHierarchy, Is.True);
                Assert.That(body.runtimeAnimatorController, Is.EqualTo(presentation.CharacterController));
                Assert.That(presentation.CharacterRigMode, Is.EqualTo(ThirdPersonCharacterRigMode.GenericPathBound));
                Assert.That(body.runtimeAnimatorController.name, Is.EqualTo(
                    (character == "Brimstone" ? "Brimstone" : "Clove") + "Warden_GenericPathBound"));
                Assert.That(body.avatar, Is.Null);
                Assert.That(gun.avatar, Is.Null);
                Assert.That(gun.runtimeAnimatorController, Is.Not.Null);
                Assert.That(body.applyRootMotion || gun.applyRootMotion, Is.False);
                Assert.That(presentation.WeaponObject.GetComponentsInChildren<Renderer>(true)
                    .Any(r => AnimationUtility.CalculateTransformPath(r.transform, presentation.WeaponObject.transform).Contains("Magazine_Extra")), Is.False);
                Assert.That(gun.runtimeAnimatorController.animationClips
                    .SelectMany(AnimationUtility.GetCurveBindings).Any(b => b.path.Contains("Magazine_Extra")), Is.False);
                Assert.That(presentation.WeaponObject.GetComponentsInChildren<Weapon>(true), Is.Empty);
                var existingUnboundHelpers = visibility.ThirdPersonWeaponPresentations
                    .Single(p => p.WeaponData.weaponName == "Vandal").CharacterController.animationClips
                    .SelectMany(AnimationUtility.GetCurveBindings)
                    .Where(b => b.type == typeof(Transform) && !string.IsNullOrEmpty(b.path) && body.transform.Find(b.path) == null)
                    .Select(b => b.path).ToHashSet();
                foreach (var clip in body.runtimeAnimatorController.animationClips.Where(c => c.name.Contains("Warden")))
                foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Transform)))
                    Assert.That(string.IsNullOrEmpty(binding.path) || body.transform.Find(binding.path) != null || existingUnboundHelpers.Contains(binding.path), Is.True, clip.name + ":" + binding.path);
                body.Rebind(); gun.Rebind();
                body.SetBool("Grounded", true);
                body.Update(0f); gun.Update(0f);
                var hold = body.runtimeAnimatorController.animationClips.Single(c => c.name.EndsWith("_Warden_Hold"));
                AssertAuthoredPose(body, hold, 0f);
                var magazine = gun.transform.Find("Skeleton/Root/Magazine_Main");
                Assert.That(magazine, Is.Not.Null);
                Vector3 seated = magazine.localPosition;
                foreach (var action in new[] { "Equip", "Reload" })
                {
                    manager.TriggerAnimation(action);
                    body.Update(.1f); gun.Update(.1f);
                    body.Update(.1f); gun.Update(.1f);
                    Assert.That(body.GetCurrentAnimatorStateInfo(body.GetLayerIndex("Upper Body Gun Pose")).IsName("Warden " + action), Is.True);
                    Assert.That(gun.GetCurrentAnimatorStateInfo(0).IsName(action), Is.True);
                    var actionClip = body.runtimeAnimatorController.animationClips.Single(c => c.name.EndsWith("_Warden_" + action));
                    var gunClip = gun.runtimeAnimatorController.animationClips.Single(c => c.name.EndsWith("_" + action));
                    foreach (float progress in new[] { .1f, .25f, .5f, .75f, .9f })
                    {
                        body.SetFloat(action + "NormalizedTime", progress);
                        visibility.SetThirdPersonWeaponAnimationFloat(action + "NormalizedTime", progress);
                        body.Update(0f); gun.Update(0f);
                        Assert.That(gun.GetFloat(action + "NormalizedTime"), Is.EqualTo(body.GetFloat(action + "NormalizedTime")));
                        AssertAuthoredPose(body, actionClip, progress);
                        AssertAuthoredPose(gun, gunClip, progress, false);
                        // The main magazine is already seated again at the reload midpoint.
                        if (action == "Reload" && progress == .25f)
                            Assert.That(Vector3.Distance(magazine.localPosition, seated), Is.GreaterThan(.0001f));
                    }
                    manager.TriggerAnimation(action + "Complete");
                    body.Update(.1f); gun.Update(.1f);
                    body.Update(.1f); gun.Update(.1f);
                    Assert.That(body.GetCurrentAnimatorStateInfo(body.GetLayerIndex("Upper Body Gun Pose")).IsName("Warden Hold"), Is.True);
                    Assert.That(gun.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
                    Assert.That(Vector3.Distance(magazine.localPosition, seated), Is.LessThan(.0001f));
                }
                manager.TriggerAnimation("Fire");
                visibility.SetThirdPersonAiming(true);
                body.Update(.2f); gun.Update(.2f);
                body.Update(.2f); gun.Update(.2f);
                Assert.That(body.GetCurrentAnimatorStateInfo(body.GetLayerIndex("Upper Body Gun Pose")).IsName("Warden Aim"), Is.True);
                var aim = body.runtimeAnimatorController.animationClips.Single(c => c.name.EndsWith("_Warden_Aim"));
                AssertAuthoredPose(body, aim, 0f);
                visibility.SetThirdPersonAiming(false);
                body.Update(.2f); body.Update(.2f);
                Assert.That(body.GetCurrentAnimatorStateInfo(body.GetLayerIndex("Upper Body Gun Pose")).IsName("Warden Hold"), Is.True);
                AssertAuthoredPose(body, hold, 0f);
                Assert.That(manager.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Vandal), Is.True);
                visibility.RefreshWeaponPresentation(0);
                Assert.That(presentation.WeaponObject.activeSelf, Is.False);
                Assert.That(body.runtimeAnimatorController.name, Does.Contain("Vandal"));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void AssertAuthoredPose(Animator animator, AnimationClip clip, float progress, bool upperBody = true)
        {
            var mask = upperBody
                ? ((AnimatorController)animator.runtimeAnimatorController).layers[animator.GetLayerIndex("Upper Body Gun Pose")].avatarMask
                : null;
            var activePaths = mask == null ? null : Enumerable.Range(0, mask.transformCount)
                .Where(mask.GetTransformActive).Select(mask.GetTransformPath).ToHashSet();
            var bindings = AnimationUtility.GetCurveBindings(clip)
                .Where(b => b.type == typeof(Transform) && (activePaths == null || activePaths.Contains(b.path)))
                .Where(b => string.IsNullOrEmpty(b.path) || animator.transform.Find(b.path) != null).ToArray();
            var rotationPaths = bindings.Where(b => b.propertyName == "m_LocalRotation.w").Select(b => b.path).Distinct().ToArray();
            if (upperBody)
                foreach (string hand in new[] { "L_Hand", "R_Hand", "R_WeaponMaster" })
                    Assert.That(rotationPaths.Any(p => p.EndsWith("/" + hand)), Is.True, clip.name + ": missing " + hand);
            var reference = new GameObject("Warden clip sample") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                foreach (string path in bindings.Select(b => b.path).Where(p => p.Length > 0).Distinct())
                {
                    var parent = reference.transform;
                    foreach (string part in path.Split('/'))
                    {
                        var child = parent.Find(part);
                        if (child == null) { child = new GameObject(part).transform; child.SetParent(parent, false); }
                        parent = child;
                    }
                }
                // Use Unity's sampler, including quaternion compression/interpolation.
                clip.SampleAnimation(reference, clip.length * progress);
                foreach (string path in rotationPaths)
                {
                    var expected = string.IsNullOrEmpty(path) ? reference.transform : reference.transform.Find(path);
                    var target = string.IsNullOrEmpty(path) ? animator.transform : animator.transform.Find(path);
                    Assert.That(Quaternion.Angle(target.localRotation, expected.localRotation), Is.LessThan(.2f), clip.name + ":" + path + " at " + progress);
                }
                foreach (var binding in bindings.Where(b => b.propertyName.StartsWith("m_LocalPosition.")))
                {
                    var target = string.IsNullOrEmpty(binding.path) ? animator.transform : animator.transform.Find(binding.path);
                    var expected = string.IsNullOrEmpty(binding.path) ? reference.transform : reference.transform.Find(binding.path);
                    int axis = binding.propertyName.EndsWith("x") ? 0 : binding.propertyName.EndsWith("y") ? 1 : 2;
                    Assert.That(target.localPosition[axis], Is.EqualTo(expected.localPosition[axis]).Within(.0001f), clip.name + ":" + binding.path + " at " + progress);
                }
            }
            finally { Object.DestroyImmediate(reference); }
        }
    }
}
