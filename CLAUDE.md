# FPS Project Working Rules

## Unity Project Exploration Rules
This is a large Unity project. Do not begin non-trivial work with a repository-wide recursive grep.
- For scenes, prefabs, GameObjects, components, GUIDs, serialized references, or current Editor state, query Unity-Skills first.
- For C# symbols, callers/callees, architecture, references, or change impact, query the local Codebase Memory MCP first.
- Read source only after the relevant symbols/assets have been narrowed down.
- Do not read raw `.meta`, `.unity`, or `.prefab` YAML when the Editor route can answer the question.
- Treat both indexes as best-effort evidence; check coverage, timestamps, Git status, source, and Unity state before making negative or destructive claims.

## Mandatory Unity Save Gate
- Never save Untitled, unsaved, or temporary Test Runner scenes. Never open a Save Scene dialog for them. This explicit user preference persists across sessions.
- Before entering Play Mode, opening or running Test Runner, loading/reloading a scene, triggering a domain reload, recompiling scripts, restarting Unity, or making a sequence of Unity Editor changes: save authored scenes that already have asset paths (skip Untitled and temporary test scenes) and call `AssetDatabase.SaveAssets()`.
- After each such operation sequence and after every test run: save only those authored scenes and call `AssetDatabase.SaveAssets()` again.
- Do not claim that work is complete until the active project scene has been checked in Unity and reports `isDirty == false`.
- Preferred save command:
  ```csharp
  for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
  {
      var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
      if (!string.IsNullOrEmpty(scene.path) && !scene.name.StartsWith("Untitled")
          && !scene.path.StartsWith("Assets/InitTestScene", System.StringComparison.Ordinal)
          && !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene)
          && scene.isDirty)
          UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
  }
  UnityEditor.AssetDatabase.SaveAssets();
  ```
