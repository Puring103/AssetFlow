using UnityEditor;

namespace AssetFlow.Editor.Workflow
{
    public abstract class AssetFlowImporterConfig<TImporter> : AssetFlowConfig
        where TImporter : AssetImporter
    {
        public sealed override string TypeKey => typeof(TImporter).FullName;
    }
}
