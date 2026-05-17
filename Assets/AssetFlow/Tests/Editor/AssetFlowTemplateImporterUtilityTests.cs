using System.IO;
using AssetFlow.Editor.Importing;
using AssetFlow.Editor.Workflow;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowTemplateImporterUtilityTests
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
        public void CreateTextureConfig_AutomaticallyCreatesTemplateImporterFromExistingMatchingAsset()
        {
            var texture = new Texture2D(2, 2);
            var texturePath = $"{TestFolder}/sample.png";
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);

            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);

            var processor = (ApplyTextureImporterTemplateProcessor)config.PreImportProcessors[0];
            Assert.That(processor.TemplateImporter, Is.TypeOf<TextureImporter>());
            Assert.That(processor.TemplateImporter.name, Is.EqualTo("TemplateImporter"));
            Assert.That(processor.TemplatePreset, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.Some.SameAs(processor.TemplatePreset));
            Assert.That(AssetDatabase.IsSubAsset(processor.TemplatePreset), Is.True);
        }

        [Test]
        public void CaptureFromAsset_ReusesExistingTemplateImporterSubAsset()
        {
            var firstTexturePath = WriteTexture("first.png");
            var secondTexturePath = WriteTexture("second.png");

            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            Assert.That(AssetFlowTemplateImporterUtility.CaptureFromAsset(config, firstTexturePath), Is.True);
            var processor = (ApplyTextureImporterTemplateProcessor)config.PreImportProcessors[0];
            var firstTemplateImporter = processor.TemplateImporter;

            Assert.That(AssetFlowTemplateImporterUtility.CaptureFromAsset(config, secondTexturePath), Is.True);

            Assert.That(processor.TemplateImporter, Is.Not.Null);
            Assert.That(processor.TemplateImporter, Is.SameAs(firstTemplateImporter));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.Some.SameAs(processor.TemplatePreset));
        }

        [Test]
        public void CreateTextureConfig_CreatesTemplateImporterEvenWhenNoMatchingAssetExistsYet()
        {
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            var processor = (ApplyTextureImporterTemplateProcessor)config.PreImportProcessors[0];

            Assert.That(processor.TemplateImporter, Is.TypeOf<TextureImporter>());
            Assert.That(processor.TemplatePreset, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.Some.SameAs(processor.TemplatePreset));
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>($"{TestFolder}/AssetFlow.Template.Texture.png"), Is.Null);
        }

        [Test]
        public void CreateTextureConfig_IgnoresNestedMatchingAssetsWhenCreatingDefaultTemplateImporter()
        {
            var nestedFolder = $"{TestFolder}/Nested";
            AssetDatabase.CreateFolder(TestFolder, "Nested");
            var nestedTexturePath = WriteTexture("Nested/nested.png");
            var nestedImporter = (TextureImporter)AssetImporter.GetAtPath(nestedTexturePath);
            nestedImporter.textureType = TextureImporterType.Sprite;
            nestedImporter.SaveAndReimport();

            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            var processor = (ApplyTextureImporterTemplateProcessor)config.PreImportProcessors[0];
            var targetTexturePath = WriteTexture("target.png");
            var targetImporter = (TextureImporter)AssetImporter.GetAtPath(targetTexturePath);

            EditorUtility.CopySerialized(processor.TemplateImporter, targetImporter);

            Assert.That(processor.TemplateImporter, Is.TypeOf<TextureImporter>());
            Assert.That(processor.TemplatePreset, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.Some.SameAs(processor.TemplatePreset));
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>($"{TestFolder}/AssetFlow.Template.Texture.png"), Is.Null);
            Assert.That(targetImporter.textureType, Is.Not.EqualTo(TextureImporterType.Sprite));
        }

        [Test]
        public void RemoveLegacyPresetSubAssets_PreservesActiveTemplateImporter()
        {
            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            var processor = (ApplyTextureImporterTemplateProcessor)config.PreImportProcessors[0];
            var templatePreset = processor.TemplatePreset;

            Assert.That(AssetFlowTemplateImporterUtility.RemoveLegacyPresetSubAssets(config), Is.False);

            Assert.That(processor.TemplatePreset, Is.SameAs(templatePreset));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.Some.SameAs(templatePreset));
        }

        [Test]
        public void EnsureTemplateImporter_CopiesExternalImporterIntoConfigSubAsset()
        {
            var texturePath = WriteTexture("external-source.png");
            var externalImporter = AssetImporter.GetAtPath(texturePath);

            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            var processor = (ApplyTextureImporterTemplateProcessor)config.PreImportProcessors[0];
            processor.SetTemplateImporter(externalImporter);

            Assert.That(AssetFlowTemplateImporterUtility.EnsureTemplateImporter(config), Is.True);

            Assert.That(processor.TemplateImporter, Is.Not.Null);
            Assert.That(processor.TemplateImporter, Is.Not.SameAs(externalImporter));
            Assert.That(processor.TemplateImporter.name, Is.EqualTo("TemplateImporter"));
            Assert.That(processor.TemplatePreset, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(processor.TemplatePreset), Is.EqualTo(configPath));
            Assert.That(AssetDatabase.IsSubAsset(processor.TemplatePreset), Is.True);
        }

        [Test]
        public void EnsureTemplateImporter_MigratesLegacyPresetToTemplateImporterSubAsset()
        {
            var texturePath = WriteTexture("legacy.png");
            var importer = AssetImporter.GetAtPath(texturePath);
            var preset = new Preset(importer)
            {
                name = "LegacyPreset"
            };

            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            var config = AssetDatabase.LoadAssetAtPath<AssetFlowTextureConfig>(configPath);
            var processor = (ApplyTextureImporterTemplateProcessor)config.PreImportProcessors[0];
            processor.SetLegacyPresetForTests(preset);
            processor.SetTemplateImporter(null);

            Assert.That(AssetFlowTemplateImporterUtility.EnsureTemplateImporter(config), Is.True);
            Assert.That(processor.TemplateImporter, Is.TypeOf<TextureImporter>());
            Assert.That(processor.LegacyPreset, Is.Null);
            Assert.That(processor.TemplateImporter.name, Is.EqualTo("TemplateImporter"));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.Some.SameAs(processor.TemplatePreset));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(configPath), Has.None.SameAs(preset));
        }

        private static string WriteTexture(string relativePath)
        {
            var texture = new Texture2D(2, 2);
            var path = $"{TestFolder}/{relativePath}";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            return path;
        }
    }
}
