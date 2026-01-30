namespace SheetAtlas.Core.Application.Services
{
    using SheetAtlas.Core.Application.Interfaces;

    /// <summary>
    /// Groups headers by semantic name, merging columns that map to the same name.
    /// Consolidates header grouping logic previously duplicated across
    /// ComparisonExportService and RowComparisonViewModel.
    /// </summary>
    public class HeaderGroupingService : IHeaderGroupingService
    {
        /// <inheritdoc/>
        public IReadOnlyList<HeaderGroup> GroupHeaders(
            IReadOnlyList<string> headers,
            IHeaderResolver? resolver = null,
            IEnumerable<string>? includedColumns = null)
        {
            ArgumentNullException.ThrowIfNull(headers);

            // Filter by includedColumns if provided
            var includedSet = includedColumns != null
                ? new HashSet<string>(includedColumns, StringComparer.OrdinalIgnoreCase)
                : null;

            // Group by display name (semantic name if available, else original)
            var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var originalHeader in headers)
            {
                // Skip if not in included set
                if (includedSet != null && !includedSet.Contains(originalHeader))
                    continue;

                // Get display name (semantic name if resolver available, else original)
                var displayName = resolver?.ResolveSemanticName(originalHeader) ?? originalHeader;

                // Group by display name
                if (!groups.TryGetValue(displayName, out var originalHeaders))
                {
                    originalHeaders = new List<string>();
                    groups[displayName] = originalHeaders;
                }
                originalHeaders.Add(originalHeader);
            }

            return groups
                .Select(g => new HeaderGroup(g.Key, g.Value))
                .ToList();
        }
    }
}
