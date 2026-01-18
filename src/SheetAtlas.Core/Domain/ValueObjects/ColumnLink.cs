using System.Text.Json.Serialization;

namespace SheetAtlas.Core.Domain.ValueObjects;

/// <summary>
/// Links multiple column names to a single semantic concept.
/// Used for grouping semantically equivalent columns across files.
/// </summary>
public sealed record ColumnLink
{
    /// <summary>
    /// User-assigned semantic name for this group of columns.
    /// Example: "Revenue" for columns named "Rev 2016", "Rev 2017", etc.
    /// </summary>
    public string SemanticName { get; init; } = string.Empty;

    /// <summary>
    /// The columns linked under this semantic name.
    /// </summary>
    public IReadOnlyList<LinkedColumn> LinkedColumns { get; init; } = Array.Empty<LinkedColumn>();

    /// <summary>
    /// The dominant data type across all linked columns.
    /// Used for display and matching decisions.
    /// </summary>
    public DataType DominantType { get; init; } = DataType.Unknown;

    /// <summary>
    /// Whether this link was created automatically (true) or manually by user (false).
    /// Auto-created links use the first column name as SemanticName.
    /// </summary>
    [JsonIgnore]
    public bool IsAutoGrouped { get; init; } = true;

    /// <summary>
    /// Gets the number of linked columns.
    /// </summary>
    [JsonIgnore]
    public int ColumnCount => LinkedColumns.Count;

    /// <summary>
    /// Gets the number of distinct source files.
    /// </summary>
    [JsonIgnore]
    public int SourceCount => LinkedColumns
        .Where(c => !string.IsNullOrEmpty(c.SourceFile))
        .Select(c => c.SourceFile)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    // === Warning Properties ===

    /// <summary>
    /// True if linked columns have different casing for the same base name (e.g., "EBIT" vs "ebit").
    /// Does not trigger for intentionally different names (e.g., "2016 VAR" vs "2017 VAR").
    /// </summary>
    [JsonIgnore]
    public bool HasCaseVariations =>
        LinkedColumns.Count > 1 &&
        LinkedColumns
            .GroupBy(c => c.Name.Trim().ToLowerInvariant())
            .Any(g => g.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count() > 1);

    /// <summary>
    /// True if linked columns have different detected types (e.g., Currency vs Number).
    /// </summary>
    [JsonIgnore]
    public bool HasTypeVariations =>
        LinkedColumns.Count > 1 &&
        LinkedColumns
            .Select(c => c.DetectedType)
            .Distinct()
            .Count() > 1;

    /// <summary>
    /// True if there are any warnings (case or type variations).
    /// </summary>
    [JsonIgnore]
    public bool HasWarnings => HasCaseVariations || HasTypeVariations;

    /// <summary>
    /// Warning message for tooltip display.
    /// Returns null if no warnings.
    /// </summary>
    [JsonIgnore]
    public string? WarningMessage
    {
        get
        {
            if (!HasWarnings)
                return null;

            var messages = new List<string>();

            if (HasCaseVariations)
            {
                // Find groups with case variations and show the variants
                var caseVariants = LinkedColumns
                    .GroupBy(c => c.Name.Trim().ToLowerInvariant())
                    .Where(g => g.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count() > 1)
                    .SelectMany(g => g.Select(c => c.Name).Distinct(StringComparer.Ordinal))
                    .Take(3);
                messages.Add($"Case variations: {string.Join(", ", caseVariants)}");
            }

            if (HasTypeVariations)
            {
                var types = LinkedColumns
                    .Select(c => c.DetectedType)
                    .Distinct()
                    .OrderBy(t => t.ToString());
                messages.Add($"Type variations: {string.Join(", ", types)}");
            }

            return string.Join("\n", messages);
        }
    }

    /// <summary>
    /// Check if a column name matches this link.
    /// Matches against SemanticName or any LinkedColumn.Name (case-insensitive).
    /// </summary>
    public bool Matches(string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            return false;

        var normalized = columnName.Trim();

        // Match semantic name
        if (SemanticName.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            return true;

        // Match any linked column name
        foreach (var linked in LinkedColumns)
        {
            if (linked.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all names that match this link (semantic + all linked).
    /// </summary>
    [JsonIgnore]
    public IEnumerable<string> AllNames
    {
        get
        {
            yield return SemanticName;
            foreach (var linked in LinkedColumns)
            {
                if (!linked.Name.Equals(SemanticName, StringComparison.OrdinalIgnoreCase))
                    yield return linked.Name;
            }
        }
    }

    // === Factory Methods ===

    /// <summary>
    /// Creates a ColumnLink from a single column (no grouping yet).
    /// </summary>
    public static ColumnLink FromSingle(LinkedColumn column) =>
        new()
        {
            SemanticName = column.Name,
            LinkedColumns = new[] { column },
            DominantType = column.DetectedType,
            IsAutoGrouped = true
        };

    /// <summary>
    /// Creates a ColumnLink from multiple columns with the same name and type.
    /// </summary>
    public static ColumnLink FromGroup(string name, DataType type, IEnumerable<LinkedColumn> columns) =>
        new()
        {
            SemanticName = name,
            LinkedColumns = columns.ToArray(),
            DominantType = type,
            IsAutoGrouped = true
        };

    // === Builder Methods ===

    /// <summary>
    /// Returns a new ColumnLink with a different semantic name.
    /// </summary>
    public ColumnLink WithSemanticName(string name) =>
        this with { SemanticName = name, IsAutoGrouped = false };

    /// <summary>
    /// Returns a new ColumnLink with an additional linked column.
    /// </summary>
    public ColumnLink WithColumn(LinkedColumn column) =>
        this with { LinkedColumns = LinkedColumns.Append(column).ToArray() };

    /// <summary>
    /// Returns a new ColumnLink without the specified column.
    /// </summary>
    public ColumnLink WithoutColumn(LinkedColumn column) =>
        this with
        {
            LinkedColumns = LinkedColumns
                .Where(c => c != column)
                .ToArray()
        };
}
