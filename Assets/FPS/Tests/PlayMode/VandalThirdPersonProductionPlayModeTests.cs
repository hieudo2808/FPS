using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace FPS.PlayModeTests
{
    public sealed class VandalThirdPersonProductionPlayModeTests
    {
        private const string PlayerPrefabPath =
            "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab";

        private static readonly string[] PlayerPrefabPaths =
        {
            PlayerPrefabPath,
            "Assets/FPS/Features/Characters/Content/Players/Sage/SagePlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Gekko/GekkoPlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Brimstone/BrimstonePlayer.prefab"
        };

        [UnityTest]
        public IEnumerator ProductionPlayers_InitializeAuthoredBodyProfileAndDedicatedGun()
        {
            foreach (string prefabPath in PlayerPrefabPaths)
            {
                PlayerMovement movement = null;
                yield return CreateIsolatedProductionPlayer(
                    value => movement = value,
                    prefabPath);

                Assert.NotNull(movement, prefabPath);
                PlayerVisibilityController visibility =
                    movement.GetComponent<PlayerVisibilityController>();
                Assert.NotNull(visibility, prefabPath);
                visibility.SetupVisibility(false);
                yield return null;

                Animator body = movement.CharacterAnimation;
                Assert.NotNull(body, prefabPath);
                Assert.True(body.isInitialized, prefabPath);
                bool isClove = prefabPath == PlayerPrefabPath;
                if (isClove)
                {
                    Assert.Null(body.avatar, prefabPath);
                    Transform[] transforms = body.GetComponentsInChildren<Transform>(true);
                    Assert.True(transforms.Any(item => item.name == "Skeleton"), prefabPath);
                    Assert.True(transforms.Any(item => item.name == "L_Hand"), prefabPath);
                    Assert.True(transforms.Any(item => item.name == "R_Hand"), prefabPath);
                }
                else
                {
                    Assert.NotNull(body.avatar, prefabPath);
                    Assert.True(body.avatar.isValid, prefabPath);
                    Assert.True(body.avatar.isHuman, prefabPath);
                    Assert.NotNull(body.GetBoneTransform(HumanBodyBones.Hips), prefabPath);
                    Assert.NotNull(body.GetBoneTransform(HumanBodyBones.Head), prefabPath);
                    Assert.NotNull(body.GetBoneTransform(HumanBodyBones.LeftHand), prefabPath);
                    Assert.NotNull(body.GetBoneTransform(HumanBodyBones.RightHand), prefabPath);
                }

                ThirdPersonLeftHandIK leftHandIK =
                    movement.GetComponent<ThirdPersonLeftHandIK>();
                Behaviour rigBuilder = body.GetComponents<Behaviour>()
                    .SingleOrDefault(component =>
                        component.GetType().FullName
                        == "UnityEngine.Animations.Rigging.RigBuilder");
                Assert.NotNull(leftHandIK, prefabPath);
                Assert.NotNull(rigBuilder, prefabPath);
                ThirdPersonWeaponPresentation vandalPresentation = visibility
                    .ThirdPersonWeaponPresentations
                    .Single(presentation => presentation.WeaponData.name == "Vandal");
                Assert.AreEqual(
                    vandalPresentation.UseLeftHandIK,
                    rigBuilder.enabled,
                    $"{prefabPath} must apply its authored Vandal support-hand rig policy.");

                GameObject thirdPersonVandal = visibility.ThirdPersonWeaponSlots[0];
                Assert.NotNull(thirdPersonVandal, prefabPath);
                Animator gun = thirdPersonVandal.GetComponentInChildren<Animator>(true);
                Assert.NotNull(gun, prefabPath);
                StringAssert.Contains(
                    "Vandal",
                    gun.runtimeAnimatorController.name,
                    prefabPath);
            }
        }

        [UnityTest]
        public IEnumerator ProductionPlayer_AllWeaponPresentationsSwitchBodyAndGunTogether()
        {
            PlayerMovement movement = null;
            yield return CreateIsolatedProductionPlayer(value => movement = value);

            PlayerVisibilityController visibility =
                movement.GetComponent<PlayerVisibilityController>();
            WeaponManager manager = movement.GetComponent<WeaponManager>();
            Assert.NotNull(visibility);
            Assert.NotNull(manager);
            visibility.SetupVisibility(false);

            PrimaryWeaponId[] primaryWeapons =
            {
                PrimaryWeaponId.Vandal,
                PrimaryWeaponId.Operator,
                PrimaryWeaponId.Odin,
                PrimaryWeaponId.Bucky
            };
            foreach (PrimaryWeaponId weaponId in primaryWeapons)
            {
                Assert.True(manager.TryReplacePrimaryWeaponServer(weaponId));
                yield return AssertActiveWeaponPresentation(
                    movement,
                    visibility,
                    manager,
                    weaponId.ToString());
            }

            manager.SetEquippedWeaponServer(1);
            visibility.RefreshWeaponPresentation(manager.CurrentWeaponIndex);
            yield return AssertActiveWeaponPresentation(
                movement,
                visibility,
                manager,
                "Classic");
        }

        [UnityTest]
        public IEnumerator ProductionPlayer_OdinUsesOriginalPathBoundMotionWithoutRuntimeIK()
        {
            PlayerMovement movement = null;
            yield return CreateIsolatedProductionPlayer(value => movement = value);

            PlayerVisibilityController visibility =
                movement.GetComponent<PlayerVisibilityController>();
            WeaponManager manager = movement.GetComponent<WeaponManager>();
            Assert.NotNull(visibility);
            Assert.NotNull(manager);
            visibility.SetupVisibility(false);

            Assert.True(manager.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Odin));
            visibility.RefreshWeaponPresentation(manager.CurrentWeaponIndex);
            yield return AssertActiveWeaponPresentation(
                movement,
                visibility,
                manager,
                "Odin");

            Behaviour rigBuilder = movement.CharacterAnimation
                .GetComponents<Behaviour>()
                .SingleOrDefault(component =>
                    component.GetType().FullName
                    == "UnityEngine.Animations.Rigging.RigBuilder");
            Assert.NotNull(
                rigBuilder,
                "The authored Body RigBuilder must remain on the player prefab.");
            Assert.False(
                rigBuilder.enabled,
                "Odin uses its original path-bound equip/reload motion and must "
                + "not enable the legacy runtime IK rig.");
        }

        [UnityTest]
        public IEnumerator ProductionPlayer_OperatorAimBlendsAndUsesRegularFire()
        {
            PlayerMovement movement = null;
            yield return CreateIsolatedProductionPlayer(value => movement = value);

            PlayerVisibilityController visibility =
                movement.GetComponent<PlayerVisibilityController>();
            WeaponManager manager = movement.GetComponent<WeaponManager>();
            Assert.NotNull(visibility);
            Assert.NotNull(manager);
            visibility.SetupVisibility(false);

            Assert.True(manager.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Operator));
            visibility.RefreshWeaponPresentation(manager.CurrentWeaponIndex);
            yield return null;

            ThirdPersonWeaponPresentation operatorPresentation = null;
            foreach (ThirdPersonWeaponPresentation presentation in
                visibility.ThirdPersonWeaponPresentations)
            {
                if (presentation?.WeaponData?.name == "Operator")
                {
                    operatorPresentation = presentation;
                    break;
                }
            }

            Assert.NotNull(operatorPresentation);
            Animator body = movement.CharacterAnimation;
            Animator gun = operatorPresentation.WeaponObject
                .GetComponentInChildren<Animator>(true);
            Assert.NotNull(body);
            Assert.NotNull(gun);

            visibility.SetThirdPersonAiming(true);
            yield return new WaitForSeconds(0.15f);
            Assert.True(body.GetCurrentAnimatorStateInfo(1).IsName("Operator Aim"));
            Assert.True(body.GetBool("Aiming"));
            Assert.True(gun.GetBool("Aiming"));

            visibility.SetThirdPersonAiming(false);
            yield return new WaitForSeconds(0.15f);
            Assert.True(body.GetCurrentAnimatorStateInfo(1).IsName("Operator Hold"));

            visibility.SetThirdPersonAiming(true);
            yield return new WaitForSeconds(0.15f);
            manager.TriggerAnimation("Fire");
            yield return null;
            Assert.True(body.GetCurrentAnimatorStateInfo(1).IsName(
                "Operator Fire"));
            Assert.True(gun.GetCurrentAnimatorStateInfo(0).IsName("Fire"));

            manager.TriggerAnimation("Reload");
            yield return null;
            Assert.False(body.GetBool("Aiming"));
            Assert.False(gun.GetBool("Aiming"));
            Assert.True(body.GetCurrentAnimatorStateInfo(1).IsName("Operator Reload"));
            Assert.True(gun.GetCurrentAnimatorStateInfo(0).IsName("Reload"));
        }

        [UnityTest]
        public IEnumerator ProductionPlayer_OperatorReloadRuntimePoseProbe()
        {
#if UNITY_EDITOR
            PlayerMovement movement = null;
            yield return CreateIsolatedProductionPlayer(value => movement = value);

            PlayerVisibilityController visibility =
                movement.GetComponent<PlayerVisibilityController>();
            WeaponManager manager = movement.GetComponent<WeaponManager>();
            Assert.NotNull(visibility);
            Assert.NotNull(manager);
            visibility.SetupVisibility(false);
            Assert.True(manager.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Operator));
            visibility.RefreshWeaponPresentation(manager.CurrentWeaponIndex);
            yield return null;

            Animator body = movement.CharacterAnimation;
            Assert.NotNull(body);
            body.SetBool("Grounded", true);
            body.SetBool("FreeFall", false);
            body.SetFloat("Speed", 7f);
            manager.TriggerAnimation("Reload");
            yield return WaitForState(body, 1, "Operator Reload");

            const string reloadClipPath =
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/"
                + "S0/3P/Anims/Clove_GenericPathBound/Operator_Reload_PathBound.anim";
            AnimationClip reloadClip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(reloadClipPath);
            GameObject authoredPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.NotNull(reloadClip);
            Assert.NotNull(authoredPrefab);

            string[] probeNames =
            {
                "MasterWeaponAim", "MasterWeapon", "R_WeaponMaster",
                "Spine1", "Spine2", "Spine3", "Spine4", "Neck", "Head",
                "L_Clavicle", "L_Shoulder", "L_Elbow", "L_Hand",
                "R_Clavicle", "R_Shoulder", "R_Elbow", "R_Hand"
            };
            Transform[] runtimeBones = probeNames.Select(name => body
                    .GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == name))
                .ToArray();

            GameObject directPreview = Object.Instantiate(authoredPrefab);
            directPreview.name = "ClovePlayer_OperatorReload_DirectClip";
            Animator directBody = directPreview.GetComponent<PlayerMovement>()
                .CharacterAnimation;
            directBody.enabled = false;
            Transform[] directBones = probeNames.Select(name => directBody
                    .GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == name))
                .ToArray();

            var lines = new System.Collections.Generic.List<string>();
            try
            {
                foreach (float sample in new[] { 0.15f, 0.35f, 0.55f, 0.75f })
                {
                    body.SetLayerWeight(2, 1f);
                    body.Play("Operator Reload", 1, sample);
                    body.Update(0.0001f);
                    Vector3[] positionsWithMovement = runtimeBones
                        .Select(transform => transform.localPosition)
                        .ToArray();
                    Quaternion[] rotationsWithMovement = runtimeBones
                        .Select(transform => transform.localRotation)
                        .ToArray();

                    body.SetLayerWeight(2, 0f);
                    body.Update(0f);
                    reloadClip.SampleAnimation(
                        directBody.gameObject,
                        sample * reloadClip.length);

                    float maximumMovementPosition = 0f;
                    float maximumMovementRotation = 0f;
                    string maximumMovementBone = string.Empty;
                    float maximumDirectPosition = 0f;
                    float maximumDirectRotation = 0f;
                    string maximumDirectBone = string.Empty;
                    for (int index = 0; index < runtimeBones.Length; index++)
                    {
                        float movementPosition = Vector3.Distance(
                            positionsWithMovement[index],
                            runtimeBones[index].localPosition);
                        float movementRotation = Quaternion.Angle(
                            rotationsWithMovement[index],
                            runtimeBones[index].localRotation);
                        if (movementPosition > maximumMovementPosition
                            || movementRotation > maximumMovementRotation)
                        {
                            maximumMovementPosition = Mathf.Max(
                                maximumMovementPosition,
                                movementPosition);
                            maximumMovementRotation = Mathf.Max(
                                maximumMovementRotation,
                                movementRotation);
                            maximumMovementBone = probeNames[index];
                        }

                        float directPosition = Vector3.Distance(
                            runtimeBones[index].localPosition,
                            directBones[index].localPosition);
                        float directRotation = Quaternion.Angle(
                            runtimeBones[index].localRotation,
                            directBones[index].localRotation);
                        if (directPosition > maximumDirectPosition
                            || directRotation > maximumDirectRotation)
                        {
                            maximumDirectPosition = Mathf.Max(
                                maximumDirectPosition,
                                directPosition);
                            maximumDirectRotation = Mathf.Max(
                                maximumDirectRotation,
                                directRotation);
                            maximumDirectBone = probeNames[index];
                        }
                    }

                    lines.Add(
                        $"sample={sample:F2} movement="
                        + $"{maximumMovementPosition:F5}m/"
                        + $"{maximumMovementRotation:F2}deg({maximumMovementBone}) "
                        + $"runtimeWithoutMovementVsDirect="
                        + $"{maximumDirectPosition:F5}m/"
                        + $"{maximumDirectRotation:F2}deg({maximumDirectBone})");
                    Assert.True(float.IsFinite(maximumMovementPosition));
                    Assert.True(float.IsFinite(maximumMovementRotation));
                    Assert.True(float.IsFinite(maximumDirectPosition));
                    Assert.True(float.IsFinite(maximumDirectRotation));
                }
            }
            finally
            {
                body.SetLayerWeight(2, 1f);
                Object.Destroy(directPreview);
            }

            string logDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName,
                "Logs");
            System.IO.Directory.CreateDirectory(logDirectory);
            System.IO.File.WriteAllLines(
                System.IO.Path.Combine(logDirectory, "OperatorReloadRuntimeProbe.txt"),
                lines);
