using System.Text.Json;
using System.Text.Json.Serialization;
using SheetAtlas.Core.Application.Json;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Domain.Entities
{
    /// <summary>
    /// Represents an Excel file template that defines expected structure and validation rules.
    /// Used to validate incoming files against a known-good template.
    /// Supports JSON serialization for persistence and sharing.
    /// </summary>
    public sealed class ExcelTemplate
    {
        /// <summary>Template name (user-defined identifier).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional description of what this template validates.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }

        /// <summary>Template version for tracking changes.</summary>
        public string Version { get; set; } = "1.0";

        /// <summary>UTC timestamp when template was created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>UTC timestamp when template was last modified.</summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Source file path used to create this template (for reference).
        /// Not used in validation, just metadata.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SourceFilePath { get; set; }

        /// <summary>
        /// Expected sheet name to validate.
        /// If null, validates the first sheet or any sheet with matching structure.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ExpectedSheetName { get; set; }

        /// <summary>Number of expected header rows (default: 1).</summary>
        public int HeaderRowCount { get; set; } = 1;

        /// <summary>
        /// List of expected columns with their validation requirements.
        /// Order matters only if Position is specified in ExpectedColumn.
        /// </summary>
        public List<ExpectedColumn> Columns { get; set; } = new();

        /// <summary>
        /// Global validation rules applied to the entire sheet.
        /// These rules are checked after column-level validation.
        /// </summary>
        public List<ValidationRule> GlobalRules { get; set; } = new();

        /// <summary>
        /// Whether to allow extra columns not defined in template.
        /// If false, extra columns generate warnings.
        /// </summary>
        public bool AllowExtraColumns { get; set; } = true;

        /// <summary>
        /// Whether to require strict column ordering.
        /// If true, columns must appear in template order (respecting Position).
        /// </summary>
        public bool RequireStrictOrdering { get; set; } = false;

        /// <summary>
        /// Minimum number of data rows expected (excluding headers).
        /// 0 means no minimum.
        /// </summary>
        public int MinDataRows { get; set; } = 0;

        /// <summary>
        /// Maximum number of data rows allowed (excluding headers).
        /// 0 means no maximum.
        /// </summary>
        public int MaxDataRows { get; set; } = 0;

        // === Factory Methods ===

        /// <summary>Create an empty template with the given name.</summary>
        public static ExcelTemplate Create(string name) =>
            new() { Name = name, CreatedAt = DateTime.UtcNow, ModifiedAt = DateTime.UtcNow };

        /// <summary>Create a template from an existing file structure.</summary>
        public static ExcelTemplate FromFile(string name, string sourceFilePath, IEnumerable<ExpectedColumn> columns) =>
            new()
            {
                Name = name,
                SourceFilePath = sourceFilePath,
                Columns = columns.ToList(),
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };

        // === Builder Methods ===

        /// <summary>Add an expected column to the template.</summary>
        public ExcelTemplate AddColumn(ExpectedColumn column)
        {
            Columns.Add(column);
            ModifiedAt = DateTime.UtcNow;
            return this;
        }

        /// <summary>Add a global validation rule.</summary>
        public ExcelTemplate AddGlobalRule(ValidationRule rule)
        {
            GlobalRules.Add(rule);
            ModifiedAt = DateTime.UtcNow;
            return this;
        }

        /// <summary>Set expected sheet name.</summary>
        public ExcelTemplate ForSheet(string sheetName)
        {
            ExpectedSheetName = sheetName;
            ModifiedAt = DateTime.UtcNow;
            return this;
        }

        /// <summary>Configure header row count.</summary>
        public ExcelTemplate WithHeaders(int count)
        {
            HeaderRowCount = count;
            ModifiedAt = DateTime.UtcNow;
            return this;
        }

        /// <summary>Set data row limits.</summary>
        public ExcelTemplate WithRowLimits(int min = 0, int max = 0)
        {
            MinDataRows = min;
            MaxDataRows = max;
            ModifiedAt = DateTime.UtcNow;
            return this;
        }

        /// <summary>Configure column ordering strictness.</summary>
        public ExcelTemplate WithStrictOrdering(bool strict = true)
        {
            RequireStrictOrdering = strict;
            ModifiedAt = DateTime.UtcNow;
            return this;
        }

        /// <summary>Configure extra column handling.</summary>
        public ExcelTemplate DisallowExtraColumns()
        {
            AllowExtraColumns = false;
            ModifiedAt = DateTime.UtcNow;
            return this;
        }

        // === Lookup Methods ===

        /// <summary>Get all required columns.</summary>
        [JsonIgnore]
        public IEnumerable<ExpectedColumn> RequiredColumns =>
            Columns.Where(c => c.IsRequired);

        /// <summary>Get all optional columns.</summary>
        [JsonIgnore]
        public IEnumerable<ExpectedColumn> OptionalColumns =>
            Columns.Where(c => !c.IsRequired);

        /// <summary>Get columns that have fixed positions.</summary>
        [JsonIgnore]
        public IEnumerable<ExpectedColumn> PositionedColumns =>
            Columns.Where(c => c.Position >= 0);

        /// <summary>Find expected column by name (including alternatives).</summary>
        public ExpectedColumn? FindColumn(string columnName)
        {
            return Columns.FirstOrDefault(c => c.MatchesName(columnName));
        }

        // === Serialization ===

        /// <summary>Serialize template to JSON string.</summary>
        public string ToJson() =>
            JsonSerializer.Serialize(this, AppJsonContext.Default.ExcelTemplate);

        /// <summary>Deserialize template from JSON string.</summary>
        public static ExcelTemplate? FromJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize(json, AppJsonContext.Default.ExcelTemplate);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Save template to file.</summary>
        public async Task SaveAsync(string filePath)
        {
            var json = ToJson();
            await File.WriteAllTextAsync(filePath, json);
        }

        /// <summary>Load template from file.</summary>
        public static async Task<ExcelTemplate?> LoadAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath);
            return FromJson(json);
        }

        // === Validation Helpers ===

        /// <summary>Validate template configuration.</summary>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Name))
                errors.Add("Template name is required");

            if (Columns.Count == 0)
                errors.Add("Template must have at least one expected column");

            if (HeaderRowCount < 0)
                errors.Add("Header row count cannot be negative");

            if (MinDataRows < 0)
                errors.Add("Minimum data rows cannot be negative");

            if (MaxDataRows < 0)
                errors.Add("Maximum data rows cannot be negative");

            if (MaxDataRows > 0 && MinDataRows > MaxDataRows)
                errors.Add("Minimum data rows cannot exceed maximum");

            // Check for duplicate column names
            var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in Columns)
            {
                if (!columnNames.Add(col.Name))
                    errors.Add($"Duplicate column name: {col.Name}");
            }

            // Check for conflicting positions
            var positions = Columns
                .Where(c => c.Position >= 0)
                .GroupBy(c => c.Position)
                .Where(g => g.Count() > 1);

            foreach (var pos in positions)
            {
                var names = string.Join(", ", pos.Select(c => c.Name));
                errors.Add($"Multiple columns defined at position {pos.Key}: {names}");
            }

            return errors;
        }
    }
}
