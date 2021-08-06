using System.Runtime.Caching;

namespace PS.Runtime.Caching.API
{
    public interface IDataSerializer
    {
        #region Members

        CacheItem DeserializeItem(byte[] data);
        byte[] SerializeItem(CacheItem item);

        #endregion
    }
}