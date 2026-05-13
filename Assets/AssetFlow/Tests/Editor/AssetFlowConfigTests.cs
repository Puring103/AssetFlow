using AssetFlow.Editor.Workflow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowConfigTests
    {
        [Test]
        public void NewTextureConfig_HasOneTemplateProcessorAtTheStart()
        {
            var config = ScriptableObject.CreateInstance<AssetFlowTextureConfig>();
            try
            {
                config.ResetToDefaultsForTests();

                Assert.That(config.TypeKey, Is.EqualTo(typeof(TextureImporter).FullName));
                Assert.That(config.IncludeSubfolders, Is.False);
                Assert.That(config.PreImportProcessors, Has.Count.EqualTo(1));
                Assert.That(config.PreImportProcessors[0], Is.TypeOf<ApplyTextureImporterPresetProcessor>());
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void EnsureSinglePresetProcessor_RemovesDuplicateTemplateProcessors()
        {
            var config = ScriptableObject.CreateInstance<AssetFlowTextureConfig>();
            try
            {
                config.ResetToDefaultsForTests();
                var first = config.PreImportProcessors[0];
                config.AddPreImportProcessorForTests(ScriptableObject.CreateInstance<ApplyTextureImporterPresetProcessor>());

                config.EnsureSinglePresetProcessor();

                Assert.That(config.PreImportProcessors, Has.Count.EqualTo(1));
                Assert.That(config.PreImportProcessors[0], Is.SameAs(first));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void RuleHash_ChangesWhenProcessorSettingsChange()
        {
            var config = ScriptableObject.CreateInstance<AssetFlowTextureConfig>();
            try
            {
                config.ResetToDefaultsForTests();
                var first = config.ComputeRuleHash();

                var processor = (ApplyTextureImporterPresetProcessor)config.PreImportProcessors[0];
                processor.SetVersionSaltForTests("changed");
                var second = config.ComputeRuleHash();

                Assert.That(second, Is.Not.EqualTo(first));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
