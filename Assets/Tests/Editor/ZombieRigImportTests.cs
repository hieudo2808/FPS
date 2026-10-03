using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FPS.Tests
{
    public class ZombieRigImportTests
    {
        [TestCase("ZombieRig/Prefabs/ZombieRig.prefab")]
        [TestCase("ZombieMaleAAB/Prefabs/ZombieMale_AAB.prefab")]
        [TestCase("ZombieMaleAAB/Prefabs/ZombieMale_AAB_BodyParts.prefab")]
        public void HumanoidClipsAndBlendsKeepHumanScaleAndGroundContact(string prefab)
        {
            string path = "Assets/FPS/Features/Characters/Content/Enemies/" + prefab;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            var baked = new Mesh();
            try
            {
                Animator animator = root.GetComponent<Animator>();
                Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
                Assert.That(animator.humanScale, Is.InRange(0.5f, 2f), "A stale Avatar skeleton previously enlarged animation 100 times.");
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                animator.Rebind();
                SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>();
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                Assert.That(renderers, Is.Not.Empty);
                Assert.That(clips.Length, Is.EqualTo(5));

                void AssertPose(string context)
                {
                    Vector3 min = Vector3.one * float.PositiveInfinity;
                    Vector3 max = Vector3.one * float.NegativeInfinity;
                    foreach (SkinnedMeshRenderer renderer in renderers)
                    {
                        renderer.BakeMesh(baked);
                        foreach (Vector3 vertex in baked.vertices)
                        {
                            Vector3 point = root.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                            min = Vector3.Min(min, point);
                            max = Vector3.Max(max, point);
                        }
                    }

                    Assert.That((max - min).magnitude, Is.InRange(0.5f, 4f), context);
                    // Allow small contact differences and the airborne phase of a running stride.
                    Assert.That(min.y, Is.InRange(-0.06f, 0.2f), context);
                    Assert.That(root.transform.position.y, Is.EqualTo(0f).Within(0.001f), context);
                }

                foreach (AnimationClip clip in clips)
                {
                    var graph = PlayableGraph.Create("ZombieRigImportTest");
                    try
                    {
                        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                        var playable = AnimationClipPlayable.Create(graph, clip);
                        var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
                        output.SetSourcePlayable(playable);
                        graph.Play();
                        for (int sample = 0; sample <= 20; sample++)
                        {
                            playable.SetTime(clip.length * sample / 20f);
                            graph.Evaluate(0);
                            AssertPose($"{clip.name}, sample {sample}");
                        }
                    }
                    finally
                    {
                        graph.Destroy();
                    }
                }

                foreach (float speed in new[] { 0f, 0.5f, 1f, 2f, 3f, 4f, 5f })
                {
                    animator.Rebind();
                    animator.SetFloat("Speed", speed);
                    animator.SetFloat("LocomotionRate", 1f);
                    animator.Play("Locomotion", 0, 0f);
                    animator.Update(0f);
                    for (int sample = 0; sample < 90; sample++)
                    {
                        animator.Update(1f / 30f);
                        AssertPose($"Speed {speed}, sample {sample}");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(baked);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
