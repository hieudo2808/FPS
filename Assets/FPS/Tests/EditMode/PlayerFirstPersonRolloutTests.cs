using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace FPS.Tests
{
    public sealed class PlayerFirstPersonRolloutTests
    {
        [TestCase("Assets/FPS/Features/Characters/Content/Players/Sage/SagePlayer.prefab", "FP_Core_NewFemale_Skelmesh.ao", 0f, 0f, 0f, 270f)]
        [TestCase("Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab", "FP_Core_NewFemale_Skelmesh.ao", 0f, 0f, 0f, 270f)]
        [TestCase("Assets/FPS/Features/Characters/Content/Players/Gekko/GekkoPlayer.prefab", "FP_AggroBot_S0_Skelmesh.ao", 0.000046329f, -0.000776258f, 0.001633956f, 91.62611f)]
        [TestCase("Assets/FPS/Features/Characters/Content/Players/Brimstone/BrimstonePlayer.prefab", "FP_Sarge_S0_Skelmesh.ao", 0.000046329f, -0.000776258f, 0.001633956f, 91.62609f)]
        public void PlayerPrefab_MatchesApprovedFirstPersonContract(
            string prefabPath,
            string expectedModel,
            float weaponX,
            float weaponY,
            float weaponZ,
            float weaponYaw)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.NotNull(prefab);

            WeaponManager manager = prefab.GetComponent<WeaponManager>();
            MouseMovement mouse = prefab.GetComponent<MouseMovement>();
            Assert.NotNull(manager);
            Assert.NotNull(mouse);

            Animator animator = manager.FirstPersonArmsAnimator;
            Assert.NotNull(animator);
            Assert.AreEqual(expectedModel, animator.name);
            Assert.NotNull(animator.transform.Find("Skeleton"));
            Assert.False(animator.applyRootMotion);

            Transform hand = animator.transform.parent;
            Assert.That(hand.localPosition,
                Is.EqualTo(new Vector3(0.09f, -1.69f, 0.11f)).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(Quaternion.Angle(hand.localRotation, Quaternion.Euler(0f, 90f, -2f)), Is.LessThan(0.001f));
            Assert.That(hand.localScale, Is.EqualTo(Vector3.one).Using(Vector3ComparerWithEqualsOperator.Instance));

            Assert.AreEqual("MainCamera", mouse.BodyCam.tag);
            Assert.AreEqual("Untagged", mouse.WeaponCam.tag);
            Assert.True(mouse.BodyCam.transform.parent.gameObject.activeSelf,
                "The camera rig must be active so enabling the owner cameras can render.");
            Assert.False(mouse.BodyCam.enabled,
                "The body camera must stay disabled until local ownership is established.");
            Assert.False(mouse.WeaponCam.enabled,
                "The weapon camera must stay disabled until local ownership is established.");
            Assert.AreEqual(60f, mouse.BodyCam.fieldOfView, 0.0001f);
            Assert.AreEqual(40f, mouse.WeaponCam.fieldOfView, 0.0001f);
            Assert.AreEqual(0.18f, mouse.BodyCam.nearClipPlane, 0.0001f);
            Assert.AreEqual(0.1f, mouse.WeaponCam.nearClipPlane, 0.0001f);
            Assert.AreEqual(191, mouse.BodyCam.cullingMask);
            Assert.AreEqual(320, mouse.WeaponCam.cullingMask);
            Assert.AreEqual(1.6f, mouse.BodyCam.transform.parent.localPosition.y, 0.0001f);
            Assert.That(Vector3.Distance(mouse.BodyCam.transform.position, mouse.WeaponCam.transform.position), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(mouse.BodyCam.transform.rotation, mouse.WeaponCam.transform.rotation), Is.LessThan(0.01f));

            Transform weaponParent = animator.transform.Find(
                "Skeleton/Root/MasterWeapon/WeaponGameOverride/R_WeaponMaster");
            Assert.NotNull(weaponParent);
            Vector3 expectedPosition = new Vector3(weaponX, weaponY, weaponZ);
            Quaternion expectedRotation = Quaternion.Euler(0f, weaponYaw, 0f);
            for (int slot = 0; slot < manager.WeaponCount; slot++)
            {
                Transform weapon = manager.GetWeapon(slot).transform;
                Assert.AreEqual(weaponParent, weapon.parent);
                Assert.That(weapon.localPosition,
                    Is.EqualTo(expectedPosition).Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(Quaternion.Angle(weapon.localRotation, expectedRotation), Is.LessThan(0.001f));
                Assert.That(weapon.localScale,
                    Is.EqualTo(Vector3.one * 0.01f).Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.True(weapon.GetComponentsInChildren<Transform>(true)
                    .All(item => item.gameObject.layer == 8));
            }
        }

        [TestCase("Assets/FPS/Features/Characters/Content/Players/Sage/SagePlayer.prefab")]
        [TestCase("Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab")]
        [TestCase("Assets/FPS/Features/Characters/Content/Players/Gekko/GekkoPlayer.prefab")]
        [TestCase("Assets/FPS/Features/Characters/Content/Players/Brimstone/BrimstonePlayer.prefab")]
        public void OperatorCandidate_UsesAuthoredPhysicalScopeTarget(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            WeaponManager manager = prefab.GetComponent<WeaponManager>();
            Assert.True(manager.TryGetPrimaryCandidate(
                PrimaryWeaponId.Operator,
                out GameObject operatorObject));

            Weapon weapon = operatorObject.GetComponent<Weapon>();
            Assert.NotNull(weapon);
            Assert.NotNull(weapon.AimSight,
                "Physical ADS must use an explicit authored sight reference.");
            Assert.AreEqual("ScopeTargetSocket", weapon.AimSight.name);
            Assert.True(weapon.AimSight.IsChildOf(operatorObject.transform));
            Assert.NotNull(weapon.AimSightEnd);
            Assert.AreEqual("ScopeTargetSocket_end", weapon.AimSightEnd.name);
            Assert.AreEqual(weapon.AimSight, weapon.AimSightEnd.parent);
        }

        [Test]
        public void VandalPrefab_UsesAuthoredPhysicalSightWithoutSniperOverlay()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/FPS/Features/Weapons/Content/Vandal/Vandal.prefab");
            Weapon weapon = prefab.GetComponent<Weapon>();
            Assert.NotNull(weapon);
            Assert.AreEqual("ScopeTargetSocket", weapon.AimSight.name);
            Assert.AreEqual("ScopeTargetSocket_end", weapon.AimSightEnd.name);
            Assert.True(weapon.Data.supportsAim);
            Assert.False(weapon.Data.showScopeOverlay);
            Assert.False(weapon.Data.exitAimAfterShot);
        }
    }
}
