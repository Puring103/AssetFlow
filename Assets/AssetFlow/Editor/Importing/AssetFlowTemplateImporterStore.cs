using System.IO;
using System.Linq;
using AssetFlow.Editor.Core;
using AssetFlow.Editor.Workflow;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace AssetFlow.Editor.Importing
{
    internal enum TemplateImporterStoreStatus
    {
        Ready,
        Created,
        Migrated,
        Unsupported,
        Failed,
    }

    internal readonly struct TemplateImporterStoreResult
    {
        public TemplateImporterStoreResult(TemplateImporterStoreStatus status, AssetImporter importer, string message = "")
        {
            Status = status;
            Importer = importer;
            Message = message ?? string.Empty;
        }

        public TemplateImporterStoreStatus Status { get; }

        public AssetImporter Importer { get; }

        public string Message { get; }

        public bool Changed => Status == TemplateImporterStoreStatus.Created
                               || Status == TemplateImporterStoreStatus.Migrated;

        public bool Success => Status != TemplateImporterStoreStatus.Unsupported
                               && Status != TemplateImporterStoreStatus.Failed;
    }

    internal static class AssetFlowTemplateImporterStore
    {
        private const string TemplateImporterName = "TemplateImporter";

        internal static TemplateImporterStoreResult EnsureTemplateImporter(AssetFlowConfig config)
        {
            var processor = GetTemplateProcessor(config);
            if (processor == null)
                return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Unsupported, null, "No template processor.");

            var configPath = AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(configPath))
                return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Failed, null, "Config has no asset path.");

            var isCurrentImporterReady = processor.LegacyPreset == null
                && processor.TemplatePreset == null
                && processor.TemplateImporterReference != null
                && processor.TemplateImporterReference.GetType().FullName == config.TypeKey
                && AssetDatabase.IsSubAsset(processor.TemplateImporterReference);
            if (isCurrentImporterReady)
            {
                processor.SetTemplateImporterTypeKey(config.TypeKey);
                var changed = RemovePresetSubAssets(configPath);
                changed |= RemoveExtraImporterSubAssets(configPath, processor.TemplateImporterReference);
                if (changed)
                    AssetDatabase.SaveAssets();
                return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Ready, processor.TemplateImporterReference);
            }

            var status = processor.LegacyPreset != null || processor.TemplatePreset != null || processor.TemplateImporterReference != null
                ? TemplateImporterStoreStatus.Migrated
                : TemplateImporterStoreStatus.Created;
            var importer = processor.LegacyPreset != null || processor.TemplatePreset != null
                ? CreateOrUpdateImporterFromPreset(config, processor, configPath)
                : CreateOrUpdateImporterFromSource(config, processor, configPath);
            if (importer == null)
                return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Failed, null, "Could not create template importer.");

            processor.SetTemplateImporterTypeKey(config.TypeKey);
            processor.SetTemplateImporter(importer);
            processor.SetTemplatePreset(null);
            processor.ClearLegacyPreset();
            RemovePresetSubAssets(configPath);
            RemoveExtraImporterSubAssets(configPath, importer);
            EditorUtility.SetDirty((Object)processor);
            EditorUtility.SetDirty(importer);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);
            return new TemplateImporterStoreResult(status, importer);
        }

        internal static bool NeedsTemplateImporterMaintenance(AssetFlowConfig config)
        {
            var processor = GetTemplateProcessor(config);
            if (processor == null)
                return false;

            var configPath = AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(configPath))
                return false;

            return processor.LegacyPreset != null
                   || processor.TemplatePreset != null
                   || processor.TemplateImporterReference == null
                   || processor.TemplateImporterReference.GetType().FullName != config.TypeKey
                   || !AssetDatabase.IsSubAsset(processor.TemplateImporterReference)
                   || HasPresetSubAssets(configPath)
                   || HasExtraImporterSubAssets(configPath, processor.TemplateImporterReference);
        }

        internal static TemplateImporterStoreResult CaptureFromAsset(AssetFlowConfig config, string assetPath)
        {
            if (config == null || string.IsNullOrEmpty(assetPath))
                return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Failed, null);

            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null || importer.GetType().FullName != config.TypeKey)
                return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Failed, null);

            var processor = GetTemplateProcessor(config);
            if (processor == null)
                return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Unsupported, null);

            var configPath = AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(configPath))
                return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Failed, null);

            var templateImporter = CreateOrUpdateImporterSubAsset(config, processor, importer);
            if (templateImporter == null)
                return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Failed, null);

            processor.SetTemplateImporterTypeKey(config.TypeKey);
            processor.SetTemplateImporter(templateImporter);
            processor.SetTemplatePreset(null);
            processor.ClearLegacyPreset();
            RemovePresetSubAssets(configPath);
            RemoveExtraImporterSubAssets(configPath, templateImporter);
            EditorUtility.SetDirty((Object)processor);
            EditorUtility.SetDirty(templateImporter);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);

            return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Migrated, templateImporter);
        }

        internal static bool RemoveLegacyPresetSubAssets(AssetFlowConfig config)
        {
            var configPath = config == null ? string.Empty : AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(configPath))
                return false;

            return RemovePresetSubAssets(configPath);
        }

        internal static bool IsTemplateSourceAsset(string path)
        {
            return !string.IsNullOrEmpty(path)
                   && path.IndexOf("/AssetFlow.Template.", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IAssetFlowImporterTemplateProcessor GetTemplateProcessor(AssetFlowConfig config)
        {
            return config?.PreImportProcessors.OfType<IAssetFlowImporterTemplateProcessor>().FirstOrDefault();
        }

        private static AssetImporter CreateOrUpdateImporterFromSource(
            AssetFlowConfig config,
            IAssetFlowImporterTemplateProcessor processor,
            string configPath)
        {
            if (processor?.TemplateImporterReference != null && processor.TemplateImporterReference.GetType().FullName == config.TypeKey)
                return CreateOrUpdateImporterSubAsset(config, processor, processor.TemplateImporterReference);

            var sourceImporter = FindExistingSourceImporter(config, configPath);
            if (sourceImporter != null)
                return CreateOrUpdateImporterSubAsset(config, processor, sourceImporter);

            return CreateOrUpdateImporterFromTemporarySource(config, processor);
        }

        private static AssetImporter CreateOrUpdateImporterFromPreset(
            AssetFlowConfig config,
            IAssetFlowImporterTemplateProcessor processor,
            string configPath)
        {
            var sourcePath = CreateTemporaryPresetSource(config.TypeKey, AssetFlowPath.GetParentFolder(configPath));
            if (string.IsNullOrEmpty(sourcePath))
                return null;

            try
            {
                var importer = AssetImporter.GetAtPath(sourcePath);
                var preset = processor.LegacyPreset != null ? processor.LegacyPreset : processor.TemplatePreset;
                if (importer == null || preset == null || importer.GetType().FullName != config.TypeKey || !preset.CanBeAppliedTo(importer))
                    return null;

                preset.ApplyTo(importer);
                return CreateOrUpdateImporterSubAsset(config, processor, importer);
            }
            finally
            {
                AssetDatabase.DeleteAsset(sourcePath);
            }
        }

        private static AssetImporter FindExistingSourceImporter(AssetFlowConfig config, string configPath)
        {
            var configFolder = AssetFlowPath.GetParentFolder(configPath);
            if (!AssetDatabase.IsValidFolder(configFolder))
                return null;

            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { configFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)
                    || !string.Equals(AssetFlowPath.GetParentFolder(path), configFolder, System.StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase)
                    || IsTemplateSourceAsset(path))
                {
                    continue;
                }

                var importer = AssetImporter.GetAtPath(path);
                if (importer != null && importer.GetType().FullName == config.TypeKey)
                    return importer;
            }

            return null;
        }

        private static AssetImporter CreateOrUpdateImporterFromTemporarySource(
            AssetFlowConfig config,
            IAssetFlowImporterTemplateProcessor processor)
        {
            var sourcePath = CreateTemporaryPresetSource(config.TypeKey, AssetFlowPath.GetParentFolder(AssetDatabase.GetAssetPath(config)));
            if (string.IsNullOrEmpty(sourcePath))
                return null;

            try
            {
                var importer = AssetImporter.GetAtPath(sourcePath);
                if (importer == null || importer.GetType().FullName != config.TypeKey)
                    return null;

                return CreateOrUpdateImporterSubAsset(config, processor, importer);
            }
            finally
            {
                AssetDatabase.DeleteAsset(sourcePath);
            }
        }

        private static AssetImporter CreateOrUpdateImporterSubAsset(
            AssetFlowConfig config,
            IAssetFlowImporterTemplateProcessor processor,
            AssetImporter importer)
        {
            var templateImporter = processor.TemplateImporterReference;
            if (templateImporter != null
                && AssetDatabase.IsSubAsset(templateImporter)
                && templateImporter.GetType() != importer.GetType())
            {
                Object.DestroyImmediate(templateImporter, allowDestroyingAssets: true);
                templateImporter = null;
            }

            if (templateImporter == null || !AssetDatabase.IsSubAsset(templateImporter))
            {
                templateImporter = Object.Instantiate(importer);
                templateImporter.name = TemplateImporterName;
                AssetDatabase.AddObjectToAsset(templateImporter, config);
            }
            else
            {
                EditorUtility.CopySerialized(importer, templateImporter);
                templateImporter.name = TemplateImporterName;
            }

            EditorUtility.SetDirty(templateImporter);
            return templateImporter;
        }

        private static bool HasExtraImporterSubAssets(string configPath, AssetImporter activeImporter)
        {
            return AssetDatabase.LoadAllAssetsAtPath(configPath)
                .OfType<AssetImporter>()
                .Any(importer => importer != activeImporter);
        }

        private static bool RemoveExtraImporterSubAssets(string configPath, AssetImporter activeImporter)
        {
            var changed = false;
            foreach (var importer in AssetDatabase.LoadAllAssetsAtPath(configPath).OfType<AssetImporter>().ToList())
            {
                if (importer == activeImporter)
                    continue;

                Object.DestroyImmediate(importer, allowDestroyingAssets: true);
                changed = true;
            }

            return changed;
        }

        private static bool HasPresetSubAssets(string configPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(configPath).OfType<Preset>().Any();
        }

        private static bool RemovePresetSubAssets(string configPath)
        {
            var changed = false;
            foreach (var preset in AssetDatabase.LoadAllAssetsAtPath(configPath).OfType<Preset>().ToList())
            {
                Object.DestroyImmediate(preset, allowDestroyingAssets: true);
                changed = true;
            }

            if (changed)
                AssetDatabase.SaveAssets();

            return changed;
        }

        private static string CreateTemporaryPresetSource(string typeKey, string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                folderPath = "Assets";

            if (typeKey == typeof(TextureImporter).FullName)
            {
                var path = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/AssetFlow.Template.Texture.png");
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return path;
            }

            if (typeKey == typeof(ModelImporter).FullName)
            {
                var path = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/AssetFlow.Template.Model.obj");
                File.WriteAllText(
                    path,
                    "o AssetFlowTemplate\n" +
                    "v 0 0 0\n" +
                    "v 1 0 0\n" +
                    "v 0 1 0\n" +
                    "f 1 2 3\n");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return path;
            }

            if (typeKey == typeof(AudioImporter).FullName)
            {
                var path = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/AssetFlow.Template.Audio.wav");
                File.WriteAllBytes(path, CreateSilentWav());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return path;
            }

            return string.Empty;
        }

        private static byte[] CreateSilentWav()
        {
            const int sampleRate = 44100;
            const short channels = 1;
            const short bitsPerSample = 16;
            const int sampleCount = 1;
            var dataSize = sampleCount * channels * bitsPerSample / 8;

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
