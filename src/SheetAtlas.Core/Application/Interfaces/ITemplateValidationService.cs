using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Application.DTOs;

namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Service for validating Excel files against templates and creating templates from files.
    /// Core service for the Template Management feature.
    /// </summary>
    /// <remarks>
    /// Workflow:
    /// 1. Load a "good" Excel file
    /// 2. Call CreateTemplateFromFileAsync to auto-detect structure
    /// 3. Customize the template (optional)
    /// 4. Save template to JSON
    /// 5. Load new files and call ValidateAsync to check conformance
    /// </remarks>
    public interface ITemplateValidationService
    {
        /// <summary>
        /// Validate an Excel file against a template.
        /// Checks column structure, data types, and validation rules.
        /// </summary>
        /// <param name="file">The loaded Excel file to validate.</param>
        /// <param name="template">The template to validate against.</param>
        /// <param name="sheetName">
        /// Specific sheet to validate. If null, uses template's ExpectedSheetName
        /// or the first sheet if not specified.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Validation report with all issues found.</returns>
        Task<ValidationReport> ValidateAsync(
            ExcelFile file,
            ExcelTemplate template,
            string? sheetName = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a template from an existing Excel file.
        /// Auto-detects column structure, data types, and suggests validation rules.
        /// Uses IColumnAnalysisService for type detection.
        /// </summary>
        /// <param name="file">The Excel file to use as template source.</param>
        /// <param name="templateName">Name for the new template.</param>
        /// <param name="sheetName">
        /// Specific sheet to use for template. If null, uses first sheet.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>New template based on file structure.</returns>
        Task<ExcelTemplate> CreateTemplateFromFileAsync(
            ExcelFile file,
            string templateName,
            string? sheetName = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate multiple files against the same template.
        /// Efficient for batch validation scenarios.
        /// </summary>
        /// <param name="files">The Excel files to validate.</param>
        /// <param name="template">The template to validate against.</param>
        /// <param name="sheetName">Specific sheet to validate in each file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Validation reports for each file.</returns>
        Task<IReadOnlyList<ValidationReport>> ValidateBatchAsync(
            IEnumerable<ExcelFile> files,
            ExcelTemplate template,
            string? sheetName = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Quick validation check: does the file match the template structure?
        /// Faster than full validation, checks only column names and positions.
        /// </summary>
        /// <param name="file">The Excel file to check.</param>
        /// <param name="template">The template to check against.</param>
        /// <param name="sheetName">Specific sheet to check.</param>
        /// <returns>True if structure matches, false otherwise.</returns>
        bool QuickStructureCheck(
            ExcelFile file,
            ExcelTemplate template,
            string? sheetName = null);
    }
}
