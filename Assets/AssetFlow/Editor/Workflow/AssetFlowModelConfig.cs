using UnityEditor;

namespace AssetFlow.Editor.Workflow
{
    public sealed class AssetFlowModelConfig : AssetFlowImporterConfig<ModelImporter>
    {
        private void Reset()
        {
            ResetProcessorLists();
            AddPreImportProcessor(CreateInstance<ApplyModelImporterTemplateProcessor>());
        }

        private void OnValidate()
        {
            EnsureSingleTemplateProcessor();
        }
    }
}
