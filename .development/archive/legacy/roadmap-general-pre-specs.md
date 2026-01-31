# ExcelViewer - Product Roadmap

## Release Strategy

### Version Numbering

- **Major releases**: New features, breaking changes (1.0, 2.0)
- **Minor releases**: New features, backward compatible (1.1, 1.2)
- **Patch releases**: Bug fixes, security updates (1.1.1, 1.1.2)

---

## Phase 1: Core Features

**Status**: 70% complete - UI infrastructure and core comparison features implemented
**Focus**: Complete essential functionality for Excel comparison and analysis, establish solid foundation for alpha release

### Sprint 1: Core Migration (Week 1)

- [x] Project analysis and backup
- [x] Avalonia UI project setup
- [x] Core library migration (100% reuse)
- [x] Basic file loading functionality
- [x] Simple comparison view

**Deliverables**: ✅ **COMPLETED**

- ✅ Working Avalonia application
- ✅ File loading and basic display
- ✅ Cross-platform compatibility

### Sprint 2: Essential Features (Week 2)

- [x] Enhanced comparison algorithms (RowComparison with structural analysis)
- [x] Search and filter functionality (TreeView with hierarchical results)
- [ ] Export capabilities (HTML, PDF)
- [x] Error handling and validation (structured logging, ExcelError domain model)
- [x] Professional UI/UX polish (modern themes, granular selection controls)

**Deliverables**: ✅ **MOSTLY COMPLETED**

- ✅ Complete comparison features with row-level analysis
- ❌ Export functionality (pending)
- ✅ Production-ready UI with theme system

**Additional Features Implemented**:

- ✅ Advanced tree-based search results with file/sheet grouping
- ✅ Granular selection management (per-search clear buttons)
- ✅ Structural warnings in row comparisons (missing headers, position mismatches)
- ✅ Complete theme system (light/dark) with proper resource management
- ✅ Clean Architecture with full dependency injection

### Sprint 2.5: Foundation Layer & Data Infrastructure (Pre-Alpha Hardening)

**Focus**: Build unified foundation for all future features - data normalization, column analysis, and template validation

**CRITICAL DECISION**: Implement foundation layer FIRST to avoid rework when adding template validation and column filtering. This unified infrastructure serves search accuracy, template validation, column filtering, and export features.

**Priority 1: Unified Data & Column Analysis Infrastructure** (1-2 weeks)

Build the Foundation Layer that serves ALL features:

- **Data Normalization**: Type-aware cleaning for all Excel data
- **Column Analysis**: Metadata extraction and type detection
- **Currency Detection**: Essential for financial comparisons
- **Merged Cell Handling**: Prevents data loss
- **File Health Analysis**: Automatic quality assessment

**📚 Full technical specification**: [foundation-layer-design.md](foundation-layer-design.md)

**Key Benefits**:

- Search accuracy +40% immediately
- No code duplication between features
- All features reuse same infrastructure

**Priority 2: File Format Support** (1 week)

- [x] **Legacy Excel Support (.xls)**
  - Research library options (NPOI, ExcelDataReader)
  - Implement separate reader for .xls format
  - Ensure feature parity with .xlsx support
  - Handle encoding issues and format quirks

- [x] **CSV Support**
  - Implement CSV parser with encoding detection
  - Handle delimiter variations (comma, semicolon, tab, pipe)
  - Support quoted fields and escape characters
  - Detect and use header row intelligently

**Priority 3: Robustness Testing** (1 week)

- [ ] **Real-World Data Testing**
  - Create test suite with real financial data (budgets, P&L statements, balance sheets)
  - Test with actual healthcare data (anonymized patient records, lab results)
  - Test supply chain data (inventory lists, SKU databases)
  - Validate search accuracy across different data types and formats
  - Test comparison with structurally different files

- [ ] **Code Quality Improvements**
  - Complete remaining refactoring tasks
  - Ensure consistent naming conventions and code style
  - Add comprehensive XML documentation for public APIs
  - Review and optimize performance-critical paths
  - Implement proper disposal patterns for large data sets

