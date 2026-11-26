#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: verify-packages.sh <package-directory> <linux-x64|linux-arm64>" >&2
  exit 2
fi

package_dir=$(cd -- "$1" && pwd)
rid=$2
case "$rid" in
  linux-x64)
    deb_arch=amd64
    rpm_arch=x86_64
    ;;
  linux-arm64)
    deb_arch=arm64
    rpm_arch=aarch64
    ;;
  *)
    echo "Unsupported Linux RID: $rid" >&2
    exit 2
    ;;
esac

deb_path="$package_dir/ts-se-tool_0.0.0~phase2_${deb_arch}.deb"
rpm_path="$package_dir/ts-se-tool-0.0.0-0.phase2.${rpm_arch}.rpm"
if [[ ! -f "$deb_path" || ! -f "$rpm_path" ]]; then
  echo "Expected DEB and RPM packages are missing from: $package_dir" >&2
  exit 1
fi

test "$(dpkg-deb --field "$deb_path" Package)" = "ts-se-tool"
test "$(dpkg-deb --field "$deb_path" Version)" = "0.0.0~phase2"
test "$(dpkg-deb --field "$deb_path" Architecture)" = "$deb_arch"
rpm_fields=$(rpm -qp --queryformat '%{NAME}|%{VERSION}|%{RELEASE}|%{ARCH}' "$rpm_path")
IFS='|' read -r rpm_name rpm_version rpm_release rpm_package_arch <<< "$rpm_fields"
test "$rpm_name" = "ts-se-tool"
test "$rpm_version" = "0.0.0"
test "$rpm_release" = "0.phase2"
test "$rpm_package_arch" = "$rpm_arch"

repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
cleanup() {
  sudo rpm -e ts-se-tool >/dev/null 2>&1 || true
  sudo apt-get remove -y ts-se-tool >/dev/null 2>&1 || true
}
trap cleanup EXIT

# The GitHub Actions runner is disposable. Install each package with its native
# package manager, start the installed wrapper under Xvfb, then remove it.
sudo apt install --no-install-recommends -y "$deb_path"
TSSE_APP_PATH=/usr/bin/ts-se-tool xvfb-run -a python3 "$repo_root/packaging/shared/desktop-smoke.py"
sudo apt-get remove -y ts-se-tool

sudo rpm --initdb
sudo rpm --install --nodeps --noscripts "$rpm_path"
TSSE_APP_PATH=/usr/bin/ts-se-tool xvfb-run -a python3 "$repo_root/packaging/shared/desktop-smoke.py"
sudo rpm --erase ts-se-tool

echo "DEB and RPM metadata, native installation, launch, and removal checks passed for $rid."
