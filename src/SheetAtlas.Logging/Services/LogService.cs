using Microsoft.Extensions.Logging;
using SheetAtlas.Logging.Models;

namespace SheetAtlas.Logging.Services
{
    /// <summary>
    /// In-memory and file-based implementation of log service
    /// Manages log message storage, file persistence, and events
    /// </summary>
    public class LogService : ILogService
    {
        private readonly ILogger<LogService> _logger;
        private readonly List<LogMessage> _messages;
        private readonly object _lock = new object();
        private readonly object _fileLock = new object();
        private readonly string _logDirectory;
        private readonly string _logFilePattern;

        public LogService(ILogger<LogService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _messages = new List<LogMessage>();

            // Setup log directory (cross-platform)
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _logDirectory = Path.Combine(appDataPath, "SheetAtlas", "Logs");
            _logFilePattern = "app-{0:yyyy-MM-dd}.log";

            // Create directory if it doesn't exist
            try
            {
                Directory.CreateDirectory(_logDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create log directory: {Directory}", _logDirectory);
            }
        }

        public int UnreadCount
        {
            get
            {
                lock (_lock)
                {
                    return _messages.Count;
                }
            }
        }

        public event EventHandler<LogMessage>? MessageAdded;
        public event EventHandler? MessagesCleared;

        public void AddLogMessage(LogMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);

            lock (_lock)
            {
                _messages.Add(message);
            }

            _logger.LogDebug("Log message added: {Title} ({Level})", message.Title, message.Level);

            // Write to file for Warning and above
            if (message.Level >= LogSeverity.Warning)
            {
                WriteToFile(message);
            }

            // Raise event for UI subscribers
            MessageAdded?.Invoke(this, message);
        }

        public void ClearMessage(Guid messageId)
        {
            lock (_lock)
            {
                var message = _messages.FirstOrDefault(n => n.Id == messageId);
                if (message != null)
                {
                    _messages.Remove(message);
                    _logger.LogDebug("Log message cleared: {Id}", messageId);
                }
            }
        }

        public void ClearAllMessages()
        {
            lock (_lock)
            {
                _messages.Clear();
            }

            _logger.LogDebug("All messages cleared");

            // Raise event for UI subscribers
            MessagesCleared?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<LogMessage> GetMessages()
        {
            lock (_lock)
            {
                // Return a copy to avoid external modifications
                return _messages.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Writes a log message to the daily log file
        /// Thread-safe operation with automatic daily rotation
        /// </summary>
        private void WriteToFile(LogMessage message)
        {
            try
            {
                // Ensure directory exists (safety net - also done in constructor)
                Directory.CreateDirectory(_logDirectory);

                var logFile = Path.Combine(_logDirectory, string.Format(_logFilePattern, DateTime.Now));
                var logLine = FormatLogLine(message);

                lock (_fileLock)
                {
                    File.AppendAllText(logFile, logLine + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // Don't throw - logging should never crash the app
                _logger.LogWarning(ex, "Failed to write log to file");
            }
        }

        /// <summary>
        /// Formats a log message for file output with improved readability
        /// </summary>
        private static string FormatLogLine(LogMessage message)
        {
            var timestamp = message.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var level = message.Level.ToString().ToUpperInvariant().PadRight(8);
            var context = !string.IsNullOrEmpty(message.Context) ? message.Context : message.Title;

            // Header line: [timestamp] LEVEL | Context
            var line = $"[{timestamp}] {level} | {context}";

            // Message on indented line for better readability
            line += $"{Environment.NewLine}  {message.Message}";

            // Add exception details if present
            if (message.Exception != null)
            {
                line += $"{Environment.NewLine}  → Exception: {message.Exception.GetType().Name}: {message.Exception.Message}";

                if (!string.IsNullOrEmpty(message.Exception.StackTrace))
                {
                    // Indent stack trace for clarity
                    var indentedStackTrace = string.Join(Environment.NewLine,
                        message.Exception.StackTrace.Split(Environment.NewLine)
                            .Select(l => $"    {l}"));
                    line += $"{Environment.NewLine}{indentedStackTrace}";
                }
            }

            return line;
        }
    }
}
