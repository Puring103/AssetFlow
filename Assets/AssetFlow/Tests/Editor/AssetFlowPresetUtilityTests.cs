using System.IO;
using AssetFlow.Editor.Importing;
using AssetFlow.Editor.Workflow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowPresetUtilityTests
    {
        private const string TestFolder = "Assets/AssetFlowPresetTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder("Assets", "AssetFlowPresetTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestFolder);
        }

        [Test]
        public void CaptureFromAsset_CreatesPresetSubAssetOnPresetProcessor()
        {
            var texture = new Texture2D(2, 2);
            var texturePath = $"{TestFolder}/sample.png";
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);

            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);

            Assert.That(AssetFlowPresetUtility.CaptureFromAsset(config, texturePath), Is.True);

            var processor = (ApplyTextureImporterPresetProcessor)config.PreImportProcessors[0];
            Assert.That(processor.Preset, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.Some.SameAs(processor.Preset));
        }

        [Test]
        public void ClearPreset_RemovesPresetReferenceAndSubAsset()
        {
            var texture = new Texture2D(2, 2);
            var texturePath = $"{TestFolder}/clear.png";
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);

            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            AssetFlowPresetUtility.CaptureFromAsset(config, texturePath);
            var processor = (ApplyTextureImporterPresetProcessor)config.PreImportProcessors[0];
            var preset = processor.Preset;

            Assert.That(AssetFlowPresetUtility.ClearPreset(config), Is.True);

            Assert.That(processor.Preset, Is.Null);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.None.SameAs(preset));
        }
    }
}
