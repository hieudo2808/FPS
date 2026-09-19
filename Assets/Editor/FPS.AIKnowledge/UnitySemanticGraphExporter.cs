using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS.AIKnowledge.Editor
{
    /// <summary>
    /// Exports Unity's semantic object/reference view without editing project assets.
    /// The output intentionally lives outside Assets so it is not imported as a Unity asset.
    /// </summary>
    internal static class UnitySemanticGraphExporter
    {
        private const string OutputRelativePath = ".codex/unity-semantic-graph.json";
        private const int MaxWarnings = 2000;

        [MenuItem("Tools/FPS/AI Knowledge/Export Unity Semantic Graph")]
        private static void ExportMenuItem()
        {
            try
            {
                var report = Export();
                Debug.Log($"Unity semantic graph exported: {report.outputPath} " +
                          $"({report.assetCount} assets, {report.prefabCount} prefabs, " +
                          $"{report.sceneCount} scenes, {report.gameObjectCount} GameObjects, " +
                          $"{report.referenceCount} serialized references).");
            }
            catch (Exception exception)
            {
                Debug.LogError($"Unity semantic graph export failed: {exception}");
            }
        }

        private static ExportReport Export()
        {
            var report = new ExportReport
            {
                schemaVersion = 2,
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                projectPath = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty,
                outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputRelativePath))
            };

            CaptureGitMetadata(report);

            var originalLoadedScenes = GetLoadedScenePaths();
            var originalActiveScenePath = SceneManager.GetActiveScene().path;

            try
            {
                CaptureAssets(report);
                CapturePrefabs(report);
                CaptureScriptableObjects(report);
                CaptureAnimatorControllers(report);
                CaptureAllScenes(report, originalLoadedScenes);
                WriteReport(report);
            }
            finally
            {
                RestoreLoadedScenes(originalLoadedScenes, originalActiveScenePath);
                EditorUtility.ClearProgressBar();
            }

            return report;
        }

        private static void CaptureAssets(ExportReport report)
        {
            var paths = AssetDatabase.GetAllAssetPaths()
                .Where(IsProjectAssetPath)
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            for (var index = 0; index < paths.Length; index++)
            {
                var path = paths[index];
                ShowProgress("Assets", index, paths.Length, path);

                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                report.assets.Add(new AssetRecord
                {
                    path = path,
                    guid = AssetDatabase.AssetPathToGUID(path),
                    name = asset != null ? asset.name : Path.GetFileNameWithoutExtension(path),
                    type = AssetDatabase.GetMainAssetTypeAtPath(path)?.FullName ?? string.Empty
                });
            }

            report.assetCount = report.assets.Count;
        }

        private static void CapturePrefabs(ExportReport report)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (var index = 0; index < guids.Length; index++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[index]);
                ShowProgress("Prefabs", index, guids.Length, path);

                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                {
                    AddWarning(report, $"Could not load prefab: {path}");
                    continue;
                }

                var prefab = new PrefabRecord
                {
                    path = path,
                    guid = guids[index],
                    name = root.name,
                    prefabAssetType = PrefabUtility.GetPrefabAssetType(root).ToString()
                };
                CaptureGameObject(root, prefab.objects, report);
                report.prefabs.Add(prefab);
            }

            report.prefabCount = report.prefabs.Count;
        }

        private static void CaptureScriptableObjects(ExportReport report)
        {
            foreach (var asset in report.assets)
            {
                if (!string.Equals(Path.GetExtension(asset.path), ".asset", StringComparison.OrdinalIgnoreCase))
                    continue;

                var scriptableObject = AssetDatabase.LoadAssetAtPath<ScriptableObject>(asset.path);
                if (scriptableObject == null)
                    continue;

                var record = new ScriptableObjectRecord
                {
                    path = asset.path,
                    guid = asset.guid,
                    name = scriptableObject.name,
                    type = scriptableObject.GetType().FullName ?? string.Empty
                };
                CaptureSerializedReferences(scriptableObject, record.serializedReferences, report);
                report.scriptableObjects.Add(record);
            }
        }

        private static void CaptureAnimatorControllers(ExportReport report)
        {
            var guids = AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null)
                    continue;

                var record = new AnimatorControllerRecord
                {
                    path = path,
                    guid = guid,
                    name = controller.name
                };
                record.parameters.AddRange(controller.parameters.Select(parameter => new AnimatorParameterRecord
                {
                    name = parameter.name,
                    type = parameter.type.ToString()
                }));
                record.layers.AddRange(controller.layers.Select(layer => new AnimatorLayerRecord
                {
                    name = layer.name,
                    stateCount = layer.stateMachine != null ? layer.stateMachine.states.Length : 0
                }));
                report.animatorControllers.Add(record);
            }
        }

        private static void CaptureAllScenes(ExportReport report, IReadOnlyCollection<string> originalLoadedScenes)
        {
            var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            for (var index = 0; index < sceneGuids.Length; index++)
            {
                var path = AssetDatabase.GUIDToAssetPath(sceneGuids[index]);
                ShowProgress("Scenes", index, sceneGuids.Length, path);

                var wasLoaded = originalLoadedScenes.Contains(path, StringComparer.OrdinalIgnoreCase);
                var scene = wasLoaded ? SceneManager.GetSceneByPath(path) : default;
                var openedForExport = false;

                try
                {
                    if (!wasLoaded)
                    {
                        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        openedForExport = scene.IsValid() && scene.isLoaded;
                    }

                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        AddWarning(report, $"Could not load scene: {path}");
                        continue;
                    }

                    var sceneRecord = new SceneRecord
                    {
                        path = path,
                        guid = sceneGuids[index],
                        name = scene.name,
                        wasLoadedBeforeExport = wasLoaded
                    };
                    foreach (var root in scene.GetRootGameObjects())
                        CaptureGameObject(root, sceneRecord.objects, report);

                    report.scenes.Add(sceneRecord);
                }
                catch (Exception exception)
                {
                    AddWarning(report, $"Scene export failed for {path}: {exception.Message}");
                }
                finally
                {
                    if (openedForExport && scene.IsValid() && scene.isLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }

            report.sceneCount = report.scenes.Count;
        }

        private static void CaptureGameObject(GameObject gameObject, List<GameObjectRecord> destination, ExportReport report)
        {
            if (gameObject == null)
                return;

            var record = new GameObjectRecord
            {
                name = gameObject.name,
                path = GetObjectPath(gameObject.transform),
                activeSelf = gameObject.activeSelf,
                tag = gameObject.tag,
                layer = gameObject.layer
            };

            foreach (var component in gameObject.GetComponents<Component>())
            {
                if (component == null)
                {
                    record.components.Add(new ComponentRecord { missing = true });
                    continue;
                }

                var componentRecord = new ComponentRecord
                {
                    type = component.GetType().FullName ?? string.Empty,
                    assembly = component.GetType().Assembly.GetName().Name ?? string.Empty
                };
                CaptureSerializedReferences(component, componentRecord.serializedReferences, report);
                record.components.Add(componentRecord);
            }

            destination.Add(record);
            report.gameObjectCount++;
            foreach (Transform child in gameObject.transform)
                CaptureGameObject(child.gameObject, destination, report);
        }

        private static void CaptureSerializedReferences(UnityEngine.Object target,
            List<SerializedReferenceRecord> destination, ExportReport report)
        {
            try
            {
                var serializedObject = new SerializedObject(target);
                var property = serializedObject.GetIterator();
                var enterChildren = true;

                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyPath == "m_Script" ||
                        property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    var referencedObject = property.objectReferenceValue;
                    var missing = referencedObject == null && property.objectReferenceEntityIdValue != 0;
                    if (referencedObject == null && !missing)
                        continue;

                    var assetPath = referencedObject != null ? AssetDatabase.GetAssetPath(referencedObject) : string.Empty;
                    destination.Add(new SerializedReferenceRecord
                    {
                        propertyPath = property.propertyPath,
                        targetName = referencedObject != null ? referencedObject.name : string.Empty,
                        targetType = referencedObject != null ? referencedObject.GetType().FullName ?? string.Empty : string.Empty,
                        targetAssetPath = IsProjectAssetPath(assetPath) ? assetPath : string.Empty,
                        targetGuid = IsProjectAssetPath(assetPath) ? AssetDatabase.AssetPathToGUID(assetPath) : string.Empty,
                        targetGlobalId = referencedObject != null ? GetGlobalId(referencedObject) : string.Empty,
                        missing = missing
                    });
                    report.referenceCount++;
                }
            }
            catch (Exception exception)
            {
                AddWarning(report, $"Serialized reference scan failed for {target.name}: {exception.Message}");
            }
        }

        private static void WriteReport(ExportReport report)
        {
            var directory = Path.GetDirectoryName(report.outputPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Could not resolve semantic graph output directory.");

            Directory.CreateDirectory(directory);
            File.WriteAllText(report.outputPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        }

        private static void CaptureGitMetadata(ExportReport report)
        {
            if (!TryRunGit(report.projectPath, "rev-parse --verify HEAD", out var revision))
            {
                report.gitMetadataError = "Could not read the current Git revision.";
                return;
            }

            if (!TryRunGit(report.projectPath, "status --porcelain=v1", out var status))
            {
                report.gitRevision = revision;
                report.gitMetadataError = "Could not read the current Git status.";
                return;
            }

            report.gitRevision = revision;
            report.gitStatusEntryCount = status
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Length;
            report.gitStatusDirty = report.gitStatusEntryCount > 0;
        }

        private static bool TryRunGit(string workingDirectory, string arguments, out string output)
        {
            output = string.Empty;
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    if (process == null)
                        return false;

                    output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static List<string> GetLoadedScenePaths()
        {
            var paths = new List<string>();
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded && !string.IsNullOrEmpty(scene.path))
                    paths.Add(scene.path);
            }
            return paths;
        }

        private static void RestoreLoadedScenes(IReadOnlyCollection<string> originalLoadedScenes, string originalActiveScenePath)
        {
            foreach (var path in originalLoadedScenes)
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded)
                    continue;

                try
                {
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Could not restore scene {path}: {exception.Message}");
                }
            }

            if (!string.IsNullOrEmpty(originalActiveScenePath))
            {
                var activeScene = SceneManager.GetSceneByPath(originalActiveScenePath);
                if (activeScene.IsValid() && activeScene.isLoaded)
                    SceneManager.SetActiveScene(activeScene);
            }
        }

        private static string GetObjectPath(Transform transform)
        {
            var segments = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
                segments.Push(current.name);
            return string.Join("/", segments);
        }

        private static string GetGlobalId(UnityEngine.Object target)
        {
            try
            {
                return GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsProjectAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   (path.Equals("Assets", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase));
        }

        private static void ShowProgress(string phase, int index, int total, string detail)
        {
            var progress = total == 0 ? 1f : (float)index / total;
            EditorUtility.DisplayProgressBar("FPS AI Knowledge", $"{detail} ({index + 1}/{total})", progress);
            if (progress < 0f)
                EditorUtility.ClearProgressBar();
        }

        private static void AddWarning(ExportReport report, string warning)
        {
            if (report.warnings.Count < MaxWarnings)
                report.warnings.Add(warning);
        }

        [Serializable]
        private sealed class ExportReport
        {
            public int schemaVersion;
            public string generatedAtUtc;
            public string unityVersion;
            public string projectPath;
            public string outputPath;
            public string gitRevision;
            public bool gitStatusDirty;
            public int gitStatusEntryCount;
            public string gitMetadataError;
            public int assetCount;
            public int prefabCount;
            public int sceneCount;
            public int gameObjectCount;
            public int referenceCount;
            public List<AssetRecord> assets = new();
            public List<PrefabRecord> prefabs = new();
            public List<SceneRecord> scenes = new();
            public List<ScriptableObjectRecord> scriptableObjects = new();
            public List<AnimatorControllerRecord> animatorControllers = new();
            public List<string> warnings = new();
        }

        [Serializable]
        private sealed class AssetRecord
        {
            public string path;
            public string guid;
            public string name;
            public string type;
        }

        [Serializable]
        private sealed class PrefabRecord
        {
            public string path;
            public string guid;
            public string name;
            public string prefabAssetType;
            public List<GameObjectRecord> objects = new();
        }

        [Serializable]
        private sealed class SceneRecord
        {
            public string path;
            public string guid;
            public string name;
            public bool wasLoadedBeforeExport;
            public List<GameObjectRecord> objects = new();
        }

        [Serializable]
        private sealed class GameObjectRecord
        {
            public string name;
            public string path;
            public bool activeSelf;
            public string tag;
            public int layer;
            public List<ComponentRecord> components = new();
        }

        [Serializable]
        private sealed class ComponentRecord
        {
            public string type;
            public string assembly;
            public bool missing;
            public List<SerializedReferenceRecord> serializedReferences = new();
        }

        [Serializable]
        private sealed class SerializedReferenceRecord
        {
            public string propertyPath;
            public string targetName;
            public string targetType;
            public string targetAssetPath;
            public string targetGuid;
            public string targetGlobalId;
            public bool missing;
        }

        [Serializable]
        private sealed class ScriptableObjectRecord
        {
            public string path;
            public string guid;
            public string name;
            public string type;
            public List<SerializedReferenceRecord> serializedReferences = new();
        }

        [Serializable]
        private sealed class AnimatorControllerRecord
        {
            public string path;
            public string guid;
            public string name;
            public List<AnimatorParameterRecord> parameters = new();
            public List<AnimatorLayerRecord> layers = new();
        }

        [Serializable]
        private sealed class AnimatorParameterRecord
        {
            public string name;
            public string type;
        }

        [Serializable]
        private sealed class AnimatorLayerRecord
        {
            public string name;
            public int stateCount;
        }
    }
}
