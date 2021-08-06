using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using PS.Runtime.Caching.API;

namespace PS.Runtime.Caching.Default
{
    public class DefaultRepository : IRepository
    {
        #region Constants

        private static readonly CacheItemPolicy DefaultPolicy;
        private static readonly string MetadataFilename = "metadata";

        #endregion

        private readonly MemoryCache _hashCache;

        #region Constructors

        static DefaultRepository()
        {
            DefaultPolicy = new CacheItemPolicy
            {
                SlidingExpiration = TimeSpan.FromMinutes(5)
            };
        }

        public DefaultRepository(string root = null)
        {
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

            _hashCache = new MemoryCache("Hash cache");
        }

        #endregion

        #region Properties

        public string Root { get; }

        #endregion

        #region IRepository Members

        public virtual void DeleteFiles(string key, string region, IReadOnlyList<string> files)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var directory = GetKeyDirectory(key, region);
            foreach (var file in files)
            {
                File.Delete(Path.Combine(directory, file));
            }
        }

        public virtual IEnumerable<string> EnumerateFiles(string key, string region, string pattern)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var directory = GetKeyDirectory(key, region);

            return Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
                           .Select(Path.GetFileName)
                           .Where(f => !string.Equals(f, MetadataFilename))
                : Enumerable.Empty<string>().ToList();
        }

        public virtual IEnumerable<string> EnumerateKeys(string region)
        {
            var regionDirectory = GetRegionDirectory(region);
            if (!Directory.Exists(regionDirectory)) yield break;

            var directories = Directory.EnumerateDirectories(regionDirectory, "*", SearchOption.TopDirectoryOnly);
            foreach (var directory in directories)
            {
                var file = Path.Combine(directory, MetadataFilename);
                if (File.Exists(file)) yield return Encoding.UTF8.GetString(ReadBytesFromFile(file));
            }
        }

        public virtual IEnumerable<string> EnumerateRegions()
        {
            if (!Directory.Exists(Root)) yield break;

            var directories = Directory.EnumerateDirectories(Root, "*", SearchOption.TopDirectoryOnly);
            foreach (var directory in directories)
            {
                var file = Path.Combine(directory, MetadataFilename);
                if (File.Exists(file)) yield return Encoding.UTF8.GetString(ReadBytesFromFile(file));
            }
        }

        public virtual byte[] ReadFile(string key, string region, string filename)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var directory = GetKeyDirectory(key, region);
            var filePath = Path.Combine(directory, filename);

            return ReadBytesFromFile(filePath);
        }

        public virtual void WriteFile(string key, string region, string filename, byte[] bytes = null)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var directory = GetKeyDirectory(key, region);
            var filePath = Path.Combine(directory, filename);
            Exception error = null;
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    EnsureMetadata(key, region);

                    WriteBytesToFile(filePath, bytes);

                    return;
                }
                catch (IOException e)
                {
                    error = e;
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }

            if (error != null)
            {
                throw error;
            }
        }

        public virtual void UpdateLastAccessTime(string key, string region, string filename, DateTime time)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var directory = GetKeyDirectory(key, region);
            var filePath = Path.Combine(directory, filename);

            File.SetLastAccessTimeUtc(filePath, time);
        }

        public virtual DateTime GetLastAccessTime(string key, string region, string filename)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var directory = GetKeyDirectory(key, region);
            var filePath = Path.Combine(directory, filename);

            return File.GetLastAccessTimeUtc(filePath);
        }

        public virtual void MarkAsDeleted(string key, string region, string filename)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var directory = GetKeyDirectory(key, region);
            var filePath = Path.Combine(directory, filename);

            File.SetAttributes(filePath, FileAttributes.Offline);
        }

        public virtual bool IsDeleted(string key, string region, string filename)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var directory = GetKeyDirectory(key, region);
            var filePath = Path.Combine(directory, filename);

            return File.GetAttributes(filePath).HasFlag(FileAttributes.Offline);
        }

        #endregion

        #region Members

        protected virtual void EnsureMetadata(string key, string region)
        {
            lock (this)
            {
                var regionDirectory = GetRegionDirectory(region);
                if (!Directory.Exists(regionDirectory))
                {
                    Directory.CreateDirectory(regionDirectory);
                }

                var regionMetadataFilePath = Path.Combine(regionDirectory, MetadataFilename);
                if (!File.Exists(regionMetadataFilePath))
                {
                    var bytes = string.IsNullOrEmpty(region) ? null : Encoding.UTF8.GetBytes(region);
                    WriteBytesToFile(regionMetadataFilePath, bytes);
                }

                var keyDirectory = GetKeyDirectory(key, region);
                if (!Directory.Exists(keyDirectory))
                {
                    Directory.CreateDirectory(keyDirectory);
                }

                var keyMetadataFilePath = Path.Combine(keyDirectory, MetadataFilename);
                if (!File.Exists(keyMetadataFilePath))
                {
                    WriteBytesToFile(keyMetadataFilePath, Encoding.UTF8.GetBytes(key));
                }
            }
        }

        protected virtual string GetKeyDirectory(string key, string regionName)
        {
            return Path.Combine(GetRegionDirectory(regionName), Hash(key));
        }

        protected virtual string GetRegionDirectory(string regionName)
        {
            var regionHash = string.IsNullOrEmpty(regionName)
                ? "00000000000000000000000000000042"
                : Hash(regionName);

            return Path.Combine(Root, regionHash);
        }

        protected virtual string Hash(string input)
        {
            if (input == null)
            {
                input = "1DB24525-F535-4217-81AF-CBC952244DC1";
            }

            if (input.Length == 0)
            {
                input = "D7676DF9-79ED-45C6-876B-B7E5DFECAEE7";
            }

            if (_hashCache[input] is string result)
            {
                return result;
            }

            // Use input string to calculate MD5 hash
            using (var md5 = MD5.Create())
            {
                var inputBytes = Encoding.ASCII.GetBytes(input);
                var hashBytes = md5.ComputeHash(inputBytes);

                var hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty);
                var cacheItem = new CacheItem(input, hash);
                _hashCache.Add(cacheItem, DefaultPolicy);
                return hash;
            }
        }

        protected virtual byte[] ReadBytesFromFile(string filePath)
        {
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                memory.Seek(0, SeekOrigin.Begin);
                return memory.ToArray();
            }
        }

        protected virtual void WriteBytesToFile(string filePath, byte[] bytes)
        {
            var intermediatePath = filePath + ".progress";
            using (var stream = File.Open(intermediatePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                if (bytes != null)
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
            }

            File.Move(intermediatePath, filePath);
        }

        #endregion
    }
}