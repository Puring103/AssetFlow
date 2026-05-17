using System;
using System.Collections.Generic;
using System.Linq;
using AssetFlow.Editor.Core;
using UnityEditor;

namespace AssetFlow.Editor.Importing
{
    public sealed class AssetFlowReprocessQueue
    {
        private static readonly AssetFlowReprocessQueue Shared = new AssetFlowReprocessQueue();
        private readonly SortedSet<string> paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool flushQueued;

        public int Count => paths.Count;

        public IReadOnlyList<string> Paths => paths.ToList();

        public void Enqueue(string assetPath)
        {
            var normalizedPath = AssetFlowPath.Normalize(assetPath);
            if (string.IsNullOrEmpty(normalizedPath))
                return;

            paths.Add(normalizedPath);
        }

        public void EnqueueMany(IEnumerable<string> assetPaths)
        {
            if (assetPaths == null)
                return;

            foreach (var assetPath in assetPaths)
                Enqueue(assetPath);
        }

        public int Flush()
        {
            if (paths.Count == 0)
                return 0;

            var importedCount = 0;
            var pendingPaths = paths.ToList();
            paths.Clear();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in pendingPaths)
                {
                    if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null)
                        continue;

                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    importedCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            return importedCount;
        }

        public void ScheduleFlush()
        {
            if (flushQueued)
                return;

            flushQueued = true;
            EditorApplication.delayCall += FlushScheduled;
        }

        public static void EnqueueShared(IEnumerable<string> assetPaths)
        {
            Shared.EnqueueMany(assetPaths);
        }

        public static void ScheduleSharedFlush()
        {
            Shared.ScheduleFlush();
        }

        internal static int FlushSharedForTests()
        {
            return Shared.Flush();
        }

        private void FlushScheduled()
        {
            EditorApplication.delayCall -= FlushScheduled;
            flushQueued = false;
            Flush();
        }
    }
}
