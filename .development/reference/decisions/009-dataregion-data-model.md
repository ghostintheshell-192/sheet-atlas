# Decision 009: DataRegion Data Model

**Date**: February 2026
**Status**: Proposed
**Impact**: critical
**Related**: data-region-selection spec, ADR-002 (row indexing)
**Summary**: DataRegions stored as Dictionary<string, DataRegion> with Name as key. No ActiveRegion concept. ColumnMetadata is per-region, not global.

## Context

The DataRegion feature allows users to define rectangular areas within Excel sheets for focused operations (search, compare, export). During architecture review, several questions arose about how to represent regions in the data model:

1. What data structure for storing multiple regions?
2. Should there be an "ActiveRegion" concept?
3. Where does ColumnMetadata belong (global or per-region)?

## Decision

### 1. Dictionary<string, DataRegion> for Storage

Regions are stored in a `Dictionary<string, DataRegion>` where the key is the region's Name.

```csharp
// In SASheetData:
private Dictionary<string, DataRegion>? _dataRegions;

// Lookup by name:
var salesRegion = _dataRegions["Sales Data"];

// Add with automatic uniqueness validation:
_dataRegions[region.Name] = region;
```

**Alternatives considered**:

| Structure | Pro | Contra |
| --------- | --- | ------ |
| `List<DataRegion>` | Simple, order preserved | O(n) lookup, manual duplicate check |
| `HashSet<DataRegion>` | No duplicates, O(1) | Must create dummy region to search |
| `Dictionary<string, DataRegion>` | O(1) lookup by name, no duplicates | Name becomes required |

**Why Dictionary**: The most common operations are lookup by name ("get region Sales Data") and uniqueness validation. Dictionary handles both efficiently.

### 2. No ActiveRegion Concept

The original design included `DataRegion? ActiveRegion` in SASheetData to track "the currently selected region for operations". This has been **eliminated**.

**Original design**:

```csharp
SASheetData {
  List<DataRegion> DataRegions
  DataRegion? ActiveRegion  // ← REMOVED
}
```

**Why removed**:

- **Ambiguous semantics**: Is ActiveRegion per-file or global? Synced with UI selection?
- **Implicit behavior**: Search would use ActiveRegion "by default" - hidden magic
- **Limited flexibility**: Single active region, but user might want to search in multiple

**Replacement**: Explicit selection via UI (Sidebar Regions). User selects which regions to use for each operation. No hidden defaults.

### 3. ColumnMetadata Per-Region

Each DataRegion has its own ColumnMetadata, stored separately.

```csharp
// In SASheetData:
private Dictionary<string, Dictionary<int, ColumnMetadata>>? _regionColumnMetadata;
// key = regionName, value = (columnIndex → metadata)

// Access:
public ColumnMetadata? GetColumnMetadata(string regionName, int columnIndex);
public void SetColumnMetadata(string regionName, int columnIndex, ColumnMetadata metadata);
```

**Why per-region**: Each region can have different headers and different type detection. Region A might have columns "ProductID, Name, Price" while Region B has "Date, Amount, Notes". Global ColumnMetadata would cause conflicts.

## Rationale

- **Dictionary**: Most efficient for the operations we need (name lookup, uniqueness)
- **No ActiveRegion**: Explicit is better than implicit. User controls, not hidden defaults.
- **Per-region metadata**: Follows the "DataRegion = interpretive lens" model. Each lens has its own view of the data.

### 4. Default Region Name

When no user-defined regions exist, a single default DataRegion covers the entire sheet. Its name is the **sheet name** (e.g., "Data", "Sheet1"). This provides a natural, meaningful identifier and avoids magic strings.

```csharp
// Default region for sheet "Data":
var defaultRegion = DataRegion.WholeSheet("Data", rowCount, colCount);
// Key in Dictionary: "Data"
```

## Consequences

- `DataRegion.Name` becomes **required** (not nullable)
- Default region name = sheet name (covers entire sheet)
- Services receive explicit region parameter (no "use ActiveRegion by default")
- UI layer handles region selection (Sidebar Regions)
- More storage for ColumnMetadata (one dictionary per region instead of one global)
- Cleaner API: no questions about "which region is active?"

## Related Decisions

- See `data-region-selection-progress.md` for full discussion (Q8, Q10, Q12)
- ADR-002: Row indexing semantics (absolute indexing within regions)
