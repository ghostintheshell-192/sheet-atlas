# ODS File Support

**Status**: backlog
**Release**: unassigned (target: pre-v1.0)
**Priority**: must-have
**Depends on**: file-loading.md

## Summary

Add support for OpenDocument Spreadsheet (.ods) format, used by LibreOffice Calc and other open-source office suites.

**Rationale**: SheetAtlas supports Linux. Without ODS support, Linux users can't use native LibreOffice files — this defeats the purpose of cross-platform support.

## User Stories

- As a LibreOffice user, I want to load .ods files directly
- As a user, I want to compare Excel files with ODS files
- As a user, I want the same features for ODS as for Excel

## Requirements

### Functional
- [ ] File Loading
  - [ ] Load .ods files
  - [ ] Parse all sheets
  - [ ] Handle cell types (text, number, date, formula)
  - [ ] Support cell formatting metadata

- [ ] Feature Parity
  - [ ] Search in .ods files
  - [ ] Compare .ods with .xlsx/.xls/.csv
  - [ ] Export from .ods data
  - [ ] Template validation for .ods

- [ ] Format Handling
  - [ ] ODS-specific date serial numbers
  - [ ] Currency formats
  - [ ] Formula preservation (display only)

### Non-Functional
- Performance: comparable to xlsx loading
- Compatibility: ODS 1.2+ specification

## Technical Notes

- ODS is XML-based (similar to xlsx but different schema)
- ODS files are ZIP containers with `content.xml` holding the data
- ODS uses different date epoch than Excel (1899-12-30 vs 1900-01-01)

### Library Options (researched Dec 2025)

| Library | Type | Notes |
|---------|------|-------|
| **DIY with SharpZipLib** | Free | Unzip + parse content.xml. Full control, matches our xlsx approach |
| **OdsReaderWriter** | Free (NuGet) | Simple read/write, may lack advanced features |
| **GemBox.Spreadsheet** | Commercial | Full-featured, supports ODS + Excel. Overkill? |

**Recommended**: DIY approach (SharpZipLib + XML parsing)
- Consistent with our xlsx reader architecture
- No external dependencies for core functionality
- Full control over cell type mapping to SACellValue

### ODS Structure (simplified)
```xml
<!-- content.xml inside .ods ZIP -->
<table:table table:name="Sheet1">
  <table:table-row>
    <table:table-cell office:value-type="float" office:value="42">
      <text:p>42</text:p>
    </table:table-cell>
  </table:table-row>
</table:table>
```

## Open Questions

- [x] ~~Which .NET library for ODS?~~ → DIY approach recommended
- [x] ~~How common is ODS among target users?~~ → Essential for Linux users
- [ ] Handle .ots (ODS template) files? Probably same as .ods

## Acceptance Criteria

- [ ] .ods files load correctly
- [ ] All cell types handled
- [ ] Search works as expected
- [ ] Comparison with other formats works
