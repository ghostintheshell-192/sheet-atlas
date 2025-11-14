#!/bin/bash
set -e  # Exit on error

# ============================================
# SheetAtlas - macOS Installer Build Script
# ============================================
# Creates a .app bundle and .dmg installer for macOS
# Usage: ./build/build-macos-installer.sh [version]
# Example: ./build/build-macos-installer.sh 0.3.0

# ============================================
# Configuration
# ============================================
APP_NAME="SheetAtlas"
BUNDLE_ID="com.sheetatlas.app"
VERSION="${1:-0.0.0}"  # Default to 0.0.0 if not provided
PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUILD_DIR="$PROJECT_ROOT/build"
PUBLISH_DIR="$BUILD_DIR/publish/osx-x64"
APP_BUNDLE="$BUILD_DIR/SheetAtlas.app"
DMG_OUTPUT="$BUILD_DIR/SheetAtlas-macos-x64.dmg"
ICON_SOURCE="$PROJECT_ROOT/assets/icons/app.ico"

echo "=================================================="
echo "SheetAtlas macOS Installer Build"
echo "=================================================="
echo "Version: $VERSION"
echo "Project Root: $PROJECT_ROOT"
echo ""

# ============================================
# Step 1: Publish .NET Application
# ============================================
echo "[1/6] Publishing .NET application for macOS..."
dotnet publish "$PROJECT_ROOT/src/SheetAtlas.UI.Avalonia/SheetAtlas.UI.Avalonia.csproj" \
  --configuration Release \
  --runtime osx-x64 \
  --self-contained true \
  --output "$PUBLISH_DIR" \
  /p:PublishSingleFile=false \
  /p:PublishReadyToRun=true \
  /p:PublishTrimmed=true \
  /p:TrimMode=partial

echo "✓ Application published successfully"
echo ""

# ============================================
# Step 2: Create .app Bundle Structure
# ============================================
echo "[2/6] Creating .app bundle structure..."

# Clean up existing bundle
if [ -d "$APP_BUNDLE" ]; then
  echo "  Removing existing bundle..."
  rm -rf "$APP_BUNDLE"
fi

# Create .app bundle directories
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"
mkdir -p "$APP_BUNDLE/Contents/Frameworks"

echo "✓ Bundle structure created"
echo ""

# ============================================
# Step 3: Copy Application Files
# ============================================
echo "[3/6] Copying application files..."

# Copy all published files to MacOS directory
cp -R "$PUBLISH_DIR/"* "$APP_BUNDLE/Contents/MacOS/"

# Make the main executable... executable
chmod +x "$APP_BUNDLE/Contents/MacOS/SheetAtlas.UI.Avalonia"

echo "✓ Application files copied"
echo ""

# ============================================
# Step 4: Create/Convert Icon
# ============================================
echo "[4/6] Creating application icon..."

# Check if we have an .ico file to convert
if [ -f "$ICON_SOURCE" ]; then
  echo "  Converting .ico to .icns..."

  # Extract icon from .ico and create .icns
  # We'll use sips to convert if available (macOS only)
  if command -v sips &> /dev/null; then
    # Create temporary directory for icon conversion
    TEMP_ICONSET="$BUILD_DIR/temp.iconset"
    mkdir -p "$TEMP_ICONSET"

    # Convert .ico to PNG first (using sips)
    sips -s format png "$ICON_SOURCE" --out "$BUILD_DIR/temp.png" 2>/dev/null || {
      echo "  Warning: Could not convert icon with sips, creating placeholder..."
      # Create a simple placeholder icon
      echo "  Placeholder icon will be used"
    }

    # If conversion succeeded, create iconset
    if [ -f "$BUILD_DIR/temp.png" ]; then
      # Create different sizes for iconset
      for size in 16 32 64 128 256 512; do
        sips -z $size $size "$BUILD_DIR/temp.png" --out "$TEMP_ICONSET/icon_${size}x${size}.png" &>/dev/null
      done

      # Create @2x versions
      for size in 32 64 256 512 1024; do
        half_size=$((size / 2))
        sips -z $size $size "$BUILD_DIR/temp.png" --out "$TEMP_ICONSET/icon_${half_size}x${half_size}@2x.png" &>/dev/null
      done

      # Convert iconset to icns
      iconutil -c icns "$TEMP_ICONSET" -o "$APP_BUNDLE/Contents/Resources/AppIcon.icns"

      # Cleanup
      rm -rf "$TEMP_ICONSET" "$BUILD_DIR/temp.png"

      echo "  ✓ Icon converted successfully"
    fi
  else
    echo "  Warning: sips not available, skipping icon conversion"
    echo "  Note: Icon will need to be added manually"
  fi
else
  echo "  Warning: Icon source not found at $ICON_SOURCE"
fi

echo ""

# ============================================
# Step 5: Create Info.plist
# ============================================
echo "[5/6] Creating Info.plist..."

# Use template if available, otherwise create basic one
PLIST_TEMPLATE="$BUILD_DIR/installer/Info.plist.template"
PLIST_OUTPUT="$APP_BUNDLE/Contents/Info.plist"

if [ -f "$PLIST_TEMPLATE" ]; then
  # Replace VERSION_PLACEHOLDER with actual version
  sed "s/VERSION_PLACEHOLDER/$VERSION/g" "$PLIST_TEMPLATE" > "$PLIST_OUTPUT"
  echo "  ✓ Info.plist created from template"
else
  # Create basic Info.plist
  cat > "$PLIST_OUTPUT" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>$APP_NAME</string>
    <key>CFBundleExecutable</key>
    <string>SheetAtlas.UI.Avalonia</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon.icns</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF
  echo "  ✓ Basic Info.plist created"
fi

echo ""

# ============================================
# Step 6: Create DMG Installer
# ============================================
echo "[6/6] Creating DMG installer..."

# Remove existing DMG if present
if [ -f "$DMG_OUTPUT" ]; then
  rm "$DMG_OUTPUT"
fi

# Create temporary directory for DMG contents
DMG_TEMP="$BUILD_DIR/dmg-temp"
rm -rf "$DMG_TEMP"
mkdir -p "$DMG_TEMP"

# Copy .app bundle to temp directory
cp -R "$APP_BUNDLE" "$DMG_TEMP/"

# Create symbolic link to Applications folder
ln -s /Applications "$DMG_TEMP/Applications"

# Create DMG
echo "  Creating disk image..."
hdiutil create -volname "$APP_NAME" \
  -srcfolder "$DMG_TEMP" \
  -ov \
  -format UDZO \
  "$DMG_OUTPUT"

# Clean up temp directory
rm -rf "$DMG_TEMP"

echo "✓ DMG created successfully"
echo ""

# ============================================
# Summary
# ============================================
echo "=================================================="
echo "Build Complete!"
echo "=================================================="
echo "App Bundle: $APP_BUNDLE"
echo "DMG Installer: $DMG_OUTPUT"
echo ""

# Display file sizes
echo "File Sizes:"
du -h "$APP_BUNDLE" | tail -1
du -h "$DMG_OUTPUT"
echo ""

echo "Installation:"
echo "  1. Open SheetAtlas-macos-x64.dmg"
echo "  2. Drag SheetAtlas.app to Applications folder"
echo "  3. Launch from Applications or Launchpad"
echo ""
echo "Note: First launch may require right-click > Open"
echo "      to bypass Gatekeeper (unsigned application)"
echo "=================================================="
