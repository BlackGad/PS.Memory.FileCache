using System;
using System.Runtime.Caching;
using PS.Runtime.Caching.API;

namespace PS.Runtime.Caching.Default
{
    public class DefaultMemoryCacheFacade : IMemoryCacheFacade
    {
        private readonly TimeSpan _maximumItemLifetime;
        private readonly MemoryCache _memoryCache;

        #region Constructors

        public DefaultMemoryCacheFacade(TimeSpan maximumItemLifetime)
        {
            _maximumItemLifetime = maximumItemLifetime;
            _memoryCache = new MemoryCache("FileCacheFastProxy");
        }

        #endregion

        #region IMemoryCacheFacade Members

        public object Get(string key, string regionName)
        {
            return _memoryCache.GetCacheItem(regionName + key)?.Value;
        }

        public void Put(string key, string regionName, object item, DateTime absoluteExpiration)
        {
            var now = DateTime.UtcNow;
            var memoryCacheItemPolicy = new CacheItemPolicy();

            if (now + _maximumItemLifetime < absoluteExpiration)
            {
                memoryCacheItemPolicy.AbsoluteExpiration = now + _maximumItemLifetime;
            }
            else
            {
                memoryCacheItemPolicy.AbsoluteExpiration = absoluteExpiration;
            }

            _memoryCache.Set(regionName + key, item, memoryCacheItemPolicy);
        }

        public void Remove(string key, string regionName)
        {
            _memoryCache.Remove(regionName + key);
        }

        #endregion
    }
}