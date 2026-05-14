using System;
using System.Collections.Generic;
using System.Reflection;
using AssetFlow.Editor.Importing;
using AssetFlow.Editor.Workflow;
using NUnit.Framework;
using UnityEngine;

namespace AssetFlow.Editor.Tests
{
    public sealed class BuiltInValidatorTests
    {
        [Test]
        public void NameValidator_SummaryDescribesConfiguredConstraints()
        {
            var validator = ScriptableObject.CreateInstance<NameValidator>();
            try
            {
                SetField(validator, "prefix", "ui_");
                SetField(validator, "suffix", "_icon");

                Assert.That(validator.Summary, Is.EqualTo("Name: prefix \"ui_\", suffix \"_icon\""));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(validator);
            }
        }

        [Test]
        public void FileExtensionValidator_ReportsDisallowedExtension()
        {
            var validator = ScriptableObject.CreateInstance<FileExtensionValidator>();
            var config = ScriptableObject.CreateInstance<AssetFlowTextureConfig>();
            var asset = ScriptableObject.CreateInstance<ScriptableObject>();
            try
            {
                SetField(validator, "allowedExtensions", new List<string> { ".png" });

                var issues = validator.Validate(asset, new AssetFlowValidationContext("Assets/Test/example.jpg", config));

                Assert.That(issues, Has.Some.Matches<AssetFlowIssue>(issue => issue.Message.Contains(".jpg")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(validator);
            }
        }

        [Test]
        public void TextureSizeValidator_ReportsTextureLargerThanLimit()
        {
            var validator = ScriptableObject.CreateInstance<TextureSizeValidator>();
            var config = ScriptableObject.CreateInstance<AssetFlowTextureConfig>();
            var texture = new Texture2D(4, 2);
            try
            {
                SetField(validator, "maxWidth", 2);
                SetField(validator, "maxHeight", 2);

                var issues = validator.Validate(texture, new AssetFlowValidationContext("Assets/Test/texture.png", config));

                Assert.That(issues, Has.Some.Matches<AssetFlowIssue>(issue => issue.Message.Contains("4x2")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(validator);
            }
        }

        [Test]
        public void MeshComplexityValidator_ReportsTriangleAndVertexLimits()
        {
            var validator = ScriptableObject.CreateInstance<MeshComplexityValidator>();
            var config = ScriptableObject.CreateInstance<AssetFlowModelConfig>();
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                },
                triangles = new[] { 0, 1, 2 },
            };
            try
            {
                SetField(validator, "maxTriangles", 0);
                SetField(validator, "maxVertices", 2);

                var issues = validator.Validate(mesh, new AssetFlowValidationContext("Assets/Test/model.fbx", config));

                Assert.That(issues, Has.Some.Matches<AssetFlowIssue>(issue => issue.Message.Contains("vertices")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(validator);
            }
        }

        [Test]
        public void AudioLengthValidator_ReportsClipLongerThanLimit()
        {
            var validator = ScriptableObject.CreateInstance<AudioLengthValidator>();
            var config = ScriptableObject.CreateInstance<AssetFlowAudioConfig>();
            var clip = AudioClip.Create("long", 44100, 1, 44100, false);
            try
            {
                SetField(validator, "maxSeconds", 0.5f);

                var issues = validator.Validate(clip, new AssetFlowValidationContext("Assets/Test/audio.wav", config));

                Assert.That(issues, Has.Some.Matches<AssetFlowIssue>(issue => issue.Message.Contains("Audio length")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
                UnityEngine.Object.DestroyImmediate(config);
                UnityEngine.Object.DestroyImmediate(validator);
            }
        }

        private static void SetField<TTarget>(TTarget target, string fieldName, object value)
        {
            var field = typeof(TTarget).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException($"Missing field {fieldName} on {typeof(TTarget).Name}.");

            field.SetValue(target, value);
        }
    }
}
