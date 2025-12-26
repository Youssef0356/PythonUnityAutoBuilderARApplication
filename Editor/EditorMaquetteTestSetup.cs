using UnityEngine;
using UnityEditor;
using System.IO;

namespace ARManagement.Editor
{
    /// <summary>
    /// Helper to quickly copy a maquette's data to StreamingAssets for editor testing.
    /// This mimics what AutomatedBuilder does but for rapid iteration in the editor.
    /// </summary>
    public class EditorMaquetteTestSetup
    {
        [MenuItem("MaintenanceAR/Test/Copy Maquette to StreamingAssets")]
        public static void CopyMaquetteForTesting()
        {
            string path = EditorUtility.OpenFolderPanel("Select Maquette Data Folder", "", "");
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[EditorMaquetteTestSetup] No folder selected.");
                return;
            }

            string maquetteName = new DirectoryInfo(path).Name;
            MaquetteBuilderUtils.PrepareMaquetteAssets(path, maquetteName);
            Debug.Log("[EditorMaquetteTestSetup] ✓ Maquette data copied to StreamingAssets for editor testing!");
        }
    }
}
