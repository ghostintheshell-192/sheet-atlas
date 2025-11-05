namespace SheetAtlas.Core.Application.DTOs
{
    /// <summary>
    /// Result of detecting header rows in sheet.
    /// </summary>
    public record HeaderDetectionResult
    {
        /// <summary>Row indices (0-based) that contain headers.</summary>
        public IReadOnlyList<int> HeaderRowIndices { get; init; } = Array.Empty<int>();

        /// <summary>First data row index (after headers).</summary>
        public int FirstDataRowIndex { get; init; }

        /// <summary>Confidence: 0.0 - 1.0.</summary>
        public double Confidence { get; init; }

        /// <summary>Human-readable explanation.</summary>
        public string Reason { get; init; } = string.Empty;

        /// <summary>Factory for auto-detected single-row header (most common case).</summary>
        public static HeaderDetectionResult SingleHeader(int headerRow, double confidence, string reason) =>
            new()
            {
                HeaderRowIndices = new[] { headerRow },
                FirstDataRowIndex = headerRow + 1,
                Confidence = confidence,
                Reason = reason
            };

        /// <summary>Factory for multi-row headers.</summary>
        public static HeaderDetectionResult MultiRowHeaders(
            IReadOnlyList<int> headerRows,
            int firstDataRow,
            double confidence,
            string reason) =>
            new()
            {
                HeaderRowIndices = headerRows,
                FirstDataRowIndex = firstDataRow,
                Confidence = confidence,
                Reason = reason
            };
    }
}
