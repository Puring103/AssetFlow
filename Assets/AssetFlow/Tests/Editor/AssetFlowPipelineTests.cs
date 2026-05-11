using AssetFlow.Editor.Importing;
using AssetFlow.Editor.Workflow;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowPipelineTests
    {
        private const string TestFolder = "Assets/AssetFlowPipelineGeneratedTests";
        private const string TexturePath = TestFolder + "/PipelineTexture.png";

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

        [Test]
        public void RunPreImport_FiltersProcessorsByImporterTypeAndCollectsContextIssues()
        {
            var config = ScriptableObject.CreateInstance<AssetFlowTextureConfig>();
            var textureProcessor = ScriptableObject.CreateInstance<ReportingTexturePreProcessor>();
            var modelProcessor = ScriptableObject.CreateInstance<CountingModelPreProcessor>();

            try
            {
                config.ResetToDefaultsForTests();
                config.AddPreImportProcessorForTests(textureProcessor);
                config.AddPreImportProcessorForTests(modelProcessor);
                var importer = CreateTextureImporter();

                var report = AssetFlowPipeline.RunPreImport(TexturePath, config, importer);

                Assert.That(textureProcessor.Count, Is.EqualTo(1));
                Assert.That(modelProcessor.Count, Is.EqualTo(0));
                Assert.That(report.Issues, Has.Some.Matches<AssetFlowIssue>(
                    issue => issue.Severity == AssetFlowIssueSeverity.Warning && issue.Message == "pre-import warning"));
            }
            finally
            {
                DeleteGeneratedTextureFolder();
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(textureProcessor);
                Object.DestroyImmediate(modelProcessor);
            }
        }

        [Test]
        public void RunPreImport_ContinuesAfterProcessorException()
        {
            var config = ScriptableObject.CreateInstance<AssetFlowTextureConfig>();
            var throwing = ScriptableObject.CreateInstance<ThrowingTexturePreProcessor>();
            var succeeding = ScriptableObject.CreateInstance<ReportingTexturePreProcessor>();

            try
            {
                config.ResetToDefaultsForTests();
                config.AddPreImportProcessorForTests(throwing);
                config.AddPreImportProcessorForTests(succeeding);
                var importer = CreateTextureImporter();

                var report = AssetFlowPipeline.RunPreImport(TexturePath, config, importer);

                Assert.That(succeeding.Count, Is.EqualTo(1));
                Assert.That(report.Errors, Has.Count.EqualTo(1));
                Assert.That(report.HasErrors, Is.True);
            }
            finally
            {
                DeleteGeneratedTextureFolder();
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(throwing);
                Object.DestroyImmediate(succeeding);
            }
        }

        [Test]
        public void RunPostImportAndValidation_ReportsErrorWhenLoopGuardPausesHandler()
        {
            var config = ScriptableObject.CreateInstance<AssetFlowTextureConfig>();
            var processor = ScriptableObject.CreateInstance<CountingPostProcessor>();
            var texture = new Texture2D(1, 1);
            var loopGuard = new AssetFlowLoopGuard(threshold: 1);

            try
            {
                config.AddPostImportProcessorForTests(processor);

                AssetFlowPipeline.RunPostImportAndValidation(
                    "Assets/icon.png",
                    "asset",
                    config,
                    new Object[] { texture },
                    loopGuard,
                    "chain");
                var blocked = AssetFlowPipeline.RunPostImportAndValidation(
                    "Assets/icon.png",
                    "asset",
                    config,
                    new Object[] { texture },
                    loopGuard,
                    "chain");

                Assert.That(processor.Count, Is.EqualTo(1));
                Assert.That(blocked.HasErrors, Is.True);
                Assert.That(blocked.Errors[0], Does.Contain("AssetFlow import loop detected"));
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(processor);
            }
        }

        private sealed class ThrowingPostProcessor : AssetFlowPostImportProcessor<Texture2D>
        {
            public override void Process(Texture2D asset, AssetFlowPostImportContext context)
            {
                throw new System.InvalidOperationException("boom");
            }
        }

        private sealed class ThrowingTexturePreProcessor : AssetFlowPreImportProcessor<TextureImporter>
        {
            public override void Process(TextureImporter importer, AssetFlowPreImportContext context)
            {
                throw new System.InvalidOperationException("pre boom");
            }
        }

        private sealed class ReportingTexturePreProcessor : AssetFlowPreImportProcessor<TextureImporter>
        {
            public int Count { get; private set; }

            public override void Process(TextureImporter importer, AssetFlowPreImportContext context)
            {
                Count++;
                context.ReportWarning("pre-import warning");
            }
        }

        private sealed class CountingModelPreProcessor : AssetFlowPreImportProcessor<ModelImporter>
        {
            public int Count { get; private set; }

            public override void Process(ModelImporter importer, AssetFlowPreImportContext context)
            {
                Count++;
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

        private static TextureImporter CreateTextureImporter()
        {
            DeleteGeneratedTextureFolder();
            AssetDatabase.CreateFolder("Assets", "AssetFlowPipelineGeneratedTests");

            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            try
            {
                System.IO.File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(TexturePath);
                return (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static void DeleteGeneratedTextureFolder()
        {
            if (AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.DeleteAsset(TestFolder);
        }
    }
}
