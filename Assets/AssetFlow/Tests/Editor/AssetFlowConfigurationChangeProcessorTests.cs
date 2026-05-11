using AssetFlow.Editor.Importing;
using NUnit.Framework;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowConfigurationChangeProcessorTests
    {
        [Test]
        public void IsConfigPath_OnlyTreatsAssetFlowAssetsAsConfigurationChanges()
        {
            Assert.That(AssetFlowConfigurationChangeProcessor.IsConfigPath(null), Is.False);
            Assert.That(AssetFlowConfigurationChangeProcessor.IsConfigPath("Assets/Art/icon.png"), Is.False);
            Assert.That(AssetFlowConfigurationChangeProcessor.IsConfigPath("Assets/Art/Plain.asset"), Is.False);
            Assert.That(AssetFlowConfigurationChangeProcessor.IsConfigPath("Assets/Art/AssetFlow.Texture.asset"), Is.True);
            Assert.That(AssetFlowConfigurationChangeProcessor.IsConfigPath("Assets/Art/assetflow.model.ASSET"), Is.True);
        }
    }
}
