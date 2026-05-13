using System;
using System.Collections.Generic;
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

            ReconcileManagedAssets(index, snapshots);
            indexStore.Save(index);
        }

        private static void ReconcileManagedAssets(AssetFlowIndex index, IReadOnlyList<AssetFlowConfigSnapshot> snapshots)
        {
            var resolver = new AssetFlowResolver(snapshots);
            var seenAssetGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var guid in AssetDatabase.FindAssets(string.Empty))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || IsIgnoredAssetPath(path))
                    continue;

                var importer = AssetImporter.GetAtPath(path);
                if (importer == null)
                    continue;

                var result = resolver.Resolve(path, importer.GetType().FullName);
                if (result.Status != AssetFlowResolveStatus.Managed)
                    continue;

                seenAssetGuids.Add(guid);
                var existing = index.Assets.FirstOrDefault(asset => string.Equals(asset.assetGuid, guid, StringComparison.OrdinalIgnoreCase));
                var lastProcessedRuleHash = existing != null
                                           && string.Equals(existing.managedByConfigGuid, result.Config.ConfigGuid, StringComparison.OrdinalIgnoreCase)
                    ? existing.lastProcessedRuleHash
                    : string.Empty;
                index.UpsertAsset(new AssetFlowAssetRecord
                {
                    assetGuid = guid,
                    assetPath = path,
                    importerTypeKey = importer.GetType().FullName,
                    managedByConfigGuid = result.Config.ConfigGuid,
                    managedByConfigPath = result.Config.ConfigPath,
                    lastProcessedRuleHash = lastProcessedRuleHash,
                    lastProcessedTicks = existing?.lastProcessedTicks ?? 0,
                });
            }

            foreach (var asset in index.Assets.ToList())
            {
                if (!seenAssetGuids.Contains(asset.assetGuid))
                    index.RemoveAsset(asset.assetGuid);
            }
        }

        private static bool IsIgnoredAssetPath(string path)
        {
            return path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                   || path.IndexOf("/AssetFlow.", StringComparison.OrdinalIgnoreCase) >= 0
                   || AssetFlowPresetUtility.IsTemplateSourceAsset(path);
        }
    }
}
