using System.Linq;
using AssetFlow.Editor.Workflow;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace AssetFlow.Editor.Importing
{
    public static class AssetFlowPresetUtility
    {
        private const string PresetSubAssetName = "importor";
        private const string LegacyPresetTypeName = "UnityEditor.Presets.Preset";

        public static bool EnsureTemplateImporter(AssetFlowConfig config)
        {
            var processor = GetTemplateProcessor(config);
            if (processor == null)
                return false;

            var configPath = AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(configPath))
                return false;

            if (processor.TemplatePreset != null && AssetDatabase.Contains(processor.TemplatePreset))
            {
                EnsurePresetNaming(processor.TemplatePreset);
                RemoveLegacyPresetSubAssets(configPath, processor.TemplatePreset);
                return false;
            }

            if (TryMigrateLegacyTemplateImporter(config, processor))
                return true;

            var existingPreset = FindExistingPreset(configPath);
            if (existingPreset == null)
                return false;

            processor.SetTemplatePreset(existingPreset);
            EditorUtility.SetDirty((Object)processor);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return true;
        }

        public static bool CaptureFromAsset(AssetFlowConfig config, string assetPath)
        {
            if (config == null || string.IsNullOrEmpty(assetPath))
                return false;

            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null || importer.GetType().FullName != config.TypeKey)
                return false;

            var processor = GetTemplateProcessor(config);
            if (processor == null)
                return false;

            var configPath = AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(configPath))
                return false;

            var preset = processor.TemplatePreset;
            if (preset == null)
            {
                preset = new Preset(importer)
                {
                    name = PresetSubAssetName
                };
                AssetDatabase.AddObjectToAsset(preset, config);
            }
            else
            {
                preset.UpdateProperties(importer);
                EnsurePresetNaming(preset);
            }

            RemoveLegacyPresetSubAssets(configPath, preset);
            processor.SetTemplatePreset(preset);
            processor.ClearLegacyTemplateImporter();
            EditorUtility.SetDirty(preset);
            EditorUtility.SetDirty((Object)processor);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);
            return true;
        }

        public static bool ClearPreset(AssetFlowConfig config)
        {
            var processor = GetTemplateProcessor(config);
            if (processor == null)
                return false;

            var changed = false;
            if (processor.TemplatePreset != null)
            {
                RemovePresetSubAsset(processor.TemplatePreset);
                processor.SetTemplatePreset(null);
                changed = true;
            }

            if (processor.LegacyTemplateImporter != null)
            {
                processor.ClearLegacyTemplateImporter();
                changed = true;
            }

            if (!changed)
                return false;

            EditorUtility.SetDirty((Object)processor);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return true;
        }

        public static bool RemoveLegacyPresetSubAssets(AssetFlowConfig config)
        {
            var configPath = config == null ? string.Empty : AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(configPath))
                return false;

            return RemoveLegacyPresetSubAssets(configPath, null);
        }

        public static bool PingPreset(AssetFlowConfig config)
        {
            var preset = GetTemplateProcessor(config)?.TemplatePreset;
            if (preset == null)
                return false;

            Selection.activeObject = preset;
            EditorGUIUtility.PingObject(preset);
            return true;
        }

        public static bool HasTemplatePreset(AssetFlowConfig config)
        {
            return GetTemplateProcessor(config)?.TemplatePreset != null;
        }

        public static bool IsTemplateSourceAsset(string path)
        {
            return !string.IsNullOrEmpty(path)
                   && path.IndexOf("/AssetFlow.Template.", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static Preset GetTemplatePreset(AssetFlowConfig config)
        {
            return GetTemplateProcessor(config)?.TemplatePreset;
        }

        private static IAssetFlowImporterTemplateProcessor GetTemplateProcessor(AssetFlowConfig config)
        {
            return config?.PreImportProcessors.OfType<IAssetFlowImporterTemplateProcessor>().FirstOrDefault();
        }

        private static bool TryMigrateLegacyTemplateImporter(AssetFlowConfig config, IAssetFlowImporterTemplateProcessor processor)
        {
            var importer = processor.LegacyTemplateImporter;
            if (config == null || importer == null || importer.GetType().FullName != config.TypeKey)
                return false;

            var configPath = AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(configPath))
                return false;

            var preset = new Preset(importer)
            {
                name = PresetSubAssetName
            };
            AssetDatabase.AddObjectToAsset(preset, config);
            RemoveLegacyPresetSubAssets(configPath, preset);
            processor.SetTemplatePreset(preset);
            processor.ClearLegacyTemplateImporter();
            EditorUtility.SetDirty(preset);
            EditorUtility.SetDirty((Object)processor);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);
            return true;
        }

        private static Preset FindExistingPreset(string configPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(configPath)
                .OfType<Preset>()
                .FirstOrDefault();
        }

        private static void EnsurePresetNaming(Preset preset)
        {
            if (preset == null || preset.name == PresetSubAssetName)
                return;

            preset.name = PresetSubAssetName;
            EditorUtility.SetDirty(preset);
        }

        private static void RemovePresetSubAsset(Preset preset)
        {
            if (preset == null)
                return;

            if (AssetDatabase.Contains(preset))
                Object.DestroyImmediate(preset, allowDestroyingAssets: true);
            else
                Object.DestroyImmediate(preset);
        }

        private static bool RemoveLegacyPresetSubAssets(string configPath, Preset keepPreset)
        {
            var changed = false;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(configPath))
            {
                if (asset == null || !AssetDatabase.IsSubAsset(asset))
                    continue;

                if (asset == keepPreset)
                    continue;

                if (asset is Preset preset)
                {
                    Object.DestroyImmediate(preset, allowDestroyingAssets: true);
                    changed = true;
                    continue;
                }

                if (asset.GetType().FullName != LegacyPresetTypeName)
                    continue;

                Object.DestroyImmediate(asset, allowDestroyingAssets: true);
                changed = true;
            }

            if (changed)
                AssetDatabase.SaveAssets();

            return changed;
        }
    }
}
