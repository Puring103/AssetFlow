using AssetFlow.Editor.Core;
using NUnit.Framework;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowPathTests
    {
        [Test]
        public void Normalize_ConvertsBackslashesAndTrimsTrailingSlash()
        {
            Assert.That(AssetFlowPath.Normalize(@"Assets\Art\Icons/"), Is.EqualTo("Assets/Art/Icons"));
            Assert.That(AssetFlowPath.Normalize(null), Is.EqualTo(string.Empty));
            Assert.That(AssetFlowPath.Normalize("   "), Is.EqualTo(string.Empty));
        }

        [Test]
        public void IsInFolder_RespectsDirectChildrenSubfoldersAndSiblingPrefixes()
        {
            Assert.That(AssetFlowPath.IsInFolder("Assets/Art/icon.png", "Assets/Art", false), Is.True);
            Assert.That(AssetFlowPath.IsInFolder("Assets/Art/UI/icon.png", "Assets/Art", false), Is.False);
            Assert.That(AssetFlowPath.IsInFolder("Assets/Art/UI/icon.png", "Assets/Art", true), Is.True);
            Assert.That(AssetFlowPath.IsInFolder("Assets/Artwork/icon.png", "Assets/Art", true), Is.False);
            Assert.That(AssetFlowPath.IsInFolder("assets/art/icon.png", "Assets/Art", false), Is.True);
        }

        [Test]
        public void IsDescendantOf_RequiresARealDescendant()
        {
            Assert.That(AssetFlowPath.IsDescendantOf("Assets/Art/UI", "Assets/Art"), Is.True);
            Assert.That(AssetFlowPath.IsDescendantOf("Assets/Art", "Assets/Art"), Is.False);
            Assert.That(AssetFlowPath.IsDescendantOf("Assets/Artwork", "Assets/Art"), Is.False);
        }

        [Test]
        public void Depth_CountsFolderSegments()
        {
            Assert.That(AssetFlowPath.Depth(null), Is.EqualTo(0));
            Assert.That(AssetFlowPath.Depth("Assets"), Is.EqualTo(1));
            Assert.That(AssetFlowPath.Depth("Assets/Art/UI"), Is.EqualTo(3));
        }
    }
}
