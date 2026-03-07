using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Models;

namespace SheetAtlas.Core.Application.DTOs
{
    /// <summary>
    /// Result of loading an Excel file. Contains status, loaded file, and any errors or warnings.
    /// </summary>
    public class FileLoadResult
    {
        public string FilePath { get; }
        public string FileName => Path.GetFileName(FilePath);
        public LoadStatus Status { get; }
        public ExcelFile? File { get; }
        public IReadOnlyList<ExcelError> Errors { get; }

        public FileLoadResult(string filePath, LoadStatus status, ExcelFile? file = null, List<ExcelError>? errors = null)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Status = status;
            File = file;
            Errors = (errors ?? new List<ExcelError>()).AsReadOnly();
        }

        public bool HasErrors => Errors.Any(e => e.Level == LogSeverity.Error || e.Level == LogSeverity.Critical);

        public bool HasWarnings => Errors.Any(e => e.Level == LogSeverity.Warning);

        public bool HasCriticalErrors => Errors.Any(e => e.Level == LogSeverity.Critical);

        public bool IsSuccessful => Status == LoadStatus.Success;

        public bool IsPartiallySuccessful => Status == LoadStatus.PartialSuccess;

        public bool IsFailed => Status == LoadStatus.Failed;
    }
}
