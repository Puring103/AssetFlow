using System.Collections.Generic;
using UnityEditor;

namespace AssetFlow.Editor.Workflow
{
    public abstract class AssetFlowProcessingContext
    {
        private readonly List<AssetFlowIssue> issues = new List<AssetFlowIssue>();

        protected AssetFlowProcessingContext(string assetPath, AssetFlowConfig config)
        {
            AssetPath = assetPath ?? string.Empty;
            Config = config;
        }

        public string AssetPath { get; }

        public AssetFlowConfig Config { get; }

        public IReadOnlyList<AssetFlowIssue> Issues => issues;

        public void ReportInfo(string message)
        {
            issues.Add(new AssetFlowIssue(AssetFlowIssueSeverity.Info, message));
        }

        public void ReportWarning(string message)
        {
            issues.Add(new AssetFlowIssue(AssetFlowIssueSeverity.Warning, message));
        }

        public void ReportError(string message)
        {
            issues.Add(new AssetFlowIssue(AssetFlowIssueSeverity.Error, message));
        }
    }

    public sealed class AssetFlowPreImportContext : AssetFlowProcessingContext
    {
        public AssetFlowPreImportContext(string assetPath, AssetFlowConfig config, AssetImporter importer)
            : base(assetPath, config)
        {
            Importer = importer;
        }

        public AssetImporter Importer { get; }
    }

    public sealed class AssetFlowPostImportContext : AssetFlowProcessingContext
    {
        public AssetFlowPostImportContext(string assetPath, AssetFlowConfig config)
            : base(assetPath, config)
        {
        }
    }

    public sealed class AssetFlowValidationContext : AssetFlowProcessingContext
    {
        public AssetFlowValidationContext(string assetPath, AssetFlowConfig config)
            : base(assetPath, config)
        {
        }
    }
}
