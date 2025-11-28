# SheetAtlas - Release Process

This document describes the release process for SheetAtlas, including how to create releases and how the automated pipeline works.

## Quick Release Guide

### Automated Steps

1. **Create and push signed tag** (from `develop` branch):
   ```bash
   git checkout develop
   git tag -s v0.3.3 -m "Release v0.3.3 - Description"
   git push origin v0.3.3
   ```

2. **Pipeline runs automatically** (~5 minutes):
   - Builds for Windows, Linux, macOS
   - Creates GitHub release with artifacts
   - Updates website files on `main` branch
   - **⚠️ Does NOT deploy website** (GitHub limitation)

3. **Deploy website manually** (required after each release):
   ```bash
   gh workflow run deploy-pages.yml
   ```
   Or via GitHub Actions UI → Deploy GitHub Pages → Run workflow

4. **Merge to main**:
   ```bash
   git checkout main
   git pull origin main  # Get website updates from pipeline
   git merge develop
   git push origin main
   ```

### What's Automatic vs Manual

| Step | Type | Command/Action |
|------|------|----------------|
| Tag creation | Manual | `git tag -s v0.3.3` + push |
| Build artifacts | ✅ Automatic | Triggered by tag push |
| GitHub release | ✅ Automatic | Created with artifacts |
| Update website files | ✅ Automatic | Committed to `main` |
| Deploy website | ⚠️ **Manual** | `gh workflow run deploy-pages.yml` |
| Merge develop→main | Manual | After release verified |

**Why manual website deploy?** GitHub Actions workflows triggered by `GITHUB_TOKEN` (like our release pipeline) don't trigger other workflows automatically (prevents infinite loops). The `deploy-pages.yml` must be triggered manually.

## Versioning Strategy

We follow **Semantic Versioning (SemVer)**:

- **v0.x.x** → Pre-release / Alpha (development phase)
- **v1.x.x+** → Stable / Production-ready

Format: `vMAJOR.MINOR.PATCH`
- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes

Examples:
- `v0.2.0` → Second alpha release
- `v0.2.1` → Alpha bugfix release
- `v1.0.0` → First stable release
- `v1.1.0` → Stable release with new features

### Prerelease Detection

The release pipeline **automatically detects** if a version is a prerelease:

- Tags starting with `v0.` → Marked as **prerelease** on GitHub
- Tags starting with `v1.` or higher → Marked as **stable release**

This is handled automatically in the workflow, no manual configuration needed.

## Release Pipeline Architecture

The release process is fully automated via GitHub Actions workflow: `.github/workflows/release.yml`

### Pipeline Structure

```
┌─────────────────────────────────────────┐
│         Tag Push (v0.3.0)              │
└─────────────┬───────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────┐
│    Release Pipeline Triggered           │
└─────────────┬───────────────────────────┘
              │
      ┌───────┴────────┬──────────────┐
      ▼                ▼              ▼
┌──────────┐   ┌──────────┐   ┌──────────┐
│  Build   │   │  Build   │   │  Build   │
│ Windows  │   │  Linux   │   │  macOS   │
│ (2-3min) │   │ (1-2min) │   │ (1-2min) │
└────┬─────┘   └────┬─────┘   └────┬─────┘
     │              │              │
     └──────┬───────┴──────┬───────┘
            ▼              ▼
      ┌──────────┐   ┌──────────────┐
      │ Release  │   │   Update     │
      │ Creation │──▶│   Website    │
      │  (~10s)  │   │   (~20s)     │
      └──────────┘   └──────┬───────┘
                            ▼
                     ┌──────────────┐
                     │Deploy Pages  │
                     │(automatic)   │
                     └──────────────┘
```

### Pipeline Jobs

#### 1. Build Jobs (Parallel)

Three build jobs run in parallel to maximize speed:

**Build Windows** (`build-windows`):
- Builds .NET application for `win-x64`
- Creates installer with Inno Setup
- Produces: `SheetAtlas-Setup-win-x64.exe`
- Duration: ~2-3 minutes

**Build Linux** (`build-linux`):
- Builds .NET application for `linux-x64`
- Creates tarball archive
- Creates Debian package (.deb) with proper structure
- Produces:
  - `SheetAtlas-linux-x64.tar.gz`
  - `SheetAtlas-linux-x64.deb` (NEW in v0.3.0+)
