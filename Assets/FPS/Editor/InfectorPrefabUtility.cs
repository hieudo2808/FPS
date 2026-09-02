using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace FPS.Editor
{
    /// <summary>Read-only validation for authored Infector assets. Never creates or fixes content.</summary>
    public static class InfectorPrefabUtility
    {
        public const string BasePath = "Assets/FPS/Features/Characters/Content/Enemies/Infector";
        public const string PrefabPath = BasePath + "/Prefabs/Infector.prefab";
        public const string FbxPath = BasePath + "/Infector.fbx";
        public const string ControllerPath = BasePath + "/InfectorAnim.controller";
        public const string MaterialPath = BasePath + "/Materials/Mat_Infector_Body.mat";
        public const string GameScenePath = "Assets/FPS/Scenes/GameScene.unity";

        private static readonly string[] PlayerPrefabPaths =
        {
            "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Sage/SagePlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Gekko/GekkoPlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Brimstone/BrimstonePlayer.prefab"
        };

        public static bool ValidateAuthoredSetup(bool logResult = true)
        {
            var errors = new List<string>();
            ValidateInfectorPrefab(errors);
            ValidatePlayerPrefabs(errors);
            ValidateNetworkRegistration(errors);
            ValidateGameScene(errors);
            if (logResult)
            {
                if (errors.Count == 0) Debug.Log("[InfectorValidation] Authored setup is valid.");
                else Debug.LogError("[InfectorValidation] Failed:\n - " + string.Join("\n - ", errors));
            }
            return errors.Count == 0;
        }

        private static void ValidateInfectorPrefab(List<string> errors)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { errors.Add($"Missing prefab: {PrefabPath}"); return; }

            Require<NetworkObject>(prefab, errors, "Infector");
            Require<NavMeshAgent>(prefab, errors, "Infector");
            Require<Animator>(prefab, errors, "Infector");
            Require<EnemyHealth>(prefab, errors, "Infector");
            Require<SI_Infector>(prefab, errors, "Infector");
            Require<LagCompensatedTarget>(prefab, errors, "Infector");

            EnemyHealth health = prefab.GetComponent<EnemyHealth>();
            if (health != null && !Mathf.Approximately(health.AuthoredMaxHealth, 200f))
                errors.Add($"Infector authored HP must be 200, found {health.AuthoredMaxHealth}.");
            NavMeshAgent agent = prefab.GetComponent<NavMeshAgent>();
            if (agent != null && (agent.speed <= 0f || agent.stoppingDistance <= 0f))
                errors.Add("Infector NavMeshAgent speed/stoppingDistance must be authored.");

            SI_Infector brain = prefab.GetComponent<SI_Infector>();
            if (brain != null)
            {
                if (brain.Type != SpecialType.Infector)
                    errors.Add($"Infector SI_Infector.specialType must be Infector, found {brain.Type}.");
                var serialized = new SerializedObject(brain);
                RequireReference(serialized, "agent", errors, "Infector SI_Infector.agent");
                RequireReference(serialized, "animator", errors, "Infector SI_Infector.animator");
            }

            Animator animator = prefab.GetComponent<Animator>();
            AnimatorController controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
            if (controller == null) errors.Add("Infector AnimatorController is missing.");
            else
            {
                foreach ((string name, AnimatorControllerParameterType type) in new[]
                {
                    ("Speed", AnimatorControllerParameterType.Float),
                    ("Attack", AnimatorControllerParameterType.Trigger),
                    ("Die", AnimatorControllerParameterType.Trigger)
                })
                {
                    if (!controller.parameters.Any(p => p.name == name && p.type == type))
                        errors.Add($"Infector Animator missing {type} parameter '{name}'.");
                }
            }
        }

        private static void ValidatePlayerPrefabs(List<string> errors)
        {
            foreach (string path in PlayerPrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { errors.Add($"Missing player prefab: {path}"); continue; }
                PlayerInfectionController infection = prefab.GetComponent<PlayerInfectionController>();
                if (infection == null) { errors.Add($"{prefab.name} missing PlayerInfectionController."); continue; }
                var serialized = new SerializedObject(infection);
                RequireReference(serialized, "cachedHealth", errors, $"{prefab.name}.cachedHealth");
                RequireReference(serialized, "cachedMovement", errors, $"{prefab.name}.cachedMovement");

                InteractionManager interaction = prefab.GetComponent<InteractionManager>();
                if (interaction == null)
                {
                    errors.Add($"{prefab.name} missing authored InteractionManager.");
                    continue;
                }

                SerializedProperty infectionReference = new SerializedObject(interaction)
                    .FindProperty("infectionController");
                if (infectionReference == null || infectionReference.objectReferenceValue != infection)
                    errors.Add($"{prefab.name}.InteractionManager.infectionController must reference its authored PlayerInfectionController.");
            }
        }

        private static void ValidateNetworkRegistration(List<string> errors)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (list == null) { errors.Add("Missing DefaultNetworkPrefabs.asset."); return; }
            int count = list.PrefabList.Count(entry => entry.Prefab == prefab);
            if (count != 1) errors.Add($"Infector must occur exactly once in DefaultNetworkPrefabs; found {count}.");
        }

        private static void ValidateGameScene(List<string> errors)
        {
            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = default;
            bool opened = false;
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            try
            {
                scene = SceneManager.GetSceneByPath(GameScenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
                    opened = true;
                }

                InfectionThreatService threat = FindInScene<InfectionThreatService>(scene);
                SpecialInfectedRegistry registry = FindInScene<SpecialInfectedRegistry>(scene);
                HUDManager hud = FindInScene<HUDManager>(scene);
                if (threat == null) errors.Add("GameScene missing InfectionThreatService.");
                if (registry == null) errors.Add("GameScene missing SpecialInfectedRegistry.");
                if (hud == null) errors.Add("GameScene missing HUDManager.");

                if (registry != null)
                {
                    SerializedProperty entries = new SerializedObject(registry).FindProperty("specialTypes");
                    int infectorEntries = 0;
                    for (int i = 0; i < entries.arraySize; i++)
                    {
                        SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                        if ((SpecialType)entry.FindPropertyRelative("type").enumValueIndex == SpecialType.Infector
                            && (SpecialImplementationState)entry.FindPropertyRelative("implementationState").enumValueIndex == SpecialImplementationState.Playable
                            && entry.FindPropertyRelative("prefab").objectReferenceValue != null)
                            infectorEntries++;
                    }
                    if (infectorEntries != 1)
                        errors.Add($"GameScene needs exactly one authored Playable Infector registry entry; found {infectorEntries}.");
                }

                if (hud != null)
                {
                    var serializedHud = new SerializedObject(hud);
                    foreach (string property in new[] { "infectionFill", "infectionIcon", "infectionStageText", "treatmentProgressFill", "sepsisWarning" })
                        RequireReference(serializedHud, property, errors, $"HUDManager.{property}");

                    Image infectionFill = serializedHud.FindProperty("infectionFill")?.objectReferenceValue as Image;
                    Image treatmentFill = serializedHud.FindProperty("treatmentProgressFill")?.objectReferenceValue as Image;
                    GameObject sepsisWarning = serializedHud.FindProperty("sepsisWarning")?.objectReferenceValue as GameObject;
                    if (infectionFill != null && (infectionFill.type != Image.Type.Filled || infectionFill.fillMethod != Image.FillMethod.Horizontal))
                        errors.Add("HUD infectionFill must be authored as a horizontal Filled Image.");
                    if (treatmentFill != null && (treatmentFill.type != Image.Type.Filled || treatmentFill.fillMethod != Image.FillMethod.Horizontal))
                        errors.Add("HUD treatmentProgressFill must be authored as a horizontal Filled Image.");
                    if (sepsisWarning != null && sepsisWarning.activeSelf)
                        errors.Add("HUD sepsisWarning must be inactive by default.");
                }
            }
            finally
            {
                if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T result = root.GetComponentInChildren<T>(true);
                if (result != null) return result;
            }
            return null;
        }

        private static void Require<T>(GameObject prefab, List<string> errors, string label) where T : Component
        {
            if (prefab.GetComponent<T>() == null) errors.Add($"{label} missing {typeof(T).Name}.");
        }

        private static void RequireReference(SerializedObject target, string propertyName, List<string> errors, string label)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null) errors.Add($"Missing authored reference: {label}.");
        }
    }
}
