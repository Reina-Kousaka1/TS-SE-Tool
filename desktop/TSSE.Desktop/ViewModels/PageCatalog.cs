using TSSE.Desktop.Services;

namespace TSSE.Desktop.ViewModels;

public sealed class PageCatalog
{
    public PageCatalog(IFoundationStatusService statusService)
    {
        FoundationStatus status = statusService.GetCurrent();
        Sections =
        [
            new SectionViewModel(
                "command-center", "Command Center", "OVERVIEW",
                "A clear view of the controller foundation and the capabilities that are actually available.",
                "FOUNDATION ONLY", "No profile is connected",
                "The app does not scan or open game profiles yet. Nothing is read from your machine.",
                [
                    new StatusCardViewModel("VERIFIED ADAPTERS", status.VerifiedAdapterCount.ToString(), "Exact game/save versions with reviewed support."),
                    new StatusCardViewModel("SAVEGAME READS", status.CanReadGameSaves ? "Enabled" : "Unavailable", status.CanReadGameSaves ? "Read-only adapters are registered." : "No verified parser or profile reader is registered."),
                    new StatusCardViewModel("SAVEGAME WRITES", status.CanWriteGameSaves ? "Enabled" : "Disabled", "Writes remain disabled until game-specific validation and recovery are proven.")
                ]),
            new SectionViewModel(
                "savegame-explorer", "Savegame Explorer", "FILES & PROFILES",
                "Prepare a safe, read-only overview of local ETS2 and ATS profiles.",
                "PREVIEW", "Profile discovery is not active",
                "No files have been scanned. A supported parser and explicit folder permission are required before read-only discovery can be enabled.",
                [new StatusCardViewModel("DISCOVERY", "Not enabled", "No game folders or save files were accessed.")]),
            new SectionViewModel(
                "vehicle-studio", "Vehicle Studio", "VEHICLE WORKSPACE",
                "Truck, trailer and coach editing concepts without loading or changing vehicle data.",
                "PREVIEW", "No vehicle data loaded",
                "The editor is an interface preview only. Vehicle fields, mod data and Coaches DLC schemas are not yet verified.",
                [new StatusCardViewModel("DATA SOURCE", "None", "A compatible, verified savegame adapter is required.")]),
            new SectionViewModel(
                "economy-intelligence", "Economy Intelligence", "LOCAL ANALYTICS",
                "A future local-only view of recorded transactions and snapshot-based balance changes.",
                "PREVIEW", "No snapshots available",
                "No account values or historical events are shown. Calculated balance differences will remain separate from recorded transactions.",
                [new StatusCardViewModel("SNAPSHOT HISTORY", "0", "No profile has been read or imported."),
                 new StatusCardViewModel("TRANSACTIONS", "Not loaded", "No historical transactions are inferred from balances.")]),
            new SectionViewModel(
                "time-machine", "Savegame Time Machine", "SNAPSHOT HISTORY",
                "Compare local snapshots only after a verified read-only pipeline exists.",
                "PREVIEW", "Nothing to compare",
                "No backup, restore point or savegame has been created by this prototype.",
                [new StatusCardViewModel("RESTORE ACTIONS", "Unavailable", "This screen cannot replace or restore files.")]),
            new SectionViewModel(
                "compatibility-lab", "Compatibility Lab", "VERSION SUPPORT",
                "Make supported game and save versions explicit before enabling any parser.",
                "UNVERIFIED", "No game versions are registered",
                "Unknown game IDs and save versions stay unsupported. This foundation does not claim ETS2, ATS, mod or Coaches compatibility.",
                [new StatusCardViewModel("REGISTERED VERSIONS", status.VerifiedAdapterCount.ToString(), "The compatibility registry starts empty.")]),
            new SectionViewModel(
                "diagnostics-center", "Diagnostics Center", "SYSTEM STATUS",
                "Show real foundation and validation status without fabricating health or game data.",
                "FOUNDATION ONLY", "No game diagnostics source",
                "The desktop app is running locally. No game process or save directory is being monitored.",
                [new StatusCardViewModel("ENGINE", status.EngineState, "Sourced from the current foundation status service."),
                 new StatusCardViewModel("PROFILE MONITORING", "Off", "No background profile watcher is installed.")]),
            new SectionViewModel(
                "settings", "Settings", "PREFERENCES",
                "Local interface preferences and clear security boundaries.",
                "AVAILABLE", "Appearance applies to this session",
                "Theme selection is not persisted yet. The application has no cloud account, remote service or profile-write setting.",
                [new StatusCardViewModel("DATA POLICY", "Local prototype", "No savegame data is collected or transmitted.")])
        ];
    }

    public IReadOnlyList<SectionViewModel> Sections { get; }
}
