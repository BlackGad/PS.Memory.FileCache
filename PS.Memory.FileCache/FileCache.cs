using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using PS.Runtime.Caching.API;
using PS.Runtime.Caching.Default;
using PS.Runtime.Caching.Extensions;

namespace PS.Runtime.Caching
{
    public class FileCache : ObjectCache
    {
        private readonly IMemoryCacheFacade _memoryCacheFacade;
        private readonly IRepository _repository;
        private readonly IDataSerializer _serializer;

        #region Constructors

        public FileCache(IRepository repository,
                         string name = null,
                         IDataSerializer serializer = null,
                         IMemoryCacheFacade memoryCacheFacade = null)
        {
            Name = name;

            DefaultCacheCapabilities = DefaultCacheCapabilities.AbsoluteExpirations |
                                       DefaultCacheCapabilities.SlidingExpirations |
                                       DefaultCacheCapabilities.InMemoryProvider |
                                       DefaultCacheCapabilities.CacheRegions;

            _repository = repository ?? new DefaultRepository();
            _memoryCacheFacade = memoryCacheFacade ?? new DefaultMemoryCacheFacade();
            _serializer = serializer ?? new DefaultDataSerializer();
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

        #endregion

        #region Override members

        public override CacheItem GetCacheItem(string key, string regionName = null)
        {
            if (_memoryCacheFacade.Get(key, regionName) is ICacheEntry entry)
            {
                //Update access time only for items with sliding expiration
                if (entry.Policy.SlidingExpiration != NoSlidingExpiration)
                {
                    _repository.UpdateAccessTime(entry, DateTime.UtcNow);
                }

                return entry.GetCacheItem(_serializer);
            }

            return GetCacheEntry(key, regionName)?.GetCacheItem(_serializer);
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
            var data = _serializer.SerializeItem(item);
            var entry = _repository.Write(item.Key, item.RegionName, data, policy);

            if (policy.SlidingExpiration != NoSlidingExpiration)
            {
                _repository.UpdateAccessTime(entry, now);
            }

            var expiration = policy.CalculateExpiration(now);
            _memoryCacheFacade.Put(item.Key, item.RegionName, entry, expiration);
        }

        public override object Remove(string key, string regionName = null)
        {
            var entry = GetCacheEntry(key, regionName);
            if (entry == null || entry.Policy.Priority == CacheItemPriority.NotRemovable) return null;

            _repository.Delete(entry);

            _memoryCacheFacade.Remove(key, regionName);

            return entry.GetCacheItem(_serializer).Value;
        }

        #endregion

        #region Members

        /// <summary>
        ///     Marks all cached items as deleted
        /// </summary>
        public void Clear()
        {
            var regions = _repository.EnumerateRegions();

            foreach (var region in regions)
            {
                foreach (var key in _repository.EnumerateKeys(region))
                {
                    Remove(key, region);
                }
            }
        }

        private ICacheEntry GetCacheEntry(string key, string regionName)
        {
            try
            {
                var now = DateTime.UtcNow;
                var entry = _repository.Read(key, regionName, now);
                if (entry == null)
                {
                    //Entry is not exist or expired
                    return null;
                }

                if (entry.Policy.SlidingExpiration != NoSlidingExpiration)
                {
                    _repository.UpdateAccessTime(entry, now);
                }

                var expiration = entry.Policy.CalculateExpiration(now);
                _memoryCacheFacade.Put(key, regionName, entry, expiration);

                return entry;
            }
            catch
            {
                return null;
            }
        }

        private IEnumerable<string> GetKeys(string regionName)
        {
            return _repository.EnumerateKeys(regionName);
        }

        #endregion
    }
}