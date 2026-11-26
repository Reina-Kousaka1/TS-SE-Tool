#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "Usage: build-bundle.sh <publish-directory> <output-directory> <osx-x64|osx-arm64>" >&2
  exit 2
fi

publish_dir=$(cd -- "$1" && pwd)
mkdir -p -- "$2"
output_dir=$(cd -- "$2" && pwd)
rid=$3
case "$rid" in
  osx-x64|osx-arm64) ;;
  *)
    echo "Unsupported macOS RID: $rid" >&2
    exit 2
    ;;
esac

for command_name in ditto hdiutil plutil; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Required macOS packaging tool is unavailable: $command_name" >&2
    exit 1
  fi
done

if [[ ! -x "$publish_dir/TSSE.Desktop" || ! -f "$publish_dir/TSSE.Desktop.dll" ]]; then
  echo "Expected apphost and managed application are missing from: $publish_dir" >&2
  exit 1
fi
if [[ -n "$(find "$publish_dir" -type l -print -quit)" ]]; then
  echo "Refusing to package a publish directory containing symbolic links." >&2
  exit 1
fi

bundle_path="$output_dir/TS-SE Tool.app"
dmg_path="$output_dir/TS-SE-Tool-${rid}.dmg"
if [[ -e "$bundle_path" || -e "$dmg_path" ]]; then
  echo "Refusing to overwrite an existing macOS package in: $output_dir" >&2
  exit 1
fi

script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
repo_root=$(cd -- "$script_dir/../.." && pwd)
temp_root=$(mktemp -d)
trap 'rm -rf -- "$temp_root"' EXIT

install -d "$bundle_path/Contents/Resources"
ditto "$publish_dir" "$bundle_path/Contents/MacOS"
install -m 0644 "$repo_root/LICENSE" "$bundle_path/Contents/Resources/LICENSE.txt"
cat > "$bundle_path/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key>
  <string>TSSE.Desktop</string>
  <key>CFBundleIdentifier</key>
  <string>com.github.reina-kousaka1.ts-se-tool</string>
  <key>CFBundleName</key>
  <string>TS-SE Tool</string>
  <key>CFBundleDisplayName</key>
  <string>TS-SE Tool</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>0.0.0</string>
  <key>CFBundleVersion</key>
  <string>0.0.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
PLIST
plutil -lint "$bundle_path/Contents/Info.plist"

install -d "$temp_root/dmg-root"
ditto "$bundle_path" "$temp_root/dmg-root/TS-SE Tool.app"
hdiutil create -volname "TS-SE Tool" -srcfolder "$temp_root/dmg-root" -ov -format UDZO "$dmg_path"

printf 'Created unsigned app bundle %s\n' "$bundle_path"
printf 'Created unsigned disk image %s\n' "$dmg_path"
