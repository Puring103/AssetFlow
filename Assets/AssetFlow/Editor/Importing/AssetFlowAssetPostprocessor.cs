using System;
using System.Collections.Generic;
using System.Linq;
using AssetFlow.Editor.Core;
using AssetFlow.Editor.Workflow;
using UnityEditor;

namespace AssetFlow.Editor.Importing
{
    public sealed class AssetFlowAssetPostprocessor : AssetPostprocessor
    {
        private static readonly AssetFlowLoopGuard LoopGuard = new AssetFlowLoopGuard(useSessionState: true);
        private static readonly Dictionary<string, AssetFlowPipelineReport> PreImportReportsByPath =
            new Dictionary<string, AssetFlowPipelineReport>(StringComparer.OrdinalIgnoreCase);

        public static AssetFlowLoopGuard SharedLoopGuard => LoopGuard;

        private void OnPreprocessAsset()
        {
            var importer = assetImporter;
            if (importer == null || ShouldSkipManagedAssetPath(assetPath))
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
            var report = AssetFlowPipeline.RunPreImport(
                assetPath,
                AssetDatabase.AssetPathToGUID(assetPath),
                config,
                importer,
                LoopGuard,
                assetPath);
            RememberPreImportReport(assetPath, report);
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (TryCollectConfigChanges(
                    importedAssets,
                    deletedAssets,
                    movedAssets,
                    movedFromAssetPaths,
                    out var configChanges))
            {
                AssetFlowConfigurationChangeProcessor.ProcessConfigurationChanges(configChanges);
            }

            var indexStore = new AssetFlowIndexStore();
            var index = indexStore.Load();

            RemoveDeletedAndMovedFromAssets(index, deletedAssets, movedFromAssetPaths);

            foreach (var assetPath in (importedAssets ?? Array.Empty<string>()).Concat(movedAssets ?? Array.Empty<string>()))
            {
                if (ShouldSkipManagedAssetPath(assetPath))
                    continue;

                var importer = AssetImporter.GetAtPath(assetPath);
                if (importer == null)
                {
                    index.RemoveAssetAtPath(assetPath);
                    continue;
                }

                var result = Resolve(assetPath, importer.GetType().FullName);
                if (result.Status == AssetFlowResolveStatus.Conflict)
                    AssetFlowConflictReporter.Report(assetPath, result);

                if (result.Status != AssetFlowResolveStatus.Managed)
                {
                    var unmanagedAssetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                    ForgetPreImportReport(assetPath);
                    index.RemoveAsset(unmanagedAssetGuid);
                    index.RemoveAssetAtPath(assetPath);
                    continue;
                }

                var config = AssetFlowConfigScanner.LoadConfig(result.Config);
                if (config == null)
                    continue;

                var importedObjects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                var report = MergeWithPreImportReport(
                    assetPath,
                    AssetFlowPipeline.RunPostImportAndValidation(
                    assetPath,
                    assetGuid,
                    config,
                    importedObjects,
                    LoopGuard,
                    string.Empty));
                UpdateIndex(index, assetPath, importer, result.Config, report);
            }

            indexStore.Save(index);
        }

        private static void RemoveDeletedAndMovedFromAssets(
            AssetFlowIndex index,
            string[] deletedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var path in (deletedAssets ?? Array.Empty<string>()).Concat(movedFromAssetPaths ?? Array.Empty<string>()))
            {
                if (ShouldSkipManagedAssetPath(path))
                    continue;

                ForgetPreImportReport(path);
                index.RemoveAssetAtPath(path);
            }
        }

        private static void RememberPreImportReport(string assetPath, AssetFlowPipelineReport report)
        {
            var normalizedPath = AssetFlowPath.Normalize(assetPath);
            if (string.IsNullOrEmpty(normalizedPath))
                return;

            if (report == null || report.Issues.Count == 0)
            {
                PreImportReportsByPath.Remove(normalizedPath);
                return;
            }

            PreImportReportsByPath[normalizedPath] = report;
        }

