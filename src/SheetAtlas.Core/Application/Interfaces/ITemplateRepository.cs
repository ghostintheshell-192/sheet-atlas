using SheetAtlas.Core.Domain.Entities;

namespace SheetAtlas.Core.Application.Interfaces
{
    /// <summary>
    /// Repository for managing Excel templates (CRUD operations).
    /// Templates are stored as JSON files in a user-configurable location.
    /// </summary>
    public interface ITemplateRepository
    {
        /// <summary>
        /// Get the default templates directory path.
        /// </summary>
        string TemplatesDirectory { get; }

        /// <summary>
        /// List all available templates.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of template summaries.</returns>
        Task<IReadOnlyList<TemplateSummary>> ListTemplatesAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Load a template by name.
        /// </summary>
        /// <param name="name">Template name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The template, or null if not found.</returns>
        Task<ExcelTemplate?> LoadTemplateAsync(
            string name,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Load a template from a specific file path.
        /// </summary>
        /// <param name="filePath">Full path to template JSON file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The template, or null if file doesn't exist or is invalid.</returns>
        Task<ExcelTemplate?> LoadTemplateFromPathAsync(
            string filePath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Save a template.
        /// </summary>
        /// <param name="template">The template to save.</param>
        /// <param name="overwrite">If true, overwrites existing template with same name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The file path where template was saved.</returns>
        Task<string> SaveTemplateAsync(
            ExcelTemplate template,
            bool overwrite = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Save a template to a specific file path.
        /// </summary>
        /// <param name="template">The template to save.</param>
        /// <param name="filePath">Full path for the template file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SaveTemplateToPathAsync(
            ExcelTemplate template,
            string filePath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a template by name.
        /// </summary>
        /// <param name="name">Template name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if deleted, false if not found.</returns>
        Task<bool> DeleteTemplateAsync(
            string name,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if a template exists.
        /// </summary>
        /// <param name="name">Template name.</param>
        /// <returns>True if template exists.</returns>
        bool TemplateExists(string name);

        /// <summary>
        /// Get the file path for a template by name.
        /// </summary>
        /// <param name="name">Template name.</param>
        /// <returns>Full path to template file.</returns>
        string GetTemplatePath(string name);

        /// <summary>
        /// Import a template from an external location.
        /// Copies the template to the templates directory.
        /// </summary>
        /// <param name="sourcePath">Source template file path.</param>
        /// <param name="overwrite">If true, overwrites existing template with same name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The imported template.</returns>
        Task<ExcelTemplate> ImportTemplateAsync(
            string sourcePath,
            bool overwrite = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Export a template to an external location.
        /// </summary>
        /// <param name="name">Template name.</param>
        /// <param name="destinationPath">Destination file path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ExportTemplateAsync(
            string name,
            string destinationPath,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Summary information about a template (for listing).
    /// </summary>
    public record TemplateSummary
    {
        /// <summary>Template name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Template description.</summary>
        public string? Description { get; init; }

        /// <summary>Template version.</summary>
        public string Version { get; init; } = "1.0";

        /// <summary>Number of expected columns.</summary>
        public int ColumnCount { get; init; }

        /// <summary>Number of required columns.</summary>
        public int RequiredColumnCount { get; init; }

        /// <summary>Creation timestamp.</summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>Last modification timestamp.</summary>
        public DateTime ModifiedAt { get; init; }

        /// <summary>Full path to template file.</summary>
        public string FilePath { get; init; } = string.Empty;

        /// <summary>Create summary from a template.</summary>
        public static TemplateSummary FromTemplate(ExcelTemplate template, string filePath) =>
            new()
            {
                Name = template.Name,
                Description = template.Description,
                Version = template.Version,
                ColumnCount = template.Columns.Count,
                RequiredColumnCount = template.Columns.Count(c => c.IsRequired),
                CreatedAt = template.CreatedAt,
                ModifiedAt = template.ModifiedAt,
                FilePath = filePath
            };
    }
}
