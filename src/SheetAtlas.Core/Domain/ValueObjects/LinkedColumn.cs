using System.Text.Json.Serialization;

namespace SheetAtlas.Core.Domain.ValueObjects;

/// <summary>
/// Represents a column from a specific source file/sheet.
/// Used to track the origin of columns in a ColumnLink.
/// </summary>
public sealed record LinkedColumn
{
    /// <summary>Original column header name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Source file name (optional, for display).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceFile { get; init; }

    /// <summary>Source sheet name (optional, for display).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceSheet { get; init; }

    /// <summary>Detected data type of this column.</summary>
    public DataType DetectedType { get; init; } = DataType.Unknown;

    /// <summary>
    /// Creates a LinkedColumn with minimal information.
    /// </summary>
    public static LinkedColumn Create(string name, DataType type) =>
        new() { Name = name, DetectedType = type };

    /// <summary>
    /// Creates a LinkedColumn with full source tracking.
    /// </summary>
    public static LinkedColumn Create(string name, DataType type, string sourceFile, string? sourceSheet = null) =>
        new()
        {
            Name = name,
            DetectedType = type,
            SourceFile = sourceFile,
            SourceSheet = sourceSheet
        };

    /// <summary>
    /// Gets a display string for the source (e.g., "FileA.xlsx" or "FileA.xlsx:Sheet1").
    /// </summary>
    [JsonIgnore]
    public string SourceDisplay =>
        string.IsNullOrEmpty(SourceFile)
            ? string.Empty
            : string.IsNullOrEmpty(SourceSheet)
                ? SourceFile
                : $"{SourceFile}:{SourceSheet}";
}
