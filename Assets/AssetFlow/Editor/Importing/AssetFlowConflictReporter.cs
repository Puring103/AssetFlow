using System.Collections.Generic;
using AssetFlow.Editor.Core;
using UnityEngine;

namespace AssetFlow.Editor.Importing
{
    public static class AssetFlowConflictReporter
    {
        private static readonly HashSet<string> ReportedKeys = new HashSet<string>();

        public static void Report(string assetPath, AssetFlowResolveResult result)
        {
            if (result == null || result.Status != AssetFlowResolveStatus.Conflict)
                return;

            var key = assetPath ?? string.Empty;
            foreach (var config in result.ConflictingConfigs)
                key += "|" + config.ConfigGuid;

            if (!ReportedKeys.Add(key))
                return;

            var message = $"AssetFlow conflict for {assetPath}. Conflicting configs:";
            foreach (var config in result.ConflictingConfigs)
                message += $"\n- {config.ConfigPath}";

            Debug.LogError(message);
        }
    }
}