#else
            Assert.Fail("Operator runtime pose probing requires the Unity Editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ProductionPlayer_OperatorReloadKeepsBodyAndGunInSync()
        {
            PlayerMovement movement = null;
            yield return CreateIsolatedProductionPlayer(value => movement = value);

            PlayerVisibilityController visibility =
                movement.GetComponent<PlayerVisibilityController>();
            WeaponManager manager = movement.GetComponent<WeaponManager>();
            Assert.NotNull(visibility);
            Assert.NotNull(manager);
            visibility.SetupVisibility(false);
            Assert.True(manager.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Operator));
            visibility.RefreshWeaponPresentation(manager.CurrentWeaponIndex);
            yield return null;

            Animator body = movement.CharacterAnimation;
            ThirdPersonWeaponPresentation presentation = visibility
                .ThirdPersonWeaponPresentations
                .Single(item => item.WeaponData.name == "Operator");
            Animator gun = presentation.WeaponObject
                .GetComponentInChildren<Animator>(true);
            Assert.NotNull(body);
            Assert.NotNull(gun);

            body.SetBool("Grounded", true);
            body.SetBool("FreeFall", false);
            body.SetFloat("Speed", 7f);
            Weapon operatorWeapon = manager.CurrentWeapon.GetComponent<Weapon>();
            Assert.NotNull(operatorWeapon);
            operatorWeapon.SetLocalAmmoState(
                operatorWeapon.CurrentAmmo,
                operatorWeapon.ReservedAmmo,
                true);
            yield return WaitForState(body, 1, "Operator Reload");
            yield return WaitForState(gun, 0, "Reload");

            float initialBodyTime = body.GetCurrentAnimatorStateInfo(1).normalizedTime;
            float initialGunTime = gun.GetCurrentAnimatorStateInfo(0).normalizedTime;
            yield return new WaitForSeconds(0.5f);

            AnimatorStateInfo bodyReload = body.GetCurrentAnimatorStateInfo(1);
            AnimatorStateInfo gunReload = gun.GetCurrentAnimatorStateInfo(0);
            Assert.True(bodyReload.IsName("Operator Reload"));
            Assert.True(gunReload.IsName("Reload"));
            Assert.Greater(bodyReload.normalizedTime - initialBodyTime, 0.05f);
            Assert.Greater(gunReload.normalizedTime - initialGunTime, 0.05f);
            Assert.Less(
                Mathf.Abs(bodyReload.normalizedTime - gunReload.normalizedTime),
                0.03f,
                "Operator gun Reload must not run ahead of the paired 3P Body Reload.");

            float completionDeadline = Time.realtimeSinceStartup + 5f;
            while ((body.GetCurrentAnimatorStateInfo(1).normalizedTime < 0.99f
                    || gun.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.99f)
                && Time.realtimeSinceStartup < completionDeadline)
            {
                Assert.True(
                    body.GetCurrentAnimatorStateInfo(1).IsName("Operator Reload"),
                    "Operator Body Reload must wait for ReloadComplete.");
                Assert.True(
                    gun.GetCurrentAnimatorStateInfo(0).IsName("Reload"),
                    "Operator gun Reload must wait for ReloadComplete.");
                Assert.Less(
                    Mathf.Abs(body.GetCurrentAnimatorStateInfo(1).normalizedTime
                        - gun.GetCurrentAnimatorStateInfo(0).normalizedTime),
                    0.03f,
                    "Operator Body and gun must remain synchronized until completion.");
                yield return null;
            }

            Assert.GreaterOrEqual(
                body.GetCurrentAnimatorStateInfo(1).normalizedTime,
                0.99f);
            Assert.GreaterOrEqual(
                gun.GetCurrentAnimatorStateInfo(0).normalizedTime,
                0.99f);
            operatorWeapon.SetLocalAmmoState(
                operatorWeapon.CurrentAmmo,
                operatorWeapon.ReservedAmmo,
                false);

            Transform rightHand = body.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "R_Hand");
            Transform leftHand = body.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "L_Hand");
            Transform trigger = presentation.WeaponObject
                .GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "Trigger");
            Transform support = presentation.WeaponObject
                .GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "Left_Hand_Target");

            float exitDeadline = Time.realtimeSinceStartup + 5f;
            while ((body.IsInTransition(1)
                    || !body.GetCurrentAnimatorStateInfo(1).IsName("Operator Hold")
                    || gun.IsInTransition(0)
                    || !gun.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
                && Time.realtimeSinceStartup < exitDeadline)
            {
                bool bodyStillInReloadFlow =
                    body.GetCurrentAnimatorStateInfo(1).IsName("Operator Reload")
                    || (body.IsInTransition(1)
                        && body.GetNextAnimatorStateInfo(1).IsName("Operator Hold"));
                bool gunStillInReloadFlow =
                    gun.GetCurrentAnimatorStateInfo(0).IsName("Reload")
                    || (gun.IsInTransition(0)
                        && gun.GetNextAnimatorStateInfo(0).IsName("Idle"));
                Assert.AreEqual(
                    bodyStillInReloadFlow,
                    gunStillInReloadFlow,
                    "Operator Body and gun must enter and leave Reload together; "
                    + "otherwise the gun snaps to Idle while the hands are still "
                    + "blending out of Reload.");
                yield return null;
            }

            AnimatorStateInfo bodyAfterReload =
                body.GetCurrentAnimatorStateInfo(1);
            AnimatorStateInfo gunAfterReload =
                gun.GetCurrentAnimatorStateInfo(0);
            Assert.False(
                body.IsInTransition(1),
                "Operator Body must finish Reload -> Hold instead of remaining in transition.");
            Assert.False(
                gun.IsInTransition(0),
                "Operator gun must finish Reload -> Idle instead of remaining in transition.");
            Assert.True(
                bodyAfterReload.IsName("Operator Hold"),
                "Operator Body must return to Hold after Reload.");
            Assert.True(
                gunAfterReload.IsName("Idle"),
                "Operator gun must return to Idle after Reload.");
            AssertThirdPersonGrip(
                rightHand,
                trigger,
                leftHand,
                support,
                "Operator after Reload",
                0.6f);

            manager.TriggerAnimation("Reload");
            yield return WaitForState(body, 1, "Operator Reload");
            yield return WaitForState(gun, 0, "Reload");
        }

        [UnityTest]
        public IEnumerator ProductionPlayer_LocomotionAndFireDriveBodyAndDedicatedGun()
        {
            PlayerMovement movement = null;
            yield return CreateIsolatedProductionPlayer(value => movement = value);
            Assert.NotNull(movement);
            PlayerVisibilityController visibility =
                movement.GetComponent<PlayerVisibilityController>();
            WeaponManager manager = movement.GetComponent<WeaponManager>();
            Assert.NotNull(visibility);
            Assert.NotNull(manager);

            visibility.SetupVisibility(false);
            Animator body = movement.CharacterAnimation;
            Animator gun = visibility.ThirdPersonWeaponSlots[0]
                .GetComponentInChildren<Animator>(true);
            Assert.NotNull(body);
            Assert.NotNull(gun);
            Assert.AreEqual("CloveVandal_GenericPathBound", body.runtimeAnimatorController.name);
            Assert.AreEqual("CloveVandal3P_GNTP", gun.runtimeAnimatorController.name);

            ThirdPersonWeaponPresentation vandalPresentation = visibility
                .ThirdPersonWeaponPresentations
                .Single(presentation => presentation.WeaponData.name == "Vandal");
            Transform rightHand = body.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "R_Hand");
            Transform leftHand = body.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "L_Hand");
            Transform trigger = vandalPresentation.WeaponObject
                .GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "Trigger");
            Transform support = vandalPresentation.WeaponObject
                .GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "Left_Hand_Target");

            body.SetBool("Grounded", true);
            body.SetBool("FreeFall", false);
            body.SetFloat("Speed", 7f);
            yield return null;
            Assert.True(body.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
            AssertThirdPersonGrip(rightHand, trigger, leftHand, support, "Walk");

            manager.TriggerAnimation("Fire");
            yield return null;
            Assert.True(body.GetCurrentAnimatorStateInfo(3).IsName("Vandal Fire"));
            Assert.True(gun.GetCurrentAnimatorStateInfo(0).IsName("Fire"));
            Assert.Less(
                Mathf.Abs(body.GetCurrentAnimatorStateInfo(3).normalizedTime
                    - gun.GetCurrentAnimatorStateInfo(0).normalizedTime),
                0.15f);
            AssertThirdPersonGrip(
                rightHand,
                trigger,
                leftHand,
                support,
                "Walk + Fire");
        }

        [UnityTest]
        public IEnumerator ProductionPlayer_ReloadCancelsRuntimeFirePresentationForEveryWeapon()
        {
            PlayerMovement movement = null;
            yield return CreateIsolatedProductionPlayer(value => movement = value);

            PlayerVisibilityController visibility =
                movement.GetComponent<PlayerVisibilityController>();
            WeaponManager manager = movement.GetComponent<WeaponManager>();
            Assert.NotNull(visibility);
            Assert.NotNull(manager);

            visibility.SetupVisibility(false);
            yield return null;

            string[] weaponNames =
            {
                "Vandal", "Operator", "Odin", "Bucky", "Classic"
            };
            foreach (string weaponName in weaponNames)
            {
                if (weaponName == "Classic")
                {
                    manager.SetEquippedWeaponServer(1);
                }
                else
                {
                    manager.SetEquippedWeaponServer(0);
                    Assert.True(System.Enum.TryParse(
                        weaponName,
                        out PrimaryWeaponId primaryWeapon));
                    Assert.True(manager.TryReplacePrimaryWeaponServer(primaryWeapon));
                }

                visibility.RefreshWeaponPresentation(manager.CurrentWeaponIndex);
                yield return null;

                ThirdPersonWeaponPresentation presentation = visibility
                    .ThirdPersonWeaponPresentations
                    .Single(candidate => candidate.WeaponData.name == weaponName);
                Animator body = movement.CharacterAnimation;
                Animator gun = presentation.WeaponObject
                    .GetComponentInChildren<Animator>(true);
                Assert.NotNull(body, weaponName);
                Assert.NotNull(gun, weaponName);

                int fireAdditiveLayer = Enumerable.Range(0, body.layerCount)
                    .Where(layer => body.GetLayerName(layer).IndexOf(
                        "Fire Additive",
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                    .DefaultIfEmpty(-1)
                    .First();

                manager.TriggerAnimation("Fire");
                yield return null;
                AssertAnimatorInState(body, weaponName + " Fire");
                Assert.True(
                    gun.GetCurrentAnimatorStateInfo(0).IsName("Fire"),
                    weaponName);

                manager.TriggerAnimation("Reload");
                yield return WaitForState(body, 1, weaponName + " Reload");
                yield return WaitForState(gun, 0, "Reload");
                yield return null;

                if (fireAdditiveLayer >= 0)
                {
                    Assert.True(
                        body.GetCurrentAnimatorStateInfo(fireAdditiveLayer)
                            .IsName("Zero"),
                        weaponName + " left its Fire Additive pose active during Reload.");
                }
                Assert.True(
                    body.GetCurrentAnimatorStateInfo(1)
                        .IsName(weaponName + " Reload"),
                    weaponName);
                Assert.True(
                    gun.GetCurrentAnimatorStateInfo(0).IsName("Reload"),
                    weaponName);
            }
        }

        [UnityTest]
        public IEnumerator AllFiveWeapons_BodyAndGunWaitForTheSameActionCompletionEdge()
        {
            PlayerMovement movement = null;
            yield return CreateIsolatedProductionPlayer(value => movement = value);

            PlayerVisibilityController visibility =
                movement.GetComponent<PlayerVisibilityController>();
            WeaponManager manager = movement.GetComponent<WeaponManager>();
            Assert.NotNull(visibility);
            Assert.NotNull(manager);
            visibility.SetupVisibility(false);
            yield return null;

            string[] weaponNames =
            {
                "Vandal", "Operator", "Odin", "Bucky", "Classic"
            };
            foreach (string weaponName in weaponNames)
            {
                if (weaponName == "Classic")
                {
                    manager.SetEquippedWeaponServer(1);
                }
                else
                {
                    manager.SetEquippedWeaponServer(0);
                    Assert.True(System.Enum.TryParse(
                        weaponName,
                        out PrimaryWeaponId primaryWeapon));
                    Assert.True(manager.TryReplacePrimaryWeaponServer(
                        primaryWeapon));
                }

                visibility.RefreshWeaponPresentation(
                    manager.CurrentWeaponIndex);
                yield return null;

                ThirdPersonWeaponPresentation presentation = visibility
                    .ThirdPersonWeaponPresentations
                    .Single(candidate =>
                        candidate.WeaponData.name == weaponName);
                Animator body = movement.CharacterAnimation;
                Animator gun = presentation.WeaponObject
                    .GetComponentInChildren<Animator>(true);
                body.SetBool("Grounded", true);
                body.SetBool("FreeFall", false);
                body.SetFloat("Speed", 7f);

                const float acceleratedDuration = 0.14f;
                manager.ConfigureThirdPersonActionDuration(
                    "Equip",
                    acceleratedDuration);
                manager.TriggerAnimation("Equip");
                yield return WaitForState(
                    body,
                    1,
                    weaponName + " Equip");
                yield return WaitForState(gun, 0, "Equip");
                yield return new WaitForSeconds(
                    acceleratedDuration + 0.04f);
                Assert.True(
                    body.GetCurrentAnimatorStateInfo(1)
                        .IsName(weaponName + " Equip"),
                    weaponName + " Body exited Equip without EquipComplete.");
                Assert.True(
                    gun.GetCurrentAnimatorStateInfo(0).IsName("Equip"),
                    weaponName + " gun exited Equip without EquipComplete.");
                Assert.Less(
                    Mathf.Abs(
                        body.GetCurrentAnimatorStateInfo(1).normalizedTime
                        - gun.GetCurrentAnimatorStateInfo(0).normalizedTime),
                    0.08f,
                    weaponName + " Equip Body/gun phase mismatch.");
                manager.TriggerAnimation("EquipComplete");
                yield return WaitForState(
                    body,
                    1,
                    weaponName + " Hold");
                yield return WaitForState(gun, 0, "Idle");

                manager.ConfigureThirdPersonActionDuration(
                    "Reload",
                    acceleratedDuration);
                manager.TriggerAnimation("Reload");
                yield return WaitForState(
                    body,
                    1,
                    weaponName + " Reload");
                yield return WaitForState(gun, 0, "Reload");
                yield return new WaitForSeconds(
                    acceleratedDuration + 0.04f);
                Assert.True(
                    body.GetCurrentAnimatorStateInfo(1)
                        .IsName(weaponName + " Reload"),
                    weaponName + " Body exited Reload without ReloadComplete.");
                Assert.True(
                    gun.GetCurrentAnimatorStateInfo(0).IsName("Reload"),
                    weaponName + " gun exited Reload without ReloadComplete.");
                Assert.Less(
                    Mathf.Abs(
                        body.GetCurrentAnimatorStateInfo(1).normalizedTime
                        - gun.GetCurrentAnimatorStateInfo(0).normalizedTime),
                    0.08f,
                    weaponName + " Reload Body/gun phase mismatch.");
                manager.TriggerAnimation("ReloadComplete");
                yield return WaitForState(
                    body,
                    1,
                    weaponName + " Hold");
                yield return WaitForState(gun, 0, "Idle");

                // Prove the previous completion trigger was consumed/reset.
                manager.ConfigureThirdPersonActionDuration(
                    "Reload",
                    acceleratedDuration);
                manager.TriggerAnimation("Reload");
                yield return WaitForState(
                    body,
                    1,
                    weaponName + " Reload");
                yield return WaitForState(gun, 0, "Reload");
                yield return null;
                Assert.True(
                    body.GetCurrentAnimatorStateInfo(1)
                        .IsName(weaponName + " Reload"),
                    weaponName + " Body consumed a stale ReloadComplete.");
                Assert.True(
                    gun.GetCurrentAnimatorStateInfo(0).IsName("Reload"),
                    weaponName + " gun consumed a stale ReloadComplete.");
                manager.TriggerAnimation("ReloadComplete");
                yield return WaitForState(
                    body,
                    1,
                    weaponName + " Hold");
                yield return WaitForState(gun, 0, "Idle");
            }
        }

        [UnityTest]
        public IEnumerator AcceptedReloadInput_DrivesVandalBodyAndGunThirdPersonReload()
        {
            PlayerMovement movement = null;
            yield return CreateIsolatedProductionPlayer(value => movement = value);

            Assert.NotNull(movement);
            PlayerVisibilityController visibility =
                movement.GetComponent<PlayerVisibilityController>();
            WeaponManager manager = movement.GetComponent<WeaponManager>();
            Assert.NotNull(visibility);
            Assert.NotNull(manager);

            visibility.SetupVisibility(false);
            yield return null;

            Animator body = movement.CharacterAnimation;
            Animator gun = visibility.ThirdPersonWeaponSlots[0]
                .GetComponentInChildren<Animator>(true);
            Assert.NotNull(body);
            Assert.NotNull(gun);

            // InputManager owns the physical R binding. Once gameplay accepts that
            // reload request, Weapon uses this bridge to drive both 3P animators.
            manager.TriggerAnimation("Reload");
            yield return WaitForState(body, 1, "Vandal Reload");
            yield return WaitForState(gun, 0, "Reload");

            Assert.Less(
                Mathf.Abs(body.GetCurrentAnimatorStateInfo(1).normalizedTime
                    - gun.GetCurrentAnimatorStateInfo(0).normalizedTime),
                0.08f,
                "Vandal Body 3P and gun 3P reload must start in sync.");
        }

        [UnityTest]
        public IEnumerator MovingReload_KeepsVandalBodyAndGunSynchronizedThroughImpactFrames()
        {
            PlayerMovement movement = null;
            yield return CreateIsolatedProductionPlayer(value => movement = value);

            Assert.NotNull(movement);
            PlayerVisibilityController visibility =
                movement.GetComponent<PlayerVisibilityController>();
            WeaponManager manager = movement.GetComponent<WeaponManager>();
            Assert.NotNull(visibility);
            Assert.NotNull(manager);

            visibility.SetupVisibility(false);
            yield return null;

            Animator body = movement.CharacterAnimation;
            ThirdPersonWeaponPresentation vandal = visibility
                .ThirdPersonWeaponPresentations
                .Single(presentation => presentation.WeaponData.name == "Vandal");
            Animator gun = vandal.WeaponObject.GetComponentInChildren<Animator>(true);
            Transform rightHand = body.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "R_Hand");
            Transform leftHand = body.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "L_Hand");
            Transform trigger = vandal.WeaponObject
                .GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "Trigger");
            Transform support = vandal.WeaponObject
                .GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "Left_Hand_Target");
            Transform mainMagazine = vandal.WeaponObject
                .GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "Magazine_Main");
            Transform extraMagazine = vandal.WeaponObject
                .GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "Magazine_Extra");
            string[] deformationBoneNames =
            {
                "Spine1",
                "Spine2",
                "Spine3",
                "Spine4",
                "L_Clavicle",
                "L_Shoulder",
                "L_Elbow",
                "R_Clavicle",
                "R_Shoulder",
                "R_Elbow"
            };
            Transform[] deformationBones = deformationBoneNames
                .Select(name => body.GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == name))
                .ToArray();
