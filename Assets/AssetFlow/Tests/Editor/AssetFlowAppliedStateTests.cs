using System;
using System.IO;
using AssetFlow.Editor.Core;
using NUnit.Framework;

namespace AssetFlow.Editor.Tests
{
    public sealed class AssetFlowAppliedStateTests
    {
        private string path;

        [SetUp]
        public void SetUp()
        {
            path = Path.Combine("Library", "AssetFlowTests", $"AppliedState_{Guid.NewGuid():N}.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        [Test]
        public void Load_ReturnsEmptyStateWhenFileDoesNotExist()
        {
            var store = new AssetFlowAppliedStateStore(path);

            var data = store.Load();

            Assert.That(data.configs, Is.Empty);
        }

        [Test]
        public void SaveAppliedSnapshot_InsertsAndUpdatesByConfigGuid()
        {
            var store = new AssetFlowAppliedStateStore(path);

            store.SaveAppliedSnapshot("config", "old", "{\"folder\":\"Assets\"}");
            store.SaveAppliedSnapshot("config", "new", null);

            var record = store.Find("config");
            Assert.That(record, Is.Not.Null);
            Assert.That(record.ruleHash, Is.EqualTo("new"));
            Assert.That(record.snapshotJson, Is.EqualTo(string.Empty));
            Assert.That(store.Load().configs, Has.Count.EqualTo(1));
        }

        [Test]
        public void Save_CreatesDirectoryAndPersistsEmptyStateWhenDataIsNull()
        {
            var store = new AssetFlowAppliedStateStore(path);

            store.Save(null);

            Assert.That(File.Exists(path), Is.True);
            Assert.That(store.Load().configs, Is.Empty);
        }
    }
}
