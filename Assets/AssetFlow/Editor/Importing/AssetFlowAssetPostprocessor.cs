using System;
using System.Linq;
using AssetFlow.Editor.Core;
using AssetFlow.Editor.Workflow;
using UnityEditor;

namespace AssetFlow.Editor.Importing
{
    public sealed class AssetFlowAssetPostprocessor : AssetPostprocessor
    {
        private static readonly AssetFlowLoopGuard LoopGuard = new AssetFlowLoopGuard(useSessionState: true);

        public static AssetFlowLoopGuard SharedLoopGuard => LoopGuard;

        private void OnPreprocessAsset()
        {
            var importer = assetImporter;
            if (importer == null || IsAssetFlowAsset(assetPath))
                return;

            var result = Resolve(assetPath, importer.GetType().FullName);
            if (result.Status == AssetFlowResolveStatus.Conflict)
                AssetFlowConflictReporter.Report(assetPath, result);

            if (result.Status != AssetFlowResolveStatus.Managed)
                return;

            var config = AssetFlowConfigScanner.LoadConfig(result.Config);
            if (config == null)
                return;

            AssetImportContextDependsOnConfig(result.Config);
            AssetFlowPipeline.RunPreImport(
                assetPath,
                AssetDatabase.AssetPathToGUID(assetPath),
                config,
                importer,
                LoopGuard,
                assetPath);
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsConfigChange(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths))
                AssetFlowConfigurationChangeProcessor.ProcessConfigurationChanges();

            var indexStore = new AssetFlowIndexStore();
            var index = indexStore.Load();

            foreach (var assetPath in importedAssets.Concat(movedAssets ?? Array.Empty<string>()))
            {
                if (IsAssetFlowAsset(assetPath))
                    continue;

                var importer = AssetImporter.GetAtPath(assetPath);
                if (importer == null)
                    continue;

                var result = Resolve(assetPath, importer.GetType().FullName);
                if (result.Status == AssetFlowResolveStatus.Conflict)
                    AssetFlowConflictReporter.Report(assetPath, result);

                if (result.Status != AssetFlowResolveStatus.Managed)
                    continue;

                var config = AssetFlowConfigScanner.LoadConfig(result.Config);
                if (config == null)
                    continue;

                var importedObjects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                var report = AssetFlowPipeline.RunPostImportAndValidation(
                    assetPath,
                    assetGuid,
                    config,
                    importedObjects,
                    LoopGuard,
                    string.Empty);
                UpdateIndex(index, assetPath, importer, result.Config, report);
            }

            indexStore.Save(index);
        }

        private static AssetFlowResolveResult Resolve(string path, string typeKey)
        {
            var resolver = new AssetFlowResolver(AssetFlowConfigScanner.FindConfigSnapshots());
            return resolver.Resolve(path, typeKey);
        }

        private static void UpdateIndex(
            AssetFlowIndex index,
            string assetPath,
            AssetImporter importer,
            AssetFlowConfigSnapshot config,
            AssetFlowPipelineReport report)
        {
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            index.UpsertConfig(new AssetFlowConfigRecord
            {
                configGuid = config.ConfigGuid,
                configPath = config.ConfigPath,
                folderPath = config.FolderPath,
                typeKey = config.TypeKey,
                includeSubfolders = config.IncludeSubfolders,
                ruleHash = config.RuleHash,
            });
            index.UpsertAsset(new AssetFlowAssetRecord
            {
                assetGuid = assetGuid,
                assetPath = assetPath,
                importerTypeKey = importer.GetType().FullName,
                managedByConfigGuid = config.ConfigGuid,
                managedByConfigPath = config.ConfigPath,
                lastProcessedRuleHash = config.RuleHash,
                lastProcessedTicks = DateTime.UtcNow.Ticks,
            });
            index.ReplaceValidationResults(
                assetGuid,
                config.ConfigGuid,
                report.Issues.Select(issue => new AssetFlowValidationRecord
                {
                    assetGuid = assetGuid,
                    configGuid = config.ConfigGuid,
                    severity = issue.Severity.ToString(),
                    message = issue.Message,
                    ticks = DateTime.UtcNow.Ticks,
                }));
        }

        private void AssetImportContextDependsOnConfig(AssetFlowConfigSnapshot config)
        {
            if (context == null || string.IsNullOrEmpty(config.ConfigGuid))
                return;

            context.DependsOnCustomDependency(AssetFlowDependency.CustomDependencyName(config.ConfigGuid));
        }

        private static bool IsAssetFlowAsset(string path)
        {
            return path != null && path.IndexOf("/AssetFlow.", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsConfigChange(params string[][] pathGroups)
        {
            foreach (var paths in pathGroups)
            {
                if (paths == null)
                    continue;

                foreach (var path in paths)
                {
                    if (AssetFlowConfigurationChangeProcessor.IsConfigPath(path))
                        return true;
                }
            }

            return false;
        }
    }
}
