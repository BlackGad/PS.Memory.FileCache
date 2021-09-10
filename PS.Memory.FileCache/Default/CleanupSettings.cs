using System;

namespace PS.Runtime.Caching.Default
{
    public class CleanupSettings
    {
        #region Static members

        public static CleanupSettings Default { get; }
        public static CleanupSettings Infinite { get; }

        #endregion

        #region Constructors

        static CleanupSettings()
        {
            Infinite = new CleanupSettings
            {
                CleanupPeriod = TimeSpan.MaxValue,
                GuarantyFileLifetimePeriod = TimeSpan.MaxValue
            };
            Default = new CleanupSettings();
        }

        public CleanupSettings()
        {
            CleanupPeriod = TimeSpan.FromSeconds(2);
            GuarantyFileLifetimePeriod = TimeSpan.FromSeconds(5);
        }

        #endregion

        #region Properties

        public TimeSpan CleanupPeriod { get; set; }
        public TimeSpan GuarantyFileLifetimePeriod { get; set; }

        #endregion
    }
}