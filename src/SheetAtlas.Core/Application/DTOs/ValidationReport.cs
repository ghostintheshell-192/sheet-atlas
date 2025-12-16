using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.DTOs
{
    /// <summary>
    /// Complete validation report for an Excel file against a template.
    /// Contains all validation results, issues, and summary statistics.
    /// </summary>
    public sealed record ValidationReport
    {
        /// <summary>Template name used for validation.</summary>
        public string TemplateName { get; init; } = string.Empty;

        /// <summary>Template version used for validation.</summary>
        public string TemplateVersion { get; init; } = "1.0";

        /// <summary>File path that was validated.</summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>Sheet name that was validated.</summary>
        public string SheetName { get; init; } = string.Empty;

        /// <summary>UTC timestamp when validation was performed.</summary>
        public DateTime ValidatedAt { get; init; } = DateTime.UtcNow;

        /// <summary>Duration of validation process.</summary>
        public TimeSpan Duration { get; init; }

        /// <summary>Overall validation passed (no errors).</summary>
        public bool Passed { get; init; }

        /// <summary>Overall validation status.</summary>
        public ValidationStatus Status { get; init; }

        /// <summary>Number of data rows in the file.</summary>
        public int DataRowCount { get; init; }

        /// <summary>Number of columns in the file.</summary>
        public int ColumnCount { get; init; }

        /// <summary>Validation results for each expected column.</summary>
        public IReadOnlyList<ColumnValidationResult> ColumnResults { get; init; } = Array.Empty<ColumnValidationResult>();

        /// <summary>All validation issues (sheet-level + column-level).</summary>
        public IReadOnlyList<ValidationIssue> AllIssues { get; init; } = Array.Empty<ValidationIssue>();

        /// <summary>Sheet-level validation issues only.</summary>
        public IReadOnlyList<ValidationIssue> SheetIssues { get; init; } = Array.Empty<ValidationIssue>();

        private ValidationReport() { }

        // === Factory Methods ===

        /// <summary>Create a successful validation report.</summary>
        public static ValidationReport Success(
            ExcelTemplate template,
            ExcelFile file,
            string sheetName,
            IReadOnlyList<ColumnValidationResult> columnResults,
            TimeSpan duration) =>
            new()
            {
                TemplateName = template.Name,
                TemplateVersion = template.Version,
                FilePath = file.FilePath,
                SheetName = sheetName,
                ValidatedAt = DateTime.UtcNow,
                Duration = duration,
                Passed = true,
                Status = ValidationStatus.Valid,
                DataRowCount = file.GetSheet(sheetName)?.DataRowCount ?? 0,
                ColumnCount = file.GetSheet(sheetName)?.ColumnCount ?? 0,
                ColumnResults = columnResults,
                AllIssues = CollectAllIssues(columnResults, Array.Empty<ValidationIssue>()),
                SheetIssues = Array.Empty<ValidationIssue>()
            };

        /// <summary>Create a validation report with issues.</summary>
        public static ValidationReport WithIssues(
            ExcelTemplate template,
            ExcelFile file,
            string sheetName,
            IReadOnlyList<ColumnValidationResult> columnResults,
            IReadOnlyList<ValidationIssue> sheetIssues,
            TimeSpan duration)
        {
            var allIssues = CollectAllIssues(columnResults, sheetIssues);
            var hasErrors = allIssues.Any(i => i.IsError);
            var hasWarnings = allIssues.Any(i => i.IsWarning);

            return new()
            {
                TemplateName = template.Name,
                TemplateVersion = template.Version,
                FilePath = file.FilePath,
                SheetName = sheetName,
                ValidatedAt = DateTime.UtcNow,
                Duration = duration,
                Passed = !hasErrors,
                Status = hasErrors ? ValidationStatus.Invalid :
                         hasWarnings ? ValidationStatus.ValidWithWarnings :
                         ValidationStatus.Valid,
                DataRowCount = file.GetSheet(sheetName)?.DataRowCount ?? 0,
                ColumnCount = file.GetSheet(sheetName)?.ColumnCount ?? 0,
                ColumnResults = columnResults,
                AllIssues = allIssues,
                SheetIssues = sheetIssues
            };
        }

        /// <summary>Create a failed validation report (file couldn't be validated).</summary>
        public static ValidationReport Failed(
            ExcelTemplate template,
            string filePath,
            string errorMessage,
            TimeSpan duration) =>
            new()
            {
                TemplateName = template.Name,
                TemplateVersion = template.Version,
                FilePath = filePath,
                SheetName = string.Empty,
                ValidatedAt = DateTime.UtcNow,
                Duration = duration,
                Passed = false,
                Status = ValidationStatus.Failed,
                DataRowCount = 0,
                ColumnCount = 0,
                ColumnResults = Array.Empty<ColumnValidationResult>(),
                AllIssues = new[] { ValidationIssue.CriticalError(errorMessage) },
                SheetIssues = new[] { ValidationIssue.CriticalError(errorMessage) }
            };

        // === Helper Methods ===

        private static IReadOnlyList<ValidationIssue> CollectAllIssues(
            IReadOnlyList<ColumnValidationResult> columnResults,
            IReadOnlyList<ValidationIssue> sheetIssues)
        {
            var allIssues = new List<ValidationIssue>(sheetIssues);
            foreach (var col in columnResults)
            {
                allIssues.AddRange(col.Issues);
            }
            return allIssues;
        }

        // === Computed Properties ===

        /// <summary>Number of columns that passed validation.</summary>
        public int PassedColumnCount => ColumnResults.Count(c => c.Passed);

        /// <summary>Number of columns that failed validation.</summary>
        public int FailedColumnCount => ColumnResults.Count(c => !c.Passed);

        /// <summary>Number of missing required columns.</summary>
        public int MissingRequiredCount => ColumnResults.Count(c => !c.Found && c.IsRequired);

        /// <summary>Number of extra columns (not in template).</summary>
        public int ExtraColumnCount => ColumnResults.Count(c =>
            c.Issues.Any(i => i.IssueType == ValidationIssueType.ExtraColumn));

        /// <summary>Total error count across all issues.</summary>
        public int TotalErrorCount => AllIssues.Count(i => i.IsError);

        /// <summary>Total warning count across all issues.</summary>
        public int TotalWarningCount => AllIssues.Count(i => i.IsWarning);

        /// <summary>Total info count across all issues.</summary>
        public int TotalInfoCount => AllIssues.Count(i => i.Severity == ValidationSeverity.Info);

        /// <summary>Issues grouped by severity.</summary>
        public ILookup<ValidationSeverity, ValidationIssue> IssuesBySeverity =>
            AllIssues.ToLookup(i => i.Severity);

        /// <summary>Issues grouped by column.</summary>
        public ILookup<string?, ValidationIssue> IssuesByColumn =>
            AllIssues.ToLookup(i => i.ColumnName);

        /// <summary>Summary string for display.</summary>
        public string Summary
        {
            get
            {
                return Status switch
                {
                    ValidationStatus.Valid =>
                        $"Valid: {PassedColumnCount}/{ColumnResults.Count} columns passed",
                    ValidationStatus.ValidWithWarnings =>
                        $"Valid with {TotalWarningCount} warning(s): {PassedColumnCount}/{ColumnResults.Count} columns passed",
                    ValidationStatus.Invalid =>
                        $"Invalid: {TotalErrorCount} error(s), {FailedColumnCount} column(s) failed",
                    ValidationStatus.Failed =>
                        $"Validation failed: {AllIssues.FirstOrDefault()?.Message ?? "Unknown error"}",
                    _ => "Unknown status"
                };
            }
        }
    }

    /// <summary>
    /// Overall validation status.
    /// </summary>
    public enum ValidationStatus
    {
        /// <summary>All validation checks passed.</summary>
        Valid,

        /// <summary>Validation passed but with warnings.</summary>
        ValidWithWarnings,

        /// <summary>Validation failed due to errors.</summary>
        Invalid,

        /// <summary>Validation could not be performed (file error, etc.).</summary>
        Failed
    }
}
