using System.Runtime.Caching;

namespace PS.Runtime.Caching.API
{
    public interface ICacheEntry
    {
        #region Properties

        CacheItemPolicy Policy { get; }

        #endregion

        #region Members

        CacheItem GetCacheItem(IDataSerializer serializer);

        #endregion
    }
}