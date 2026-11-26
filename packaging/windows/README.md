# Windows packaging: planned

## Target

Package the self-contained Avalonia .NET 10 desktop application as separate MSI installers for Windows x64 (`win-x64`) and ARM64 (`win-arm64`). Publish each RID independently, then use a WiX SDK-style project to build the matching MSI. WiX's `InstallerPlatform` supports both `x64` and `arm64`; .NET self-contained publication is also RID-specific ([.NET deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/), [WiX MSBuild SDK](https://docs.firegiant.com/wix/tools/msbuild/)).

## Validation required

- Build each MSI from its matching self-contained publish directory.
- Verify install, launch, upgrade, repair, and uninstall on clean Windows x64 and ARM64 environments.
- Confirm that user settings remain in the per-user local application-data location across upgrade and uninstall.
- Do not publish or sign installers during Phase 2.

## Tooling and adoption status

No WiX project or installer is implemented. The current workstation has .NET SDK 8.0.425, no WiX CLI, and no .NET 10 SDK, so it cannot produce or validate the planned package locally.

The official WiX documentation lists WiX v7.0.1 as the current release. WiX v7 requires explicit acceptance of EULA ID `wix7`. The current OSMF terms require organizations generating more than US$10,000 in annual revenue from projects that use WiX to sponsor the WiX Toolset. The repository owner must confirm how that threshold applies, verify any required sponsorship, and explicitly authorize recording the EULA acceptance before `AcceptEula` is added to a project or a WiX build is run. No EULA acceptance or MSI build has been performed. WiX v6 is not used as a workaround: consumer security fixes end on 2027-02-05 ([WiX lifecycle](https://docs.firegiant.com/wix/), [WiX release notes](https://docs.firegiant.com/wix/whatsnew/releasenotes/), [OSMF and EULA](https://docs.firegiant.com/wix/osmf/)).

Avalonia uses its Win32 backend; the desktop app has no WebView2, browser, Phoenix, or OTP dependency.
