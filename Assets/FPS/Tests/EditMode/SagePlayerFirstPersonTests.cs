using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace FPS.Tests
{
    public sealed class SagePlayerFirstPersonTests
    {
        private const string SagePlayerPath = "Assets/FPS/Features/Characters/Content/Players/Sage/SagePlayer.prefab";
        private const string ControllerPath = "Assets/FPS/Features/Weapons/Content/FirstPerson/FPAnim.controller";
        private const string NormalizedFolder = "Assets/FPS/Features/Weapons/Content/FirstPerson/Normalized/Vandal";

        [Test]
        public void SagePlayer_CentralBindingTargetsModelAnimatorAndBakedWeaponTransforms()
        {
            GameObject prefab = LoadSage();
            WeaponManager manager = prefab.GetComponent<WeaponManager>();
            Assert.NotNull(manager);
            Animator armsAnimator = manager.FirstPersonArmsAnimator;
            Assert.NotNull(armsAnimator, "WeaponManager must own the centralized first-person animator reference.");
            Assert.AreEqual("FP_Core_NewFemale_Skelmesh.ao", armsAnimator.gameObject.name);
            Assert.NotNull(armsAnimator.transform.Find("Skeleton"), "The bound Animator must be on the model that owns Skeleton.");
            Assert.AreEqual("Hand", armsAnimator.transform.parent.name);
            Assert.IsNull(armsAnimator.transform.parent.GetComponent<Animator>(), "Animator must not be moved onto Hand.");

            Assert.IsNull(prefab.GetComponent("FirstPersonPresentationRig"),
                "Static first-person calibration must be baked into prefab transforms, not a runtime component.");
            Transform weaponParent = armsAnimator.transform.Find(
                "Skeleton/Root/MasterWeapon/WeaponGameOverride/R_WeaponMaster");
            Assert.NotNull(weaponParent);
            Assert.IsNull(weaponParent.Find("FPWeaponSocket"),
                "The legacy shared runtime-calibrated socket must be removed.");

            for (int slot = 0; slot < manager.WeaponCount; slot++)
            {
                Weapon weapon = manager.GetWeapon(slot);
                Assert.NotNull(weapon);
                Assert.AreEqual(weaponParent, weapon.transform.parent);
                Assert.That(weapon.transform.localPosition, Is.EqualTo(Vector3.zero).Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(Quaternion.Angle(weapon.transform.localRotation, Quaternion.Euler(0f, 270f, 0f)), Is.LessThan(0.001f));
                Assert.That(weapon.transform.localScale, Is.EqualTo(Vector3.one * 0.01f).Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.True(weapon.GetComponentsInChildren<Transform>(true).All(item => item.gameObject.layer == 8),
                    $"Every object in weapon slot {slot} must render on Weapon layer.");
            }
        }

        [Test]
        public void SagePlayer_DualCamerasAndRecursiveLayersMatchPresentationContract()
        {
            GameObject prefab = LoadSage();
            MouseMovement mouse = prefab.GetComponent<MouseMovement>();
            Assert.NotNull(mouse);
            Assert.AreEqual("MainCamera", mouse.BodyCam.tag);
            Assert.AreEqual("Untagged", mouse.WeaponCam.tag);
            Assert.AreEqual(191, mouse.BodyCam.cullingMask);
            Assert.AreEqual(320, mouse.WeaponCam.cullingMask);
            Assert.AreEqual(0.1f, mouse.WeaponCam.nearClipPlane, 0.0001f);
            Assert.AreEqual(60f, mouse.BodyCam.fieldOfView, 0.0001f);
            Assert.AreEqual(40f, mouse.WeaponCam.fieldOfView, 0.0001f);
            Assert.That(Vector3.Distance(mouse.BodyCam.transform.position, mouse.WeaponCam.transform.position), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(mouse.BodyCam.transform.rotation, mouse.WeaponCam.transform.rotation), Is.LessThan(0.01f));
            Assert.AreEqual(1.6f, mouse.BodyCam.transform.parent.localPosition.y, 0.0001f);

            Transform hand = mouse.WeaponCam.transform.Find("Hand");
            Assert.NotNull(hand);
            Assert.That(hand.localPosition, Is.EqualTo(new Vector3(0.09f, -1.69f, 0.11f)).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(Quaternion.Angle(hand.localRotation, Quaternion.Euler(0f, 90f, -2f)), Is.LessThan(0.001f));
            Assert.That(hand.localScale, Is.EqualTo(Vector3.one).Using(Vector3ComparerWithEqualsOperator.Instance));
            WeaponManager manager = prefab.GetComponent<WeaponManager>();
            Transform[] weaponRoots = hand.GetComponentsInChildren<Weapon>(true)
                .Select(weapon => weapon.transform)
                .ToArray();
            Assert.True(hand.GetComponentsInChildren<Transform>(true)
                .Where(item => !weaponRoots.Any(root => item == root || item.IsChildOf(root)))
                .All(item => item.gameObject.layer == 6));
        }

        [Test]
        public void SagePlayer_WeaponsUseMeshAuthoredMuzzlesDirectly()
        {
            WeaponManager manager = LoadSage().GetComponent<WeaponManager>();
            for (int slot = 0; slot < manager.WeaponCount; slot++)
            {
                Weapon weapon = manager.GetWeapon(slot);
                Assert.NotNull(weapon.BulletSpawnPoint,
                    $"Weapon slot {slot} must reference its mesh-authored muzzle directly.");
                Assert.AreNotEqual("Vandal_BulletSpawnPoint", weapon.BulletSpawnPoint.name);
                Assert.AreNotEqual("Classic_BulletSpawnPoint", weapon.BulletSpawnPoint.name);
            }
        }

        [Test]
        public void SagePlayer_VandalIdleMatchesApprovedFraming()
        {
            UnityEngine.SceneManagement.Scene preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(LoadSage(), preview);
                WeaponManager manager = root.GetComponent<WeaponManager>();
                Animator animator = manager.FirstPersonArmsAnimator;

                Weapon vandal = manager.GetWeapon(0);
                AnimationClip idle = LoadIdleClip(vandal.Data.name);
                AnimationMode.StartAnimationMode();
                AnimationMode.SampleAnimationClip(animator.gameObject, idle, 0f);

                Camera camera = root.GetComponent<MouseMovement>().WeaponCam;
                camera.aspect = 16f / 9f;
                Vector3 muzzle = camera.WorldToViewportPoint(vandal.BulletSpawnPoint.position);
                // AnimationMode sampling does not evaluate the complete runtime
                // Animator layer stack. Keep this EditMode assertion broad; the
                // exact camera-space parity is verified in Play Mode.
                Assert.That(muzzle.x, Is.InRange(0.55f, 0.75f));
                Assert.That(muzzle.y, Is.InRange(0.10f, 0.50f));
                Assert.That(Vector2.Distance(new Vector2(muzzle.x, muzzle.y), new Vector2(0.5f, 0.5f)), Is.GreaterThan(0.06f));
                AnimationMode.StopAnimationMode();
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        [Test]
        public void PathCompatibleVandalClipsPreserveOriginalTimingAndResolveFromModelAnimator()
        {
            Animator animator = LoadSage().GetComponent<WeaponManager>().FirstPersonArmsAnimator;
            AnimationClip[] originals = AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/FPS/Features/Weapons/Content/Vandal/Animation/FP_Vandal.fbx")
                .OfType<AnimationClip>()
                .ToArray();
            foreach (string stateName in new[] { "Idle", "Equip", "Fire", "Reload", "Inspect" })
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{NormalizedFolder}/Vandal_{stateName}.anim");
                Assert.NotNull(clip, $"Missing path-compatible Vandal {stateName} clip.");
                string originalSuffix = stateName == "Idle" ? "IdlePose" : stateName;
                AnimationClip original = originals.First(candidate => candidate.name.EndsWith(originalSuffix, StringComparison.Ordinal));
                Assert.AreEqual(original.length, clip.length, 0.000001f, $"{stateName} length must remain identical to the FBX source.");
                Assert.AreEqual(original.frameRate, clip.frameRate, 0.000001f, $"{stateName} frame rate must remain identical to the FBX source.");
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    Assert.False(binding.path.StartsWith("FP_Core_NewFemale_Skelmesh.ao/", StringComparison.Ordinal));
                    Assert.AreNotEqual(typeof(Camera), binding.type);
                    Assert.True(string.IsNullOrEmpty(binding.path) || animator.transform.Find(binding.path) != null,
                        $"Unresolved binding '{binding.path}' in {clip.name}.");
                }
            }
        }

        [Test]
        public void FirstPersonControllerHasNeutralBaseAndGatedWeaponTransitions()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.NotNull(controller);
            CollectionAssert.AreEqual(
                new[] { "Base", "Classic", "Vandal", "Operator", "Odin", "Bucky" },
                controller.layers.Select(layer => layer.name).ToArray());
            Assert.AreEqual(1f, controller.layers[0].defaultWeight);
            Assert.True(controller.parameters.Any(parameter => parameter.name == "ActiveWeaponLayer"
                && parameter.type == AnimatorControllerParameterType.Int));

            foreach (string weaponLayer in new[] { "Classic", "Vandal" })
            {
                int layerIndex = Array.FindIndex(controller.layers, layer => layer.name == weaponLayer);
                AnimatorStateMachine machine = controller.layers[layerIndex].stateMachine;
                CollectionAssert.IsSubsetOf(
                    new[] { "Idle", "Equip", "Fire", "Reload", "Inspect" },
                    machine.states.Select(item => item.state.name).ToArray());
                if (weaponLayer == "Vandal")
                {
                    foreach (AnimatorState state in machine.states.Select(item => item.state))
                    {
                        Assert.NotNull(state.motion, $"Vandal/{state.name} must have a motion.");
                        StringAssert.StartsWith(
                            $"{NormalizedFolder}/",
                            AssetDatabase.GetAssetPath(state.motion),
                            $"Vandal/{state.name} must use its path-compatible normalized clip.");
                        AnimationClip clip = (AnimationClip)state.motion;
                        float expectedDuration = state.name switch
                        {
                            "Idle" => 1f,
                            "Equip" => 4.125f / 2f,
                            "Fire" => 0.12f,
                            "Reload" => 7.3333335f / 2f,
                            _ => clip.length
                        };
                        Assert.AreEqual(expectedDuration, clip.length / state.speed, 0.0001f,
                            $"Vandal/{state.name} hand timing must match WeaponData and the gun controller.");
                    }
                }
                foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
                {
                    Assert.True(transition.conditions.Any(condition => condition.parameter == "ActiveWeaponLayer"
                        && condition.mode == AnimatorConditionMode.Equals
                        && Mathf.Approximately(condition.threshold, layerIndex)));
                    if (transition.destinationState.name == "Fire")
                        Assert.True(transition.canTransitionToSelf, "Auto fire must be able to restart Fire per shot.");
                }
            }
        }

        [Test]
        public void WeaponDataMatchesSagePilotTimingAndFireModes()
        {
            WeaponData vandal = AssetDatabase.LoadAssetAtPath<WeaponData>(
                "Assets/FPS/Features/Weapons/Content/Vandal/Vandal.asset");
            WeaponData classic = AssetDatabase.LoadAssetAtPath<WeaponData>(
                "Assets/FPS/Features/Weapons/Content/Classic/Classic.asset");
            Assert.AreEqual(FireMode.Auto, vandal.fireMode);
            Assert.AreEqual("Vandal", vandal.firstPersonAnimatorLayer);
            Assert.AreEqual(4.125f / 2f, vandal.EquipDuration, 0.0001f);
            Assert.AreEqual(7.3333335f / 2f, vandal.ReloadDuration, 0.0001f);
            Assert.AreEqual(FireMode.Single, classic.fireMode);
            Assert.AreEqual("Classic", classic.firstPersonAnimatorLayer);
            Assert.AreEqual(2.5f / 2f, classic.EquipDuration, 0.0001f);
            Assert.AreEqual(5.2916665f / 2f, classic.ReloadDuration, 0.0001f);
        }

        [Test]
        public void AdditionalPrimaryLayersHaveResolvedHandMotionsAndMatchGunTiming()
        {
            Animator armsAnimator = LoadSage().GetComponent<WeaponManager>().FirstPersonArmsAnimator;
            AnimatorController fpController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var gunControllerPaths = new System.Collections.Generic.Dictionary<string, string>
            {
                ["Operator"] = "Assets/FPS/Features/Weapons/Content/Operator/Animation/Operator.controller",
                ["Odin"] = "Assets/FPS/Features/Weapons/Content/Odin/Animation/OdinAnim.controller",
                ["Bucky"] = "Assets/FPS/Features/Weapons/Content/Bucky/Animations/BuckyAnim.controller"
            };

            foreach ((string weaponName, string gunControllerPath) in gunControllerPaths)
            {
                int layerIndex = Array.FindIndex(fpController.layers, layer => layer.name == weaponName);
                Assert.Greater(layerIndex, 0, $"Missing FPAnim layer {weaponName}.");
                AnimatorStateMachine handMachine = fpController.layers[layerIndex].stateMachine;
                Assert.AreEqual("Equip", handMachine.defaultState.name);
                CollectionAssert.IsSubsetOf(
                    new[] { "Idle", "Equip", "Fire", "Reload", "Inspect" },
                    handMachine.states.Select(item => item.state.name).ToArray());

                AnimatorController gunController = AssetDatabase.LoadAssetAtPath<AnimatorController>(gunControllerPath);
                AnimatorStateMachine gunMachine = gunController.layers[0].stateMachine;
                foreach (AnimatorState handState in handMachine.states.Select(item => item.state))
                {
                    AnimationClip handClip = handState.motion as AnimationClip;
                    Assert.NotNull(handClip, $"{weaponName}/{handState.name} hand motion must not be null.");
                    foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(handClip))
                    {
                        Assert.True(string.IsNullOrEmpty(binding.path) || armsAnimator.transform.Find(binding.path) != null,
                            $"Unresolved {weaponName}/{handState.name} binding '{binding.path}'.");
                    }

                    AnimatorState gunState = gunMachine.states.Select(item => item.state)
                        .FirstOrDefault(state => state.name == handState.name);
                    if (gunState?.motion is not AnimationClip gunClip)
                        continue;

                    if (weaponName == "Odin" && handState.name == "Fire")
                    {
                        Assert.AreEqual(0f, handState.speed, 0.0001f,
                            "Odin hands Fire must be sampled from the gun master clock.");
                        continue;
                    }

                    float handDuration = handClip.length / handState.speed;
                    float gunDuration = gunClip.length / gunState.speed;
                    Assert.AreEqual(gunDuration, handDuration, 0.0002f,
                        $"{weaponName}/{handState.name} hand and gun timing must match.");
                }

                foreach (AnimatorStateTransition transition in handMachine.anyStateTransitions)
                {
                    Assert.True(transition.conditions.Any(condition => condition.parameter == "ActiveWeaponLayer"
                        && condition.mode == AnimatorConditionMode.Equals
                        && Mathf.Approximately(condition.threshold, layerIndex)));
                }

                WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(
                    $"Assets/FPS/Features/Weapons/Content/{weaponName}/{weaponName}.asset");
                Assert.AreEqual(weaponName, data.firstPersonAnimatorLayer);
                AnimatorState equip = gunMachine.states.Select(item => item.state).First(state => state.name == "Equip");
                AnimatorState reload = gunMachine.states.Select(item => item.state).First(state => state.name == "Reload");
                Assert.AreEqual(((AnimationClip)equip.motion).length / equip.speed, data.EquipDuration, 0.0002f);
                Assert.AreEqual(((AnimationClip)reload.motion).length / reload.speed, data.ReloadDuration, 0.0002f);

                if (weaponName == "Odin")
                {
                    Assert.False(data.restartFireAnimationPerShot,
                        "Odin auto fire must let the feed/ejection clip advance instead of restarting every bullet.");
                    AnimatorState fire = gunMachine.states.Select(item => item.state).First(state => state.name == "Fire");
                    Assert.AreEqual(0, data.fireLoopStartFrame);
                    Assert.AreEqual(30, data.fireLoopEndFrame);
                    Assert.IsEmpty(fire.transitions,
                        "Odin Fire must remain in the authored feed/ejection loop until code releases it.");
                }
                if (weaponName == "Bucky")
                {
                    Assert.AreEqual(ReloadMode.PerShell, data.reloadMode);
                    AnimationClip reloadClip = (AnimationClip)reload.motion;
                    int lastFrame = Mathf.RoundToInt(reloadClip.length * reloadClip.frameRate);
                    Assert.That(data.reloadLoopStartFrame, Is.InRange(0, lastFrame - 1));
                    Assert.That(data.reloadLoopEndFrame,
                        Is.InRange(data.reloadLoopStartFrame + 1, lastFrame));
                    float secondsPerFrame = 1f / (reloadClip.frameRate * reload.speed);
                    Assert.AreEqual(data.reloadLoopStartFrame * secondsPerFrame,
                        data.PerShellOpeningDuration, 0.0002f);
                    Assert.AreEqual((data.reloadLoopEndFrame - data.reloadLoopStartFrame) * secondsPerFrame,
                        data.PerShellInterval, 0.0002f);
                }
            }
        }

        private static GameObject LoadSage()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SagePlayerPath);
            Assert.NotNull(prefab);
            return prefab;
        }

        private static AnimationClip LoadIdleClip(string weaponName)
        {
            if (weaponName == "Vandal")
                return AssetDatabase.LoadAssetAtPath<AnimationClip>($"{NormalizedFolder}/Vandal_Idle.anim");

            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/FPS/Features/Weapons/Content/Classic/Animations/FP_Classic.fbx")
                .OfType<AnimationClip>()
                .First(candidate => candidate.name == "IdleAdd" || candidate.name.EndsWith("IdleAdd", StringComparison.Ordinal));
            Assert.NotNull(clip);
            return clip;
        }
    }
}
