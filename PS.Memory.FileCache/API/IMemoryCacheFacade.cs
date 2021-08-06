using System;

namespace PS.Runtime.Caching.API
{
    public interface IMemoryCacheFacade
    {
        #region Members

        object Get(string key, string regionName);
        void Put(string key, string regionName, object item, DateTime absoluteExpiration);
        void Remove(string key, string regionName);

        #endregion
    }
}