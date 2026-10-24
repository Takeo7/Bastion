using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.EditorTools
{
    /// <summary>
    /// Every style sheet of the UI imported again, saying which came with errors or warnings (USS refuses some CSS, and a
    /// sheet that fails is simply not applied: 23/10, Room.uss). The importer's own lines go to the log with them.
    ///
    ///   unity/tools/unity.sh -executeMethod Bastion.UI.EditorTools.UiSheets.Check
    /// </summary>
    public static class UiSheets
    {
        public static void Check()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:StyleSheet", new[] { "Assets/Bastion/UI" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                var state = sheet == null ? "NOT LOADED" : sheet.importedWithErrors ? "ERRORS" : sheet.importedWithWarnings ? "warnings" : "ok";
                Debug.Log($"UI sheets: {path} {state}");
            }
        }
    }
}
