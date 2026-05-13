using System.IO;
using System.Linq;
using AssetFlow.Editor.Core;
using AssetFlow.Editor.Workflow;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.Importing
{
    public static class AssetFlowPresetUtility
    {
        private const string LegacyPresetTypeName = "UnityEditor.Presets.Preset";

        public static bool EnsureTemplateImporter(AssetFlowConfig config)
        {
            var processor = config == null
                ? null
                : config.PreImportProcessors.OfType<IAssetFlowImporterTemplateProcessor>().FirstOrDefault();
            if (processor == null)
                return false;

            if (IsCompatibleTemplateImporter(config, processor.TemplateImporter))
                return false;

            var templatePath = GetTemplateAssetPath(config);
            if (string.IsNullOrEmpty(templatePath))
                return false;

            var importer = AssetImporter.GetAtPath(templatePath);
            if (importer == null)
            {
                if (!CreateTemplateSourceAsset(config.TypeKey, templatePath))
                    return false;

                AssetDatabase.ImportAsset(templatePath, ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(templatePath);
            }

            if (importer == null || importer.GetType().FullName != config.TypeKey)
                return false;

            var configPath = AssetDatabase.GetAssetPath(config);
            if (!string.IsNullOrEmpty(configPath))
                RemoveLegacyPresetSubAssets(configPath);

            processor.SetTemplateImporter(importer);
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

            var processor = config.PreImportProcessors.OfType<IAssetFlowImporterTemplateProcessor>().FirstOrDefault();
            if (processor == null)
                return false;

            var configPath = AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(configPath))
                return false;

            RemoveLegacyPresetSubAssets(configPath);
            processor.SetTemplateImporter(importer);
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
                : config.PreImportProcessors.OfType<IAssetFlowImporterTemplateProcessor>().FirstOrDefault();
            if (processor == null || processor.TemplateImporter == null)
                return false;

            processor.SetTemplateImporter(null);
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

            return RemoveLegacyPresetSubAssets(configPath);
        }

        public static bool PingPreset(AssetFlowConfig config)
        {
            var importer = config?.PreImportProcessors.OfType<IAssetFlowImporterTemplateProcessor>().FirstOrDefault()?.TemplateImporter;
            if (importer == null)
                return false;

            var asset = AssetDatabase.LoadMainAssetAtPath(importer.assetPath);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return true;
        }

        public static bool IsTemplateSourceAsset(string path)
        {
            return !string.IsNullOrEmpty(path)
                   && path.IndexOf("/AssetFlow.Template.", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool RemoveLegacyPresetSubAssets(string configPath)
        {
            var changed = false;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(configPath))
            {
                if (asset == null || !AssetDatabase.IsSubAsset(asset) || asset.GetType().FullName != LegacyPresetTypeName)
                    continue;

                Object.DestroyImmediate(asset, allowDestroyingAssets: true);
                changed = true;
            }

            if (!changed)
                return false;

            AssetDatabase.SaveAssets();
            return true;
        }

        private static bool IsCompatibleTemplateImporter(AssetFlowConfig config, AssetImporter importer)
        {
            return config != null
                   && importer != null
                   && importer.GetType().FullName == config.TypeKey
                   && !string.IsNullOrEmpty(importer.assetPath)
                   && AssetDatabase.LoadMainAssetAtPath(importer.assetPath) != null;
        }

        private static string GetTemplateAssetPath(AssetFlowConfig config)
        {
            var configPath = config == null ? string.Empty : AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(configPath))
                return string.Empty;

            var folder = AssetFlowPath.GetParentFolder(configPath);
            if (config.TypeKey == typeof(TextureImporter).FullName)
                return $"{folder}/AssetFlow.Template.Texture.png";
            if (config.TypeKey == typeof(ModelImporter).FullName)
                return $"{folder}/AssetFlow.Template.Model.obj";
            if (config.TypeKey == typeof(AudioImporter).FullName)
                return $"{folder}/AssetFlow.Template.Audio.wav";

            return string.Empty;
        }

        private static bool CreateTemplateSourceAsset(string typeKey, string path)
        {
            var absolutePath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrEmpty(directory))
                return false;

            Directory.CreateDirectory(directory);
            if (typeKey == typeof(TextureImporter).FullName)
            {
                var texture = new Texture2D(1, 1);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                return true;
            }

            if (typeKey == typeof(ModelImporter).FullName)
            {
                File.WriteAllText(
                    absolutePath,
                    "o AssetFlowTemplate\n" +
                    "v 0 0 0\n" +
                    "v 1 0 0\n" +
                    "v 0 1 0\n" +
                    "vn 0 0 1\n" +
                    "vt 0 0\n" +
                    "vt 1 0\n" +
                    "vt 0 1\n" +
                    "f 1/1/1 2/2/1 3/3/1\n");
                return true;
            }

            if (typeKey == typeof(AudioImporter).FullName)
            {
                File.WriteAllBytes(absolutePath, CreateSilentWavBytes());
                return true;
            }

            return false;
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
