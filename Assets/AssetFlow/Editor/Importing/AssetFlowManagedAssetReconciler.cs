using System;
using System.Collections.Generic;
using System.Linq;
using AssetFlow.Editor.Core;
using UnityEditor;

namespace AssetFlow.Editor.Importing
{
    public readonly struct AssetFlowManagedAssetCandidate
    {
        public AssetFlowManagedAssetCandidate(string guid, string path, string importerTypeKey)
        {
            Guid = guid ?? string.Empty;
            Path = AssetFlowPath.Normalize(path);
            ImporterTypeKey = importerTypeKey ?? string.Empty;
        }

        public string Guid { get; }

        public string Path { get; }

        public string ImporterTypeKey { get; }
    }

    public sealed class ManagedAssetReconcileResult
    {
        public ManagedAssetReconcileResult(AssetFlowIndex index, IReadOnlyList<string> changedAssetGuids)
        {
            Index = index ?? new AssetFlowIndex();
            ChangedAssetGuids = changedAssetGuids ?? Array.Empty<string>();
        }

        public AssetFlowIndex Index { get; }

        public IReadOnlyList<string> ChangedAssetGuids { get; }
    }

    public static class AssetFlowManagedAssetReconciler
    {
        public static ManagedAssetReconcileResult Reconcile(
            AssetFlowIndex index,
            IReadOnlyList<AssetFlowConfigSnapshot> snapshots,
            IEnumerable<AssetFlowAssetRecord> previousAssets)
        {
            index = index ?? new AssetFlowIndex();
            snapshots = snapshots ?? Array.Empty<AssetFlowConfigSnapshot>();
            var previous = previousAssets?.ToList() ?? new List<AssetFlowAssetRecord>();
            var resolver = new AssetFlowResolver(snapshots);
            var changedAssetGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenAssetGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in FindCandidates(snapshots, previous))
            {
                if (string.IsNullOrEmpty(candidate.Guid) || string.IsNullOrEmpty(candidate.Path))
                    continue;

                var result = resolver.Resolve(candidate.Path, candidate.ImporterTypeKey);
                if (result.Status != AssetFlowResolveStatus.Managed)
                    continue;

                seenAssetGuids.Add(candidate.Guid);
                var existing = index.Assets.FirstOrDefault(asset =>
                    string.Equals(asset.assetGuid, candidate.Guid, StringComparison.OrdinalIgnoreCase));
                if (existing == null
                    || !string.Equals(existing.managedByConfigGuid, result.Config.ConfigGuid, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(AssetFlowPath.Normalize(existing.assetPath), candidate.Path, StringComparison.OrdinalIgnoreCase))
                {
                    changedAssetGuids.Add(candidate.Guid);
                }

                var lastProcessedRuleHash = existing != null
                                            && string.Equals(existing.managedByConfigGuid, result.Config.ConfigGuid, StringComparison.OrdinalIgnoreCase)
                    ? existing.lastProcessedRuleHash
                    : string.Empty;
                index.UpsertAsset(new AssetFlowAssetRecord
                {
                    assetGuid = candidate.Guid,
                    assetPath = candidate.Path,
                    importerTypeKey = candidate.ImporterTypeKey,
                    managedByConfigGuid = result.Config.ConfigGuid,
                    managedByConfigPath = result.Config.ConfigPath,
                    lastProcessedRuleHash = lastProcessedRuleHash,
                    lastProcessedTicks = existing?.lastProcessedTicks ?? 0,
                });
            }

            foreach (var asset in index.Assets.ToList())
            {
                if (!seenAssetGuids.Contains(asset.assetGuid))
                {
                    changedAssetGuids.Add(asset.assetGuid);
                    index.RemoveAsset(asset.assetGuid);
                }
            }

            return new ManagedAssetReconcileResult(index, changedAssetGuids.ToList());
        }

        public static List<string> FindManagedAssetsForConfig(
            AssetFlowConfigSnapshot config,
            IEnumerable<AssetFlowConfigSnapshot> allConfigs,
            IEnumerable<AssetFlowManagedAssetCandidate> candidates)
        {
            var resolver = new AssetFlowResolver(allConfigs ?? Array.Empty<AssetFlowConfigSnapshot>());
            return (candidates ?? Array.Empty<AssetFlowManagedAssetCandidate>())
                .Where(candidate => string.Equals(candidate.ImporterTypeKey, config.TypeKey, StringComparison.Ordinal))
                .Where(candidate =>
                {
                    var result = resolver.Resolve(candidate.Path, candidate.ImporterTypeKey);
                    return result.Status == AssetFlowResolveStatus.Managed
                           && string.Equals(result.Config.ConfigGuid, config.ConfigGuid, StringComparison.OrdinalIgnoreCase);
                })
                .Select(candidate => candidate.Path)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static IEnumerable<AssetFlowManagedAssetCandidate> FindCandidates(
            IReadOnlyList<AssetFlowConfigSnapshot> snapshots,
            IEnumerable<AssetFlowAssetRecord> previousAssets = null,
            string typeKey = "")
        {
            var folders = (snapshots ?? Array.Empty<AssetFlowConfigSnapshot>())
                .Select(snapshot => snapshot.FolderPath)
                .Concat((previousAssets ?? Enumerable.Empty<AssetFlowAssetRecord>())
                    .Select(asset => AssetFlowPath.GetParentFolder(asset.assetPath)))
                .Where(folder => !string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (folders.Length == 0)
                yield break;

            foreach (var guid in AssetDatabase.FindAssets(string.Empty, folders).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (ShouldIgnoreAssetPath(path))
                    continue;

                var importer = AssetImporter.GetAtPath(path);
                if (importer == null)
                    continue;

                var importerTypeKey = importer.GetType().FullName;
                if (!string.IsNullOrEmpty(typeKey) && !string.Equals(importerTypeKey, typeKey, StringComparison.Ordinal))
                    continue;

                yield return new AssetFlowManagedAssetCandidate(guid, path, importerTypeKey);
            }
        }

        internal static bool ShouldIgnoreAssetPath(string path)
        {
            return string.IsNullOrEmpty(path)
                   || path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                   || AssetFlowTemplateImporterUtility.IsTemplateSourceAsset(path);
        }
    }
}
