using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AssetFlow.Editor.Core
{
    [Serializable]
    public sealed class AssetFlowIndexData
    {
        public int schemaVersion = 1;
        public List<AssetFlowConfigRecord> configs = new List<AssetFlowConfigRecord>();
        public List<AssetFlowAssetRecord> assets = new List<AssetFlowAssetRecord>();
        public List<AssetFlowValidationRecord> validationResults = new List<AssetFlowValidationRecord>();
    }

    [Serializable]
    public sealed class AssetFlowConfigRecord
    {
        public string configGuid;
        public string configPath;
        public string folderPath;
        public string typeKey;
        public bool includeSubfolders;
        public string ruleHash;
    }

    [Serializable]
    public sealed class AssetFlowAssetRecord
    {
        public string assetGuid;
        public string assetPath;
        public string importerTypeKey;
        public string managedByConfigGuid;
        public string managedByConfigPath;
        public string lastProcessedRuleHash;
        public long lastProcessedTicks;
    }

    [Serializable]
    public sealed class AssetFlowValidationRecord
    {
        public string assetGuid;
        public string configGuid;
        public string severity;
        public string message;
        public long ticks;
    }

    public sealed class AssetFlowIndex
    {
        private readonly AssetFlowIndexData data;

        public AssetFlowIndex()
            : this(new AssetFlowIndexData())
        {
        }

        public AssetFlowIndex(AssetFlowIndexData data)
        {
            this.data = data ?? new AssetFlowIndexData();
            this.data.configs = this.data.configs ?? new List<AssetFlowConfigRecord>();
            this.data.assets = this.data.assets ?? new List<AssetFlowAssetRecord>();
            this.data.validationResults = this.data.validationResults ?? new List<AssetFlowValidationRecord>();
        }

        public IReadOnlyList<AssetFlowConfigRecord> Configs => data.configs;

        public IReadOnlyList<AssetFlowAssetRecord> Assets => data.assets;

        public IReadOnlyList<AssetFlowValidationRecord> ValidationResults => data.validationResults;

        public AssetFlowIndexData ToData()
        {
            return data;
        }

        public void UpsertConfig(AssetFlowConfigRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.configGuid))
                return;

            var index = data.configs.FindIndex(existing => existing.configGuid == record.configGuid);
            if (index >= 0)
                data.configs[index] = record;
            else
                data.configs.Add(record);
        }

        public void UpsertAsset(AssetFlowAssetRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.assetGuid))
                return;

            var index = data.assets.FindIndex(existing => existing.assetGuid == record.assetGuid);
            if (index >= 0)
                data.assets[index] = record;
            else
                data.assets.Add(record);
        }

        public void RemoveAsset(string assetGuid)
        {
            if (string.IsNullOrWhiteSpace(assetGuid))
                return;

            data.assets.RemoveAll(record => string.Equals(record.assetGuid, assetGuid, StringComparison.OrdinalIgnoreCase));
            data.validationResults.RemoveAll(record => string.Equals(record.assetGuid, assetGuid, StringComparison.OrdinalIgnoreCase));
        }

        public void RemoveAssetAtPath(string assetPath)
        {
            var normalizedPath = AssetFlowPath.Normalize(assetPath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
                return;

            var removedGuids = data.assets
                .Where(record => string.Equals(AssetFlowPath.Normalize(record.assetPath), normalizedPath, StringComparison.OrdinalIgnoreCase))
                .Select(record => record.assetGuid)
                .Where(guid => !string.IsNullOrWhiteSpace(guid))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            data.assets.RemoveAll(record => string.Equals(AssetFlowPath.Normalize(record.assetPath), normalizedPath, StringComparison.OrdinalIgnoreCase));

            foreach (var guid in removedGuids)
                data.validationResults.RemoveAll(record => string.Equals(record.assetGuid, guid, StringComparison.OrdinalIgnoreCase));
        }

        public void ReplaceValidationResults(string assetGuid, string configGuid, IEnumerable<AssetFlowValidationRecord> records)
        {
            data.validationResults.RemoveAll(record => record.assetGuid == assetGuid && record.configGuid == configGuid);
            if (records != null)
                data.validationResults.AddRange(records);
        }

        public void RemoveMissingConfigs(IEnumerable<string> existingConfigGuids)
        {
            var existing = new HashSet<string>(existingConfigGuids ?? Enumerable.Empty<string>());
            data.configs.RemoveAll(record => !existing.Contains(record.configGuid));
        }

        public bool IsOutOfDate(string assetGuid, string configGuid, string currentRuleHash)
        {
            var record = data.assets.FirstOrDefault(asset => asset.assetGuid == assetGuid);
            if (record == null)
                return true;

            if (!string.Equals(record.managedByConfigGuid, configGuid, StringComparison.OrdinalIgnoreCase))
                return true;

            return !string.Equals(record.lastProcessedRuleHash, currentRuleHash, StringComparison.Ordinal);
        }
    }

    public sealed class AssetFlowIndexStore
    {
        public const string DefaultPath = "Library/AssetFlow/Index.json";

        private readonly string path;

        public AssetFlowIndexStore(string path = DefaultPath)
        {
            this.path = AssetFlowPath.Normalize(path);
        }

        public AssetFlowIndex Load()
        {
            if (!File.Exists(path))
                return new AssetFlowIndex();

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return new AssetFlowIndex();

            AssetFlowIndexData data;
            try
            {
                data = JsonUtility.FromJson<AssetFlowIndexData>(json);
            }
            catch (ArgumentException)
            {
                return new AssetFlowIndex();
            }

            return new AssetFlowIndex(data);
        }

        public void Save(AssetFlowIndex index)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonUtility.ToJson(index?.ToData() ?? new AssetFlowIndexData(), prettyPrint: true);
            File.WriteAllText(path, json);
        }
    }
}
