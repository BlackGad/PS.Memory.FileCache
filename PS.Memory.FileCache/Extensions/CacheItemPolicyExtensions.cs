using System;
using System.Runtime.Caching;
using System.Runtime.Serialization;

namespace PS.Runtime.Caching.Extensions
{
    public static class CacheItemPolicyExtensions
    {
        #region Static members

        public static DateTime CalculateExpiration(this CacheItemPolicy policy, DateTime lastAccessTime)
        {
            if (policy.Priority == CacheItemPriority.NotRemovable)
            {
                return ObjectCache.InfiniteAbsoluteExpiration.UtcDateTime;
            }

            if (policy.AbsoluteExpiration != ObjectCache.InfiniteAbsoluteExpiration)
            {
                return policy.AbsoluteExpiration.UtcDateTime;
            }

            if (policy.SlidingExpiration != ObjectCache.NoSlidingExpiration)
            {
                return lastAccessTime + policy.SlidingExpiration;
            }

            return ObjectCache.InfiniteAbsoluteExpiration.UtcDateTime;
        }

        public static CacheItemPolicy DeserializeCacheItemPolicy(this string policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (policy.Length < 2)
            {
                throw new SerializationException("Policy string cannot be deserialized");
            }

            var result = new CacheItemPolicy();
            var prefix = policy.Substring(0, 2);
            var extra = policy.Substring(2);
            switch (prefix)
            {
                case "NR":
                    result.Priority = CacheItemPriority.NotRemovable;
                    break;
                case "AE":
                    var time = extra.DateTimeFromSpecial();
                    result.AbsoluteExpiration = DateTime.SpecifyKind(time, DateTimeKind.Utc);
                    break;
                case "SE":
                    var span = extra.TimeSpanFromSpecial();
                    result.SlidingExpiration = span;
                    break;
                case "IN":
                    break;
                default:
                    throw new SerializationException("Policy string cannot be deserialized. Unknown mode.");
            }

            return result;
        }

        public static string SerializeCacheItemPolicy(this CacheItemPolicy policy)
        {
            if (policy.Priority == CacheItemPriority.NotRemovable)
            {
                return "NR";
            }

            if (policy.AbsoluteExpiration != ObjectCache.InfiniteAbsoluteExpiration)
            {
                return "AE" + policy.AbsoluteExpiration.UtcDateTime.DateTimeToSpecial();
            }

            if (policy.SlidingExpiration != ObjectCache.NoSlidingExpiration)
            {
                return "SE" + policy.SlidingExpiration.TimeSpanToSpecial();
            }

            return "IN";
        }

        #endregion
    }
}