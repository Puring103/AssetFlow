using System;
using System.Collections.Generic;
using UnityEditor;

namespace AssetFlow.Editor.Importing
{
    public enum AssetFlowStage
    {
        PreImport,
        PostImport,
        Validation,
    }

    public readonly struct AssetFlowLoopKey : IEquatable<AssetFlowLoopKey>
    {
        public AssetFlowLoopKey(string assetGuid, string configGuid, AssetFlowStage stage, string handlerTypeFullName)
        {
            AssetGuid = assetGuid ?? string.Empty;
            ConfigGuid = configGuid ?? string.Empty;
            Stage = stage;
            HandlerTypeFullName = handlerTypeFullName ?? string.Empty;
        }

        public string AssetGuid { get; }

        public string ConfigGuid { get; }

        public AssetFlowStage Stage { get; }

        public string HandlerTypeFullName { get; }

        public bool Equals(AssetFlowLoopKey other)
        {
            return AssetGuid == other.AssetGuid
                   && ConfigGuid == other.ConfigGuid
                   && Stage == other.Stage
                   && HandlerTypeFullName == other.HandlerTypeFullName;
        }

        public override bool Equals(object obj)
        {
            return obj is AssetFlowLoopKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = AssetGuid.GetHashCode();
                hashCode = (hashCode * 397) ^ ConfigGuid.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Stage;
                hashCode = (hashCode * 397) ^ HandlerTypeFullName.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"{AssetGuid}|{ConfigGuid}|{Stage}|{HandlerTypeFullName}";
        }
    }

    public sealed class AssetFlowLoopGuard
    {
        private const string SessionPrefix = "AssetFlow.LoopPaused.";
        private readonly Dictionary<string, int> chainCounts = new Dictionary<string, int>();
        private readonly Dictionary<AssetFlowLoopKey, List<DateTime>> rollingCounts = new Dictionary<AssetFlowLoopKey, List<DateTime>>();
        private readonly HashSet<AssetFlowLoopKey> pausedInMemory = new HashSet<AssetFlowLoopKey>();
        private readonly int threshold;
        private readonly bool useSessionState;
        private readonly TimeSpan rollingWindow;
        private readonly Func<DateTime> clock;

        public AssetFlowLoopGuard(
            int threshold = 3,
            bool useSessionState = false,
            TimeSpan? rollingWindow = null,
            Func<DateTime> clock = null)
        {
            this.threshold = Math.Max(1, threshold);
            this.useSessionState = useSessionState;
            this.rollingWindow = rollingWindow ?? TimeSpan.FromSeconds(10);
            this.clock = clock ?? (() => DateTime.UtcNow);
        }

        public bool ShouldRun(AssetFlowLoopKey key, string chainId)
        {
            if (IsPaused(key))
                return false;

            if (string.IsNullOrEmpty(chainId))
                return ShouldRunInRollingWindow(key);

            var counterKey = $"{chainId ?? string.Empty}|{key}";
            chainCounts.TryGetValue(counterKey, out var count);
            count++;
            chainCounts[counterKey] = count;

            if (count <= threshold)
                return true;

            Pause(key);
            return false;
        }

        private bool ShouldRunInRollingWindow(AssetFlowLoopKey key)
        {
            var now = clock();
            if (!rollingCounts.TryGetValue(key, out var samples))
            {
                samples = new List<DateTime>();
                rollingCounts[key] = samples;
            }

            samples.RemoveAll(sample => now - sample > rollingWindow);
            samples.Add(now);

            if (samples.Count <= threshold)
                return true;

            Pause(key);
            return false;
        }

        public bool IsPaused(AssetFlowLoopKey key)
        {
            if (pausedInMemory.Contains(key))
                return true;

            return useSessionState && SessionState.GetBool(SessionPrefix + key, false);
        }

        public IReadOnlyList<AssetFlowLoopKey> GetPausedKeysForAsset(string assetGuid)
        {
            var result = new List<AssetFlowLoopKey>();
            foreach (var key in pausedInMemory)
            {
                if (key.AssetGuid == (assetGuid ?? string.Empty))
                    result.Add(key);
            }

            return result;
        }

        public void Pause(AssetFlowLoopKey key)
        {
            pausedInMemory.Add(key);
            if (useSessionState)
                SessionState.SetBool(SessionPrefix + key, true);
        }

        public void Retry(AssetFlowLoopKey key)
        {
            pausedInMemory.Remove(key);
            ClearCounts(key);
            rollingCounts.Remove(key);
            if (useSessionState)
                SessionState.EraseBool(SessionPrefix + key);
        }

        private void ClearCounts(AssetFlowLoopKey key)
        {
            var suffix = "|" + key;
            var keysToRemove = new List<string>();
            foreach (var countKey in chainCounts.Keys)
            {
                if (countKey.EndsWith(suffix, StringComparison.Ordinal))
                    keysToRemove.Add(countKey);
            }

            foreach (var countKey in keysToRemove)
                chainCounts.Remove(countKey);
        }
    }
}
