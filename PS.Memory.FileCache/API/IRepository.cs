using System;
using System.Collections.Generic;

namespace PS.Runtime.Caching.API
{
    public interface IRepository
    {
        #region Members

        void DeleteFiles(string key, string region, IReadOnlyList<string> files);
        IEnumerable<string> EnumerateFiles(string key, string region, string pattern);
        IEnumerable<string> EnumerateKeys(string region);
        IEnumerable<string> EnumerateRegions();
        byte[] ReadFile(string key, string region, string filename);
        void WriteFile(string key, string region, string filename, byte[] bytes = null);
        void UpdateLastAccessTime(string key, string region, string filename, DateTime time);
        DateTime GetLastAccessTime(string key, string region, string filename);
        void MarkAsDeleted(string key, string region, string filename);
        bool IsDeleted(string key, string region, string filename);

        #endregion
    }
}