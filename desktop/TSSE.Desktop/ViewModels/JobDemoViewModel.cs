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
    private string _baseUnits = "3";
    private string _baseDistance = "120";

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
    public string UnitsInput { get => _unitsInput; set { if (_unitsInput == _lastUnits) _baseUnits = _lastUnits; if (SetProperty(ref _unitsInput, value ?? "")) NotifyDrafts(); } }
    public string DistanceInput { get => _distanceInput; set { if (_distanceInput == _lastDistance) _baseDistance = _lastDistance; if (SetProperty(ref _distanceInput, value ?? "")) NotifyDrafts(); } }
    public bool IsUnitsStale => UnitsInput != _lastUnits && _baseUnits != _lastUnits;
    public bool IsDistanceStale => DistanceInput != _lastDistance && _baseDistance != _lastDistance;
    public string UnitsDraftNotice => IsUnitsStale ? _text.Format("editor.staleDraft", _lastUnits) : "";
    public string DistanceDraftNotice => IsDistanceStale ? _text.Format("editor.staleDraft", _lastDistance) : "";
    public string ApplyReviewedLabel => _text.Get("editor.applyReviewed");
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public RelayCommand<string> ActionCommand { get; }

    private void Execute(string action)
    {
        if ((action == "apply" && IsUnitsStale) || (action == "distance" && IsDistanceStale))
        {
            Message = _text.Get("editor.staleBlocked");
            return;
        }
        bool success = false;
        try
        {
            if ((action is "apply" or "apply-reviewed") && int.TryParse(UnitsInput, NumberStyles.None, CultureInfo.InvariantCulture, out int count))
            {
                _session.SetUnitsCount(count);
                success = true;
            }
            else if ((action is "distance" or "distance-reviewed") && int.TryParse(DistanceInput, NumberStyles.None, CultureInfo.InvariantCulture, out int distance))
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
            if (action is "apply" or "apply-reviewed") { _baseUnits = _lastUnits; SetProperty(ref _unitsInput, _lastUnits, nameof(UnitsInput)); }
            if (action is "distance" or "distance-reviewed") { _baseDistance = _lastDistance; SetProperty(ref _distanceInput, _lastDistance, nameof(DistanceInput)); }
            NotifyDrafts();
        }
    }

    private void RefreshFromWorkspace()
    {
        string units = _session.UnitsCount.ToString(CultureInfo.InvariantCulture);
        string distance = _session.PlannedDistanceKm.ToString(CultureInfo.InvariantCulture);
        if (UnitsInput == _lastUnits || UnitsInput == units) { _baseUnits = units; SetProperty(ref _unitsInput, units, nameof(UnitsInput)); }
        if (DistanceInput == _lastDistance || DistanceInput == distance) { _baseDistance = distance; SetProperty(ref _distanceInput, distance, nameof(DistanceInput)); }
        _lastUnits = units;
        _lastDistance = distance;
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(ChangeSummary));
        NotifyDrafts();
    }

    private void NotifyDrafts()
    {
        OnPropertyChanged(nameof(IsUnitsStale));
        OnPropertyChanged(nameof(IsDistanceStale));
        OnPropertyChanged(nameof(UnitsDraftNotice));
        OnPropertyChanged(nameof(DistanceDraftNotice));
    }
}
