using System;
using System.Globalization;

namespace PS.Runtime.Caching.Extensions
{
    static class SpecialFormattingExtensions
    {
        #region Static members

        /// <summary>
        ///     Parses string for special DateTime formatting
        /// </summary>
        /// <param name="source">Source string</param>
        /// <returns>Parsed DateTime</returns>
        public static DateTime DateTimeFromSpecial(this string source)
        {
            var value = Int64.Parse(source, NumberStyles.HexNumber);
            return DateTime.FromBinary(value);
        }

        /// <summary>
        ///     Converts DateTime to special format (DateTime.Binary as hex string)
        /// </summary>
        /// <param name="source">Source DateTime</param>
        /// <returns>Serialized DateTime</returns>
        public static string DateTimeToSpecial(this DateTime source)
        {
            return source.ToBinary().ToString("X");
        }

        public static TimeSpan TimeSpanFromSpecial(this string source)
        {
            source = source.PadRight(16, '0');
            var value = Int64.Parse(source, NumberStyles.HexNumber);
            var milliseconds = BitConverter.Int64BitsToDouble(value);
            return TimeSpan.FromMilliseconds(milliseconds);
        }

        public static string TimeSpanToSpecial(this TimeSpan source)
        {
            return BitConverter.DoubleToInt64Bits(source.TotalMilliseconds)
                               .ToString("X")
                               .TrimEnd('0');
        }

        #endregion
    }
}