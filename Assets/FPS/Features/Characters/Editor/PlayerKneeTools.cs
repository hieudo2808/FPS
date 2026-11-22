#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FPS.EditorTools
{
    // Art round-trip keeps the imported skeleton, materials, UVs and vertex order intact.
    public static class PlayerKneeTools
    {
        const string Players = "Assets/FPS/Features/Characters/Content/Players";
        const string Output = "Reports/BlenderWork/";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] public class MatrixData { public float[] values; }
        [Serializable] public class SkinData
        {
            public string mesh, source;
            public Vector3[] vertices, normals, knees;
            public int[] triangles, indices;
            public float[] weights;
            public string[] bones;
            public MatrixData[] matrices;
        }
        [Serializable] public class ShapeData { public Vector3[] delta; }

        static void SaveAuthored()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!string.IsNullOrEmpty(scene.path) && scene.isDirty) EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }

        static object Call(PlayerLifePresenter presenter, string method, params object[] args)
            => typeof(PlayerLifePresenter).GetMethod(method, Private).Invoke(presenter, args);

        [MenuItem("Tools/FPS/Player Life/Export Knee Pose")]
        public static void Export() => Review(true);

        [MenuItem("Tools/FPS/Player Life/Capture Knee Pose")]
        public static void Capture() => Review(false);

        [MenuItem("Tools/FPS/Player Life/Install Blender Knee Shapes")]
        public static void Install()
        {
            SaveAuthored();
            try
            {
                foreach (string name in new[] { "Brimstone", "Clove", "Gekko", "Sage" })
                {
                    string path = $"{Players}/{name}/{name}Player.prefab";
                    var data = JsonUtility.FromJson<SkinData>(File.ReadAllText(Output + name + "-skin.json"));
                    var shape = JsonUtility.FromJson<ShapeData>(File.ReadAllText(Output + name + "-shape.json"));
                    var source = AssetDatabase.LoadAllAssetsAtPath(data.source).OfType<Mesh>().Single(m => m.name == data.mesh);
                    if (shape.delta.Length != source.vertexCount || !source.vertices.SequenceEqual(data.vertices))
                        throw new InvalidOperationException(name + ": source topology changed since Blender export");
                    if (shape.delta.Any(d => !float.IsFinite(d.x) || !float.IsFinite(d.y) || !float.IsFinite(d.z) || d.magnitude > 0.0005f))
                        throw new InvalidOperationException(name + ": invalid knee delta");
                    var corrected = Object.Instantiate(source);
                    corrected.name = source.name + " Knee Corrected";
                    var normalMesh = Object.Instantiate(source);
                    normalMesh.RecalculateNormals();
                    var baseNormals = normalMesh.normals;
                    normalMesh.vertices = source.vertices.Zip(shape.delta, (v, d) => v + d).ToArray();
                    normalMesh.RecalculateNormals();
                    var originalNormals = source.normals;
                    var correctedNormals = normalMesh.normals;
                    var normalDelta = new Vector3[source.vertexCount];
                    var tangentDelta = new Vector3[source.vertexCount];
                    var tangents = source.tangents;
                    for (int i = 0; i < source.vertexCount; i++)
                    {
                        Quaternion turn = baseNormals[i].sqrMagnitude > 1e-12f && correctedNormals[i].sqrMagnitude > 1e-12f
                            ? Quaternion.FromToRotation(baseNormals[i], correctedNormals[i]) : Quaternion.identity;
                        normalDelta[i] = turn * originalNormals[i] - originalNormals[i];
                        Vector3 tangent = tangents[i];
                        tangentDelta[i] = turn * tangent - tangent;
                    }
                    corrected.AddBlendShapeFrame(PlayerLifePresenter.KneeVolumeShape, 100f, shape.delta, normalDelta, tangentDelta);
                    Object.DestroyImmediate(normalMesh);
                    string meshPath = $"{Players}/{name}/Models/DownedKneeMesh.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (existing == null) AssetDatabase.CreateAsset(corrected, meshPath);
                    else { EditorUtility.CopySerialized(corrected, existing); Object.DestroyImmediate(corrected); corrected = existing; }
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var skin = root.GetComponent<WeaponManager>().CharacterAnimation.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                            .Single(s => s.sharedMesh == source || AssetDatabase.GetAssetPath(s.sharedMesh) == meshPath);
                        skin.sharedMesh = corrected;
                        skin.SetBlendShapeWeight(corrected.GetBlendShapeIndex(PlayerLifePresenter.KneeVolumeShape), 0f);
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                    Debug.Log($"[KneeInstall] {name}: {source.vertexCount} vertices preserved; {shape.delta.Count(d => d.sqrMagnitude > 1e-18f)} corrected; {meshPath}");
                }
            }
            finally { SaveAuthored(); }
        }

        static void Review(bool export)
        {
            SaveAuthored();
            Directory.CreateDirectory(Output);
            foreach (string name in new[] { "Brimstone", "Clove", "Gekko", "Sage" })
            {
                Scene scene = EditorSceneManager.NewPreviewScene();
                GameObject root = null;
                try
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Players}/{name}/{name}Player.prefab");
                    root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    var presenter = root.GetComponent<PlayerLifePresenter>();
                    var visibility = root.GetComponent<PlayerVisibilityController>();
                    var animator = root.GetComponent<WeaponManager>().CharacterAnimation;
                    Call(presenter, "Awake");
                    visibility.FirstPersonArms.SetActive(false);
                    var classic = visibility.ThirdPersonWeaponPresentations.First(p => p != null && p.WeaponData != null && p.WeaponData.usableWhileDowned);
                    foreach (var entry in visibility.ThirdPersonWeaponPresentations)
                        if (entry?.WeaponObject != null) entry.WeaponObject.SetActive(entry == classic);
                    animator.gameObject.SetActive(true);
                    animator.runtimeAnimatorController = classic.CharacterController;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.enabled = r.transform.IsChildOf(animator.transform);
                    foreach (var c in root.GetComponentsInChildren<Camera>(true)) c.enabled = false;
                    Call(presenter, "BeginDowned", true);
                    ((PlayableGraph)typeof(PlayerLifePresenter).GetField("graph", Private).GetValue(presenter)).Evaluate();
                    Call(presenter, "UpdateThirdPerson", 1f);
                    var skin = animator.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(s => s.sharedMesh.vertexCount).First();
                    if (export) ExportSkin(skin, name);
                    Vector3[] after = Shoot(skin, scene, name, "after");
                    int shapeIndex = skin.sharedMesh.GetBlendShapeIndex(PlayerLifePresenter.KneeVolumeShape);
                    if (shapeIndex >= 0) skin.SetBlendShapeWeight(shapeIndex, 0f);
                    var defaults = (System.Collections.Generic.Dictionary<Transform, Quaternion>)typeof(PlayerLifePresenter).GetField("bodyDefaults", Private).GetValue(presenter);
                    foreach (var bone in new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg })
                    {
                        var knee = animator.GetBoneTransform(bone);
                        var corrective = knee.Find(knee.name + "_Ndl");
                        if (corrective != null) corrective.localRotation = Quaternion.Inverse(knee.localRotation) * defaults[knee] * defaults[corrective];
                    }
                    Vector3[] before = Shoot(skin, scene, name, "before");
                    float change = before.Zip(after, (a, b) => Vector3.Distance(skin.transform.TransformPoint(a), skin.transform.TransformPoint(b))).Max();
                    Debug.Log($"[KneeReview] {name}: maximum posed before/after change={change * 1000f:F3} mm");
                    if (change < 0.0001f) throw new InvalidOperationException(name + ": capture did not observe knee change");
                    Debug.Log($"[KneeReview] {name}: mesh={skin.sharedMesh.name} vertices={skin.sharedMesh.vertexCount} helpers={presenter.HelperBoneCount} hold={presenter.SidearmHoldBoneCount} raise={presenter.RaiseWeight}");
                    Call(presenter, "ReleaseImmediately");
                }
                finally
                {
                    if (root != null) Object.DestroyImmediate(root);
                    EditorSceneManager.ClosePreviewScene(scene);
                    SaveAuthored();
                }
            }
        }

        static void ExportSkin(SkinnedMeshRenderer skin, string name)
        {
            Mesh mesh = skin.sharedMesh;
            string sourcePath = $"{Players}/{name}/Models/{(name == "Clove" ? "Body" : "Human")}.fbx";
            var imported = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<Mesh>().OrderByDescending(m => m.vertexCount).First();
            if (!mesh.vertices.SequenceEqual(imported.vertices)) throw new InvalidOperationException(name + ": body no longer matches source FBX");
            var weights = mesh.boneWeights;
            var data = new SkinData
            {
                mesh = imported.name, source = sourcePath, vertices = mesh.vertices, normals = mesh.normals,
                triangles = mesh.triangles, bones = skin.bones.Select(b => b.name).ToArray(),
                indices = weights.SelectMany(w => new[] { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 }).ToArray(),
                weights = weights.SelectMany(w => new[] { w.weight0, w.weight1, w.weight2, w.weight3 }).ToArray(),
                matrices = skin.bones.Select((b, i) =>
                {
                    Matrix4x4 m = skin.transform.worldToLocalMatrix * b.localToWorldMatrix * mesh.bindposes[i];
                    return new MatrixData { values = Enumerable.Range(0, 16).Select(j => m[j / 4, j % 4]).ToArray() };
                }).ToArray(),
                knees = new[] { "L_Knee", "R_Knee" }.Select(k => mesh.bindposes[Array.FindIndex(skin.bones, b => b.name == k)].inverse.MultiplyPoint3x4(Vector3.zero)).ToArray()
            };
            File.WriteAllText(Output + name + "-skin.json", JsonUtility.ToJson(data));
        }

        static Vector3[] Shoot(SkinnedMeshRenderer skin, Scene scene, string name, string suffix)
        {
            var baked = Object.Instantiate(skin.sharedMesh);
            var positions = baked.vertices;
            var normals = baked.normals;
            var tangents = baked.tangents;
            var dv = new Vector3[baked.vertexCount];
            var dn = new Vector3[baked.vertexCount];
            var dt = new Vector3[baked.vertexCount];
            for (int shape = 0; shape < baked.blendShapeCount; shape++)
            {
                baked.GetBlendShapeFrameVertices(shape, 0, dv, dn, dt);
                float amount = skin.GetBlendShapeWeight(shape) * 0.01f;
                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] += dv[i] * amount;
                    normals[i] += dn[i] * amount;
                    Vector3 tangent = (Vector3)tangents[i] + dt[i] * amount;
                    tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
                }
            }
            var matrices = skin.bones.Select((b, i) => skin.transform.worldToLocalMatrix * b.localToWorldMatrix * baked.bindposes[i]).ToArray();
            var weights = baked.boneWeights;
            for (int i = 0; i < positions.Length; i++)
            {
                var w = weights[i];
                Vector3 Point(Vector3 p) => matrices[w.boneIndex0].MultiplyPoint3x4(p) * w.weight0 + matrices[w.boneIndex1].MultiplyPoint3x4(p) * w.weight1
                    + matrices[w.boneIndex2].MultiplyPoint3x4(p) * w.weight2 + matrices[w.boneIndex3].MultiplyPoint3x4(p) * w.weight3;
                Vector3 Direction(Vector3 p) => matrices[w.boneIndex0].MultiplyVector(p) * w.weight0 + matrices[w.boneIndex1].MultiplyVector(p) * w.weight1
                    + matrices[w.boneIndex2].MultiplyVector(p) * w.weight2 + matrices[w.boneIndex3].MultiplyVector(p) * w.weight3;
                positions[i] = Point(positions[i]);
                normals[i] = Direction(normals[i]).normalized;
                Vector3 tangent = Direction(tangents[i]).normalized;
                tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
            }
            baked.ClearBlendShapes();
            baked.vertices = positions;
            baked.normals = normals;
            baked.tangents = tangents;
            baked.RecalculateBounds();
            var frozen = new GameObject("Baked review body");
            frozen.transform.SetParent(skin.transform, false);
            frozen.AddComponent<MeshFilter>().sharedMesh = baked;
            frozen.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
            bool enabled = skin.enabled;
            skin.enabled = false;
            var camera = new GameObject("Knee review camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.19f, 0.20f, 0.22f);
            camera.orthographic = true;
            camera.allowHDR = false;
            var light = new GameObject("Knee review light").AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(light.gameObject, scene);
            light.type = LightType.Directional;
            light.intensity = 2f;
            light.transform.rotation = Quaternion.Euler(35f, -45f, 0f);
            var animator = skin.GetComponentInParent<Animator>();
            Vector3 center = (animator.GetBoneTransform(HumanBodyBones.Head).position
                + (animator.GetBoneTransform(HumanBodyBones.LeftFoot).position + animator.GetBoneTransform(HumanBodyBones.RightFoot).position) * 0.5f) * 0.5f;
            var rt = new RenderTexture(1600, 1000, 24);
            var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            camera.targetTexture = rt;
            RenderTexture previous = RenderTexture.active;
            try
            {
                for (int view = 0; view < 2; view++)
                {
                    Transform knee = skin.bones.FirstOrDefault(b => b != null && (b.name == "R_Knee" || b.name.Contains("R_Knee")));
                    Vector3 focus = view == 0 ? center : (knee != null ? knee.position : center);
                    camera.orthographicSize = view == 0 ? 0.8f : 0.27f;
                    camera.transform.position = focus + new Vector3(1f, 0.48f, 0.23f).normalized * 4f;
                    camera.transform.LookAt(focus);
                    camera.Render();
                    RenderTexture.active = rt;
                    image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
                    image.Apply();
                    File.WriteAllBytes($"{Output}{name}-{suffix}-{(view == 0 ? "body" : "knee")}.png", image.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                rt.Release();
                skin.enabled = enabled;
                foreach (var o in new Object[] { frozen, baked, image, rt, camera.gameObject, light.gameObject }) Object.DestroyImmediate(o);
            }
            return positions;
        }
    }
}
#endif
