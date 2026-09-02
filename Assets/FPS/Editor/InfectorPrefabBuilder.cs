using UnityEditor;
using UnityEngine;

namespace FPS.Editor
{
    public static class InfectorPrefabBuilder
    {
        [MenuItem("FPS/Validate Infector Authored Setup")]
        public static void ValidateInfectorAuthoredSetup()
        {
            InfectorPrefabUtility.ValidateAuthoredSetup();
        }
    }
}
