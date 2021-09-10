using System;
using System.Collections.Generic;
using System.Runtime.Caching;

namespace PS.Runtime.Caching.API
{
    public interface IRepository
    {
        #region Members

        void Cleanup();
        void Delete(ICacheEntry entry);
        IEnumerable<string> EnumerateKeys(string region);
        IEnumerable<string> EnumerateRegions();
        ICacheEntry Read(string key, string region, DateTime time);
        void UpdateAccessTime(ICacheEntry entry, DateTime time);
        ICacheEntry Write(string key, string region, byte[] bytes, CacheItemPolicy cacheItemPolicy);

        #endregion
    }
}