using System;

namespace AssetFlow.Editor.Core
{
    public readonly struct AssetFlowConfigSnapshot
    {
        public AssetFlowConfigSnapshot(
            string configGuid,
            string configPath,
            string folderPath,
            string typeKey,
            bool includeSubfolders,
            string ruleHash)
        {
            ConfigGuid = configGuid ?? string.Empty;
            ConfigPath = AssetFlowPath.Normalize(configPath);
            FolderPath = AssetFlowPath.NormalizeFolder(folderPath);
            TypeKey = typeKey ?? string.Empty;
            IncludeSubfolders = includeSubfolders;
            RuleHash = ruleHash ?? string.Empty;
        }

        public string ConfigGuid { get; }

        public string ConfigPath { get; }

        public string FolderPath { get; }

        public string TypeKey { get; }

        public bool IncludeSubfolders { get; }

        public string RuleHash { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(ConfigGuid)
                               && !string.IsNullOrWhiteSpace(FolderPath)
                               && !string.IsNullOrWhiteSpace(TypeKey);

        public bool IsSameFolderAndType(AssetFlowConfigSnapshot other)
        {
            return string.Equals(FolderPath, other.FolderPath, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(TypeKey, other.TypeKey, StringComparison.Ordinal);
        }
    }
}
