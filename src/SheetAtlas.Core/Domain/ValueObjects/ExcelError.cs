using SheetAtlas.Logging.Models;

namespace SheetAtlas.Core.Domain.ValueObjects
{
    /// <summary>
    /// Indicates the overall outcome of a file load operation.
    /// </summary>
    public enum LoadStatus
    {
        /// <summary>File loaded successfully with no errors.</summary>
        Success,
        /// <summary>File loaded but some non-critical issues occurred.</summary>
        PartialSuccess,
        /// <summary>File failed to load due to critical errors.</summary>
        Failed
    }

    /// <summary>
    /// Represents an error, warning, or informational message encountered during Excel file processing.
    /// Immutable value object with factory methods for different error types.
    /// </summary>
    public class ExcelError
    {
        /// <summary>Severity level of this error (Info, Warning, Error, Critical).</summary>
        public LogSeverity Level { get; }

        /// <summary>Human-readable error message.</summary>
        public string Message { get; }

        /// <summary>Context where the error occurred (e.g., "File", "Sheet:SheetName").</summary>
        public string Context { get; }

        /// <summary>Optional cell location if error is cell-specific.</summary>
        public CellReference? Location { get; }

        /// <summary>Optional underlying exception that caused this error.</summary>
        public Exception? InnerException { get; }

        /// <summary>UTC timestamp when this error was created.</summary>
        public DateTime Timestamp { get; }

        private ExcelError(LogSeverity level, string message, string context, CellReference? location = null, Exception? innerException = null)
        {
            Level = level;
            Message = message;
            Context = context;
            Location = location;
            InnerException = innerException;
            Timestamp = DateTime.UtcNow;
        }

        // Constructor overload for JSON deserialization (preserves original timestamp)
        private ExcelError(LogSeverity level, string message, string context, DateTime timestamp, CellReference? location = null, Exception? innerException = null)
        {
            Level = level;
            Message = message;
            Context = context;
            Location = location;
            InnerException = innerException;
            Timestamp = timestamp;
        }

        /// <summary>
        /// Creates a file-level error.
        /// </summary>
        public static ExcelError FileError(string message, Exception? ex = null)
        {
            return new ExcelError(LogSeverity.Error, message, "File", null, ex);
        }

        /// <summary>
        /// Creates a sheet-level error.
        /// </summary>
        public static ExcelError SheetError(string sheetName, string message, Exception? ex = null)
        {
            return new ExcelError(LogSeverity.Error, message, $"Sheet:{sheetName}", null, ex);
        }

        /// <summary>
        /// Creates a cell-level error with specific location.
        /// </summary>
        public static ExcelError CellError(string sheetName, CellReference location, string message, Exception? ex = null)
        {
            return new ExcelError(LogSeverity.Error, message, $"Cell:{sheetName}", location, ex);
        }

        /// <summary>
        /// Creates a warning-level message.
        /// </summary>
        public static ExcelError Warning(string context, string message)
        {
            return new ExcelError(LogSeverity.Warning, message, context);
        }

        /// <summary>
        /// Creates an informational message.
        /// </summary>
        public static ExcelError Info(string context, string message)
        {
            return new ExcelError(LogSeverity.Info, message, context);
        }

        /// <summary>
        /// Creates a critical error that prevented file loading.
        /// </summary>
        public static ExcelError Critical(string context, string message, Exception? ex = null)
        {
            return new ExcelError(LogSeverity.Critical, message, context, null, ex);
        }

        /// <summary>
        /// Factory method for JSON deserialization that preserves the original timestamp.
        /// </summary>
        public static ExcelError FromJson(LogSeverity level, string message, string context, DateTime timestamp, CellReference? location = null, Exception? innerException = null)
        {
            return new ExcelError(level, message, context, timestamp, location, innerException);
        }

        public override string ToString()
        {
            var locationStr = Location != null ? $" at {Location}" : "";
            return $"[{Level}] {Context}: {Message}{locationStr}";
        }
    }

    /// <summary>
    /// Represents a specific cell location within a sheet using absolute 0-based indices.
    /// Row 0 = first row in sheet (typically header), Row 1 = first data row, etc.
    /// Column 0 = first column (A), Column 1 = second column (B), etc.
    /// </summary>
    public class CellReference
    {
        /// <summary>Zero-based absolute row index (0 = first row in sheet, typically header).</summary>
        public int Row { get; }

        /// <summary>Zero-based column index.</summary>
        public int Column { get; }

        public CellReference(int row, int column)
        {
            Row = row;
            Column = column;
        }

        /// <summary>
        /// Returns cell reference in R1C1 notation (e.g., "R5C2").
        /// </summary>
        public override string ToString() => $"R{Row}C{Column}";

        /// <summary>
        /// Converts to Excel A1 notation (e.g., "A1" for row=0/col=0, "B2" for row=1/col=1).
        /// Simply converts 0-based indices to 1-based Excel notation.
        /// </summary>
        public string ToExcelNotation()
        {
            string columnName = "";
            int col = Column;
            while (col >= 0)
            {
                columnName = (char)('A' + (col % 26)) + columnName;
                col = col / 26 - 1;
            }
            // Convert 0-based to 1-based Excel row
            int excelRow = Row + 1;
            return $"{columnName}{excelRow}";
        }
    }
}