        private static void ForgetPreImportReport(string assetPath)
        {
            var normalizedPath = AssetFlowPath.Normalize(assetPath);
            if (!string.IsNullOrEmpty(normalizedPath))
                PreImportReportsByPath.Remove(normalizedPath);
        }

        private static AssetFlowPipelineReport MergeWithPreImportReport(
            string assetPath,
            AssetFlowPipelineReport postImportReport)
        {
            var normalizedPath = AssetFlowPath.Normalize(assetPath);
            if (string.IsNullOrEmpty(normalizedPath)
                || !PreImportReportsByPath.TryGetValue(normalizedPath, out var preImportReport))
            {
                return postImportReport;
            }

            PreImportReportsByPath.Remove(normalizedPath);
            var combined = new AssetFlowPipelineReport();
            combined.AddIssues(preImportReport.Issues);
            combined.AddIssues(postImportReport?.Issues);
            return combined;
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
            var existingRecord = index.Assets.FirstOrDefault(asset =>
                string.Equals(asset.assetGuid, assetGuid, StringComparison.OrdinalIgnoreCase));
            var processedRuleHash = report != null && report.HasErrors
                ? existingRecord?.lastProcessedRuleHash ?? string.Empty
                : config.RuleHash;
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
                lastProcessedRuleHash = processedRuleHash,
                lastProcessedTicks = DateTime.UtcNow.Ticks,
            });
            index.ReplaceValidationResults(
                assetGuid,
                config.ConfigGuid,
                (report?.Issues ?? Array.Empty<AssetFlowIssue>()).Select(issue => new AssetFlowValidationRecord
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

        private static bool ShouldSkipManagedAssetPath(string path)
        {
            return AssetFlowConfigurationChangeProcessor.IsConfigPath(path)
                   || AssetFlowTemplateImporterUtility.IsTemplateSourceAsset(path);
        }

        private static bool TryCollectConfigChanges(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            out List<AssetFlowConfigurationChangeProcessor.Change> changes)
        {
            changes = new List<AssetFlowConfigurationChangeProcessor.Change>();
            var index = new AssetFlowIndexStore().Load();

            AddImportedConfigChanges(changes, importedAssets, index);
            AddConfigChanges(changes, deletedAssets, AssetFlowConfigurationChangeProcessor.ChangeKind.Removed, index);
            AddConfigChanges(changes, movedAssets, AssetFlowConfigurationChangeProcessor.ChangeKind.Moved, index);
            AddConfigChanges(changes, movedFromAssetPaths, AssetFlowConfigurationChangeProcessor.ChangeKind.Moved, index);

            return changes.Count > 0;
        }

        private static void AddImportedConfigChanges(
            List<AssetFlowConfigurationChangeProcessor.Change> changes,
            IEnumerable<string> paths,
            AssetFlowIndex index)
        {
            if (paths == null)
                return;

            foreach (var path in paths)
            {
                if (!AssetFlowConfigurationChangeProcessor.IsConfigPath(path))
                    continue;

                var kind = AssetFlowConfigurationChangeProcessor.IsKnownConfigPath(path, index)
                    ? AssetFlowConfigurationChangeProcessor.ChangeKind.Edited
                    : AssetFlowConfigurationChangeProcessor.ChangeKind.Added;
                changes.Add(new AssetFlowConfigurationChangeProcessor.Change(path, kind));
            }
        }

        private static void AddConfigChanges(
            List<AssetFlowConfigurationChangeProcessor.Change> changes,
            IEnumerable<string> paths,
            AssetFlowConfigurationChangeProcessor.ChangeKind kind,
            AssetFlowIndex index)
        {
            if (paths == null)
                return;

            foreach (var path in paths)
            {
                if (!AssetFlowConfigurationChangeProcessor.IsConfigPath(path)
                    && !AssetFlowConfigurationChangeProcessor.IsKnownConfigPath(path, index))
                {
                    continue;
                }

                changes.Add(new AssetFlowConfigurationChangeProcessor.Change(path, kind));
            }
        }
    }
}
