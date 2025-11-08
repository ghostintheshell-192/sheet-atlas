using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Tests.Foundation.Fixtures
{
    /// <summary>
    /// Common setup and helper methods for foundation layer tests.
    /// Provides test data builders and assertions.
    /// </summary>
    public class FoundationLayerFixture
    {
        /// <summary>
        /// Creates a minimal test sheet with specified dimensions.
        /// </summary>
        public static SASheetData CreateTestSheet(string sheetName = "TestSheet", int rowCount = 10, int columnCount = 5)
        {
            var columnNames = Enumerable.Range(0, columnCount)
                .Select(i => $"Column{i + 1}")
                .ToArray();

            var sheet = new SASheetData(sheetName, columnNames, rowCount);

            for (int r = 0; r < rowCount; r++)
            {
                var row = new SACellData[columnCount];
                for (int c = 0; c < columnCount; c++)
                {
                    row[c] = new SACellData(SACellValue.FromText($"R{r}C{c}"));
                }
                sheet.AddRow(row);
            }

            return sheet;
        }

        /// <summary>
        /// Creates a sheet with merged cells.
        /// Merges cells in range A1:C1 with a header value.
        /// </summary>
        public static SASheetData CreateSheetWithMergedCells(
            string sheetName = "MergedSheet",
            string mergeRange = "A1:C1",
            object? mergeValue = null)
        {
            var sheet = CreateTestSheet(sheetName, 5, 5);
            mergeValue = mergeValue ?? "MergedHeader";

            // Add merged cell range A1:C1 (row 0, columns 0-2)
            // Note: MergedRange(StartRow, StartCol, EndRow, EndCol)
            sheet.AddMergedCell(mergeRange, new MergedRange(0, 0, 0, 2));

            return sheet;
        }

        /// <summary>
        /// Creates sample cell values with different data types for testing.
        /// </summary>
        public static List<SACellValue> CreateSampleCellValues()
        {
            return new List<SACellValue>
            {
                SACellValue.FromInteger(123),                  // Number
                SACellValue.FromText("hello"),                 // Text
                SACellValue.FromBoolean(true),                 // Boolean
                SACellValue.FromInteger(45292),                // Excel serial date
                SACellValue.FromText("2024-11-05"),            // ISO date string
                SACellValue.FromText("11/5/2024"),             // US date
                SACellValue.FromText("$1,234.56"),             // Currency
                SACellValue.FromText("€1.234,56"),             // European currency
                SACellValue.Empty,                             // Null/Empty
                SACellValue.FromText("")                       // Empty string
            };
        }

        /// <summary>
        /// Creates a set of Excel format strings for testing currency detection.
        /// </summary>
        public static List<string> CreateSampleExcelFormats()
        {
            return new List<string>
            {
                "[$€-407] #,##0.00",        // EUR German
                "[$$-409] #,##0.00",        // USD English
                "[$£-809] #,##0.00",        // GBP English
                "[$¥-411] #,##0",           // JPY Japanese
                "#,##0.00",                 // General number
                "General",                  // General format
                "mm/dd/yyyy",               // Date format
                "0%",                       // Percentage
                "@"                         // Text
            };
        }

        /// <summary>
        /// Verifies that a NormalizationResult is successful.
        /// </summary>
        public static void AssertNormalizationSuccess(
            SheetAtlas.Core.Application.DTOs.NormalizationResult result,
            string? expectedCleanedValue = null)
        {
            if (!result.IsSuccess)
                throw new AssertionException($"Normalization failed: {result.ErrorMessage}");

            if (result.CleanedValue == null)
                throw new AssertionException("Cleaned value is null after successful normalization");

            if (expectedCleanedValue != null && result.CleanedValue.ToString() != expectedCleanedValue)
                throw new AssertionException(
                    $"Expected cleaned value '{expectedCleanedValue}' but got '{result.CleanedValue}'");
        }

        /// <summary>
        /// Verifies that a NormalizationResult failed.
        /// </summary>
        public static void AssertNormalizationFailure(
            SheetAtlas.Core.Application.DTOs.NormalizationResult result)
        {
            if (result.IsSuccess)
                throw new AssertionException("Normalization succeeded but was expected to fail");

            if (result.CleanedValue != null)
                throw new AssertionException("Cleaned value should be null after failed normalization");
        }
    }

    /// <summary>
    /// Simple assertion exception for test utilities.
    /// </summary>
    public class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }
}
