using UnityEditor;

namespace AssetFlow.Editor.Workflow
{
    public sealed class AssetFlowAudioConfig : AssetFlowImporterConfig<AudioImporter>
    {
        private void Reset()
        {
            ResetProcessorLists();
            AddPreImportProcessor(CreateInstance<ApplyAudioImporterTemplateProcessor>());
        }

        private void OnValidate()
        {
            EnsureSingleTemplateProcessor();
        }
    }
}
