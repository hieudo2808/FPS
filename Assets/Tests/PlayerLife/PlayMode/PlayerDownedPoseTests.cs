#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    /// <summary>
    /// The Downed body on every player prefab's real 3P body: last frame of the downed clip plus PlayerDownedPose.
    /// Legs must be untwisted and on the ground, shoulders raised, both arms on the sidearm. Writes review sheets.
    /// </summary>
    public sealed class PlayerDownedPoseTests
    {
        const string Players = "Assets/FPS/Features/Characters/Content/Players";
        const string Output = "Reports/PlayerLife-2026-11-14/fix/";

        sealed class Driver : MonoBehaviour
        {
            public PlayableGraph graph;
            public PlayerDownedPose pose;
            public Vector3 aim;
            public float body = 1f;
            public Vector3 barrelInHand;
            public float gripError, rotationError, shoulderShift;
            public System.Collections.Generic.List<(Transform bone, Vector3 position, Quaternion hold)> hold = new();
            void LateUpdate()
            {
                // Same order as PlayerLifePresenter: body, the standing sidearm hold, then the shoulders onto the aim.
                graph.Evaluate();
                pose.ApplyBody(body, Vector3.forward);
                foreach (var (bone, position, rotation) in hold) bone.SetLocalPositionAndRotation(position, rotation);
                Transform B(HumanBodyBones b) => GetComponentInChildren<Animator>().GetBoneTransform(b);
                var left = B(HumanBodyBones.LeftHand); var right = B(HumanBodyBones.RightHand);
                var leftShoulder = B(HumanBodyBones.LeftUpperArm); var rightShoulder = B(HumanBodyBones.RightUpperArm);
                Vector3 offset = Quaternion.Inverse(right.rotation) * (left.position - right.position);
                Quaternion rotationBefore = Quaternion.Inverse(right.rotation) * left.rotation;
                Vector3 leftOrigin = leftShoulder.position, rightOrigin = rightShoulder.position;
                pose.AimShoulders(aim, body, right.rotation * barrelInHand);
                gripError = Mathf.Max(gripError, Vector3.Distance(offset, Quaternion.Inverse(right.rotation) * (left.position - right.position)));
                rotationError = Mathf.Max(rotationError, Quaternion.Angle(rotationBefore, Quaternion.Inverse(right.rotation) * left.rotation));
                shoulderShift = Mathf.Max(shoulderShift, Vector3.Distance(leftOrigin, leftShoulder.position), Vector3.Distance(rightOrigin, rightShoulder.position));
            }
        }

        static (GameObject root, Animator animator, PlayableGraph graph) Spawn(GameObject prefab, AnimationClip clip, AnimationClip hold = null)
        {
            var root = new GameObject("Player root"); // Looks along +Z.
            var avatar = (Avatar)typeof(PlayerLifePresenter).GetField("humanoidAvatar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(prefab.GetComponent<PlayerLifePresenter>());
            var body = Object.Instantiate(prefab.GetComponent<WeaponManager>().CharacterAnimation.gameObject, root.transform);
            body.transform.localPosition = Vector3.zero;
            var animator = body.GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            var graph = PlayableGraph.Create("downed");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, clip);
            // Same layering as PlayerLifePresenter: the fall clip, plus the standing sidearm hold on the arms.
            var mixer = AnimationLayerMixerPlayable.Create(graph, 2);
            graph.Connect(playable, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);
            if (hold != null)
            {
                graph.Connect(AnimationClipPlayable.Create(graph, hold), 0, mixer, 1);
                mixer.SetLayerMaskFromAvatarMask(1, PlayerLifePresenter.ArmsMask());
                mixer.SetInputWeight(1, 1f);
            }
            AnimationPlayableOutput.Create(graph, "out", animator).SetSourcePlayable(mixer);
            graph.Play();
            playable.SetTime(clip.length);
            graph.Evaluate();
            body.transform.rotation = Quaternion.AngleAxis(PlayerDownedPose.FeetYaw(animator, root.transform.forward), Vector3.up) * body.transform.rotation;
            return (root, animator, graph);
        }

        static float Twist(Animator animator, string muscle)
        {
            var handler = new HumanPoseHandler(animator.avatar, animator.transform);
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            handler.Dispose();
            return pose.muscles[System.Array.IndexOf(HumanTrait.MuscleName, muscle)];
        }

        [UnityTest]
        public IEnumerator DownedBodyIsTidyOnEveryCharacter()
        {
            LogAssert.ignoreFailingMessages = true;
            Directory.CreateDirectory(Output);
            var set = AssetDatabase.LoadAssetAtPath<PlayerLifeAnimationSet>("Assets/FPS/Features/Characters/Animation/Content/3P/PlayerLifeAnimationSet.asset");
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { Players }).Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p.GetComponent<PlayerLifePresenter>() != null).ToArray();
            Assert.That(prefabs.Length, Is.EqualTo(4));

            // Which sign of the Front-Back muscles lifts the shoulders: measured, not assumed.
            float best = float.NegativeInfinity, chosen = 1f;
            foreach (float sign in new[] { 1f, -1f })
            {
                PlayerDownedPose.SpineLiftSign = sign;
                var (root, animator, graph) = Spawn(prefabs[0], set.downedFall);
                var pose = new PlayerDownedPose(animator);
                pose.Apply(1f, Vector3.forward, Vector3.forward, 0f);
                float head = animator.GetBoneTransform(HumanBodyBones.Head).position.y;
                Debug.Log($"[DownedPose] sign {sign:+0;-0}: head {head:0.00}");
                if (head > best) { best = head; chosen = sign; }
                pose.Dispose(); graph.Destroy(); Object.Destroy(root);
                yield return null;
            }
            Debug.Log($"[DownedPose] SpineLiftSign = {chosen}");
            PlayerDownedPose.SpineLiftSign = chosen;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); // Review only.
            floor.transform.localScale = Vector3.one * 2f;
            floor.GetComponent<Renderer>().sharedMaterial = new Material(floor.GetComponent<Renderer>().sharedMaterial) { color = new Color(0.2f, 0.21f, 0.23f) };
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var camera = new GameObject("Review camera").AddComponent<Camera>();
            camera.fieldOfView = 35f;
            const int w = 960, h = 720, views = 4;
            var rt = new RenderTexture(w, h, 24);
            camera.targetTexture = rt;

            bool allOk = true;
            foreach (var prefab in prefabs)
            {
                var (root, animator, graph) = Spawn(prefab, set.downedFall);
                var pose = new PlayerDownedPose(animator);
                Debug.Log($"[DownedPose] {prefab.name} calibration: {pose.LegCalibration}");
                var driver = root.AddComponent<Driver>();
                driver.graph = graph; driver.pose = pose; driver.aim = Vector3.forward + Vector3.up * 0.12f;
                var holdAsset = AssetDatabase.LoadAssetAtPath<PlayerSidearmHoldPose>("Assets/FPS/Features/Characters/Animation/Content/3P/PlayerSidearmHoldPose.asset");
                var classic = prefab.GetComponent<PlayerVisibilityController>().ThirdPersonWeaponPresentations.First(p => p != null && p.WeaponData != null && p.WeaponData.usableWhileDowned);
                var entry = holdAsset.FindEntry(classic.CharacterController.name);
                var bones = entry.bones;
                driver.barrelInHand = entry.barrelInRightHand;
                Assert.That(driver.barrelInHand.sqrMagnitude, Is.GreaterThan(0.9f), "Rebake the authored barrel calibration");
                Assert.That(bones, Is.Not.Null, prefab.name + " has a baked sidearm hold");
                var byName = animator.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                foreach (var bone in bones) if (byName.TryGetValue(bone.name, out var t)) driver.hold.Add((t, bone.localPosition, bone.localRotation));
                yield return null;
                yield return null;
                Transform B(HumanBodyBones b) => animator.GetBoneTransform(b);
                float head = B(HumanBodyBones.Head).position.y, hips = B(HumanBodyBones.Hips).position.y;
                float footL = B(HumanBodyBones.LeftFoot).position.y, footR = B(HumanBodyBones.RightFoot).position.y, kneeR = B(HumanBodyBones.RightLowerLeg).position.y;
                float twistUpper = Mathf.Max(Mathf.Abs(Twist(animator, "Left Upper Leg Twist In-Out")), Mathf.Abs(Twist(animator, "Right Upper Leg Twist In-Out")));
                float twistLower = Mathf.Max(Mathf.Abs(Twist(animator, "Left Lower Leg Twist In-Out")), Mathf.Abs(Twist(animator, "Right Lower Leg Twist In-Out")));
                Vector3 feet = Vector3.ProjectOnPlane((B(HumanBodyBones.LeftFoot).position + B(HumanBodyBones.RightFoot).position) * 0.5f - B(HumanBodyBones.Hips).position, Vector3.up);
                float feetAngle = Vector3.Angle(feet, Vector3.forward);
                Vector3 handsMid = (B(HumanBodyBones.LeftHand).position + B(HumanBodyBones.RightHand).position) * 0.5f;
                Vector3 shouldersMid = (B(HumanBodyBones.LeftUpperArm).position + B(HumanBodyBones.RightUpperArm).position) * 0.5f;
                float armAngle = Vector3.Angle(B(HumanBodyBones.RightHand).rotation * driver.barrelInHand, driver.aim);
                float handGap = Vector3.Distance(B(HumanBodyBones.LeftHand).position, B(HumanBodyBones.RightHand).position);
                Vector3 torso = B(HumanBodyBones.Neck).position - B(HumanBodyBones.Hips).position;
                float torsoLift = Mathf.Asin(Mathf.Clamp(torso.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                float kneeBend = Vector3.Angle(B(HumanBodyBones.RightLowerLeg).position - B(HumanBodyBones.RightUpperLeg).position,
                    B(HumanBodyBones.RightFoot).position - B(HumanBodyBones.RightLowerLeg).position);
                bool ok = hips < 0.4f && head > hips + 0.15f && head < 1.0f && footL < 0.35f && footR < 0.45f && kneeR > footR
                          && twistUpper < 0.15f && twistLower < 0.15f && feetAngle < 30f && armAngle < 15f && handGap < 0.2f && kneeBend < 70f && torsoLift > 15f && torsoLift < 40f;
                allOk &= ok;
                Debug.Log($"[DownedPose] {prefab.name}: head {head:0.00} hips {hips:0.00} feet L {footL:0.00} R {footR:0.00} kneeR {kneeR:0.00} " +
                          $"twist upper {twistUpper:0.00} lower {twistLower:0.00} feet->look {feetAngle:0} gun->aim {armAngle:0} hands apart {handGap:0.00} m knee bend {kneeBend:0} torso lift {torsoLift:0} ok={ok}");

                var sheet = new Texture2D(w * views, h, TextureFormat.RGB24, false);
                var renderers = root.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                for (int v = 0; v < views; v++)
                {
                    // Front (over the feet), left side, behind the head, high three-quarter.
                    Vector3[] dirs = { new Vector3(0.15f, 0.35f, 1f), new Vector3(-1f, 0.25f, 0.1f), new Vector3(0.3f, 0.5f, -1f), new Vector3(1f, 1.1f, 0.6f) };
                    camera.transform.position = bounds.center + dirs[v].normalized * 3.6f;
                    camera.transform.LookAt(bounds.center);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, w, h), v * w, 0);
                    RenderTexture.active = null;
                }
                sheet.Apply();
                File.WriteAllBytes($"{Output}downed-pose__{prefab.name}.png", sheet.EncodeToPNG());
                Object.Destroy(sheet);
                foreach (float yaw in new[] { -50f, 50f, 0f })
                {
                    driver.aim = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward + Vector3.up * 0.12f;
                    yield return null;
                    yield return null;
                    float muzzleError = Vector3.Angle(B(HumanBodyBones.RightHand).rotation * driver.barrelInHand, driver.aim);
                    Assert.That(muzzleError, Is.LessThan(0.1f), prefab.name + " barrel follows aim at " + yaw);
                }
                Debug.Log($"[DownedGrip] {prefab.name}: max wrist offset error={driver.gripError:F5} m rotation={driver.rotationError:F3} deg shoulder shift={driver.shoulderShift:F5} m");
                Assert.That(driver.gripError, Is.LessThan(0.002f), prefab.name + " keeps the authored grip at +/-50 degrees");
                Assert.That(driver.rotationError, Is.LessThan(0.1f), prefab.name + " keeps relative wrist rotation");
                Assert.That(driver.shoulderShift, Is.LessThan(0.0001f), prefab.name + " keeps both shoulder sockets fixed");
                pose.Dispose(); graph.Destroy(); Object.Destroy(root);
                yield return null;
            }
            camera.targetTexture = null;
            rt.Release();
            foreach (var o in new Object[] { floor, sun.gameObject, camera.gameObject }) Object.Destroy(o);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(allOk, Is.True, "Every character lies tidy: legs untwisted on the ground, shoulders up, aiming over the feet");
        }
    }
}
#endif
