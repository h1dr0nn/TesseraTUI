using UnityEditor;
using UnityEngine;
using System.IO;

namespace Tessera.Editor
{
    public class CsvPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string str in importedAssets)
            {
                if (str.EndsWith(".csv"))
                {
                    ValidateCsv(str);
                }
            }
        }

        private static void ValidateCsv(string assetPath)
        {
            Debug.Log($"[Tessera] CSV imported: {assetPath}");
            // TODO: Implement full validation when Core integration is complete
        }
    }
}
