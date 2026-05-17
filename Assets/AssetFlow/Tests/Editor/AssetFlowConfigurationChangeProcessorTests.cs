using AssetFlow.Editor.Importing;
using AssetFlow.Editor.Workflow;
using AssetFlow.Editor.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowConfigurationChangeProcessorTests
    {
        private const string TestFolder = "Assets/AssetFlowConfigurationChangeGeneratedTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder("Assets", "AssetFlowConfigurationChangeGeneratedTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestFolder);
        }

        [Test]
        public void IsConfigPath_OnlyTreatsAssetFlowAssetsAsConfigurationChanges()
        {
            Assert.That(AssetFlowConfigurationChangeProcessor.IsConfigPath(null), Is.False);
            Assert.That(AssetFlowConfigurationChangeProcessor.IsConfigPath("Assets/Art/icon.png"), Is.False);
            Assert.That(AssetFlowConfigurationChangeProcessor.IsConfigPath("Assets/Art/Plain.asset"), Is.False);

            var configPath = AssetFlowConfigFactory.CreateTextureConfig(TestFolder);
            Assert.That(AssetFlowConfigurationChangeProcessor.IsConfigPath(configPath), Is.True);
        }

        [Test]
        public void IsConfigPath_DoesNotTreatNameOnlyAssetFlowAssetAsConfig()
        {
            var asset = ScriptableObject.CreateInstance<PlainScriptableObject>();
            var path = TestFolder + "/AssetFlow.Texture.asset";
            try
            {
                AssetDatabase.CreateAsset(asset, path);

                Assert.That(AssetFlowConfigurationChangeProcessor.IsConfigPath(path), Is.False);
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (asset != null)
                    Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void IsKnownConfigPath_ReturnsTrueForDeletedConfigPathRecordedInIndex()
        {
            var index = new AssetFlowIndex();
            index.UpsertConfig(new AssetFlowConfigRecord
            {
                configGuid = "config",
                configPath = TestFolder + "/Deleted.asset",
            });

            Assert.That(AssetFlowConfigurationChangeProcessor.IsKnownConfigPath(TestFolder + "/Deleted.asset", index), Is.True);
            Assert.That(AssetFlowConfigurationChangeProcessor.IsKnownConfigPath(TestFolder + "/Other.asset", index), Is.False);
        }

        private sealed class PlainScriptableObject : ScriptableObject
        {
        }
    }
}