#if UNITY_EDITOR
            GameObject authoredPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PlayerPrefabPath);
            Transform authoredBody = authoredPrefab.transform.Find("Body");
            Assert.NotNull(authoredBody);
            Vector3[] authoredBonePositions = deformationBoneNames
                .Select(name => authoredBody
                    .GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == name)
                    .localPosition)
                .ToArray();
            GameObject directPreview = Object.Instantiate(authoredPrefab);
            directPreview.name = "ClovePlayer_3P_DirectClipReference";
            Animator directPreviewBody = directPreview.GetComponent<PlayerMovement>()
                .CharacterAnimation;
            directPreviewBody.enabled = false;
            Transform[] directPreviewBones = deformationBoneNames
                .Select(name => directPreviewBody
                    .GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == name))
                .ToArray();
            AnimationClip directReloadClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Vandal/"
                + "S0/3P/Anims/Clove_GenericPathBound/"
                + "TP_Core_AK_S0_Reload_UB_c3e94e17_PathBound.anim");
            Assert.NotNull(directReloadClip);
#endif

            body.SetBool("Grounded", true);
            body.SetBool("FreeFall", false);
            body.SetFloat("Speed", 7f);
            yield return null;

            Weapon currentWeapon = manager.CurrentWeapon.GetComponent<Weapon>();
            Assert.NotNull(currentWeapon);
            currentWeapon.SetLocalAmmoState(
                Mathf.Max(0, currentWeapon.CurrentAmmo - 1),
                currentWeapon.ReservedAmmo,
                true);
            Assert.True(currentWeapon.IsReloading);
            yield return WaitForState(body, 1, "Vandal Reload");
            yield return WaitForState(gun, 0, "Reload");

            float previousBodyTime = -1f;
            float previousGunTime = -1f;
            var probeLines = new System.Collections.Generic.List<string>();
            foreach (float sampleTime in new[] { 0.25f, 0.5f, 0.75f })
            {
                float deadline = Time.realtimeSinceStartup + 1.5f;
                while (body.GetCurrentAnimatorStateInfo(1).IsName("Vandal Reload")
                    && body.GetCurrentAnimatorStateInfo(1).normalizedTime < sampleTime
                    && Time.realtimeSinceStartup < deadline)
                {
                    AnimatorStateInfo bodyFrame =
                        body.GetCurrentAnimatorStateInfo(1);
                    AnimatorStateInfo gunFrame = gun.GetCurrentAnimatorStateInfo(0);
                    Assert.That(
                        bodyFrame.normalizedTime + 0.02f,
                        Is.GreaterThanOrEqualTo(previousBodyTime),
                        "Vandal Body reload restarted while advancing.");
                    Assert.That(
                        gunFrame.normalizedTime + 0.02f,
                        Is.GreaterThanOrEqualTo(previousGunTime),
                        "Vandal gun reload restarted while advancing.");
                    previousBodyTime = bodyFrame.normalizedTime;
                    previousGunTime = gunFrame.normalizedTime;
                    yield return null;
                }

                AnimatorStateInfo bodyState = body.GetCurrentAnimatorStateInfo(1);
                AnimatorStateInfo gunState = gun.GetCurrentAnimatorStateInfo(0);
                Assert.True(bodyState.IsName("Vandal Reload"));
                Assert.True(gunState.IsName("Reload"));
                Assert.Less(
                    Mathf.Abs(bodyState.normalizedTime - gunState.normalizedTime),
                    0.08f,
                    $"Vandal reload timelines diverged at {sampleTime:P0}.");

                float right = Vector3.Distance(rightHand.position, trigger.position);
                float left = Vector3.Distance(leftHand.position, support.position);
                float handGap = Vector3.Distance(rightHand.position, leftHand.position);
                float leftToMainMagazine = Vector3.Distance(
                    leftHand.position,
                    mainMagazine.position);
                float leftToExtraMagazine = Vector3.Distance(
                    leftHand.position,
                    extraMagazine.position);
                Vector3 weaponWithMovement = vandal.WeaponObject.transform.position;
                Quaternion weaponRotationWithMovement =
                    vandal.WeaponObject.transform.rotation;
                Vector3[] positionsWithMovement = deformationBones
                    .Select(transform => transform.localPosition)
                    .ToArray();
                Quaternion[] rotationsWithMovement = deformationBones
                    .Select(transform => transform.localRotation)
                    .ToArray();
                body.SetLayerWeight(2, 0f);
                body.Update(0f);
                float movementWeaponPositionDelta = Vector3.Distance(
                    weaponWithMovement,
                    vandal.WeaponObject.transform.position);
                float movementWeaponRotationDelta = Quaternion.Angle(
                    weaponRotationWithMovement,
                    vandal.WeaponObject.transform.rotation);
                float maximumBoneLengthRatio = 1f;
                float maximumBonePositionDelta = 0f;
                float maximumBoneRotationDelta = 0f;
                string maximumBoneName = string.Empty;
#if UNITY_EDITOR
                float maximumAuthoredBoneLengthRatio = 1f;
                string maximumAuthoredBoneName = string.Empty;
                directReloadClip.SampleAnimation(
                    directPreviewBody.gameObject,
                    Mathf.Clamp01(bodyState.normalizedTime)
                        * directReloadClip.length);
                float maximumDirectPreviewRotationDelta = 0f;
                float maximumDirectPreviewPositionDelta = 0f;
                string maximumDirectPreviewBoneName = string.Empty;
#endif
                for (int boneIndex = 0;
                     boneIndex < deformationBones.Length;
                     boneIndex++)
                {
                    Transform bone = deformationBones[boneIndex];
                    float referenceLength = Mathf.Max(
                        bone.localPosition.magnitude,
                        0.000001f);
                    float lengthRatio = positionsWithMovement[boneIndex].magnitude
                        / referenceLength;
                    float positionDelta = Vector3.Distance(
                        positionsWithMovement[boneIndex],
                        bone.localPosition);
                    float rotationDelta = Quaternion.Angle(
                        rotationsWithMovement[boneIndex],
                        bone.localRotation);
                    float symmetricLengthRatio = Mathf.Max(
                        lengthRatio,
                        1f / Mathf.Max(lengthRatio, 0.000001f));
                    if (symmetricLengthRatio > maximumBoneLengthRatio)
                    {
                        maximumBoneLengthRatio = symmetricLengthRatio;
                        maximumBoneName = bone.name;
                    }
                    maximumBonePositionDelta = Mathf.Max(
                        maximumBonePositionDelta,
                        positionDelta);
                    maximumBoneRotationDelta = Mathf.Max(
                        maximumBoneRotationDelta,
                        rotationDelta);
#if UNITY_EDITOR
                    float authoredLength = Mathf.Max(
                        authoredBonePositions[boneIndex].magnitude,
                        0.000001f);
                    float authoredRatio = positionsWithMovement[boneIndex].magnitude
                        / authoredLength;
                    float symmetricAuthoredRatio = Mathf.Max(
                        authoredRatio,
                        1f / Mathf.Max(authoredRatio, 0.000001f));
                    if (symmetricAuthoredRatio > maximumAuthoredBoneLengthRatio)
                    {
                        maximumAuthoredBoneLengthRatio = symmetricAuthoredRatio;
                        maximumAuthoredBoneName = bone.name;
                    }
                    float previewRotationDelta = Quaternion.Angle(
                        rotationsWithMovement[boneIndex],
                        directPreviewBones[boneIndex].localRotation);
                    float previewPositionDelta = Vector3.Distance(
                        positionsWithMovement[boneIndex],
                        directPreviewBones[boneIndex].localPosition);
                    if (previewRotationDelta > maximumDirectPreviewRotationDelta)
                    {
                        maximumDirectPreviewRotationDelta = previewRotationDelta;
                        maximumDirectPreviewBoneName = bone.name;
                    }
                    maximumDirectPreviewPositionDelta = Mathf.Max(
                        maximumDirectPreviewPositionDelta,
                        previewPositionDelta);
#endif
                }
                body.SetLayerWeight(2, 1f);
                body.Update(0f);
                string probeLine =
                    $"[VandalReloadProbe] sample={sampleTime:F2} "
                    + $"body={bodyState.normalizedTime:F3} "
                    + $"gun={gunState.normalizedTime:F3} "
                    + $"right={right:F3} left={left:F3} handGap={handGap:F3} "
                    + $"leftMainMag={leftToMainMagazine:F3} "
                    + $"leftExtraMag={leftToExtraMagazine:F3} "
                    + $"moveWeaponDelta={movementWeaponPositionDelta:F3}m/"
                    + $"{movementWeaponRotationDelta:F1}deg "
                    + $"maxBoneLengthRatio={maximumBoneLengthRatio:F2}x"
                    + $"({maximumBoneName}) "
                    + $"maxBonePositionDelta={maximumBonePositionDelta:F4} "
                    + $"maxBoneRotationDelta={maximumBoneRotationDelta:F1}deg "
#if UNITY_EDITOR
                    + $"maxAuthoredLengthRatio={maximumAuthoredBoneLengthRatio:F2}x"
                    + $"({maximumAuthoredBoneName}) "
                    + $"runtimeVsDirect={maximumDirectPreviewRotationDelta:F1}deg"
                    + $"({maximumDirectPreviewBoneName})/"
                    + $"{maximumDirectPreviewPositionDelta:F4}pos "
#endif
                    + $"movementLayer={body.GetCurrentAnimatorStateInfo(2).normalizedTime:F3}";
                Debug.Log(probeLine);
                probeLines.Add(probeLine);
                Assert.True(float.IsFinite(right));
                Assert.True(float.IsFinite(left));
                Assert.True(float.IsFinite(handGap));
                Assert.True(float.IsFinite(leftToMainMagazine));
                Assert.True(float.IsFinite(leftToExtraMagazine));
                Assert.True(float.IsFinite(movementWeaponPositionDelta));
                Assert.True(float.IsFinite(movementWeaponRotationDelta));
                Assert.Less(
                    leftToMainMagazine,
                    0.30f,
                    $"Vandal reload hand missed the animated main magazine at {sampleTime:P0}.");
                Assert.Less(
                    movementWeaponPositionDelta,
                    0.05f,
                    $"Locomotion displaced Vandal during reload at {sampleTime:P0}.");
                Assert.Less(
                    movementWeaponRotationDelta,
                    5f,
                    $"Locomotion rotated Vandal during reload at {sampleTime:P0}.");
                Assert.Less(
                    right,
                    0.35f,
                    $"Vandal firing hand detached at {sampleTime:P0} reload.");
                Assert.Less(
                    handGap,
                    0.85f,
                    $"Vandal reload tore the two hands apart at {sampleTime:P0}.");
            }

