using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPS.Editor
{
    public static class HelicopterLandingAuthoring
    {
        private static void SaveAuthoredScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!string.IsNullOrEmpty(scene.path) && !scene.name.StartsWith("Untitled")
                    && !scene.path.StartsWith("Assets/InitTestScene", System.StringComparison.Ordinal)
                    && !EditorSceneManager.IsPreviewScene(scene) && scene.isDirty)
                    EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/FPS/Helicopter/Create landing equip clips")]
        public static void CreateEquipClips()
        {
            SaveAuthoredScenes();
            foreach (var pose in Object.FindObjectsByType<HelicopterRappellerPose>(FindObjectsInactive.Include))
            {
                string character = pose.name.Replace("Rappel_Double_", "");
                string native = (character == "Brimstone" ? "Brimstone_" : "") + "TP_Core_AK_S0_Equip_UB_6d8d3d9c_PathBound";
                var source = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Vandal/{native}.anim");
                if (source == null || pose.armedIdle == null) throw new System.InvalidOperationException("Missing equip source for " + pose.name);
                string path = $"Assets/FPS/Features/World/Content/Vehicles/Helicopters/Mi24/Animations/{character}_InsertionEquip.anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                bool create = clip == null;
                if (create) clip = new AnimationClip();
                EditorUtility.CopySerialized(pose.armedIdle, clip);
                clip.name = character + "_InsertionEquip";
                var bindings = AnimationUtility.GetCurveBindings(pose.armedIdle);
                string prefix = bindings.First(b => b.path.Contains("/Skeleton/")).path.Split(new[] { "/Skeleton/" }, System.StringSplitOptions.None)[0];
                float duration = AnimationUtility.GetAnimationClipSettings(source).stopTime;
                clip.ClearCurves();
                // Keep the planted lower body; the existing equip supplies the arms and weapon master.
                foreach (var binding in bindings.Where(b => b.type == typeof(Transform)
                    && (!b.path.Contains("/Splitter/") || b.path.Contains("/Pelvis"))))
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, duration,
                        AnimationUtility.GetEditorCurve(pose.armedIdle, binding).Evaluate(0)));
                foreach (var binding in AnimationUtility.GetCurveBindings(source).Where(b => b.type == typeof(Transform)
                    && b.path.Contains("/Splitter/") && !b.path.Contains("/Pelvis")))
                {
                    var target = binding;
                    target.path = prefix + binding.path.Substring(binding.path.IndexOf("/Skeleton/", System.StringComparison.Ordinal));
                    if (pose.transform.Find(target.path) != null)
                    {
                        var curve = AnimationUtility.GetEditorCurve(source, binding);
                        curve.keys = curve.keys.Where(k => k.time < duration).Append(new Keyframe(duration, curve.Evaluate(duration))).ToArray();
                        AnimationUtility.SetEditorCurve(clip, target, curve);
                    }
                }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.startTime = 0; settings.stopTime = duration; settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AnimationUtility.SetAnimationEvents(clip, System.Array.Empty<AnimationEvent>());
                clip.EnsureQuaternionContinuity();
                if (create) AssetDatabase.CreateAsset(clip, path);
                EditorUtility.SetDirty(clip);
                pose.armedEquip = clip;
                EditorUtility.SetDirty(pose);
                EditorSceneManager.MarkSceneDirty(pose.gameObject.scene);
            }
            SaveAuthoredScenes();
        }

        [MenuItem("Tools/FPS/Helicopter/Repair landing walk clips")]
        public static void RepairWalkClips()
        {
            SaveAuthoredScenes();
            foreach (string character in new[] { "Brimstone", "Clove", "Gekko", "Sage" })
            {
                // Brimstone uses a different bone orientation; the other three share the Clove rig.
                string source = character == "Brimstone" ? "Brimstone_Walk_3b2899d1_PathBound" : "Walk_3b2899d1_PathBound";
                var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/FPS/Features/Characters/Animation/Content/3P/Shared/{source}.anim");
                string path = $"Assets/FPS/Features/World/Content/Vehicles/Helicopters/Mi24/Animations/{character}_InsertionArmedWalk.anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                var bindings = AnimationUtility.GetCurveBindings(clip);
                string prefix = bindings.First(b => b.path.Contains("/Skeleton/")).path.Split(new[] { "/Skeleton/" }, System.StringSplitOptions.None)[0];
                foreach (var binding in bindings.Where(b => b.path.Contains("/Pelvis") && b.propertyName.Contains("localEulerAngles")))
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                foreach (var binding in AnimationUtility.GetCurveBindings(walk).Where(b => b.path.Contains("/Pelvis") && b.propertyName.StartsWith("m_LocalRotation")))
                {
                    var target = binding;
                    target.path = prefix + binding.path.Substring(binding.path.IndexOf("/Skeleton/", System.StringComparison.Ordinal));
                    AnimationUtility.SetEditorCurve(clip, target, AnimationUtility.GetEditorCurve(walk, binding));
                }
                clip.EnsureQuaternionContinuity();
                EditorUtility.SetDirty(clip);
            }
            foreach(var pose in Object.FindObjectsByType<HelicopterRappellerPose>(FindObjectsInactive.Include))
            {
                var probe=Object.Instantiate(pose);
                var mesh=new Mesh();
                try
                {
                    probe.Pose(0,5,4.25f);
                    var body=probe.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.bones.Contains(probe.leftFoot));
                    body.BakeMesh(mesh,false);
                    float bottom=mesh.vertices.Min(v=>(body.transform.position+body.transform.rotation*v).y);
                    pose.footSoleHeight=probe.footSoleHeight-bottom+probe.transform.position.y;
                    if(pose.footSoleHeight<.01f || pose.footSoleHeight>.3f)
                        throw new System.InvalidOperationException("Invalid sole calibration for "+pose.name);
                    EditorUtility.SetDirty(pose);
                    Debug.Log($"{pose.name}: foot sole height {pose.footSoleHeight:F3}m.");
                }
                finally { Object.DestroyImmediate(probe.gameObject); Object.DestroyImmediate(mesh); }
            }
            SaveAuthoredScenes();
            Debug.Log("Repaired all four insertion walk clips with the existing lower-body walking animation.");
        }

    }
}
