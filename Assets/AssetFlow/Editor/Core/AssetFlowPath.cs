using System;

namespace AssetFlow.Editor.Core
{
    public static class AssetFlowPath
    {
        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            return path.Replace('\\', '/').TrimEnd('/');
        }

        public static string NormalizeFolder(string folderPath)
        {
            return Normalize(folderPath);
        }

        public static string GetParentFolder(string assetPath)
        {
            var normalized = Normalize(assetPath);
            var index = normalized.LastIndexOf('/');
            return index < 0 ? string.Empty : normalized.Substring(0, index);
        }

        public static bool IsInFolder(string assetPath, string folderPath, bool includeSubfolders)
        {
            var normalizedAsset = Normalize(assetPath);
            var normalizedFolder = NormalizeFolder(folderPath);

            if (string.IsNullOrEmpty(normalizedAsset) || string.IsNullOrEmpty(normalizedFolder))
                return false;

            var parent = GetParentFolder(normalizedAsset);
            if (string.Equals(parent, normalizedFolder, StringComparison.OrdinalIgnoreCase))
                return true;

            return includeSubfolders && IsDescendantOf(parent, normalizedFolder);
        }

        public static bool IsDescendantOf(string candidateFolder, string ancestorFolder)
        {
            var candidate = NormalizeFolder(candidateFolder);
            var ancestor = NormalizeFolder(ancestorFolder);

            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(ancestor))
                return false;

            return candidate.StartsWith(ancestor + "/", StringComparison.OrdinalIgnoreCase);
        }

        public static int Depth(string folderPath)
        {
            var normalized = NormalizeFolder(folderPath);
            if (string.IsNullOrEmpty(normalized))
                return 0;

            var depth = 1;
            for (var i = 0; i < normalized.Length; i++)
            {
                if (normalized[i] == '/')
                    depth++;
            }

            return depth;
        }
    }
}
