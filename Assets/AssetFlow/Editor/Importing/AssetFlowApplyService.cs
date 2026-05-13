using System;
using System.Collections.Generic;
using System.Linq;
using AssetFlow.Editor.Core;
using AssetFlow.Editor.Workflow;
using UnityEditor;

namespace AssetFlow.Editor.Importing
{
    public readonly struct AssetFlowApplyCandidate
    {
        public AssetFlowApplyCandidate(string guid, string path, string importerTypeKey)
        {
            Guid = guid ?? string.Empty;
            Path = AssetFlowPath.Normalize(path);
            ImporterTypeKey = importerTypeKey ?? string.Empty;
        }

        public string Guid { get; }

        public string Path { get; }

        public string ImporterTypeKey { get; }
    }

    public static class AssetFlowApplyService
    {
        private static AssetFlowAppliedStateStore appliedStateStore = new AssetFlowAppliedStateStore();

        public readonly struct AssetFlowManagedStats
        {
            public AssetFlowManagedStats(int managedCount, int outOfDateCount)
            {
                ManagedCount = managedCount;
                OutOfDateCount = outOfDateCount;
            }

            public int ManagedCount { get; }

            public int OutOfDateCount { get; }
        }

        public static List<string> FindManagedAssetsForConfig(
            AssetFlowConfigSnapshot config,
            IEnumerable<AssetFlowConfigSnapshot> allConfigs,
            IEnumerable<AssetFlowApplyCandidate> candidates)
        {
            var resolver = new AssetFlowResolver(allConfigs);
            return candidates
                .Where(candidate => candidate.ImporterTypeKey == config.TypeKey)
                .Where(candidate =>
                {
                    var result = resolver.Resolve(candidate.Path, candidate.ImporterTypeKey);
                    return result.Status == AssetFlowResolveStatus.Managed
                           && result.Config.ConfigGuid == config.ConfigGuid;
                })
                .Select(candidate => candidate.Path)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static int ApplyToManagedAssets(AssetFlowConfig config)
        {
            if (config == null)
                return 0;

            AssetFlowDependency.RegisterAll();
            var snapshot = config.ToSnapshot();
            var configs = AssetFlowConfigScanner.FindConfigSnapshots();
            var candidates = FindImporterCandidates(snapshot.TypeKey);
            var paths = FindManagedAssetsForConfig(snapshot, configs, candidates);

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in paths)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            appliedStateStore.SaveAppliedSnapshot(
                snapshot.ConfigGuid,
                snapshot.RuleHash,
                EditorJsonUtility.ToJson(config));
            return paths.Count;
        }

        public static int CountOutOfDateManagedAssets(AssetFlowConfig config)
        {
            if (config == null)
                return 0;

            var snapshot = config.ToSnapshot();
            var configs = AssetFlowConfigScanner.FindConfigSnapshots();
            var candidates = FindImporterCandidates(snapshot.TypeKey);
            var managed = FindManagedAssetsForConfig(snapshot, configs, candidates);
            var index = new AssetFlowIndexStore().Load();
            return managed.Count(path =>
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                return index.IsOutOfDate(guid, snapshot.ConfigGuid, snapshot.RuleHash);
            });
        }

        public static AssetFlowManagedStats GetManagedStats(AssetFlowConfig config)
        {
            if (config == null)
                return new AssetFlowManagedStats(0, 0);

            var snapshot = config.ToSnapshot();
            var configs = AssetFlowConfigScanner.FindConfigSnapshots();
            var candidates = FindImporterCandidates(snapshot.TypeKey);
            var managed = FindManagedAssetsForConfig(snapshot, configs, candidates);
            var index = new AssetFlowIndexStore().Load();
            var outOfDate = managed.Count(path =>
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                return index.IsOutOfDate(guid, snapshot.ConfigGuid, snapshot.RuleHash);
            });

            return new AssetFlowManagedStats(managed.Count, outOfDate);
        }

        public static List<string> FindManagedAssetsForConfig(AssetFlowConfig config)
        {
            if (config == null)
                return new List<string>();

            var snapshot = config.ToSnapshot();
            var configs = AssetFlowConfigScanner.FindConfigSnapshots();
            var candidates = FindImporterCandidates(snapshot.TypeKey);
            return FindManagedAssetsForConfig(snapshot, configs, candidates);
        }

        internal static void SetAppliedStateStoreForTests(AssetFlowAppliedStateStore store)
        {
            appliedStateStore = store ?? new AssetFlowAppliedStateStore();
        }

        private static IEnumerable<AssetFlowApplyCandidate> FindImporterCandidates(string typeKey)
        {
            foreach (var guid in AssetDatabase.FindAssets(string.Empty))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    continue;

                var importer = AssetImporter.GetAtPath(path);
                if (importer == null)
                    continue;

                var importerTypeKey = importer.GetType().FullName;
                if (importerTypeKey == typeKey)
                    yield return new AssetFlowApplyCandidate(guid, path, importerTypeKey);
            }
        }
    }
}
