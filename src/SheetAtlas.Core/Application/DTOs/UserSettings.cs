namespace SheetAtlas.Core.Application.DTOs
{
    /// <summary>
    /// User preferences with persistent storage.
    /// All properties have sensible defaults - app works without configuration.
    /// </summary>
    public record UserSettings
    {
        /// <summary>
        /// Settings schema version for future migrations.
        /// Current version: 1
        /// </summary>
        public int Version { get; init; } = 1;

        /// <summary>
        /// Appearance preferences (theme).
        /// </summary>
        public AppearanceSettings Appearance { get; init; } = new();

        /// <summary>
        /// Data processing preferences (headers, export format, naming).
        /// </summary>
        public DataProcessingSettings DataProcessing { get; init; } = new();

        /// <summary>
        /// File location preferences (output folder).
        /// </summary>
        public FileLocationSettings FileLocations { get; init; } = new();

        /// <summary>
        /// Creates default settings with sensible out-of-the-box values.
        /// </summary>
        public static UserSettings CreateDefault() => new();
    }

    /// <summary>
    /// Appearance preferences.
    /// </summary>
    public record AppearanceSettings
    {
        /// <summary>
        /// Theme preference.
        /// Default: System (follows operating system theme).
        /// </summary>
        public ThemePreference Theme { get; init; } = ThemePreference.System;
    }

    /// <summary>
    /// Data processing preferences.
    /// </summary>
    public record DataProcessingSettings
    {
        /// <summary>
        /// Default number of header rows for new files.
        /// Default: 1 (most common case).
        /// Valid range: 1-10.
        /// </summary>
        public int DefaultHeaderRowCount { get; init; } = 1;

        /// <summary>
        /// Default export format for reports (comparison, search results).
        /// Default: Excel (richer format with formatting).
        /// </summary>
        public ExportFormat DefaultExportFormat { get; init; } = ExportFormat.Excel;

        /// <summary>
        /// Naming pattern for normalized/processed files.
        /// Default: DatePrefix (avoids collisions, maintains chronology).
        /// </summary>
        public NamingPattern NormalizedFileNaming { get; init; } = NamingPattern.DatePrefix;
    }

    /// <summary>
    /// File location preferences.
    /// </summary>
    public record FileLocationSettings
    {
        /// <summary>
        /// Default output folder for exports and processed files.
        /// Default: ~/Documents/SheetAtlas/output
        /// </summary>
        public string OutputFolder { get; init; } = GetDefaultOutputFolder();

        /// <summary>
        /// Gets the default output folder path based on OS conventions.
        /// </summary>
        private static string GetDefaultOutputFolder() =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "SheetAtlas",
                "output");
    }

    /// <summary>
    /// Theme preference options.
    /// </summary>
    public enum ThemePreference
    {
        /// <summary>Light theme (bright background)</summary>
        Light,

        /// <summary>Dark theme (dark background)</summary>
        Dark,

        /// <summary>Follow operating system theme setting (default)</summary>
        System
    }

    /// <summary>
    /// Export format for reports (comparison results, search results).
    /// </summary>
    public enum ExportFormat
    {
        /// <summary>Excel format (.xlsx) with rich formatting (default)</summary>
        Excel,

        /// <summary>CSV format (.csv) for maximum compatibility</summary>
        CSV
    }

    /// <summary>
    /// Naming pattern for normalized/processed files.
    /// Determines how output files are named to avoid collisions.
    /// </summary>
    public enum NamingPattern
    {
        /// <summary>
        /// {date}_{filename}.{ext}
        /// Example: 2025-01-14_Budget.xlsx
        /// Avoids collisions, maintains chronology (default)
        /// </summary>
        DatePrefix,

        /// <summary>
        /// {filename}_{date}.{ext}
        /// Example: Budget_2025-01-14.xlsx
        /// Keeps original name first, appends date
        /// </summary>
        DateSuffix,

        /// <summary>
        /// {date}_{time}_{filename}.{ext}
        /// Example: 2025-01-14_1523_Budget.xlsx
        /// Full timestamp for high-frequency processing
        /// </summary>
        DateTimePrefix
    }
}
