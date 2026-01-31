# SheetAtlas Release Plan

**Updated**: 2026-01-29

---

## v0.4.0 (released 2025-12-16)

**Focus**: Template Management, Column Linking & Export Backend

| Spec | Status | Notes |
|------|--------|-------|
| `template-validation.md` | ✅ implemented | Templates tab, batch validate, per-file results |
| `column-linking.md` | ✅ implemented | Semantic names, grouping, highlighting, persistence |
| `column-grouping.md` | ✅ implemented | Runtime grouping via column-linking |
| `export-results.md` | 🔶 partial | Backend services done, UI not connected |

---

## v0.5.0 (released 2026-01-18)

**Focus**: Settings, Column Filtering, Export UI

| Spec | Status | Notes |
|------|--------|-------|
| `settings-configuration.md` | ✅ implemented | Theme, output folder, defaults |
| `column-filtering.md` | ✅ implemented | Checkboxes in sidebar |
| `column-grouping.md` | ✅ implemented | Warning badges for case/type variations |
| `export-results.md` | 🔶 partial | Normalized files done, comparison pending |

---

## v0.5.1 (released 2026-01-21)

**Focus**: Export Comparison Results

| Spec | Status | Notes |
|------|--------|-------|
| `export-results.md` | ✅ implemented | Numeric type/format preservation |

**New**: `ExportCellValue.cs` — wrapper struct for cell value + number format

---

## v0.5.2 (released 2026-01-28)

**Focus**: CI & Serialization Fixes

| Change | Notes |
|--------|-------|
| Source-generated JSON serialization | PublishTrimmed support |
| Auto-set version from git tag | Release builds |
| Remove .app bundle upload | macOS (unsigned) |

---

## v0.6.0 (planned)

**Focus**: Data Region & Headers

| Spec | Status |
|------|--------|
| `data-region-selection.md` | planned |
| `multi-row-headers.md` | planned |

---

## v0.7.0 (planned)

**Focus**: Template Application (Phase 1)

| Spec | Status |
|------|--------|
| `template-application.md` | planned |

---

## v0.8.0 (planned)

**Focus**: Analytics & Format Support

| Spec | Status |
|------|--------|
| `telemetry.md` | planned |
| `ods-support.md` | must-have (Linux support) |

---

## v0.9.0 (planned)

**Focus**: UI/UX Overhaul

| Spec | Status |
|------|--------|
| `ui-rework.md` | planned |

---

## v1.0.0 (stable release)

**Focus**: Production Ready

| Spec | Status |
|------|--------|
| Stabilization | planned |
| Documentation | planned |
| `payment-licensing.md` | planned |

---

## v1.1.0 (post-launch)

**Focus**: Web & Cleanup

| Spec | Status |
|------|--------|
| `web-app-cleanup.md` | should-have |

---

## Spec → Release Mapping

```
template-validation.md          → v0.4 ✅
column-linking.md               → v0.4 ✅
column-grouping.md              → v0.5 ✅
export-results.md               → v0.5.1 ✅
settings-configuration.md       → v0.5 ✅
column-filtering.md             → v0.5 ✅
data-region-selection.md        → v0.6
multi-row-headers.md            → v0.6
template-application.md         → v0.7
telemetry.md                    → v0.8
ods-support.md                  → v0.8
ui-rework.md                    → v0.9
payment-licensing.md            → v1.0
web-app-cleanup.md              → v1.1
cpp-core-optimization.md        → post-v1.0 (learning opportunity)
vertical-comparison.md          → post-v1.0 (nice-to-have)
```

---

*Last updated: 2026-01-29*