#if UNITY_EDITOR
            Object.Destroy(directPreview);
            string logDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName,
                "Logs");
            System.IO.Directory.CreateDirectory(logDirectory);
            System.IO.File.WriteAllLines(
                System.IO.Path.Combine(logDirectory, "VandalReloadProbe.txt"),
                probeLines);
#endif
        }

        [UnityTest]
        public IEnumerator TestSceneF5Player_ReloadAdvancesWithoutConflictingBodyAnimator()
        {
#if UNITY_EDITOR
            Scene testScene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/FPS/Scenes/TestScene.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            try
            {
#endif
            PlayerMovement movement = Object
                .FindObjectsByType<PlayerMovement>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .SingleOrDefault(candidate =>
                    candidate.name == "ClovePlayer_3P_Test");
            Assert.NotNull(
                movement,
                "The authored TestScene ClovePlayer_3P_Test instance was not loaded.");

            movement.gameObject.SetActive(true);
            PlayerVisibilityController visibility =
                movement.GetComponent<PlayerVisibilityController>();
            WeaponManager manager = movement.GetComponent<WeaponManager>();
            Assert.NotNull(visibility);
            Assert.NotNull(manager);

            // This is the runtime path used by F5: show the local player's
            // third-person body and apply the selected authored presentation.
            visibility.SetupVisibility(false);
            yield return null;

            Animator body = movement.CharacterAnimation;
            Assert.NotNull(body);
            Animator[] bodyHierarchyAnimators = body.gameObject
                .GetComponentsInChildren<Animator>(true);
            Animator[] skeletonOwners = bodyHierarchyAnimators
                .Where(animator => animator.transform == body.transform
                    || body.transform.IsChildOf(animator.transform))
                .ToArray();
            Assert.AreEqual(
                1,
                skeletonOwners.Length,
                "Only the authored Body Animator may own Clove's skeleton.");

            ThirdPersonWeaponPresentation vandal = visibility
                .ThirdPersonWeaponPresentations
                .Single(presentation => presentation.WeaponData.name == "Vandal");
            Animator gun = vandal.WeaponObject.GetComponentInChildren<Animator>(true);
            Assert.NotNull(gun);

            body.SetBool("Grounded", true);
            body.SetBool("FreeFall", false);
            body.SetFloat("Speed", 7f);
            manager.TriggerAnimation("Reload");
            yield return WaitForState(body, 1, "Vandal Reload");
            yield return WaitForState(gun, 0, "Reload");

            var probeLines = new System.Collections.Generic.List<string>();
            float initialBodyTime = body.GetCurrentAnimatorStateInfo(1).normalizedTime;
            float initialGunTime = gun.GetCurrentAnimatorStateInfo(0).normalizedTime;
            float previousBodyTime = initialBodyTime;
            float previousGunTime = initialGunTime;
            int frame = 0;
            float observationDeadline = Time.realtimeSinceStartup + 0.25f;
            while (Time.realtimeSinceStartup < observationDeadline)
            {
                AnimatorStateInfo bodyReload = body.GetCurrentAnimatorStateInfo(1);
                AnimatorStateInfo gunReload = gun.GetCurrentAnimatorStateInfo(0);
                string layers = string.Join(
                    "; ",
                    Enumerable.Range(0, body.layerCount).Select(layer =>
                    {
                        AnimatorStateInfo current = body.GetCurrentAnimatorStateInfo(layer);
                        AnimatorStateInfo next = body.GetNextAnimatorStateInfo(layer);
                        return $"L{layer}:{body.GetLayerName(layer)} "
                            + $"w={body.GetLayerWeight(layer):F2} "
                            + $"cur={current.fullPathHash}/{current.normalizedTime:F3} "
                            + $"transition={body.IsInTransition(layer)} "
                            + $"next={next.fullPathHash}/{next.normalizedTime:F3}";
                    }));
                probeLines.Add(
                    $"frame={frame} bodyReload={bodyReload.normalizedTime:F3} "
                    + $"gunReload={gunReload.normalizedTime:F3} {layers}");

                Assert.That(
                    bodyReload.normalizedTime + 0.002f,
                    Is.GreaterThanOrEqualTo(previousBodyTime),
                    "TestScene Body reload restarted or froze.");
                Assert.That(
                    gunReload.normalizedTime + 0.002f,
                    Is.GreaterThanOrEqualTo(previousGunTime),
                    "TestScene gun reload restarted or froze.");
                previousBodyTime = bodyReload.normalizedTime;
                previousGunTime = gunReload.normalizedTime;
                frame++;
                yield return null;
            }

            Assert.Greater(
                previousBodyTime - initialBodyTime,
                0.05f,
                "TestScene Body reload did not advance over 0.25 seconds.");
            Assert.Greater(
                previousGunTime - initialGunTime,
                0.05f,
                "TestScene gun reload did not advance over 0.25 seconds.");

#if UNITY_EDITOR
            string logDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName,
                "Logs");
            System.IO.Directory.CreateDirectory(logDirectory);
            System.IO.File.WriteAllLines(
                System.IO.Path.Combine(
                    logDirectory,
                    "TestSceneThirdPersonRuntimeProbe.txt"),
                probeLines);
            }
            finally
            {
                if (testScene.IsValid() && testScene.isLoaded)
                    SceneManager.UnloadSceneAsync(testScene);
            }
