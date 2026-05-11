using AssetFlow.Editor.Importing;
using AssetFlow.Editor.Workflow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowPipelineTests
    {
        [Test]
        public void RunPostImportAndValidation_ContinuesAfterHandlerException()
        {
            var config = ScriptableObject.CreateInstance<AssetFlowTextureConfig>();
            var throwing = ScriptableObject.CreateInstance<ThrowingPostProcessor>();
            var succeeding = ScriptableObject.CreateInstance<CountingPostProcessor>();
            var validator = ScriptableObject.CreateInstance<CountingValidator>();
            var texture = new Texture2D(1, 1);

            try
            {
                config.AddPostImportProcessorForTests(throwing);
                config.AddPostImportProcessorForTests(succeeding);
                config.AddValidatorForTests(validator);

                var report = AssetFlowPipeline.RunPostImportAndValidation(
                    "Assets/icon.png",
                    config,
                    new Object[] { texture });

                Assert.That(succeeding.Count, Is.EqualTo(1));
                Assert.That(validator.Count, Is.EqualTo(1));
                Assert.That(report.Errors, Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(throwing);
                Object.DestroyImmediate(succeeding);
                Object.DestroyImmediate(validator);
            }
        }

        [Test]
        public void RunPostImportAndValidation_FiltersAssetsByProcessorType()
        {
            var config = ScriptableObject.CreateInstance<AssetFlowTextureConfig>();
            var textureProcessor = ScriptableObject.CreateInstance<CountingPostProcessor>();
            var materialProcessor = ScriptableObject.CreateInstance<CountingMaterialPostProcessor>();
            var texture = new Texture2D(1, 1);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            try
            {
                config.AddPostImportProcessorForTests(textureProcessor);
                config.AddPostImportProcessorForTests(materialProcessor);

                AssetFlowPipeline.RunPostImportAndValidation(
                    "Assets/model.fbx",
                    config,
                    new Object[] { texture, material });

                Assert.That(textureProcessor.Count, Is.EqualTo(1));
                Assert.That(materialProcessor.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(textureProcessor);
                Object.DestroyImmediate(materialProcessor);
            }
        }

        [Test]
        public void RunPostImportAndValidation_CollectsIssuesReturnedByValidators()
        {
            var config = ScriptableObject.CreateInstance<AssetFlowTextureConfig>();
            var validator = ScriptableObject.CreateInstance<ReturningValidator>();
            var texture = new Texture2D(1, 1);

            try
            {
                config.AddValidatorForTests(validator);

                var report = AssetFlowPipeline.RunPostImportAndValidation(
                    "Assets/icon.png",
                    config,
                    new Object[] { texture });

                Assert.That(report.Issues, Has.Some.Matches<AssetFlowIssue>(
                    issue => issue.Severity == AssetFlowIssueSeverity.Warning && issue.Message == "small texture"));
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(validator);
            }
        }

        private sealed class ThrowingPostProcessor : AssetFlowPostImportProcessor<Texture2D>
        {
            public override void Process(Texture2D asset, AssetFlowPostImportContext context)
            {
                throw new System.InvalidOperationException("boom");
            }
        }

        private sealed class CountingPostProcessor : AssetFlowPostImportProcessor<Texture2D>
        {
            public int Count { get; private set; }

            public override void Process(Texture2D asset, AssetFlowPostImportContext context)
            {
                Count++;
            }
        }

        private sealed class CountingMaterialPostProcessor : AssetFlowPostImportProcessor<Material>
        {
            public int Count { get; private set; }

            public override void Process(Material asset, AssetFlowPostImportContext context)
            {
                Count++;
            }
        }

        private sealed class CountingValidator : AssetFlowValidator<Texture2D>
        {
            public int Count { get; private set; }

            public override System.Collections.Generic.IEnumerable<AssetFlowIssue> Validate(Texture2D asset, AssetFlowValidationContext context)
            {
                Count++;
                return System.Array.Empty<AssetFlowIssue>();
            }
        }

        private sealed class ReturningValidator : AssetFlowValidator<Texture2D>
        {
            public override System.Collections.Generic.IEnumerable<AssetFlowIssue> Validate(Texture2D asset, AssetFlowValidationContext context)
            {
                yield return new AssetFlowIssue(AssetFlowIssueSeverity.Warning, "small texture");
            }
        }
    }
}
