namespace SheetAtlas.Core.Domain.ValueObjects
{
    /// <summary>
    /// Indicates which date serial number system the Excel workbook uses.
    /// Affects how date serial numbers are converted to DateTime values.
    /// </summary>
    /// <remarks>
    /// Excel stores dates as serial numbers (days elapsed since epoch).
    /// Two different epoch systems exist with 1,462 day difference (4 years + 1 leap day).
    ///
    /// Historical context:
    /// - 1900 system: Windows Excel default, has deliberate 1900 leap year bug (Lotus 1-2-3 compatibility)
    /// - 1904 system: Mac Excel historical default, avoids 1900 leap year issue
    /// </remarks>
    public enum DateSystem
    {
        /// <summary>
        /// 1900 date system (Windows default).
        /// Epoch: January 1, 1900 = serial 1
        /// Note: Serial 60 = February 29, 1900 (non-existent date, kept for Lotus compatibility)
        /// </summary>
        Date1900 = 0,

        /// <summary>
        /// 1904 date system (Mac historical default).
        /// Epoch: January 1, 1904 = serial 0
        /// Serial numbers are 1,462 days smaller than 1900 system for same date.
        /// </summary>
        Date1904 = 1
    }
}
