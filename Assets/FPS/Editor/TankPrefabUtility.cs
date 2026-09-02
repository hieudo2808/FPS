using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace FPS.Editor
{
    public static class TankPrefabUtility
    {
        public const string PrefabPath = "Assets/FPS/Features/Characters/Content/Enemies/Tanker/Prefabs/Tank.prefab";

        public static GameObject EnsureTankPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                ConfigureTankValues(existing);
                RegisterNetworkPrefab(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }

            string basePath = "Assets/FPS/Features/Characters/Content/Enemies/Tanker";
            string fbxPath = basePath + "/source/Dante Beast FPSC Pack/Model/Dante Beast.fbx";
            string matPath = basePath + "/Materials/TankMaterial.mat";
            string controllerPath = basePath + "/Animations/TankAnimatorController.controller";
            string audioBasePath = basePath + "/source/Dante Beast FPSC Pack/Audio";

            if (!AssetDatabase.IsValidFolder(basePath + "/Prefabs"))
            {
                AssetDatabase.CreateFolder(basePath, "Prefabs");
            }

            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null)
            {
                Debug.LogError($"[TankPrefabUtility] Cannot load FBX at {fbxPath}");
                return null;
            }

            Material tankMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            RuntimeAnimatorController animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

            AudioClip roarClip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioBasePath + "/danteroar.wav");
            AudioClip smashClip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioBasePath + "/dantesmash.wav");
            AudioClip punchClip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioBasePath + "/dantepunch.wav");

            GameObject instance = Object.Instantiate(fbx);
            instance.name = "Tank";

            SkinnedMeshRenderer smr = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null && tankMat != null)
            {
                smr.sharedMaterial = tankMat;
            }

            Animator anim = instance.GetComponent<Animator>();
            if (anim == null) anim = instance.AddComponent<Animator>();
            if (animatorController != null)
            {
                anim.runtimeAnimatorController = animatorController;
            }

            NetworkObject netObj = instance.GetComponent<NetworkObject>();
            if (netObj == null) netObj = instance.AddComponent<NetworkObject>();

            NetworkTransform netTransform = instance.GetComponent<NetworkTransform>();
            if (netTransform == null) netTransform = instance.AddComponent<NetworkTransform>();
            netTransform.Interpolate = true;
            netTransform.UseUnreliableDeltas = true;
            netTransform.UseHalfFloatPrecision = true;

            NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
            if (agent == null) agent = instance.AddComponent<NavMeshAgent>();
            agent.radius = 0.8f;
            agent.height = 2.7f;
            agent.speed = 3.5f;
            agent.angularSpeed = 120f;
            agent.stoppingDistance = 2.0f;
            agent.autoBraking = true;
            agent.updateRotation = false;

            CapsuleCollider col = instance.GetComponent<CapsuleCollider>();
            if (col == null) col = instance.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.35f, 0f);
            col.radius = 0.8f;
            col.height = 2.7f;

            EnemyHealth health = instance.GetComponent<EnemyHealth>();
            if (health == null) health = instance.AddComponent<EnemyHealth>();
            SerializedObject serializedHealth = new SerializedObject(health);
            serializedHealth.FindProperty("maxHealth").floatValue = 2500f;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();

            SI_Tank tank = instance.GetComponent<SI_Tank>();
            if (tank == null) tank = instance.AddComponent<SI_Tank>();
            SerializedObject serializedTank = new SerializedObject(tank);
            serializedTank.FindProperty("specialHPMultiplier").floatValue = 1f;
            serializedTank.FindProperty("heavySwingDamage").floatValue = 50f;
            serializedTank.FindProperty("heavySwingKnockbackForce").floatValue = 8f;
            serializedTank.FindProperty("heavySwingWindup").floatValue = 0.8f;
            serializedTank.FindProperty("heavySwingRange").floatValue = 3.5f;
            serializedTank.FindProperty("slamDamage").floatValue = 25f;
            serializedTank.FindProperty("slamRadius").floatValue = 4.5f;
            serializedTank.FindProperty("slamKnockbackForce").floatValue = 12f;
            serializedTank.FindProperty("slamCooldown").floatValue = 15f;
            serializedTank.FindProperty("slamWindup").floatValue = 1.2f;
            serializedTank.FindProperty("healthPerPlayer").floatValue = 2500f;
            serializedTank.FindProperty("heavySwingArcDegrees").floatValue = 120f;
            serializedTank.FindProperty("staggerDamageFraction").floatValue = 0.15f;
            serializedTank.FindProperty("staggerWindow").floatValue = 3f;
            serializedTank.FindProperty("staggerDuration").floatValue = 1.25f;
            serializedTank.FindProperty("staggerImmunityDuration").floatValue = 5f;

            if (roarClip != null) serializedTank.FindProperty("roarSound").objectReferenceValue = roarClip;
            if (smashClip != null) serializedTank.FindProperty("slamSound").objectReferenceValue = smashClip;
            if (punchClip != null) serializedTank.FindProperty("heavySwingSound").objectReferenceValue = punchClip;
            serializedTank.ApplyModifiedPropertiesWithoutUndo();

            HitboxSegment hitbox = instance.GetComponent<HitboxSegment>();
            if (hitbox == null) hitbox = instance.AddComponent<HitboxSegment>();
            SerializedObject serializedHitbox = new SerializedObject(hitbox);
            serializedHitbox.FindProperty("zone").enumValueIndex = (int)HitboxZone.Body;
            serializedHitbox.FindProperty("damageMultiplier").floatValue = 1f;
            serializedHitbox.ApplyModifiedPropertiesWithoutUndo();

            LagCompensatedTarget lagComp = instance.GetComponent<LagCompensatedTarget>();
            if (lagComp == null) instance.AddComponent<LagCompensatedTarget>();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);

            RegisterNetworkPrefab(savedPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return savedPrefab;
        }

        private static void ConfigureTankValues(GameObject prefab)
        {
            EnemyHealth health = prefab.GetComponent<EnemyHealth>();
            SI_Tank tank = prefab.GetComponent<SI_Tank>();
            if (health == null || tank == null)
            {
                Debug.LogError("[TankPrefabUtility] Existing Tank prefab is missing EnemyHealth or SI_Tank.");
                return;
            }

            SerializedObject serializedHealth = new SerializedObject(health);
            serializedHealth.FindProperty("maxHealth").floatValue = 2500f;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedTank = new SerializedObject(tank);
            serializedTank.FindProperty("specialHPMultiplier").floatValue = 1f;
            serializedTank.FindProperty("healthPerPlayer").floatValue = 2500f;
            serializedTank.FindProperty("heavySwingArcDegrees").floatValue = 120f;
            serializedTank.FindProperty("staggerDamageFraction").floatValue = 0.15f;
            serializedTank.FindProperty("staggerWindow").floatValue = 3f;
            serializedTank.FindProperty("staggerDuration").floatValue = 1.25f;
            serializedTank.FindProperty("staggerImmunityDuration").floatValue = 5f;
            serializedTank.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(health);
            EditorUtility.SetDirty(tank);
        }

        private static void RegisterNetworkPrefab(GameObject prefab)
        {
            NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (list == null || prefab == null || list.Contains(prefab))
                return;

            list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
        }
    }
}
