# Linux packaging

The repository can build validation packages for the self-contained Avalonia app as DEB and RPM on Ubuntu 24.04, for `linux-x64` and `linux-arm64`. The package contains the published app under `/opt/ts-se-tool`, a `/usr/bin/ts-se-tool` launcher, a desktop entry, and the repository's Apache-2.0 license text. Package metadata is marked as a Phase 2 validation build (`0.0.0~phase2` / `0.0.0-0.phase2`); these are not release packages.

## Build and verify

GitHub Actions run [45726994656](https://github.com/Reina-Kousaka1/TS-SE-Tool/actions/runs/45726994656) installed `rpm`, built both packages from their matching self-contained publish directories, verified package metadata and architecture, installed each package on disposable Ubuntu 24.04 x64/ARM64 runners, launched the installed app under Xvfb, and removed each package. The Debian package installed its declared Ubuntu 24.04 dependencies. The RPM was installed with dependency resolution disabled because the runner is Ubuntu; Fedora-family dependency resolution and runtime compatibility are not yet verified.

To build locally on Ubuntu 24.04, install `dpkg-deb`, `rpm`, and `rpmbuild`, then run:

```sh
bash packaging/linux/build-packages.sh <publish-directory> <output-directory> <linux-x64|linux-arm64>
bash packaging/linux/verify-packages.sh <output-directory> <linux-x64|linux-arm64>
```

The installer metadata declares the native Avalonia/X11 and .NET runtime libraries for Ubuntu 24.04 DEB packages, and the corresponding Fedora-family package names for RPM. Only Ubuntu 24.04 is exercised by CI. Wayland, other distribution releases, desktop accessibility review, signatures, AppImage, and repository publication are not covered. No package is uploaded or released by these workflows.
