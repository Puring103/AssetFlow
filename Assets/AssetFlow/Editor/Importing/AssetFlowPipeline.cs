using System;
using System.Collections.Generic;
using System.Linq;
using AssetFlow.Editor.Workflow;
using UnityEditor;

namespace AssetFlow.Editor.Importing
{
    public sealed class AssetFlowPipelineReport
    {
        private readonly List<AssetFlowIssue> issues = new List<AssetFlowIssue>();
        private readonly List<string> errors = new List<string>();

        public IReadOnlyList<AssetFlowIssue> Issues => issues;

        public IReadOnlyList<string> Errors => errors;

        public bool HasErrors => errors.Count > 0
                                 || issues.Any(issue => issue.Severity == AssetFlowIssueSeverity.Error);

        public void AddIssues(IEnumerable<AssetFlowIssue> newIssues)
        {
            if (newIssues != null)
                issues.AddRange(newIssues);
        }

        public void AddError(string message)
        {
            errors.Add(message ?? string.Empty);
            issues.Add(new AssetFlowIssue(AssetFlowIssueSeverity.Error, message));
        }
    }

    public static class AssetFlowPipeline
    {
        public static AssetFlowPipelineReport RunPreImport(string assetPath, AssetFlowConfig config, AssetImporter importer)
        {
            return RunPreImport(assetPath, string.Empty, config, importer, null, null);
        }

        public static AssetFlowPipelineReport RunPreImport(
            string assetPath,
            string assetGuid,
            AssetFlowConfig config,
            AssetImporter importer,
            AssetFlowLoopGuard loopGuard,
            string chainId)
        {
            var report = new AssetFlowPipelineReport();
            if (config == null || importer == null)
                return report;

            foreach (var processor in config.PreImportProcessors.Where(processor => processor != null))
            {
                if (!processor.ImporterType.IsInstanceOfType(importer))
                    continue;

                var context = new AssetFlowPreImportContext(assetPath, config, importer);
                if (!ShouldRun(loopGuard, assetGuid, config, AssetFlowStage.PreImport, processor, chainId, report))
                    continue;

                RunHandler(report, processor.GetType().Name, () => processor.Process(importer, context));
                report.AddIssues(context.Issues);
            }

            return report;
        }

        public static AssetFlowPipelineReport RunPostImportAndValidation(
            string assetPath,
            AssetFlowConfig config,
            IReadOnlyList<UnityEngine.Object> importedObjects)
        {
            return RunPostImportAndValidation(assetPath, string.Empty, config, importedObjects, null, null);
        }

        public static AssetFlowPipelineReport RunPostImportAndValidation(
            string assetPath,
            string assetGuid,
            AssetFlowConfig config,
            IReadOnlyList<UnityEngine.Object> importedObjects,
            AssetFlowLoopGuard loopGuard,
            string chainId)
        {
            var report = new AssetFlowPipelineReport();
            if (config == null || importedObjects == null)
                return report;

            RunPostImport(assetPath, assetGuid, config, importedObjects, report, loopGuard, chainId);
            RunValidation(assetPath, assetGuid, config, importedObjects, report, loopGuard, chainId);
            return report;
        }

        private static void RunPostImport(
            string assetPath,
            string assetGuid,
            AssetFlowConfig config,
            IReadOnlyList<UnityEngine.Object> importedObjects,
            AssetFlowPipelineReport report,
            AssetFlowLoopGuard loopGuard,
            string chainId)
        {
            foreach (var processor in config.PostImportProcessors.Where(processor => processor != null))
            {
                foreach (var asset in importedObjects.Where(asset => asset != null && processor.AssetType.IsInstanceOfType(asset)))
                {
                    var context = new AssetFlowPostImportContext(assetPath, config);
                    if (!ShouldRun(loopGuard, assetGuid, config, AssetFlowStage.PostImport, processor, chainId, report))
                        continue;

                    RunHandler(report, processor.GetType().Name, () => processor.Process(asset, context));
                    report.AddIssues(context.Issues);
                }
            }
        }

        private static void RunValidation(
            string assetPath,
            string assetGuid,
            AssetFlowConfig config,
            IReadOnlyList<UnityEngine.Object> importedObjects,
            AssetFlowPipelineReport report,
            AssetFlowLoopGuard loopGuard,
            string chainId)
        {
            foreach (var validator in config.Validators.Where(validator => validator != null))
            {
                foreach (var asset in importedObjects.Where(asset => asset != null && validator.AssetType.IsInstanceOfType(asset)))
                {
                    var context = new AssetFlowValidationContext(assetPath, config);
                    if (!ShouldRun(loopGuard, assetGuid, config, AssetFlowStage.Validation, validator, chainId, report))
                        continue;

                    RunHandler(report, validator.GetType().Name, () => report.AddIssues(validator.Validate(asset, context)));
                    report.AddIssues(context.Issues);
                }
            }
        }

        private static bool ShouldRun(
            AssetFlowLoopGuard loopGuard,
            string assetGuid,
            AssetFlowConfig config,
            AssetFlowStage stage,
            AssetFlowHandler handler,
            string chainId,
            AssetFlowPipelineReport report)
        {
            if (loopGuard == null)
                return true;

            var key = new AssetFlowLoopKey(
                assetGuid,
                config == null ? string.Empty : config.ToSnapshot().ConfigGuid,
                stage,
                handler.GetType().FullName);
            if (loopGuard.ShouldRun(key, string.IsNullOrEmpty(chainId) ? assetGuid : chainId))
                return true;

            report.AddError($"AssetFlow import loop detected. Handler paused: {handler.GetType().FullName}");
            return false;
        }

        private static void RunHandler(AssetFlowPipelineReport report, string label, Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                report.AddError($"{label}: {exception.Message}");
            }
        }
    }
}
