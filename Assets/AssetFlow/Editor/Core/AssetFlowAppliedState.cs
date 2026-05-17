using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AssetFlow.Editor.Core
{
    [Serializable]
    public sealed class AssetFlowAppliedStateData
    {
        public List<AssetFlowAppliedConfigRecord> configs = new List<AssetFlowAppliedConfigRecord>();
    }

    [Serializable]
    public sealed class AssetFlowAppliedConfigRecord
    {
        public string configGuid;
        public string ruleHash;
        public string snapshotJson;
    }

    public sealed class AssetFlowAppliedStateStore
    {
        public const string DefaultPath = "Library/AssetFlow/AppliedState.json";

        private readonly string path;

        public AssetFlowAppliedStateStore(string path = DefaultPath)
        {
            this.path = AssetFlowPath.Normalize(path);
        }

        public AssetFlowAppliedStateData Load()
        {
            if (!File.Exists(path))
                return new AssetFlowAppliedStateData();

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return new AssetFlowAppliedStateData();

            AssetFlowAppliedStateData data;
            try
            {
                data = JsonUtility.FromJson<AssetFlowAppliedStateData>(json);
            }
            catch (ArgumentException)
            {
                return new AssetFlowAppliedStateData();
            }

            if (data == null)
                return new AssetFlowAppliedStateData();

            data.configs = data.configs ?? new List<AssetFlowAppliedConfigRecord>();
            return data;
        }

        public void Save(AssetFlowAppliedStateData data)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, JsonUtility.ToJson(data ?? new AssetFlowAppliedStateData(), true));
        }

        public AssetFlowAppliedConfigRecord Find(string configGuid)
        {
            return Load().configs.Find(record => record.configGuid == configGuid);
        }

        public void SaveAppliedSnapshot(string configGuid, string ruleHash, string snapshotJson)
        {
            var data = Load();
            var existing = data.configs.Find(record => record.configGuid == configGuid);
            if (existing == null)
            {
                existing = new AssetFlowAppliedConfigRecord { configGuid = configGuid };
                data.configs.Add(existing);
            }

            existing.ruleHash = ruleHash;
            existing.snapshotJson = snapshotJson ?? string.Empty;
            Save(data);
        }
    }
}
