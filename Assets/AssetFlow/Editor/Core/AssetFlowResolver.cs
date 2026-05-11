using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetFlow.Editor.Core
{
    public sealed class AssetFlowResolver
    {
        private readonly Dictionary<string, FolderTypeEntry> entriesByFolder;

        public AssetFlowResolver(IEnumerable<AssetFlowConfigSnapshot> configs)
        {
            entriesByFolder = BuildEntries(configs ?? Enumerable.Empty<AssetFlowConfigSnapshot>());
        }

        public AssetFlowResolveResult Resolve(string assetPath, string typeKey)
        {
            var normalizedAssetPath = AssetFlowPath.Normalize(assetPath);
            if (string.IsNullOrEmpty(normalizedAssetPath) || string.IsNullOrEmpty(typeKey))
                return AssetFlowResolveResult.Unmanaged();

            var folder = AssetFlowPath.GetParentFolder(normalizedAssetPath);
            var candidates = entriesByFolder.Values
                .Where(entry => string.Equals(entry.TypeKey, typeKey, StringComparison.Ordinal)
                                && AssetFlowPath.IsInFolder(normalizedAssetPath, entry.FolderPath, includeSubfolders: true))
                .OrderByDescending(entry => AssetFlowPath.Depth(entry.FolderPath))
                .ToList();

            foreach (var candidate in candidates)
            {
                if (candidate.IsConflict)
                {
                    if (string.Equals(folder, candidate.FolderPath, StringComparison.OrdinalIgnoreCase))
                        return AssetFlowResolveResult.Conflict(candidate.Configs);

                    if (AssetFlowPath.IsDescendantOf(folder, candidate.FolderPath))
                    {
                        var deeperBoundaryExists = candidates.Any(other =>
                            other != candidate
                            && AssetFlowPath.Depth(other.FolderPath) > AssetFlowPath.Depth(candidate.FolderPath)
                            && AssetFlowPath.IsInFolder(normalizedAssetPath, other.FolderPath, includeSubfolders: true));

                        if (!deeperBoundaryExists)
                            return AssetFlowResolveResult.Unmanaged();
                    }

                    continue;
                }

                var config = candidate.Configs[0];
                if (AssetFlowPath.IsInFolder(normalizedAssetPath, config.FolderPath, config.IncludeSubfolders))
                    return AssetFlowResolveResult.Managed(config);

                if (AssetFlowPath.IsDescendantOf(folder, config.FolderPath))
                    return AssetFlowResolveResult.Unmanaged();
            }

            return AssetFlowResolveResult.Unmanaged();
        }

        private static Dictionary<string, FolderTypeEntry> BuildEntries(IEnumerable<AssetFlowConfigSnapshot> configs)
        {
            return configs
                .Where(config => config.IsValid)
                .GroupBy(config => Key(config.FolderPath, config.TypeKey), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => new FolderTypeEntry(group.First().FolderPath, group.First().TypeKey, group.ToList()),
                    StringComparer.OrdinalIgnoreCase);
        }

        private static string Key(string folderPath, string typeKey)
        {
            return $"{folderPath}|{typeKey}";
        }

        private sealed class FolderTypeEntry
        {
            public FolderTypeEntry(string folderPath, string typeKey, List<AssetFlowConfigSnapshot> configs)
            {
                FolderPath = folderPath;
                TypeKey = typeKey;
                Configs = configs;
            }

            public string FolderPath { get; }

            public string TypeKey { get; }

            public List<AssetFlowConfigSnapshot> Configs { get; }

            public bool IsConflict => Configs.Count > 1;
        }
    }
}
