# Extensible SheetData Design - APPROVED

## Requirements

1. **Efficient Storage**: Replace DataTable (10-14x overhead) with lightweight structure (2-3x)
2. **Type Preservation**: Store values in native types (CellValue) instead of all-string
3. **Extensibility**: Support future metadata (formulas, styles, validation, data cleaning)
4. **Maintainability**: Clear structure, easy to understand and modify
5. **Performance**: Fast access for search, comparison, display

## Core Principles

- ✅ **No legacy code** - Direct refactoring, single code path
- ✅ **Progressive enhancement** - Core data always present, metadata on-demand
- ✅ **Future-proof** - Support for templates and data cleaning built-in
- ✅ **Memory efficient** - Metadata only where needed (~5-10% of cells)

## Structure

### Layer 1: CellData (Value + Optional Metadata)

```csharp
/// <summary>
/// Complete cell information: value + optional metadata.
/// Most cells have only value (no metadata) for memory efficiency.
/// </summary>
public readonly struct CellData : IEquatable<CellData>
{
    private readonly CellValue _value;
    private readonly CellMetadata? _metadata;

    public CellData(CellValue value, CellMetadata? metadata = null)
    {
        _value = value;
        _metadata = metadata;
    }

    public CellValue Value => _value;
    public CellMetadata? Metadata => _metadata;
    public bool HasMetadata => _metadata != null;

    // Memory: 24 bytes per cell (16 CellValue + 8 reference)
}

/// <summary>
/// Optional cell metadata for validation, cleaning, formulas, styles.
/// </summary>
public class CellMetadata
{
    // Data cleaning support
    public CellValue? OriginalValue { get; set; }  // Before cleaning
    public CellValue? CleanedValue { get; set; }   // After cleaning
    public DataQualityIssue? QualityIssue { get; set; }

    // Validation support
    public DataValidation? Validation { get; set; }

    // Formula support (future)
    public string? Formula { get; set; }

    // Style support (future)
    public CellStyle? Style { get; set; }

    // Extensibility
    public Dictionary<string, object>? CustomData { get; set; }
}

public enum DataQualityIssue
{
    None,
    ExtraWhitespace,
    InconsistentFormat,
    InvalidCharacters,
    TypeMismatch,
    OutOfRange,
    DuplicateValue
}
```

### Layer 2: SheetData (Storage)

```csharp
/// <summary>
/// Efficient sheet storage with typed cell values.
/// Replaces DataTable with 2-3x overhead instead of 10-14x.
/// </summary>
public class SheetData : IDisposable
{
    private bool _disposed = false;

    public string SheetName { get; }
    public string[] ColumnNames { get; }
    public List<CellData[]> Rows { get; }

    // Optional metadata (lazy-loaded)
    private Dictionary<string, MergedRange>? _mergedCells;
    private Dictionary<int, ColumnMetadata>? _columnMetadata;

    public SheetData(string sheetName, string[] columnNames)
    {
        SheetName = sheetName;
        ColumnNames = columnNames;
        Rows = new List<CellData[]>();
    }

    public void AddRow(CellData[] rowData)
    {
        if (rowData.Length != ColumnNames.Length)
            throw new ArgumentException($"Row has {rowData.Length} cells, expected {ColumnNames.Length}");
        Rows.Add(rowData);
    }

    public CellValue GetCellValue(int row, int column)
    {
        if (row < 0 || row >= Rows.Count || column < 0 || column >= ColumnNames.Length)
            return CellValue.Empty;
        return Rows[row][column].Value;
    }

    public CellMetadata? GetCellMetadata(int row, int column)
    {
        if (row < 0 || row >= Rows.Count || column < 0 || column >= ColumnNames.Length)
            return null;
        return Rows[row][column].Metadata;
    }

    public int RowCount => Rows.Count;
    public int ColumnCount => ColumnNames.Length;

    public void Dispose()
    {
        if (_disposed) return;
        Rows.Clear();
        _mergedCells?.Clear();
        _columnMetadata?.Clear();
        _disposed = true;
    }
}

public record MergedRange(int StartRow, int StartCol, int EndRow, int EndCol);
public record ColumnMetadata(double? Width, bool IsHidden);
```

### Layer 3: Template & Validation Support

