using SheetAtlas.Logging.Models;

namespace SheetAtlas.Core.Domain.ValueObjects
{
    /// <summary>
    /// Represents an anomaly detected in a cell during column analysis.
    /// Used by ColumnAnalysisService to report data quality issues with context.
    /// </summary>
    public record CellAnomaly
    {
        /// <summary>
        /// Row index where anomaly was found WITHIN THE SAMPLE provided to ColumnAnalysisService.
        /// Uses SAMPLE-RELATIVE 0-based indexing (0 = first cell in sample array).
        /// NOTE: This is NOT absolute row position in sheet!
        /// The caller (SheetAnalysisOrchestrator) must map this to absolute row using sample tracking.
        /// </summary>
        public int RowIndex { get; init; }

        /// <summary>
        /// The cell value that triggered the anomaly.
        /// </summary>
        public SACellValue CellValue { get; init; }

        /// <summary>
        /// Type of data quality issue detected.
        /// </summary>
        public DataQualityIssue Issue { get; init; }

        /// <summary>
        /// Expected data type based on column analysis.
        /// </summary>
        public DataType ExpectedType { get; init; }

        /// <summary>
        /// Actual data type detected in the cell.
        /// </summary>
        public DataType ActualType { get; init; }

        /// <summary>
        /// Human-readable message describing the anomaly.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// Severity level derived automatically from the issue type.
        /// Maps to application's standard log severity levels.
        /// </summary>
        public LogSeverity Severity => GetLogSeverity(Issue);

        /// <summary>
        /// Maps DataQualityIssue to LogSeverity for consistent error reporting.
        /// </summary>
        private static LogSeverity GetLogSeverity(DataQualityIssue issue)
        {
            return issue switch
            {
                DataQualityIssue.None => LogSeverity.Info,
                DataQualityIssue.ExtraWhitespace => LogSeverity.Info,
                DataQualityIssue.InconsistentFormat => LogSeverity.Warning,
                DataQualityIssue.MissingRequired => LogSeverity.Warning,
                DataQualityIssue.InvalidCharacters => LogSeverity.Error,
                DataQualityIssue.TypeMismatch => LogSeverity.Error,
                DataQualityIssue.OutOfRange => LogSeverity.Error,
                DataQualityIssue.DuplicateValue => LogSeverity.Critical,
                _ => LogSeverity.Warning
            };
        }

        /// <summary>
        /// Factory method for creating a type mismatch anomaly.
        /// </summary>
        public static CellAnomaly TypeMismatch(
            int rowIndex,
            SACellValue cellValue,
            DataType expectedType,
            DataType actualType,
            string? customMessage = null)
        {
            var message = customMessage ??
                $"Type mismatch: expected {expectedType}, found {actualType}";

            return new CellAnomaly
            {
                RowIndex = rowIndex,
                CellValue = cellValue,
                Issue = DataQualityIssue.TypeMismatch,
                ExpectedType = expectedType,
                ActualType = actualType,
                Message = message
            };
        }

        /// <summary>
        /// Factory method for creating a formula error anomaly (#N/A, #REF!, etc).
        /// </summary>
        public static CellAnomaly FormulaError(
            int rowIndex,
            SACellValue cellValue,
            DataType expectedType,
            string errorCode)
        {
            return new CellAnomaly
            {
                RowIndex = rowIndex,
                CellValue = cellValue,
                Issue = DataQualityIssue.InconsistentFormat,
                ExpectedType = expectedType,
                ActualType = DataType.Error,
                Message = $"Excel formula error: {errorCode}"
            };
        }

        /// <summary>
        /// Factory method for creating an invalid character anomaly.
        /// </summary>
        public static CellAnomaly InvalidCharacters(
            int rowIndex,
            SACellValue cellValue,
            DataType expectedType,
            string invalidChars)
        {
            return new CellAnomaly
            {
                RowIndex = rowIndex,
                CellValue = cellValue,
                Issue = DataQualityIssue.InvalidCharacters,
                ExpectedType = expectedType,
                ActualType = DataType.Text,
                Message = $"Invalid characters found: {invalidChars}"
            };
        }
    }
}
