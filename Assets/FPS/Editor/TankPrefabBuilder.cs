using UnityEditor;
using UnityEngine;

namespace FPS.Editor
{
    public static class TankPrefabBuilder
    {
        [MenuItem("FPS/Build Tank Prefab")]
        public static void BuildTankPrefab()
        {
            GameObject prefab = TankPrefabUtility.EnsureTankPrefab();
            if (prefab == null)
            {
                Debug.LogError("[TankPrefabBuilder] Tank prefab setup failed.");
                return;
            }

            Debug.Log($"[TankPrefabBuilder] Tank prefab configured at {TankPrefabUtility.PrefabPath}");
        }
    }
}
