using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Configuration;
using SheetAtlas.Logging.Services;
using Microsoft.Extensions.Options;

namespace SheetAtlas.Infrastructure.External.Readers
{
    /// <summary>
    /// Facade that groups common dependencies for all file format readers.
    /// Reduces constructor parameter count and centralizes shared service access.
    /// </summary>
    /// <remarks>
    /// Inspired by the PanelsIO pattern used in legacy WPF systems, but adapted
    /// for modern dependency injection with compile-time type safety.
    ///
    /// Contains services used by 100% of file readers:
    /// - Logging
    /// - Sheet analysis orchestration
    /// - User settings
    /// - Security configuration
    /// </remarks>
    public class FileReaderContext
    {
        /// <summary>
        /// Service for logging operations and errors during file reading.
        /// </summary>
        public ILogService Logger { get; }

        /// <summary>
        /// Orchestrator for analyzing and enriching sheet data (merged cells, normalization, etc.).
        /// </summary>
        public ISheetAnalysisOrchestrator AnalysisOrchestrator { get; }

        /// <summary>
        /// Service for accessing user settings (header row count, merge strategy, etc.).
        /// </summary>
        public ISettingsService Settings { get; }

        /// <summary>
        /// Security settings for file size limits, formula sanitization, etc.
        /// Extracted from AppSettings for convenience.
        /// </summary>
        public SecuritySettings SecuritySettings { get; }

        public FileReaderContext(
            ILogService logger,
            ISheetAnalysisOrchestrator analysisOrchestrator,
            ISettingsService settingsService,
            IOptions<AppSettings> appSettings)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            AnalysisOrchestrator = analysisOrchestrator ?? throw new ArgumentNullException(nameof(analysisOrchestrator));
            Settings = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            // Extract SecuritySettings for convenience (avoids accessing .Value.Security repeatedly)
            SecuritySettings = appSettings?.Value?.Security ?? new SecuritySettings();
        }
    }
}
