using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace AssetFlow.Editor.Workflow
{
    public abstract class AssetFlowBuiltInValidator<TAsset> : AssetFlowValidator<TAsset>
        where TAsset : UnityEngine.Object
    {
        [SerializeField] private AssetFlowIssueSeverity severity = AssetFlowIssueSeverity.Warning;

        protected AssetFlowIssue Issue(string message)
        {
            return new AssetFlowIssue(severity, message);
        }
    }

    public abstract class AssetFlowMainAssetValidator : AssetFlowValidator
    {
        [SerializeField] private AssetFlowIssueSeverity severity = AssetFlowIssueSeverity.Warning;

        public override Type AssetType => typeof(UnityEngine.Object);

        public sealed override IEnumerable<AssetFlowIssue> Validate(UnityEngine.Object asset, AssetFlowValidationContext context)
        {
            if (asset == null || context == null)
                yield break;

            var mainAsset = AssetDatabase.LoadMainAssetAtPath(context.AssetPath);
            if (mainAsset != null && mainAsset != asset)
                yield break;

            foreach (var issue in ValidateMainAsset(asset, context))
                yield return issue;
        }

        protected abstract IEnumerable<AssetFlowIssue> ValidateMainAsset(UnityEngine.Object asset, AssetFlowValidationContext context);

        protected AssetFlowIssue Issue(string message)
        {
            return new AssetFlowIssue(severity, message);
        }
    }

    public sealed class NameValidator : AssetFlowMainAssetValidator
    {
        public enum NameTarget
        {
            FileNameWithoutExtension,
            FileNameWithExtension,
            AssetName,
        }

        [SerializeField] private NameTarget target = NameTarget.FileNameWithoutExtension;
        [SerializeField] private string prefix = string.Empty;
        [SerializeField] private string suffix = string.Empty;
        [SerializeField] private string pattern = string.Empty;
        [SerializeField] private bool caseSensitive = true;

        public override string Summary
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(prefix))
                    parts.Add($"prefix \"{prefix}\"");
                if (!string.IsNullOrEmpty(suffix))
                    parts.Add($"suffix \"{suffix}\"");
                if (!string.IsNullOrEmpty(pattern))
                    parts.Add($"matches {pattern}");

                return parts.Count == 0 ? "Name: no constraints" : $"Name: {string.Join(", ", parts)}";
            }
        }

        protected override IEnumerable<AssetFlowIssue> ValidateMainAsset(UnityEngine.Object asset, AssetFlowValidationContext context)
        {
            var candidate = GetCandidateName(asset, context.AssetPath);
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            if (!string.IsNullOrEmpty(prefix) && !candidate.StartsWith(prefix, comparison))
                yield return Issue($"Name '{candidate}' must start with '{prefix}'.");

            if (!string.IsNullOrEmpty(suffix) && !candidate.EndsWith(suffix, comparison))
                yield return Issue($"Name '{candidate}' must end with '{suffix}'.");

            if (string.IsNullOrEmpty(pattern))
                yield break;

            Regex regex;
            string regexError = null;
            try
            {
                var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                regex = new Regex(pattern, options);
            }
            catch (ArgumentException exception)
            {
                regex = null;
                regexError = exception.Message;
            }

            if (regex == null)
            {
                yield return Issue($"Name pattern is invalid: {regexError}");
                yield break;
            }

            if (!regex.IsMatch(candidate))
                yield return Issue($"Name '{candidate}' must match pattern '{pattern}'.");
        }

        private string GetCandidateName(UnityEngine.Object asset, string assetPath)
        {
            switch (target)
            {
                case NameTarget.FileNameWithExtension:
                    return Path.GetFileName(assetPath);
                case NameTarget.AssetName:
                    return asset == null ? string.Empty : asset.name;
                default:
                    return Path.GetFileNameWithoutExtension(assetPath);
            }
        }
    }

    public sealed class FileExtensionValidator : AssetFlowMainAssetValidator
    {
        [SerializeField] private List<string> allowedExtensions = new List<string>();

        public override string Summary
        {
            get
            {
                return allowedExtensions == null || allowedExtensions.Count == 0
                    ? "Extensions: none configured"
                    : $"Extensions: {string.Join(", ", NormalizedExtensions())}";
            }
        }

        protected override IEnumerable<AssetFlowIssue> ValidateMainAsset(UnityEngine.Object asset, AssetFlowValidationContext context)
        {
            var allowed = NormalizedExtensions().ToList();
            if (allowed.Count == 0)
                yield break;

            var extension = NormalizeExtension(Path.GetExtension(context.AssetPath));
            if (!allowed.Contains(extension, StringComparer.OrdinalIgnoreCase))
                yield return Issue($"Extension '{extension}' is not allowed. Allowed: {string.Join(", ", allowed)}.");
        }

        private IEnumerable<string> NormalizedExtensions()
        {
            return (allowedExtensions ?? new List<string>())
                .Where(extension => !string.IsNullOrWhiteSpace(extension))
                .Select(NormalizeExtension)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeExtension(string extension)
        {
            extension = (extension ?? string.Empty).Trim();
            if (extension.Length == 0)
                return extension;

            return extension[0] == '.' ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
        }
    }

    public sealed class FileSizeValidator : AssetFlowMainAssetValidator
    {
        public enum SizeUnit
        {
            Bytes,
            KB,
            MB,
            GB,
        }

        [SerializeField] private float maxSize = 1f;
        [SerializeField] private SizeUnit unit = SizeUnit.MB;

        public override string Summary => $"File size <= {maxSize:g} {unit}";

        protected override IEnumerable<AssetFlowIssue> ValidateMainAsset(UnityEngine.Object asset, AssetFlowValidationContext context)
        {
            if (maxSize <= 0f || string.IsNullOrEmpty(context.AssetPath) || !File.Exists(context.AssetPath))
                yield break;

            var maxBytes = maxSize * UnitMultiplier(unit);
            var fileSize = new FileInfo(context.AssetPath).Length;
            if (fileSize > maxBytes)
                yield return Issue($"File size is {FormatBytes(fileSize)}, expected <= {maxSize:g} {unit}.");
        }

        private static double UnitMultiplier(SizeUnit sizeUnit)
        {
            switch (sizeUnit)
            {
                case SizeUnit.KB:
                    return 1024d;
                case SizeUnit.MB:
                    return 1024d * 1024d;
                case SizeUnit.GB:
                    return 1024d * 1024d * 1024d;
                default:
                    return 1d;
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L)
                return $"{bytes / (1024d * 1024d * 1024d):0.##} GB";
            if (bytes >= 1024L * 1024L)
                return $"{bytes / (1024d * 1024d):0.##} MB";
            if (bytes >= 1024L)
                return $"{bytes / 1024d:0.##} KB";
            return $"{bytes} Bytes";
        }
    }

    public sealed class TextureSizeValidator : AssetFlowBuiltInValidator<Texture2D>
    {
        [SerializeField] private int maxWidth = 2048;
        [SerializeField] private int maxHeight = 2048;

        public override string Summary => $"Texture size <= {maxWidth}x{maxHeight}";

        public override IEnumerable<AssetFlowIssue> Validate(Texture2D asset, AssetFlowValidationContext context)
        {
            if (asset == null || maxWidth <= 0 || maxHeight <= 0)
                yield break;

            var width = asset.width;
            var height = asset.height;
            if (AssetImporter.GetAtPath(context.AssetPath) is TextureImporter importer)
                importer.GetSourceTextureWidthAndHeight(out width, out height);

            if (width > maxWidth || height > maxHeight)
                yield return Issue($"Texture size is {width}x{height}, expected <= {maxWidth}x{maxHeight}.");
        }
    }

    public sealed class TextureAlphaValidator : AssetFlowBuiltInValidator<Texture2D>
    {
        public enum AlphaMode
        {
            Allow,
            Require,
            Forbid,
        }

        [SerializeField] private AlphaMode mode = AlphaMode.Forbid;

        public override string Summary => $"Alpha: {mode.ToString().ToLowerInvariant()}";

        public override IEnumerable<AssetFlowIssue> Validate(Texture2D asset, AssetFlowValidationContext context)
        {
            if (asset == null || mode == AlphaMode.Allow)
                yield break;

            var hasAlpha = HasAlpha(asset, context.AssetPath);
            if (mode == AlphaMode.Require && !hasAlpha)
                yield return Issue("Texture must have an alpha channel.");
            else if (mode == AlphaMode.Forbid && hasAlpha)
                yield return Issue("Texture must not have an alpha channel.");
        }

        private static bool HasAlpha(Texture2D texture, string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
                return importer.DoesSourceTextureHaveAlpha();

            return GraphicsFormatUtility.HasAlphaChannel(texture.graphicsFormat);
        }
    }

    public sealed class MeshComplexityValidator : AssetFlowBuiltInValidator<Mesh>
    {
        [SerializeField] private int maxTriangles = 5000;
        [SerializeField] private int maxVertices = 3000;

        public override string Summary => $"Mesh: triangles <= {maxTriangles}, vertices <= {maxVertices}";

        public override IEnumerable<AssetFlowIssue> Validate(Mesh asset, AssetFlowValidationContext context)
        {
            if (asset == null)
                yield break;

            var triangles = CountTriangles(asset);
            if (maxTriangles > 0 && triangles > maxTriangles)
                yield return Issue($"Mesh '{asset.name}' has {triangles} triangles, expected <= {maxTriangles}.");

            if (maxVertices > 0 && asset.vertexCount > maxVertices)
                yield return Issue($"Mesh '{asset.name}' has {asset.vertexCount} vertices, expected <= {maxVertices}.");
        }

        private static long CountTriangles(Mesh mesh)
        {
            long triangles = 0;
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetTopology(i) == MeshTopology.Triangles)
                    triangles += (long)mesh.GetIndexCount(i) / 3L;
            }

            return triangles;
        }
    }

    public sealed class AudioLengthValidator : AssetFlowBuiltInValidator<AudioClip>
    {
        [SerializeField] private float maxSeconds = 10f;

        public override string Summary => $"Audio length <= {maxSeconds:g}s";

        public override IEnumerable<AssetFlowIssue> Validate(AudioClip asset, AssetFlowValidationContext context)
        {
            if (asset == null || maxSeconds <= 0f)
                yield break;

            if (asset.length > maxSeconds)
                yield return Issue($"Audio length is {asset.length:0.##}s, expected <= {maxSeconds:g}s.");
        }
    }

    public sealed class AudioPeakValidator : AssetFlowBuiltInValidator<AudioClip>
    {
        [SerializeField] private float maxPeakDb = -1f;

        public override string Summary => $"Peak <= {maxPeakDb:g} dB";

        public override IEnumerable<AssetFlowIssue> Validate(AudioClip asset, AssetFlowValidationContext context)
        {
            if (asset == null || asset.samples <= 0 || asset.channels <= 0)
                yield break;

            if (!TryFindPeak(asset, out var peak))
                yield break;

            var peakDb = peak <= 0f ? float.NegativeInfinity : 20f * Mathf.Log10(peak);
            if (peakDb > maxPeakDb)
                yield return Issue($"Audio peak is {peakDb:0.##} dB, expected <= {maxPeakDb:g} dB.");
        }

        private static bool TryFindPeak(AudioClip clip, out float peak)
        {
            peak = 0f;
            const int ChunkSamples = 16384;
            var channels = clip.channels;
            var offset = 0;

            while (offset < clip.samples)
            {
                var frames = Mathf.Min(ChunkSamples, clip.samples - offset);
                var buffer = new float[frames * channels];
                if (!clip.GetData(buffer, offset))
                    return false;

                for (var i = 0; i < buffer.Length; i++)
                    peak = Mathf.Max(peak, Mathf.Abs(buffer[i]));

                offset += frames;
            }

            return true;
        }
    }
}
