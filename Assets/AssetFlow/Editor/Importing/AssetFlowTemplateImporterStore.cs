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
        private const string TemplatePresetName = "TemplateImporter";
        private static readonly System.Collections.Generic.Dictionary<int, PreviewImporterRecord> PreviewImporters =
            new System.Collections.Generic.Dictionary<int, PreviewImporterRecord>();

        internal static TemplateImporterStoreResult EnsureTemplateImporter(AssetFlowConfig config)
        {
            var processor = GetTemplateProcessor(config);
            if (processor == null)
                return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Unsupported, null, "No template processor.");

            var configPath = AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(configPath))
                return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Failed, null, "Config has no asset path.");

            RemoveImporterSubAssets(configPath);

            if (processor.LegacyPreset == null
                && processor.TemplateImporterReference == null
                && processor.TemplatePreset != null
                && PresetMatches(processor, config.TypeKey))
            {
                processor.SetTemplateImporterTypeKey(config.TypeKey);
                RemoveLegacyPresetSubAssets(configPath, processor.TemplatePreset);
                ClearImporterReference(processor);
                var importer = GetPreviewImporter(processor);
                return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Ready, importer);
            }

            var status = processor.LegacyPreset != null || processor.TemplateImporter != null
                ? TemplateImporterStoreStatus.Migrated
                : TemplateImporterStoreStatus.Created;
            var preset = processor.LegacyPreset != null
                ? CreateOrUpdatePresetFromLegacyPreset(config, processor, configPath)
                : CreateOrUpdatePresetFromSource(config, processor, configPath);
            if (preset == null)
                return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Failed, null, "Could not create template preset.");

            processor.SetTemplatePreset(preset);
            processor.SetTemplateImporterTypeKey(config.TypeKey);
            processor.SetTemplateImporter(null);
            processor.ClearLegacyPreset();
            RemoveLegacyPresetSubAssets(configPath, preset);
            EditorUtility.SetDirty((Object)processor);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);
            return new TemplateImporterStoreResult(status, GetPreviewImporter(processor));
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
                   || processor.TemplateImporterReference != null
                   || processor.TemplatePreset == null
                   || !PresetMatches(processor, config.TypeKey)
                   || HasLegacyPresetSubAssets(configPath, processor.TemplatePreset)
                   || HasImporterSubAssets(configPath);
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

            var preset = CreateOrUpdatePresetSubAsset(config, processor, importer);
            if (preset == null)
                return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Failed, null);

            processor.SetTemplatePreset(preset);
            processor.SetTemplateImporterTypeKey(config.TypeKey);
            processor.SetTemplateImporter(null);
            processor.ClearLegacyPreset();
            RemoveLegacyPresetSubAssets(configPath, preset);
            RemoveImporterSubAssets(configPath);
            EditorUtility.SetDirty((Object)processor);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);

            return new TemplateImporterStoreResult(TemplateImporterStoreStatus.Migrated, GetPreviewImporter(processor));
        }

        internal static AssetImporter GetPreviewImporter(IAssetFlowImporterTemplateProcessor processor)
        {
            if (processor?.TemplatePreset == null)
                return null;

            var processorId = ((Object)processor).GetInstanceID();
            if (PreviewImporters.TryGetValue(processorId, out var existing)
                && existing.Preset == processor.TemplatePreset
                && existing.Importer != null)
            {
                processor.TemplatePreset.ApplyTo(existing.Importer);
                return existing.Importer;
            }

            var importer = CreateImporterForTypeName(processor.TemplateImporterTypeKey);
            if (importer == null)
                return null;

            processor.TemplatePreset.ApplyTo(importer);
            importer.name = TemplatePresetName;
            PreviewImporters[processorId] = new PreviewImporterRecord(processor.TemplatePreset, importer);
            return importer;
        }

        internal static bool RemoveLegacyPresetSubAssets(AssetFlowConfig config)
        {
            var configPath = config == null ? string.Empty : AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(configPath))
                return false;

            return RemoveLegacyPresetSubAssets(configPath, GetTemplateProcessor(config)?.TemplatePreset);
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

        private static Preset CreateOrUpdatePresetFromSource(
            AssetFlowConfig config,
            IAssetFlowImporterTemplateProcessor processor,
            string configPath)
        {
            if (processor?.TemplateImporterReference != null && processor.TemplateImporterReference.GetType().FullName == config.TypeKey)
                return CreateOrUpdatePresetSubAsset(config, processor, processor.TemplateImporterReference);

            var sourceImporter = FindExistingSourceImporter(config, configPath);
            if (sourceImporter != null)
                return CreateOrUpdatePresetSubAsset(config, processor, sourceImporter);

            return CreateOrUpdatePresetFromTemporarySource(config, processor);
        }

        private static Preset CreateOrUpdatePresetFromLegacyPreset(
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
                if (importer == null || importer.GetType().FullName != config.TypeKey || !processor.LegacyPreset.CanBeAppliedTo(importer))
                    return null;

                processor.LegacyPreset.ApplyTo(importer);
                return CreateOrUpdatePresetSubAsset(config, processor, importer);
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

        private static Preset CreateOrUpdatePresetFromTemporarySource(
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

                return CreateOrUpdatePresetSubAsset(config, processor, importer);
            }
            finally
            {
                AssetDatabase.DeleteAsset(sourcePath);
            }
        }

        private static Preset CreateOrUpdatePresetSubAsset(
            AssetFlowConfig config,
            IAssetFlowImporterTemplateProcessor processor,
            AssetImporter importer)
        {
            var preset = processor.TemplatePreset;
            if (preset == null || !AssetDatabase.IsSubAsset(preset))
            {
                preset = new Preset(importer)
                {
                    name = TemplatePresetName
                };
                AssetDatabase.AddObjectToAsset(preset, config);
            }
            else
            {
                preset.UpdateProperties(importer);
                preset.name = TemplatePresetName;
            }

            EditorUtility.SetDirty(preset);
            return preset;
        }

        private static AssetImporter CreateImporterForTypeName(string typeKey)
        {
            var folder = "Assets";
            var sourcePath = CreateTemporaryPresetSource(typeKey, folder);
            if (string.IsNullOrEmpty(sourcePath))
                return null;

            try
            {
                var importer = AssetImporter.GetAtPath(sourcePath);
                if (importer == null)
                    return null;

                var copy = Object.Instantiate(importer);
                copy.name = TemplatePresetName;
                return copy;
            }
            finally
            {
                AssetDatabase.DeleteAsset(sourcePath);
            }
        }

        private static bool PresetMatches(IAssetFlowImporterTemplateProcessor processor, string typeKey)
        {
            return processor?.TemplatePreset != null
                   && (string.Equals(processor.TemplateImporterTypeKey, typeKey, System.StringComparison.Ordinal)
                       || string.Equals(processor.TemplatePreset.GetTargetFullTypeName(), typeKey, System.StringComparison.Ordinal));
        }

        private static void ClearImporterReference(IAssetFlowImporterTemplateProcessor processor)
        {
            if (processor?.TemplateImporterReference != null)
            {
                processor.SetTemplateImporter(null);
                EditorUtility.SetDirty((Object)processor);
            }
        }

        private static bool HasImporterSubAssets(string configPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(configPath).OfType<AssetImporter>().Any();
        }

        private static void RemoveImporterSubAssets(string configPath)
        {
            foreach (var importer in AssetDatabase.LoadAllAssetsAtPath(configPath).OfType<AssetImporter>().ToList())
                Object.DestroyImmediate(importer, allowDestroyingAssets: true);
        }

        private static bool HasLegacyPresetSubAssets(string configPath, Preset activePreset)
        {
            return AssetDatabase.LoadAllAssetsAtPath(configPath)
                .OfType<Preset>()
                .Any(preset => preset != activePreset);
        }

        private static bool RemoveLegacyPresetSubAssets(string configPath, Preset activePreset)
        {
            var changed = false;
            foreach (var preset in AssetDatabase.LoadAllAssetsAtPath(configPath).OfType<Preset>().ToList())
            {
                if (preset == activePreset)
                    continue;

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

        private readonly struct PreviewImporterRecord
        {
            public PreviewImporterRecord(Preset preset, AssetImporter importer)
            {
                Preset = preset;
                Importer = importer;
            }

            public Preset Preset { get; }

            public AssetImporter Importer { get; }
        }
    }
}
