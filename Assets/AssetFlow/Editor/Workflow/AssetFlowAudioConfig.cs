using UnityEditor;

namespace AssetFlow.Editor.Workflow
{
    public sealed class AssetFlowAudioConfig : AssetFlowImporterConfig<AudioImporter>
    {
        private void Reset()
        {
            ResetProcessorLists();
            AddPreImportProcessor(CreateInstance<ApplyAudioImporterPresetProcessor>());
        }

        private void OnValidate()
        {
            EnsureSinglePresetProcessor();
        }
    }
}
