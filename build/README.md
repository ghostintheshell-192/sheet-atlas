# SheetAtlas - Build & Distribution Guide

Complete guide for building SheetAtlas installers and distributable packages for Windows, Linux, and macOS.

---

## 📋 Table of Contents

- [Quick Start](#quick-start)
- [Windows Installer](#windows-installer)
- [Linux Package](#linux-package)
- [macOS Package](#macos-package)
- [GitHub Actions CI/CD](#github-actions-cicd)
- [Code Signing](#code-signing)
- [Troubleshooting](#troubleshooting)

---

## 🚀 Quick Start

### Prerequisites

- **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Git** - For cloning and version control

### Platform-Specific Tools

| Platform | Required Tools |
|----------|---------------|
| **Windows** | [Inno Setup 6](https://jrsoftware.org/isdl.php), [Windows SDK](https://developer.microsoft.com/windows/downloads/windows-sdk/) (for signtool) |
| **Linux** | `tar`, `gzip` (usually pre-installed) |
| **macOS** | Xcode Command Line Tools |

---

## 🪟 Windows Installer

### Manual Build

1. **Open PowerShell** in the project root

2. **Run the build script:**
   ```powershell
   .\build\build-windows-installer.ps1
   ```

3. **Options:**
   ```powershell
   # Build with specific configuration
   .\build\build-windows-installer.ps1 -Configuration Release

   # Skip code signing
   .\build\build-windows-installer.ps1 -SkipSign

   # Use existing build (skip dotnet publish)
   .\build\build-windows-installer.ps1 -SkipBuild
   ```

4. **Output location:**
   ```
   build/output/SheetAtlas-Setup-1.1.0-win-x64.exe
   ```

### What Gets Installed

```
C:\Program Files\SheetAtlas\
├── SheetAtlas.UI.Avalonia.exe    # Main executable
├── app.ico                        # Application icon
├── *.dll                          # .NET runtime & dependencies
├── LICENSE                        # MIT License
└── README.md                      # Documentation
```

### Start Menu Shortcuts

- **SheetAtlas** - Launches the application
- **Uninstall SheetAtlas** - Runs the uninstaller

### Desktop Shortcut

Optional during installation (user choice).

---

## 🐧 Linux Package

### Build Tarball

```bash
# Publish self-contained build
dotnet publish src/SheetAtlas.UI.Avalonia/SheetAtlas.UI.Avalonia.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output build/publish/linux-x64 \
  /p:PublishTrimmed=true

# Create tarball
cd build/publish
tar -czf SheetAtlas-linux-x64.tar.gz linux-x64/
```

### Installation

```bash
# Extract
tar -xzf SheetAtlas-linux-x64.tar.gz

# Move to installation directory
sudo mv linux-x64 /opt/SheetAtlas

# Create symlink (optional)
sudo ln -s /opt/SheetAtlas/SheetAtlas.UI.Avalonia /usr/local/bin/sheetatlas

# Run application
sheetatlas
# or
/opt/SheetAtlas/SheetAtlas.UI.Avalonia
```

### System Requirements

- **Ubuntu 20.04+**, **Debian 11+**, **Fedora 36+**, or compatible
- **X11 libraries**: `libx11-dev`, `libice-dev`, `libsm-dev`, `libfontconfig1-dev`

```bash
# Install dependencies (Ubuntu/Debian)
sudo apt install libx11-6 libice6 libsm6 libfontconfig1
```

---

## 🍎 macOS Package

### Build DMG Installer

**Automated Build (Recommended):**

```bash
# Run the build script
./build/build-macos-installer.sh 0.3.0
```

This creates:
- **SheetAtlas.app** - Complete .app bundle
- **SheetAtlas-macos-x64.dmg** - DMG installer with drag-and-drop

**Manual Build:**

```bash
# 1. Publish for macOS
dotnet publish src/SheetAtlas.UI.Avalonia/SheetAtlas.UI.Avalonia.csproj \
  --configuration Release \
  --runtime osx-x64 \
  --self-contained true \
  --output build/publish/osx-x64 \
  /p:PublishTrimmed=true

# 2. Run the installer script
chmod +x build/build-macos-installer.sh
./build/build-macos-installer.sh 0.3.0
```

### Installation

**From DMG (Recommended):**
1. Open `SheetAtlas-macos-x64.dmg`
2. Drag `SheetAtlas.app` to the `Applications` folder
3. Launch from Launchpad or Applications folder
4. On first launch: Right-click → Open (to bypass Gatekeeper for unsigned apps)

**From Tarball (Legacy):**
```bash
# Extract
tar -xzf SheetAtlas-macos-x64.tar.gz

# Move to Applications
sudo mv osx-x64 /Applications/SheetAtlas

# Run application
/Applications/SheetAtlas/SheetAtlas.UI.Avalonia
```

### System Requirements

- **macOS 10.15 Catalina** or later
- **Intel (x64) or Apple Silicon (M1/M2/M3)** - Universal build coming soon

---

## 🤖 GitHub Actions CI/CD

### Automated Builds

SheetAtlas uses GitHub Actions to automatically build installers and packages for all platforms.

**Workflow File:** `.github/workflows/build-installer.yml`

### Triggers

| Trigger | Description |
|---------|-------------|
| **Push to `main`** | Creates release artifacts |
| **Push to `develop`** | Creates development builds |
| **Tags (`v*`)** | Creates GitHub releases |
| **Pull Requests** | Validation builds |
| **Manual** | `workflow_dispatch` with custom version |

### Artifacts

After successful build, artifacts are available in the Actions tab:

- **SheetAtlas-Windows-Installer** - `.exe` installer (30 days retention)
- **SheetAtlas-Linux-Build** - `.tar.gz` package (30 days)
- **SheetAtlas-macOS-Build** - `.tar.gz` package (30 days)

### Creating a Release

1. **Tag your commit:**
   ```bash
   git tag -a v1.1.0 -m "Release v1.1.0"
   git push origin v1.1.0
   ```

2. **GitHub Actions automatically:**
   - Builds all platform packages
   - Creates a GitHub Release
   - Uploads installer artifacts
   - Generates release notes

3. **Access the release:**
   - Go to **Releases** tab on GitHub
   - Download installer for your platform

---

## 🔐 Code Signing

### Current Status: Self-Signed Certificate

⚠️ **Development certificate only** - Not trusted by Windows SmartScreen.

**Location:** `build/certificates/`

**Files:**
- `SheetAtlas-CodeSigning.pfx` - Windows format
- `SheetAtlas-CodeSigning.crt` - Public certificate
- `SheetAtlas-CodeSigning.key` - Private key (keep secure!)

**Password:** `sheetatlas-dev`

### Production Code Signing

For production releases, you **MUST** obtain a commercial code signing certificate.

#### Option 1: Standard Code Signing Certificate

**Cost:** ~$100-300/year

**Providers:**
- [Sectigo](https://sectigo.com/ssl-certificates-tls/code-signing) - $100-150/year
- [Certum](https://www.certum.eu/en/code-signing-certificates/) - ~$100/year
- [DigiCert](https://www.digicert.com/signing/code-signing-certificates) - ~$300/year

**Pros:**
✅ Recognized by Windows
✅ Relatively affordable

**Cons:**
❌ SmartScreen warnings initially (reputation builds over time)
❌ Requires 6+ months and downloads to build reputation

#### Option 2: EV Code Signing Certificate (⭐ Recommended)

**Cost:** ~$300-600/year

**Providers:**
- [DigiCert EV](https://www.digicert.com/signing/code-signing-certificates) - ~$500/year
- [Sectigo EV](https://sectigo.com/ssl-certificates-tls/code-signing) - ~$300/year

**Pros:**
✅ **No SmartScreen warnings from day 1**
✅ Instant trust - Microsoft's highest validation level
✅ Includes hardware USB token

**Cons:**
❌ Higher cost
❌ More rigorous identity validation
❌ Requires physical USB token for signing

### Applying Your Certificate

1. **Obtain certificate** from provider (PFX/P12 file)

2. **Replace development certificate:**
   ```powershell
   # Backup old certificate
   Move-Item build\certificates\SheetAtlas-CodeSigning.pfx build\certificates\SheetAtlas-CodeSigning-dev.pfx

   # Copy your new certificate
   Copy-Item path\to\your\certificate.pfx build\certificates\SheetAtlas-CodeSigning.pfx
   ```

3. **Update password** in `build-windows-installer.ps1`:
   ```powershell
   $CertPassword = "your-certificate-password"
   ```

4. **Update GitHub Actions** (for CI/CD):
   - Add certificate as GitHub Secret: `CERT_PASSWORD`
   - Add certificate file (base64 encoded): `CERT_FILE`
   - Update workflow to decode and use certificate

### Signing Manually

```powershell
# Sign installer
signtool sign /f "build\certificates\SheetAtlas-CodeSigning.pfx" `
  /p "your-password" `
  /tr http://timestamp.digicert.com `
  /td sha256 `
  /fd sha256 `
  /v `
  "build\output\SheetAtlas-Setup-1.1.0-win-x64.exe"

# Verify signature
signtool verify /pa /v "build\output\SheetAtlas-Setup-1.1.0-win-x64.exe"
```

---

## 🛠️ Troubleshooting

### Windows

#### "Inno Setup not found"

**Solution:**
1. Download [Inno Setup 6](https://jrsoftware.org/isdl.php)
2. Install to default location: `C:\Program Files (x86)\Inno Setup 6`
3. Add to PATH or use full path in script

#### "signtool not found"

**Solution:**
1. Install [Windows SDK](https://developer.microsoft.com/windows/downloads/windows-sdk/)
2. Add to PATH:
   ```powershell
   $env:PATH += ";C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64"
   ```

#### Build size too large (>100MB)

**Solution:**
- Trimming is already enabled
- Consider `PublishSingleFile=true` (increases startup time slightly)
- Check for unnecessary assets in output

### Linux

#### Missing X11 libraries

```bash
# Ubuntu/Debian
sudo apt install libx11-6 libice6 libsm6 libfontconfig1

# Fedora
sudo dnf install libX11 libICE libSM fontconfig

# Arch
sudo pacman -S libx11 libice libsm fontconfig
```

#### Permission denied when running

```bash
chmod +x SheetAtlas.UI.Avalonia
```

### macOS

#### "SheetAtlas.app is damaged and can't be opened"

**Cause:** Gatekeeper blocking unsigned app

**Solution:**
```bash
xattr -cr /Applications/SheetAtlas.app
```

Or right-click > Open > Open (overrides Gatekeeper once)

---

## 📊 Build Output Sizes

| Platform | Uncompressed | Compressed (Installer/Tarball) |
|----------|--------------|-------------------------------|
| **Windows x64** | ~95 MB | ~35 MB (installer) |
| **Linux x64** | ~90 MB | ~30 MB (tarball) |
| **macOS x64** | ~92 MB | ~32 MB (tarball) |

With trimming and optimization enabled.

---

## 📚 Additional Resources

- [Inno Setup Documentation](https://jrsoftware.org/ishelp/)
- [.NET Deployment Guide](https://learn.microsoft.com/dotnet/core/deploying/)
- [Avalonia Deployment](https://docs.avaloniaui.net/docs/deployment/)
- [Code Signing Guide](https://docs.microsoft.com/windows/win32/seccrypto/cryptography-tools)

---

**For questions or issues, please open an issue on [GitHub](https://github.com/ghostintheshell-192/sheet-atlas/issues).**

*Last updated: October 2025*
