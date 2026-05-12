using System.Collections.Generic;
using AssetFlow.Editor.Importing;
using AssetFlow.Editor.Workflow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowConfigHandlerAssetTests
    {
        private const string TestFolder = "Assets/AssetFlowConfigHandlerAssetTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder("Assets", "AssetFlowConfigHandlerAssetTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestFolder);
        }

        [Test]
        public void AddHandlerAsSubAsset_AddsReferenceAndSubAsset()
        {
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            var processor = ScriptableObject.CreateInstance<ConfigHandlerAssetTestPostProcessor>();

            config.AddHandlerAsSubAsset(processor);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);

            Assert.That(config.PostImportProcessors, Has.Some.SameAs(processor));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.Some.SameAs(processor));
        }

        [Test]
        public void RemoveHandlerAndSubAsset_RemovesReferenceAndSubAsset()
        {
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            var processor = ScriptableObject.CreateInstance<ConfigHandlerAssetTestPostProcessor>();
            config.AddHandlerAsSubAsset(processor);
            AssetDatabase.SaveAssets();

            config.RemoveHandlerAndSubAsset(processor);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);

            Assert.That(config.PostImportProcessors, Has.None.SameAs(processor));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.None.SameAs(processor));
        }

        [Test]
        public void RemovePresetProcessor_RemovesPresetSubAsset()
        {
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            var processor = (ApplyTextureImporterPresetProcessor)config.PreImportProcessors[0];
            var preset = processor.Preset;

            config.RemoveHandlerAndSubAsset(processor);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(configPath);

            Assert.That(config.PreImportProcessors, Has.None.SameAs(processor));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.None.SameAs(processor));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.None.SameAs(preset));
        }
    }

    public sealed class ConfigHandlerAssetTestPostProcessor : AssetFlowPostImportProcessor<Texture2D>
    {
        [SerializeField] private int threshold = 16;

        public int Threshold => threshold;

        public override void Process(Texture2D asset, AssetFlowPostImportContext context)
        {
        }
    }
}
