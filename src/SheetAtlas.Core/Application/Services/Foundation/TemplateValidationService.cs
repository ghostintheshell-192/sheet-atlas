using System.Diagnostics;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Core.Application.Services.Foundation
{
    /// <summary>
    /// Service for validating Excel files against templates and creating templates from files.
    /// Uses IColumnAnalysisService for type detection and validation.
    /// </summary>
    public class TemplateValidationService : ITemplateValidationService
    {
        private readonly IColumnAnalysisService _columnAnalysisService;
        private const int DefaultSampleSize = 100;

        public TemplateValidationService(IColumnAnalysisService columnAnalysisService)
        {
            _columnAnalysisService = columnAnalysisService ?? throw new ArgumentNullException(nameof(columnAnalysisService));
        }

        /// <summary>
        /// Parameterless constructor for convenience (uses default services).
        /// </summary>
        public TemplateValidationService()
            : this(new ColumnAnalysisService())
        {
        }

        #region ITemplateValidationService Implementation

        public async Task<ValidationReport> ValidateAsync(
            ExcelFile file,
            ExcelTemplate template,
            string? sheetName = null,
            string? regionName = null,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Determine which sheet to validate
                var targetSheet = ResolveTargetSheet(file, template, sheetName);
                if (targetSheet == null)
                {
                    stopwatch.Stop();
                    return ValidationReport.Failed(
                        template,
                        file.FilePath,
                        $"Sheet not found: {sheetName ?? template.ExpectedSheetName ?? "first sheet"}",
                        stopwatch.Elapsed);
                }

                // Resolve DataRegion when specified
                DataRegion? region = regionName != null ? targetSheet.GetDataRegion(regionName) : null;

                // Validate template configuration first
                var templateErrors = template.Validate().ToList();
                if (templateErrors.Count > 0)
                {
                    stopwatch.Stop();
                    return ValidationReport.Failed(
                        template,
                        file.FilePath,
                        $"Invalid template: {string.Join("; ", templateErrors)}",
                        stopwatch.Elapsed);
                }

                var sheetIssues = new List<ValidationIssue>();
                var columnResults = new List<ColumnValidationResult>();

                // Check sheet name if expected
                if (!string.IsNullOrEmpty(template.ExpectedSheetName) &&
                    !template.ExpectedSheetName.Equals(targetSheet.SheetName, StringComparison.OrdinalIgnoreCase))
                {
                    sheetIssues.Add(ValidationIssue.SheetNameMismatch(
                        template.ExpectedSheetName, targetSheet.SheetName));
                }

                // Check row count limits
                if (template.MinDataRows > 0 && targetSheet.DataRowCount < template.MinDataRows)
                {
                    sheetIssues.Add(ValidationIssue.RowCountOutOfRange(
                        targetSheet.DataRowCount, template.MinDataRows, template.MaxDataRows));
                }
                if (template.MaxDataRows > 0 && targetSheet.DataRowCount > template.MaxDataRows)
                {
                    sheetIssues.Add(ValidationIssue.RowCountOutOfRange(
                        targetSheet.DataRowCount, template.MinDataRows, template.MaxDataRows));
                }

                // Build column name to index mapping (limited to region's column bounds when set)
                var actualColumns = BuildColumnMapping(targetSheet, region);

                // Validate each expected column
                foreach (var expected in template.Columns)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var result = await ValidateColumnAsync(expected, targetSheet, actualColumns, region, cancellationToken);
                    columnResults.Add(result);
                }

                // Check for extra columns if not allowed
                if (!template.AllowExtraColumns)
                {
                    foreach (var kvp in actualColumns)
                    {
                        var columnName = kvp.Key;
                        var columnIndex = kvp.Value;

                        if (template.FindColumn(columnName) == null)
                        {
                            var analysis = AnalyzeColumn(targetSheet, columnIndex, region);
                            columnResults.Add(ColumnValidationResult.Extra(columnName, columnIndex, analysis));
                        }
                    }
                }

                stopwatch.Stop();

                // Generate report
                return ValidationReport.WithIssues(
                    template,
                    file,
                    targetSheet.SheetName,
                    columnResults,
                    sheetIssues,
                    stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return ValidationReport.Failed(template, file.FilePath, ex.Message, stopwatch.Elapsed);
            }
        }

        public async Task<ExcelTemplate> CreateTemplateFromFileAsync(
            ExcelFile file,
            string templateName,
            string? sheetName = null,
            string? regionName = null,
            CancellationToken cancellationToken = default)
        {
            // Determine which sheet to use
            var targetSheet = sheetName != null
                ? file.GetSheet(sheetName)
                : file.Sheets.Values.FirstOrDefault();

            if (targetSheet == null)
            {
                throw new ArgumentException(
                    $"Sheet not found: {sheetName ?? "no sheets in file"}",
                    nameof(sheetName));
            }

            // Resolve DataRegion when specified
            DataRegion? region = regionName != null ? targetSheet.GetDataRegion(regionName) : null;

            var template = ExcelTemplate.Create(templateName);
            template.SourceFilePath = file.FilePath;
            template.ExpectedSheetName = targetSheet.SheetName;

            // Use region's HeaderRowCount when region has explicit header bounds; otherwise use sheet default
            template.HeaderRowCount = (region?.HeaderStartRow != null)
                ? region.HeaderRowCount
                : targetSheet.HeaderRowCount;

            // Determine column range (region bounds when set, otherwise all columns)
            int startCol = region?.StartColumn ?? 0;
            int endCol = region?.EndColumn ?? (targetSheet.ColumnCount - 1);
            endCol = Math.Min(endCol, targetSheet.ColumnCount - 1);

            // Analyze each column and create ExpectedColumn
            for (int colIndex = startCol; colIndex <= endCol; colIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var columnName = targetSheet.ColumnNames[colIndex];
                var analysis = AnalyzeColumn(targetSheet, colIndex, region);

                var expectedColumn = CreateExpectedColumnFromAnalysis(columnName, colIndex, analysis);
                template.AddColumn(expectedColumn);
            }

            return await Task.FromResult(template);
        }

        public async Task<IReadOnlyList<ValidationReport>> ValidateBatchAsync(
            IEnumerable<ExcelFile> files,
            ExcelTemplate template,
            string? sheetName = null,
            string? regionName = null,
            CancellationToken cancellationToken = default)
        {
            var reports = new List<ValidationReport>();

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var report = await ValidateAsync(file, template, sheetName, regionName, cancellationToken);
                reports.Add(report);
            }

            return reports;
        }

        public bool QuickStructureCheck(
            ExcelFile file,
            ExcelTemplate template,
            string? sheetName = null,
            string? regionName = null)
        {
            var targetSheet = ResolveTargetSheet(file, template, sheetName);
            if (targetSheet == null)
                return false;

            DataRegion? region = regionName != null ? targetSheet.GetDataRegion(regionName) : null;
            var actualColumns = BuildColumnMapping(targetSheet, region);

            // Check all required columns exist
            foreach (var expected in template.RequiredColumns)
            {
                // Check by name
                bool found = actualColumns.Keys.Any(name => expected.MatchesName(name));
                if (!found)
                    return false;

                // Check position if specified
                if (expected.Position >= 0)
                {
                    var matchingName = actualColumns.Keys.FirstOrDefault(name => expected.MatchesName(name));
                    if (matchingName != null && actualColumns[matchingName] != expected.Position)
                        return false;
                }
            }

            return true;
        }

        #endregion

        #region Private Methods

        private SASheetData? ResolveTargetSheet(ExcelFile file, ExcelTemplate template, string? sheetName)
        {
            if (!string.IsNullOrEmpty(sheetName))
                return file.GetSheet(sheetName);

            if (!string.IsNullOrEmpty(template.ExpectedSheetName))
                return file.GetSheet(template.ExpectedSheetName);

            return file.Sheets.Values.FirstOrDefault();
        }

        private static Dictionary<string, int> BuildColumnMapping(SASheetData sheet, DataRegion? region = null)
        {
            var mapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int startCol = region?.StartColumn ?? 0;
            int endCol = Math.Min(region?.EndColumn ?? (sheet.ColumnNames.Length - 1), sheet.ColumnNames.Length - 1);

            for (int i = startCol; i <= endCol; i++)
            {
                var name = sheet.ColumnNames[i];
                if (!string.IsNullOrWhiteSpace(name) && !mapping.ContainsKey(name))
                {
                    mapping[name] = i;
                }
            }

            return mapping;
        }

        private Task<ColumnValidationResult> ValidateColumnAsync(
            ExpectedColumn expected,
            SASheetData sheet,
            Dictionary<string, int> actualColumns,
            DataRegion? region,
            CancellationToken cancellationToken)
        {
            // Find matching column
            int? columnIndex = null;
            string? actualName = null;

            foreach (var kvp in actualColumns)
            {
                if (expected.MatchesName(kvp.Key))
                {
                    actualName = kvp.Key;
                    columnIndex = kvp.Value;
                    break;
                }
            }

            // Column not found
            if (!columnIndex.HasValue)
            {
                return Task.FromResult(ColumnValidationResult.Missing(expected));
            }

            // Analyze the column (scoped to region rows when applicable)
            var analysis = AnalyzeColumn(sheet, columnIndex.Value, region);

            // Collect issues
            var issues = new List<ValidationIssue>();

            // Position check
            if (expected.Position >= 0 && expected.Position != columnIndex.Value)
            {
                issues.Add(ValidationIssue.WrongPosition(
                    expected.Name, expected.Position, columnIndex.Value));
            }

            // Type check
            if (expected.ExpectedType != DataType.Unknown &&
                expected.ExpectedType != analysis.DetectedType)
            {
                issues.Add(ValidationIssue.TypeMismatch(
                    expected.Name,
                    columnIndex.Value,
                    expected.ExpectedType,
                    analysis.DetectedType,
                    analysis.TypeConfidence));
            }

            // Confidence check
            if (analysis.TypeConfidence < expected.MinTypeConfidence)
            {
                issues.Add(ValidationIssue.LowConfidence(
                    expected.Name,
                    columnIndex.Value,
                    analysis.TypeConfidence,
                    expected.MinTypeConfidence));
            }

            // Propagate cell anomalies as validation issues
            // This catches type mismatches within cells even when column type matches overall
            foreach (var anomaly in analysis.Anomalies.Where(a =>
                a.Issue == DataQualityIssue.TypeMismatch ||
                a.Issue == DataQualityIssue.InvalidCharacters))
            {
                var severity = anomaly.Severity >= Logging.Models.LogSeverity.Error
                    ? ValidationSeverity.Error
                    : ValidationSeverity.Warning;

                issues.Add(ValidationIssue.RuleViolation(
                    expected.Name,
                    columnIndex.Value,
                    anomaly.RowIndex,
                    $"R{anomaly.RowIndex + 1}",
                    "DataQuality",
                    anomaly.Message,
                    anomaly.CellValue.ToString(),
                    severity));
            }

            // Currency check
            if (!string.IsNullOrEmpty(expected.ExpectedCurrency))
            {
                var actualCurrency = analysis.Currency?.Code;
                if (actualCurrency == null ||
                    !expected.ExpectedCurrency.Equals(actualCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(ValidationIssue.CurrencyMismatch(
                        expected.Name,
                        columnIndex.Value,
                        expected.ExpectedCurrency,
                        actualCurrency));
                }
            }

            // Check for empty column
            if (expected.Rules.Any(r => r.Type == RuleType.NotEmpty))
            {
                var hasData = HasNonEmptyData(sheet, columnIndex.Value, region);
                if (!hasData)
                {
                    issues.Add(ValidationIssue.EmptyColumn(expected.Name, columnIndex.Value));
                }
            }

            // Check for unique values
            if (expected.Rules.Any(r => r.Type == RuleType.Unique))
            {
                var duplicates = FindDuplicateValues(sheet, columnIndex.Value, region);
                foreach (var dup in duplicates.Take(5)) // Limit to first 5
                {
                    issues.Add(ValidationIssue.DuplicateValue(
                        expected.Name,
                        columnIndex.Value,
                        dup.Value,
                        dup.Count));
                }
            }

            // Return result
            if (issues.Count == 0)
            {
                return Task.FromResult(ColumnValidationResult.Valid(expected, columnIndex.Value, analysis));
            }

            return Task.FromResult(ColumnValidationResult.WithIssues(expected, columnIndex.Value, analysis, issues));
        }

        private ColumnAnalysisResult AnalyzeColumn(SASheetData sheet, int columnIndex, DataRegion? region = null)
        {
            var columnName = columnIndex < sheet.ColumnNames.Length
                ? sheet.ColumnNames[columnIndex]
                : $"Column{columnIndex + 1}";

            // Sample data cells, scoped to region rows when available
            var sampleCells = new List<SACellValue>();
            var numberFormats = new List<string?>();

            int sampleCount = 0;
            var rows = region != null ? sheet.EnumerateDataRows(region) : sheet.EnumerateDataRows();
            foreach (var row in rows)
            {
                if (sampleCount >= DefaultSampleSize)
                    break;

                var cell = row[columnIndex];
                sampleCells.Add(cell.EffectiveValue);
                numberFormats.Add(cell.Metadata?.NumberFormat);
                sampleCount++;
            }

            return _columnAnalysisService.AnalyzeColumn(
                columnIndex,
                columnName,
                sampleCells,
                numberFormats);
        }

        private ExpectedColumn CreateExpectedColumnFromAnalysis(
            string columnName,
            int columnIndex,
            ColumnAnalysisResult analysis)
        {
            var expectedColumn = new ExpectedColumn
            {
                Name = columnName,
                Position = columnIndex, // Fixed position by default
                ExpectedType = analysis.DetectedType,
                IsRequired = true, // Default to required
                MinTypeConfidence = 0.8,
                ExpectedCurrency = analysis.Currency?.Code
            };

            // Add suggested rules based on detected type
            var rules = new List<ValidationRule>();

            rules.Add(ValidationRule.NotEmpty());
            rules.Add(ValidationRule.TypeMatch());

            if (analysis.DetectedType == DataType.Currency && analysis.Currency != null)
            {
                rules.Add(ValidationRule.Currency(analysis.Currency.Code));
            }

            return expectedColumn with { Rules = rules };
        }

        private bool HasNonEmptyData(SASheetData sheet, int columnIndex, DataRegion? region = null)
        {
            var rows = region != null ? sheet.EnumerateDataRows(region) : sheet.EnumerateDataRows();
            foreach (var row in rows)
            {
                var value = row[columnIndex].EffectiveValue;
                if (!value.IsEmpty)
                    return true;
            }
            return false;
        }

        private IEnumerable<(string Value, int Count)> FindDuplicateValues(SASheetData sheet, int columnIndex, DataRegion? region = null)
        {
            var valueCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var rows = region != null ? sheet.EnumerateDataRows(region) : sheet.EnumerateDataRows();
            foreach (var row in rows)
            {
                var value = row[columnIndex].EffectiveValue;
                if (value.IsEmpty)
                    continue;

                var stringValue = value.ToString() ?? string.Empty;
                valueCounts.TryGetValue(stringValue, out int count);
                valueCounts[stringValue] = count + 1;
            }

            return valueCounts
                .Where(kvp => kvp.Value > 1)
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => (kvp.Key, kvp.Value));
        }

        #endregion
    }
}