#endif
        }

        [UnityTest]
        public IEnumerator ProductionPlayer_RemoteReloadDrivesBodyAndDedicatedGun()
        {
            PlayerMovement movement = null;
            yield return CreateIsolatedProductionPlayer(value => movement = value);
            PlayerVisibilityController visibility =
                movement.GetComponent<PlayerVisibilityController>();
            WeaponManager manager = movement.GetComponent<WeaponManager>();
            visibility.SetupVisibility(false);

            manager.ApplyPresentationState(
                default,
                new WeaponPresentationState
                {
                    slotIndex = 0,
                    isReloading = true,
                    equipCompleteTime = -1d
                });
            yield return WaitForState(
                movement.CharacterAnimation,
                1,
                "Vandal Reload");

            Animator body = movement.CharacterAnimation;
            Animator gun = visibility.ThirdPersonWeaponSlots[0]
                .GetComponentInChildren<Animator>(true);
            Assert.True(body.GetCurrentAnimatorStateInfo(1).IsName("Vandal Reload"));
            Assert.True(gun.GetCurrentAnimatorStateInfo(0).IsName("Reload"));
            Assert.Less(
                Mathf.Abs(body.GetCurrentAnimatorStateInfo(1).normalizedTime
                    - gun.GetCurrentAnimatorStateInfo(0).normalizedTime),
                0.08f);
        }

        [UnityTest]
        public IEnumerator ProductionPlayer_RemoteEquipDrivesBodyAndDedicatedGun()
        {
            PlayerMovement movement = null;
            yield return CreateIsolatedProductionPlayer(value => movement = value);

            PlayerVisibilityController visibility =
                movement.GetComponent<PlayerVisibilityController>();
            WeaponManager manager = movement.GetComponent<WeaponManager>();
            visibility.SetupVisibility(false);

            double presentationTime = manager.NetworkManager != null
                && manager.NetworkManager.IsListening
                    ? manager.NetworkManager.ServerTime.Time
                    : Time.timeAsDouble;

            manager.ApplyPresentationState(
                new WeaponPresentationState
                {
                    slotIndex = 0,
                    equipCompleteTime = -1d
                },
                new WeaponPresentationState
                {
                    slotIndex = 0,
                    equipCompleteTime = presentationTime + 1d
                });
            yield return WaitForState(
                movement.CharacterAnimation,
                1,
                "Vandal Equip");

            Animator body = movement.CharacterAnimation;
            Animator gun = visibility.ThirdPersonWeaponSlots[0]
                .GetComponentInChildren<Animator>(true);
            Assert.True(body.GetCurrentAnimatorStateInfo(1).IsName("Vandal Equip"));
            Assert.True(gun.GetCurrentAnimatorStateInfo(0).IsName("Equip"));
            Assert.Less(
                Mathf.Abs(body.GetCurrentAnimatorStateInfo(1).normalizedTime
                    - gun.GetCurrentAnimatorStateInfo(0).normalizedTime),
                0.08f);
        }

        [UnityTest]
        public IEnumerator ProductionPlayer_AirStateCycleDrivesLowerAndUpperBodyLayers()
        {
            PlayerMovement movement = null;
            yield return CreateIsolatedProductionPlayer(value => movement = value);

            PlayerVisibilityController visibility =
                movement.GetComponent<PlayerVisibilityController>();
            visibility.SetupVisibility(false);
            Animator body = movement.CharacterAnimation;

            body.SetBool("Grounded", true);
            body.SetBool("FreeFall", false);
            body.SetFloat("Speed", 0f);
            yield return null;
            Assert.True(body.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"));
            Assert.True(body.GetCurrentAnimatorStateInfo(1).IsName("Vandal Hold"));
            Assert.True(body.GetCurrentAnimatorStateInfo(2).IsName("Locomotion Add"));

            body.SetBool("Grounded", false);
            body.SetBool("FreeFall", false);
            yield return WaitForState(body, 0, "Jump");
            yield return WaitForState(body, 2, "Jump Add");

            body.SetBool("FreeFall", true);
            yield return WaitForState(body, 0, "Fall");
            yield return WaitForState(body, 2, "Fall Add");

            body.SetBool("Grounded", true);
            body.SetBool("FreeFall", false);
            yield return WaitForState(body, 0, "Land");
            yield return WaitForState(body, 2, "Land Add");
        }

        private static IEnumerator CreateIsolatedProductionPlayer(
            System.Action<PlayerMovement> onCreated,
            string prefabPath = PlayerPrefabPath)
        {
#if UNITY_EDITOR
            const string scenePrefix = "VandalThirdPersonProductionTests_";
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene previousTestScene = SceneManager.GetSceneAt(i);
                if (!previousTestScene.name.StartsWith(
                        scenePrefix,
                        System.StringComparison.Ordinal))
                    continue;

                foreach (GameObject root in previousTestScene.GetRootGameObjects())
                    root.SetActive(false);
            }

            Scene isolatedScene = SceneManager.CreateScene(
                scenePrefix + System.Guid.NewGuid().ToString("N"));
            Assert.True(SceneManager.SetActiveScene(isolatedScene));

            // Test Runner owns its bootstrap scene, so it must remain loaded.
            // Disable only the hand-test scene's runtime copy; PlayMode teardown
            // restores the saved scene and no task-parallel asset is modified.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene == isolatedScene
                    || !scene.path.EndsWith(
                        "Assets/FPS/Scenes/TestScene.unity",
                        System.StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                    root.SetActive(false);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.NotNull(prefab);
            GameObject instance = Object.Instantiate(prefab);
            Assert.NotNull(instance);
            instance.name = "ClovePlayer_3P_AutomatedTest";
            SceneManager.MoveGameObjectToScene(instance, isolatedScene);
            onCreated(instance.GetComponent<PlayerMovement>());
            yield return null;
#else
            Assert.Fail("This production prefab verification requires the Unity Editor.");
            yield break;
#endif
        }

        private static IEnumerator WaitForState(
            Animator animator,
            int layer,
            string stateName,
            float timeout = 0.5f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName)
                && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.True(
                animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName),
                $"Layer {layer} did not reach state '{stateName}' within {timeout:F2}s.");
        }

        private static IEnumerator AssertActiveWeaponPresentation(
            PlayerMovement movement,
            PlayerVisibilityController visibility,
            WeaponManager manager,
            string weaponName)
        {
            yield return null;

            ThirdPersonWeaponPresentation selected = null;
            foreach (ThirdPersonWeaponPresentation presentation
                in visibility.ThirdPersonWeaponPresentations)
            {
                bool isSelected = presentation.WeaponData.name == weaponName;
                if (isSelected)
                    selected = presentation;

                Animator candidateAnimator = presentation.WeaponObject
                    .GetComponentInChildren<Animator>(true);
                Assert.AreEqual(
                    isSelected,
                    candidateAnimator.gameObject.activeInHierarchy,
                    weaponName);
            }

            Assert.NotNull(selected, weaponName);
            ThirdPersonLeftHandIK leftHandIK =
                movement.GetComponent<ThirdPersonLeftHandIK>();
            Assert.NotNull(leftHandIK, weaponName);
            Assert.AreEqual(
                selected.AnimationDrivenLeftHandIK,
                leftHandIK.UsesAnimationDrivenWeight,
                $"{weaponName} must apply its authored left-hand IK mode when selected.");
            Behaviour rigBuilder = movement.CharacterAnimation
                .GetComponents<Behaviour>()
                .SingleOrDefault(component =>
                    component.GetType().FullName
                    == "UnityEngine.Animations.Rigging.RigBuilder");
            Assert.NotNull(rigBuilder, weaponName);
            Assert.AreEqual(
                selected.UseLeftHandIK,
                rigBuilder.enabled,
                $"{weaponName} must apply its authored left-hand rig policy.");
            Animator body = movement.CharacterAnimation;
            Animator gun = selected.WeaponObject.GetComponentInChildren<Animator>(true);
            Assert.AreEqual(selected.CharacterController, body.runtimeAnimatorController);
            Assert.AreEqual(
                ThirdPersonCharacterRigMode.GenericPathBound,
                selected.CharacterRigMode,
                weaponName);
            Assert.Null(body.avatar, weaponName);
            Assert.NotNull(gun.runtimeAnimatorController, weaponName);
            StringAssert.Contains(
                weaponName,
                gun.runtimeAnimatorController.name,
                "The authored gun controller must belong to the selected weapon.");

            Transform rightHand = body.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "R_Hand");
            Transform leftHand = body.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "L_Hand");
            Transform trigger = selected.WeaponObject
                .GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "Trigger");
            Transform support = selected.WeaponObject
                .GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "Left_Hand_Target");
            float maximumHandGap = weaponName == "Odin" ? 1.0f : 0.6f;
            body.SetBool("Grounded", true);
            body.SetBool("FreeFall", false);
            body.SetFloat("Speed", 7f);
            yield return null;
            AssertThirdPersonGrip(
                rightHand,
                trigger,
                leftHand,
                support,
                weaponName + " Walk",
                maximumHandGap);

            manager.TriggerAnimation("Fire");
            yield return null;
            AssertAnimatorInState(body, weaponName + " Fire");
            Assert.True(gun.GetCurrentAnimatorStateInfo(0).IsName("Fire"));
            AssertThirdPersonGrip(
                rightHand,
                trigger,
                leftHand,
                support,
                weaponName + " Walk + Fire",
                maximumHandGap);

            manager.TriggerAnimation("Equip");
            yield return null;
            AssertAnimatorInState(body, weaponName + " Equip");
            Assert.True(gun.GetCurrentAnimatorStateInfo(0).IsName("Equip"));

            manager.TriggerAnimation("Reload");
            yield return null;
            AssertAnimatorInState(body, weaponName + " Reload");
            Assert.True(gun.GetCurrentAnimatorStateInfo(0).IsName("Reload"));
        }

        private static void AssertAnimatorInState(Animator animator, string stateName)
        {
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                if (animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName)
                    || (animator.IsInTransition(layer)
                        && animator.GetNextAnimatorStateInfo(layer).IsName(stateName)))
                    return;
            }

            Assert.Fail($"Animator did not enter '{stateName}'.");
        }

        private static void AssertThirdPersonGrip(
            Transform rightHand,
            Transform trigger,
            Transform leftHand,
            Transform support,
            string phase,
            float maximumHandGap = 0.6f)
        {
            Assert.Less(
                Vector3.Distance(rightHand.position, trigger.position),
                0.35f,
                phase + " must keep the firing hand on the Vandal grip.");
            Assert.Less(
                Vector3.Distance(leftHand.position, support.position),
                0.35f,
                phase + " must keep the support hand on the Vandal foregrip.");
            Assert.Less(
                Vector3.Distance(rightHand.position, leftHand.position),
                maximumHandGap,
                phase + " must not tear the two hands apart.");
        }
    }
}
