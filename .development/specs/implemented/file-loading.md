# File Loading

**Status**: implemented
**Release**: v0.1.0
**Priority**: must-have

## Summary

Load Excel files (.xlsx, .xls) and CSV files into the application for analysis, with robust error handling and format detection.

## User Stories

- As a user, I want to drag & drop files to load them quickly
- As a user, I want to load multiple files simultaneously for comparison
- As a user, I want clear feedback when a file fails to load

## Requirements

### Functional
- [x] Load .xlsx files (OpenXML format)
- [x] Load .xls files (legacy Excel format)
- [x] Load .csv files with delimiter auto-detection
- [x] Support multiple file loading
- [x] Display loading progress
- [x] Handle corrupted/invalid files gracefully
- [x] Show file metadata (sheets, row counts)

### Non-Functional
- Performance: <2 seconds for 10MB files
- Security: ZIP bomb protection, CSV injection sanitization

## Technical Notes

- Uses DocumentFormat.OpenXml for .xlsx
- Uses ExcelDataReader for .xls
- Custom CSV parser with encoding detection
- See ADR: `001-error-handling-philosophy.md`

## Acceptance Criteria

- [x] All three formats load successfully
- [x] Errors shown inline with retry option
- [x] Large files don't freeze UI
