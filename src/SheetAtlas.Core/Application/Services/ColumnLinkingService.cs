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
    /// Auto-group columns by name (case-insensitive).
    /// Columns with the same name are grouped together regardless of type.
    /// Use HasCaseVariations/HasTypeVariations on ColumnLink to detect inconsistencies.
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

    /// <summary>
    /// Merge two column links into one.
    /// The result takes the semantic name from the target.
    /// </summary>
    ColumnLink MergeGroups(ColumnLink target, ColumnLink source);

    /// <summary>
    /// Ungroup a column link, returning individual links for each column.
    /// </summary>
    IReadOnlyList<ColumnLink> Ungroup(ColumnLink link);
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

        // Group by name only (case-insensitive) - type variations will show warnings
        var groups = columnList
            .GroupBy(c => c.Name.Trim().ToLowerInvariant())
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

            // Determine dominant type (most frequent)
            var dominantType = linkedColumns
                .GroupBy(c => c.DetectedType)
                .OrderByDescending(g => g.Count())
                .First().Key;

            var link = ColumnLink.FromGroup(
                firstColumn.Name,
                dominantType,
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

    public ColumnLink MergeGroups(ColumnLink target, ColumnLink source)
    {
        // Combine linked columns from both groups
        var mergedColumns = target.LinkedColumns
            .Concat(source.LinkedColumns)
            .ToArray();

        // Determine dominant type (most frequent)
        var dominantType = mergedColumns
            .GroupBy(c => c.DetectedType)
            .OrderByDescending(g => g.Count())
            .First().Key;

        return new ColumnLink
        {
            SemanticName = target.SemanticName,
            LinkedColumns = mergedColumns,
            DominantType = dominantType,
            IsAutoGrouped = false // Manual merge
        };
    }

    public IReadOnlyList<ColumnLink> Ungroup(ColumnLink link)
    {
        if (link.LinkedColumns.Count <= 1)
            return new[] { link };

        return link.LinkedColumns
            .Select(col => new ColumnLink
            {
                SemanticName = col.Name,
                LinkedColumns = new[] { col },
                DominantType = col.DetectedType,
                IsAutoGrouped = false
            })
            .ToArray();
    }
}
