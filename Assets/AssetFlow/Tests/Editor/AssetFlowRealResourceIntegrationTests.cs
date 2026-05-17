using System.IO;
using System.Linq;
using AssetFlow.Editor.Core;
using AssetFlow.Editor.Importing;
using AssetFlow.Editor.UI;
using AssetFlow.Editor.Workflow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowRealResourceIntegrationTests
    {
        private const string TestRoot = "Assets/AssetFlowRealResourceIntegrationTests";
        private const string TextureFolder = TestRoot + "/Textures";
        private const string ModelFolder = TestRoot + "/Models";
        private const string AudioFolder = TestRoot + "/Audio";
        private const string UnmanagedFolder = TestRoot + "/Unmanaged";
        private const string TextureNestedFolder = TextureFolder + "/Nested";
        private const string TextureConflictFolder = TextureFolder + "/Conflict";
        private const string IndexPath = "Library/AssetFlow/Index.json";
        private const string AppliedStatePath = "Library/AssetFlowTests/AppliedState.json";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            if (File.Exists(IndexPath))
                File.Delete(IndexPath);
            if (File.Exists(AppliedStatePath))
                File.Delete(AppliedStatePath);
            AssetFlowApplyService.SetAppliedStateStoreForTests(new AssetFlowAppliedStateStore(AppliedStatePath));

            CreateFolder("Assets", "AssetFlowRealResourceIntegrationTests");
            CreateFolder(TestRoot, "Textures");
            CreateFolder(TestRoot, "Models");
            CreateFolder(TestRoot, "Audio");
            CreateFolder(TestRoot, "Unmanaged");
            CreateFolder(TextureFolder, "Nested");
            CreateFolder(TextureFolder, "Conflict");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            if (File.Exists(IndexPath))
                File.Delete(IndexPath);
            if (File.Exists(AppliedStatePath))
                File.Delete(AppliedStatePath);
            AssetFlowApplyService.SetAppliedStateStoreForTests(null);
        }

        [Test]
        public void ApplyToManagedAssets_AppliesTemplateImportersToRealTextureModelAndAudioAssets()
        {
            var textureSource = WritePng(TextureFolder + "/texture-source.png");
            var textureTarget = WritePng(TextureFolder + "/texture-target.png");
            var modelSource = WriteObj(ModelFolder + "/model-source.obj");
            var modelTarget = WriteObj(ModelFolder + "/model-target.obj");
            var audioSource = WriteWav(AudioFolder + "/audio-source.wav");
            var audioTarget = WriteWav(AudioFolder + "/audio-target.wav");

            ConfigureTextureImporter(textureSource, TextureImporterType.Sprite, mipmaps: false);
            ConfigureTextureImporter(textureTarget, TextureImporterType.Default, mipmaps: true);
            ConfigureModelImporter(modelSource, 2.5f, importCameras: false);
            ConfigureModelImporter(modelTarget, 1f, importCameras: true);
            ConfigureAudioImporter(audioSource, forceToMono: true, loadInBackground: true);
            ConfigureAudioImporter(audioTarget, forceToMono: false, loadInBackground: false);

            var textureConfig = CreateConfig<AssetFlowTextureConfig>(TextureFolder, AssetFlowConfigFactory.CreateTextureConfig);
            var modelConfig = CreateConfig<AssetFlowModelConfig>(ModelFolder, AssetFlowConfigFactory.CreateModelConfig);
            var audioConfig = CreateConfig<AssetFlowAudioConfig>(AudioFolder, AssetFlowConfigFactory.CreateAudioConfig);

            Assert.That(AssetFlowApplyService.FindManagedAssetsForConfig(textureConfig), Is.EquivalentTo(new[] { textureSource, textureTarget }));
            Assert.That(AssetFlowApplyService.FindManagedAssetsForConfig(modelConfig), Is.EquivalentTo(new[] { modelSource, modelTarget }));
            Assert.That(AssetFlowApplyService.FindManagedAssetsForConfig(audioConfig), Is.EquivalentTo(new[] { audioSource, audioTarget }));

            Assert.That(AssetFlowApplyService.ApplyToManagedAssets(textureConfig), Is.EqualTo(2));
            Assert.That(AssetFlowApplyService.ApplyToManagedAssets(modelConfig), Is.EqualTo(2));
            Assert.That(AssetFlowApplyService.ApplyToManagedAssets(audioConfig), Is.EqualTo(2));

            var targetTextureImporter = (TextureImporter)AssetImporter.GetAtPath(textureTarget);
            Assert.That(targetTextureImporter.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(targetTextureImporter.mipmapEnabled, Is.False);

            var targetModelImporter = (ModelImporter)AssetImporter.GetAtPath(modelTarget);
            Assert.That(targetModelImporter.globalScale, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(targetModelImporter.importCameras, Is.False);

            var targetAudioImporter = (AudioImporter)AssetImporter.GetAtPath(audioTarget);
            Assert.That(targetAudioImporter.forceToMono, Is.True);
            Assert.That(targetAudioImporter.loadInBackground, Is.True);

            var index = new AssetFlowIndexStore().Load();
            Assert.That(FindAssetRecord(index, textureTarget).lastProcessedRuleHash, Is.EqualTo(textureConfig.ComputeRuleHash()));
            Assert.That(FindAssetRecord(index, modelTarget).lastProcessedRuleHash, Is.EqualTo(modelConfig.ComputeRuleHash()));
            Assert.That(FindAssetRecord(index, audioTarget).lastProcessedRuleHash, Is.EqualTo(audioConfig.ComputeRuleHash()));
        }

        [Test]
        public void ScopeResolution_WithRealTextureAssets_RespectsIncludeSubfoldersChildConfigAndConflictBoundaries()
        {
            var rootTexture = WritePng(TextureFolder + "/root.png");
            var nestedTexture = WritePng(TextureNestedFolder + "/nested.png");
            var conflictTexture = WritePng(TextureConflictFolder + "/conflict.png");

            var rootConfigPath = AssetFlowConfigFactory.CreateTextureConfig(TextureFolder);
            var rootConfig = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(rootConfigPath);
            rootConfig.IncludeSubfolders = true;
            EditorUtility.SetDirty(rootConfig);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(rootConfigPath);

            var childConfigPath = AssetFlowConfigFactory.CreateTextureConfig(TextureNestedFolder);
            var childConfig = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(childConfigPath);

            var conflictConfigAPath = AssetFlowConfigFactory.CreateTextureConfig(TextureConflictFolder);
            var conflictConfigBPath = AssetFlowConfigFactory.CreateTextureConfig(TextureConflictFolder);
            var snapshots = AssetFlowConfigScanner.FindConfigSnapshots();
            var resolver = new AssetFlowResolver(snapshots);

            Assert.That(resolver.Resolve(rootTexture, typeof(TextureImporter).FullName).Config.ConfigGuid, Is.EqualTo(rootConfig.ToSnapshot().ConfigGuid));
            Assert.That(resolver.Resolve(nestedTexture, typeof(TextureImporter).FullName).Config.ConfigGuid, Is.EqualTo(childConfig.ToSnapshot().ConfigGuid));
            Assert.That(resolver.Resolve(conflictTexture, typeof(TextureImporter).FullName).Status, Is.EqualTo(AssetFlowResolveStatus.Conflict));

            var rootManaged = AssetFlowApplyService.FindManagedAssetsForConfig(rootConfig);
            Assert.That(rootManaged, Contains.Item(rootTexture));
            Assert.That(rootManaged, Does.Not.Contain(nestedTexture));
            Assert.That(rootManaged, Does.Not.Contain(conflictTexture));

            Assert.That(AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(conflictConfigAPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(conflictConfigBPath), Is.Not.Null);
        }

        [Test]
        public void AutomaticImport_WritesIndexAndValidationResultsForRealManagedTexture()
        {
            var texturePath = WritePng(TextureFolder + "/validated.png");
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TextureFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            var validator = ScriptableObject.CreateInstance<RealResourceTextureValidator>();
            validator.name = nameof(RealResourceTextureValidator);
            config.AddHandlerAsSubAsset(validator);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);

            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

            var index = new AssetFlowIndexStore().Load();
            var assetRecord = FindAssetRecord(index, texturePath);
            Assert.That(assetRecord.managedByConfigGuid, Is.EqualTo(config.ToSnapshot().ConfigGuid));
            Assert.That(assetRecord.lastProcessedRuleHash, Is.EqualTo(config.ComputeRuleHash()));

            var validationRecord = index.ValidationResults.Single(record => record.assetGuid == AssetDatabase.AssetPathToGUID(texturePath));
            Assert.That(validationRecord.configGuid, Is.EqualTo(config.ToSnapshot().ConfigGuid));
            Assert.That(validationRecord.severity, Is.EqualTo(AssetFlowIssueSeverity.Warning.ToString()));
            Assert.That(validationRecord.message, Does.Contain(texturePath));
        }

        [Test]
        public void AutomaticImport_WritesPreImportIssuesToIndex()
        {
            var texturePath = WritePng(TextureFolder + "/pre-import-warning.png");
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TextureFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            config.ResetToDefaultsForTests();
            var preProcessor = ScriptableObject.CreateInstance<ReportingPreImportProcessor>();
            preProcessor.name = nameof(ReportingPreImportProcessor);
            config.AddHandlerAsSubAsset(preProcessor);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);

            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

            var index = new AssetFlowIndexStore().Load();
            var assetGuid = AssetDatabase.AssetPathToGUID(texturePath);
            Assert.That(index.ValidationResults, Has.Some.Matches<AssetFlowValidationRecord>(
                record => record.assetGuid == assetGuid
                          && record.configGuid == config.ToSnapshot().ConfigGuid
                          && record.severity == AssetFlowIssueSeverity.Warning.ToString()
                          && record.message == "pre-import warning"));
        }

        [Test]
        public void MovingRealTextureIntoManagedFolder_ReevaluatesAndIndexesMovedAsset()
        {
            var unmanagedPath = WritePng(UnmanagedFolder + "/move-me.png");
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TextureFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            var movedPath = TextureFolder + "/move-me.png";

            Assert.That(AssetDatabase.MoveAsset(unmanagedPath, movedPath), Is.Empty);
            AssetDatabase.ImportAsset(movedPath, ImportAssetOptions.ForceUpdate);

            var index = new AssetFlowIndexStore().Load();
            var assetRecord = FindAssetRecord(index, movedPath);
            Assert.That(assetRecord.managedByConfigGuid, Is.EqualTo(config.ToSnapshot().ConfigGuid));
            Assert.That(assetRecord.managedByConfigPath, Is.EqualTo(configPath));
        }

        [Test]
        public void MovingRealTextureOutOfManagedFolder_RemovesAssetFromIndex()
        {
            var texturePath = WritePng(TextureFolder + "/move-out.png");
            AssetFlowConfigFactory.CreateTextureConfig(TextureFolder);

            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            var guid = AssetDatabase.AssetPathToGUID(texturePath);
            Assert.That(new AssetFlowIndexStore().Load().Assets.Any(asset => asset.assetGuid == guid), Is.True);

            var movedPath = UnmanagedFolder + "/move-out.png";
            Assert.That(AssetDatabase.MoveAsset(texturePath, movedPath), Is.Empty);
            AssetDatabase.ImportAsset(movedPath, ImportAssetOptions.ForceUpdate);

            var index = new AssetFlowIndexStore().Load();
            Assert.That(index.Assets.Any(asset => asset.assetGuid == guid), Is.False);
            Assert.That(index.ValidationResults.Any(record => record.assetGuid == guid), Is.False);
        }

        [Test]
        public void MovingRealTextureIntoUnincludedSubfolder_RemovesAssetFromIndex()
        {
            var texturePath = WritePng(TextureFolder + "/move-into-subfolder.png");
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TextureFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            config.IncludeSubfolders = false;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);

            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            var guid = AssetDatabase.AssetPathToGUID(texturePath);
            Assert.That(new AssetFlowIndexStore().Load().Assets.Any(asset => asset.assetGuid == guid), Is.True);

            var movedPath = TextureNestedFolder + "/move-into-subfolder.png";
            Assert.That(AssetDatabase.MoveAsset(texturePath, movedPath), Is.Empty);
            AssetDatabase.ImportAsset(movedPath, ImportAssetOptions.ForceUpdate);

            var index = new AssetFlowIndexStore().Load();
            Assert.That(index.Assets.Any(asset => asset.assetGuid == guid), Is.False);
            Assert.That(index.ValidationResults.Any(record => record.assetGuid == guid), Is.False);
        }

        [Test]
        public void ManagerCacheSignature_ChangesWhenGuidMovesIntoUnincludedSubfolder()
        {
            var texturePath = WritePng(TextureFolder + "/stale-manager-cache.png");
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TextureFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            config.IncludeSubfolders = false;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);

            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            var guid = AssetDatabase.AssetPathToGUID(texturePath);
            var snapshot = config.ToSnapshot();
            var staleIndex = new AssetFlowIndex();
            staleIndex.UpsertAsset(new AssetFlowAssetRecord
            {
                assetGuid = guid,
                assetPath = texturePath,
                importerTypeKey = typeof(TextureImporter).FullName,
                managedByConfigGuid = snapshot.ConfigGuid,
                managedByConfigPath = snapshot.ConfigPath,
                lastProcessedRuleHash = snapshot.RuleHash,
                lastProcessedTicks = 1,
            });

            var beforeSignature = AssetFlowManagerWindow.BuildCacheSignature(staleIndex, new[] { snapshot });
            var movedPath = TextureNestedFolder + "/stale-manager-cache.png";
            Assert.That(AssetDatabase.MoveAsset(texturePath, movedPath), Is.Empty);

            var afterSignature = AssetFlowManagerWindow.BuildCacheSignature(staleIndex, new[] { snapshot });
            Assert.That(afterSignature, Is.Not.EqualTo(beforeSignature));

            var pathsByConfig = AssetFlowManagerWindow.FindManagedAssetPathsByConfig(staleIndex, new[] { snapshot }, out var cacheNeedsReconcile);
            Assert.That(pathsByConfig[snapshot.ConfigGuid], Is.Empty);
            Assert.That(cacheNeedsReconcile, Is.True);
        }

        [Test]
        public void DeletingRealManagedTexture_RemovesAssetFromIndex()
        {
            var texturePath = WritePng(TextureFolder + "/delete-me.png");
            AssetFlowConfigFactory.CreateTextureConfig(TextureFolder);

            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            var guid = AssetDatabase.AssetPathToGUID(texturePath);
            Assert.That(new AssetFlowIndexStore().Load().Assets.Any(asset => asset.assetGuid == guid), Is.True);

            AssetDatabase.DeleteAsset(texturePath);

            var index = new AssetFlowIndexStore().Load();
            Assert.That(index.Assets.Any(asset => asset.assetGuid == guid), Is.False);
            Assert.That(index.ValidationResults.Any(record => record.assetGuid == guid), Is.False);
        }

        [Test]
        public void EditingConfigWithoutApply_MarksRealManagedTextureOutOfDate()
        {
            var texturePath = WritePng(TextureFolder + "/stale.png");
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TextureFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);

            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            Assert.That(AssetFlowApplyService.CountOutOfDateManagedAssets(config), Is.EqualTo(0));

            config.IncludeSubfolders = !config.IncludeSubfolders;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Assert.That(AssetFlowApplyService.CountOutOfDateManagedAssets(config), Is.EqualTo(1));
        }

        [Test]
        public void ConfigurationChange_ReconcilesManagedAssetsForCacheBackedManagerTree()
        {
            var nestedTexture = WritePng(TextureNestedFolder + "/from-config-change.png");
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TextureFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            config.IncludeSubfolders = false;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);
            AssetFlowConfigurationChangeProcessor.ProcessConfigurationChanges();

            var guid = AssetDatabase.AssetPathToGUID(nestedTexture);
            Assert.That(new AssetFlowIndexStore().Load().Assets.Any(asset => asset.assetGuid == guid), Is.False);

            config.IncludeSubfolders = true;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);
            AssetFlowConfigurationChangeProcessor.ProcessConfigurationChanges();

            var snapshot = config.ToSnapshot();
            var index = new AssetFlowIndexStore().Load();
            var assetRecord = index.Assets.SingleOrDefault(asset => asset.assetGuid == guid);
            Assert.That(assetRecord, Is.Not.Null);
            Assert.That(assetRecord.managedByConfigGuid, Is.EqualTo(snapshot.ConfigGuid));
            Assert.That(assetRecord.assetPath, Is.EqualTo(nestedTexture));
            Assert.That(index.IsOutOfDate(guid, snapshot.ConfigGuid, snapshot.RuleHash), Is.True);
        }

        [Test]
        public void ConfigurationChange_WithAddedConfig_AutomaticallyReprocessesNewlyManagedAssets()
        {
            var texturePath = WritePng(TextureFolder + "/auto-added-config.png");
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TextureFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);

            var count = AssetFlowConfigurationChangeProcessor.ProcessConfigurationChangesImmediatelyForTests(new[]
            {
                new AssetFlowConfigurationChangeProcessor.Change(
                    configPath,
                    AssetFlowConfigurationChangeProcessor.ChangeKind.Added),
            });

            var index = new AssetFlowIndexStore().Load();
            var assetRecord = FindAssetRecord(index, texturePath);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(assetRecord.managedByConfigGuid, Is.EqualTo(config.ToSnapshot().ConfigGuid));
            Assert.That(assetRecord.lastProcessedRuleHash, Is.EqualTo(config.ComputeRuleHash()));
        }

        [Test]
        public void ConfigurationChange_WithEditedConfig_MarksManagedAssetsOutOfDateWithoutAutoReprocess()
        {
            var texturePath = WritePng(TextureFolder + "/edited-config-stale.png");
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TextureFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);

            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            var firstRuleHash = config.ComputeRuleHash();

            config.IncludeSubfolders = !config.IncludeSubfolders;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            var count = AssetFlowConfigurationChangeProcessor.ProcessConfigurationChangesImmediatelyForTests(new[]
            {
                new AssetFlowConfigurationChangeProcessor.Change(
                    configPath,
                    AssetFlowConfigurationChangeProcessor.ChangeKind.Edited),
            });

            var index = new AssetFlowIndexStore().Load();
            var assetRecord = FindAssetRecord(index, texturePath);
            Assert.That(count, Is.EqualTo(0));
            Assert.That(assetRecord.lastProcessedRuleHash, Is.EqualTo(firstRuleHash));
            Assert.That(index.IsOutOfDate(AssetDatabase.AssetPathToGUID(texturePath), config.ToSnapshot().ConfigGuid, config.ComputeRuleHash()), Is.True);
        }

        [Test]
        public void ApplyToManagedAssets_SavesAppliedStateForCurrentConfigSnapshot()
        {
            var texturePath = WritePng(TextureFolder + "/applied-state.png");
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TextureFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);

            config.IncludeSubfolders = !config.IncludeSubfolders;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Assert.That(AssetFlowApplyService.ApplyToManagedAssets(config), Is.EqualTo(1));

            var snapshot = config.ToSnapshot();
            var applied = new AssetFlowAppliedStateStore(AppliedStatePath).Find(snapshot.ConfigGuid);
            Assert.That(applied, Is.Not.Null);
            Assert.That(applied.ruleHash, Is.EqualTo(snapshot.RuleHash));
            Assert.That(applied.snapshotJson, Does.Contain("includeSubfolders"));
            Assert.That(AssetDatabase.AssetPathToGUID(texturePath), Is.Not.Empty);
        }

        [Test]
        public void ApplyToManagedAssets_SavesDirtyTemplateImporterBeforeRecordingAppliedState()
        {
            var texturePath = WritePng(TextureFolder + "/dirty-template-subasset.png");
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TextureFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            var processor = (ApplyTextureImporterTemplateProcessor)config.PreImportProcessors[0];
            var templateImporter = (TextureImporter)processor.TemplateImporter;
            templateImporter.mipmapEnabled = false;
            EditorUtility.SetDirty(templateImporter);
            EditorUtility.SetDirty(config);

            var expectedRuleHash = config.ComputeRuleHash();
            Assert.That(AssetFlowApplyService.ApplyToManagedAssets(config), Is.EqualTo(1));

            var applied = new AssetFlowAppliedStateStore(AppliedStatePath).Find(config.ToSnapshot().ConfigGuid);
            var targetImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            Assert.That(applied, Is.Not.Null);
            Assert.That(applied.ruleHash, Is.EqualTo(expectedRuleHash));
            Assert.That(targetImporter.mipmapEnabled, Is.False);
        }

        [Test]
        public void ConfigurationChange_RemovesDeletedConfigAndFormerManagedAssetsFromIndex()
        {
            var texturePath = WritePng(TextureFolder + "/tracked.png");
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TextureFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);

            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            var indexBeforeDelete = new AssetFlowIndexStore().Load();
            Assert.That(indexBeforeDelete.Configs.Any(record => record.configGuid == config.ToSnapshot().ConfigGuid), Is.True);
            Assert.That(indexBeforeDelete.Assets.Any(record => record.assetGuid == AssetDatabase.AssetPathToGUID(texturePath)), Is.True);

            var configGuid = config.ToSnapshot().ConfigGuid;
            AssetDatabase.DeleteAsset(configPath);
            AssetFlowConfigurationChangeProcessor.ProcessConfigurationChanges();

            var indexAfterDelete = new AssetFlowIndexStore().Load();
            Assert.That(indexAfterDelete.Configs.Any(record => record.configGuid == configGuid), Is.False);
            Assert.That(indexAfterDelete.Assets.Any(record => record.assetGuid == AssetDatabase.AssetPathToGUID(texturePath)), Is.False);
        }

        private static TConfig CreateConfig<TConfig>(
            string folder,
            System.Func<string, string> createConfig)
            where TConfig : AssetFlowConfig
        {
            var configPath = createConfig(folder);
            return AssetDatabase.LoadAssetAtPath<TConfig>(configPath);
        }

        private static AssetFlowAssetRecord FindAssetRecord(AssetFlowIndex index, string assetPath)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var record = index.Assets.SingleOrDefault(asset => asset.assetGuid == guid);
            Assert.That(record, Is.Not.Null, $"Expected index record for {assetPath}.");
            return record;
        }

        private static void ConfigureTextureImporter(string path, TextureImporterType textureType, bool mipmaps)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = textureType;
            importer.mipmapEnabled = mipmaps;
            importer.SaveAndReimport();
        }

        private static void ConfigureModelImporter(string path, float globalScale, bool importCameras)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = globalScale;
            importer.importCameras = importCameras;
            importer.SaveAndReimport();
        }

        private static void ConfigureAudioImporter(string path, bool forceToMono, bool loadInBackground)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = forceToMono;
            importer.loadInBackground = loadInBackground;
            importer.SaveAndReimport();
        }

        private static string WritePng(string path)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                    texture.SetPixel(x, y, (x + y) % 2 == 0 ? Color.white : Color.cyan);
            }

            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return path;
        }

        private static string WriteObj(string path)
        {
            File.WriteAllText(
                path,
                "o AssetFlowQuad\n" +
                "v 0 0 0\n" +
                "v 1 0 0\n" +
                "v 1 1 0\n" +
                "v 0 1 0\n" +
                "vn 0 0 1\n" +
                "vt 0 0\n" +
                "vt 1 0\n" +
                "vt 1 1\n" +
                "vt 0 1\n" +
                "f 1/1/1 2/2/1 3/3/1\n" +
                "f 1/1/1 3/3/1 4/4/1\n");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return path;
        }

        private static string WriteWav(string path)
        {
            const int sampleRate = 44100;
            const short channels = 1;
            const short bitsPerSample = 16;
            const int sampleCount = 16;
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
                for (var i = 0; i < sampleCount; i++)
                    writer.Write((short)0);
                File.WriteAllBytes(path, stream.ToArray());
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return path;
        }

        private static void CreateFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private sealed class ReportingPreImportProcessor : AssetFlowPreImportProcessor<TextureImporter>
        {
            public override void Process(TextureImporter importer, AssetFlowPreImportContext context)
            {
                context.ReportWarning("pre-import warning");
            }
        }
    }
}
