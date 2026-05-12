using AssetFlow.Editor.Workflow;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.Importing
{
    public static class AssetFlowConfigFactory
    {
        public static string CreateTextureConfig(string folderPath)
        {
            return CreateImporterConfig<AssetFlowTextureConfig>(
                folderPath,
                "AssetFlow.Texture.asset");
        }

        public static string CreateModelConfig(string folderPath)
        {
            return CreateImporterConfig<AssetFlowModelConfig>(
                folderPath,
                "AssetFlow.Model.asset");
        }

        public static string CreateAudioConfig(string folderPath)
        {
            return CreateImporterConfig<AssetFlowAudioConfig>(
                folderPath,
                "AssetFlow.Audio.asset");
        }

        private static string CreateImporterConfig<TConfig>(
            string folderPath,
            string fileName)
            where TConfig : AssetFlowConfig
        {
            var normalizedFolder = (folderPath ?? "Assets").Replace('\\', '/').TrimEnd('/');
            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{normalizedFolder}/{fileName}");
            var config = ScriptableObject.CreateInstance<TConfig>();

            AssetDatabase.CreateAsset(config, assetPath);
            AddChildHandlers(config);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);
            return assetPath;
        }

        private static void AddChildHandlers(AssetFlowConfig config)
        {
            foreach (var processor in config.PreImportProcessors)
            {
                if (processor == null)
                    continue;

                processor.name = processor.GetType().Name;
                AssetDatabase.AddObjectToAsset(processor, config);
                if (processor is IAssetFlowPresetProcessor presetProcessor)
                    AssetFlowPresetUtility.EnsurePreset(config, presetProcessor);

                EditorUtility.SetDirty(processor);
            }
        }
    }
}
