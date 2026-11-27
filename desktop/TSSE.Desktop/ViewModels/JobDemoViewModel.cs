using System.Globalization;
using TSSE.Core.Editing;
using TSSE.Desktop.Services;
using TSSE.GameAdapters.Editing;

namespace TSSE.Desktop.ViewModels;

/// <summary>In-memory synthetic example; never opens or saves a file.</summary>
public sealed class JobDemoViewModel : ObservableObject
{
    private readonly DemoWorkspace _workspace;
    private readonly IUiTextCatalog _text;
    private readonly LegacyJobPreview _session;
    private string _unitsInput = "3";
    private string _distanceInput = "120";
    private string _message;
    private string _lastUnits = "3";
    private string _lastDistance = "120";

    public JobDemoViewModel(IUiTextCatalog text, DemoWorkspace? workspace = null)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _workspace = workspace ?? new DemoWorkspace();
        _session = _workspace.Workspace.Job;
        _workspace.Changed += (_, _) => RefreshFromWorkspace();
        _message = _text.Get("jobDemo.ready");
        ActionCommand = new RelayCommand<string>(Execute);
    }

    public string Heading => _text.Get("jobDemo.heading");
    public string Description => _text.Get("jobDemo.description");
    public string CargoLabel => _text.Get("jobDemo.cargo");
    public string Cargo => _session.CargoReference;
    public string UnitsLabel => _text.Get("jobDemo.units");
    public string DistanceLabel => _text.Get("jobDemo.distance");
    public string ApplyLabel => _text.Get("jobDemo.apply");
    public string UndoLabel => _text.Get("jobDemo.undo");
    public string RedoLabel => _text.Get("jobDemo.redo");
    public string PreviewLabel => _text.Get("jobDemo.preview");
    public string PreviewText => _session.PreviewText;
    public string ChangesLabel => _text.Get("editor.changes");
    public string SharedHistoryLabel => _text.Get("editor.sharedHistory");
    public string ChangeSummary => DemoChangeSummary.Format(_session.Changes, _text.Get("editor.noChanges"));
    public string UnitsInput { get => _unitsInput; set => SetProperty(ref _unitsInput, value ?? ""); }
    public string DistanceInput { get => _distanceInput; set => SetProperty(ref _distanceInput, value ?? ""); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public RelayCommand<string> ActionCommand { get; }

    private void Execute(string action)
    {
        bool success = false;
        try
        {
            if (action == "apply" && int.TryParse(UnitsInput, NumberStyles.None, CultureInfo.InvariantCulture, out int count))
            {
                _session.SetUnitsCount(count);
                success = true;
            }
            else if (action == "distance" && int.TryParse(DistanceInput, NumberStyles.None, CultureInfo.InvariantCulture, out int distance))
            {
                _session.SetPlannedDistanceKm(distance);
                success = true;
            }
            else if (action == "undo") success = _session.Undo();
            else if (action == "redo") success = _session.Redo();
        }
        catch (EditHistoryLimitException)
        {
            Message = _text.Get("editor.historyLimit");
            return;
        }
        catch (ArgumentOutOfRangeException) { }
        catch (InvalidOperationException) { }
        Message = _text.Get(success ? "jobDemo.applied" : "jobDemo.invalid");
        if (success)
        {
            _workspace.NotifyChanged();
            if (action == "apply") UnitsInput = _lastUnits;
            if (action == "distance") DistanceInput = _lastDistance;
        }
    }

    private void RefreshFromWorkspace()
    {
        string units = _session.UnitsCount.ToString(CultureInfo.InvariantCulture);
        string distance = _session.PlannedDistanceKm.ToString(CultureInfo.InvariantCulture);
        if (UnitsInput == _lastUnits) UnitsInput = units;
        if (DistanceInput == _lastDistance) DistanceInput = distance;
        _lastUnits = units;
        _lastDistance = distance;
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(ChangeSummary));
    }
}
