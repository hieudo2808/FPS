using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FPS.Editor
{
    public static class TankSceneVerification
    {
        private const string GameplayScenePath = "Assets/FPS/Scenes/GameScene.unity";

        [MenuItem("FPS/Verify Tank in Scene")]
        public static void VerifyTankInScene()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string reportPath = Path.Combine(projectRoot, "Logs", "TankSceneVerificationReport.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            var report = new StringBuilder();
            report.AppendLine("=== TANK SCENE VERIFICATION REPORT ===");
            report.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            GameObject temporaryTank = null;
            bool success = false;

            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TankPrefabUtility.PrefabPath);
                Require(prefab != null, "Tank prefab exists.", report);
                Require(prefab.GetComponent<SI_Tank>() != null, "SI_Tank component exists.", report);
                Require(prefab.GetComponent<EnemyHealth>() != null, "EnemyHealth component exists.", report);
                Require(prefab.GetComponent<NavMeshAgent>() != null, "NavMeshAgent component exists.", report);
                Require(prefab.GetComponent<Animator>()?.runtimeAnimatorController != null,
                    "Animator controller is assigned.", report);

                Scene gameplayScene = SceneManager.GetSceneByPath(GameplayScenePath);
                if (!gameplayScene.IsValid() || !gameplayScene.isLoaded)
                    gameplayScene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);

                NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
                Require(triangulation.vertices != null && triangulation.vertices.Length >= 2,
                    "Gameplay scene has baked NavMesh data.", report);

                Vector3 spawnPoint = triangulation.vertices[0];
                Require(NavMesh.SamplePosition(spawnPoint, out NavMeshHit spawnHit, 2f, NavMesh.AllAreas),
                    "Tank spawn resolves onto NavMesh.", report);

                temporaryTank = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gameplayScene);
                temporaryTank.name = "Tank_Verification_Temporary";
                temporaryTank.transform.position = spawnHit.position;

                NavMeshAgent agent = temporaryTank.GetComponent<NavMeshAgent>();
                agent.Warp(spawnHit.position);
                Require(agent.enabled && agent.isOnNavMesh, "Tank agent is bound to NavMesh.", report);

                NavMeshPath path = new NavMeshPath();
                bool pathFound = false;
                for (int i = 1; i < triangulation.vertices.Length; i++)
                {
                    if ((triangulation.vertices[i] - spawnHit.position).sqrMagnitude < 4f)
                        continue;

                    if (NavMesh.CalculatePath(spawnHit.position, triangulation.vertices[i], NavMesh.AllAreas, path)
                        && path.status == NavMeshPathStatus.PathComplete)
                    {
                        pathFound = true;
                        break;
                    }
                }
                Require(pathFound, "Tank can calculate a complete NavMesh path.", report);

                EnemyHealth health = temporaryTank.GetComponent<EnemyHealth>();
                SI_Tank tank = temporaryTank.GetComponent<SI_Tank>();
                Require(Mathf.Approximately(health.MaxHealth, 2500f), "Prefab authored MaxHP is 2500.", report);
                Require(Mathf.Approximately(tank.HealthPerPlayer, 2500f), "Tank healthPerPlayer is 2500.", report);
                Require(Mathf.Approximately(tank.StaggerDamageFraction, 0.15f), "Stagger fraction is 15%.", report);

                success = true;
                report.AppendLine("=== VERIFICATION COMPLETE: ALL CHECKS PASSED ===");
            }
            catch (Exception exception)
            {
                report.AppendLine($"[FAIL] {exception.Message}");
                Debug.LogException(exception);
            }
            finally
            {
                if (temporaryTank != null)
                    UnityEngine.Object.DestroyImmediate(temporaryTank);

                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene restoredScene = SceneManager.GetSceneAt(i);
                    if (restoredScene.isDirty)
                    {
                        success = false;
                        report.AppendLine($"[FAIL] Restored scene remains dirty: {restoredScene.path}");
                    }
                }
                File.WriteAllText(reportPath, report.ToString());
            }

            if (!success)
                throw new InvalidOperationException($"Tank scene verification failed. See {reportPath}");

            Debug.Log("[TankSceneVerification] Verification passed.\n" + report);
        }

        private static void Require(bool condition, string message, StringBuilder report)
        {
            if (!condition)
                throw new InvalidOperationException(message);

            report.AppendLine("[PASS] " + message);
        }
    }
}