- Duration: ~1-2 minutes

**Build macOS** (`build-macos`):
- Builds .NET application for `osx-x64`
- Creates tarball archive
- Produces: `SheetAtlas-macos-x64.tar.gz`
- Duration: ~1-2 minutes
- Note: Unsigned (requires Apple Developer cert for .dmg)

#### 2. Release Job (`release`)

Runs after all builds complete:
- Downloads all artifacts from build jobs
- Detects if version is prerelease (v0.x check)
- Creates GitHub release with:
  - All platform artifacts
  - CHANGELOG.md as release body
  - Correct prerelease flag
  - Auto-generated release notes
- Duration: ~10 seconds

#### 3. Website Update Job (`update-website`)

Runs after release is created:
- Checks out `main` branch
- Generates `index.html` from `index.html.template` using `envsubst`
- Replaces placeholders:
  - `${VERSION}` → e.g., `v0.3.0`
  - `${VERSION_NUMBER}` → e.g., `0.3.0`
  - `${RELEASE_DATE}` → Current date
  - `${ALPHA_BANNER}` → Warning banner for v0.x versions
  - `${RELEASE_STATUS_TEXT}` → Status message
- Commits updated `index.html` to `main` branch
- Triggers `deploy-pages.yml` automatically
- Duration: ~20 seconds

#### 4. Deploy Pages (Automatic)

The existing `deploy-pages.yml` workflow automatically triggers when `docs/website/` changes on `main`:
- Deploys updated website to GitHub Pages
- Duration: ~30-60 seconds
- **No manual intervention required**

## Artifact Naming Convention

All release artifacts use **version-agnostic naming** for compatibility with GitHub's `/releases/latest/download/` URLs:

```
SheetAtlas-Setup-win-x64.exe           # Windows installer
SheetAtlas-linux-x64.tar.gz            # Linux tarball
SheetAtlas-linux-x64.deb               # Linux Debian package
SheetAtlas-macos-x64.tar.gz            # macOS tarball
```

This allows users to always download the latest version using static URLs:
```
https://github.com/ghostintheshell-192/sheet-atlas/releases/latest/download/SheetAtlas-Setup-win-x64.exe
```

**Why not include version in filename?**
- `/latest/` URLs work automatically
- No website updates needed per release
- Standard used by VS Code, GitHub CLI, Docker Desktop

## Website Template System

The website is automatically updated on each release using a template-based system.

### How It Works

1. **Template File**: `docs/website/index.html.template`
   - Contains placeholders like `${VERSION}`, `${VERSION_NUMBER}`
   - Committed to repository, maintained like code

2. **Generation**: Uses `envsubst` (built-in Linux tool)
   - Fast, simple, no dependencies
   - Replaces all `${VAR}` placeholders with environment variables

3. **Conditional Content**: Alpha banner for v0.x versions
   - Automatically shows/hides warning banner
   - Based on version number detection

4. **Auto-Commit**: Generated `index.html` committed to `main`
   - Triggers GitHub Pages deployment
   - Full audit trail of website changes

### Template Placeholders

| Placeholder | Example Value | Description |
|-------------|---------------|-------------|
| `${VERSION}` | `v0.3.0` | Full version with 'v' prefix |
| `${VERSION_NUMBER}` | `0.3.0` | Version without 'v' prefix |
| `${RELEASE_DATE}` | `2025-10-14` | ISO format date |
| `${ALPHA_BANNER}` | `<div>⚠️ Alpha...</div>` | HTML banner (empty for v1.x+) |
| `${RELEASE_STATUS_TEXT}` | `Alpha release - ...` | Status message |

### Manual Template Testing

Test template generation locally:

```bash
cd /data/repos/sheet-atlas

# Set variables
export VERSION=v0.3.0
export VERSION_NUMBER=0.3.0
export RELEASE_DATE=$(date +%Y-%m-%d)
export ALPHA_BANNER='<div>⚠️ Alpha Software</div>'
export RELEASE_STATUS_TEXT="Alpha release - Testing phase"

# Generate
envsubst < docs/website/index.html.template > /tmp/test-index.html

# View result
firefox /tmp/test-index.html  # or your browser
```

## Manual Release Process

If you need to create a release manually (troubleshooting, special cases):

### 1. Create Tag Locally

