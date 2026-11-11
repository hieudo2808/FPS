#if UNITY_EDITOR
using System.Collections;
using FPS.RecoilPort;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public sealed partial class SurvivalNetworkTests
    {
        [UnityTest]
        public IEnumerator CosmeticYawCannotChangeCanonicalAimOrRootRotation()
        {
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath("Clove")));
            player.GetComponent<PlayerMovement>().enabled = false;
            var look = player.GetComponent<MouseMovement>();
            look.enabled = false;
            player.GetComponent<NetworkObject>().Spawn();
            yield return null;
            Vector3 aim = look.AimDirection;
            Quaternion root = player.transform.rotation;
            look.SetCosmeticRecoil(new Vector2(2, 3));
            Invoke(look, "RenderRotation");
            Assert.That(look.XRotation, Is.Zero);
            Assert.That(look.YRotation, Is.Zero);
            Assert.That(Vector3.Angle(aim, look.AimDirection), Is.LessThan(.0001));
            Assert.That(Quaternion.Angle(root, player.transform.rotation), Is.LessThan(.0001));
            Assert.That(Mathf.DeltaAngle(0, look.BodyCam.transform.localEulerAngles.y), Is.EqualTo(3).Within(.0001));
            Assert.That(Mathf.DeltaAngle(0, look.BodyCam.transform.localEulerAngles.x), Is.EqualTo(-2).Within(.0001));
            player.GetComponent<NetworkObject>().Despawn();
        }

        [UnityTest]
        public IEnumerator CurvedPopKeepsAdsScaleAndReleaseClearsPresentation()
        {
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath("Clove")));
            player.GetComponent<PlayerMovement>().enabled = false;
            var look = player.GetComponent<MouseMovement>();
            look.enabled = false;
            player.GetComponent<NetworkObject>().Spawn();
            var manager = player.GetComponent<WeaponManager>();
            manager.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Warden);
            manager.SetEquippedWeaponServer(0);
            yield return Until(() => !player.GetComponent<WeaponFireHandler>().IsServerEquipping, "pop equip");
            var weapon = manager.GetWeapon(0);
            weapon.enabled = false;
            var data = Object.Instantiate(weapon.Data);
            var profile = Object.Instantiate(data.recoilPattern);
            data.recoilPattern = profile;
            SetField(weapon, "weaponData", data);
            profile.cameraPopScale = Vector2.one;
            profile.cameraRecoilFollow = profile.adsCameraRecoilFollow = Vector2.one;
            profile.cameraPopPitchCurve = new[] { PopKey(0,0), PopKey(.01f,1), PopKey(.2f,0) };
            profile.cameraPopYawCurve = new[] { PopKey(0,0), PopKey(.01f,.5f), PopKey(.2f,0) };
            var recoil = player.GetComponent<RecoilController>();
            recoil.enabled = false;
            float previousCapture = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 30;
                recoil.Bind(weapon, true, 17);
                yield return null;
                Invoke(recoil, "Update");
                Invoke(look, "RenderRotation");
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, look.BodyCam.transform.localEulerAngles.x)), Is.EqualTo(1).Within(.0001));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, look.BodyCam.transform.localEulerAngles.y)), Is.EqualTo(.5f).Within(.0001));
                Assert.That(Vector3.Angle(look.AimDirection, Vector3.forward), Is.LessThan(.0001));
                recoil.Release(weapon);
                Invoke(look, "RenderRotation");
                Assert.That(Quaternion.Angle(look.BodyCam.transform.localRotation, Quaternion.identity), Is.LessThan(.0001));
                recoil.Bind(weapon, false, 18);
                yield return new WaitForSeconds(.25f);
                weapon.ReconcileStability(default);
                Invoke(recoil, "Update");
                Invoke(look, "RenderRotation");
                Assert.That(Quaternion.Angle(look.BodyCam.transform.localRotation, Quaternion.identity), Is.LessThan(.0001), "expired pop cannot replay on ACK");
            }
            finally
            {
                Time.captureDeltaTime = previousCapture;
                recoil.Release(weapon);
                player.GetComponent<NetworkObject>().Despawn();
                Object.Destroy(profile); Object.Destroy(data);
            }
        }

        [UnityTest]
        public IEnumerator CameraFollowSeparatesRayWithoutChangingAimAndSurvivesCompensation()
        {
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath("Clove")));
            player.GetComponent<PlayerMovement>().enabled = false;
            var look = player.GetComponent<MouseMovement>();
            look.enabled = false;
            player.GetComponent<NetworkObject>().Spawn();
            yield return null;
            var recoil = player.GetComponent<RecoilController>();
            recoil.enabled = false;
            var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponData>(
                "Assets/FPS/Features/Weapons/Content/Vandal/Vandal.asset").recoilPattern);
            profile.cameraPopPitchCurve = profile.cameraPopYawCurve = null;
            profile.cameraPop = 0;
            profile.snappiness = 10000;
            profile.resetCooldown = 1000;
            profile.cameraRecoilFollow = new Vector2(.5f, .25f);
            try
            {
                recoil.Fire(profile);
                SetField(recoil, "targetRecoil", new Vector2(2, 4));
                Invoke(recoil, "Update");
                Invoke(look, "RenderRotation");
                Assert.That(look.XRotation, Is.EqualTo(-2).Within(.0001));
                Assert.That(look.YRotation, Is.EqualTo(4).Within(.0001));
                Assert.That(Mathf.DeltaAngle(0, look.BodyCam.transform.localEulerAngles.x), Is.EqualTo(-1).Within(.0001), "half camera pitch, full canonical pitch");
                Assert.That(Mathf.DeltaAngle(0, look.BodyCam.transform.localEulerAngles.y), Is.EqualTo(-3).Within(.0001));
                Vector3 projected = look.BodyCam.WorldToViewportPoint(look.BodyCam.transform.position + look.AimDirection * 100);
                Assert.That(projected.y, Is.GreaterThan(.5), "ray rises above the camera center");

                Vector2 compensation = look.ApplyRecoil(-2, -4);
                recoil.ConsumeLookCompensation(compensation);
                Invoke(recoil, "Update");
                Invoke(look, "RenderRotation");
                Assert.That(look.XRotation, Is.Zero.Within(.0001));
                Assert.That(look.YRotation, Is.Zero.Within(.0001));
                Assert.That(Mathf.DeltaAngle(0, look.BodyCam.transform.localEulerAngles.x), Is.EqualTo(1).Within(.0001), "mouse compensation must not snap the visual ledger");
                SetField(recoil, "targetRecoil", Vector2.zero);
                Invoke(recoil, "Update");
                Invoke(look, "RenderRotation");
                Assert.That(look.XRotation, Is.Zero.Within(.0001), "recovery must not undo compensated aim");
                Assert.That(Quaternion.Angle(look.BodyCam.transform.localRotation, Quaternion.identity), Is.LessThan(.0001));
            }
            finally
            {
                Invoke(recoil, "Clear");
                player.GetComponent<NetworkObject>().Despawn();
                Object.Destroy(profile);
            }
        }

        static UnrealRecoilCurve.Key PopKey(float time, float value)
            => new UnrealRecoilCurve.Key { time = time, value = value, mode = 1 };

        [UnityTest]
        public IEnumerator HipFollowCurveLetsLateSprayRiseAboveCenterWhileAdsStillFollows()
        {
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath("Clove")));
            player.GetComponent<PlayerMovement>().enabled = false;
            var look = player.GetComponent<MouseMovement>();
            look.enabled = false;
            player.GetComponent<NetworkObject>().Spawn();
            var manager = player.GetComponent<WeaponManager>();
            manager.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Vandal);
            manager.SetEquippedWeaponServer(0);
            yield return Until(() => !player.GetComponent<WeaponFireHandler>().IsServerEquipping, "hip curve equip");
            var weapon = manager.GetWeapon(0);
            weapon.enabled = false;
            var data = Object.Instantiate(weapon.Data);
            var profile = Object.Instantiate(data.recoilPattern);
            data.recoilPattern = profile;
            SetField(weapon, "weaponData", data);
            profile.cameraPopPitchCurve = profile.cameraPopYawCurve = null;
            profile.cameraPop = 0;
            profile.snappiness = 10000;
            profile.cameraRecoilFollow = profile.adsCameraRecoilFollow = Vector2.one;
            profile.hipPitchCameraFollow = new[] { PopKey(0, 1), PopKey(7, 0) };
            var recoil = player.GetComponent<RecoilController>();
            recoil.enabled = false;
            try
            {
                var prediction = Field<WeaponStabilityPrediction>(weapon, "stabilityPrediction");
                for (ushort i = 1; i <= 8; i++)
                    prediction.Fire(profile, data.FireInterval, i, weapon.StabilityTime,
                        new WeaponStabilityContext { grounded = true }, i, false);
                recoil.Bind(weapon, false, 17);
                Assert.That(Field<Vector2>(recoil, "cameraFollow"), Is.EqualTo(new Vector2(0, 1)),
                    "late hip kick must stop dragging the camera up with the canonical ray");
                Invoke(recoil, "Update");
                Invoke(look, "RenderRotation");
                Assert.That(look.XRotation, Is.LessThan(-1));
                Vector3 ray = look.BodyCam.WorldToViewportPoint(look.BodyCam.transform.position + look.AimDirection * 100);
                Assert.That(ray.y, Is.GreaterThan(.5));
                recoil.Bind(weapon, true, 18);
                Assert.That(Field<Vector2>(recoil, "cameraFollow"), Is.EqualTo(Vector2.one));
                recoil.Release(weapon);
                Invoke(look, "RenderRotation");
                Assert.That(Quaternion.Angle(look.BodyCam.transform.localRotation, Quaternion.identity), Is.LessThan(.0001));
            }
            finally
            {
                recoil.Release(weapon);
                player.GetComponent<NetworkObject>().Despawn();
                Object.Destroy(profile); Object.Destroy(data);
            }
        }
    }
}
#endif
