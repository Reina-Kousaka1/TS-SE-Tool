#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: verify-bundle.sh <package-directory> <osx-x64|osx-arm64>" >&2
  exit 2
fi

package_dir=$(cd -- "$1" && pwd)
rid=$2
case "$rid" in
  osx-x64|osx-arm64) ;;
  *)
    echo "Unsupported macOS RID: $rid" >&2
    exit 2
    ;;
esac

bundle_path="$package_dir/TS-SE Tool.app"
dmg_path="$package_dir/TS-SE-Tool-${rid}.dmg"
if [[ ! -x "$bundle_path/Contents/MacOS/TSSE.Desktop" || ! -f "$dmg_path" ]]; then
  echo "Expected app bundle or disk image is missing from: $package_dir" >&2
  exit 1
fi
plutil -lint "$bundle_path/Contents/Info.plist"

repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
temp_root=$(mktemp -d)
mount_point="$temp_root/mount"
mkdir "$mount_point"
mounted=0
cleanup() {
  if [[ "$mounted" == "1" ]]; then
    hdiutil detach -quiet "$mount_point" || true
  fi
  rm -rf -- "$temp_root"
}
trap cleanup EXIT

hdiutil attach -readonly -nobrowse -mountpoint "$mount_point" "$dmg_path"
mounted=1
mounted_bundle="$mount_point/TS-SE Tool.app"
if [[ ! -x "$mounted_bundle/Contents/MacOS/TSSE.Desktop" ]]; then
  echo "The mounted disk image does not contain a runnable app bundle." >&2
  exit 1
fi
plutil -lint "$mounted_bundle/Contents/Info.plist"
TSSE_APP_PATH="$mounted_bundle/Contents/MacOS/TSSE.Desktop" python3 "$repo_root/packaging/shared/desktop-smoke.py"
hdiutil detach -quiet "$mount_point"
mounted=0

echo "The $rid unsigned app bundle and DMG passed metadata, mount, launch, and detach checks."
