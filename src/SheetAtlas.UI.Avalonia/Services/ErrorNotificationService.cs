using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Models;
using SheetAtlas.Logging.Services;

namespace SheetAtlas.UI.Avalonia.Services
{
    /// <summary>
    /// UI-layer service for displaying errors to users.
    /// Bridges exception handling with dialog presentation.
    /// </summary>
    public interface IErrorNotificationService
    {
        /// <summary>
        /// Displays an exception to the user in a friendly way
        /// </summary>
        Task ShowExceptionAsync(Exception exception, string context);

        /// <summary>
        /// Displays an ExcelError to the user
        /// </summary>
        Task ShowErrorAsync(ExcelError error);

        /// <summary>
        /// Displays multiple errors (e.g., from partial file load)
        /// </summary>
        Task ShowErrorsAsync(IEnumerable<ExcelError> errors, string title);
    }

    public class ErrorNotificationService : IErrorNotificationService
    {
        private readonly IDialogService _dialogService;
        private readonly ILogService _logService;
        private readonly IExceptionHandler _exceptionHandler;

        public ErrorNotificationService(
            IDialogService dialogService,
            IExceptionHandler exceptionHandler,
            ILogService logService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        public async Task ShowExceptionAsync(Exception exception, string context)
        {
            var error = _exceptionHandler.Handle(exception, context);
            await ShowErrorAsync(error);
        }

        public async Task ShowErrorAsync(ExcelError error)
        {
            var title = error.Level switch
            {
                LogSeverity.Critical => "Fatal Error",
                LogSeverity.Error => "Error",
                LogSeverity.Warning => "Warning",
                LogSeverity.Info => "Information",
                _ => "Error"
            };

            var message = FormatErrorMessage(error);

            // Log the error to LogService (in addition to showing dialog)
            _logService.AddLogMessage(new LogMessage(
                error.Level,
                title,
                error.Message,
                error.Context,
                error.InnerException
            ));

            if (error.Level == LogSeverity.Warning)
            {
                await _dialogService.ShowWarningAsync(message, title);
            }
            else if (error.Level == LogSeverity.Info)
            {
                await _dialogService.ShowInformationAsync(message, title);
            }
            else
            {
                await _dialogService.ShowErrorAsync(message, title);
            }
        }

        public async Task ShowErrorsAsync(IEnumerable<ExcelError> errors, string title)
        {
            var errorList = errors.ToList();
            if (errorList.Count == 0)
                return;

            // Group by level
            var criticalErrors = errorList.Where(e => e.Level == LogSeverity.Critical).ToList();
            var regularErrors = errorList.Where(e => e.Level == LogSeverity.Error).ToList();
            var warnings = errorList.Where(e => e.Level == LogSeverity.Warning).ToList();

            var message = BuildMultiErrorMessage(criticalErrors, regularErrors, warnings);

            if (criticalErrors.Count != 0 || regularErrors.Count != 0)
            {
                await _dialogService.ShowErrorAsync(message, title);
            }
            else
            {
                await _dialogService.ShowWarningAsync(message, title);
            }
        }

        private static string FormatErrorMessage(ExcelError error)
        {
            var message = error.Message;

            if (error.Location != null)
            {
                message += $"\n\nPosizione: {error.Location.ToExcelNotation()}";
            }

            if (!string.IsNullOrEmpty(error.Context))
            {
                message += $"\nContesto: {error.Context}";
            }

            return message;
        }

        private static string BuildMultiErrorMessage(
            List<ExcelError> criticalErrors,
            List<ExcelError> regularErrors,
            List<ExcelError> warnings)
        {
            var lines = new List<string>();

            if (criticalErrors.Count != 0)
            {
                lines.Add("❌ Errori Critici:");
                lines.AddRange(criticalErrors.Select(e => $"  • {e.Message}"));
                lines.Add("");
            }

            if (regularErrors.Count != 0)
            {
                lines.Add("⚠️ Errori:");
                lines.AddRange(regularErrors.Select(e => $"  • {e.Message}"));
                lines.Add("");
            }

            if (warnings.Count != 0)
            {
                lines.Add("ℹ️ Avvisi:");
                lines.AddRange(warnings.Select(e => $"  • {e.Message}"));
            }

            return string.Join("\n", lines).Trim();
        }
    }
}
