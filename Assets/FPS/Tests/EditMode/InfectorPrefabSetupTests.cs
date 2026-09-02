using FPS.Editor;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace FPS.Tests
{
    public class InfectorPrefabSetupTests
    {
        private GameObject infectorPrefab;
        private AnimatorController animatorController;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            infectorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InfectorPrefabUtility.PrefabPath);
            animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(InfectorPrefabUtility.ControllerPath);
        }

        [Test]
        public void InfectorPrefab_IsAuthoredAndLoaded()
        {
            Assert.IsNotNull(infectorPrefab, "Infector.prefab should exist and load successfully.");
        }

        [Test]
        public void InfectorPrefab_HasAllRequiredComponents()
        {
            Assert.IsNotNull(infectorPrefab.GetComponent<Animator>(), "Must have Animator.");
            Assert.IsNotNull(infectorPrefab.GetComponent<NetworkObject>(), "Must have NetworkObject.");
            Assert.IsNotNull(infectorPrefab.GetComponent<NetworkTransform>(), "Must have NetworkTransform.");
            Assert.IsNotNull(infectorPrefab.GetComponent<SI_Infector>(), "Must have SI_Infector.");
            Assert.IsNotNull(infectorPrefab.GetComponent<EnemyHealth>(), "Must have EnemyHealth.");
            Assert.IsNotNull(infectorPrefab.GetComponent<NavMeshAgent>(), "Must have NavMeshAgent.");
            Assert.IsNotNull(infectorPrefab.GetComponent<CapsuleCollider>(), "Must have CapsuleCollider.");
            Assert.IsNotNull(infectorPrefab.GetComponent<HitboxSegment>(), "Must have HitboxSegment on root.");
            Assert.IsNotNull(infectorPrefab.GetComponent<LagCompensatedTarget>(), "Must have LagCompensatedTarget.");
        }

        [Test]
        public void InfectorPrefab_NavMeshAgent_ConfiguredForAgility()
        {
            var agent = infectorPrefab.GetComponent<NavMeshAgent>();
            Assert.IsNotNull(agent);
            Assert.AreEqual(0.5f, agent.radius, 0.01f, "Infector agent radius should be 0.5.");
            Assert.AreEqual(2.0f, agent.height, 0.01f, "Infector agent height should be 2.0.");
            Assert.AreEqual(5.75f, agent.speed, 0.01f, "Infector agent speed should be 5.75 (agile stalker).");
            Assert.AreEqual(1.5f, agent.stoppingDistance, 0.01f, "Infector stopping distance should be 1.5.");
        }

        [Test]
        public void InfectorPrefab_Health_ConfiguredTo200()
        {
            var health = infectorPrefab.GetComponent<EnemyHealth>();
            Assert.IsNotNull(health);
            Assert.AreEqual(200f, health.MaxHealth, "Infector base health must be configured to 200 (fragile special).");
        }

        [Test]
        public void InfectorPrefab_Collider_ConfiguredCorrectly()
        {
            var collider = infectorPrefab.GetComponent<CapsuleCollider>();
            Assert.IsNotNull(collider);
            Assert.AreEqual(0.5f, collider.radius, 0.01f);
            Assert.AreEqual(2.0f, collider.height, 0.01f);
            Assert.AreEqual(new Vector3(0f, 1.0f, 0f), collider.center);
        }

        [Test]
        public void InfectorPrefab_HasStandardHitboxSegments_DividedIntoHeadChestAndBody()
        {
            var segments = infectorPrefab.GetComponentsInChildren<HitboxSegment>(true);
            Assert.AreEqual(3, segments.Length, "Infector must have exactly 3 standard HitboxSegments (Head, Chest, Body).");

            bool hasHead = false, hasChest = false, hasBody = false;

            foreach (var segment in segments)
            {
                var col = segment.GetComponent<Collider>();
                Assert.IsNotNull(col, $"Segment '{segment.name}' must have a Collider attached.");
                Assert.IsFalse(col.isTrigger, $"Segment '{segment.name}' Collider must not be a trigger for weapon raycasts.");
                Assert.IsNotNull(segment.DamageTarget, $"Segment '{segment.name}' must reference DamageTarget (EnemyHealth).");
                Assert.IsNotNull(segment.OwnerNetworkObject, $"Segment '{segment.name}' must reference OwnerNetworkObject.");

                switch (segment.Zone)
                {
                    case HitboxZone.Head:
                        hasHead = true;
                        Assert.AreEqual(2f, segment.DamageMultiplier, "Head hitbox must have 2.0x damage multiplier.");
                        break;
                    case HitboxZone.Chest:
                        hasChest = true;
                        Assert.AreEqual(1f, segment.DamageMultiplier, "Chest hitbox must have 1.0x damage multiplier.");
                        break;
                    case HitboxZone.Body:
                        hasBody = true;
                        Assert.AreEqual(1f, segment.DamageMultiplier, "Body hitbox must have 1.0x damage multiplier.");
                        break;
                }
            }

            Assert.IsTrue(hasHead, "Infector must have a Head HitboxSegment.");
            Assert.IsTrue(hasChest, "Infector must have a Chest HitboxSegment.");
            Assert.IsTrue(hasBody, "Infector must have a Body HitboxSegment.");
        }

        [Test]
        public void InfectorPrefab_LagCompensatedTarget_ReferencesAllHitboxSegments()
        {
            var lagComp = infectorPrefab.GetComponent<LagCompensatedTarget>();
            Assert.IsNotNull(lagComp, "Must have LagCompensatedTarget.");

            var so = new SerializedObject(lagComp);
            var segmentsProp = so.FindProperty("hitboxSegments");
            Assert.AreEqual(3, segmentsProp.arraySize, "LagCompensatedTarget must have all 3 hitbox segments serialized.");

            for (int i = 0; i < segmentsProp.arraySize; i++)
            {
                var seg = segmentsProp.GetArrayElementAtIndex(i).objectReferenceValue as HitboxSegment;
                Assert.IsNotNull(seg, $"LagCompensatedTarget hitbox segment [{i}] must not be null.");
            }
        }

        [Test]
        public void InfectorPrefab_SI_Infector_HasAllComponentAndAudioReferences()
        {
            var infector = infectorPrefab.GetComponent<SI_Infector>();
            Assert.IsNotNull(infector);
            var so = new SerializedObject(infector);

            var agent = so.FindProperty("agent").objectReferenceValue as NavMeshAgent;
            var animator = so.FindProperty("animator").objectReferenceValue as Animator;
            var attackSound = so.FindProperty("attackSound").objectReferenceValue as AudioClip;
            var deathSound = so.FindProperty("deathSound").objectReferenceValue as AudioClip;
            var roar = so.FindProperty("roarSound").objectReferenceValue as AudioClip;
            var stab = so.FindProperty("implantStabSound").objectReferenceValue as AudioClip;
            var windup = so.FindProperty("implantWindupSound").objectReferenceValue as AudioClip;
            var hiss = so.FindProperty("stalkHissSound").objectReferenceValue as AudioClip;

            Assert.IsNotNull(agent, "NavMeshAgent reference must be assigned to SI_Infector.");
            Assert.IsNotNull(animator, "Animator reference must be assigned to SI_Infector.");
            Assert.IsNotNull(attackSound, "Attack sound clip must be assigned.");
            Assert.IsNotNull(deathSound, "Death sound clip must be assigned.");
            Assert.IsNotNull(roar, "Roar sound clip must be assigned.");
            Assert.IsNotNull(stab, "Implant stab sound clip must be assigned.");
            Assert.IsNotNull(windup, "Implant windup sound clip must be assigned.");
            Assert.IsNotNull(hiss, "Stalk hiss sound clip must be assigned.");
        }

        [Test]
        public void InfectorPrefab_AnimatorController_ConfiguredCleanly()
        {
            Assert.IsNotNull(animatorController, "InfectorAnim.controller must exist.");

            // Verify Parameters
            bool hasSpeed = false, hasAttack = false, hasDie = false, hasRoar = false;
            foreach (var p in animatorController.parameters)
            {
                if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) hasSpeed = true;
                if (p.name == "Attack" && p.type == AnimatorControllerParameterType.Trigger) hasAttack = true;
                if (p.name == "Die" && p.type == AnimatorControllerParameterType.Trigger) hasDie = true;
                if (p.name == "Roar" && p.type == AnimatorControllerParameterType.Trigger) hasRoar = true;
            }

            Assert.IsTrue(hasSpeed, "Animator must have Float parameter 'Speed'.");
            Assert.IsTrue(hasAttack, "Animator must have Trigger parameter 'Attack'.");
            Assert.IsTrue(hasDie, "Animator must have Trigger parameter 'Die'.");
            Assert.IsTrue(hasRoar, "Animator must have Trigger parameter 'Roar'.");

            var sm = animatorController.layers[0].stateMachine;
            Assert.AreEqual("Idle", sm.defaultState.name, "Default state must be 'Idle'.");

            // Verify States exist
            string[] expectedStates = { "Idle", "Run", "Attack", "Roar", "Death" };
            foreach (var expected in expectedStates)
            {
                bool found = false;
                foreach (var s in sm.states)
                {
                    if (s.state.name == expected)
                    {
                        found = true;
                        Assert.IsNotNull(s.state.motion, $"State '{expected}' must have a valid Motion clip.");
                        break;
                    }
                }
                Assert.IsTrue(found, $"State '{expected}' must exist in AnimatorController.");
            }

            // Verify Locomotion transitions are responsive (no exit time)
            foreach (var s in sm.states)
            {
                if (s.state.name == "Idle")
                {
                    foreach (var t in s.state.transitions)
                    {
                        if (t.destinationState.name == "Run")
                        {
                            Assert.IsFalse(t.hasExitTime, "Idle -> Run transition must have hasExitTime = false for responsive movement.");
                            Assert.AreEqual(1, t.conditions.Length);
                            Assert.AreEqual("Speed", t.conditions[0].parameter);
                            Assert.AreEqual(AnimatorConditionMode.Greater, t.conditions[0].mode);
                        }
                    }
                }
                else if (s.state.name == "Run")
                {
                    foreach (var t in s.state.transitions)
                    {
                        if (t.destinationState.name == "Idle")
                        {
                            Assert.IsFalse(t.hasExitTime, "Run -> Idle transition must have hasExitTime = false for responsive movement.");
                            Assert.AreEqual(1, t.conditions.Length);
                            Assert.AreEqual("Speed", t.conditions[0].parameter);
                            Assert.AreEqual(AnimatorConditionMode.Less, t.conditions[0].mode);
                        }
                    }
                }
            }

            // Verify AnyState transitions have conditions
            foreach (var t in sm.anyStateTransitions)
            {
                Assert.Greater(t.conditions.Length, 0, $"AnyState -> {t.destinationState.name} transition must have at least one condition.");
                Assert.IsFalse(t.hasExitTime, $"AnyState -> {t.destinationState.name} transition must not have exit time.");
            }
        }

        [Test]
        public void InfectorPrefab_RegisteredInDefaultNetworkPrefabs()
        {
            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            Assert.IsNotNull(list);
            Assert.IsTrue(list.Contains(infectorPrefab), "Infector prefab must be registered in DefaultNetworkPrefabs for multiplayer replication.");
        }

        [TestCase("Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab")]
        [TestCase("Assets/FPS/Features/Characters/Content/Players/Sage/SagePlayer.prefab")]
        [TestCase("Assets/FPS/Features/Characters/Content/Players/Gekko/GekkoPlayer.prefab")]
        [TestCase("Assets/FPS/Features/Characters/Content/Players/Brimstone/BrimstonePlayer.prefab")]
        public void PlayerPrefab_HasAuthoredInfectionAndInteractionReferences(string prefabPath)
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.NotNull(playerPrefab);

            PlayerInfectionController infection = playerPrefab.GetComponent<PlayerInfectionController>();
            InteractionManager interaction = playerPrefab.GetComponent<InteractionManager>();
            Assert.NotNull(infection, $"{playerPrefab.name} must author PlayerInfectionController.");
            Assert.NotNull(interaction, $"{playerPrefab.name} must author InteractionManager.");

            var infectionSerialized = new SerializedObject(infection);
            Assert.NotNull(infectionSerialized.FindProperty("cachedHealth").objectReferenceValue);
            Assert.NotNull(infectionSerialized.FindProperty("cachedMovement").objectReferenceValue);

            var interactionSerialized = new SerializedObject(interaction);
            Assert.AreSame(infection,
                interactionSerialized.FindProperty("infectionController").objectReferenceValue,
                "InteractionManager must reference the authored infection controller on the same prefab.");
        }

    }
}
