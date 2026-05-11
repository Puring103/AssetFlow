using AssetFlow.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.Importing
{
    public static class AssetFlowDependency
    {
        public static string CustomDependencyName(string configGuid)
        {
            return $"com.assetflow/{configGuid}";
        }

        public static void RegisterAll()
        {
            foreach (var snapshot in AssetFlowConfigScanner.FindConfigSnapshots())
            {
                if (string.IsNullOrEmpty(snapshot.ConfigGuid) || string.IsNullOrEmpty(snapshot.RuleHash))
                    continue;

                AssetDatabase.RegisterCustomDependency(
                    CustomDependencyName(snapshot.ConfigGuid),
                    Hash128.Compute(snapshot.RuleHash));
            }
        }
    }
}
