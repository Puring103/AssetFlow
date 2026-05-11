using System.IO;
using AssetFlow.Editor.Core;
using NUnit.Framework;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowIndexTests
    {
        [Test]
        public void SaveAndLoad_RoundTripsConfigAndAssetRecords()
        {
            var path = Path.Combine("Library", "AssetFlowTests", "Index.json");
            var store = new AssetFlowIndexStore(path);
            var index = new AssetFlowIndex();

            index.UpsertConfig(new AssetFlowConfigRecord
            {
                configGuid = "config",
                configPath = "Assets/Art/AssetFlow.Texture.asset",
                folderPath = "Assets/Art",
                typeKey = "UnityEditor.TextureImporter",
                includeSubfolders = true,
                ruleHash = "rule",
            });
            index.UpsertAsset(new AssetFlowAssetRecord
            {
                assetGuid = "asset",
                assetPath = "Assets/Art/icon.png",
                importerTypeKey = "UnityEditor.TextureImporter",
                managedByConfigGuid = "config",
                managedByConfigPath = "Assets/Art/AssetFlow.Texture.asset",
                lastProcessedRuleHash = "rule",
                lastProcessedTicks = 7,
            });

            store.Save(index);
            var loaded = store.Load();

            Assert.That(loaded.Configs, Has.Count.EqualTo(1));
            Assert.That(loaded.Assets, Has.Count.EqualTo(1));
            Assert.That(loaded.Configs[0].configGuid, Is.EqualTo("config"));
            Assert.That(loaded.Assets[0].assetGuid, Is.EqualTo("asset"));
        }

        [Test]
        public void UpsertAsset_ReplacesExistingRecordByGuid()
        {
            var index = new AssetFlowIndex();

            index.UpsertAsset(new AssetFlowAssetRecord { assetGuid = "asset", assetPath = "Assets/Old.png" });
            index.UpsertAsset(new AssetFlowAssetRecord { assetGuid = "asset", assetPath = "Assets/New.png" });

            Assert.That(index.Assets, Has.Count.EqualTo(1));
            Assert.That(index.Assets[0].assetPath, Is.EqualTo("Assets/New.png"));
        }

        [Test]
        public void IsOutOfDate_ReturnsTrueWhenManagedAssetHasOldRuleHash()
        {
            var index = new AssetFlowIndex();
            index.UpsertAsset(new AssetFlowAssetRecord
            {
                assetGuid = "asset",
                managedByConfigGuid = "config",
                lastProcessedRuleHash = "old",
            });

            Assert.That(index.IsOutOfDate("asset", "config", "new"), Is.True);
            Assert.That(index.IsOutOfDate("asset", "config", "old"), Is.False);
        }

        [Test]
        public void ReplaceValidationResults_ReplacesResultsForSameAssetAndConfigOnly()
        {
            var index = new AssetFlowIndex();

            index.ReplaceValidationResults("asset", "config", new[]
            {
                new AssetFlowValidationRecord { assetGuid = "asset", configGuid = "config", message = "old" },
                new AssetFlowValidationRecord { assetGuid = "asset", configGuid = "config", message = "older" },
            });
            index.ReplaceValidationResults("asset", "other", new[]
            {
                new AssetFlowValidationRecord { assetGuid = "asset", configGuid = "other", message = "other" },
            });
            index.ReplaceValidationResults("asset", "config", new[]
            {
                new AssetFlowValidationRecord { assetGuid = "asset", configGuid = "config", message = "new" },
            });

            Assert.That(index.ValidationResults, Has.Count.EqualTo(2));
            Assert.That(index.ValidationResults, Has.Some.Matches<AssetFlowValidationRecord>(record => record.message == "new"));
            Assert.That(index.ValidationResults, Has.Some.Matches<AssetFlowValidationRecord>(record => record.message == "other"));
        }

        [Test]
        public void RemoveMissingConfigs_RemovesConfigRecordsThatNoLongerExist()
        {
            var index = new AssetFlowIndex();
            index.UpsertConfig(new AssetFlowConfigRecord { configGuid = "alive" });
            index.UpsertConfig(new AssetFlowConfigRecord { configGuid = "deleted" });

            index.RemoveMissingConfigs(new[] { "alive" });

            Assert.That(index.Configs, Has.Count.EqualTo(1));
            Assert.That(index.Configs[0].configGuid, Is.EqualTo("alive"));
        }
    }
}
