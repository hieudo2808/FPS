using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FPS.EditorTools
{
    /// <summary>
    /// The 3P Death set is authored on the shared Core_Skeleton. Its root object is named differently from
    /// the player models, so Generic path binding never matches. Import it as Humanoid with the same Avatar
    /// the shared TP_Core clips copy (TP_Core_IdlePose), so it retargets onto all four player characters.
    /// </summary>
    public static class PlayerLifeClipImport
    {
        public const string DeathFolder = "Assets/FPS/Features/Characters/Animation/Content/3P/Death";
        public const string SetPath = "Assets/FPS/Features/Characters/Animation/Content/3P/PlayerLifeAnimationSet.asset";
        const string PlayersFolder = "Assets/FPS/Features/Characters/Content/Players";
        const string DownedFall = "TP_Core_Death_Leg_Back_LG";
        const string AvatarSource = "Assets/FPS/Features/Characters/Animation/Content/3P/Shared/TP_Core_IdlePose.fbx";
        static readonly System.Reflection.FieldInfo ParentName = typeof(SkeletonBone).GetField("parentName",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        [MenuItem("Tools/FPS/Player Life/Import Death Set As Humanoid")]
        public static void ImportDeathSet()
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(AvatarSource).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman) { Debug.LogError("[PlayerLife] No humanoid avatar in " + AvatarSource); return; }
            // The full mapping and rest pose live on the Avatar itself (the importer stores none).
            HumanDescription sourceDescription = avatar.humanDescription;
            var restPose = sourceDescription.skeleton.GroupBy(b => b.name).ToDictionary(g => g.Key, g => g.First());
            int changed = 0, total = 0, valid = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { DeathFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                total++;
                importer.animationType = ModelImporterAnimationType.Human;
                // The files carry an extra Core_Skeleton.001 level, so Copy From Other fails the hierarchy
                // check. Build the Avatar from the file itself with the shared mapping and the shared rest
                // pose, so it retargets exactly like the TP_Core clips.
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var description = sourceDescription;
                string rootName = model.name + "(Clone)";
                description.skeleton = model.GetComponentsInChildren<Transform>(true).Select(t =>
                {
                    bool root = t == model.transform;
                    SkeletonBone bone = !root && restPose.TryGetValue(t.name, out var shared)
                        ? shared
                        : new SkeletonBone { name = root ? rootName : t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale };
                    // Skeleton hangs under Core_Skeleton.001 here, not under the IdlePose root.
                    object boxed = bone;
                    ParentName.SetValue(boxed, root ? string.Empty : t.parent == model.transform ? rootName : t.parent.name);
                    return (SkeletonBone)boxed;
                }).ToArray();
                importer.humanDescription = description;
                importer.importAnimation = true;
                var clips = (importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations);
                foreach (var clip in clips)
                {
                    clip.loopTime = false;
                    // Root stays where the network puts the player; the fall lives in the pose.
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionY = true;
                    clip.keepOriginalPositionXZ = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                changed++;
                var built = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                if (built != null && built.isValid && built.isHuman) valid++;
                else Debug.LogWarning("[PlayerLife] Avatar invalid for " + path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[PlayerLife] Death set imported as Humanoid: {changed}/{total}, valid avatars {valid}");
        }

        static AnimationClip ClipOf(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        /// <summary>TP_Core_Death_{Region}_{Side}_{SM|LG}: Body, Belly and Lumbar are body hits; Montage, InAir and Land are skipped.</summary>
        [MenuItem("Tools/FPS/Player Life/Build Animation Set")]
        public static void BuildAnimationSet()
        {
            var set = AssetDatabase.LoadAssetAtPath<PlayerLifeAnimationSet>(SetPath);
            if (set == null) { set = ScriptableObject.CreateInstance<PlayerLifeAnimationSet>(); AssetDatabase.CreateAsset(set, SetPath); }
            var deaths = new List<PlayerLifeAnimationSet.DeathClip>();
            var headshots = new List<AnimationClip>();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { DeathFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name.EndsWith("_Montage")) continue;
                var clip = ClipOf(path);
                if (clip == null) continue;
                if (name.StartsWith("TP_Core_Death_Headshot")) { headshots.Add(clip); continue; }
                string[] parts = name.Substring("TP_Core_Death_".Length).Split('_');
                if (parts.Length != 3) continue;
                PlayerHitRegion region;
                switch (parts[0])
                {
                    case "Head": region = PlayerHitRegion.Head; break;
                    case "Leg": region = PlayerHitRegion.Leg; break;
                    case "Body": case "Belly": case "Lumbar": region = PlayerHitRegion.Body; break;
                    default: continue;
                }
                if (!Enum.TryParse(parts[1], out PlayerHitSide side)) continue;
                deaths.Add(new PlayerLifeAnimationSet.DeathClip { region = region, side = side, heavy = parts[2] == "LG", clip = clip });
            }
            set.deaths = deaths.OrderBy(d => d.region).ThenBy(d => d.side).ThenBy(d => d.heavy).ThenBy(d => d.clip.name).ToArray();
            set.headshots = headshots.ToArray();
            set.downedFall = ClipOf($"{DeathFolder}/{DownedFall}.fbx");
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PlayerLife] Animation set: {set.deaths.Length} deaths, {set.headshots.Length} headshots, downed fall {(set.downedFall != null ? set.downedFall.name : "MISSING")}");
        }

        [MenuItem("Tools/FPS/Player Life/Add Presenter To Player Prefabs")]
        public static void AddPresenters()
        {
            var set = AssetDatabase.LoadAssetAtPath<PlayerLifeAnimationSet>(SetPath);
            if (set == null) { Debug.LogError("[PlayerLife] Build the animation set first."); return; }
            var log = new System.Text.StringBuilder("[PlayerLife] Presenter:");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PlayersFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<PlayerHealth>() == null) continue;
                    var manager = root.GetComponent<WeaponManager>();
                    var movement = root.GetComponent<PlayerMovement>();
                    Animator animator = manager != null ? manager.CharacterAnimation : null;
                    if (animator == null && movement != null) animator = movement.CharacterAnimation;
                    // The avatar must bind to this prefab's body hierarchy (Sage and Gekko's own Human.fbx avatars do not)
                    // and be in metres: the shared TP_Core avatar is in centimetres and blows the body up 100x.
                    // Binding is probed on a throwaway instance so Rebind never writes bone values into the prefab.
                    Avatar avatar = new[] { FindHumanoidAvatar(root, animator) }.Concat(PlayerModelAvatars())
                        .FirstOrDefault(candidate => BindsOnInstance(path, candidate));
                    var presenter = root.GetComponent<PlayerLifePresenter>();
                    if (presenter == null) presenter = root.AddComponent<PlayerLifePresenter>();
                    var serialized = new SerializedObject(presenter);
                    serialized.FindProperty("animationSet").objectReferenceValue = set;
                    serialized.FindProperty("humanoidAvatar").objectReferenceValue = avatar;
                    serialized.FindProperty("sidearmHold").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<PlayerSidearmHoldPose>("Assets/FPS/Features/Characters/Animation/Content/3P/PlayerSidearmHoldPose.asset");
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.Append(Environment.NewLine).Append(path).Append(" animator=").Append(animator != null ? animator.name : "NONE")
                        .Append(" avatar=").Append(avatar != null ? avatar.name : "NONE");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        static IEnumerable<Avatar> PlayerModelAvatars() => AssetDatabase.FindAssets("t:Model", new[] { PlayersFolder })
            .SelectMany(g => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(g)).OfType<Avatar>())
            .Where(a => a.isHuman);

        static bool BindsOnInstance(string prefabPath, Avatar avatar)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            try
            {
                var manager = instance.GetComponent<WeaponManager>();
                var movement = instance.GetComponent<PlayerMovement>();
                Animator animator = manager != null ? manager.CharacterAnimation : null;
                if (animator == null && movement != null) animator = movement.CharacterAnimation;
                return Binds(animator, avatar);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        static bool Binds(Animator animator, Avatar avatar)
        {
            if (animator == null || avatar == null || !avatar.isHuman) return false;
            var previous = animator.avatar;
            animator.avatar = avatar;
            animator.Rebind();
            bool ok = animator.GetBoneTransform(HumanBodyBones.Head) != null && animator.GetBoneTransform(HumanBodyBones.LeftFoot) != null
                && animator.GetBoneTransform(HumanBodyBones.RightHand) != null;
            animator.avatar = previous;
            return ok;
        }

        static Avatar FindHumanoidAvatar(GameObject root, Animator animator)
        {
            if (animator != null && animator.avatar != null && animator.avatar.isHuman) return animator.avatar;
            var visibility = root.GetComponent<PlayerVisibilityController>();
            var presentations = visibility != null ? visibility.ThirdPersonWeaponPresentations : null;
            foreach (var presentation in presentations ?? Array.Empty<ThirdPersonWeaponPresentation>())
                if (presentation?.CharacterAvatar != null && presentation.CharacterAvatar.isHuman) return presentation.CharacterAvatar;
            if (animator == null) return null;
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(animator.gameObject);
            string model = source != null ? AssetDatabase.GetAssetPath(source) : null;
            return string.IsNullOrEmpty(model) ? null : AssetDatabase.LoadAllAssetsAtPath(model).OfType<Avatar>().FirstOrDefault(a => a.isHuman);
        }

        [MenuItem("Tools/FPS/Player Life/Report Avatar Binding")]
        public static void ReportAvatarBinding()
        {
            var log = new System.Text.StringBuilder("[PlayerLife] Avatar binding:");
            var candidates = AssetDatabase.FindAssets("t:Avatar", new[] { "Assets/FPS/Features/Characters" })
                .SelectMany(g => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(g)).OfType<Avatar>())
                .Where(a => a.isHuman).Distinct().ToArray();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PlayersFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                try
                {
                    var manager = instance.GetComponent<WeaponManager>();
                    var movement = instance.GetComponent<PlayerMovement>();
                    Animator animator = manager != null ? manager.CharacterAnimation : null;
                    if (animator == null && movement != null) animator = movement.CharacterAnimation;
                    if (animator == null) continue;
                    var original = animator.avatar;
                    string mesh = string.Join(",", animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.name).Take(3));
                    log.Append(Environment.NewLine).Append(path).Append(" animator=").Append(animator.name)
                        .Append(" avatar=").Append(original != null ? original.name + "@" + AssetDatabase.GetAssetPath(original) : "null").Append(" meshes=").Append(mesh);
                    foreach (var avatar in candidates)
                    {
                        animator.avatar = avatar;
                        animator.Rebind();
                        bool binds = animator.GetBoneTransform(HumanBodyBones.Head) != null && animator.GetBoneTransform(HumanBodyBones.LeftFoot) != null;
                        if (binds) log.Append(Environment.NewLine).Append("    binds: ").Append(avatar.name).Append(" @ ").Append(AssetDatabase.GetAssetPath(avatar));
                    }
                    var presenter = instance.GetComponent<PlayerLifePresenter>();
                    var field = typeof(PlayerLifePresenter).GetField("humanoidAvatar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    var assigned = presenter != null ? field.GetValue(presenter) as Avatar : null;
                    log.Append(Environment.NewLine).Append("    presenter avatar: ").Append(assigned != null ? assigned.name + " @ " + AssetDatabase.GetAssetPath(assigned) : "null");
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            Debug.Log(log.ToString());
        }
    }
}
