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
        public void CaptureFromAsset_AssignsTemplateImporterOnTemplateProcessor()
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
            Assert.That(processor.TemplateImporter, Is.SameAs(AssetImporter.GetAtPath(texturePath)));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.None.SameAs(processor.TemplateImporter));
        }

        [Test]
        public void CaptureFromAsset_ReplacesExistingTemplateImporterReference()
        {
            var firstTexture = new Texture2D(2, 2);
            var firstTexturePath = $"{TestFolder}/first.png";
            File.WriteAllBytes(firstTexturePath, firstTexture.EncodeToPNG());
            Object.DestroyImmediate(firstTexture);
            AssetDatabase.ImportAsset(firstTexturePath);

            var secondTexture = new Texture2D(2, 2);
            var secondTexturePath = $"{TestFolder}/second.png";
            File.WriteAllBytes(secondTexturePath, secondTexture.EncodeToPNG());
            Object.DestroyImmediate(secondTexture);
            AssetDatabase.ImportAsset(secondTexturePath);

            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            Assert.That(AssetFlowPresetUtility.CaptureFromAsset(config, firstTexturePath), Is.True);
            var processor = (ApplyTextureImporterPresetProcessor)config.PreImportProcessors[0];
            var firstImporter = processor.TemplateImporter;

            Assert.That(AssetFlowPresetUtility.CaptureFromAsset(config, secondTexturePath), Is.True);

            Assert.That(processor.TemplateImporter, Is.Not.Null);
            Assert.That(processor.TemplateImporter, Is.Not.SameAs(firstImporter));
            Assert.That(processor.TemplateImporter, Is.SameAs(AssetImporter.GetAtPath(secondTexturePath)));
        }

        [Test]
        public void CreateTextureConfig_CreatesDefaultTemplateProcessorWithAutoTemplateImporter()
        {
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            var processor = (ApplyTextureImporterPresetProcessor)config.PreImportProcessors[0];

            Assert.That(processor.TemplateImporter, Is.Not.Null);
            Assert.That(processor.TemplateImporter, Is.TypeOf<TextureImporter>());
            Assert.That(processor.TemplateImporter.assetPath, Is.EqualTo($"{TestFolder}/AssetFlow.Template.Texture.png"));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.Some.SameAs(processor));
        }

        [Test]
        public void EnsureTemplateImporter_RecreatesMissingAutoTemplateImporter()
        {
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            var processor = (ApplyTextureImporterPresetProcessor)config.PreImportProcessors[0];
            var templatePath = processor.TemplateImporter.assetPath;

            Assert.That(AssetDatabase.DeleteAsset(templatePath), Is.True);
            Assert.That(AssetFlowPresetUtility.EnsureTemplateImporter(config), Is.True);

            Assert.That(processor.TemplateImporter, Is.Not.Null);
            Assert.That(processor.TemplateImporter.assetPath, Is.EqualTo(templatePath));
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(templatePath), Is.Not.Null);
        }

        [Test]
        public void ClearPreset_RemovesTemplateImporterReference()
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

            Assert.That(AssetFlowPresetUtility.ClearPreset(config), Is.True);

            Assert.That(processor.TemplateImporter, Is.Null);
        }
    }
}
