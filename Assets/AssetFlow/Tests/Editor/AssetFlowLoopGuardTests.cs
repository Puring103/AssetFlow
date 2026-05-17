using AssetFlow.Editor.Importing;
using NUnit.Framework;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowLoopGuardTests
    {
        [Test]
        public void ShouldRun_PausesOnlyTheHandlerThatExceedsThresholdInAChain()
        {
            var guard = new AssetFlowLoopGuard(threshold: 3);
            var key = new AssetFlowLoopKey("asset", "config", AssetFlowStage.PostImport, "HandlerA");
            var other = new AssetFlowLoopKey("asset", "config", AssetFlowStage.PostImport, "HandlerB");
            var chain = "chain";

            Assert.That(guard.ShouldRun(key, chain), Is.True);
            Assert.That(guard.ShouldRun(key, chain), Is.True);
            Assert.That(guard.ShouldRun(key, chain), Is.True);
            Assert.That(guard.ShouldRun(key, chain), Is.False);
            Assert.That(guard.ShouldRun(other, chain), Is.True);
        }

        [Test]
        public void Retry_ClearsPausedHandler()
        {
            var guard = new AssetFlowLoopGuard(threshold: 1);
            var key = new AssetFlowLoopKey("asset", "config", AssetFlowStage.PreImport, "Handler");

            Assert.That(guard.ShouldRun(key, "chain"), Is.True);
            Assert.That(guard.ShouldRun(key, "chain"), Is.False);

            guard.Retry(key);

            Assert.That(guard.ShouldRun(key, "chain"), Is.True);
        }

        [Test]
        public void GetPausedKeysForAsset_ReturnsOnlyPausedKeysForRequestedAsset()
        {
            var guard = new AssetFlowLoopGuard(threshold: 1);
            var paused = new AssetFlowLoopKey("asset", "config", AssetFlowStage.PostImport, "Handler");
            var otherAsset = new AssetFlowLoopKey("other", "config", AssetFlowStage.PostImport, "Handler");

            guard.ShouldRun(paused, "chain");
            guard.ShouldRun(paused, "chain");
            guard.ShouldRun(otherAsset, "chain");
            guard.ShouldRun(otherAsset, "chain");

            var pausedForAsset = guard.GetPausedKeysForAsset("asset");

            Assert.That(pausedForAsset, Is.EqualTo(new[] { paused }));
        }

        [Test]
        public void GetPausedKeysForAsset_ReturnsSessionStateKeysAfterNewGuardIsCreated()
        {
            var pauseStore = new InMemoryPauseStore();
            var firstGuard = new AssetFlowLoopGuard(threshold: 1, pauseStore: pauseStore);
            var secondGuard = new AssetFlowLoopGuard(threshold: 1, pauseStore: pauseStore);
            var key = new AssetFlowLoopKey(
                "session-asset",
                "session-config",
                AssetFlowStage.Validation,
                "SessionHandler");

            try
            {
                firstGuard.ShouldRun(key, "chain");
                firstGuard.ShouldRun(key, "chain");

                Assert.That(secondGuard.GetPausedKeysForAsset("session-asset"), Is.EqualTo(new[] { key }));

                secondGuard.Retry(key);

                Assert.That(firstGuard.IsPaused(key), Is.False);
            }
            finally
            {
                firstGuard.Retry(key);
                secondGuard.Retry(key);
            }
        }

        [Test]
        public void ShouldRun_UsesRollingWindowWhenChainIdIsEmpty()
        {
            var now = new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
            var guard = new AssetFlowLoopGuard(threshold: 2, rollingWindow: System.TimeSpan.FromSeconds(10), clock: () => now);
            var key = new AssetFlowLoopKey("asset", "config", AssetFlowStage.PostImport, "Handler");

            Assert.That(guard.ShouldRun(key, string.Empty), Is.True);
            Assert.That(guard.ShouldRun(key, string.Empty), Is.True);
            Assert.That(guard.ShouldRun(key, string.Empty), Is.False);

            now = now.AddSeconds(11);
            guard.Retry(key);

            Assert.That(guard.ShouldRun(key, string.Empty), Is.True);
        }

        private sealed class InMemoryPauseStore : ILoopGuardPauseStore
        {
            private readonly System.Collections.Generic.HashSet<AssetFlowLoopKey> keys =
                new System.Collections.Generic.HashSet<AssetFlowLoopKey>();

            public bool IsPaused(AssetFlowLoopKey key)
            {
                return keys.Contains(key);
            }

            public System.Collections.Generic.IReadOnlyList<AssetFlowLoopKey> GetPausedKeysForAsset(string assetGuid)
            {
                var result = new System.Collections.Generic.List<AssetFlowLoopKey>();
                foreach (var key in keys)
                {
                    if (key.AssetGuid == assetGuid)
                        result.Add(key);
                }

                return result;
            }

            public void Pause(AssetFlowLoopKey key)
            {
                keys.Add(key);
            }

            public void Retry(AssetFlowLoopKey key)
            {
                keys.Remove(key);
            }
        }
    }
}
