using AssetFlow.Editor.Workflow;

namespace AssetFlow.Editor.Importing
{
    internal static class AssetFlowTemplateImporterUtility
    {
        internal static bool EnsureTemplateImporter(AssetFlowConfig config)
        {
            return AssetFlowTemplateImporterStore.EnsureTemplateImporter(config).Changed;
        }

        internal static bool NeedsTemplateImporterMaintenance(AssetFlowConfig config)
        {
            return AssetFlowTemplateImporterStore.NeedsTemplateImporterMaintenance(config);
        }

        internal static bool CaptureFromAsset(AssetFlowConfig config, string assetPath)
        {
            return AssetFlowTemplateImporterStore.CaptureFromAsset(config, assetPath).Success;
        }

        internal static bool RemoveLegacyPresetSubAssets(AssetFlowConfig config)
        {
            return AssetFlowTemplateImporterStore.RemoveLegacyPresetSubAssets(config);
        }

        internal static bool IsTemplateSourceAsset(string path)
        {
            return AssetFlowTemplateImporterStore.IsTemplateSourceAsset(path);
        }
    }
}
