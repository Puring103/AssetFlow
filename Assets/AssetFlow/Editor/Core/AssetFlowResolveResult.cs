using System.Collections.Generic;

namespace AssetFlow.Editor.Core
{
    public enum AssetFlowResolveStatus
    {
        Unmanaged,
        Managed,
        Conflict,
    }

    public sealed class AssetFlowResolveResult
    {
        private static readonly IReadOnlyList<AssetFlowConfigSnapshot> EmptyConflicts =
            new List<AssetFlowConfigSnapshot>();

        private AssetFlowResolveResult(
            AssetFlowResolveStatus status,
            AssetFlowConfigSnapshot config,
            IReadOnlyList<AssetFlowConfigSnapshot> conflictingConfigs)
        {
            Status = status;
            Config = config;
            ConflictingConfigs = conflictingConfigs ?? EmptyConflicts;
        }

        public AssetFlowResolveStatus Status { get; }

        public AssetFlowConfigSnapshot Config { get; }

        public IReadOnlyList<AssetFlowConfigSnapshot> ConflictingConfigs { get; }

        public static AssetFlowResolveResult Unmanaged()
        {
            return new AssetFlowResolveResult(AssetFlowResolveStatus.Unmanaged, default, EmptyConflicts);
        }

        public static AssetFlowResolveResult Managed(AssetFlowConfigSnapshot config)
        {
            return new AssetFlowResolveResult(AssetFlowResolveStatus.Managed, config, EmptyConflicts);
        }

        public static AssetFlowResolveResult Conflict(IReadOnlyList<AssetFlowConfigSnapshot> configs)
        {
            return new AssetFlowResolveResult(AssetFlowResolveStatus.Conflict, default, configs);
        }
    }
}
