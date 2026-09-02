using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    /// <summary>
    /// Regression coverage for the authored third-person rig contract.
    /// Clove's weapon animations are path-bound and own both hands plus the
    /// MasterWeapon branch; comparison characters retain their legacy proxy IK.
    /// </summary>
    public sealed class ThirdPersonLeftHandIKTests
    {
        private const string ClovePrefabPath =
            "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab";
        private const string UpperBodyLayer = "Upper Body Gun Pose";
        private const string WeaponBranch =
            "/MasterWeaponAim/MasterWeapon/R_WeaponMaster";

        private static readonly string[] FirearmNames =
        {
            "Vandal",
            "Classic",
            "Operator",
            "Odin",
            "Bucky"
        };

        private static readonly string[] ComparisonPlayerPrefabPaths =
        {
            "Assets/FPS/Features/Characters/Content/Players/Sage/SagePlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Gekko/GekkoPlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Brimstone/BrimstonePlayer.prefab"
        };

        [Test]
        public void Clove_AllFirearmsUseAuthoredGenericMasterWeaponProfiles()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                ClovePrefabPath);
            Assert.NotNull(prefab);

            PlayerVisibilityController visibility =
                prefab.GetComponent<PlayerVisibilityController>();
            ThirdPersonLeftHandIK legacyRig =
                prefab.GetComponent<ThirdPersonLeftHandIK>();
            Assert.NotNull(visibility);
            Assert.NotNull(legacyRig);
            Assert.True(legacyRig.HasValidReferences);
            CollectionAssert.AreEquivalent(
                FirearmNames,
                visibility.ThirdPersonWeaponPresentations
                    .Select(item => item.WeaponData.name)
                    .ToArray());

            foreach (ThirdPersonWeaponPresentation presentation in
                     visibility.ThirdPersonWeaponPresentations)
            {
                string weaponName = presentation.WeaponData.name;
                Assert.AreEqual(
                    ThirdPersonCharacterRigMode.GenericPathBound,
                    presentation.CharacterRigMode,
                    weaponName);
                Assert.Null(presentation.CharacterAvatar, weaponName);
                Assert.False(presentation.UseLeftHandIK, weaponName);
                Assert.False(
                    presentation.AnimationDrivenLeftHandIK,
                    weaponName);
                Assert.NotNull(presentation.CharacterController, weaponName);
                Assert.AreEqual(
                    "Clove" + weaponName + "_GenericPathBound",
                    presentation.CharacterController.name,
                    weaponName);
                Assert.NotNull(presentation.WeaponObject, weaponName);
                Assert.AreEqual(
                    "R_WeaponMaster",
                    presentation.WeaponObject.transform.parent.name,
                    weaponName);

                ThirdPersonWeaponGrip grip = presentation.WeaponObject
                    .GetComponentInChildren<ThirdPersonWeaponGrip>(true);
                Assert.NotNull(grip, weaponName);
                Assert.True(grip.IsValid, weaponName);
            }
        }

        [TestCase("Vandal")]
        [TestCase("Classic")]
        [TestCase("Operator")]
        [TestCase("Odin")]
        [TestCase("Bucky")]
        public void Clove_HoldAndReloadOwnBothHandsAndMasterWeapon(
            string weaponName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                ClovePrefabPath);
            PlayerVisibilityController visibility =
                prefab.GetComponent<PlayerVisibilityController>();
            ThirdPersonWeaponPresentation presentation = visibility
                .ThirdPersonWeaponPresentations
                .Single(item => item.WeaponData.name == weaponName);
            AnimatorController controller =
                presentation.CharacterController as AnimatorController;
            Assert.NotNull(controller, weaponName);

            AnimatorStateMachine upperBody = controller.layers
                .Single(layer => layer.name == UpperBodyLayer)
                .stateMachine;
            AssertPathBoundMotion(
                FindDirectState(upperBody, weaponName + " Hold")?.motion,
                weaponName + " Hold");
            AssertPathBoundMotion(
                FindDirectState(upperBody, weaponName + " Reload")?.motion,
                weaponName + " Reload");
        }

        [Test]
        public void Clove_SwitchingFirearmsKeepsGenericProfileAndDisablesIKGraph()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                ClovePrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                PlayerVisibilityController visibility =
                    instance.GetComponent<PlayerVisibilityController>();
                WeaponManager manager = instance.GetComponent<WeaponManager>();
                Animator body = instance.transform.Find("Body")
                    .GetComponent<Animator>();
                Behaviour rigBuilder = body.GetComponents<Behaviour>()
                    .Single(component => component.GetType().FullName
                        == "UnityEngine.Animations.Rigging.RigBuilder");

                visibility.SetupVisibility(false);
                foreach (PrimaryWeaponId id in new[]
                {
                    PrimaryWeaponId.Vandal,
                    PrimaryWeaponId.Operator,
                    PrimaryWeaponId.Odin,
                    PrimaryWeaponId.Bucky
                })
                {
                    Assert.True(manager.TryReplacePrimaryWeaponServer(id));
                    visibility.RefreshWeaponPresentation(
                        manager.CurrentWeaponIndex);
                    AssertSelectedProfile(
                        visibility,
                        body,
                        rigBuilder,
                        id.ToString());
                }

                manager.SetEquippedWeaponServer(1);
                visibility.RefreshWeaponPresentation(
                    manager.CurrentWeaponIndex);
                AssertSelectedProfile(
                    visibility,
                    body,
                    rigBuilder,
                    "Classic");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ComparisonPlayersKeepLegacyProxyRigMode()
        {
            foreach (string prefabPath in ComparisonPlayerPrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);
                Assert.NotNull(prefab, prefabPath);
                GameObject instance = Object.Instantiate(prefab);
                try
                {
                    ThirdPersonLeftHandIK rig =
                        instance.GetComponent<ThirdPersonLeftHandIK>();
                    Assert.NotNull(rig, prefabPath);
                    Assert.True(rig.HasValidReferences, prefabPath);
                    Assert.True(rig.SupportsDynamicWeaponBinding, prefabPath);
                    Assert.False(rig.UsesAnimationDrivenWeight, prefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        private static void AssertSelectedProfile(
            PlayerVisibilityController visibility,
            Animator body,
            Behaviour rigBuilder,
            string weaponName)
        {
            ThirdPersonWeaponPresentation selected = visibility
                .ThirdPersonWeaponPresentations
                .Single(item => item.WeaponData.name == weaponName);
            Assert.AreSame(
                selected.CharacterController,
                body.runtimeAnimatorController,
                weaponName);
            Assert.Null(body.avatar, weaponName);
            Assert.False(rigBuilder.enabled, weaponName);
            Assert.True(selected.WeaponObject.activeSelf, weaponName);
            Assert.AreEqual(
                "R_WeaponMaster",
                selected.WeaponObject.transform.parent.name,
                weaponName);
        }

        private static void AssertPathBoundMotion(
            Motion motion,
            string stateName)
        {
            AnimationClip clip = motion as AnimationClip;
            Assert.NotNull(clip, stateName);
            Assert.False(clip.humanMotion, stateName);
            StringAssert.EndsWith(
                "_PathBound.anim",
                AssetDatabase.GetAssetPath(clip),
                stateName);

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(
                clip);
            Assert.True(bindings.Any(binding =>
                binding.type == typeof(Transform)
                && binding.path.EndsWith("/R_Hand", StringComparison.Ordinal)),
                stateName + " must animate the right hand.");
            Assert.True(bindings.Any(binding =>
                binding.type == typeof(Transform)
                && binding.path.EndsWith("/L_Hand", StringComparison.Ordinal)),
                stateName + " must animate the left hand.");
            Assert.True(bindings.Any(binding =>
                binding.type == typeof(Transform)
                && binding.path.Contains(WeaponBranch, StringComparison.Ordinal)),
                stateName + " must animate R_WeaponMaster.");
            Assert.False(bindings.Any(binding =>
                binding.path.Contains(
                    "ThirdPersonWeaponRig",
                    StringComparison.Ordinal)),
                stateName + " must not contain baked runtime-IK curves.");
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
    }
}
