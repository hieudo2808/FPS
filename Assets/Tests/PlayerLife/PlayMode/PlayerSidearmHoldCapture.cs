#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    /// <summary>
    /// Reference: each player prefab's third-person body holding the Classic while standing, with exactly the
    /// controller, rig mode and avatar its ThirdPersonWeaponPresentation authors. Diagnostic screenshots.
    /// </summary>
    public sealed class PlayerSidearmHoldCapture
    {
        const string Output = "Reports/PlayerLife-2026-11-14/";

        [UnityTest, Explicit("Writes review screenshots")]
        public IEnumerator CaptureEveryCharacter()
        {
            LogAssert.ignoreFailingMessages = true;
            Directory.CreateDirectory(Output);
            var classic = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/FPS/Features/Weapons/Content/Classic/Classic.asset");
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40f, -20f, 0f);
            var camera = new GameObject("Review camera").AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.32f, 0.34f, 0.38f);
            const int w = 360, h = 360;
            var rt = new RenderTexture(w, h, 24);
            camera.targetTexture = rt;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/FPS/Features/Characters/Content/Players" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var visibility = prefab.GetComponent<PlayerVisibilityController>();
                if (visibility == null) continue;
                var presentation = visibility.ThirdPersonWeaponPresentations.FirstOrDefault(p => p != null && p.WeaponData == classic);
                if (presentation == null) { Debug.Log($"[SidearmHold] {prefab.name}: no Classic presentation"); continue; }

                // The visual body with its weapons, without the networked player around it.
                var source = prefab.GetComponent<WeaponManager>().CharacterAnimation;
                var body = Object.Instantiate(source.gameObject);
                body.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = body.GetComponent<Animator>();
                animator.runtimeAnimatorController = presentation.CharacterController;
                animator.avatar = presentation.CharacterRigMode == ThirdPersonCharacterRigMode.GenericPathBound ? null : presentation.CharacterAvatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                // Show only the Classic among the 3P weapon models.
                string weaponPath = presentation.WeaponObject != null && presentation.WeaponObject.transform.IsChildOf(source.transform)
                    ? AnimationUtility.CalculateTransformPath(presentation.WeaponObject.transform, source.transform) : "(outside the body)";
                foreach (var p in visibility.ThirdPersonWeaponPresentations)
                {
                    if (p == null || p.WeaponObject == null || !p.WeaponObject.transform.IsChildOf(source.transform)) continue;
                    var copy = body.transform.Find(AnimationUtility.CalculateTransformPath(p.WeaponObject.transform, source.transform));
                    if (copy != null) copy.gameObject.SetActive(p == presentation);
                }
                for (int i = 0; i < 40; i++) yield return null; // Let the idle hold settle.

                var sheet = new Texture2D(w * 3, h, TextureFormat.RGB24, false);
                Vector3 focus = new Vector3(0f, 1.35f, 0f);
                Vector3[] views = { new Vector3(0.2f, 0.1f, 2.4f), new Vector3(2.4f, 0.1f, 0.3f), new Vector3(-1.6f, 0.4f, 1.6f) };
                for (int v = 0; v < views.Length; v++)
                {
                    // TP_Core faces -X in clip space; the prefab body may be turned. Frame around its own forward.
                    Vector3 forward = Vector3.ProjectOnPlane(body.transform.forward, Vector3.up).normalized;
                    Quaternion frame = Quaternion.LookRotation(forward);
                    camera.transform.position = focus + frame * views[v];
                    camera.transform.LookAt(focus);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, w, h), v * w, 0);
                    RenderTexture.active = null;
                }
                sheet.Apply();
                File.WriteAllBytes($"{Output}standing-classic__{prefab.name}.png", sheet.EncodeToPNG());
                Debug.Log($"[SidearmHold] {prefab.name}: controller={(presentation.CharacterController != null ? presentation.CharacterController.name : "none")} rig={presentation.CharacterRigMode} " +
                          $"avatar={(presentation.CharacterAvatar != null ? presentation.CharacterAvatar.name : "none")} weapon={weaponPath}");
                Object.Destroy(sheet);
                Object.Destroy(body);
                yield return null;
            }
            camera.targetTexture = null;
            rt.Release();
            Object.Destroy(sun.gameObject);
            Object.Destroy(camera.gameObject);
            LogAssert.ignoreFailingMessages = false;
        }
    }
}
#endif
