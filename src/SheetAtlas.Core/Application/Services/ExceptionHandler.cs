using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.Exceptions;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.Core.Application.Services
{
    /// <summary>
    /// Centralized exception handling implementation.
    /// Converts technical exceptions to user-friendly messages and logs details.
    /// </summary>
    public class ExceptionHandler : IExceptionHandler
    {
        private readonly ILogService _logger;

        public ExceptionHandler(ILogService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ExcelError Handle(Exception exception, string context)
        {
            // Log technical details
            _logger.LogError($"Error in {context}: {exception.Message}", exception, "ExceptionHandler");

            // Convert to user-friendly error
            return exception switch
            {
                // Domain exceptions (already have user messages)
                ComparisonException compEx => ExcelError.Critical(
                    context,
                    compEx.UserMessage,
                    compEx),

                // Framework exceptions (need translation)
                FileNotFoundException fnfEx => ExcelError.Critical(
                    context,
                    $"File not found: {Path.GetFileName(fnfEx.FileName ?? "unknown")}",
                    fnfEx),

                UnauthorizedAccessException uaEx => ExcelError.Critical(
                    context,
                    "File access denied. Check permissions.",
                    uaEx),

                IOException ioEx => ExcelError.Critical(
                    context,
                    $"File reading error: {ioEx.Message}",
                    ioEx),

                InvalidOperationException invOpEx => ExcelError.Critical(
                    context,
                    "Invalid operation on Excel file.",
                    invOpEx),

                // Generic fallback
                _ => ExcelError.Critical(
                    context,
                    "Unexpected error during processing.",
                    exception)
            };
        }

        public string GetUserMessage(Exception exception)
        {
            return exception switch
            {
                SheetAtlasException customEx => customEx.UserMessage,
                FileNotFoundException _ => "File not found",
                UnauthorizedAccessException _ => "Access denied",
                IOException _ => "File reading error",
                InvalidOperationException _ => "Corrupted Excel file",
                _ => "Unexpected error"
            };
        }

        public bool IsRecoverable(Exception exception)
        {
            return IsRecoverableException(exception);
        }

        /// <summary>
        /// Static helper to determine if an exception is recoverable.
        /// Recoverable = user can take action to fix the issue (e.g., select different file, grant permissions).
        /// Used by both ExceptionHandler and ExcelErrorJsonConverter to ensure consistent logic.
        /// </summary>
        public static bool IsRecoverableException(Exception? exception)
        {
            if (exception == null)
                return false;

            return exception switch
            {
                // Recoverable: user can fix by selecting different file or granting permissions
                ComparisonException => true,
                FileNotFoundException => true,
                UnauthorizedAccessException => true,  // User can grant file permissions
                IOException => true,

                // Not recoverable: programming errors (should never reach user in production)
                ArgumentNullException => false,
                NullReferenceException => false,
                InvalidCastException => false,

                // Default: treat custom exceptions as recoverable
                SheetAtlasException => true,
                _ => false
            };
        }
    }
}
