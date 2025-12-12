using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.Services;

/// <summary>
/// Input for column linking: column info from a loaded file.
/// </summary>
public record ColumnInfo(
    string Name,
    DataType DetectedType,
    string SourceFile,
    string? SourceSheet = null);

/// <summary>
/// Service for linking semantically equivalent columns across files.
/// </summary>
public interface IColumnLinkingService
{
    /// <summary>
    /// Auto-group columns by name + type.
    /// Columns with the same name AND same type are grouped together.
    /// Columns with the same name but different types remain separate.
    /// </summary>
    IReadOnlyList<ColumnLink> CreateInitialGroups(IEnumerable<ColumnInfo> columns);

    /// <summary>
    /// Find the ColumnLink that matches a given column name.
    /// Matches against SemanticName or any LinkedColumn.Name.
    /// </summary>
    ColumnLink? FindMatchingLink(string columnName, IEnumerable<ColumnLink> links);

    /// <summary>
    /// Extract column info from loaded Excel files.
    /// </summary>
    IEnumerable<ColumnInfo> ExtractColumnsFromFiles(IEnumerable<ExcelFile> files);
}

/// <summary>
/// Implementation of column linking service.
/// </summary>
public class ColumnLinkingService : IColumnLinkingService
{
    public IReadOnlyList<ColumnLink> CreateInitialGroups(IEnumerable<ColumnInfo> columns)
    {
        var columnList = columns.ToList();
        if (columnList.Count == 0)
            return Array.Empty<ColumnLink>();

        // Group by (name, type) - case insensitive name comparison
        var groups = columnList
            .GroupBy(c => (
                Name: c.Name.Trim().ToLowerInvariant(),
                Type: c.DetectedType
            ))
            .ToList();

        var result = new List<ColumnLink>();

        foreach (var group in groups)
        {
            var linkedColumns = group
                .Select(c => LinkedColumn.Create(
                    c.Name,
                    c.DetectedType,
                    c.SourceFile,
                    c.SourceSheet))
                .ToArray();

            // Use the first column's original name (preserving case) as semantic name
            var firstColumn = group.First();
            var link = ColumnLink.FromGroup(
                firstColumn.Name,
                firstColumn.DetectedType,
                linkedColumns);

            result.Add(link);
        }

        // Sort by: semantic name (alphabetically)
        return result
            .OrderBy(l => l.SemanticName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ColumnLink? FindMatchingLink(string columnName, IEnumerable<ColumnLink> links)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            return null;

        foreach (var link in links)
        {
            if (link.Matches(columnName))
                return link;
        }

        return null;
    }

    public IEnumerable<ColumnInfo> ExtractColumnsFromFiles(IEnumerable<ExcelFile> files)
    {
        foreach (var file in files)
        {
            if (file.Status != LoadStatus.Success && file.Status != LoadStatus.PartialSuccess)
                continue;

            foreach (var sheetName in file.GetSheetNames())
            {
                var sheet = file.GetSheet(sheetName);
                if (sheet == null)
                    continue;

                var headers = sheet.ColumnNames;

                for (int i = 0; i < headers.Length; i++)
                {
                    var header = headers[i];
                    if (string.IsNullOrWhiteSpace(header))
                        continue;

                    var metadata = sheet.GetColumnMetadata(i);
                    var type = metadata?.DetectedType ?? DataType.Unknown;

                    yield return new ColumnInfo(
                        Name: header,
                        DetectedType: type,
                        SourceFile: file.FileName,
                        SourceSheet: sheetName);
                }
            }
        }
    }
}
