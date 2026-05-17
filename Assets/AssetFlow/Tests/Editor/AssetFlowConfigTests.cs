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
                Assert.That(config.PreImportProcessors[0], Is.TypeOf<ApplyTextureImporterTemplateProcessor>());
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void EnsureSingleTemplateProcessor_RemovesDuplicateTemplateProcessors()
        {
            var config = ScriptableObject.CreateInstance<AssetFlowTextureConfig>();
            try
            {
                config.ResetToDefaultsForTests();
                var first = config.PreImportProcessors[0];
                config.AddPreImportProcessorForTests(ScriptableObject.CreateInstance<ApplyTextureImporterTemplateProcessor>());

                config.EnsureSingleTemplateProcessor();

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

                var processor = (ApplyTextureImporterTemplateProcessor)config.PreImportProcessors[0];
                processor.SetVersionSaltForTests("changed");
                var second = config.ComputeRuleHash();

                Assert.That(second, Is.Not.EqualTo(first));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void RuleHash_IsStableForSameSerializedSettings()
        {
            var config = ScriptableObject.CreateInstance<AssetFlowTextureConfig>();
            try
            {
                config.ResetToDefaultsForTests();
                var processor = (ApplyTextureImporterTemplateProcessor)config.PreImportProcessors[0];
                processor.SetVersionSaltForTests("stable");

                var first = config.ComputeRuleHash();
                var second = config.ComputeRuleHash();

                Assert.That(second, Is.EqualTo(first));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
