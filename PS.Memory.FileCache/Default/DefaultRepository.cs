using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Caching;
using System.Runtime.Serialization;
using System.Threading;
using PS.Runtime.Caching.API;
using PS.Runtime.Caching.Extensions;

namespace PS.Runtime.Caching.Default
{
    public class DefaultRepository : IRepository,
                                     IDisposable
    {
        #region Constants

        public static readonly string CacheExtension = "cache";

        #endregion

        #region Static members

        public static CacheItemPolicy DeserializeCacheItemPolicy(string filename)
        {
            var parts = filename.Split('.');
            if (parts.Length != 4)
            {
                var message = $"Invalid filename. Expected dot separated string in format: <timestamp>.<seed>.<policy>.{CacheExtension}";
                throw new SerializationException(message);
            }

            return parts[2].DeserializeCacheItemPolicy();
        }

        public static string SerializeCacheItemPolicy(CacheItemPolicy cacheItemPolicy)
        {
            var filename = string.Join(".",
                                       DateTime.UtcNow.DateTimeToSpecial(),
                                       Guid.NewGuid().ToString("N").Substring(0, 4),
                                       cacheItemPolicy.SerializeCacheItemPolicy(),
                                       CacheExtension
            );
            return filename;
        }

        #endregion

        private readonly object _cleanupLocker;

        private readonly CleanupSettings _cleanupSettings;

        private readonly bool _sanitizeNames;
        private readonly Timer _timer;

        #region Constructors

        public DefaultRepository(string root = null, bool sanitizeNames = true, CleanupSettings cleanupSettings = null)
        {
            _sanitizeNames = sanitizeNames;
            _cleanupSettings = cleanupSettings ?? CleanupSettings.Default;
            _cleanupLocker = new object();

            if (root == null)
            {
                var entryLocation = Assembly.GetExecutingAssembly().Location;
                var entryDirectory = Path.GetDirectoryName(entryLocation) ?? Environment.CurrentDirectory;
                Root = Path.Combine(entryDirectory, "Cache");
            }
            else
            {
                Root = root;
            }

            if (_cleanupSettings.CleanupPeriod != TimeSpan.MaxValue)
            {
                _timer = new Timer(CleanupTimer, null, TimeSpan.Zero, TimeSpan.Zero);
            }
        }

        #endregion

        #region Properties

        protected string Root { get; }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                Cleanup();
            }
        }

        #endregion

        #region IRepository Members

        public virtual IEnumerable<string> EnumerateKeys(string region)
        {
            var regionDirectory = new DirectoryInfo(GetRegionDirectory(region));

            if (!regionDirectory.Exists) yield break;
            var directories = regionDirectory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly);
            foreach (var directory in directories)
            {
                var name = directory.Name;
                if (_sanitizeNames)
                {
                    name = WebUtility.UrlDecode(name);
                }

                yield return name;
            }
        }

        public virtual IEnumerable<string> EnumerateRegions()
        {
            var rootDirectory = new DirectoryInfo(Root);
            if (!rootDirectory.Exists) yield break;

            var directories = rootDirectory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly);
            foreach (var directory in directories)
            {
                var name = directory.Name;
                if (_sanitizeNames)
                {
                    name = WebUtility.UrlDecode(name);
                }

                yield return name;
            }
        }

        public virtual void Delete(ICacheEntry entry)
        {
            var cacheEntry = CastEntry(entry);
            cacheEntry.File.Refresh();

            if (cacheEntry.File.Exists)
            {
                cacheEntry.File.Attributes = FileAttributes.Offline;
            }
        }

        public virtual ICacheEntry Read(string key, string region, DateTime time)
        {
            var directory = GetKeyDirectory(key, region);
            if (!Directory.Exists(directory))
            {
                return null;
            }

            var pattern = $"*.*.{CacheExtension}";
            var mostRecentFile = Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
                                          .OrderByDescending(s => s)
                                          .FirstOrDefault();
            if (mostRecentFile == null)
            {
                //Data file missed
                return null;
            }

            var file = new FileInfo(mostRecentFile);
            if (file.Attributes.HasFlag(FileAttributes.Offline))
            {
                //Data file marked as deleted
                return null;
            }

            var policy = DeserializeCacheItemPolicy(file.Name);

            var now = DateTime.UtcNow;
            var lastAccessTime = policy.SlidingExpiration != ObjectCache.NoSlidingExpiration
                ? file.LastAccessTimeUtc
                : now;

            var expirationTime = policy.CalculateExpiration(lastAccessTime);
            if (expirationTime < now)
            {
                //Item expired
                return null;
            }

            var bytes = ReadBytesFromFile(file);
            return new CacheEntry(file, bytes, policy);
        }

        public virtual void UpdateAccessTime(ICacheEntry entry, DateTime time)
        {
            var cacheEntry = CastEntry(entry);
            cacheEntry.File.Refresh();

            if (cacheEntry.File.Exists)
            {
                cacheEntry.File.LastAccessTimeUtc = time;
            }
        }

        public virtual ICacheEntry Write(string key, string region, byte[] bytes, CacheItemPolicy cacheItemPolicy)
        {
            var directory = GetKeyDirectory(key, region);
            var filename = SerializeCacheItemPolicy(cacheItemPolicy);

            var file = new FileInfo(Path.Combine(directory, filename));
            if (file.Directory?.Exists == false)
            {
                file.Directory.Create();
            }

            RetryPolicy(() => WriteBytesToFile(file, bytes), 3, TimeSpan.FromSeconds(1));

            return new CacheEntry(file, bytes, cacheItemPolicy);
        }

        public virtual void Cleanup()
        {
            lock (_cleanupLocker)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var regions = EnumerateRegions();
                    var guarantyFileLifetimePeriod = _cleanupSettings.GuarantyFileLifetimePeriod;
                    foreach (var region in regions)
                    {
                        var regionDirectory = GetRegionDirectory(region);
                        foreach (var key in EnumerateKeys(region))
                        {
                            var keyDirectory = GetKeyDirectory(key, region);
                            if (!Directory.Exists(keyDirectory))
                            {
                                continue;
                            }

                            var pattern = $"*.*.{CacheExtension}";
                            var files = Directory.EnumerateFiles(keyDirectory, pattern, SearchOption.TopDirectoryOnly)
                                                 .OrderByDescending(s => s)
                                                 .ToList();

                            var mostRecentFile = files.FirstOrDefault();
                            if (mostRecentFile == null)
                            {
                                continue;
                            }

                            var obsoleteFiles = files.Skip(1).ToList();

                            var file = new FileInfo(mostRecentFile);
                            if (file.Attributes.HasFlag(FileAttributes.Offline))
                            {
                                obsoleteFiles.Add(file.FullName);
                            }

                            var policy = DeserializeCacheItemPolicy(file.Name);

                            var lastAccessTime = policy.SlidingExpiration != ObjectCache.NoSlidingExpiration
                                ? file.LastAccessTimeUtc
                                : now;

                            var expirationTime = policy.CalculateExpiration(lastAccessTime);
                            if (expirationTime + guarantyFileLifetimePeriod < now)
                            {
                                //Item expired
                                obsoleteFiles.Add(file.FullName);
                            }

                            if (obsoleteFiles.Any())
                            {
                                foreach (var obsoleteFile in obsoleteFiles)
                                {
                                    CleanupFile(obsoleteFile);
                                }
                            }

                            CleanupDirectory(keyDirectory);
                        }

                        CleanupDirectory(regionDirectory);
                    }
                }
                catch
                {
                    //Nothing
                }
            }
        }

        #endregion

        #region Members

        protected virtual CacheEntry CastEntry(ICacheEntry entry)
        {
            if (entry is CacheEntry cacheEntry)
            {
                return cacheEntry;
            }

            throw new InvalidCastException($"Current repository cache entries must be inherited from {typeof(CacheEntry)}");
        }

        protected virtual void CleanupDirectory(string directory)
        {
            try
            {
                var remainingFiles = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly);
                if (!remainingFiles.Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch
            {
                //Nothing
            }
        }

        protected virtual void CleanupFile(string file)
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                //Nothing
            }
        }

        protected virtual string GetKeyDirectory(string key, string regionName)
        {
            if (_sanitizeNames)
            {
                key = WebUtility.UrlEncode(key) ?? key;
            }

            return Path.Combine(GetRegionDirectory(regionName), key);
        }

        protected virtual string GetRegionDirectory(string regionName)
        {
            regionName = string.IsNullOrWhiteSpace(regionName) ? "Default" : regionName;

            if (_sanitizeNames)
            {
                regionName = WebUtility.UrlEncode(regionName) ?? regionName;
            }

            return Path.Combine(Root, regionName);
        }

        protected virtual byte[] ReadBytesFromFile(FileInfo file)
        {
            using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                memory.Seek(0, SeekOrigin.Begin);
                return memory.ToArray();
            }
        }

        protected virtual void WriteBytesToFile(FileInfo file, byte[] bytes)
        {
            var intermediatePath = file.FullName + ".progress";
            using (var stream = File.Open(intermediatePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
            }

            File.Move(intermediatePath, file.FullName);
            file.LastAccessTimeUtc = DateTime.UtcNow;
        }

        private void CleanupTimer(object state)
        {
            Cleanup();
            _timer.Change(_cleanupSettings.CleanupPeriod, TimeSpan.Zero);
        }

        private void RetryPolicy(Action action, int attempts, TimeSpan sleep)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            Exception error = null;
            for (var i = 0; i < attempts; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (IOException e)
                {
                    error = e;
                    Thread.Sleep(sleep);
                }
            }

            if (error != null)
            {
                throw error;
            }
        }

        #endregion

        #region Nested type: CacheEntry

        public class CacheEntry : ICacheEntry
        {
            private CacheItem _cacheItem;

            #region Constructors

            public CacheEntry(FileInfo file, byte[] data, CacheItemPolicy policy)
            {
                File = file;
                Data = data;
                Policy = policy;
            }

            #endregion

            #region Properties

            public byte[] Data { get; }
            public FileInfo File { get; }

            #endregion

            #region ICacheEntry Members

            public CacheItemPolicy Policy { get; }

            public CacheItem GetCacheItem(IDataSerializer serializer)
            {
                lock (this)
                {
                    return _cacheItem ?? (_cacheItem = serializer.DeserializeItem(Data));
                }
            }

            #endregion
        }

        #endregion
    }
}