#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "Usage: build-packages.sh <publish-directory> <output-directory> <linux-x64|linux-arm64>" >&2
  exit 2
fi

publish_dir=$(cd -- "$1" && pwd)
mkdir -p -- "$2"
output_dir=$(cd -- "$2" && pwd)
rid=$3

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

if [[ ! -x "$publish_dir/TSSE.Desktop" ]]; then
  echo "Expected executable is missing: $publish_dir/TSSE.Desktop" >&2
  exit 1
fi
if [[ -n "$(find "$publish_dir" -type l -print -quit)" ]]; then
  echo "Refusing to package a publish directory containing symbolic links." >&2
  exit 1
fi

for command_name in dpkg-deb rpmbuild tar; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Required packaging tool is unavailable: $command_name" >&2
    exit 1
  fi
done

deb_path="$output_dir/ts-se-tool_0.0.0~phase2_${deb_arch}.deb"
rpm_path="$output_dir/ts-se-tool-0.0.0-0.phase2.${rpm_arch}.rpm"
if [[ -e "$deb_path" || -e "$rpm_path" ]]; then
  echo "Refusing to overwrite an existing package in: $output_dir" >&2
  exit 1
fi

script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
repo_root=$(cd -- "$script_dir/../.." && pwd)
temp_root=$(mktemp -d)
trap 'rm -rf -- "$temp_root"' EXIT

payload_root="$temp_root/payload"
install -d \
  "$payload_root/opt/ts-se-tool" \
  "$payload_root/usr/bin" \
  "$payload_root/usr/share/applications" \
  "$payload_root/usr/share/doc/ts-se-tool"
cp -a "$publish_dir/." "$payload_root/opt/ts-se-tool/"
install -m 0644 "$repo_root/LICENSE" "$payload_root/usr/share/doc/ts-se-tool/copyright"
cat > "$payload_root/usr/bin/ts-se-tool" <<'WRAPPER'
#!/bin/sh
exec /opt/ts-se-tool/TSSE.Desktop "$@"
WRAPPER
chmod 0755 "$payload_root/usr/bin/ts-se-tool"
cat > "$payload_root/usr/share/applications/ts-se-tool.desktop" <<'DESKTOP'
[Desktop Entry]
Type=Application
Name=TS-SE Tool
Comment=Local savegame tool foundation
Exec=ts-se-tool
Terminal=false
Categories=Utility;
DESKTOP

deb_root="$temp_root/deb-root"
mkdir -p "$deb_root/DEBIAN"
cp -a "$payload_root/." "$deb_root/"
cat > "$deb_root/DEBIAN/control" <<CONTROL
Package: ts-se-tool
Version: 0.0.0~phase2
Section: utils
Priority: optional
Architecture: $deb_arch
Maintainer: TS-SE Tool contributors <noreply@github.com>
Depends: ca-certificates, libc6, libgcc-s1, libstdc++6, libgssapi-krb5-2, libicu74, libssl3t64, tzdata, zlib1g, libfontconfig1, libx11-6, libice6, libsm6
Description: TS-SE Tool local desktop foundation
 Phase 2 validation package for the self-contained Avalonia desktop application.
 This package is built and tested by CI and is not a stable release.
CONTROL
dpkg-deb --build --root-owner-group "$deb_root" "$deb_path"

rpm_top="$temp_root/rpmbuild"
mkdir -p "$rpm_top/BUILD" "$rpm_top/BUILDROOT" "$rpm_top/RPMS" "$rpm_top/SOURCES" "$rpm_top/SPECS" "$rpm_top/SRPMS"
tar -czf "$rpm_top/SOURCES/payload.tar.gz" -C "$temp_root" payload
cat > "$rpm_top/SPECS/ts-se-tool.spec" <<SPEC
%global _enable_debug_packages 0
Name: ts-se-tool
Version: 0.0.0
Release: 0.phase2
Summary: TS-SE Tool local desktop foundation
License: Apache-2.0
BuildArch: $rpm_arch
AutoReqProv: no
Requires: glibc
Requires: libgcc
Requires: libstdc++
Requires: krb5-libs
Requires: libicu
Requires: openssl-libs
Requires: ca-certificates
Requires: tzdata
Requires: zlib
Requires: fontconfig
Requires: libX11
Requires: libICE
Requires: libSM
Source0: payload.tar.gz

%description
Phase 2 validation package for the self-contained Avalonia desktop application.
This package is built and tested by CI and is not a stable release.

%prep
%setup -q -n payload

%build

%install
install -d %{buildroot}/opt/ts-se-tool
cp -a opt/ts-se-tool/. %{buildroot}/opt/ts-se-tool/
install -D -m 0755 usr/bin/ts-se-tool %{buildroot}/usr/bin/ts-se-tool
install -D -m 0644 usr/share/applications/ts-se-tool.desktop %{buildroot}/usr/share/applications/ts-se-tool.desktop
install -D -m 0644 usr/share/doc/ts-se-tool/copyright %{buildroot}/usr/share/doc/ts-se-tool/copyright

%files
%dir /opt/ts-se-tool
/opt/ts-se-tool/*
%attr(0755,root,root) /usr/bin/ts-se-tool
/usr/share/applications/ts-se-tool.desktop
%license /usr/share/doc/ts-se-tool/copyright
SPEC
rpmbuild --define "_topdir $rpm_top" -bb "$rpm_top/SPECS/ts-se-tool.spec"
found_rpm=$(find "$rpm_top/RPMS" -type f -name 'ts-se-tool-*.rpm' -print -quit)
if [[ -z "$found_rpm" ]]; then
  echo "rpmbuild did not produce a binary RPM." >&2
  exit 1
fi
cp -- "$found_rpm" "$rpm_path"

printf 'Created %s\n' "$deb_path"
printf 'Created %s\n' "$rpm_path"
