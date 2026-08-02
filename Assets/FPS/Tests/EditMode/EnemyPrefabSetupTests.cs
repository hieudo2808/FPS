using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace FPS.Tests
{
    public class EnemyPrefabSetupTests
    {
        private static readonly string[] EnemyPrefabPaths =
        {
            "Assets/FPS/Features/Characters/Content/Enemies/Copzombie/Prefabs/copzombie_l_actisdato.prefab",
            "Assets/FPS/Features/Characters/Content/Enemies/ZombiegirlW/Prefabs/Zombiegirl W Kurniawan.prefab",
            "Assets/FPS/Features/Characters/Content/Enemies/Screamer/Prefabs/BookHeadMonster_withBlood.prefab"
        };

        [Test]
        public void EnemyPrefabs_DoNotDependOnRuntimeGroundingComponent()
        {
            foreach (string prefabPath in EnemyPrefabPaths)
            {
                GameObject prefab = LoadPrefab(prefabPath);
                System.Type groundingType = System.Type.GetType("FPS.EnemyGrounding, FPS");

                if (groundingType != null)
                {
                    Assert.IsNull(prefab.GetComponentInChildren(groundingType, true),
                        $"{prefabPath} should be grounded by prefab/model setup, not by EnemyGrounding.");
                }
            }
        }

        [Test]
        public void EnemyPrefabs_HaveGroundedColliderAgentAndAnimationSetup()
        {
            foreach (string prefabPath in EnemyPrefabPaths)
            {
                GameObject prefab = LoadPrefab(prefabPath);

                var agent = prefab.GetComponent<NavMeshAgent>();
                Assert.IsNotNull(agent, $"{prefabPath} should own its NavMeshAgent setup on the prefab root.");
                Assert.AreEqual(0f, agent.baseOffset, 0.01f,
                    $"{prefabPath} should not need runtime baseOffset correction.");

                var animator = prefab.GetComponentInChildren<Animator>(true);
                Assert.IsNotNull(animator, $"{prefabPath} should include an Animator.");
                Assert.IsFalse(animator.applyRootMotion,
                    $"{prefabPath} should declare root-motion policy in the prefab.");

                var collider = prefab.GetComponent<CapsuleCollider>();
                Assert.IsNotNull(collider, $"{prefabPath} should include a root CapsuleCollider.");
                float colliderBottomY = collider.center.y - collider.height * 0.5f;
                Assert.AreEqual(0f, colliderBottomY, 0.08f,
                    $"{prefabPath} capsule bottom should sit near the prefab root ground plane.");

                AssertFootBonesNearPrefabGround(prefabPath, prefab);
            }
        }

        private static GameObject LoadPrefab(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, $"Missing enemy prefab at {prefabPath}.");
            return prefab;
        }

        private static void AssertFootBonesNearPrefabGround(string prefabPath, GameObject prefab)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
                float minFootY = float.MaxValue;
                int footBoneCount = 0;

                foreach (Transform transform in transforms)
                {
                    string lowerName = transform.name.ToLowerInvariant();
                    if (!lowerName.Contains("foot") && !lowerName.Contains("toe"))
                        continue;

                    footBoneCount++;
                    minFootY = Mathf.Min(minFootY, transform.position.y);
                }

                Assert.Greater(footBoneCount, 0, $"{prefabPath} should expose foot/toe bones for prefab grounding validation.");
                Assert.GreaterOrEqual(minFootY, -0.25f,
                    $"{prefabPath} foot bones should not be buried far below the prefab ground plane.");
                Assert.LessOrEqual(minFootY, 0.35f,
                    $"{prefabPath} foot bones should not float above the prefab ground plane.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
