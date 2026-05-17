using System;
using System.Collections.Generic;
using System.Linq;
using AssetFlow.Editor.Core;
using AssetFlow.Editor.Workflow;
using UnityEditor;

namespace AssetFlow.Editor.Importing
{
    public static class AssetFlowConfigurationChangeProcessor
    {
        public enum ChangeKind
        {
            Added,
            Removed,
            Moved,
            Edited,
        }

        public readonly struct Change
        {
            public Change(string path, ChangeKind kind)
            {
                Path = AssetFlowPath.Normalize(path);
                Kind = kind;
            }

            public string Path { get; }

            public ChangeKind Kind { get; }
        }

        public static bool IsConfigPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                return false;

            return AssetDatabase.LoadAssetAtPath<AssetFlowConfig>(path) != null;
        }

        public static bool IsKnownConfigPath(string path, AssetFlowIndex index)
        {
            var normalizedPath = AssetFlowPath.Normalize(path);
            if (string.IsNullOrEmpty(normalizedPath))
                return false;

            return index?.Configs.Any(record =>
                string.Equals(
                    AssetFlowPath.Normalize(record.configPath),
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase)) == true;
        }

        public static void ProcessConfigurationChanges()
        {
            ProcessConfigurationChanges(Array.Empty<Change>());
        }

        public static int ProcessConfigurationChanges(IEnumerable<Change> changes)
        {
            return ProcessConfigurationChanges(changes, flushImmediately: false);
        }

        public static int ProcessConfigurationChangesImmediatelyForTests(IEnumerable<Change> changes)
        {
            return ProcessConfigurationChanges(changes, flushImmediately: true);
        }

        private static int ProcessConfigurationChanges(IEnumerable<Change> changes, bool flushImmediately)
        {
            AssetFlowDependency.RegisterAll();

            var snapshots = AssetFlowConfigScanner.FindConfigSnapshots();
            var indexStore = new AssetFlowIndexStore();
            var index = indexStore.Load();
            var previousAssets = index.Assets
                .Where(asset => !string.IsNullOrEmpty(asset.assetGuid))
                .ToDictionary(asset => asset.assetGuid, asset => asset, StringComparer.OrdinalIgnoreCase);
            var beforeChangedAssetGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var shouldAutoReprocess = ShouldAutoReprocess(changes);

            index.RemoveMissingConfigs(snapshots.Select(snapshot => snapshot.ConfigGuid));

            foreach (var snapshot in snapshots)
            {
                index.UpsertConfig(new AssetFlowConfigRecord
                {
                    configGuid = snapshot.ConfigGuid,
                    configPath = snapshot.ConfigPath,
                    folderPath = snapshot.FolderPath,
                    typeKey = snapshot.TypeKey,
                    includeSubfolders = snapshot.IncludeSubfolders,
                    ruleHash = snapshot.RuleHash,
                });
            }

            var reconcile = AssetFlowManagedAssetReconciler.Reconcile(index, snapshots, previousAssets.Values);
            foreach (var guid in reconcile.ChangedAssetGuids)
                beforeChangedAssetGuids.Add(guid);
            indexStore.Save(index);

            if (!shouldAutoReprocess)
                return 0;

            var queue = new AssetFlowReprocessQueue();
            EnqueueChangedManagedAssets(queue, index, previousAssets, beforeChangedAssetGuids);
            EnqueueNewlyManagedAssetsForAddedConfigs(queue, index, changes);
            if (flushImmediately)
                return queue.Flush();

            AssetFlowReprocessQueue.EnqueueShared(queue.Paths);
            AssetFlowReprocessQueue.ScheduleSharedFlush();
            return queue.Count;
        }

        private static bool ShouldAutoReprocess(IEnumerable<Change> changes)
        {
            var changeList = (changes ?? Array.Empty<Change>()).ToList();
            return changeList.Count > 0
                   && changeList.Any(change => change.Kind != ChangeKind.Edited);
        }

        private static void EnqueueChangedManagedAssets(
            AssetFlowReprocessQueue queue,
            AssetFlowIndex index,
            IReadOnlyDictionary<string, AssetFlowAssetRecord> previousAssets,
            IEnumerable<string> changedAssetGuids)
        {
            foreach (var guid in changedAssetGuids ?? Enumerable.Empty<string>())
            {
                var currentPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(currentPath))
                {
                    queue.Enqueue(currentPath);
                    continue;
                }

                if (previousAssets != null && previousAssets.TryGetValue(guid, out var previous))
                    queue.Enqueue(previous.assetPath);
            }
        }

        private static void EnqueueNewlyManagedAssetsForAddedConfigs(
            AssetFlowReprocessQueue queue,
            AssetFlowIndex index,
            IEnumerable<Change> changes)
        {
            var addedPaths = new HashSet<string>(
                (changes ?? Enumerable.Empty<Change>())
                .Where(change => change.Kind == ChangeKind.Added)
                .Select(change => change.Path),
                StringComparer.OrdinalIgnoreCase);
            if (addedPaths.Count == 0)
                return;

            foreach (var asset in index.Assets)
            {
                if (addedPaths.Contains(AssetFlowPath.Normalize(asset.managedByConfigPath)))
                    queue.Enqueue(asset.assetPath);
            }
        }

        private static bool IsIgnoredAssetPath(string path)
        {
            return AssetFlowManagedAssetReconciler.ShouldIgnoreAssetPath(path);
        }
    }
}
