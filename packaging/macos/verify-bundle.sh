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
detach_mounted_image() {
  local attempt
  for attempt in 1 2 3 4 5; do
    if hdiutil detach -quiet "$mount_point"; then
      mounted=0
      return 0
    fi
    if [[ "$attempt" != "5" ]]; then
      sleep 1
    fi
  done

  echo "The mounted disk image is still busy after five detach attempts; retrying once with diagnostics." >&2
  if hdiutil detach "$mount_point"; then
    mounted=0
    return 0
  fi

  return 1
}

cleanup() {
  local exit_status=$?
  trap - EXIT
  if [[ "$mounted" == "1" ]]; then
    if ! detach_mounted_image; then
      echo "Cannot remove the temporary directory while the DMG remains mounted at $mount_point; leaving it for runner cleanup." >&2
      if [[ "$exit_status" == "0" ]]; then
        exit_status=1
      fi
      exit "$exit_status"
    fi
  fi
  rm -rf -- "$temp_root"
  exit "$exit_status"
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
if ! detach_mounted_image; then
  echo "Unable to detach the mounted disk image after validation." >&2
  exit 1
fi

echo "The $rid unsigned app bundle and DMG passed metadata, mount, launch, and detach checks."
