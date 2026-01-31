# Foundation Layer - Known Limitations (Alpha)

Quick reference for accepted trade-offs and constraints in the Foundation Layer implementation.

**Status**: Active for v0.3.x - v0.4.x (Alpha/Beta)
**Review**: Before v1.0 release
**Last Updated**: 2025-11-28

---

## Parsing Constraints

| Feature | Limitation | Rationale | Revisit When |
|---------|-----------|-----------|--------------|
| Boolean "X" | Not supported as boolean | Conflicts with single-char text identifiers (ticker "X") | User reports checkbox data entry need |
| Header rows | Single-row only (HeaderRowCount=1) | 95% of real-world files use single header | v0.5 - UI control for manual multi-row selection |
| Date system | Auto-detect only (1900/1904) | Excel default (1900) works for vast majority | Mixed-system files reported in production |
| Currency mixing | Warning only, no auto-resolution | Complex to resolve automatically without context | Multi-currency comparison feature requested |
| Merge complexity | Warn at >20%, no rejection | User should decide if acceptable | High chaos reports in production |

---

## Type Detection Confidence

| Type | Confidence Level | Supported Patterns | Edge Cases |
|------|-----------------|-------------------|------------|
| Text | High | Any string | Single-char identifiers always treated as text |
| Number | High | Integer, floating-point, scientific notation | Ambiguous thousand separators (1,234 vs 1.234) |
| Date | Medium | Excel serial, ISO, US/EU formats | Ambiguous formats (01/02/2024 = Jan 2 or Feb 1?) |
| Boolean | High | true/yes/y/✓/✔/☑ (false/no/n/✗/✘/☐) | "X" explicitly NOT boolean (removed 2025-11-28) |
| Currency | Medium | Format-based detection ([$€-407] pattern) | Mixed currencies in same column flagged as warning |
| Percentage | High | Format-based + trailing % | Decimal vs percentage ambiguity (0.5 vs 50%) |

---

## Column Analysis Parameters

| Parameter | Alpha Value | Rationale | Configurable |
|-----------|------------|-----------|--------------|
| Sample size | 100 cells per column | Balance accuracy vs speed | Yes (appsettings.json) |
| Confidence threshold | >0.8 = strong type | 80% of sampled cells agree | Yes (appsettings.json) |
| Context window | ±3 cells (local anomaly detection) | Catches isolated errors without false positives | Hardcoded (Phase 2: ±6, ±9, ±12) |
| Warning penalty | Info=0%, Warning=2%, Error=5%, Critical=10% | Confidence reduced for data quality issues | Hardcoded |

---

## Performance Targets (Alpha)

| Metric | Target | Actual (Tested) | Status |
|--------|--------|-----------------|--------|
| File load (10MB) | <2 seconds | ~1.2 seconds | ✅ Met |
| Type detection overhead | <10% of load time | ~5% | ✅ Met |
| Memory usage (large files) | <500MB | ~250MB (10MB file) | ✅ Met |
| Test coverage | >80% | 100% (155/155 tests) | ✅ Exceeded |

---

## Merge Strategy Behavior

| Strategy | Behavior | Use Case | Default |
|----------|----------|----------|---------|
| ExpandValue | Replicate top-left value to all merged cells | Best for search accuracy | ✅ Yes |
| KeepTopLeft | Only top-left has value, rest empty | Preserve Excel structure | No |
| FlattenToString | Concatenate all non-empty values | Complex multi-cell headers | No |
| TreatAsHeader | Context-aware (auto-detect if header row) | Intelligent fallback | No |

**Complexity Levels**:
- **Simple**: Few horizontal merges (headers only) → Safe
- **Complex**: Vertical merges OR mixed patterns → Careful handling
- **Chaos**: >20% merged AND ≥5 ranges → Warning generated

---

## Boolean Parsing Patterns

### Supported (True)
```
"true", "yes", "y", "✓", "✔", "☑"
```

### Supported (False)
```
"false", "no", "n", "✗", "✘", "☐"
```

### Explicitly NOT Supported
```
"x" / "X"     - Removed 2025-11-28 (conflicts with text identifiers)
"1" / "0"     - Too ambiguous (numeric IDs, binary data)
```

**Rationale**: Pragmatic choice based on real-world data conflicts.

---

## Date System Handling

| System | Serial Start | Leap Year Bug | Auto-Detection |
|--------|--------------|---------------|----------------|
| 1900 | Jan 1, 1900 (serial=1) | Yes (Feb 29, 1900 exists as serial 60) | ✅ Default assumption |
| 1904 | Jan 1, 1904 (serial=0) | No | ✅ Detected from workbook metadata |

**Edge Case**: Files with both systems → First detected system wins (rare scenario).

---

## Known Edge Cases

### Accepted Limitations
1. **Multi-row headers**: Not auto-detected (future: manual UI control)
2. **Context-free normalization**: Cell normalized individually (no column context)
3. **Single-char ambiguity**: Always treated as text (even "Y", "N")
4. **Formula errors**: Always flagged as anomaly, never dominant type
5. **Mixed currencies**: Warning only, no automatic resolution

### Under Consideration
- **Gradual context expansion**: ±3 → ±6 → ±9 → ±12 (Phase 2 enhancement)
- **Tiebreaking priority**: Date > Currency > Number > Text when equal counts
- **LocalContext severity**: Anomaly supported locally → Info, contrasts locally → Error

See: `.personal/active/tech-debt/` for tracked enhancements

---

## Future Enhancements Roadmap

| Enhancement | Complexity | Impact | Planned For |
|-------------|-----------|--------|-------------|
| Multi-row header UI | Medium | High (reporting features) | v0.5 |
| Context-aware boolean parsing | Low | Low (edge case) | v1.0 or user request |
| Gradual window expansion | Low | Medium (better accuracy) | Post-MVP |
| Multi-currency auto-resolution | High | Medium (finance users) | v1.0+ |
| Custom type detectors (plugin) | High | High (extensibility) | v2.0 |

---

## References

- **Design**: `.personal/planning/foundation-layer-design.md`
- **API Design**: `.personal/active/foundation-layer-api-design.md`
- **Progress**: `.personal/planning/foundation-layer-tasks.md`
- **Tech Debt**: `.personal/active/tech-debt/` (individual issues)
- **ADR**: `.personal/reference/decisions/004-foundation-layer-first.md`

---

*Quick reference guide - Not exhaustive. See source code and tests for complete behavior.*
