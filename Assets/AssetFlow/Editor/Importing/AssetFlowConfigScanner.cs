using System.Collections.Generic;
using AssetFlow.Editor.Core;
using AssetFlow.Editor.Workflow;
using UnityEditor;

namespace AssetFlow.Editor.Importing
{
    public static class AssetFlowConfigScanner
    {
        public static List<AssetFlowConfigSnapshot> FindConfigSnapshots()
        {
            var snapshots = new List<AssetFlowConfigSnapshot>();
            foreach (var guid in AssetDatabase.FindAssets("t:AssetFlowConfig"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<AssetFlowConfig>(path);
                if (config == null)
                    continue;

                snapshots.Add(new AssetFlowConfigSnapshot(
                    guid,
                    path,
                    AssetFlowPath.GetParentFolder(path),
                    config.TypeKey,
                    config.IncludeSubfolders,
                    config.ComputeRuleHash()));
            }

            return snapshots;
        }

        public static AssetFlowConfig LoadConfig(AssetFlowConfigSnapshot snapshot)
        {
            return AssetDatabase.LoadAssetAtPath<AssetFlowConfig>(snapshot.ConfigPath);
        }
    }
}
