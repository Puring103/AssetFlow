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
        public void CreateTextureConfig_CreatesRecommendedAssetWithDefaultPresetProcessor()
        {
            var path = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);

            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(path);

            Assert.That(path, Is.EqualTo(Path.Combine(TestFolder, "AssetFlow.Texture.asset").Replace('\\', '/')));
            Assert.That(config, Is.Not.Null);
            Assert.That(config.PreImportProcessors, Has.Count.EqualTo(1));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(path), Has.Some.TypeOf<ApplyTextureImporterPresetProcessor>());
        }

        [Test]
        public void Scanner_FindsConfigSnapshots()
        {
            AssetFlowConfigFactory.CreateTextureConfig(TestFolder);

            var snapshots = AssetFlowConfigScanner.FindConfigSnapshots();

            Assert.That(snapshots, Has.Some.Matches<Core.AssetFlowConfigSnapshot>(
                snapshot => snapshot.FolderPath == TestFolder && snapshot.TypeKey == typeof(TextureImporter).FullName));
        }
    }
}
