using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPS.Tests
{
    public sealed class WeaponAimRegressionTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void WardenUsesItsOwnScopeAndKeepsItsCombatProfile()
        {
            var warden = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/FPS/Features/Weapons/Content/Warden/Warden.asset");
            var op = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/FPS/Features/Weapons/Content/Operator/Operator.asset");
            Assert.That(warden.supportsAim && warden.showScopeOverlay, Is.True);
            Assert.That(warden.scopeOverlaySprite, Is.Not.Null);
            var scope = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/FPS/Features/Weapons/Content/Warden/Scope/WardenScopeADS.png");
            Assert.That(scope, Is.Not.Null);
            Assert.That(warden.scopeOverlaySprite, Is.EqualTo(scope));
            Assert.That(warden.damage, Is.LessThan(op.damage));
            Assert.That(warden.falloffStartDistance, Is.LessThan(op.falloffStartDistance));
            Assert.That(warden.aimedWorldFov, Is.GreaterThan(op.aimedWorldFov));
            Assert.That(warden.exitAimAfterShot, Is.False, "Warden can keep its scope up while firing.");
        }

        [TestCase("Brimstone")]
        [TestCase("Clove")]
        [TestCase("Gekko")]
        [TestCase("Sage")]
        public void EveryPlayerCanRaiseAndLowerWardenWithoutPoseDrift(string character)
        {
            var root = PrefabUtility.LoadPrefabContents(
                $"Assets/FPS/Features/Characters/Content/Players/{character}/{character}Player.prefab");
            try
            {
                var manager = root.GetComponent<WeaponManager>();
                var camera = root.GetComponent<MouseMovement>().BodyCam;
                Weapon warden = null;
                foreach (var weapon in root.GetComponentsInChildren<Weapon>(true))
                    if (weapon.Data != null && weapon.Data.weaponName == "Warden") warden = weapon;
                Assert.That(warden, Is.Not.Null);
                Assert.That(manager.FirstPersonArmsAnimator, Is.Not.Null);
                Assert.That(warden.AimSight, Is.Not.Null);
                Assert.That(warden.AimSightEnd, Is.Not.Null);
                Assert.That(warden.AimSightEnd.IsChildOf(warden.AimSight), Is.True);
                Assert.That((warden.AimSightEnd.position - warden.AimSight.position).sqrMagnitude, Is.GreaterThan(.000001f));

                root.GetComponent<PlayerVisibilityController>().SetupVisibility(true);
                foreach (var weapon in root.GetComponentsInChildren<Weapon>(true))
                    weapon.gameObject.SetActive(weapon == warden);
                warden.WeaponAnimator.enabled = true;
                warden.BindFirstPersonPresentation(camera, manager.FirstPersonArmsAnimator);
                manager.FirstPersonArmsAnimator.Rebind();
                warden.PrepareFirstPersonAnimation();
                manager.FirstPersonArmsAnimator.Update(0f);
                warden.WeaponAnimator.Play("Idle", 0, 0f);
                warden.WeaponAnimator.Update(0f);
                var optic = warden.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name.Contains("SniperScope"));
                var vertices = optic.sharedMesh.vertices;
                var materials = optic.GetComponent<MeshRenderer>().sharedMaterials;
                var glass = optic.sharedMesh.GetIndices(System.Array.FindIndex(materials, m => m.name == "WardenGlass"));
                var rear = optic.sharedMesh.GetIndices(System.Array.FindIndex(materials, m => m.name == "Tritium_MI"));
                var rearBounds = new Bounds(vertices[rear[0]], Vector3.zero);
                foreach (int index in rear) rearBounds.Encapsulate(vertices[index]);
                var glassBounds = new Bounds(vertices[glass[0]], Vector3.zero);
                foreach (int index in glass) glassBounds.Encapsulate(vertices[index]);
                var viewmodel = manager.FirstPersonArmsAnimator.transform.parent;
                var hipPosition = viewmodel.localPosition;
                var hipRotation = viewmodel.localRotation;
                typeof(Weapon).GetField("unscopedWorldFov", PrivateInstance).SetValue(warden, camera.fieldOfView);
                float hipFov = camera.fieldOfView;
                for (int toggle = 0; toggle < 3; toggle++)
                {
                    Assert.That(Invoke(warden, "TryCalculateAimedViewmodelPose"), Is.EqualTo(true));
                    Invoke(warden, "ApplyAimPresentation", true, warden.Data.aimTransitionDuration);
                    Assert.That(warden.IsAiming, Is.True);
                    Assert.That(camera.fieldOfView, Is.EqualTo(warden.Data.aimedWorldFov).Within(.001f));
                    var sightPosition = camera.transform.InverseTransformPoint(warden.AimSight.position);
                    Assert.That(Vector3.Distance(sightPosition, new Vector3(0f, 0f, .12f)), Is.LessThan(.001f));
                    AssertOpticCentered();
                    Invoke(warden, "ApplyAimPresentation", false, warden.Data.aimTransitionDuration);
                    Assert.That(warden.IsAiming, Is.False);
                    AssertOpticCentered(); // HUD leaves before the physical optic starts lowering.
                    Invoke(warden, "ApplyAimPresentation", false, warden.Data.aimTransitionDuration);
                    Assert.That(camera.fieldOfView, Is.EqualTo(hipFov).Within(.001f));
                    Assert.That(Vector3.Distance(viewmodel.localPosition, hipPosition), Is.LessThan(.00001f));
                    Assert.That(Quaternion.Angle(viewmodel.localRotation, hipRotation), Is.LessThan(.01f));
                }

                void AssertOpticCentered()
                {
                    var weaponCamera = root.GetComponent<MouseMovement>().WeaponCam;
                    foreach (var center in new[] { rearBounds.center, glassBounds.center })
                    {
                        Vector3 viewport = weaponCamera.WorldToViewportPoint(optic.transform.TransformPoint(center));
                        Assert.That(Vector2.Distance(viewport, new Vector2(.5f, .5f)), Is.LessThan(.0001f));
                        Assert.That(viewport.z, Is.GreaterThan(weaponCamera.nearClipPlane));
                    }
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static object Invoke(Weapon weapon, string name, params object[] args) =>
            typeof(Weapon).GetMethod(name, PrivateInstance).Invoke(weapon, args);
    }
}
