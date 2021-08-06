using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PS.Runtime.Caching.Default;

namespace PS.Runtime.Caching.Tests
{
    [TestFixture]
    public class FileCacheTests
    {
        #region Members

        [Test]
        public void AbsoluteExpirationTest()
        {
            var cacheKey = "test";
            var expectedValue = 42;

            var cleanupSettings = new CleanupSettings
            {
                GuarantyFileLifetimePeriod = null,
                CleanupPeriod = TimeSpan.MaxValue
            };

            var repository = new DefaultRepository(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

            using (var cache1 = new FileCache(repository: repository, cleanupSettings: cleanupSettings))
            using (var cache2 = new FileCache(repository: repository, cleanupSettings: cleanupSettings))
            {
                var policy = new CacheItemPolicy
                {
                    AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromSeconds(1)
                };

                cache1.Set(cacheKey, expectedValue, policy);

                Assert.AreEqual(expectedValue, cache1.Get("test"));
                Assert.AreEqual(expectedValue, cache2.Get("test"));

                Thread.Sleep(2000);

                Assert.AreEqual(null, cache1.Get("test"));
                Assert.AreEqual(null, cache2.Get("test"));
            }
        }

        [Test]
        public void HighPressureAbsoluteExpirationTest()
        {
            var items = new Dictionary<int, object>();
            var random = new Random();
            var policy = new CacheItemPolicy
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(100)
            };

            for (var i = 0; i < 20; i++)
            {
                items.Add(i, Guid.NewGuid());
            }

            var repository = new DefaultRepository(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

            using (var cache1 = new FileCache(repository: repository))
            using (var cache2 = new FileCache(repository: repository))
            {
                var tasks = new List<Task>();
                for (var i = 0; i < 20; i++)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        var key = random.Next(items.Count);
                        var expectedValue = items[key];

                        // ReSharper disable once AccessToDisposedClosure
                        var actual = cache1.AddOrGetExisting(key.ToString(), expectedValue, policy);

                        Assert.AreEqual(expectedValue, actual);
                    }));

                    tasks.Add(Task.Run(() =>
                    {
                        var key = random.Next(items.Count);
                        var expectedValue = items[key];

                        // ReSharper disable once AccessToDisposedClosure
                        var actual = cache2.AddOrGetExisting(key.ToString(), expectedValue, policy);

                        Assert.AreEqual(expectedValue, actual);
                    }));
                }

                Task.WaitAll(tasks.ToArray());
            }
        }

        [Test]
        public void HighPressureSlidingExpirationTest()
        {
            var items = new Dictionary<int, object>();
            var random = new Random();
            var policy = new CacheItemPolicy
            {
                SlidingExpiration = TimeSpan.FromMilliseconds(100)
            };

            for (var i = 0; i < 20; i++)
            {
                items.Add(i, Guid.NewGuid());
            }

            var repository = new DefaultRepository(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

            using (var cache1 = new FileCache(repository: repository))
            using (var cache2 = new FileCache(repository: repository))
            {
                var tasks = new List<Task>();
                for (var i = 0; i < 20; i++)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        var key = random.Next(items.Count);
                        var expectedValue = items[key];

                        // ReSharper disable once AccessToDisposedClosure
                        var actual = cache1.AddOrGetExisting(key.ToString(), expectedValue, policy);

                        Assert.AreEqual(expectedValue, actual);
                    }));

                    tasks.Add(Task.Run(() =>
                    {
                        var key = random.Next(items.Count);
                        var expectedValue = items[key];

                        // ReSharper disable once AccessToDisposedClosure
                        var actual = cache2.AddOrGetExisting(key.ToString(), expectedValue, policy);

                        Assert.AreEqual(expectedValue, actual);
                    }));
                }

                Task.WaitAll(tasks.ToArray());
            }
        }

        [Test]
        public void NotRemovableAbsoluteExpirationTest()
        {
            var cacheKey = "test";
            var expectedValue = 42;

            var cleanupSettings = new CleanupSettings
            {
                GuarantyFileLifetimePeriod = null,
                CleanupPeriod = TimeSpan.MaxValue
            };

            var repository = new DefaultRepository(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

            using (var cache1 = new FileCache(repository: repository, cleanupSettings: cleanupSettings))
            using (var cache2 = new FileCache(repository: repository, cleanupSettings: cleanupSettings))
            {
                var policy = new CacheItemPolicy
                {
                    Priority = CacheItemPriority.NotRemovable,
                    AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromSeconds(1)
                };

                cache1.Set(cacheKey, expectedValue, policy);

                Assert.AreEqual(expectedValue, cache1.Get("test"));
                Assert.AreEqual(expectedValue, cache2.Get("test"));

                cache1.Remove(cacheKey);

                Assert.AreEqual(expectedValue, cache1.Get("test"));
                Assert.AreEqual(expectedValue, cache2.Get("test"));

                Thread.Sleep(2000);

                Assert.AreEqual(expectedValue, cache1.Get("test"));
                Assert.AreEqual(expectedValue, cache2.Get("test"));
            }
        }

        [Test]
        public void NotRemovableSlidingExpirationTest()
        {
            var cacheKey = "test";
            var expectedValue = 42;

            var cleanupSettings = new CleanupSettings
            {
                GuarantyFileLifetimePeriod = null,
                CleanupPeriod = TimeSpan.MaxValue
            };

            var repository = new DefaultRepository(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

            using (var cache1 = new FileCache(repository: repository, cleanupSettings: cleanupSettings))
            using (var cache2 = new FileCache(repository: repository, cleanupSettings: cleanupSettings))
            {
                var policy = new CacheItemPolicy
                {
                    Priority = CacheItemPriority.NotRemovable,
                    SlidingExpiration = TimeSpan.FromSeconds(1)
                };

                cache1.Set(cacheKey, expectedValue, policy);

                Assert.AreEqual(expectedValue, cache1.Get("test"));
                Assert.AreEqual(expectedValue, cache2.Get("test"));

                cache1.Remove(cacheKey);

                Assert.AreEqual(expectedValue, cache1.Get("test"));
                Assert.AreEqual(expectedValue, cache2.Get("test"));

                Thread.Sleep(2000);

                Assert.AreEqual(expectedValue, cache1.Get("test"));
                Assert.AreEqual(expectedValue, cache2.Get("test"));
            }
        }

        [Test]
        public void SlidingExpirationTest()
        {
            var cacheKey = "test";
            var expectedValue = 42;

            var cleanupSettings = new CleanupSettings
            {
                GuarantyFileLifetimePeriod = null,
                CleanupPeriod = TimeSpan.MaxValue
            };

            var repository = new DefaultRepository(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

            using (var cache1 = new FileCache(repository: repository, cleanupSettings: cleanupSettings))
            using (var cache2 = new FileCache(repository: repository, cleanupSettings: cleanupSettings))
            {
                var policy = new CacheItemPolicy
                {
                    SlidingExpiration = TimeSpan.FromSeconds(2)
                };

                cache1.Set(cacheKey, expectedValue, policy);

                Assert.AreEqual(expectedValue, cache1.Get("test"));
                Assert.AreEqual(expectedValue, cache2.Get("test"));

                Thread.Sleep(1000);

                Assert.AreEqual(expectedValue, cache1.Get("test"));
                Assert.AreEqual(expectedValue, cache2.Get("test"));

                Thread.Sleep(3000);

                Assert.AreEqual(null, cache1.Get("test"));
                Assert.AreEqual(null, cache2.Get("test"));
            }
        }

        #endregion
    }
}