using System.IO;
using AssetFlow.Editor.Importing;
using AssetFlow.Editor.Workflow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowConfigScannerTests
    {
        private const string TestFolder = "Assets/AssetFlowGeneratedTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder("Assets", "AssetFlowGeneratedTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestFolder);
        }

        [Test]
        public void CreateTextureConfig_CreatesRecommendedAssetWithDefaultTemplateProcessor()
        {
            var path = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);

            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(path);

            Assert.That(path, Is.EqualTo(Path.Combine(TestFolder, "AssetFlow.Texture.asset").Replace('\\', '/')));
            Assert.That(config, Is.Not.Null);
            Assert.That(config.PreImportProcessors, Has.Count.EqualTo(1));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(path), Has.Some.TypeOf<ApplyTextureImporterTemplateProcessor>());
        }

        [Test]
        public void CreateModelAndAudioConfig_CreateTypedConfigsWithDefaultTemplateProcessor()
        {
            var modelPath = AssetFlowConfigFactory.CreateModelConfig(TestFolder);
            var audioPath = AssetFlowConfigFactory.CreateAudioConfig(TestFolder);

            var modelConfig = AssetDatabase.LoadAssetAtPath<AssetFlowModelConfig>(modelPath);
            var audioConfig = AssetDatabase.LoadAssetAtPath<AssetFlowAudioConfig>(audioPath);

            Assert.That(modelPath, Is.EqualTo(Path.Combine(TestFolder, "AssetFlow.Model.asset").Replace('\\', '/')));
            Assert.That(audioPath, Is.EqualTo(Path.Combine(TestFolder, "AssetFlow.Audio.asset").Replace('\\', '/')));
            Assert.That(modelConfig.TypeKey, Is.EqualTo(typeof(ModelImporter).FullName));
            Assert.That(audioConfig.TypeKey, Is.EqualTo(typeof(AudioImporter).FullName));
            Assert.That(modelConfig.PreImportProcessors, Has.Count.EqualTo(1));
            Assert.That(audioConfig.PreImportProcessors, Has.Count.EqualTo(1));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(modelPath), Has.Some.TypeOf<ApplyModelImporterTemplateProcessor>());
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(audioPath), Has.Some.TypeOf<ApplyAudioImporterTemplateProcessor>());
        }

        [Test]
        public void Scanner_FindsConfigSnapshots()
        {
            AssetFlowConfigFactory.CreateTextureConfig(TestFolder);

            var snapshots = AssetFlowConfigScanner.FindConfigSnapshots();

            Assert.That(snapshots, Has.Some.Matches<Core.AssetFlowConfigSnapshot>(
                snapshot => snapshot.FolderPath == TestFolder && snapshot.TypeKey == typeof(TextureImporter).FullName));
        }

        [Test]
        public void DependencyBootstrap_RegistersDuringEditorLoad()
        {
            var attribute = typeof(AssetFlowDependencyBootstrap)
                .GetCustomAttributes(typeof(InitializeOnLoadAttribute), inherit: false);

            Assert.That(attribute, Is.Not.Empty);
            Assert.DoesNotThrow(AssetFlowDependency.RegisterAll);
        }
    }
}
