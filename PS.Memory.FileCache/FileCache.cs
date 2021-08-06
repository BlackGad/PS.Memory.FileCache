using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Threading;
using PS.Runtime.Caching.API;
using PS.Runtime.Caching.Default;
using PS.Runtime.Caching.Extensions;

namespace PS.Runtime.Caching
{
    public class FileCache : ObjectCache,
                             IDisposable
    {
        #region Static members

        private static DateTime CalculateExpiration(CacheItemPolicy policy, DateTime? lastAccessTime)
        {
            if (policy.Priority == CacheItemPriority.NotRemovable)
            {
                return InfiniteAbsoluteExpiration.UtcDateTime;
            }

            if (policy.AbsoluteExpiration != InfiniteAbsoluteExpiration)
            {
                return policy.AbsoluteExpiration.UtcDateTime;
            }

            if (lastAccessTime.HasValue && policy.SlidingExpiration != NoSlidingExpiration)
            {
                return lastAccessTime.Value + policy.SlidingExpiration;
            }

            return InfiniteAbsoluteExpiration.UtcDateTime;
        }

        #endregion

        private readonly TimeSpan? _cleanupPeriod;
        private readonly TimeSpan? _guarantyFileLifetimePeriod;
        private readonly IMemoryCacheFacade _memoryCacheFacade;
        private readonly IDataSerializer _serializer;
        private readonly Timer _timer;

        #region Constructors

        public FileCache(string name = null,
                         IRepository repository = null,
                         IDataSerializer serializer = null,
                         IMemoryCacheFacade memoryCacheFacade = null,
                         CleanupSettings cleanupSettings = null)
        {
            Name = name;

            DefaultCacheCapabilities = DefaultCacheCapabilities.AbsoluteExpirations |
                                       DefaultCacheCapabilities.SlidingExpirations |
                                       DefaultCacheCapabilities.InMemoryProvider |
                                       DefaultCacheCapabilities.CacheRegions;

            Repository = repository ?? new DefaultRepository();
            _memoryCacheFacade = memoryCacheFacade ?? new DefaultMemoryCacheFacade(TimeSpan.FromMinutes(10));
            _serializer = serializer ?? new DefaultDataSerializer();

            cleanupSettings = cleanupSettings ?? new CleanupSettings();

            _cleanupPeriod = cleanupSettings.CleanupPeriod;
            _guarantyFileLifetimePeriod = cleanupSettings.GuarantyFileLifetimePeriod;

            if (_cleanupPeriod.HasValue)
            {
                _timer = new Timer(CleanupTimer, null, TimeSpan.Zero, TimeSpan.Zero);
            }
        }

        #endregion

        #region Properties

        public override DefaultCacheCapabilities DefaultCacheCapabilities { get; }

        public override object this[string key]
        {
            get { return Get(key); }
            set { Set(key, value, InfiniteAbsoluteExpiration); }
        }

        public override string Name { get; }

        public IRepository Repository { get; }

        #endregion

        #region Override members

        public override CacheItem GetCacheItem(string key, string regionName = null)
        {
            if (_memoryCacheFacade.Get(key, regionName) is InternalCacheItem item)
            {
                //Update access time only for items with sliding expiration
                if (item.Policy.SlidingExpiration != NoSlidingExpiration)
                {
                    Repository.UpdateLastAccessTime(key, regionName, item.Origin, DateTime.UtcNow);
                }

                return item.CacheItem;
            }

            return GetInternalCacheItem(key, regionName)?.CacheItem;
        }

        protected override IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return GetValues(GetKeys(null)).GetEnumerator();
        }

        public override CacheEntryChangeMonitor CreateCacheEntryChangeMonitor(IEnumerable<string> keys, string regionName = null)
        {
            throw new NotSupportedException();
        }

        public override IDictionary<string, object> GetValues(IEnumerable<string> keys, string regionName = null)
        {
            var result = new Dictionary<string, object>();
            foreach (var key in keys.Enumerate().Distinct())
            {
                var item = GetCacheItem(key, regionName);
                if (item == null) continue;

                result.Add(key, item.Value);
            }

            return result;
        }

        public override object AddOrGetExisting(string key, object value, CacheItemPolicy policy, string regionName = null)
        {
            return AddOrGetExisting(new CacheItem(key, value, regionName), policy).Value;
        }

        public override object Get(string key, string regionName = null)
        {
            return GetCacheItem(key, regionName)?.Value;
        }

        public override bool Contains(string key, string regionName = null)
        {
            return GetCacheItem(key, regionName) != null;
        }

        public override void Set(string key, object value, CacheItemPolicy policy, string regionName = null)
        {
            Set(new CacheItem(key, value, regionName), policy);
        }

        public override void Set(string key, object value, DateTimeOffset absoluteExpiration, string regionName = null)
        {
            var policy = new CacheItemPolicy
            {
                AbsoluteExpiration = absoluteExpiration
            };
            Set(new CacheItem(key, value, regionName), policy);
        }

        public override object AddOrGetExisting(string key, object value, DateTimeOffset absoluteExpiration, string regionName = null)
        {
            return AddOrGetExisting(new CacheItem(key, value, regionName),
                                    new CacheItemPolicy
                                    {
                                        AbsoluteExpiration = absoluteExpiration
                                    });
        }

