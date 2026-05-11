using AssetFlow.Editor.Core;
using NUnit.Framework;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowConfigSnapshotTests
    {
        [Test]
        public void Constructor_NormalizesPathsAndDefaultsNullValues()
        {
            var snapshot = new AssetFlowConfigSnapshot(
                null,
                @"Assets\Art\AssetFlow.Texture.asset/",
                @"Assets\Art/",
                null,
                true,
                null);

            Assert.That(snapshot.ConfigGuid, Is.EqualTo(string.Empty));
            Assert.That(snapshot.ConfigPath, Is.EqualTo("Assets/Art/AssetFlow.Texture.asset"));
            Assert.That(snapshot.FolderPath, Is.EqualTo("Assets/Art"));
            Assert.That(snapshot.TypeKey, Is.EqualTo(string.Empty));
            Assert.That(snapshot.RuleHash, Is.EqualTo(string.Empty));
            Assert.That(snapshot.IncludeSubfolders, Is.True);
        }

        [Test]
        public void IsValid_RequiresGuidFolderAndType()
        {
            Assert.That(new AssetFlowConfigSnapshot("guid", "Assets/AssetFlow.Texture.asset", "Assets", "type", false, "hash").IsValid, Is.True);
            Assert.That(new AssetFlowConfigSnapshot("", "Assets/AssetFlow.Texture.asset", "Assets", "type", false, "hash").IsValid, Is.False);
            Assert.That(new AssetFlowConfigSnapshot("guid", "Assets/AssetFlow.Texture.asset", "", "type", false, "hash").IsValid, Is.False);
            Assert.That(new AssetFlowConfigSnapshot("guid", "Assets/AssetFlow.Texture.asset", "Assets", "", false, "hash").IsValid, Is.False);
        }

        [Test]
        public void IsSameFolderAndType_IgnoresFolderCaseButKeepsTypeCaseSensitive()
        {
            var snapshot = new AssetFlowConfigSnapshot("a", "Assets/Art/A.asset", "Assets/Art", "UnityEditor.TextureImporter", false, "hash");
            var sameFolderAndType = new AssetFlowConfigSnapshot("b", "Assets/art/B.asset", "assets/art", "UnityEditor.TextureImporter", false, "hash");
            var differentTypeCase = new AssetFlowConfigSnapshot("c", "Assets/Art/C.asset", "Assets/Art", "unityeditor.textureimporter", false, "hash");

            Assert.That(snapshot.IsSameFolderAndType(sameFolderAndType), Is.True);
            Assert.That(snapshot.IsSameFolderAndType(differentTypeCase), Is.False);
        }
    }
}
