using System.Collections.Generic;
using AssetFlow.Editor.Core;
using NUnit.Framework;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowResolverTests
    {
        private const string TextureType = "UnityEditor.TextureImporter";

        [Test]
        public void Resolve_UsesNearestConfigAndStopsAtChildConfigBoundary()
        {
            var configs = new[]
            {
                Config("root", "Assets", TextureType, includeSubfolders: true),
                Config("ui", "Assets/UI", TextureType, includeSubfolders: false),
            };

            var resolver = new AssetFlowResolver(configs);

            var rootResult = resolver.Resolve("Assets/Textures/hero.png", TextureType);
            var childResult = resolver.Resolve("Assets/UI/icon.png", TextureType);
            var childNestedResult = resolver.Resolve("Assets/UI/Icons/icon.png", TextureType);

            Assert.That(rootResult.Status, Is.EqualTo(AssetFlowResolveStatus.Managed));
            Assert.That(rootResult.Config.ConfigGuid, Is.EqualTo("root"));
            Assert.That(childResult.Status, Is.EqualTo(AssetFlowResolveStatus.Managed));
            Assert.That(childResult.Config.ConfigGuid, Is.EqualTo("ui"));
            Assert.That(childNestedResult.Status, Is.EqualTo(AssetFlowResolveStatus.Unmanaged));
        }

        [Test]
        public void Resolve_TreatsSameFolderAndTypeDuplicatesAsConflictBoundary()
        {
            var configs = new[]
            {
                Config("root", "Assets", TextureType, includeSubfolders: true),
                Config("a", "Assets/UI", TextureType, includeSubfolders: true),
                Config("b", "Assets/UI", TextureType, includeSubfolders: true),
                Config("nested", "Assets/UI/Icons", TextureType, includeSubfolders: false),
            };

            var resolver = new AssetFlowResolver(configs);

            var conflictResult = resolver.Resolve("Assets/UI/button.png", TextureType);
            var blockedResult = resolver.Resolve("Assets/UI/Other/button.png", TextureType);
            var nestedResult = resolver.Resolve("Assets/UI/Icons/button.png", TextureType);

            Assert.That(conflictResult.Status, Is.EqualTo(AssetFlowResolveStatus.Conflict));
            Assert.That(conflictResult.ConflictingConfigs, Has.Count.EqualTo(2));
            Assert.That(blockedResult.Status, Is.EqualTo(AssetFlowResolveStatus.Unmanaged));
            Assert.That(nestedResult.Status, Is.EqualTo(AssetFlowResolveStatus.Managed));
            Assert.That(nestedResult.Config.ConfigGuid, Is.EqualTo("nested"));
        }

        [Test]
        public void Resolve_OnlyMatchesTheRequestedTypeKey()
        {
            var configs = new[]
            {
                Config("texture", "Assets", TextureType, includeSubfolders: true),
                Config("audio", "Assets/Audio", "UnityEditor.AudioImporter", includeSubfolders: true),
            };

            var resolver = new AssetFlowResolver(configs);

            var result = resolver.Resolve("Assets/Audio/theme.wav", TextureType);

            Assert.That(result.Status, Is.EqualTo(AssetFlowResolveStatus.Managed));
            Assert.That(result.Config.ConfigGuid, Is.EqualTo("texture"));
        }

        private static AssetFlowConfigSnapshot Config(string guid, string folder, string typeKey, bool includeSubfolders)
        {
            return new AssetFlowConfigSnapshot(guid, $"{folder}/AssetFlow.asset", folder, typeKey, includeSubfolders, "hash");
        }
    }
}
