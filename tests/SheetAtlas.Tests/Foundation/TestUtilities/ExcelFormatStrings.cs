namespace SheetAtlas.Tests.Foundation.TestUtilities
{
    /// <summary>
    /// Real Excel number format strings from actual Excel files.
    /// Used for testing currency detection and number normalization.
    /// Format: [symbol-locale] pattern
    /// </summary>
    public static class ExcelFormatStrings
    {
        // ===== CURRENCY FORMATS =====

        /// <summary>
        /// Euro formats by locale
        /// </summary>
        public static class Euro
        {
            public const string German = "[$€-407] #,##0.00";       // €-DE
            public const string French = "[$€-40C] #,##0.00";       // €-FR
            public const string Italian = "[$€-410] #,##0.00";      // €-IT
            public const string Spanish = "[$€-C0A] #,##0.00";      // €-ES
            public const string Portuguese = "[$€-816] #,##0.00";   // €-PT
            public const string DutchBelgian = "[$€-813] #,##0.00"; // €-NL/BE
        }

        /// <summary>
        /// Dollar formats by locale
        /// </summary>
        public static class Dollar
        {
            public const string AmericanEnglish = "[$$-409] #,##0.00";      // $-US
            public const string CanadianEnglish = "[$$-C09] #,##0.00";      // $-CA (English)
            public const string CanadianFrench = "[$$-C0C] #,##0.00";       // $-CA (French)
            public const string AustralianEnglish = "[$$-C09] #,##0.00";    // $-AU
            public const string NewZealand = "[$$-1409] #,##0.00";         // $-NZ
            public const string Singapore = "[$$-1004] #,##0.00";          // $-SG
            public const string HongKong = "[$$-C04] #,##0.00";            // $-HK
        }

        /// <summary>
        /// British Pound formats
        /// </summary>
        public static class Pound
        {
            public const string English = "[$£-809] #,##0.00";             // £-GB
            public const string WithoutDecimals = "[$£-809] #,##0";        // £-GB (no decimals)
        }

        /// <summary>
        /// Japanese Yen (no decimal places)
        /// </summary>
        public static class Yen
        {
            public const string Japanese = "[$¥-411] #,##0";              // ¥-JP (no decimals)
            public const string WithDecimals = "[$¥-411] #,##0.00";        // ¥-JP (rarely used)
        }

        /// <summary>
        /// Other major currencies
        /// </summary>
        public static class Other
        {
            public const string SwissFranc = "[$$-F00] #,##0.00";
            public const string IndonesianRupiah = "[Rp-421] #,##0";       // No decimals
            public const string IndianiRupee = "[₹-4009] #,##0.00";
            public const string RussianRuble = "[₽-419] #,##0.00";
            public const string SouthKoreanWon = "[₩-412] #,##0";          // No decimals
            public const string ChinesYuan = "[¥-804] #,##0.00";
            public const string CzechKoruna = "[Kč-405] #,##0.00";
            public const string DanishKrone = "[kr-406] #,##0.00";
            public const string SwedishKrona = "[kr-41D] #,##0.00";
            public const string NorwegianKrone = "[kr-414] #,##0.00";
            public const string PolishZloty = "[zł-415] #,##0.00";
            public const string UkrainianHryvnia = "[₴-422] #,##0.00";
            public const string BrazilianReal = "[R$-416] #,##0.00";
            public const string MexicanPeso = "[$-C0A] #,##0.00";
        }

        // ===== AMBIGUOUS CURRENCY FORMATS =====

        /// <summary>
        /// Problematic formats that need context to disambiguate
        /// </summary>
        public static class Ambiguous
        {
            public const string DollarOnly = "$ #,##0.00";                  // No locale, could be USD/CAD/AUD/etc
            public const string DollarPrefix = "$#,##0.00";
            public const string DollarSuffix = "#,##0.00 $";
            public const string GenericCurrency = "¤ #,##0.00";            // Generic currency symbol
            public const string QuotesOnly = "\"$\" #,##0.00";            // Dollar in quotes
        }

        // ===== NON-CURRENCY NUMERIC FORMATS =====

        public static class Numeric
        {
            public const string General = "General";
            public const string Integer = "0";
            public const string TwoDecimals = "0.00";
            public const string ThousandSeparator = "#,##0";
            public const string ThousandWithDecimals = "#,##0.00";
            public const string Scientific = "0.00E+00";
            public const string Percentage = "0%";
            public const string PercentageDecimals = "0.00%";
            public const string Fraction = "# ?/?";
            public const string FractionComplex = "# ??/??";
            public const string Negative = "#,##0.00;[Red]-#,##0.00";
            public const string NegativeWithParens = "#,##0.00_);[Red](#,##0.00)";
        }

        // ===== DATE FORMATS =====

        public static class Date
        {
            public const string ShortDate = "mm/dd/yyyy";
            public const string LongDate = "dddd, mmmm dd, yyyy";
            public const string IsoFormat = "yyyy-mm-dd";
            public const string EuropeanDate = "dd/mm/yyyy";
            public const string MonthYear = "mmmm yyyy";
            public const string DateWithTime = "mm/dd/yyyy hh:mm:ss";
            public const string TimeOnly = "hh:mm:ss";
            public const string TimeWithAMPM = "h:mm:ss AM/PM";
        }

        // ===== SPECIAL FORMATS =====

        public static class Special
        {
            public const string Text = "@";
            public const string Blank = "";
            public const string PhoneNumber = "[<=9999999]###-####;(###) ###-####";
            public const string SocialSecurityNumber = "000-00-0000";
            public const string ZipCode = "00000";
            public const string ZipCodePlus4 = "00000-0000";
        }

        // ===== LOCALE CODES =====

        public static class LocaleCodes
        {
            public const string US = "409";
            public const string UK = "809";
            public const string Germany = "407";
            public const string France = "40C";
            public const string Italy = "410";
            public const string Spain = "C0A";
            public const string Netherlands = "413";
            public const string Belgium = "813";
            public const string Canada = "C09"; // English Canada, C0C = French Canada
            public const string Australia = "C09";
            public const string Japan = "411";
            public const string China = "804";
            public const string Taiwan = "404";
            public const string HongKong = "C04";
        }

        // ===== UTILITY METHODS =====

        /// <summary>
        /// Gets all common currency format strings for testing.
        /// </summary>
        public static IReadOnlyList<string> GetAllCurrencyFormats()
        {
            return new[]
            {
                Euro.German,
                Euro.French,
                Dollar.AmericanEnglish,
                Dollar.CanadianEnglish,
                Pound.English,
                Yen.Japanese,
                Other.SwissFranc,
                Other.IndianiRupee,
                Other.BrazilianReal
            };
        }

        /// <summary>
        /// Gets all numeric formats (non-currency) for testing.
        /// </summary>
        public static IReadOnlyList<string> GetAllNumericFormats()
        {
            return new[]
            {
                Numeric.General,
                Numeric.Integer,
                Numeric.TwoDecimals,
                Numeric.ThousandSeparator,
                Numeric.ThousandWithDecimals,
                Numeric.Scientific,
                Numeric.Percentage
            };
        }

        /// <summary>
        /// Gets all date formats for testing.
        /// </summary>
        public static IReadOnlyList<string> GetAllDateFormats()
        {
            return new[]
            {
                Date.ShortDate,
                Date.LongDate,
                Date.IsoFormat,
                Date.EuropeanDate,
                Date.MonthYear,
                Date.DateWithTime
            };
        }

        /// <summary>
        /// Returns true if format is a recognized currency format.
        /// </summary>
        public static bool IsCurrencyFormat(string format)
        {
            if (string.IsNullOrEmpty(format))
                return false;

            return format.Contains('$')
                || format.Contains('€')
                || format.Contains('£')
                || format.Contains('¥')
                || format.Contains("kr")
                || format.Contains("zł")
                || format.Contains('₹')
                || format.Contains('₽')
                || format.Contains('₩')
                || format.Contains('₴')
                || format.Contains('¤');
        }

        /// <summary>
        /// Returns true if format is a date format.
        /// </summary>
        public static bool IsDateFormat(string format)
        {
            if (string.IsNullOrEmpty(format))
                return false;

            var lower = format.ToLower();
            return lower.Contains("mm")
                || lower.Contains("dd")
                || lower.Contains("yyyy")
                || lower.Contains("yy")
                || lower.Contains('m')
                || lower.Contains('d')
                || lower.Contains("h:")
                || lower.Contains("am/pm");
        }
    }
}
