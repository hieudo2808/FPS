#if UNITY_EDITOR
using System.Collections;
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
    /// For every player prefab: which humanoid avatars bind its real 3P body AND keep it to human size when a
    /// Death clip plays (head and hips near the floor, body no longer than ~2.5 m). Logs the full matrix.
    /// </summary>
    public sealed class PlayerLifeAvatarScaleTests
    {
        const string Players = "Assets/FPS/Features/Characters/Content/Players";
        const string Clip = "Assets/FPS/Features/Characters/Animation/Content/3P/Death/TP_Core_Death_Leg_Back_LG.fbx";

        [UnityTest]
        public IEnumerator EveryPlayerHasAnAvatarThatLiesDownAtHumanSize()
        {
            LogAssert.ignoreFailingMessages = true;
            var avatars = AssetDatabase.FindAssets("t:Avatar", new[] { "Assets/FPS/Features/Characters" })
                .SelectMany(g => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(g)).OfType<Avatar>())
                .Where(a => a.isHuman).Distinct().ToArray();
            var clip = AssetDatabase.LoadAllAssetsAtPath(Clip).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            bool allOk = true;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Players }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponent<PlayerHealth>() == null) continue;
                string assigned = "";
                var presenterField = typeof(PlayerLifePresenter).GetField("humanoidAvatar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var presenter = prefab.GetComponent<PlayerLifePresenter>();
                var assignedAvatar = presenter != null ? presenterField.GetValue(presenter) as Avatar : null;
                if (assignedAvatar != null) assigned = assignedAvatar.name + "@" + AssetDatabase.GetAssetPath(assignedAvatar);
                bool anyOk = false, assignedOk = false;
                foreach (var avatar in avatars)
                {
                    // Only the visual body, without network scripts.
                    var source = prefab.GetComponent<WeaponManager>().CharacterAnimation;
                    var body = Object.Instantiate(source.gameObject);
                    body.transform.position = Vector3.zero;
                    body.transform.rotation = Quaternion.identity;
                    var animator = body.GetComponent<Animator>();
                    animator.runtimeAnimatorController = null;
                    animator.avatar = avatar;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.Rebind();
                    bool binds = animator.GetBoneTransform(HumanBodyBones.Head) != null && animator.GetBoneTransform(HumanBodyBones.LeftFoot) != null;
                    if (!binds) { Object.Destroy(body); continue; }
                    var graph = PlayableGraph.Create("scale");
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var playable = AnimationClipPlayable.Create(graph, clip);
                    AnimationPlayableOutput.Create(graph, "out", animator).SetSourcePlayable(playable);
                    graph.Play();
                    playable.SetTime(clip.length);
                    graph.Evaluate();
                    yield return null;
                    graph.Evaluate();
                    Vector3 head = animator.GetBoneTransform(HumanBodyBones.Head).position, hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                    Vector3 foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                    float length = Vector3.Distance(head, foot);
                    bool ok = head.y < 0.6f && hips.y < 0.5f && hips.y > -0.3f && length > 1.0f && length < 2.5f;
                    anyOk |= ok;
                    if (avatar == assignedAvatar) assignedOk = ok;
                    Debug.Log($"[AvatarScale] {System.IO.Path.GetFileNameWithoutExtension(path)} avatar={avatar.name}@{AssetDatabase.GetAssetPath(avatar)} " +
                              $"headY={head.y:0.00} hipsY={hips.y:0.00} headToFoot={length:0.00} ok={ok} scale={body.transform.lossyScale.x:0.###}");
                    graph.Destroy();
                    Object.Destroy(body);
                    yield return null;
                }
                Debug.Log($"[AvatarScale] {System.IO.Path.GetFileNameWithoutExtension(path)} assigned={assigned} assignedOk={assignedOk}");
                allOk &= assignedOk;
            }
            LogAssert.ignoreFailingMessages = false;
            Assert.That(allOk, Is.True, "Every player's PlayerLifePresenter avatar binds its body and lies it down at human size");
        }
    }
}
#endif
