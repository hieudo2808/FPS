#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    /// <summary>
    /// A life clip must win over the character's own AnimatorController, including after the Animator rebinds.
    /// Measured on a real player model with the shared 3P controller: the head must reach the ground.
    /// </summary>
    public sealed class PlayerLifeOutputOrderTests
    {
        const string Model = "Assets/FPS/Features/Characters/Content/Players/Brimstone/Models/Human.fbx";
        const string Controller = "Assets/FPS/Features/Characters/Animation/Content/3P/Shared/ThirdPersonCharacter.controller";
        const string Clip = "Assets/FPS/Features/Characters/Animation/Content/3P/Death/TP_Core_Death_Body_Front_LG.fbx";

        [UnityTest]
        public IEnumerator LifeClipOverridesTheCharacterController([Values(false, true)] bool sorted, [Values(false, true)] bool rebindAfter)
        {
            LogAssert.ignoreFailingMessages = true;
            var body = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            var animator = body.GetComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().First();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controller);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            for (int i = 0; i < 3; i++) yield return null;

            var clip = AssetDatabase.LoadAllAssetsAtPath(Clip).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            var graph = PlayableGraph.Create("life");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "life", animator);
            if (sorted) output.SetSortingOrder(PlayerLifePresenter.OutputSortingOrder);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetTime(clip.length);
            playable.SetSpeed(0d);
            output.SetSourcePlayable(playable);
            graph.Play();
            if (rebindAfter) { yield return null; animator.Rebind(); }
            for (int i = 0; i < 5; i++) yield return null;
            float head = animator.GetBoneTransform(HumanBodyBones.Head).position.y;
            Debug.Log($"[LifeOutputOrder] sorted={sorted} rebind={rebindAfter} headY={head:0.00}");
            graph.Destroy();
            Object.Destroy(body);
            LogAssert.ignoreFailingMessages = false;
            if (sorted) Assert.That(head, Is.LessThan(0.6f), "The Death clip lies the body down over the controller");
        }
    }
}
#endif
