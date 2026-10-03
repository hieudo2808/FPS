#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace FPS.Tests
{
    /// <summary>Live locomotion on the saved Asylum bake, not a combat or multi-peer acceptance test.</summary>
    public sealed class CampaignMorgueNavigationTests
    {
        GameObject probe;
        NavMeshDataInstance navigation;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (probe != null) Object.Destroy(probe);
            yield return null;
            if (navigation.valid) navigation.Remove();
        }

        [UnityTest]
        public IEnumerator TankAgentSettings_ReachReaderAndLiftFromBothConcealedAnchors()
        {
            Assert.That(CampaignMissionController.Instance, Is.Null, "Run in the Test Runner's isolated scene.");
            var navAssets = AssetDatabase.GetDependencies("Assets/FPS/Scenes/GameScene.unity")
                .Where(p => p.StartsWith("Assets/FPS/Features/World/Content/Campaign/MorgueArena/"))
                .Select(AssetDatabase.LoadAssetAtPath<NavMeshData>).Where(data => data != null).ToArray();
            Assert.That(navAssets.Length, Is.EqualTo(1), "Use the NavMesh actually referenced by GameScene, not an old diagnostic bake.");
            navigation = NavMesh.AddNavMeshData(navAssets[0]);
            Assert.That(navigation.valid, Is.True);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/FPS/Features/Characters/Content/Enemies/Tanker/Prefabs/Tank.prefab");
            var authored = prefab.GetComponent<NavMeshAgent>();
            Assert.That(authored, Is.Not.Null);
            probe = new GameObject("Tank navigation probe");
            probe.SetActive(false);
            var agent = probe.AddComponent<NavMeshAgent>();
            // Copy the real prefab settings; do not shrink the Tank or speed up the test agent.
            EditorUtility.CopySerialized(authored, agent);
            var starts = new[] { new Vector3(164, -3.79f, 238), new Vector3(163, -3.79f, 240) };
            var targets = new[] { new Vector3(152, -3.84f, 239.8f), new Vector3(146.3f, -3.84f, 238.79f) };
            probe.transform.position = starts[0];
            probe.SetActive(true);
            agent.enabled = true;
            foreach (var start in starts)
                foreach (var target in targets)
                {
                    string route = start + " -> " + target;
                    Assert.That(agent.Warp(start), Is.True, route);
                    agent.ResetPath();
                    agent.isStopped = false;
                    Assert.That(agent.SetDestination(target), Is.True, route);
                    float deadline = Time.realtimeSinceStartup + 20f;
                    float arrival = Mathf.Max(.3f, agent.stoppingDistance + .15f);
                    bool passedPartition = false;
                    do
                    {
                        yield return null;
                        Assert.That(agent.isOnNavMesh, Is.True, route);
                        Assert.That(Mathf.Abs(probe.transform.position.y + 3.84f - agent.baseOffset), Is.LessThan(.4f), route);
                        passedPartition |= probe.transform.position.x < 158f;
                    }
                    while (Time.realtimeSinceStartup < deadline &&
                        (agent.pathPending || !agent.hasPath || agent.remainingDistance > arrival));
                    Assert.That(agent.pathPending, Is.False, route);
                    Assert.That(agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete), route);
                    Assert.That(Vector3.Distance(probe.transform.position, target), Is.LessThan(arrival + .4f), route);
                    Assert.That(passedPartition, Is.True, "Agent must navigate around the concealed bay: " + route);
                    Debug.Log("[MorgueNavigation] Reached " + route);
                }
        }
    }
}
#endif
