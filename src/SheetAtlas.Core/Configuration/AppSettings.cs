namespace SheetAtlas.Core.Configuration
{
    /// <summary>
    /// Application-wide configuration settings
    /// </summary>
    public class AppSettings
    {
        public PerformanceSettings Performance { get; set; } = new();
        public LoggingSettings Logging { get; set; } = new();
        public FoundationLayerSettings FoundationLayer { get; set; } = new();
        public SecuritySettings Security { get; set; } = new();
    }

    /// <summary>
    /// Security-related configuration for file processing
    /// </summary>
    public class SecuritySettings
    {
        /// <summary>
        /// Maximum allowed file size in bytes (default: 100 MB).
        /// Files larger than this will be rejected.
        /// </summary>
        public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100 MB

        /// <summary>
        /// Maximum allowed decompressed size for XLSX files in bytes (default: 2 GB).
        /// Protects against ZIP bomb attacks.
        /// </summary>
        public long MaxDecompressedSizeBytes { get; set; } = 2L * 1024 * 1024 * 1024; // 2 GB

        /// <summary>
        /// Maximum allowed compression ratio for XLSX files (default: 100).
        /// Ratios higher than this are suspicious (potential ZIP bomb).
        /// </summary>
        public double MaxCompressionRatio { get; set; } = 100;

        /// <summary>
        /// Sanitize CSV cell values that could be interpreted as formulas.
        /// Prefixes dangerous characters (=, +, -, @) with apostrophe.
        /// </summary>
        public bool SanitizeCsvFormulas { get; set; } = true;
    }

    /// <summary>
    /// Performance-related configuration
    /// </summary>
    public class PerformanceSettings
    {
        /// <summary>
        /// Maximum number of Excel files to load simultaneously.
        /// Higher values = faster batch loading but more memory usage.
        /// Recommended: 3-5 for typical systems.
        /// </summary>
        public int MaxConcurrentFileLoads { get; set; } = 5;
    }

    /// <summary>
    /// Logging-related configuration
    /// </summary>
    public class LoggingSettings
    {
        public bool EnableFileLogging { get; set; } = true;
        public bool EnableActivityLog { get; set; } = true;
    }

    /// <summary>
    /// Foundation Layer configuration (data normalization, column analysis, merged cells)
    /// </summary>
    public class FoundationLayerSettings
    {
        public MergedCellsSettings MergedCells { get; set; } = new();
        public ColumnAnalysisSettings ColumnAnalysis { get; set; } = new();
        public NormalizationSettings Normalization { get; set; } = new();
    }

    /// <summary>
    /// Merged cells handling configuration
    /// </summary>
    public class MergedCellsSettings
    {
        /// <summary>
        /// Default strategy for resolving merged cells.
        /// Options: ExpandValue, KeepTopLeft, FlattenToString, TreatAsHeader
        /// </summary>
        public string DefaultStrategy { get; set; } = "ExpandValue";

        /// <summary>
        /// Warn user if more than this percentage of cells are merged.
        /// Indicates potential data quality issues.
        /// </summary>
        public int WarnThresholdPercentage { get; set; } = 20;
    }

    /// <summary>
    /// Column analysis configuration
    /// </summary>
    public class ColumnAnalysisSettings
    {
        /// <summary>
        /// Number of cells to sample per column for type detection.
        /// Higher = more accurate but slower. Recommended: 100-200.
        /// </summary>
        public int SampleSize { get; set; } = 100;

        /// <summary>
        /// Minimum confidence score (0.0-1.0) to classify column as strong type.
        /// more than 0.8 = strong type, less than 0.8 = mixed type.
        /// </summary>
        public double ConfidenceThreshold { get; set; } = 0.8;

        /// <summary>
        /// Maximum number of rows to scan for headers during auto-detection.
        /// Typical: 10-20, scientific datasets: 50+
        /// </summary>
        public int MaxHeaderRows { get; set; } = 20;
    }

    /// <summary>
    /// Data normalization configuration
    /// </summary>
    public class NormalizationSettings
    {
        /// <summary>
        /// Normalize date formats (Excel serial, US/EU formats, ISO).
        /// Improves search accuracy +20%.
        /// </summary>
        public bool EnableDateNormalization { get; set; } = true;

        /// <summary>
        /// Detect and parse currency symbols from number formats.
        /// Critical for financial data comparison.
        /// </summary>
        public bool EnableCurrencyDetection { get; set; } = true;

        /// <summary>
        /// Normalize boolean variations (Yes/No, 1/0, TRUE/FALSE, X/blank) to true/false.
        /// </summary>
        public bool EnableBooleanNormalization { get; set; } = true;

        /// <summary>
        /// Clean text values (trim whitespace, remove zero-width characters).
        /// Improves search accuracy +10%.
        /// </summary>
        public bool EnableTextCleaning { get; set; } = true;
    }
}
