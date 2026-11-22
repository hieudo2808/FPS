#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    /// <summary>
    /// Bakes PlayerSidearmHoldPose: every character's Classic idle run by its own controller (as in game),
    /// then the local rotation of every bone from both clavicles down, excluding the weapon models.
    /// </summary>
    public sealed class PlayerSidearmHoldBaker
    {
        public const string AssetPath = "Assets/FPS/Features/Characters/Animation/Content/3P/PlayerSidearmHoldPose.asset";

        [UnityTest, Explicit("Writes an asset")]
        public IEnumerator BakeSidearmHold()
        {
            LogAssert.ignoreFailingMessages = true;
            var classic = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/FPS/Features/Weapons/Content/Classic/Classic.asset");
            var entries = new List<PlayerSidearmHoldPose.Entry>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/FPS/Features/Characters/Content/Players" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var visibility = prefab.GetComponent<PlayerVisibilityController>();
                var presentation = visibility?.ThirdPersonWeaponPresentations.FirstOrDefault(p => p != null && p.WeaponData == classic);
                if (presentation == null || presentation.CharacterController == null) continue;
                string controller = presentation.CharacterController.name;
                if (entries.Any(e => e.controller == controller)) continue;

                var source = prefab.GetComponent<WeaponManager>().CharacterAnimation;
                var body = Object.Instantiate(source.gameObject);
                var animator = body.GetComponent<Animator>();
                animator.runtimeAnimatorController = presentation.CharacterController;
                animator.avatar = presentation.CharacterRigMode == ThirdPersonCharacterRigMode.GenericPathBound ? null : presentation.CharacterAvatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                foreach (var p in visibility.ThirdPersonWeaponPresentations)
                {
                    if (p?.WeaponObject == null || !p.WeaponObject.transform.IsChildOf(source.transform)) continue;
                    body.transform.Find(AnimationUtility.CalculateTransformPath(p.WeaponObject.transform, source.transform))?.gameObject.SetActive(p == presentation);
                }
                for (int i = 0; i < 40; i++) yield return null;

                var bones = new List<PlayerSidearmHoldPose.Bone>();
                foreach (string clavicle in new[] { "L_Clavicle", "R_Clavicle" })
                {
                    var root = body.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == clavicle);
                    Assert.That(root, Is.Not.Null, $"{prefab.name}: {clavicle}");
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (t.GetComponentInParent<Animator>(true) == animator)
                            bones.Add(new PlayerSidearmHoldPose.Bone { name = t.name, localPosition = t.localPosition, localRotation = t.localRotation });
                }
                var all = body.GetComponentsInChildren<Transform>(true);
                var rightHand = all.First(t => t.name == "R_Hand");
                var weaponMaster = all.First(t => t.name == "R_WeaponMaster");
                var weapon = body.transform.Find(AnimationUtility.CalculateTransformPath(presentation.WeaponObject.transform, source.transform));
                var muzzle = weapon.GetComponentsInChildren<Transform>(true).First(t => t.name == "Muzzle");
                var muzzlePoint = weapon.GetComponentsInChildren<Transform>(true).First(t => t.name == "MuzzlePoint");
                Debug.Log($"[SidearmHoldBake] muzzle={muzzle.position.ToString("F4")} point={muzzlePoint.position.ToString("F4")} right={muzzlePoint.right.ToString("F4")} up={muzzlePoint.up.ToString("F4")} forward={muzzlePoint.forward.ToString("F4")} hand={rightHand.position.ToString("F4")} body={body.transform.forward.ToString("F4")}");
                entries.Add(new PlayerSidearmHoldPose.Entry { controller = controller, bones = bones.ToArray(),
                    // The imported UE muzzle uses -X along the bore (+Z points up in the standing model).
                    barrelInRightHand = Quaternion.Inverse(rightHand.rotation) * -muzzlePoint.right,
                    weaponPositionInRightHand = Quaternion.Inverse(rightHand.rotation) * (weaponMaster.position - rightHand.position),
                    weaponRotationInRightHand = Quaternion.Inverse(rightHand.rotation) * weaponMaster.rotation });
                Debug.Log($"[SidearmHoldBake] {prefab.name} -> {controller}: {bones.Count} bones");
                Object.Destroy(body);
                yield return null;
            }
            var asset = AssetDatabase.LoadAssetAtPath<PlayerSidearmHoldPose>(AssetPath);
            if (asset == null) { asset = ScriptableObject.CreateInstance<PlayerSidearmHoldPose>(); AssetDatabase.CreateAsset(asset, AssetPath); }
            asset.entries = entries.ToArray();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            LogAssert.ignoreFailingMessages = false;
            Assert.That(entries.Count, Is.GreaterThanOrEqualTo(2));
        }
    }
}
#endif