        public override CacheItem AddOrGetExisting(CacheItem value, CacheItemPolicy policy)
        {
            var existing = GetCacheItem(value.Key, value.RegionName);
            if (existing != null) return existing;

            Set(value, policy);
            return value;
        }

        public override long GetCount(string regionName = null)
        {
            return GetValues(GetKeys(regionName), regionName).Count;
        }

        public override void Set(CacheItem item, CacheItemPolicy policy)
        {
            var now = DateTime.UtcNow;
            var origin = FileEntry.CreateFilename(now, policy);
            var internalCacheItem = new InternalCacheItem
            {
                CacheItem = item,
                Policy = policy,
                Origin = origin
            };

            var data = _serializer.SerializeItem(item);

            Repository.WriteFile(item.Key, item.RegionName, origin, data);

            if (policy.SlidingExpiration != NoSlidingExpiration)
            {
                Repository.UpdateLastAccessTime(item.Key, item.RegionName, origin, now);
            }

            var expiration = CalculateExpiration(policy, now);
            _memoryCacheFacade.Put(item.Key, item.RegionName, internalCacheItem, expiration);
        }

        public override object Remove(string key, string regionName = null)
        {
            var existingItem = GetInternalCacheItem(key, regionName);
            if (existingItem == null || existingItem.Policy.Priority == CacheItemPriority.NotRemovable) return null;

            Repository.MarkAsDeleted(key, regionName, existingItem.Origin);

            _memoryCacheFacade.Remove(key, regionName);

            return existingItem.CacheItem.Value;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            _timer.Dispose();
            Cleanup();
        }

        #endregion

        #region Members

        /// <summary>
        ///     Cleanups saved files
        /// </summary>
        public void Cleanup()
        {
            try
            {
                var now = DateTime.UtcNow;
                var regions = Repository.EnumerateRegions();
                var guarantyFileLifetimePeriod = _guarantyFileLifetimePeriod ?? TimeSpan.Zero;
                foreach (var region in regions)
                {
                    foreach (var key in Repository.EnumerateKeys(region))
                    {
                        var files = ScanForFiles(key, region);
                        var entry = SelectValidCacheEntry(key, region, files, now);
                        var obsoleteFiles = files.Except(new[] { entry })
                                                 .Where(f => f.Timestamp + guarantyFileLifetimePeriod < now)
                                                 .Select(f => f.File)
                                                 .ToList();
                        if (obsoleteFiles.Any())
                        {
                            Repository.DeleteFiles(key, region, obsoleteFiles);
                        }
                    }
                }
            }
            catch
            {
                //Nothing
            }
        }

        /// <summary>
        ///     Marks all cached items as deleted
        /// </summary>
        public void Reset()
        {
            var regions = Repository.EnumerateRegions();

            foreach (var region in regions)
            {
                foreach (var key in Repository.EnumerateKeys(region))
                {
                    Remove(key, region);
                }
            }
        }

        private void CleanupTimer(object state)
        {
            Cleanup();

            // ReSharper disable once PossibleInvalidOperationException
            _timer.Change(_cleanupPeriod.Value, TimeSpan.Zero);
        }

        private InternalCacheItem GetInternalCacheItem(string key, string regionName)
        {
            try
            {
                var now = DateTime.UtcNow;
                var files = ScanForFiles(key, regionName);
                var entry = SelectValidCacheEntry(key, regionName, files, now);

                if (entry == null)
                {
                    //Entry cannot be created in current state
                    return null;
                }

                var data = Repository.ReadFile(key, regionName, entry.File);
                var cacheItem = _serializer.DeserializeItem(data);

                if (entry.Policy.SlidingExpiration != NoSlidingExpiration)
                {
                    Repository.UpdateLastAccessTime(key, regionName, entry.File, now);
                }

                var expiration = CalculateExpiration(entry.Policy, now);
                var internalCacheItem = new InternalCacheItem
                {
                    CacheItem = cacheItem,
                    Policy = entry.Policy,
                    Origin = entry.File
                };
                _memoryCacheFacade.Put(key, regionName, internalCacheItem, expiration);

                return internalCacheItem;
            }
            catch
            {
                return null;
            }
        }

        private IEnumerable<string> GetKeys(string regionName)
        {
            return Repository.EnumerateKeys(regionName);
        }

        private IReadOnlyList<FileEntry> ScanForFiles(string key, string regionName)
        {
            return Repository.EnumerateFiles(key, regionName, "*." + FileEntry.CacheExtension).Select(FileEntry.Parse).ToList();
        }

        private FileEntry SelectValidCacheEntry(string key, string regionName, IReadOnlyList<FileEntry> files, DateTime now)
        {
            var entry = files.OrderByDescending(f => f.Timestamp).FirstOrDefault();
            if (entry == null)
            {
                //Data file missed
                return null;
            }

            if (Repository.IsDeleted(key, regionName, entry.File))
            {
                //Data file marked as deleted
                return null;
            }

            DateTime? lastAccessTime = null;

            if (entry.Policy.SlidingExpiration != NoSlidingExpiration)
            {
                lastAccessTime = Repository.GetLastAccessTime(key, regionName, entry.File);
            }

            var expirationTime = CalculateExpiration(entry.Policy, lastAccessTime);
            if (expirationTime < now)
            {
                //Item expired
                return null;
            }

            return entry;
        }

        #endregion
    }
}