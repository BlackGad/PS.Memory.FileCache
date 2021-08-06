using System;

namespace PS.Runtime.Caching
{
    public class CleanupSettings
    {
        #region Constructors

        public CleanupSettings()
        {
            CleanupPeriod = TimeSpan.FromSeconds(2);
            GuarantyFileLifetimePeriod = TimeSpan.FromSeconds(5);
        }

        #endregion

        #region Properties

        public TimeSpan? CleanupPeriod { get; set; }
        public TimeSpan? GuarantyFileLifetimePeriod { get; set; }

        #endregion
    }
}