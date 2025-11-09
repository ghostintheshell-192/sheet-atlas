using SheetAtlas.Logging.Models;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.UI.Avalonia.Services
{
    /// <summary>
    /// Service for displaying toast notifications to users
    /// </summary>
    public class ToastNotificationService : IToastNotificationService
    {
        private readonly ILogService _logService;

        public ToastNotificationService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public void ShowToast(LogSeverity level, string title, string message, int durationMs = 4000)
        {
            // Add to LogService (will handle internal logging automatically)
            _logService.AddLogMessage(new LogMessage(level, title, message));

            // TODO: Show visual Avalonia toast (Phase 2 - UI implementation)
        }
    }
}
