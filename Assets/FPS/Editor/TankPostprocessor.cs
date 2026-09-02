using UnityEditor;

namespace FPS.Editor
{
    public class TankPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            for (int i = 0; i < importedAssets.Length; i++)
            {
                string path = importedAssets[i];
                if (path.EndsWith("/SI_Tank.cs")
                    || path.Contains("/Tanker/Animations/")
                    || path.Contains("/Tanker/source/Dante Beast FPSC Pack/Audio/"))
                {
                    EditorApplication.delayCall += () => TankPrefabUtility.EnsureTankPrefab();
                    return;
                }
            }
        }
    }
}
