using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.Entities;

namespace SheetAtlas.Core.Application.Services.Foundation
{
    /// <summary>
    /// File-based repository for managing Excel templates.
    /// Stores templates as JSON files in a configurable directory.
    /// </summary>
    public class TemplateRepository : ITemplateRepository
    {
        private const string TemplateExtension = ".json";
        private const string DefaultSubfolder = "SheetAtlas/Templates";

        public string TemplatesDirectory { get; }

        /// <summary>
        /// Creates a template repository with default templates directory.
        /// Default: {UserDocuments}/SheetAtlas/Templates
        /// </summary>
        public TemplateRepository()
            : this(GetDefaultTemplatesDirectory())
        {
        }

        /// <summary>
        /// Creates a template repository with custom templates directory.
        /// </summary>
        public TemplateRepository(string templatesDirectory)
        {
            if (string.IsNullOrWhiteSpace(templatesDirectory))
                throw new ArgumentException("Templates directory cannot be empty", nameof(templatesDirectory));

            TemplatesDirectory = templatesDirectory;
            EnsureDirectoryExists();
        }

        #region ITemplateRepository Implementation

        public async Task<IReadOnlyList<TemplateSummary>> ListTemplatesAsync(
            CancellationToken cancellationToken = default)
        {
            var summaries = new List<TemplateSummary>();

            if (!Directory.Exists(TemplatesDirectory))
                return summaries;

            var files = Directory.GetFiles(TemplatesDirectory, $"*{TemplateExtension}");

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var template = await LoadTemplateFromPathAsync(file, cancellationToken);
                    if (template != null)
                    {
                        summaries.Add(TemplateSummary.FromTemplate(template, file));
                    }
                }
                catch
                {
                    // Skip invalid template files
                }
            }

            return summaries.OrderBy(s => s.Name).ToList();
        }

        public async Task<ExcelTemplate?> LoadTemplateAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            var path = GetTemplatePath(name);
            return await LoadTemplateFromPathAsync(path, cancellationToken);
        }

        public async Task<ExcelTemplate?> LoadTemplateFromPathAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return ExcelTemplate.FromJson(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task<string> SaveTemplateAsync(
            ExcelTemplate template,
            bool overwrite = false,
            CancellationToken cancellationToken = default)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            if (string.IsNullOrWhiteSpace(template.Name))
                throw new ArgumentException("Template name is required", nameof(template));

            var path = GetTemplatePath(template.Name);

            if (!overwrite && File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"Template '{template.Name}' already exists. Use overwrite=true to replace.");
            }

            await SaveTemplateToPathAsync(template, path, cancellationToken);
            return path;
        }

        public async Task SaveTemplateToPathAsync(
            ExcelTemplate template,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required", nameof(filePath));

            // Update modification timestamp
            template.ModifiedAt = DateTime.UtcNow;

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = template.ToJson();
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }

        public async Task<bool> DeleteTemplateAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            var path = GetTemplatePath(name);

            if (!File.Exists(path))
                return false;

            File.Delete(path);
            return await Task.FromResult(true);
        }

        public bool TemplateExists(string name)
        {
            var path = GetTemplatePath(name);
            return File.Exists(path);
        }

        public string GetTemplatePath(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Template name cannot be empty", nameof(name));

            // Sanitize name for file system
            var safeName = SanitizeTemplateName(name);
            return Path.Combine(TemplatesDirectory, safeName + TemplateExtension);
        }

        public async Task<ExcelTemplate> ImportTemplateAsync(
            string sourcePath,
            bool overwrite = false,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Source template file not found", sourcePath);

            var template = await LoadTemplateFromPathAsync(sourcePath, cancellationToken)
                ?? throw new InvalidOperationException("Invalid template file");

            await SaveTemplateAsync(template, overwrite, cancellationToken);
            return template;
        }

        public async Task ExportTemplateAsync(
            string name,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            var template = await LoadTemplateAsync(name, cancellationToken)
                ?? throw new InvalidOperationException($"Template '{name}' not found");

            await SaveTemplateToPathAsync(template, destinationPath, cancellationToken);
        }

        #endregion

        #region Private Methods

        private static string GetDefaultTemplatesDirectory()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documents, DefaultSubfolder);
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(TemplatesDirectory))
            {
                Directory.CreateDirectory(TemplatesDirectory);
            }
        }

        private static string SanitizeTemplateName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safeName = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));

            // Remove leading/trailing spaces and dots
            safeName = safeName.Trim().TrimEnd('.');

            // Ensure not empty after sanitization
            if (string.IsNullOrEmpty(safeName))
                safeName = "template";

            return safeName;
        }

        #endregion
    }
}
