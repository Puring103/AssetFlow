using UnityEditor;

namespace AssetFlow.Editor.Workflow
{
    public sealed class AssetFlowTextureConfig : AssetFlowImporterConfig<TextureImporter>
    {
        private void Reset()
        {
            ResetToDefaults();
        }

        private void OnValidate()
        {
            EnsureSinglePresetProcessor();
        }

        internal void ResetToDefaultsForTests()
        {
            ResetToDefaults();
        }

        internal void AddPreImportProcessorForTests(AssetFlowPreImportProcessor processor)
        {
            AddPreImportProcessor(processor);
        }

        private void ResetToDefaults()
        {
            ResetProcessorLists();
            AddPreImportProcessor(CreateInstance<ApplyTextureImporterPresetProcessor>());
            EnsureSinglePresetProcessor();
        }
    }
}
