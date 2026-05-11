using System;
using System.Linq;
using AssetFlow.Editor.Core;
using AssetFlow.Editor.Workflow;
using UnityEditor;

namespace AssetFlow.Editor.Importing
{
    public static class AssetFlowConfigurationChangeProcessor
    {
        public static bool IsConfigPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                return false;

            return AssetDatabase.LoadAssetAtPath<AssetFlowConfig>(path) != null
                   || path.IndexOf("AssetFlow.", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void ProcessConfigurationChanges()
        {
            AssetFlowDependency.RegisterAll();

            var snapshots = AssetFlowConfigScanner.FindConfigSnapshots();
            var indexStore = new AssetFlowIndexStore();
            var index = indexStore.Load();
            index.RemoveMissingConfigs(snapshots.Select(snapshot => snapshot.ConfigGuid));

            foreach (var snapshot in snapshots)
            {
                index.UpsertConfig(new AssetFlowConfigRecord
                {
                    configGuid = snapshot.ConfigGuid,
                    configPath = snapshot.ConfigPath,
                    folderPath = snapshot.FolderPath,
                    typeKey = snapshot.TypeKey,
                    includeSubfolders = snapshot.IncludeSubfolders,
                    ruleHash = snapshot.RuleHash,
                });
            }

            indexStore.Save(index);
        }
    }
}