```bash
git checkout develop
git pull origin develop

# Tag the commit
git tag v0.3.0 -m "Release v0.3.0 - Feature description"

# Push tag (triggers pipeline)
git push origin v0.3.0
```

### 2. Trigger Workflow Manually

Via GitHub Actions UI:
1. Go to **Actions** → **Release Pipeline**
2. Click **Run workflow**
3. Enter tag name: `v0.3.0`
4. Click **Run workflow**

### 3. Monitor Progress

```bash
# Via GitHub CLI
gh run list --workflow=release.yml
gh run watch <run-id>

# Or via GitHub web UI
# https://github.com/ghostintheshell-192/sheet-atlas/actions
```

## Troubleshooting

### Build Failures

**Windows build fails**:
- Check Inno Setup installation in workflow
- Verify `build/installer/SheetAtlas-Installer.iss` exists
- Check `.csproj` configuration for Windows

**Linux .deb creation fails**:
- Check Debian package structure in workflow
- Verify `dpkg-deb` command syntax
- Check file permissions in package

**macOS build fails**:
- Check Xcode/SDK compatibility
- Verify .NET 8 supports `osx-x64` runtime

### Release Creation Failures

**Artifacts not found**:
- Check all build jobs completed successfully
- Verify artifact upload/download steps in workflow
- Check artifact names match in all jobs

**Prerelease flag incorrect**:
- Check version tag format (`v0.3.0` not `0.3.0`)
- Verify prerelease detection logic in workflow
- SemVer: v0.x = prerelease, v1.x+ = stable

### Website Update Failures

**Template not found**:
- Ensure `index.html.template` exists on `main` branch
- Check workflow checks out `main` correctly
- Verify file path: `docs/website/index.html.template`

**envsubst fails**:
- Check all required variables are exported
- Verify no typos in placeholder names
- Test template generation locally first

**Commit fails**:
- Check Git configuration in workflow
- Verify `GITHUB_TOKEN` has write permissions
- Check branch protection rules on `main`

**GitHub Pages not updating**:
- Verify `deploy-pages.yml` workflow triggered
- Check file is in `docs/website/` path
- Check GitHub Pages settings (source: main branch, /docs)

### Common Issues

**Race conditions (multiple workflows running)**:
- Old duplicate workflows were deleted in v0.3.0
- Only `release.yml` should trigger on tags now
- If issue persists, check for other workflows with `on: push: tags:`

**Version mismatch**:
- Ensure tag format is exactly `vX.Y.Z` (lowercase v, no spaces)
- Update version in `*.csproj` files before tagging
- Keep CHANGELOG.md updated

## Release Checklist

Before creating a release:

- [ ] All tests passing (`dotnet test`)
- [ ] CHANGELOG.md updated with new version section
- [ ] Version number updated in `.csproj` files
- [ ] Breaking changes documented (if MAJOR version bump)
- [ ] Documentation updated for new features
- [ ] Local build tested (`dotnet build --configuration Release`)

Creating release:

- [ ] Create and push tag (or use `release-changelog.yml`)
- [ ] Monitor workflow progress
- [ ] Verify all builds successful
- [ ] Check GitHub release created correctly
- [ ] Verify website updated (https://ghostintheshell-192.github.io/sheet-atlas/)
- [ ] Test download links work
- [ ] Announce release (GitHub Discussions, social media, etc.)

## Future Enhancements (Phase 3)

Planned improvements for future releases:

### Packaging Enhancements
- **Windows**: Code signing (.exe), winget package, portable .zip
- **Linux**: RPM package, Flatpak/Snap, AppImage
- **macOS**: Notarized .dmg installer, Homebrew cask

### Workflow Improvements
- Integrate `release-changelog.yml` into main pipeline
- Single command: version bump → changelog → tag → build → release → website
- Automated testing before release
- Release notes from conventional commits

### Distribution
- Automatic update mechanism in application
- Package manager submissions (winget, Homebrew, apt/yum repos)
- Digital signatures and checksums for all artifacts

## References

- **Workflow File**: `.github/workflows/release.yml`
- **Template File**: `docs/website/index.html.template`
- **Design Document**: `.personal/work-in-progress/workflow-redesign-2025-10-14.md`
- **Semantic Versioning**: https://semver.org/
- **GitHub Actions**: https://docs.github.com/en/actions

---

**Last Updated**: 2025-10-14
**Version**: 1.0
**Maintained by**: SheetAtlas Team
