using System.Linq;
using AssetFlow.Editor.Workflow;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace AssetFlow.Editor.Importing
{
    public static class AssetFlowPresetUtility
    {
        public static bool CaptureFromAsset(AssetFlowConfig config, string assetPath)
        {
            if (config == null || string.IsNullOrEmpty(assetPath))
                return false;

            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null || importer.GetType().FullName != config.TypeKey)
                return false;

            var processor = config.PreImportProcessors.OfType<IAssetFlowPresetProcessor>().FirstOrDefault();
            if (processor == null)
                return false;

            var preset = new Preset(importer)
            {
                name = importer.GetType().Name + " Preset"
            };

            var configPath = AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(configPath))
                return false;

            AssetDatabase.AddObjectToAsset(preset, config);

            if (processor is ApplyTextureImporterPresetProcessor textureProcessor)
                textureProcessor.SetPreset(preset);
            else if (processor is ApplyModelImporterPresetProcessor modelProcessor)
                modelProcessor.SetPreset(preset);
            else if (processor is ApplyAudioImporterPresetProcessor audioProcessor)
                audioProcessor.SetPreset(preset);
            else
                return false;

            EditorUtility.SetDirty((UnityEngine.Object)processor);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);
            return true;
        }

        public static bool ClearPreset(AssetFlowConfig config)
        {
            var processor = config == null
                ? null
                : config.PreImportProcessors.OfType<IAssetFlowPresetProcessor>().FirstOrDefault();
            if (processor == null || processor.Preset == null)
                return false;

            var preset = processor.Preset;
            if (processor is ApplyTextureImporterPresetProcessor textureProcessor)
                textureProcessor.SetPreset(null);
            else if (processor is ApplyModelImporterPresetProcessor modelProcessor)
                modelProcessor.SetPreset(null);
            else if (processor is ApplyAudioImporterPresetProcessor audioProcessor)
                audioProcessor.SetPreset(null);
            else
                return false;

            if (AssetDatabase.Contains(preset))
                Object.DestroyImmediate(preset, allowDestroyingAssets: true);
            else
                Object.DestroyImmediate(preset);

            EditorUtility.SetDirty((Object)processor);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return true;
        }

        public static bool PingPreset(AssetFlowConfig config)
        {
            var preset = config?.PreImportProcessors.OfType<IAssetFlowPresetProcessor>().FirstOrDefault()?.Preset;
            if (preset == null)
                return false;

            Selection.activeObject = preset;
            EditorGUIUtility.PingObject(preset);
            return true;
        }
    }
}
