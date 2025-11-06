using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Models;

namespace SheetAtlas.Core.Domain.Entities
{
    /// <summary>
    /// Represents a loaded Excel file with its sheets, load status, and any errors encountered.
    /// Implements IDisposable to properly release memory used by sheet data.
    /// </summary>
    public class ExcelFile : IDisposable
    {
        private bool _disposed = false;

        /// <summary>
        /// Absolute file path of the Excel file.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// File name without path (e.g., "report.xlsx").
        /// </summary>
        public string FileName => Path.GetFileName(FilePath);

        /// <summary>
        /// Load status indicating success, partial success, or failure.
        /// </summary>
        public LoadStatus Status { get; }

        /// <summary>
        /// UTC timestamp when the file was loaded.
        /// </summary>
        public DateTime LoadedAt { get; }

        /// <summary>
        /// Dictionary of sheets keyed by sheet name.
        /// </summary>
        public IReadOnlyDictionary<string, SASheetData> Sheets { get; }

        /// <summary>
        /// List of errors and warnings encountered during file loading.
        /// </summary>
        public IReadOnlyList<ExcelError> Errors { get; }

        /// <summary>
        /// Date system used by this workbook (1900 or 1904).
        /// Determines how date serial numbers are converted to DateTime.
        /// Most Windows Excel files use Date1900, older Mac files may use Date1904.
        /// </summary>
        public DateSystem DateSystem { get; }

        public ExcelFile(
            string filePath,
            LoadStatus status,
            Dictionary<string, SASheetData> sheets,
            List<ExcelError> errors,
            DateSystem dateSystem = DateSystem.Date1900)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Status = status;
            Sheets = sheets?.AsReadOnly() ?? throw new ArgumentNullException(nameof(sheets));
            Errors = errors?.AsReadOnly() ?? throw new ArgumentNullException(nameof(errors));
            DateSystem = dateSystem;
            LoadedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Retrieves a sheet by name, or null if not found.
        /// </summary>
        public SASheetData? GetSheet(string sheetName)
        {
            return Sheets.TryGetValue(sheetName, out var sheet) ? sheet : null;
        }

        /// <summary>
        /// True if there are any errors with severity Error or Critical.
        /// </summary>
        public bool HasErrors => Errors.Any(e => e.Level == LogSeverity.Error || e.Level == LogSeverity.Critical);

        /// <summary>
        /// True if there are any warnings.
        /// </summary>
        public bool HasWarnings => Errors.Any(e => e.Level == LogSeverity.Warning);

        /// <summary>
        /// True if there are any critical errors that prevented full file loading.
        /// </summary>
        public bool HasCriticalErrors => Errors.Any(e => e.Level == LogSeverity.Critical);

        /// <summary>
        /// Filters errors by severity level.
        /// </summary>
        public IEnumerable<ExcelError> GetErrorsByLevel(LogSeverity level)
        {
            return Errors.Where(e => e.Level == level);
        }

        /// <summary>
        /// Returns all sheet names in this file.
        /// </summary>
        public IEnumerable<string> GetSheetNames()
        {
            return Sheets.Keys;
        }

        public void Dispose()
        {
            Dispose(true);

            // NOTE: GC.SuppressFinalize() intentionally NOT called
            // REASON: DataTable has 10-14x memory overhead (managed but memory-intensive)
            // ISSUE: If lingering references keep this object alive, finalizer ensures cleanup
            // TODO: When DataTable is replaced with lightweight structures, add SuppressFinalize()
            // and follow standard IDisposable pattern for better GC performance
        }

        ~ExcelFile()
        {
            // Finalizer as safety net for aggressive cleanup
            // Critical for releasing large DataTable memory (hundreds of MB per file)
            // Ensures disposal even if external references prevent immediate collection
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Dispose all SASheetData instances to free memory
                // SASheetData contains large SACellData arrays that should be cleared promptly
                foreach (var sheet in Sheets.Values)
                {
                    sheet?.Dispose();
                }
            }

            _disposed = true;
        }
    }
}
