using AssetFlow.Editor.Importing;
using NUnit.Framework;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowReprocessQueueTests
    {
        [Test]
        public void Enqueue_DeduplicatesAndOrdersPaths()
        {
            var queue = new AssetFlowReprocessQueue();

            queue.Enqueue("Assets/B.png");
            queue.Enqueue(@"Assets\A.png");
            queue.Enqueue("Assets/B.png");
            queue.Enqueue(null);

            Assert.That(queue.Count, Is.EqualTo(2));
            Assert.That(queue.Paths, Is.EqualTo(new[] { "Assets/A.png", "Assets/B.png" }));
        }
    }
}
