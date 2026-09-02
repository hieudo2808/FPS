using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FPS.Tests
{
    public sealed class WeaponAnimatorFlowTests
    {
        private const string VandalThirdPersonBodyController =
            "Assets/FPS/Features/Characters/Animation/Content/3P/ThirdPersonCharacter.controller";
        private const string VandalThirdPersonGunController =
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Vandal/Vandal3P_Gun.controller";

        private static readonly string[] ThirdPersonWeaponNames =
        {
            "Vandal", "Classic", "Operator", "Odin", "Bucky"
        };

        [TestCase("Assets/FPS/Features/Weapons/Content/Vandal/Animation/VandalAnim.controller")]
        [TestCase("Assets/FPS/Features/Weapons/Content/Classic/Animations/ClassicAnim.controller")]
        [TestCase("Assets/FPS/Features/Weapons/Content/Operator/Animation/Operator.controller")]
        [TestCase("Assets/FPS/Features/Weapons/Content/Odin/Animation/OdinAnim.controller")]
        [TestCase("Assets/FPS/Features/Weapons/Content/Bucky/Animations/BuckyAnim.controller")]
        public void GunController_UsesEquipEntryAndUnblendedOneShotFlow(string controllerPath)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            Assert.NotNull(controller);
            AssertOneShotFlow(
                controller.layers[0].stateMachine,
                controller.name,
                hasRuntimeControlledContinuousFire: controller.name == "OdinAnim");
        }

        [TestCase("Classic")]
        [TestCase("Vandal")]
        [TestCase("Operator")]
        [TestCase("Odin")]
        [TestCase("Bucky")]
        public void FirstPersonWeaponLayer_MatchesGunOneShotFlow(string layerName)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/FPS/Features/Weapons/Content/FirstPerson/FPAnim.controller");
            AnimatorControllerLayer layer = controller.layers.Single(item => item.name == layerName);
            AssertOneShotFlow(
                layer.stateMachine,
                $"FPAnim/{layerName}",
                hasRuntimeControlledContinuousFire: layerName == "Odin");

            int layerIndex = System.Array.FindIndex(controller.layers, item => item.name == layerName);
            foreach (AnimatorStateTransition transition in layer.stateMachine.anyStateTransitions)
            {
                Assert.True(transition.conditions.Any(condition =>
                    condition.parameter == "ActiveWeaponLayer"
                    && condition.mode == AnimatorConditionMode.Equals
                    && condition.threshold == layerIndex));
            }
        }

        [Test]
        public void VandalThirdPersonControllers_UseMatchingActionDurationsAndTriggers()
        {
            AnimatorController body = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                VandalThirdPersonBodyController);
            AnimatorController gun = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                VandalThirdPersonGunController);
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(
                "Assets/FPS/Features/Weapons/Content/Vandal/Vandal.asset");

            Assert.NotNull(body);
            Assert.NotNull(gun);
            Assert.NotNull(data);
            CollectionAssert.IsSubsetOf(
                new[] { "Speed", "Grounded", "FreeFall", "Equip", "Reload", "Fire" },
                body.parameters.Select(parameter => parameter.name).ToArray());
            Assert.False(body.parameters.Any(parameter => parameter.name == "AimPitch"));
            CollectionAssert.IsSubsetOf(
                new[] { "Equip", "Reload", "Fire" },
                gun.parameters.Select(parameter => parameter.name).ToArray());

            AnimatorStateMachine bodyActions = body.layers.Single(
                layer => layer.name == "Upper Body Gun Pose").stateMachine;
            AnimatorStateMachine bodyFire = body.layers.Single(
                layer => layer.name == "Vandal Fire Additive").stateMachine;
            AnimatorStateMachine gunActions = gun.layers[0].stateMachine;

            AnimatorState bodyEquip = FindState(bodyActions, "Vandal Equip");
            AnimatorState bodyReload = FindState(bodyActions, "Vandal Reload");
            AnimatorState bodyFireState = FindState(bodyFire, "Vandal Fire");
            AnimatorState gunEquip = FindState(gunActions, "Equip");
            AnimatorState gunReload = FindState(gunActions, "Reload");
            AnimatorState gunFire = FindState(gunActions, "Fire");
            AnimatorState gunIdle = FindState(gunActions, "Idle");

            StringAssert.Contains("GNTP_Core_AK_S0_Reload", gunReload.motion.name);

            Assert.AreEqual(data.EquipDuration, EffectiveDuration(bodyEquip), 0.002f);
            Assert.AreEqual(data.ReloadDuration, EffectiveDuration(bodyReload), 0.002f);
            Assert.AreEqual(data.ReloadDuration, EffectiveDuration(gunReload), 0.002f);
            Assert.AreEqual(data.FireInterval, EffectiveDuration(bodyFireState), 0.002f);
            Assert.AreEqual(data.FireInterval, EffectiveDuration(gunFire), 0.002f);
            Assert.AreEqual(EffectiveDuration(bodyEquip), EffectiveDuration(gunEquip), 0.002f);
            Assert.AreEqual(gunIdle, gunActions.defaultState);

            AssertAnyStateTrigger(bodyActions, bodyEquip, "Equip");
            AssertAnyStateTrigger(bodyActions, bodyReload, "Reload");
            AssertAnyStateTrigger(bodyFire, bodyFireState, "Fire");
            AssertAnyStateTrigger(gunActions, gunEquip, "Equip");
            AssertAnyStateTrigger(gunActions, gunReload, "Reload");
            AssertAnyStateTrigger(gunActions, gunFire, "Fire");
            AssertGunClipRootMatchesPrefab(
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Vandal/Vandal3P.prefab",
                gunEquip,
                gunReload,
                gunFire,
                gunIdle);
        }

        [TestCase(
            "Classic",
            "Assets/FPS/Features/Weapons/Content/Classic/Classic.asset",
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Classic/Classic3P_Body.controller",
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Classic/Classic3P_Gun.controller")]
        [TestCase(
            "Operator",
            "Assets/FPS/Features/Weapons/Content/Operator/Operator.asset",
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/Operator3P_Body.controller",
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/Operator3P_Gun.controller")]
        [TestCase(
            "Odin",
            "Assets/FPS/Features/Weapons/Content/Odin/Odin.asset",
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Odin/Odin3P_Body.controller",
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Odin/Odin3P_Gun.controller")]
        [TestCase(
            "Bucky",
            "Assets/FPS/Features/Weapons/Content/Bucky/Bucky.asset",
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Bucky/Bucky3P_Body.controller",
            "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Bucky/Bucky3P_Gun.controller")]
        public void RemainingThirdPersonControllers_MatchWeaponTimingsAndTriggers(
            string weaponName,
            string dataPath,
            string bodyControllerPath,
            string gunControllerPath)
        {
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(dataPath);
            AnimatorController body = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                bodyControllerPath);
            AnimatorController gun = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                gunControllerPath);

            Assert.NotNull(data);
            Assert.NotNull(body);
            Assert.NotNull(gun);

            AnimatorState bodyEquip = FindState(body, weaponName + " Equip");
            AnimatorState bodyReload = FindState(body, weaponName + " Reload");
            AnimatorState bodyFire = FindState(body, weaponName + " Fire");
            AnimatorState gunEquip = FindState(gun.layers[0].stateMachine, "Equip");
            AnimatorState gunReload = FindState(gun.layers[0].stateMachine, "Reload");
            AnimatorState gunFire = FindState(gun.layers[0].stateMachine, "Fire");
            AnimatorState gunIdle = FindState(gun.layers[0].stateMachine, "Idle");

            Assert.True(((AnimationClip)bodyEquip.motion).humanMotion);
            Assert.True(((AnimationClip)bodyReload.motion).humanMotion);
            Assert.True(((AnimationClip)bodyFire.motion).humanMotion);
            Assert.AreEqual(data.EquipDuration, EffectiveDuration(bodyEquip), 0.002f);
            Assert.AreEqual(data.ReloadDuration, EffectiveDuration(bodyReload), 0.002f);
            Assert.AreEqual(data.FireInterval, EffectiveDuration(bodyFire), 0.002f);
            Assert.AreEqual(data.EquipDuration, EffectiveDuration(gunEquip), 0.002f);
            Assert.AreEqual(data.ReloadDuration, EffectiveDuration(gunReload), 0.002f);
            Assert.AreEqual(data.FireInterval, EffectiveDuration(gunFire), 0.002f);
            Assert.AreEqual(gunIdle, gun.layers[0].stateMachine.defaultState);

            AssertAnyStateTrigger(FindMachine(body, bodyEquip), bodyEquip, "Equip");
            AssertAnyStateTrigger(FindMachine(body, bodyReload), bodyReload, "Reload");
            AssertAnyStateTrigger(FindMachine(body, bodyFire), bodyFire, "Fire");
            AssertAnyStateTrigger(gun.layers[0].stateMachine, gunEquip, "Equip");
            AssertAnyStateTrigger(gun.layers[0].stateMachine, gunReload, "Reload");
            AssertAnyStateTrigger(gun.layers[0].stateMachine, gunFire, "Fire");
            AssertGunClipRootMatchesPrefab(
                gunControllerPath.Replace("_Gun.controller", ".prefab"),
                gunEquip,
                gunReload,
                gunFire,
                gunIdle);
        }

        [TestCase("Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab")]
        [TestCase("Assets/FPS/Features/Characters/Content/Players/Sage/SagePlayer.prefab")]
        [TestCase("Assets/FPS/Features/Characters/Content/Players/Gekko/GekkoPlayer.prefab")]
        [TestCase("Assets/FPS/Features/Characters/Content/Players/Brimstone/BrimstonePlayer.prefab")]
        public void PlayerPrefab_UsesProductionBodyAndDedicatedVandalThirdPersonVisual(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.NotNull(prefab);

            PlayerMovement movement = prefab.GetComponent<PlayerMovement>();
            WeaponManager manager = prefab.GetComponent<WeaponManager>();
            PlayerVisibilityController visibility = prefab.GetComponent<PlayerVisibilityController>();
            Assert.NotNull(movement);
            Assert.NotNull(manager);
            Assert.NotNull(visibility);
            Assert.NotNull(movement.CharacterAnimation);
            if (prefabPath.Contains("/Clove/", StringComparison.Ordinal))
            {
                Assert.AreEqual(
                    "CloveOperator_GenericPathBound",
                    movement.CharacterAnimation.runtimeAnimatorController.name);
                Assert.Null(
                    movement.CharacterAnimation.avatar,
                    "Clove authors Operator as the active Generic preview profile.");
            }
            else
            {
                Assert.AreEqual(
                    "ThirdPersonCharacter",
                    movement.CharacterAnimation.runtimeAnimatorController.name);
            }

            var managerProperties = new SerializedObject(manager);
            Assert.AreEqual(
                movement.CharacterAnimation,
                managerProperties.FindProperty("characterAnimation").objectReferenceValue);

            Assert.GreaterOrEqual(visibility.ThirdPersonWeaponSlots.Length, 2);
            Assert.AreEqual("Vandal_3P", visibility.ThirdPersonWeaponSlots[0].name);
            Assert.AreEqual("Classic_3P", visibility.ThirdPersonWeaponSlots[1].name);

            ThirdPersonWeaponPresentation[] presentations =
                visibility.ThirdPersonWeaponPresentations;
            Assert.AreEqual(ThirdPersonWeaponNames.Length, presentations.Length);
            CollectionAssert.AreEquivalent(
                ThirdPersonWeaponNames,
                presentations.Select(item => item.WeaponData.name).ToArray());

            foreach (ThirdPersonWeaponPresentation presentation in presentations)
            {
                Assert.NotNull(presentation.WeaponData);
                Assert.NotNull(presentation.WeaponObject);
                Assert.NotNull(presentation.CharacterController);
                string expectedParent = prefabPath.Contains(
                        "/Clove/",
                        StringComparison.Ordinal)
                    ? "R_WeaponMaster"
                    : "R_Hand";
                Assert.AreEqual(
                    expectedParent,
                    presentation.WeaponObject.transform.parent.name);
                Assert.False(visibility.FirstPersonWeaponSlots.Contains(
                    presentation.WeaponObject));

                Animator gunAnimator = presentation.WeaponObject
                    .GetComponentInChildren<Animator>(true);
                Assert.NotNull(gunAnimator);
                if (prefabPath.Contains("/Clove/", StringComparison.Ordinal))
                {
                    StringAssert.Contains(
                        presentation.WeaponData.name,
                        gunAnimator.runtimeAnimatorController.name);
                }
                else
                {
                    Assert.AreEqual(
                        presentation.WeaponData.name + "3P_Gun",
                        gunAnimator.runtimeAnimatorController.name);
                }

                MonoBehaviour[] authoredBehaviours = presentation.WeaponObject
                    .GetComponentsInChildren<MonoBehaviour>(true);
                Assert.IsNotEmpty(authoredBehaviours);
                Assert.True(
                    authoredBehaviours.All(component =>
                        component is ThirdPersonWeaponGrip),
                    presentation.WeaponData.name
                    + " may only contain the authored grip metadata component.");
                Assert.Zero(presentation.WeaponObject
                    .GetComponentsInChildren<Collider>(true).Length);
                Assert.Zero(presentation.WeaponObject
                    .GetComponentsInChildren<Rigidbody>(true).Length);
            }
        }

        [TestCase("Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab")]
        public void PlayerPrefab_ThirdPersonHoldPoseKeepsAuthoritativeGripsAligned(
            string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.NotNull(prefab, prefabPath);

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                    behaviour.enabled = false;

                PlayerMovement movement = instance.GetComponent<PlayerMovement>();
                PlayerVisibilityController visibility =
                    instance.GetComponent<PlayerVisibilityController>();
                Assert.NotNull(movement, prefabPath);
                Assert.NotNull(visibility, prefabPath);

                Animator body = movement.CharacterAnimation;
                Assert.NotNull(body, prefabPath);
                body.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                body.enabled = true;

                foreach (ThirdPersonWeaponPresentation presentation
                    in visibility.ThirdPersonWeaponPresentations)
                {
                    presentation.WeaponObject.SetActive(true);
                    Transform attachment = presentation.WeaponObject.transform;
                    Assert.That(attachment.localScale.x, Is.GreaterThan(0f));
                    Assert.That(attachment.localScale.y, Is.GreaterThan(0f));
                    Assert.That(attachment.localScale.z, Is.GreaterThan(0f));

                    body.avatar = presentation.CharacterRigMode switch
                    {
                        ThirdPersonCharacterRigMode.AuthoredAvatar =>
                            presentation.CharacterAvatar,
                        ThirdPersonCharacterRigMode.GenericPathBound => null,
                        _ => body.avatar
                    };
                    body.runtimeAnimatorController = presentation.CharacterController;
                    foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
                        renderer.enabled = true;
                    body.Rebind();
                    body.Update(0f);

                    string holdState = presentation.WeaponData.name + " Hold";
                    int holdHash = Animator.StringToHash(holdState);
                    int holdLayer = System.Array.FindIndex(
                        Enumerable.Range(0, body.layerCount).ToArray(),
                        layer => body.HasState(layer, holdHash));
                    Assert.That(holdLayer, Is.GreaterThanOrEqualTo(0), holdState);
                    body.Play(holdHash, holdLayer, 0f);
                    body.Update(0.0001f);

                    Animator gun = presentation.WeaponObject
                        .GetComponentInChildren<Animator>(true);
                    Assert.NotNull(gun, presentation.WeaponData.name);
                    gun.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    gun.enabled = true;
                    foreach (Renderer renderer in gun.GetComponentsInChildren<Renderer>(true))
                        renderer.enabled = true;
                    gun.Rebind();
                    gun.Play("Idle", 0, 0f);
                    gun.Update(0.0001f);

                    Transform trigger = gun.GetComponentsInChildren<Transform>(true)
                        .Single(item => item.name == "Trigger");
                    Transform supportTarget = gun.GetComponentsInChildren<Transform>(true)
                        .Single(item => item.name == "Left_Hand_Target");
                    Transform rightHand;
                    Transform leftHand;
                    if (presentation.CharacterRigMode
                        == ThirdPersonCharacterRigMode.GenericPathBound)
                    {
                        rightHand = body.GetComponentsInChildren<Transform>(true)
                            .Single(item => item.name == "R_Hand");
                        leftHand = body.GetComponentsInChildren<Transform>(true)
                            .Single(item => item.name == "L_Hand");
                    }
                    else
                    {
                        rightHand = body.GetBoneTransform(HumanBodyBones.RightHand);
                        leftHand = body.GetBoneTransform(HumanBodyBones.LeftHand);
                    }

                    Assert.That(
                        Vector3.Distance(trigger.position, rightHand.position),
                        Is.LessThan(0.35f),
                        $"{prefabPath}/{presentation.WeaponData.name}: right hand to trigger grip");
                    // Operator's legacy Left_Hand_Target is not authoritative in
                    // the Generic/no-IK profile. Its body clip directly owns the
                    // support hand and MasterWeapon branch, covered by the path-
                    // binding tests; fitting the mesh to this unused marker would
                    // regress the visually approved authored pose.
                    if (presentation.WeaponData.name != "Operator")
                    {
                        Assert.That(
                            Vector3.Distance(
                                supportTarget.position,
                                leftHand.position),
                            Is.LessThan(0.35f),
                            $"{prefabPath}/{presentation.WeaponData.name}: "
                            + "left hand to support grip");
                    }

                    gun.enabled = false;
                    presentation.WeaponObject.SetActive(false);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void VandalThirdPersonLocomotion_MatchesCloveMovementSpeedAndFootCadence()
        {
            AnimatorController body = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                VandalThirdPersonBodyController);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab");

            Assert.NotNull(body);
            Assert.NotNull(playerPrefab);

            PlayerMovement movement = playerPrefab.GetComponent<PlayerMovement>();
            Assert.NotNull(movement);

            var movementProperties = new SerializedObject(movement);
            float runSpeed = movementProperties.FindProperty("speed").floatValue;
            float walkMultiplier = movementProperties.FindProperty("walkMultiplier").floatValue;
            float walkSpeed = runSpeed / walkMultiplier;

            AssertLocomotionBlendTree(
                body,
                "Locomotion",
                "GroundedLocomotion",
                walkSpeed,
                runSpeed);
            AssertLocomotionBlendTree(
                body,
                "Upper Body Movement Additive",
                "UpperLocomotionAdditive",
                walkSpeed,
                runSpeed);
        }

        [Test]
        public void OperatorThirdPersonReloadTransitions_UseSharedCompletionTrigger()
        {
            var cases = new[]
            {
                new
                {
                    Path = "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/"
                        + "S0/3P/Anims/Clove_GenericPathBound/"
                        + "CloveOperator_GenericPathBound.controller",
                    Layer = "Upper Body Gun Pose",
                    Reload = "Operator Reload",
                    Destination = "Operator Hold"
                },
                new
                {
                    Path = "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/"
                        + "CloveOperator3P_GNTP.controller",
                    Layer = "Base Layer",
                    Reload = "Reload",
                    Destination = "Idle"
                }
            };

            foreach (var item in cases)
            {
                AnimatorController controller =
                    AssetDatabase.LoadAssetAtPath<AnimatorController>(item.Path);
                Assert.NotNull(controller, item.Path);
                Assert.True(controller.parameters.Any(parameter =>
                    parameter.name == "ReloadComplete"
                    && parameter.type == AnimatorControllerParameterType.Trigger));

                AnimatorStateMachine machine = controller.layers
                    .Single(layer => layer.name == item.Layer)
                    .stateMachine;
                AnimatorState reload = machine.states
                    .Select(child => child.state)
                    .Single(state => state.name == item.Reload);
                AnimatorState destination = machine.states
                    .Select(child => child.state)
                    .Single(state => state.name == item.Destination);
                AnimatorStateTransition transition = reload.transitions
                    .Single(candidate => candidate.destinationState == destination);

                Assert.False(transition.hasExitTime, item.Path);
                Assert.True(transition.hasFixedDuration, item.Path);
                Assert.AreEqual(0.05f, transition.duration, 0.0001f, item.Path);
                Assert.AreEqual(1, transition.conditions.Length, item.Path);
                Assert.AreEqual(
                    "ReloadComplete",
                    transition.conditions[0].parameter,
                    item.Path);
                Assert.AreEqual(
                    AnimatorConditionMode.If,
                    transition.conditions[0].mode,
                    item.Path);
            }
        }

        [Test]
        public void AllAuthoredThirdPersonActions_UseOneSharedTimingPolicy()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab");
            Assert.NotNull(prefab);
            PlayerVisibilityController visibility =
                prefab.GetComponent<PlayerVisibilityController>();
            Assert.NotNull(visibility);

            foreach (ThirdPersonWeaponPresentation presentation in
                     visibility.ThirdPersonWeaponPresentations)
            {
                Assert.NotNull(presentation, "Null 3P presentation entry.");
                Assert.NotNull(presentation.WeaponData);
                Assert.NotNull(presentation.WeaponObject);
                string weapon = presentation.WeaponData.name;
                AnimatorController body = presentation.CharacterController
                    as AnimatorController;
                Animator gunAnimator = presentation.WeaponObject
                    .GetComponentInChildren<Animator>(true);
                AnimatorController gun = gunAnimator != null
                    ? gunAnimator.runtimeAnimatorController as AnimatorController
                    : null;
                Assert.NotNull(body, weapon + " Body");
                Assert.NotNull(gun, weapon + " gun");

                AssertCompletionAction(
                    body,
                    weapon + " Reload",
                    weapon + " Hold",
                    "ReloadComplete",
                    "ReloadPlaybackSpeed",
                    presentation.WeaponData.ReloadDuration,
                    weapon + " Body Reload");
                AssertCompletionAction(
                    gun,
                    "Reload",
                    "Idle",
                    "ReloadComplete",
                    "ReloadPlaybackSpeed",
                    presentation.WeaponData.ReloadDuration,
                    weapon + " gun Reload");
                AssertCompletionAction(
                    body,
                    weapon + " Equip",
                    weapon + " Hold",
                    "EquipComplete",
                    "EquipPlaybackSpeed",
                    presentation.WeaponData.EquipDuration,
                    weapon + " Body Equip");
                AssertCompletionAction(
                    gun,
                    "Equip",
                    "Idle",
                    "EquipComplete",
                    "EquipPlaybackSpeed",
                    presentation.WeaponData.EquipDuration,
                    weapon + " gun Equip");

                AssertTimedFire(
                    body,
                    weapon + " Fire",
                    presentation.WeaponData.FireInterval,
                    weapon + " Body Fire");
                AssertTimedFire(
                    gun,
                    "Fire",
                    presentation.WeaponData.FireInterval,
                    weapon + " gun Fire");
            }
        }

        [Test]
        public void ThirdPersonGunControllers_UseOnlyGntpOrStaticMotions()
        {
            string[] controllerPaths =
            {
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Vandal/Vandal3P_Gun.controller",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Classic/Classic3P_Gun.controller",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/Operator3P_Gun.controller",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Odin/Odin3P_Gun.controller",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Bucky/Bucky3P_Gun.controller",
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/S0/3P/Anims/OperatorGNTP.controller",
                "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/CloveVandal3P_GNTP.controller",
                "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/CloveVandal3P_Gun.overrideController",
                "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/CloveClassic3P_GNTP.controller",
                "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/CloveClassic3P_Gun.overrideController",
                "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/CloveOperator3P_GNTP.controller",
                "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/CloveOperator3P_Gun.overrideController",
                "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/CloveOdin3P_Gun.controller",
                "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/CloveOdin3P_ThirdPersonGun.controller",
                "Assets/FPS/Features/Characters/Animation/Content/3P/Clove/CloveBucky3P_GNTP.controller"
            };

            foreach (string controllerPath in controllerPaths)
            {
                RuntimeAnimatorController controller =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        controllerPath);
                Assert.NotNull(controller, controllerPath);

                foreach (AnimationClip motion in controller.animationClips.Distinct())
                {
                    string motionPath = AssetDatabase.GetAssetPath(motion);
                    bool isGntp = motion.name.Contains(
                            "GNTP",
                            StringComparison.OrdinalIgnoreCase)
                        || motionPath.Contains(
                            "GNTP",
                            StringComparison.OrdinalIgnoreCase);
                    bool isStaticPose = motionPath.Contains(
                        "StaticPose",
                        StringComparison.OrdinalIgnoreCase);
                    bool isOperatorZoomedFire = motionPath.EndsWith(
                        "/Operator/S0/3P/Anims/GN_Core_Boltsniper_S0_FireZoomed.fbx",
                        StringComparison.Ordinal);

                    Assert.True(
                        isGntp || isStaticPose || isOperatorZoomedFire,
                        $"{controllerPath} uses forbidden non-GNTP "
                        + $"gun motion '{motionPath}'.");
                }
            }
        }

        private static void AssertOneShotFlow(
            AnimatorStateMachine machine,
            string context,
            bool hasRuntimeControlledContinuousFire = false)
        {
            AnimatorState idle = machine.states.Select(item => item.state).Single(item => item.name == "Idle");
            AnimatorState equip = machine.states.Select(item => item.state).Single(item => item.name == "Equip");
            Assert.AreEqual(equip, machine.defaultState, $"{context}: Entry/default must be Equip.");
            Assert.IsEmpty(idle.transitions, $"{context}: Idle must not leave without an explicit action trigger.");

            foreach (AnimatorState state in machine.states.Select(item => item.state).Where(item => item != idle))
            {
                if (hasRuntimeControlledContinuousFire && state.name == "Fire")
                {
                    Assert.IsEmpty(
                        state.transitions,
                        $"{context}/Fire: continuous fire is looped by Weapon and returns to Idle on release.");
                    continue;
                }

                Assert.AreEqual(1, state.transitions.Length, $"{context}/{state.name}: expected one exit to Idle.");
                AnimatorStateTransition exit = state.transitions[0];
                Assert.AreEqual(idle, exit.destinationState);
                Assert.True(exit.hasExitTime, $"{context}/{state.name}: one-shot state must finish before Idle.");
                Assert.AreEqual(0.98f, exit.exitTime, 0.0001f);
                Assert.AreEqual(0f, exit.duration, 0.0001f);
                Assert.IsEmpty(exit.conditions);
            }

            foreach (AnimatorStateTransition enter in machine.anyStateTransitions)
            {
                Assert.False(enter.hasExitTime);
                Assert.AreEqual(0f, enter.duration, 0.0001f);
                Assert.IsNotEmpty(enter.conditions, $"{context}: Any State transitions require an explicit trigger.");
            }
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
        {
            return machine.states.Select(item => item.state).Single(state => state.name == stateName);
        }

        private static AnimatorState FindState(
            AnimatorController controller,
            string stateName)
        {
            return controller.layers
                .SelectMany(layer => layer.stateMachine.states)
                .Select(item => item.state)
                .Single(state => state.name == stateName);
        }

        private static AnimatorStateMachine FindMachine(
            AnimatorController controller,
            AnimatorState state)
        {
            return controller.layers
                .Select(layer => layer.stateMachine)
                .Single(machine => machine.states.Any(item => item.state == state));
        }

        private static float EffectiveDuration(AnimatorState state)
        {
            Assert.IsInstanceOf<AnimationClip>(state.motion, $"{state.name} must use a clip motion.");
            return ((AnimationClip)state.motion).length / state.speed;
        }

        private static void AssertCompletionAction(
            AnimatorController controller,
            string stateName,
            string destinationName,
            string completionParameter,
            string playbackParameter,
            float expectedDuration,
            string context)
        {
            AnimatorControllerParameter completion = controller.parameters
                .SingleOrDefault(parameter =>
                    parameter.name == completionParameter);
            AnimatorControllerParameter playback = controller.parameters
                .SingleOrDefault(parameter =>
                    parameter.name == playbackParameter);
            Assert.NotNull(completion, context);
            Assert.NotNull(playback, context);
            Assert.AreEqual(
                AnimatorControllerParameterType.Trigger,
                completion.type,
                context);
            Assert.AreEqual(
                AnimatorControllerParameterType.Float,
                playback.type,
                context);
            Assert.AreEqual(1f, playback.defaultFloat, 0.0001f, context);

            AnimatorState state = FindState(controller, stateName);
            AnimatorState destination = FindState(controller, destinationName);
            Assert.AreEqual(
                expectedDuration,
                EffectiveDuration(state),
                0.002f,
                context);
            Assert.True(state.speedParameterActive, context);
            Assert.AreEqual(playbackParameter, state.speedParameter, context);

            AnimatorStateTransition transition = state.transitions.Single(
                candidate => candidate.destinationState == destination);
            Assert.False(transition.hasExitTime, context);
            Assert.True(transition.hasFixedDuration, context);
            Assert.AreEqual(0.05f, transition.duration, 0.0001f, context);
            Assert.AreEqual(1, transition.conditions.Length, context);
            Assert.AreEqual(
                completionParameter,
                transition.conditions[0].parameter,
                context);
            Assert.AreEqual(
                AnimatorConditionMode.If,
                transition.conditions[0].mode,
                context);
        }

        private static void AssertTimedFire(
            AnimatorController controller,
            string stateName,
            float expectedDuration,
            string context)
        {
            AnimatorState state = FindState(controller, stateName);
            Assert.AreEqual(
                expectedDuration,
                EffectiveDuration(state),
                0.002f,
                context);
            Assert.False(state.speedParameterActive, context);
            Assert.AreEqual(1, state.transitions.Length, context);
            Assert.True(state.transitions[0].hasExitTime, context);
            Assert.AreEqual(1f, state.transitions[0].exitTime, 0.0001f, context);
            Assert.AreEqual(0, state.transitions[0].conditions.Length, context);
        }

        private static void AssertAnyStateTrigger(
            AnimatorStateMachine machine,
            AnimatorState destination,
            string parameter)
        {
            AnimatorStateTransition transition = machine.anyStateTransitions.Single(
                item => item.destinationState == destination);
            Assert.True(transition.conditions.Any(condition =>
                condition.parameter == parameter && condition.mode == AnimatorConditionMode.If));
        }

        private static void AssertLocomotionBlendTree(
            AnimatorController controller,
            string layerName,
            string treeName,
            float walkSpeed,
            float runSpeed)
        {
            AnimatorControllerLayer layer = controller.layers.Single(item => item.name == layerName);
            BlendTree tree = layer.stateMachine.states
                .Select(item => item.state.motion)
                .OfType<BlendTree>()
                .Single(item => item.name == treeName);
            ChildMotion[] children = tree.children;

            Assert.AreEqual(0f, children[0].threshold, 0.001f);
            Assert.AreEqual(walkSpeed, children[1].threshold, 0.001f);
            Assert.AreEqual(runSpeed, children[2].threshold, 0.001f);
            Assert.AreEqual(1f, children[0].timeScale, 0.001f);
            Assert.AreEqual(1.28f, children[1].timeScale, 0.001f);
            Assert.AreEqual(2.95f, children[2].timeScale, 0.001f);
        }

        private static void AssertGunClipRootMatchesPrefab(
            string prefabPath,
            params AnimatorState[] states)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.NotNull(prefab, prefabPath);

            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.NotNull(animator, prefabPath);

            foreach (AnimatorState state in states)
            {
                Assert.IsInstanceOf<AnimationClip>(state.motion, state.name);
                var clip = (AnimationClip)state.motion;
                EditorCurveBinding[] rootBindings = AnimationUtility
                    .GetCurveBindings(clip)
                    .Where(binding => binding.type == typeof(Transform)
                        && string.IsNullOrEmpty(binding.path))
                    .ToArray();

                float[] sampleTimes = { 0f, clip.length * 0.5f, clip.length };
                AssertRootVectorCurves(
                    clip,
                    rootBindings,
                    "m_LocalScale",
                    animator.transform.localScale,
                    sampleTimes,
                    state.name);
                AssertRootVectorCurves(
                    clip,
                    rootBindings,
                    "m_LocalPosition",
                    animator.transform.localPosition,
                    sampleTimes,
                    state.name);
                AssertRootRotationCurves(
                    clip,
                    rootBindings,
                    animator.transform.localRotation,
                    sampleTimes,
                    state.name);
            }
        }

        private static void AssertRootVectorCurves(
            AnimationClip clip,
            EditorCurveBinding[] bindings,
            string propertyPrefix,
            Vector3 expected,
            float[] sampleTimes,
            string context)
        {
            string[] suffixes = { ".x", ".y", ".z" };
            for (int axis = 0; axis < suffixes.Length; axis++)
            {
                EditorCurveBinding? binding = bindings
                    .Cast<EditorCurveBinding?>()
                    .SingleOrDefault(candidate => candidate.Value.propertyName
                        == propertyPrefix + suffixes[axis]);
                if (!binding.HasValue)
                    continue;

                AnimationCurve curve = AnimationUtility.GetEditorCurve(
                    clip,
                    binding.Value);
                foreach (float time in sampleTimes)
                {
                    Assert.AreEqual(
                        expected[axis],
                        curve.Evaluate(time),
                        0.001f,
                        $"{context}: {propertyPrefix}{suffixes[axis]} does not match the gun prefab root.");
                }
            }
        }

        private static void AssertRootRotationCurves(
            AnimationClip clip,
            EditorCurveBinding[] bindings,
            Quaternion expected,
            float[] sampleTimes,
            string context)
        {
            string[] properties =
            {
                "m_LocalRotation.x",
                "m_LocalRotation.y",
                "m_LocalRotation.z",
                "m_LocalRotation.w"
            };
            AnimationCurve[] curves = properties
                .Select(property => bindings
                    .Where(binding => binding.propertyName == property)
                    .Select(binding => AnimationUtility.GetEditorCurve(clip, binding))
                    .SingleOrDefault())
                .ToArray();
            if (curves.Any(curve => curve == null))
                return;

            foreach (float time in sampleTimes)
            {
                var sampled = new Quaternion(
                    curves[0].Evaluate(time),
                    curves[1].Evaluate(time),
                    curves[2].Evaluate(time),
                    curves[3].Evaluate(time));
                Assert.Less(
                    Quaternion.Angle(expected, sampled),
                    0.1f,
                    $"{context}: root rotation does not match the gun prefab root.");
            }
        }
    }
}
