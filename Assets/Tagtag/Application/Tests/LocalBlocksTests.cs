using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tagtag.Services;

namespace Tagtag.Tests
{
    public sealed class LocalBlocksTests
    {
        [Test]
        public void OfflineBlocksPersistOnlyForTheirAccount()
        {
            string uid = "test-" + Guid.NewGuid().ToString("N");
            var storage = new LocalBlocks();
            try
            {
                storage.Save(uid, new HashSet<string> { "blocked-author" });
                Assert.That(new LocalBlocks().Read(uid), Does.Contain("blocked-author"));
                Assert.That(new LocalBlocks().Read(uid + "-other"), Is.Empty);
                storage.Remove(uid);
                Assert.That(storage.Read(uid), Is.Empty);
            }
            finally { storage.Remove(uid); }
        }
    }
}
