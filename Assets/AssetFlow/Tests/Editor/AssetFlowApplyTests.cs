using System.Collections.Generic;
using AssetFlow.Editor.Core;
using AssetFlow.Editor.Importing;
using NUnit.Framework;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowApplyTests
    {
        private const string TextureType = "UnityEditor.TextureImporter";

        [Test]
        public void FindManagedAssetsForConfig_DoesNotCrossChildConfigOrConflictBoundary()
        {
            var target = new AssetFlowConfigSnapshot("root", "Assets/AssetFlow.Texture.asset", "Assets", TextureType, true, "hash");
            var configs = new[]
            {
                target,
                new AssetFlowConfigSnapshot("child", "Assets/UI/AssetFlow.Texture.asset", "Assets/UI", TextureType, false, "hash"),
                new AssetFlowConfigSnapshot("conflictA", "Assets/Bad/A.asset", "Assets/Bad", TextureType, true, "hash"),
                new AssetFlowConfigSnapshot("conflictB", "Assets/Bad/B.asset", "Assets/Bad", TextureType, true, "hash"),
            };
            var assets = new[]
            {
                new AssetFlowApplyCandidate("root", "Assets/icon.png", TextureType),
                new AssetFlowApplyCandidate("child", "Assets/UI/icon.png", TextureType),
                new AssetFlowApplyCandidate("blocked", "Assets/Bad/icon.png", TextureType),
                new AssetFlowApplyCandidate("nestedBlocked", "Assets/Bad/Deep/icon.png", TextureType),
            };

            var managed = AssetFlowApplyService.FindManagedAssetsForConfig(target, configs, assets);

            Assert.That(managed, Is.EqualTo(new List<string> { "Assets/icon.png" }));
        }

        [Test]
        public void FindManagedAssetsForConfig_UsesCurrentConfigSnapshotWhenConfigIsNew()
        {
            var target = new AssetFlowConfigSnapshot("new", "Assets/New/AssetFlow.Texture.asset", "Assets/New", TextureType, false, "hash");
            var configs = new[] { target };
            var assets = new[]
            {
                new AssetFlowApplyCandidate("asset", "Assets/New/icon.png", TextureType),
            };

            var managed = AssetFlowApplyService.FindManagedAssetsForConfig(target, configs, assets);

            Assert.That(managed, Is.EqualTo(new List<string> { "Assets/New/icon.png" }));
        }
    }
}