**Priority 4: Essential Export** (3-5 days)

- [ ] **Search Results Export**
  - Export search results to CSV (FileName, SheetName, CellAddress, Value, Context)
  - Simple, universal format for sharing and analysis
  - Enable "search → save → share with team" workflow

- [ ] **Deliverables**

- Robust data handling that works with messy real-world files
- Support for .xls and .csv formats
- Automatic health checks providing actionable feedback
- Verified stability with actual financial/operational data
- Clean, professional codebase ready for public scrutiny
- Basic export capability for search results

- [ ] **Success Criteria**:

- Search accuracy >95% across normalized data types
- Zero crashes with 100+ real-world test files
- Health analyzer correctly identifies all documented Excel pitfalls
- Code passes review by external developer perspective
- Performance <2 sec for 10MB files maintained

### Sprint 3: Polish & Alpha Release

**Focus**: Finalize core features, create professional first impression, release to public

- [ ] **Export Enhancements**
  - Export comparison results to Excel with conditional formatting (red=different, green=same)
  - Export comparison to CSV with structured diff format
  - Basic HTML export for sharing (styling, readable tables)

- [ ] **User Experience Polish**
  - Progress indicators for long-running operations
  - Improved error messages with actionable suggestions
  - Keyboard shortcuts for common operations (Ctrl+O, Ctrl+F, Ctrl+E for export)
  - Recent files list with quick access

- [ ] **Documentation**
  - User guide with screenshots for common workflows
  - GIF demos for GitHub README (30-second workflows)
  - FAQ addressing common use cases
  - Troubleshooting guide for known issues

- [ ] **Distribution Preparation**
  - GitHub Actions for automated builds (Windows/Linux/macOS)
  - Self-contained installers for each platform
  - Portable executables (no installation required)
  - Clear installation instructions

- [ ] **Alpha Release (v0.1.0-alpha)**
  - GitHub Release with binary downloads
  - Community announcement (r/excel, DEV.to, HackerNews)
  - Alpha tester recruitment (target: 50 users)
  - Feedback collection mechanism (GitHub Issues, form)

**Deliverables**:

- Public alpha release ready for testing
- Professional presentation (demos, docs, clean repo)
- User acquisition channels activated
- Feedback loop established

**Success Metrics**:

- 50-100 alpha downloads in first week
- 10-20 engaged testers providing feedback
- 5+ GitHub stars (social proof)
- Zero critical bugs reported
- Positive feedback on core value proposition

### Sprint 4: Template Validation & Beta Preparation

**Focus**: Leverage foundation layer to build template validation system, then add column filtering UI

**REVISED ORDER**: Template validation BEFORE column filtering UI, because template system provides validation rules that filtering can display.

