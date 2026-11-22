using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;

namespace FPS.Tests
{
    public sealed class PlayerKneeCorrectiveTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Call(PlayerLifePresenter presenter, string method, params object[] args)
            => typeof(PlayerLifePresenter).GetMethod(method, Private).Invoke(presenter, args);

        [TestCase("Brimstone")]
        [TestCase("Clove")]
        [TestCase("Gekko")]
        [TestCase("Sage")]
        public void KneeShapeKeepsSourceSkinAndResetsWithPresenter(string name)
        {
            AssetDatabase.SaveAssets();
            string folder = $"Assets/FPS/Features/Characters/Content/Players/{name}/";
            var root = PrefabUtility.LoadPrefabContents(folder + name + "Player.prefab");
            PlayerLifePresenter presenter = null;
            try
            {
                var animator = root.GetComponent<WeaponManager>().CharacterAnimation;
                var skin = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(s => s.sharedMesh.GetBlendShapeIndex(PlayerLifePresenter.KneeVolumeShape) >= 0);
                var mesh = skin.sharedMesh;
                var original = AssetDatabase.LoadAllAssetsAtPath(folder + "Models/" + (name == "Clove" ? "Body" : "Human") + ".fbx")
                    .OfType<Mesh>().OrderByDescending(m => m.vertexCount).First();
                Assert.That(mesh.vertices, Is.EqualTo(original.vertices), "Rest vertices preserved");
                Assert.That(mesh.normals, Is.EqualTo(original.normals));
                Assert.That(mesh.uv, Is.EqualTo(original.uv));
                Assert.That(mesh.tangents, Is.EqualTo(original.tangents));
                Assert.That(mesh.boneWeights, Is.EqualTo(original.boneWeights));
                Assert.That(mesh.bindposes, Is.EqualTo(original.bindposes));
                Assert.That(mesh.subMeshCount, Is.EqualTo(original.subMeshCount));
                for (int sub = 0; sub < mesh.subMeshCount; sub++) Assert.That(mesh.GetIndices(sub), Is.EqualTo(original.GetIndices(sub)));
                int shape = mesh.GetBlendShapeIndex(PlayerLifePresenter.KneeVolumeShape);
                var delta = new Vector3[mesh.vertexCount];
                mesh.GetBlendShapeFrameVertices(shape, 0, delta, null, null);
                Assert.That(delta.Count(d => d.sqrMagnitude > 1e-18f), Is.InRange(100, 1500));
                Assert.That(delta.Max(d => d.magnitude), Is.LessThan(0.0005f));
                var kneeCenters = new[] { "L_Knee", "R_Knee" }.Select(k => mesh.bindposes[Array.FindIndex(skin.bones, b => b.name == k)].inverse.MultiplyPoint3x4(Vector3.zero)).ToArray();
                var vertices = mesh.vertices;
                for (int i = 0; i < delta.Length; i++)
                    if (delta[i].sqrMagnitude > 1e-18f)
                        Assert.That(kneeCenters.Min(k => Vector3.Distance(k, vertices[i])), Is.LessThan(0.00221f), "Correction stays near knees");

                presenter = root.GetComponent<PlayerLifePresenter>();
                Call(presenter, "Awake");
                var visibility = root.GetComponent<PlayerVisibilityController>();
                var classic = visibility.ThirdPersonWeaponPresentations.First(p => p?.WeaponData != null && p.WeaponData.usableWhileDowned);
                animator.gameObject.SetActive(true);
                animator.runtimeAnimatorController = classic.CharacterController;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Assert.That(skin.GetBlendShapeWeight(shape), Is.Zero);
                Call(presenter, "BeginDowned", true);
                ((PlayableGraph)typeof(PlayerLifePresenter).GetField("graph", Private).GetValue(presenter)).Evaluate();
                Call(presenter, "UpdateThirdPerson", 1f);
                Assert.That(skin.GetBlendShapeWeight(shape), Is.EqualTo(100f).Within(0.01f));
                Call(presenter, "OnDisable");
                Assert.That(skin.GetBlendShapeWeight(shape), Is.Zero, "Disable/pool reuse clears shape");
                Assert.That(presenter.IsOverridingCharacter, Is.False);
                Debug.Log($"[KneeShape] {name}: source channels preserved; localized shape applied=100, disable=0");
            }
            finally
            {
                if (presenter != null) Call(presenter, "ReleaseImmediately");
                PrefabUtility.UnloadPrefabContents(root);
                AssetDatabase.SaveAssets();
                Assert.That(EditorSceneManager.GetActiveScene().isDirty, Is.False);
            }
        }
    }
}
