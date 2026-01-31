# Web App - Data Cleanup Tool

**Status**: backlog
**Release**: v1.0 or shortly after (marketing launch)
**Priority**: should-have
**Depends on**: foundation-layer.md, export-results.md, desktop app complete

## Summary

Lightweight web application for quick data cleanup, serving as a funnel to the full desktop application.

**Timing**: Launch with or shortly after v1.0 — when the desktop app is feature-complete and ready to promote.

**Hosting**: Currently GitHub Pages (static HTML). Will need migration to proper hosting for Blazor WASM (or static site with WASM assets).

## User Stories

- As a casual user, I want to clean up a messy Excel file without installing anything
- As a potential customer, I want to try SheetAtlas features before downloading
- As a user, I want quick access to basic cleanup without the full app

## Requirements

### Functional
- [ ] Core Features (subset of desktop)
  - [ ] Upload single file
  - [ ] Detect and fix common issues:
    - [ ] Trailing whitespace
    - [ ] Inconsistent date formats
    - [ ] Mixed number formats
    - [ ] Empty rows/columns
  - [ ] Preview cleaned data
  - [ ] Download cleaned file

- [ ] Marketing Integration
  - [ ] Clear branding
  - [ ] "Want more? Download SheetAtlas" CTA
  - [ ] Feature comparison (web vs desktop)
  - [ ] Email capture (optional, for updates)

- [ ] Limitations (intentional)
  - [ ] Single file only
  - [ ] No comparison features
  - [ ] No template system
  - [ ] File size limit (e.g., 5MB)

### Non-Functional
- Performance: runs in browser, no server processing
- Privacy: file never leaves user's browser (WebAssembly)
- Simplicity: single-page, no account required

## Technical Notes

### Recommended: Blazor WebAssembly
- **Pro**: Reuse Foundation Layer code (normalization, readers, etc.)
- **Pro**: Same logic = same results as desktop
- **Pro**: No server needed — runs entirely in browser
- **Con**: Initial download size (~5-10MB for .NET runtime)
- **Con**: First load slower than pure JS

### Hosting Options
| Option | Pros | Cons |
|--------|------|------|
| GitHub Pages | Free, already using | May need tweaks for WASM routing |
| Netlify/Vercel | Free tier, easy deploy | Another service to manage |
| Azure Static Web Apps | .NET native, free tier | Microsoft ecosystem lock-in |
| Self-hosted | Full control | Cost, maintenance |

**Recommendation**: Start with GitHub Pages if possible (Blazor WASM can work as static files), migrate if needed.

### URL Structure
- `sheetatlas.github.io/cleanup` (GitHub Pages)
- or `cleanup.sheetatlas.com` (subdomain, requires DNS + hosting change)
- or `sheetatlas.com/cleanup` (if/when migrating to custom domain)

## Open Questions

- [x] ~~Blazor WASM or separate implementation?~~ → Blazor WASM (code reuse)
- [ ] GitHub Pages sufficient for WASM, or need migration?
- [ ] Custom domain (sheetatlas.com) — when to invest in this?
- [ ] Analytics: track conversions to desktop download?

## Acceptance Criteria

- [ ] Works in modern browsers
- [ ] File processing happens client-side
- [ ] Clear path to desktop download
- [ ] Fast and responsive UX
