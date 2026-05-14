using System.IO;
using AssetFlow.Editor.Importing;
using AssetFlow.Editor.Workflow;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Presets;
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
        public void CaptureFromAsset_AssignsPresetSubAssetOnTemplateProcessor()
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
            Assert.That(processor.TemplatePreset, Is.Not.Null);
            Assert.That(processor.TemplatePreset.name, Is.EqualTo("importor"));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.Some.SameAs(processor.TemplatePreset));
        }

        [Test]
        public void CaptureFromAsset_ReusesExistingPresetSubAsset()
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
            var firstPreset = processor.TemplatePreset;

            Assert.That(AssetFlowPresetUtility.CaptureFromAsset(config, secondTexturePath), Is.True);

            Assert.That(processor.TemplatePreset, Is.Not.Null);
            Assert.That(processor.TemplatePreset, Is.SameAs(firstPreset));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.Some.SameAs(firstPreset));
        }

        [Test]
        public void CreateTextureConfig_DoesNotCreateAutoTemplateAssetOrPreset()
        {
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            var processor = (ApplyTextureImporterPresetProcessor)config.PreImportProcessors[0];

            Assert.That(processor.TemplatePreset, Is.Null);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.None.TypeOf<Preset>());
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>($"{TestFolder}/AssetFlow.Template.Texture.png"), Is.Null);
        }

        [Test]
        public void EnsureTemplateImporter_MigratesLegacyTemplateImporterToPresetSubAsset()
        {
            var texture = new Texture2D(2, 2);
            var texturePath = $"{TestFolder}/legacy.png";
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);

            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            var processor = (ApplyTextureImporterPresetProcessor)config.PreImportProcessors[0];
            var legacyImporter = AssetImporter.GetAtPath(texturePath);

            processor.SetTemplatePreset(null);
            var field = typeof(ApplyTextureImporterPresetProcessor).BaseType?.GetField("templateImporter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(processor, legacyImporter);

            Assert.That(AssetFlowPresetUtility.EnsureTemplateImporter(config), Is.True);
            Assert.That(processor.TemplatePreset, Is.Not.Null);
            Assert.That(processor.LegacyTemplateImporter, Is.Null);
            Assert.That(processor.TemplatePreset.name, Is.EqualTo("importor"));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.Some.SameAs(processor.TemplatePreset));
        }

        [Test]
        public void ClearPreset_RemovesTemplatePresetReferenceAndSubAsset()
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
            var preset = processor.TemplatePreset;

            Assert.That(AssetFlowPresetUtility.ClearPreset(config), Is.True);

            Assert.That(processor.TemplatePreset, Is.Null);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.None.SameAs(preset));
        }
    }
}
