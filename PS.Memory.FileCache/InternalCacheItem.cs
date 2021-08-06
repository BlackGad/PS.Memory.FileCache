using System.Runtime.Caching;

namespace PS.Runtime.Caching
{
    internal class InternalCacheItem
    {
        #region Properties

        public CacheItem CacheItem { get; set; }
        public string Origin { get; set; }
        public CacheItemPolicy Policy { get; set; }

        #endregion
    }
}