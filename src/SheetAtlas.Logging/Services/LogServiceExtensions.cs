using SheetAtlas.Logging.Models;

namespace SheetAtlas.Logging.Services
{
    /// <summary>
    /// Extension methods for ILogService to simplify logging calls
    /// </summary>
    public static class LogServiceExtensions
    {
        /// <summary>
        /// Logs an informational message
        /// </summary>
        public static void LogInfo(this ILogService logService, string message, string? context = null)
        {
            logService?.AddLogMessage(new LogMessage(
                level: LogSeverity.Info,
                title: "Info",
                message: message,
                context: context));
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        public static void LogWarning(this ILogService logService, string message, string? context = null)
        {
            logService?.AddLogMessage(new LogMessage(
                level: LogSeverity.Warning,
                title: "Warning",
                message: message,
                context: context));
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        public static void LogError(this ILogService logService, string message, string? context = null)
        {
            logService?.AddLogMessage(new LogMessage(
                level: LogSeverity.Error,
                title: "Error",
                message: message,
                context: context));
        }

        /// <summary>
        /// Logs a critical error message
        /// </summary>
        public static void LogCritical(this ILogService logService, string message, string? context = null)
        {
            logService?.AddLogMessage(new LogMessage(
                level: LogSeverity.Critical,
                title: "Critical",
                message: message,
                context: context));
        }

        /// <summary>
        /// Logs an error with exception details
        /// </summary>
        public static void LogError(this ILogService logService, string message, Exception exception, string? context = null)
        {
            var fullMessage = $"{message}: {exception?.Message}";
            logService?.AddLogMessage(new LogMessage(
                level: LogSeverity.Error,
                title: "Error",
                message: fullMessage,
                context: context,
                exception: exception));
        }

        /// <summary>
        /// Logs a critical error with exception details
        /// </summary>
        public static void LogCritical(this ILogService logService, string message, Exception exception, string? context = null)
        {
            var fullMessage = $"{message}: {exception?.Message}";
            logService?.AddLogMessage(new LogMessage(
                level: LogSeverity.Critical,
                title: "Critical",
                message: fullMessage,
                context: context,
                exception: exception));
        }
    }
}