- [ ] **Template Validation System** (1 week - Community Priority #1, Built on Foundation)
  - Implement `ITemplateValidationService` and `ITemplateStorageService`
  - Create `ExcelTemplate` entity with expected columns and validation rules
  - Create `ExpectedColumn` with Name, Position, DataType, Required flag
  - Create `ValidationRule` types: NotEmpty, DateFormat, NumericRange, RegexPattern
  - Implement `CreateTemplateFromFileAsync()` - REUSES `IColumnAnalysisService` from Sprint 2.5
  - Implement `ValidateAsync()` - applies rules and generates `ValidationReport`
  - UI workflow: "Save as Template" → customize rules → "Validate Against Template"
  - Store templates as JSON files in user's documents folder
  - **Value Proposition**: "Ensure vendor/department files match your format BEFORE processing"
  - **NOTE**: This reuses ALL column analysis infrastructure from Sprint 2.5 - no duplication

- [ ] **Column Filtering UI** (3-5 days - Simplified, NO Phase 3 linking)
  - Implement `ColumnFilterViewModel` in SearchView sidebar (250px with GridSplitter)
  - Display TreeView with File > Sheet > Column hierarchy using `FileSchema` from foundation
  - Implement checkbox selection (three-state for parent nodes)
  - Add quick actions: Select All, Clear, Invert Selection
  - Integrate with `ISearchExportService` to filter exported columns
  - Show validation warnings from template (if template is active)
  - **DEFERRED**: Column linking/visual feedback system (Phase 3 of original plan) - not needed yet
  - **DEFERRED**: Keyboard shortcuts (Phase 4 of original plan) - add after user feedback

- [ ] **Comparison Export with Highlighting** (3-5 days)
  - Implement `IComparisonExportService.ExportToExcelAsync()`
  - Export `RowComparison` results to Excel with conditional formatting
  - Red background for different values, green for matched, yellow for missing columns
  - Support column filtering (only export selected columns if filter active)
  - Include comparison summary sheet with statistics
  - **Value Proposition**: "Get comparison report you can email to stakeholders"

- [ ] **Smart Column Mapping** (1 week - Community Priority #2, if time permits)
  - Intelligent column alignment for comparison across files
  - Fuzzy matching for similar header names ("Employee ID" → "EmpID" → "ID")
  - Position-based suggestions (column 3 in File A → likely column 5 in File B)
  - Type-based matching (both numeric → likely same semantic meaning)
  - Manual override with drag-drop UI (similar to SQL join builder)
  - Save mapping as reusable template
  - **Value Proposition**: "Compare files even when column order and names differ"
  - **NOTE**: Defer if alpha feedback doesn't prioritize this

- [ ] **Advanced Search Features** (Based on Feedback - Low Priority)
  - Filter by data type (dates, numbers, text)
  - Search in specific columns only
  - Exclude certain sheets/files from search
  - Search history with saved queries
  - Bulk operations on search results
  - **NOTE**: Implement only if users explicitly request during alpha

- [ ] **Performance Optimization**
  - Streaming for files >50MB
  - Lazy loading of sheet data
  - Background processing with proper cancellation
  - Memory usage optimization for large datasets

- [ ] **Settings & Preferences**
  - Persistent user preferences
  - Default search options
  - UI customization (font size, theme preference)
  - File associations (optional)

**Deliverables**:

- Feature-complete beta version (v0.5.0-beta)
- Community-validated functionality
- Enhanced user workflows
- Performance optimization for production use

**Success Metrics**:

- 200-500 beta users
- Template validation used by >30% of users
- Smart column mapping solves real reported problems
- Feature requests drive 80% of development
- Retention >50% (users return after first use)

---

## Phase 2: Professional Features (Post v1.0)

**Focus**: Advanced functionality for power users and professional workflows
**Timeline**: After stable 1.0 release with >1,000 users

### Version 1.1: Advanced Comparison & Automation

- **Enhanced Comparison Algorithms**
  - Fuzzy matching for similar data (typos, variations)
  - Structural comparison beyond cell-by-cell
  - Statistical analysis integration (variance, trends)
  - Intelligent diff highlighting (semantic, not just textual)

- **Batch Processing**
  - Multiple file comparison workflows
  - Automated comparison of folder contents
  - Scheduled comparison tasks
  - Command-line interface for scripting

- **Professional Export**
  - Custom report templates with branding
  - PDF export with proper formatting
  - Excel export with advanced conditional formatting
  - JSON export for programmatic consumption
  - Detailed comparison statistics and summaries

- **Database Export** (If Requested by Users)
  - Export to JSON format (ready for MongoDB/PostgreSQL)
  - Generate SQL INSERT statements with inferred types
  - Respect template schema if available
  - Connection to actual databases (optional, advanced)
  - **Note**: Build only if 10%+ users request this feature

### Version 1.2: Collaboration Features

- **File Versioning Support**
  - Git integration for tracking changes
  - Version history visualization
  - Diff between versions
  - Merge conflict resolution UI

- **Annotation System**
  - Comments on specific differences
  - Approval/rejection workflows
  - Review status tracking
  - Team collaboration features

- **Workspace Management**
  - Save comparison sessions
  - Organize related files in projects
  - Quick access to frequent comparisons

### Version 1.3: API & Automation

- **REST API** (If Enterprise Demand Exists)
  - Programmatic file comparison
  - Integration with existing tools
  - Webhook notifications for automated workflows

- **Scripting Support**
  - PowerShell modules
  - Python integration
  - Custom automation scripts
  - Plugin system for extensions

---

## Phase 3: Enterprise Features (18+ months)

**Focus**: Enterprise-grade security, compliance, and collaboration features
**Timeline**: Only if demand validated and funding secured

### Version 2.0: Enterprise Platform

- **Multi-user Capabilities**
  - Shared comparison libraries
  - User role management
  - Team collaboration features
  - Concurrent access control

- **Compliance & Audit**
  - Detailed audit trails
  - Compliance reporting (SOX, GDPR, HIPAA)
  - Digital signatures
  - Regulatory templates

- **Advanced Security**
  - SSO integration (SAML, OAuth)
  - Encryption at rest
  - Access control policies
  - Data loss prevention

### Version 2.1: Scalability

- **Performance Optimization**
  - Streaming for very large files (>100MB)
  - Parallel processing across cores
  - Advanced memory optimization
  - Distributed processing (future)

- **Cloud Integration** (Optional)
  - Secure cloud storage connectors (Azure, AWS, GCP)
  - Hybrid deployment options
  - Enterprise cloud compliance
  - **Note**: Only if data sovereignty concerns addressed

### Version 2.2: Industry Specialization

- **Financial Services Package**
  - Regulatory reporting templates (Basel, IFRS)
  - Risk analysis tools
  - Compliance dashboards
  - Audit-ready outputs

- **Healthcare Package**
  - HIPAA compliance features
  - Medical data handling
  - PHI anonymization tools
  - Clinical trial data comparison

---

## Phase 4: Platform Expansion (Future)

**Focus**: Extended platform support and advanced integrations
**Timeline**: 24+ months, contingent on success

### Platform Expansion

- **Web Companion App**
  - Light comparison for non-sensitive data
  - Team dashboard and reporting
  - Remote access to desktop features
  - Read-only result viewing

- **Mobile Companion**
  - View comparison results on mobile
  - Approve/reject workflows
  - Notification management
  - Quick result access

### Market Verticals

- **Government Sector**
  - FedRAMP compliance
  - Security clearance requirements
  - Government procurement processes

- **Legal Industry**
  - Document review workflows
  - Legal hold compliance
  - E-discovery integration

### Technology Evolution

- **AI-Powered Features** (If Viable)
  - Intelligent anomaly detection
  - Pattern recognition across files
  - Predictive data quality issues
  - Smart suggestions for mappings

---

## Go-to-Market Strategy

### Alpha Phase (Month 1-2)

**Goal**: 50-100 engaged testers, validate core value proposition

**Tactics**:

- GitHub Release with clear "Alpha" labeling
- Organic community posts (r/excel, r/opensource, DEV.to)
- Direct outreach to finance/ops professionals
- Feedback collection via GitHub Issues
- Weekly iteration based on bug reports

**Success Metrics**:

- 50+ downloads
- 10+ meaningful feedback items
- 5+ GitHub stars
- Zero critical crashes reported
- Positive sentiment on core features

### Beta Phase (Month 3-4)

**Goal**: 200-500 users, feature validation, testimonials

**Tactics**:

- Feature-complete beta release
- Content marketing (blog posts, use case demos)
- User testimonials and case studies
- SEO optimization (keywords: excel comparison, file validation)
- Community building (GitHub Discussions, user group)

**Success Metrics**:

- 200+ users
- 50+ GitHub stars
- 3+ written testimonials
- Template validation adopted by 30%+ users
- Organic growth from referrals

### v1.0 Launch (Month 5-6)

**Goal**: 1,000 users, establish freemium model

**Tactics**:

- Major announcement across channels
- Freemium model: core features free, convenience features paid
- Pro tier: $49-79 one-time purchase (branded export, priority support, templates)
- Product Hunt launch
- Press outreach (niche publications)

**Success Metrics**:

- 1,000+ free users
- 50+ paying customers ($2,500-4,000 revenue)
- 100+ GitHub stars
- Community contributions (PRs, translations)
- Sustainable project momentum

### Commercial Growth (Month 7-18)

**Goal**: 5,000 users, $30-50K revenue, sustainable income

**Tactics**:

- Content marketing (1 post/week)
- SEO optimization (rank for key terms)
- Partnership with accounting software
- Team licenses ($199/year for 5 users)
- Enterprise pilot programs

**Success Metrics**:

- 5,000+ users
- 300-500 paying customers
- $30-50K annual revenue
- Professional network effects
- Full-time viable (if desired)

---

## Feature Prioritization Matrix

### High Impact, Low Effort (Quick Wins) - Do First

1. ✅ Export search results to CSV (3-5 days)
2. Data normalization layer (1 week)
3. File health analyzer (1 week)
4. .xls and .csv support (1 week)
5. Keyboard shortcuts (2-3 days)
6. Recent files list (2 days)

### High Impact, High Effort (Major Features) - Do After Alpha

1. Template validation system (1-2 weeks)
2. Smart column mapping (1-2 weeks)
3. Export comparison to Excel with formatting (3-5 days)
4. Advanced comparison algorithms (2 weeks)
5. Batch processing capabilities (1-2 weeks)

### Medium Impact, Low Effort (Nice to Have) - Community Driven

1. Additional file format support (ODS, Numbers)
2. Localization to other languages
3. Tutorial system and guided tours
4. Advanced search filters

### Low Impact, High Effort (Avoid Unless Requested)

1. Database export (build only if 10%+ users request)
2. API development (enterprise demand required)
3. Cloud integration (data sovereignty concerns)
4. Real-time collaboration (complex, niche use case)
5. Custom scripting language (overkill for target users)

---

## Success Metrics

### Technical Targets

- **Performance**: <2 sec load times for 10MB files, <10 sec for 100MB
- **Quality**: <0.1% crash rate, 90%+ test coverage for core
- **Compatibility**: 100% feature parity across Windows/Linux/macOS
- **Scalability**: Support for 100MB+ files efficiently
- **Search Accuracy**: >95% correct results with normalized data

### User Experience Goals

- Intuitive interface requiring <5 min to master basic workflow
- High user satisfaction (>4.5/5 rating if surveyed)
- Comprehensive documentation addressing common questions
- Active community feedback driving 80% of feature priorities
- Testimonials validating specific use cases

### Business Metrics (If Monetization Pursued)

- **Year 1**: 1,000 users, 50 paying ($2,500 revenue)
- **Year 2**: 5,000 users, 400 paying ($32,000 revenue)
- **Year 3**: 15,000 users, 1,500 paying ($150,000 revenue)
- **Conversion Rate**: 5-10% free to paid
- **Retention**: >80% annual retention for paid users

---

## Risk Mitigation

### Technical Risks

- **Avalonia maturity**: Extensive cross-platform testing, contribute fixes upstream if needed
- **Performance issues**: Profiling and optimization cycles, streaming for large files
- **Cross-platform bugs**: Automated CI/CD testing on all platforms, community testing
- **File format edge cases**: Comprehensive test suite with real-world files, graceful degradation

### Market Risks

- **Low adoption**: Focus on specific pain points (comparison, validation), organic community growth
- **Feature creep**: Strict prioritization based on user feedback, resist building unused features
- **Competition**: Differentiate on security-first, cross-platform, specific workflows (comparison/validation)
- **Monetization challenges**: Freemium model, optional paid features, one-time purchase reduces friction

### Execution Risks

- **Solo developer burnout**: Pace sustainably, don't commit to timelines, build in public for motivation
- **Code quality debt**: Maintain high standards, refactor before alpha, comprehensive testing
- **Community management**: Clear communication, responsive to feedback, transparent roadmap
- **Scope management**: MVP approach, ship early, iterate based on real usage

---

## Development Philosophy

### Core Principles

1. **Ship Early, Iterate Fast**: Alpha release with core features, learn from users
2. **Quality Over Speed**: Robust foundation before marketing push
3. **Community-Driven**: 80% of post-alpha features from user feedback
4. **Security-First**: No cloud, no telemetry, no data leaving user's machine
5. **Open Core**: MIT license, optional paid convenience features

### Decision Framework

**For Every Feature, Ask**:

1. Does this solve a validated user problem? (Not speculative)
2. Can we build it in <2 weeks? (If not, break down or defer)
3. Does it align with security-first positioning? (No cloud, no tracking)
4. Will users pay for it, or is it table-stakes? (Informs pricing)
5. Can we maintain it long-term? (Code quality, testing)

---

## C++ Core Migration Strategy

**CRITICAL DECISION**: Migrate to C++ ONLY after feature-complete C# implementation and validated performance bottlenecks.

### When NOT to Migrate

❌ **Do NOT migrate if**:

- Performance meets user expectations (<2sec for 10MB, <10sec for 100MB files)
- Bottleneck is I/O disk, not CPU (profiling shows >70% time in file reading)
- Feature velocity is more important than raw speed (alpha/beta phase)
- Team size is small (1-2 developers without C++ production experience)
- <20% of users complain about performance

### When to Migrate

✅ **Migrate ONLY if**:
>
- >20% users document performance complaints (GitHub issues, support tickets)
- Profiling identifies CPU bottleneck (70%+ time in comparison/normalization algorithms)
- Target users process >100MB files regularly (not edge cases)
- File size is competitive differentiator vs competitors
- Team has C++ expertise for long-term maintenance

### Migration Timeline (IF migration is validated)

**Prerequisites** (before starting migration):

1. Feature freeze on C# Core (API stabilized, no new features)
2. Comprehensive test suite (>80% coverage) that can validate C++ port
3. Performance profiling data identifying exact bottlenecks
4. Cross-platform build infrastructure ready (CMake, vcpkg)

**Phase 1: Infrastructure Setup** (2 weeks)

- Setup CMake build system with cross-platform support (Windows/Linux/macOS)
- Configure vcpkg for C++ dependencies (libxlsxwriter or Apache POI C++)
- Setup C#/C++ interop layer (P/Invoke wrapper in `SheetAtlas.Core.Interop`)
- Configure memory debugging tools (Valgrind, AddressSanitizer)
- Create CI/CD pipeline for C++ builds

**Phase 2: Core Migration** (4-6 weeks)

- Week 1-2: Excel file reader (OpenXML parser in C++)
- Week 3: Data normalization service
- Week 4: Column analysis service
- Week 5: Template validation logic
- Week 6: Comparison algorithms

**Phase 3: Testing & Optimization** (2-3 weeks)

- Memory leak detection and fixes
- Performance benchmarking vs C# baseline
- Cross-platform compatibility testing
- Edge case validation with real user files

**Total Estimated Time**: 8-11 weeks full-time development

### Architecture After Migration

```
SheetAtlas.UI.Avalonia (C# - no changes)
        ↓
SheetAtlas.Core.Interop (C# - P/Invoke wrapper, thin layer)
        ↓
SheetAtlas.Core.Native (C++ DLL)
        ├── ExcelReader (libxlsxwriter)
        ├── DataNormalizer
        ├── ColumnAnalyzer
        ├── TemplateValidator
        └── ComparisonEngine
```

### Risk Mitigation

**Technical Risks**:

- Memory management bugs (use RAII, smart pointers, AddressSanitizer)
- Cross-platform compilation issues (test on all platforms early)
- Performance regression (benchmark continuously against C# baseline)

**Business Risks**:

- 8-11 weeks investment with uncertain ROI
- Feature development stalls during migration
- Increased maintenance complexity long-term

### Decision Point

**After alpha/beta release (Sprint 3-4)**:

1. Collect performance metrics from real users
2. Profile Core layer with actual user files
3. Calculate ROI: Will 2-3x speed improvement retain/convert users worth 11 weeks investment?
4. DECIDE: Migrate (if clear ROI) or KEEP C# (if performance adequate)

**Recommendation**: Default to KEEP C# unless performance is validated constraint on growth.

---

*Last updated: November 2025*
*Version: 1.2 - Revised with foundation-first approach and C++ migration strategy*
*Next review: After alpha feedback (December 2025)*