```csharp
/// <summary>
/// Column template for validation and data cleaning.
/// </summary>
public record ColumnTemplate
{
    public string ColumnName { get; init; }
    public CellType ExpectedType { get; init; }
    public DataValidation? Validation { get; init; }
    public bool IsRequired { get; init; }
}

/// <summary>
/// Sheet template for file structure validation.
/// </summary>
public class SheetTemplate
{
    public string SheetName { get; init; }
    public List<ColumnTemplate> Columns { get; init; }

    public ValidationResult ValidateSheet(SheetData sheet)
    {
        // Check column existence
        // Check cell types match expected
        // Apply validation rules
        // Return results
    }
}

/// <summary>
/// Data validation rules.
/// </summary>
public record DataValidation
{
    public ValidationType Type { get; init; }
    public object? MinValue { get; init; }
    public object? MaxValue { get; init; }
    public string? Formula { get; init; }
}

public enum ValidationType
{
    None,
    WholeNumber,
    Decimal,
    List,
    Date,
    Custom
}
```

### Layer 4: Data Cleaning Support

```csharp
/// <summary>
/// Service for automatic data cleaning during file load.
/// </summary>
public class DataCleaningService
{
    public CellData CleanCell(CellData cell, ColumnTemplate template)
    {
        // If text but should be number
        if (cell.Value.IsText && template.ExpectedType == CellType.Number)
        {
            var cleanedValue = ExtractNumber(cell.Value.AsText());
            if (cleanedValue.HasValue)
            {
                var metadata = new CellMetadata
                {
                    OriginalValue = cell.Value,
                    CleanedValue = CellValue.FromNumber(cleanedValue.Value),
                    QualityIssue = DataQualityIssue.TypeMismatch
                };
                return new CellData(CellValue.FromNumber(cleanedValue.Value), metadata);
            }
        }

        // Add more cleaning rules...

        return cell;
    }
}

/// <summary>
/// Report on data quality issues found in a sheet.
/// </summary>
public class DataQualityReport
{
    public int TotalCells { get; set; }
    public int CleanCells { get; set; }
    public int DirtyCells { get; set; }
    public Dictionary<DataQualityIssue, int> IssuesByType { get; set; }

    public static DataQualityReport Analyze(SheetData sheet)
    {
        // Scan all cells, count those with metadata.QualityIssue
    }
}
```

## Memory Comparison (1000 rows × 20 cols = 20K cells)

| Component | DataTable | SheetData | Savings |
|-----------|-----------|-----------|---------|
| Cell values | 560 KB (string) | 320 KB (CellValue) | 43% |
| Row/Col metadata | 100 KB | 40 KB | 60% |
| Storage overhead | 5.6 MB (10x) | 160 KB (struct refs) | 97% |
| **TOTAL** | **~6.3 MB** | **~530 KB** | **91%** |

With 10% metadata cells: ~645 KB (90% reduction)

## Implementation Plan (No Legacy Code)

### Phase 1: Core Types
1. ✓ CellValue.cs (done)
2. CellData.cs (struct + metadata)
3. SheetData.cs (storage)
4. Update ExcelFile.cs

### Phase 2: Reading Layer
1. Update ICellValueReader → return CellValue
2. Update CellValueReader implementation
3. Update OpenXmlFileReader → produce SheetData
4. Update XlsFileReader, CsvFileReader

### Phase 3: Application Layer
1. Update SearchService (read CellValue)
2. Update SearchResult (store CellValue)
3. Update RowComparisonService

### Phase 4: UI Layer
1. Add .ToString() in ViewModels where needed
2. Test display binding

## Future Extensions (Supported by Design)

### Template Validation
```csharp
var template = new SheetTemplate
{
    SheetName = "Fatture",
    Columns = [
        new ColumnTemplate
        {
            ColumnName = "Importo",
            ExpectedType = CellType.Number,
            IsRequired = true
        }
    ]
};
var result = template.ValidateSheet(loadedSheet);
```

### Data Cleaning
```csharp
var cleaner = new DataCleaningService();
var cleanedCell = cleaner.CleanCell(dirtyCell, columnTemplate);
var report = DataQualityReport.Analyze(sheet);
```

### Formulas (Future)
```csharp
var metadata = new CellMetadata
{
    Formula = "=A1+B1",
    // Value stores computed result
};
```

## Expected Results

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Memory (40 MB data) | 593 MB | ~80 MB | 87% |
| Ratio (RAM/Data) | 15:1 | 2:1 | 87% |
| Load time | Baseline | Similar/Faster | No regression |
| Search time | Baseline | Faster | Type-aware |

---
**Status**: APPROVED - Ready for implementation
**Created**: 2025-10-11
**Next**: Implement Phase 1 (Core Types)
