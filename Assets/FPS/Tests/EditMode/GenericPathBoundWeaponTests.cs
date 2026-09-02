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
    public sealed class GenericPathBoundWeaponTests
    {
        private const string PrefabPath =
            "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab";
        private const string ModelRootName = "CS_Smonk_S0_Skelmesh.ao";
        private const string RightHandSuffix = "/R_Hand";
        private const string LeftHandSuffix = "/L_Hand";
        private const string WeaponBranch =
            "/MasterWeaponAim/MasterWeapon/R_WeaponMaster";
        private const string SharedFolder =
            "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/GenericPathBoundShared";
        private const string OperatorFolder =
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/"
            + "S0/3P/Anims/Clove_GenericPathBound";

        private static readonly Profile[] Profiles =
        {
            new Profile("Vandal", "TP_Core_AK_S0_IdlePose_UB.fbx", "TP_Core_AK_S0_Reload_UB.fbx"),
            new Profile("Classic", "TP_Core_BasePistol_S0_IdlePose_UB.fbx", "TP_Core_BasePistol_S0_Reload_UB.fbx"),
            new Profile("Odin", "TP_Core_HMG_S0_IdlePose_UB.fbx", "TP_Core_HMG_S0_Reload_UB.fbx"),
            new Profile("Bucky", "TP_Core_PumpShotgun_S0_IdlePose_UB.fbx", "TP_Core_PumpShotgun_S0_Reload_UB.fbx")
        };

        [TestCase("Vandal")]
        [TestCase("Classic")]
        [TestCase("Odin")]
        [TestCase("Bucky")]
        public void Clove_RemainingWeapon_UsesAuthoredGenericMasterWeaponProfile(
            string weaponName)
        {
            Profile profile = Profiles.Single(item => item.Name == weaponName);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.NotNull(prefab);
            PlayerVisibilityController visibility =
                prefab.GetComponent<PlayerVisibilityController>();
            ThirdPersonWeaponPresentation presentation = visibility
                .ThirdPersonWeaponPresentations
                .Single(item => item.WeaponData.name == weaponName);

            Assert.AreEqual(
                ThirdPersonCharacterRigMode.GenericPathBound,
                presentation.CharacterRigMode);
            Assert.Null(presentation.CharacterAvatar);
            Assert.False(presentation.UseLeftHandIK);
            Assert.False(presentation.AnimationDrivenLeftHandIK);
            Assert.AreEqual("R_WeaponMaster", presentation.WeaponObject.transform.parent.name);
            Assert.AreEqual(
                "Clove" + weaponName + "_GenericPathBound",
                presentation.CharacterController.name);

            AnimatorController controller =
                presentation.CharacterController as AnimatorController;
            Assert.NotNull(controller);
            AssertGeneratedGripClip(controller, profile.HoldFileName);
            AssertGeneratedGripClip(controller, profile.ReloadFileName);
            AssertSourceRemainsHumanoid(profile.SourceFolder + "/" + profile.HoldFileName);
            AssertSourceRemainsHumanoid(profile.SourceFolder + "/" + profile.ReloadFileName);
        }

        [TestCase("Vandal")]
        [TestCase("Classic")]
        [TestCase("Odin")]
        [TestCase("Bucky")]
        public void Clove_RemainingWeapon_HoldKeepsBothGripsNearHands(
            string weaponName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                PlayerVisibilityController visibility =
                    instance.GetComponent<PlayerVisibilityController>();
                ThirdPersonWeaponPresentation presentation = visibility
                    .ThirdPersonWeaponPresentations
                    .Single(item => item.WeaponData.name == weaponName);
                presentation.WeaponObject.SetActive(true);
                Animator gunAnimator = presentation.WeaponObject
                    .GetComponentInChildren<Animator>(true);
                Assert.NotNull(gunAnimator, weaponName);
                gunAnimator.enabled = true;
                gunAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                gunAnimator.Rebind();
                gunAnimator.Update(0.0001f);
                Animator animator = instance.transform.Find("Body")
                    .GetComponent<Animator>();
                animator.runtimeAnimatorController = presentation.CharacterController;
                animator.avatar = null;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);

                string stateName = weaponName + " Hold";
                int stateHash = Animator.StringToHash(stateName);
                int layer = Enumerable.Range(0, animator.layerCount)
                    .Single(index => animator.HasState(index, stateHash));
                animator.Play(stateHash, layer, 0f);
                animator.Update(0.0001f);

                Transform model = animator.GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == ModelRootName);
                Transform rightHand = model.GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == "R_Hand");
                Transform leftHand = model.GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == "L_Hand");
                Transform trigger = presentation.WeaponObject
                    .GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == "Trigger");
                Transform support = presentation.WeaponObject
                    .GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == "Left_Hand_Target");

                Assert.That(
                    Vector3.Distance(rightHand.position, trigger.position),
                    Is.LessThan(0.30f),
                    weaponName + " right-hand grip");
                Assert.That(
                    Vector3.Distance(leftHand.position, support.position),
                    Is.LessThan(0.30f),
                    weaponName + " support-hand grip");
                Assert.That(
                    Vector3.Distance(rightHand.position, leftHand.position),
                    Is.LessThan(1.00f),
                    weaponName + " must not tear the two hands apart");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [TestCase("WalkAdd")]
        [TestCase("RunAdd")]
        [TestCase("JumpAdd")]
        [TestCase("LandAdd")]
        public void Clove_PathBoundMovementAdditiveUsesItsOwnReferenceFrame(
            string clipPrefix)
        {
            AnimationClip clip = AssetDatabase.FindAssets(
                    clipPrefix + " t:AnimationClip",
                    new[] { SharedFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(
                    "_PathBound.anim",
                    StringComparison.Ordinal))
                .Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
                .Single(item => item.name.StartsWith(
                    clipPrefix + "_",
                    StringComparison.Ordinal));

            AnimationClipSettings settings =
                AnimationUtility.GetAnimationClipSettings(clip);
            Assert.False(
                settings.hasAdditiveReferencePose,
                clip.name + " must not retain an FBX reference with old paths.");
            Assert.Null(settings.additiveReferencePoseClip, clip.name);
        }

        [TestCase("WalkAdd")]
        [TestCase("RunAdd")]
        [TestCase("JumpAdd")]
        [TestCase("LandAdd")]
        public void Clove_OperatorMovementAdditiveUsesItsOwnReferenceFrame(
            string clipLabel)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                OperatorFolder + "/Operator_" + clipLabel + "_PathBound.anim");
            Assert.NotNull(clip, clipLabel);

            AnimationClipSettings settings =
                AnimationUtility.GetAnimationClipSettings(clip);
            Assert.False(
                settings.hasAdditiveReferencePose,
                clip.name + " must not retain an FBX reference with old paths.");
            Assert.Null(settings.additiveReferencePoseClip, clip.name);
        }

        [Test]
        public void Clove_UpperBodyMaskDoesNotResetAuthoredWeaponMounts()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Transform body = prefab.transform.Find("Body");
            PlayerVisibilityController visibility =
                prefab.GetComponent<PlayerVisibilityController>();
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                SharedFolder + "/CloveGenericUpperBody.mask");

            Assert.NotNull(body);
            Assert.NotNull(mask);
            string weaponMasterPath = ModelRootName
                + "/Skeleton/Root/Splitter/MasterWeaponAim/MasterWeapon/"
                + "R_WeaponMaster";
            int weaponMasterIndex = Enumerable.Range(0, mask.transformCount)
                .Where(item =>
                    mask.GetTransformPath(item) == weaponMasterPath)
                .DefaultIfEmpty(-1)
                .Single();
            Assert.GreaterOrEqual(weaponMasterIndex, 0);
            Assert.True(mask.GetTransformActive(weaponMasterIndex));

            foreach (ThirdPersonWeaponPresentation presentation in
                     visibility.ThirdPersonWeaponPresentations)
            {
                string path = AnimationUtility.CalculateTransformPath(
                    presentation.WeaponObject.transform,
                    body);
                StringAssert.Contains(
                    WeaponBranch,
                    path,
                    presentation.WeaponData.name);
                Assert.False(
                    Enumerable.Range(0, mask.transformCount).Any(index =>
                        mask.GetTransformPath(index) == path
                        || mask.GetTransformPath(index).StartsWith(
                            path + "/",
                            StringComparison.Ordinal)),
                    presentation.WeaponData.name
                    + " must remain outside the Body Animator mask so its "
                    + "authored local mount and dedicated gun Animator are not reset.");
            }
        }

        [Test]
        public void Clove_AllFirearmsUseGenericProfilesWithoutRuntimeIK()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            PlayerVisibilityController visibility =
                prefab.GetComponent<PlayerVisibilityController>();
            CollectionAssert.AreEquivalent(
                new[] { "Vandal", "Classic", "Operator", "Odin", "Bucky" },
                visibility.ThirdPersonWeaponPresentations
                    .Select(item => item.WeaponData.name)
                    .ToArray());
            foreach (ThirdPersonWeaponPresentation presentation in
                     visibility.ThirdPersonWeaponPresentations)
            {
                Assert.AreEqual(
                    ThirdPersonCharacterRigMode.GenericPathBound,
                    presentation.CharacterRigMode,
                    presentation.WeaponData.name);
                Assert.Null(presentation.CharacterAvatar, presentation.WeaponData.name);
                Assert.False(presentation.UseLeftHandIK, presentation.WeaponData.name);
                Assert.False(
                    presentation.AnimationDrivenLeftHandIK,
                    presentation.WeaponData.name);
                Assert.AreEqual(
                    "R_WeaponMaster",
                    presentation.WeaponObject.transform.parent.name,
                    presentation.WeaponData.name);
            }
        }

        [Test]
        public void Clove_OperatorUsesEyeAlignedAdsAndOneRegularFirePath()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            PlayerVisibilityController visibility =
                prefab.GetComponent<PlayerVisibilityController>();
            ThirdPersonWeaponPresentation presentation = visibility
                .ThirdPersonWeaponPresentations
                .Single(item => item.WeaponData.name == "Operator");
            AnimatorController body =
                presentation.CharacterController as AnimatorController;
            Assert.NotNull(body);

            AnimatorStateMachine bodyMachine = body.layers
                .Single(layer => layer.name == "Upper Body Gun Pose")
                .stateMachine;
            AnimatorState aim = FindState(bodyMachine, "Operator Aim");
            AnimatorState hold = FindState(bodyMachine, "Operator Hold");
            AnimatorState fire = FindState(bodyMachine, "Operator Fire");
            Assert.NotNull(aim);
            Assert.NotNull(hold);
            Assert.NotNull(fire);
            Assert.Null(FindState(bodyMachine, "Operator Fire Zoomed"));
            Assert.AreEqual("Operator_Aim_PathBound", aim.motion.name);
            Assert.AreNotSame(hold.motion, aim.motion);
            AssertSingleFireFlow(bodyMachine, fire);

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                ThirdPersonWeaponPresentation instancePresentation = instance
                    .GetComponent<PlayerVisibilityController>()
                    .ThirdPersonWeaponPresentations
                    .Single(item => item.WeaponData.name == "Operator");
                Animator bodyAnimator = instance.transform.Find("Body")
                    .GetComponent<Animator>();
                bodyAnimator.runtimeAnimatorController = body;
                bodyAnimator.avatar = null;
                bodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                bodyAnimator.enabled = true;
                bodyAnimator.Rebind();
                bodyAnimator.Update(0f);

                Transform model = bodyAnimator
                    .GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == ModelRootName);
                Transform rightEye = model
                    .GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == "R_Eyeball");
                Transform rightHand = model
                    .GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == "R_Hand");
                Transform leftHand = model
                    .GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == "L_Hand");
                Transform weapon = instancePresentation.WeaponObject.transform;
                Transform scope = weapon
                    .GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == "ScopeTarget");
                Transform muzzle = weapon
                    .GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == "Muzzle");
                Transform trigger = weapon
                    .GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == "Trigger");
                Transform support = weapon
                    .GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == "Left_Hand_Target");

                bodyAnimator.Play("Operator Hold", 1, 0f);
                bodyAnimator.Update(0.0001f);
                Vector3 holdScopePosition = scope.position;
                bodyAnimator.Play("Operator Aim", 1, 0f);
                bodyAnimator.Update(0.0001f);

                Vector3 sightAxis = (muzzle.position - scope.position).normalized;
                float sightLineMiss = Vector3.Cross(
                    rightEye.position - scope.position,
                    sightAxis).magnitude;
                float eyeRelief = Vector3.Dot(
                    scope.position - rightEye.position,
                    sightAxis);
                Assert.That(
                    Vector3.Distance(holdScopePosition, scope.position),
                    Is.GreaterThan(0.1f),
                    "ADS must raise the scope away from the hip-fire Hold pose.");
                Assert.That(sightLineMiss, Is.LessThan(0.03f));
                Assert.That(eyeRelief, Is.InRange(0.12f, 0.32f));
                Assert.That(
                    Vector3.Dot(sightAxis, bodyAnimator.transform.forward),
                    Is.GreaterThan(0.9f));
                Assert.That(
                    Vector3.Distance(rightHand.position, trigger.position),
                    Is.LessThan(0.05f));
                Assert.That(
                    Vector3.Distance(leftHand.position, support.position),
                    Is.LessThan(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            Animator gunAnimator = presentation.WeaponObject
                .GetComponentInChildren<Animator>(true);
            AnimatorController gun =
                gunAnimator.runtimeAnimatorController as AnimatorController;
            Assert.NotNull(gun);
            AnimatorStateMachine gunMachine = gun.layers
                .Single(layer => layer.name == "Base Layer")
                .stateMachine;
            AnimatorState gunFire = FindState(gunMachine, "Fire");
            Assert.NotNull(gunFire);
            Assert.Null(FindState(gunMachine, "Fire Zoomed"));
            AssertSingleFireFlow(gunMachine, gunFire);
        }

        private static AnimatorState FindState(
            AnimatorStateMachine machine,
            string stateName)
        {
            return machine.states
                .Select(child => child.state)
                .SingleOrDefault(state => state.name == stateName);
        }

        private static void AssertSingleFireFlow(
            AnimatorStateMachine machine,
            AnimatorState fire)
        {
            AnimatorStateTransition[] incoming = machine.anyStateTransitions
                .Where(transition => transition.destinationState == fire)
                .Concat(machine.states.SelectMany(child =>
                    child.state.transitions.Where(transition =>
                        transition.destinationState == fire)))
                .ToArray();
            Assert.IsNotEmpty(incoming);
            foreach (AnimatorStateTransition transition in incoming)
            {
                Assert.True(transition.conditions.Any(condition =>
                    condition.parameter == "Fire"));
                Assert.False(transition.conditions.Any(condition =>
                    condition.parameter == "Aiming"));
            }
        }

        private static void AssertGeneratedGripClip(
            AnimatorController controller,
            string sourceFileName)
        {
            string stem = Path.GetFileNameWithoutExtension(sourceFileName);
            AnimationClip clip = controller.animationClips
                .Distinct()
                .Single(item =>
                    item.name.StartsWith(stem + "_", StringComparison.Ordinal)
                    && AssetDatabase.GetAssetPath(item).EndsWith(
                        "_PathBound.anim",
                        StringComparison.Ordinal));
            Assert.False(clip.humanMotion, clip.name);
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.True(bindings.Any(binding =>
                binding.type == typeof(Transform)
                && binding.path.EndsWith(RightHandSuffix, StringComparison.Ordinal)));
            Assert.True(bindings.Any(binding =>
                binding.type == typeof(Transform)
                && binding.path.EndsWith(LeftHandSuffix, StringComparison.Ordinal)));
            Assert.True(bindings.Any(binding =>
                binding.type == typeof(Transform)
                && binding.path.Contains(WeaponBranch, StringComparison.Ordinal)));
            Assert.False(bindings.Any(binding =>
                binding.type == typeof(Transform)
                && binding.propertyName.StartsWith(
                    "m_LocalScale",
                    StringComparison.Ordinal)));
        }

        private static void AssertSourceRemainsHumanoid(string sourcePath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(sourcePath)
                as ModelImporter;
            Assert.NotNull(importer, sourcePath);
            Assert.AreEqual(
                ModelImporterAnimationType.Human,
                importer.animationType,
                sourcePath);
        }

        private sealed class Profile
        {
            public Profile(string name, string holdFileName, string reloadFileName)
            {
                Name = name;
                HoldFileName = holdFileName;
                ReloadFileName = reloadFileName;
            }

            public string Name { get; }
            public string HoldFileName { get; }
            public string ReloadFileName { get; }
            public string SourceFolder =>
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/"
                + Name + "/S0/3P/Anims";
        }
    }
}
