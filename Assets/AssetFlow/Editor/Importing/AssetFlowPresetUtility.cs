using System.Linq;
using System.IO;
using AssetFlow.Editor.Core;
using AssetFlow.Editor.Workflow;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace AssetFlow.Editor.Importing
{
    public static class AssetFlowPresetUtility
    {
        private const string TemporaryAssetFolder = "Assets/AssetFlow/Editor/TemporaryPresetSources";

        public static bool EnsurePreset(AssetFlowConfig config, IAssetFlowPresetProcessor processor)
        {
            if (config == null || processor == null)
                return false;

            if (processor.Preset != null)
                return true;

            var preset = CreatePreset(config);
            if (preset == null)
                return false;

            var configPath = AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(configPath))
                return false;

            AssetDatabase.AddObjectToAsset(preset, config);
            if (!SetPreset(processor, preset))
                return false;

            EditorUtility.SetDirty(preset);
            EditorUtility.SetDirty((Object)processor);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);
            return true;
        }

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

            if (!SetPreset(processor, preset))
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
            if (!SetPreset(processor, null))
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

        public static string CreateTemporarySourceAssetForPresetEditing(string typeKey)
        {
            return CreateTemporarySourceAsset(typeKey);
        }

        public static void DeleteTemporarySourceAssetForPresetEditing(string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath))
                AssetDatabase.DeleteAsset(assetPath);

            DeleteTemporaryFolderIfEmpty();
        }

        private static bool SetPreset(IAssetFlowPresetProcessor processor, Preset preset)
        {
            if (processor is ApplyTextureImporterPresetProcessor textureProcessor)
            {
                textureProcessor.SetPreset(preset);
                return true;
            }

            if (processor is ApplyModelImporterPresetProcessor modelProcessor)
            {
                modelProcessor.SetPreset(preset);
                return true;
            }

            if (processor is ApplyAudioImporterPresetProcessor audioProcessor)
            {
                audioProcessor.SetPreset(preset);
                return true;
            }

            return false;
        }

        private static Preset CreatePreset(AssetFlowConfig config)
        {
            var importer = FindImporterInScope(config, AssetFlowPath.GetParentFolder(AssetDatabase.GetAssetPath(config)));
            if (importer != null)
                return new Preset(importer) { name = importer.GetType().Name + " Preset" };

            var temporaryPath = CreateTemporarySourceAsset(config.TypeKey);
            if (string.IsNullOrEmpty(temporaryPath))
                return null;

            try
            {
                importer = AssetImporter.GetAtPath(temporaryPath);
                return importer == null
                    ? null
                    : new Preset(importer) { name = importer.GetType().Name + " Preset" };
            }
            finally
            {
                AssetDatabase.DeleteAsset(temporaryPath);
                DeleteTemporaryFolderIfEmpty();
            }
        }

        private static AssetImporter FindImporterInScope(AssetFlowConfig config, string configFolder)
        {
            var normalizedFolder = AssetFlowPath.Normalize(configFolder);
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { normalizedFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!config.IncludeSubfolders && !string.Equals(AssetFlowPath.GetParentFolder(path), normalizedFolder, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var importer = AssetImporter.GetAtPath(path);
                if (importer != null && importer.GetType().FullName == config.TypeKey)
                    return importer;
            }

            return null;
        }

        private static string CreateTemporarySourceAsset(string typeKey)
        {
            EnsureTemporaryFolder();

            if (typeKey == typeof(TextureImporter).FullName)
            {
                var texture = new Texture2D(1, 1);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                var path = $"{TemporaryAssetFolder}/AssetFlowPresetSource.png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                return path;
            }

            if (typeKey == typeof(AudioImporter).FullName)
            {
                var path = $"{TemporaryAssetFolder}/AssetFlowPresetSource.wav";
                File.WriteAllBytes(path, CreateSilentWavBytes());
                AssetDatabase.ImportAsset(path);
                return path;
            }

            return string.Empty;
        }

        private static void EnsureTemporaryFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/AssetFlow/Editor/TemporaryPresetSources"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/AssetFlow/Editor"))
                    return;

                AssetDatabase.CreateFolder("Assets/AssetFlow/Editor", "TemporaryPresetSources");
            }
        }

        private static void DeleteTemporaryFolderIfEmpty()
        {
            var guids = AssetDatabase.FindAssets(string.Empty, new[] { TemporaryAssetFolder });
            if (guids.Length == 0)
                AssetDatabase.DeleteAsset(TemporaryAssetFolder);
        }

        private static byte[] CreateSilentWavBytes()
        {
            const int sampleRate = 44100;
            const short channels = 1;
            const short bitsPerSample = 16;
            const int sampleCount = 1;
            const int dataSize = sampleCount * channels * bitsPerSample / 8;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * bitsPerSample / 8);
                writer.Write((short)(channels * bitsPerSample / 8));
                writer.Write(bitsPerSample);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);
                writer.Write((short)0);
                return stream.ToArray();
            }
        }
    }
}
