using System;
using System.Collections.Generic;
using System.Linq;
using AssetFlow.Editor.Core;
using UnityEditor;

namespace AssetFlow.Editor.UI
{
    internal sealed class AssetFlowManagerProjection
    {
        public AssetFlowManagerProjection(
            IReadOnlyDictionary<string, List<string>> managedAssetPathsByConfigGuid,
            IReadOnlyDictionary<string, int> outOfDateCountByConfigGuid,
            IReadOnlyDictionary<string, int> validationCountByConfigGuid,
            bool cacheNeedsReconcile,
            string signature)
        {
            ManagedAssetPathsByConfigGuid = managedAssetPathsByConfigGuid;
            OutOfDateCountByConfigGuid = outOfDateCountByConfigGuid;
            ValidationCountByConfigGuid = validationCountByConfigGuid;
            CacheNeedsReconcile = cacheNeedsReconcile;
            Signature = signature ?? string.Empty;
        }

        public IReadOnlyDictionary<string, List<string>> ManagedAssetPathsByConfigGuid { get; }

        public IReadOnlyDictionary<string, int> OutOfDateCountByConfigGuid { get; }

        public IReadOnlyDictionary<string, int> ValidationCountByConfigGuid { get; }

        public bool CacheNeedsReconcile { get; }

        public string Signature { get; }

        public static AssetFlowManagerProjection Build(AssetFlowIndex index, IReadOnlyList<AssetFlowConfigSnapshot> snapshots)
        {
            index = index ?? new AssetFlowIndex();
            snapshots = snapshots ?? Array.Empty<AssetFlowConfigSnapshot>();
            var pathsByConfig = FindManagedAssetPathsByConfig(index, snapshots, out var cacheNeedsReconcile);
            var outOfDate = snapshots.ToDictionary(
                snapshot => snapshot.ConfigGuid,
                snapshot => CountOutOfDate(pathsByConfig[snapshot.ConfigGuid], snapshot, index),
                StringComparer.OrdinalIgnoreCase);
            var validation = snapshots.ToDictionary(
                snapshot => snapshot.ConfigGuid,
                snapshot => index.ValidationResults.Count(record =>
                    string.Equals(record.configGuid, snapshot.ConfigGuid, StringComparison.OrdinalIgnoreCase)),
                StringComparer.OrdinalIgnoreCase);

            return new AssetFlowManagerProjection(
                pathsByConfig,
                outOfDate,
                validation,
                cacheNeedsReconcile,
                BuildCacheSignature(index, snapshots));
        }

        internal static Dictionary<string, List<string>> FindManagedAssetPathsByConfig(
            AssetFlowIndex index,
            IReadOnlyList<AssetFlowConfigSnapshot> snapshots,
            out bool cacheNeedsReconcile)
        {
            cacheNeedsReconcile = false;
            var resolver = new AssetFlowResolver(snapshots);
            var assetsByConfigGuid = snapshots.ToDictionary(
                snapshot => snapshot.ConfigGuid,
                _ => new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            foreach (var asset in index.Assets)
            {
                if (string.IsNullOrEmpty(asset.assetGuid))
                    continue;

                var currentPath = AssetDatabase.GUIDToAssetPath(asset.assetGuid);
                if (string.IsNullOrEmpty(currentPath))
                {
                    cacheNeedsReconcile = true;
                    continue;
                }

                var importer = AssetImporter.GetAtPath(currentPath);
                if (importer == null)
                {
                    cacheNeedsReconcile = true;
                    continue;
                }

                var result = resolver.Resolve(currentPath, importer.GetType().FullName);
                if (result.Status != AssetFlowResolveStatus.Managed)
                {
                    cacheNeedsReconcile = true;
                    continue;
                }

                if (assetsByConfigGuid.TryGetValue(result.Config.ConfigGuid, out var paths))
                {
                    if (!string.Equals(asset.assetPath, currentPath, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(asset.managedByConfigGuid, result.Config.ConfigGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        cacheNeedsReconcile = true;
                    }

                    paths.Add(AssetFlowPath.Normalize(currentPath));
                }
            }

            return assetsByConfigGuid;
        }

        internal static string BuildCacheSignature(AssetFlowIndex index, IReadOnlyList<AssetFlowConfigSnapshot> snapshots)
        {
            var configPart = string.Join(
                "|",
                snapshots.Select(snapshot => $"{snapshot.ConfigGuid}:{snapshot.RuleHash}:{snapshot.ConfigPath}"));
            var assetPart = string.Join(
                "|",
                index.Assets
                    .OrderBy(asset => asset.assetGuid, StringComparer.OrdinalIgnoreCase)
                    .Select(asset =>
                    {
                        var currentPath = string.IsNullOrEmpty(asset.assetGuid)
                            ? string.Empty
                            : AssetDatabase.GUIDToAssetPath(asset.assetGuid);
                        return $"{asset.assetGuid}:{asset.assetPath}:{AssetFlowPath.Normalize(currentPath)}:{asset.managedByConfigGuid}:{asset.lastProcessedRuleHash}:{asset.lastProcessedTicks}";
                    }));
            var validationPart = string.Join(
                "|",
                index.ValidationResults
                    .OrderBy(record => record.assetGuid, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(record => record.configGuid, StringComparer.OrdinalIgnoreCase)
                    .Select(record => $"{record.assetGuid}:{record.configGuid}:{record.severity}:{record.message}:{record.ticks}"));
            return $"{configPart}\n{assetPart}\n{validationPart}";
        }

        private static int CountOutOfDate(IEnumerable<string> assetPaths, AssetFlowConfigSnapshot snapshot, AssetFlowIndex index)
        {
            return assetPaths.Count(path =>
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                return index.IsOutOfDate(guid, snapshot.ConfigGuid, snapshot.RuleHash);
            });
        }
    }
}
