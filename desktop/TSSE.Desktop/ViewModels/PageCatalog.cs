using TSSE.Desktop.Services;

namespace TSSE.Desktop.ViewModels;

public sealed class PageCatalog
{
    private readonly IUiTextCatalog _text;

    public PageCatalog(IFoundationStatusService statusService, IUiTextCatalog text)
    {
        ArgumentNullException.ThrowIfNull(statusService);
        _text = text ?? throw new ArgumentNullException(nameof(text));
        FoundationStatus status = statusService.GetCurrent();
        string adapters = status.VerifiedAdapterCount.ToString(_text.Culture);

        Sections =
        [
            new SectionViewModel(
                "command-center", T("page.command.title"), T("page.command.eyebrow"),
                T("page.command.subtitle"), T("page.command.status"), T("page.command.emptyTitle"),
                T("page.command.emptyMessage"),
                [
                    new StatusCardViewModel(T("command.adapters.label"), adapters, T("command.adapters.detail")),
                    new StatusCardViewModel(T("command.reads.label"),
                        T(status.CanReadGameSaves ? "command.reads.enabled" : "command.reads.unavailable"),
                        T(status.CanReadGameSaves ? "command.reads.detailEnabled" : "command.reads.detailUnavailable")),
                    new StatusCardViewModel(T("command.writes.label"),
                        T(status.CanWriteGameSaves ? "command.writes.enabled" : "command.writes.disabled"),
                        T("command.writes.detail"))
                ], _text),
            new SectionViewModel(
                "savegame-explorer", T("page.save.title"), T("page.save.eyebrow"),
                T("page.save.subtitle"), T("page.save.status"), T("page.save.emptyTitle"),
                T("page.save.emptyMessage"),
                [new StatusCardViewModel(T("save.discovery.label"), T("save.discovery.value"), T("save.discovery.detail"))], _text),
            new SectionViewModel(
                "vehicle-studio", T("page.vehicle.title"), T("page.vehicle.eyebrow"),
                T("page.vehicle.subtitle"), T("page.vehicle.status"), T("page.vehicle.emptyTitle"),
                T("page.vehicle.emptyMessage"),
                [new StatusCardViewModel(T("vehicle.dataSource.label"), T("vehicle.dataSource.value"), T("vehicle.dataSource.detail"))], _text),
            new SectionViewModel(
                "economy-intelligence", T("page.economy.title"), T("page.economy.eyebrow"),
                T("page.economy.subtitle"), T("page.economy.status"), T("page.economy.emptyTitle"),
                T("page.economy.emptyMessage"),
                [
                    new StatusCardViewModel(T("economy.snapshots.label"), T("economy.snapshots.value"), T("economy.snapshots.detail")),
                    new StatusCardViewModel(T("economy.transactions.label"), T("economy.transactions.value"), T("economy.transactions.detail"))
                ], _text),
            new SectionViewModel(
                "time-machine", T("page.time.title"), T("page.time.eyebrow"),
                T("page.time.subtitle"), T("page.time.status"), T("page.time.emptyTitle"),
                T("page.time.emptyMessage"),
                [new StatusCardViewModel(T("time.restore.label"), T("time.restore.value"), T("time.restore.detail"))], _text),
            new SectionViewModel(
                "compatibility-lab", T("page.compatibility.title"), T("page.compatibility.eyebrow"),
                T("page.compatibility.subtitle"), T("page.compatibility.status"), T("page.compatibility.emptyTitle"),
                T("page.compatibility.emptyMessage"),
                [new StatusCardViewModel(T("compatibility.versions.label"), adapters, T("compatibility.versions.detail"))], _text),
            new SectionViewModel(
                "diagnostics-center", T("page.diagnostics.title"), T("page.diagnostics.eyebrow"),
                T("page.diagnostics.subtitle"), T("page.diagnostics.status"), T("page.diagnostics.emptyTitle"),
                T("page.diagnostics.emptyMessage"),
                [
                    new StatusCardViewModel(T("diagnostics.engine.label"), LocalizeEngineState(status.EngineState), T("diagnostics.engine.detail")),
                    new StatusCardViewModel(T("diagnostics.profileMonitoring.label"),
                        T("diagnostics.profileMonitoring.value"), T("diagnostics.profileMonitoring.detail"))
                ], _text),
            new SectionViewModel(
                "settings", T("page.settings.title"), T("page.settings.eyebrow"),
                T("page.settings.subtitle"), T("page.settings.status"), T("page.settings.emptyTitle"),
                T("page.settings.emptyMessage"),
                [
                    new StatusCardViewModel(T("settings.theme.label"), T("settings.theme.value"), T("settings.theme.detail")),
                    new StatusCardViewModel(T("settings.dataPolicy.label"), T("settings.dataPolicy.value"), T("settings.dataPolicy.detail")),
                    new StatusCardViewModel(T("settings.languageCardLabel"), _text.CurrentLanguageName, T("settings.languageDetail"))
                ], _text)
        ];
    }

    public IReadOnlyList<SectionViewModel> Sections { get; }

    private string T(string key) => _text.Get(key);

    private string LocalizeEngineState(string engineState) => engineState switch
    {
        "Foundation only" => T("diagnostics.engine.foundationOnly"),
        _ => engineState
    };
}
