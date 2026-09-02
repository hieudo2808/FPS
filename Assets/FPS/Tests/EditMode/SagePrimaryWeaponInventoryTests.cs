using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPS.Tests
{
    public sealed class SagePrimaryWeaponInventoryTests
    {
        private static readonly string[] PlayerPaths =
        {
            "Assets/FPS/Features/Characters/Content/Players/Sage/SagePlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Gekko/GekkoPlayer.prefab",
            "Assets/FPS/Features/Characters/Content/Players/Brimstone/BrimstonePlayer.prefab"
        };

        [TestCaseSource(nameof(PlayerPaths))]
        public void PlayerOwnsOnlyVandalAndClassicButHasFourPrimaryCandidates(string playerPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
            WeaponManager manager = prefab.GetComponent<WeaponManager>();

            Assert.AreEqual(2, manager.WeaponCount);
            Assert.AreEqual("Vandal", manager.GetWeapon(0).Data.weaponName);
            Assert.AreEqual("Classic", manager.GetWeapon(1).Data.weaponName);
            Assert.AreEqual(4, manager.PrimaryCandidateCount);

            SerializedProperty entries = new SerializedObject(manager)
                .FindProperty("primaryWeaponCandidates");
            var ids = new HashSet<int>();
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                int id = entry.FindPropertyRelative("id").enumValueIndex;
                GameObject candidate = entry.FindPropertyRelative("weaponObject").objectReferenceValue as GameObject;
                Assert.True(ids.Add(id), $"Duplicate primary candidate id {id}.");
                Assert.NotNull(candidate);
                Assert.NotNull(candidate.GetComponent<Weapon>()?.Data);
                Assert.NotNull(candidate.GetComponent<Weapon>().WeaponAnimator);
                Assert.NotNull(candidate.GetComponent<Weapon>().BulletSpawnPoint);
                Assert.False(candidate.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true).Any(),
                    "Primary candidates are presentation children, not nested network objects.");
                Assert.True(candidate.GetComponentsInChildren<Transform>(true)
                    .All(item => item.gameObject.layer == 8));
                if ((PrimaryWeaponId)id != PrimaryWeaponId.Vandal)
                    Assert.False(candidate.activeSelf, $"Unowned {candidate.name} must be inactive by default.");
            }
        }

        [TestCaseSource(nameof(PlayerPaths))]
        public void ReplacingPrimaryChangesSlotZeroWithoutChangingClassicOrSlotCount(string playerPath)
        {
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(playerPath));
            try
            {
                WeaponManager manager = instance.GetComponent<WeaponManager>();
                GameObject classic = manager.GetWeapon(1).gameObject;

                Assert.True(manager.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Odin));
                Assert.AreEqual(2, manager.WeaponCount);
                Assert.AreEqual("Odin", manager.GetWeapon(0).Data.weaponName);
                Assert.AreSame(classic, manager.GetWeapon(1).gameObject);
                Assert.True(manager.GetWeapon(0).gameObject.activeSelf);
                Assert.False(classic.activeSelf);

                manager.SetEquippedWeaponServer(1);
                Assert.True(classic.activeSelf);
                Assert.False(manager.GetWeapon(0).gameObject.activeSelf);

                Assert.True(manager.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Bucky));
                Assert.AreEqual("Bucky", manager.GetWeapon(0).Data.weaponName);
                Assert.True(classic.activeSelf, "Replacing an unequipped primary must keep Classic visible.");
                Assert.False(manager.GetWeapon(0).gameObject.activeSelf);

                manager.SetEquippedWeaponServer(0);
                Assert.True(manager.GetWeapon(0).gameObject.activeSelf);
                Assert.False(classic.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [TestCaseSource(nameof(PlayerPaths))]
        public void RuntimeSnapshotKeepsSelectedPrimaryForReconnect(string playerPath)
        {
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(playerPath));
            try
            {
                WeaponManager manager = instance.GetComponent<WeaponManager>();
                Assert.True(manager.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Operator));

                PlayerRuntimeSnapshot snapshot = instance.GetComponent<PlayerHealth>().CaptureRuntimeSnapshot();
                Assert.AreEqual(PrimaryWeaponId.Operator, snapshot.primaryWeaponId);
                Assert.AreEqual(NetworkProtocol.SnapshotSchemaVersion, snapshot.schemaVersion);
                Assert.AreEqual(3, snapshot.inventorySchemaVersion);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
