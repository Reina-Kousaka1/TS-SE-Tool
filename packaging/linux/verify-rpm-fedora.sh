#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: verify-rpm-fedora.sh <package-directory> <linux-x64|linux-arm64>" >&2
  exit 2
fi

package_dir=$(cd -- "$1" && pwd)
rid=$2
case "$rid" in
  linux-x64) rpm_arch=x86_64 ;;
  linux-arm64) rpm_arch=aarch64 ;;
  *)
    echo "Unsupported Linux RID: $rid" >&2
    exit 2
    ;;
esac

rpm_path="$package_dir/ts-se-tool-0.0.0-0.phase2.${rpm_arch}.rpm"
if [[ ! -f "$rpm_path" ]]; then
  echo "Expected Fedora RPM is missing: $rpm_path" >&2
  exit 1
fi

rpm_fields=$(rpm -qp --queryformat '%{NAME}|%{VERSION}|%{RELEASE}|%{ARCH}' "$rpm_path")
IFS='|' read -r rpm_name rpm_version rpm_release rpm_package_arch <<< "$rpm_fields"
test "$rpm_name" = "ts-se-tool"
test "$rpm_version" = "0.0.0"
test "$rpm_release" = "0.phase2"
test "$rpm_package_arch" = "$rpm_arch"

if rpm -q ts-se-tool >/dev/null 2>&1; then
  echo "Refusing to replace an already installed ts-se-tool package." >&2
  exit 1
fi

xvfb_pid=""
cleanup() {
  if [[ -n "$xvfb_pid" ]] && kill -0 "$xvfb_pid" >/dev/null 2>&1; then
    kill "$xvfb_pid" >/dev/null 2>&1 || true
    wait "$xvfb_pid" >/dev/null 2>&1 || true
  fi
  dnf remove --assumeyes ts-se-tool >/dev/null 2>&1 || true
}
trap cleanup EXIT

# This is an unsigned CI-only package, so signature checking is explicitly out
# of scope. DNF still resolves all declared runtime dependencies from Fedora.
dnf install --assumeyes --nogpgcheck "$rpm_path"
test -x /usr/bin/ts-se-tool

display_number=$((90 + $$ % 100))
display=":$display_number"
Xvfb "$display" -screen 0 1280x800x24 -ac -nolisten tcp >"${TMPDIR:-/tmp}/ts-se-tool-xvfb.log" 2>&1 &
xvfb_pid=$!
for attempt in $(seq 1 50); do
  if [[ -S "/tmp/.X11-unix/X${display_number}" ]]; then
    break
  fi
  if ! kill -0 "$xvfb_pid" >/dev/null 2>&1; then
    cat "${TMPDIR:-/tmp}/ts-se-tool-xvfb.log" >&2
    exit 1
  fi
  sleep 0.1
done
if [[ ! -S "/tmp/.X11-unix/X${display_number}" ]]; then
  cat "${TMPDIR:-/tmp}/ts-se-tool-xvfb.log" >&2
  echo "Xvfb did not create its display socket." >&2
  exit 1
fi

DISPLAY="$display" TSSE_APP_PATH=/usr/bin/ts-se-tool python3 packaging/shared/desktop-smoke.py
kill "$xvfb_pid"
wait "$xvfb_pid" || true
xvfb_pid=""

dnf remove --assumeyes ts-se-tool
if rpm -q ts-se-tool >/dev/null 2>&1; then
  echo "DNF reported removal but the ts-se-tool RPM remains installed." >&2
  exit 1
fi

echo "Fedora $rpm_arch RPM dependency resolution, install, app launch, and removal passed."
