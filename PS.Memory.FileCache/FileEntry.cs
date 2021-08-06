using System;
using System.Linq;
using System.Runtime.Caching;
using PS.Runtime.Caching.Extensions;

namespace PS.Runtime.Caching
{
    internal class FileEntry
    {
        #region Constants

        public static readonly string CacheExtension = "cache";

        #endregion

        #region Static members

        public static string CreateFilename(DateTime timestamp, CacheItemPolicy source)
        {
            var seed = Guid.NewGuid().ToString("N").Substring(0, 4);
            return string.Join(".",
                               timestamp.DateTimeToSpecial(),
                               source.SlidingExpiration.TimeSpanToSpecial(),
                               source.AbsoluteExpiration.UtcDateTime.DateTimeToSpecial(),
                               (int)source.Priority,
                               seed,
                               CacheExtension
            );
        }

        public static FileEntry Parse(string file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            var parts = file.Split('.');
            var extension = parts.Last();

            if (!string.Equals(extension, CacheExtension))
            {
                throw new InvalidOperationException("Invalid extension");
            }

            return new FileEntry
            {
                File = file,
                Timestamp = parts[0].DateTimeFromSpecial(),
                Policy = new CacheItemPolicy()
                {
                    SlidingExpiration = parts[1].TimeSpanFromSpecial(),
                    AbsoluteExpiration = parts[2].DateTimeFromSpecial(),
                    Priority = (CacheItemPriority)int.Parse(parts[3])
                },
                Extension = extension
            };
        }

        #endregion

        #region Properties

        public string Extension { get; private set; }
        public string File { get; private set; }
        public CacheItemPolicy Policy { get; private set; }
        public DateTime Timestamp { get; private set; }

        #endregion
    }
}