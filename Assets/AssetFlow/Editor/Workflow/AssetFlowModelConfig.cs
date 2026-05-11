using UnityEditor;

namespace AssetFlow.Editor.Workflow
{
    public sealed class AssetFlowModelConfig : AssetFlowImporterConfig<ModelImporter>
    {
        private void Reset()
        {
            ResetProcessorLists();
            AddPreImportProcessor(CreateInstance<ApplyModelImporterPresetProcessor>());
        }

        private void OnValidate()
        {
            EnsureSinglePresetProcessor();
        }
    }
}
