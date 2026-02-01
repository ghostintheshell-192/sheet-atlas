# SheetAtlas - Documentation Structure

This directory contains all documentation for the SheetAtlas project, organized by purpose and audience.

## 📁 Directory Organization

### `website/`

**GitHub Pages website** - Public-facing project website

- **URL**: <https://ghostintheshell-192.github.io/sheet-atlas/>
- **Contents**: Landing page, downloads, features, screenshots
- **Deployment**: Automatic via GitHub Actions on push to `main` branch
- **Workflow**: `.github/workflows/deploy-pages.yml`

**Files:**

- `index.html` - Main landing page (generated from `index.html.template`)
- `index.html.template` - Template with version placeholders
- `styles/` - CSS stylesheets
- `images/` - Screenshots and visual assets
- `scripts/` - JavaScript for website interactivity

---

### `project/`

**Developer documentation** - Technical reference for contributors

- **Audience**: Developers, contributors, code reviewers
- **Contents**: Architecture specs, development guides, quick start
- **Entry point**: [project/README.md](project/README.md)

**Files:**

- `README.md` - Developer hub (setup, build, test)
- `ARCHITECTURE.md` - Architecture overview with Mermaid diagrams
- `technical-specs.md` - Performance, security, config specifications

---

## 🔄 Documentation Workflows

### Updating the Website

1. Edit template: `docs/website/index.html.template`
2. Commit and push to `main` branch
3. GitHub Actions regenerates `index.html` with current version
4. Website automatically deploys to GitHub Pages

**Template placeholders:**

- `${VERSION}` - Full version (e.g., `v0.3.3`)
- `${VERSION_NUMBER}` - Version number (e.g., `0.3.3`)
- `${RELEASE_DATE}` - Release date
- `${ALPHA_BANNER}` - Warning banner for pre-release versions

### Updating Developer Docs

1. Edit files in `docs/project/`
2. Commit and push normally
3. Documentation visible on GitHub repository

### Release Documentation Updates

The release pipeline (`.github/workflows/release.yml`) automatically:

- Generates `index.html` from template with current version
- Commits updated website to `main` branch
- Triggers GitHub Pages deployment

See [RELEASE_PROCESS.md](../docs/RELEASE_PROCESS.md) for complete release workflow.

---

## 📝 Documentation Standards

- **Language**: All documentation in English
- **Format**: Markdown for text, YAML for data
- **Updates**: Keep "Last Updated" dates current
- **Links**: Use relative paths for internal docs

---

## 🔗 Key Documentation Files

Located in repository root:

- **[README.md](../README.md)** - Main project overview and user guide
- **[CLAUDE.md](../CLAUDE.md)** - Development standards and conventions
- **[CHANGELOG.md](../CHANGELOG.md)** - Version history (auto-generated)
- **[RELEASE_PROCESS.md](RELEASE_PROCESS.md)** - Release workflow documentation

---

## 📊 Documentation Migration History

**October 2025 Reorganization:**

- Separated website (`docs/website/`) from developer docs
- Introduced template-based website generation
- Removed business strategy docs (moved to `.personal/`)
- Eliminated duplicate files (`overview.md`, `roadmap.md`)

**November 2025 Cleanup:**

- Consolidated overlapping README files
- Updated all references to reflect current structure
- Removed broken links to deleted files

---

*Last Updated: January 2026*
