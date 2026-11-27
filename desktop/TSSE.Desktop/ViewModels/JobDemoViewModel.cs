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
    private string _modelInput = "0";
    private string _urgencyInput = "0";
    private string _lastUrgency = "0";
    private string _baseUrgency = "0";
    private string _message;
    private string _lastUnits = "3";
    private string _lastDistance = "120";
    private string _baseUnits = "3";
    private string _baseDistance = "120";
    private string _lastModel = "0";
    private string _baseModel = "0";
    private string _selectedCargo = "cargo.synthetic";
    private string _lastCargo = "cargo.synthetic";
    private string _baseCargo = "cargo.synthetic";

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
    public IReadOnlyList<string> CargoChoices => _session.ObservedCargoReferences();
    public string CargoSelectionLabel => _text.Get("jobDemo.cargoSelection");
    public string CargoSelectionDetail => _text.Get("jobDemo.cargoSelectionDetail");
    public string ApplyCargoLabel => _text.Get("jobDemo.applyCargo");
    public string DiscardCargoLabel => _text.Get("jobDemo.discardCargo");
    public string ApplyCargoDetailsLabel => _text.Get("jobDemo.applyCargoDetails");
    public string ApplyCargoDetailsReviewedLabel => _text.Get("jobDemo.applyCargoDetailsReviewed");
    public bool IsCargoDetailsStale => IsCargoStale || IsUnitsStale || IsModelStale;
    public string SelectedCargo { get => _selectedCargo; set { if (_selectedCargo == _lastCargo) _baseCargo = _lastCargo; if (SetProperty(ref _selectedCargo, value ?? "")) NotifyDrafts(); } }
    public bool IsCargoStale => SelectedCargo != _lastCargo && _baseCargo != _lastCargo;
    public string CargoDraftNotice => IsCargoStale ? _text.Format("editor.staleDraft", _lastCargo) : "";
    public string RouteLabel => _text.Get("jobDemo.route");
    public string RouteDetail => _text.Get("jobDemo.routeDetail");
    public string Route => _text.Format("jobDemo.routeValue", _session.SourceCompany, _session.TargetCompany);
    public string SwapRouteLabel => _text.Get("jobDemo.swapRoute");
    public string UnitsLabel => _text.Get("jobDemo.units");
    public string DistanceLabel => _text.Get("jobDemo.distance");
    public string ModelLabel => _text.Get("jobDemo.modelIndex");
    public string ModelDetail => _text.Get("jobDemo.modelDetail");
    public string UrgencyLabel => _text.Get("jobDemo.urgency");
    public string UrgencyDetail => _text.Get("jobDemo.urgencyDetail");
    public string DiscardUrgencyLabel => _text.Get("jobDemo.discardUrgency");
    public string UrgencyInput { get => _urgencyInput; set { if (_urgencyInput == _lastUrgency) _baseUrgency = _lastUrgency; if (SetProperty(ref _urgencyInput, value ?? "")) NotifyDrafts(); } }
    public bool IsUrgencyStale => UrgencyInput != _lastUrgency && _baseUrgency != _lastUrgency;
    public string UrgencyDraftNotice => IsUrgencyStale ? _text.Format("editor.staleDraft", _lastUrgency) : "";
    public string ApplyLabel => _text.Get("jobDemo.apply");
    public string ApplyBothLabel => _text.Get("jobDemo.applyBoth");
    public string ApplyBothReviewedLabel => _text.Get("jobDemo.applyBothReviewed");
    public string DiscardUnitsLabel => _text.Get("jobDemo.discardUnits");
    public string DiscardDistanceLabel => _text.Get("jobDemo.discardDistance");
    public string DiscardModelLabel => _text.Get("jobDemo.discardModel");
    public string UndoLabel => _text.Get("jobDemo.undo");
    public string RedoLabel => _text.Get("jobDemo.redo");
    public string PreviewLabel => _text.Get("jobDemo.preview");
    public string PreviewText => _session.PreviewText;
    public string ChangesLabel => _text.Get("editor.changes");
    public string SharedHistoryLabel => _text.Get("editor.sharedHistory");
    public string ChangeSummary => DemoChangeSummary.Format(_session.Changes, _text.Get("editor.noChanges"));
    public string UnitsInput { get => _unitsInput; set { if (_unitsInput == _lastUnits) _baseUnits = _lastUnits; if (SetProperty(ref _unitsInput, value ?? "")) NotifyDrafts(); } }
    public string DistanceInput { get => _distanceInput; set { if (_distanceInput == _lastDistance) _baseDistance = _lastDistance; if (SetProperty(ref _distanceInput, value ?? "")) NotifyDrafts(); } }
    public string ModelInput { get => _modelInput; set { if (_modelInput == _lastModel) _baseModel = _lastModel; if (SetProperty(ref _modelInput, value ?? "")) NotifyDrafts(); } }
    public bool IsUnitsStale => UnitsInput != _lastUnits && _baseUnits != _lastUnits;
    public bool IsDistanceStale => DistanceInput != _lastDistance && _baseDistance != _lastDistance;
    public bool IsModelStale => ModelInput != _lastModel && _baseModel != _lastModel;
    public bool IsEitherStale => IsUnitsStale || IsDistanceStale;
    public string UnitsDraftNotice => IsUnitsStale ? _text.Format("editor.staleDraft", _lastUnits) : "";
    public string DistanceDraftNotice => IsDistanceStale ? _text.Format("editor.staleDraft", _lastDistance) : "";
    public string ModelDraftNotice => IsModelStale ? _text.Format("editor.staleDraft", _lastModel) : "";
    public string ApplyReviewedLabel => _text.Get("editor.applyReviewed");
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public RelayCommand<string> ActionCommand { get; }

    private void Execute(string action)
    {
        if (action is "discard-units" or "discard-distance" or "discard-model" or "discard-cargo" or "discard-urgency")
        {
            if (action == "discard-units") { _baseUnits = _lastUnits; SetProperty(ref _unitsInput, _lastUnits, nameof(UnitsInput)); }
            else if (action == "discard-distance") { _baseDistance = _lastDistance; SetProperty(ref _distanceInput, _lastDistance, nameof(DistanceInput)); }
            else if (action == "discard-model") { _baseModel = _lastModel; SetProperty(ref _modelInput, _lastModel, nameof(ModelInput)); }
            else if (action == "discard-cargo") { _baseCargo = _lastCargo; SetProperty(ref _selectedCargo, _lastCargo, nameof(SelectedCargo)); }
            else { _baseUrgency = _lastUrgency; SetProperty(ref _urgencyInput, _lastUrgency, nameof(UrgencyInput)); }
            NotifyDrafts();
            Message = _text.Get("jobDemo.draftDiscarded");
            return;
        }
        if ((action == "apply" && IsUnitsStale) || (action == "distance" && IsDistanceStale) ||
            (action == "apply-both" && (IsUnitsStale || IsDistanceStale)) || (action == "model" && IsModelStale) ||
            (action == "cargo" && IsCargoStale) || (action == "cargo-details" && IsCargoDetailsStale) ||
            (action == "urgency" && IsUrgencyStale))
        {
            Message = _text.Get("editor.staleBlocked");
            return;
        }
        bool success = false;
        try
        {
            if ((action is "cargo-details" or "cargo-details-reviewed") &&
                int.TryParse(UnitsInput, NumberStyles.None, CultureInfo.InvariantCulture, out int cargoCount) &&
                int.TryParse(ModelInput, NumberStyles.None, CultureInfo.InvariantCulture, out int cargoModel))
            {
                _session.SetCargoDetails(SelectedCargo, cargoCount, cargoModel);
                success = true;
            }
            else if ((action is "apply-both" or "apply-both-reviewed") &&
                int.TryParse(UnitsInput, NumberStyles.None, CultureInfo.InvariantCulture, out int bothCount) &&
                int.TryParse(DistanceInput, NumberStyles.None, CultureInfo.InvariantCulture, out int bothDistance))
            {
                _session.SetUnitsAndPlannedDistance(bothCount, bothDistance);
                success = true;
            }
            else if ((action is "apply" or "apply-reviewed") && int.TryParse(UnitsInput, NumberStyles.None, CultureInfo.InvariantCulture, out int count))
            {
                _session.SetUnitsCount(count);
                success = true;
            }
            else if ((action is "distance" or "distance-reviewed") && int.TryParse(DistanceInput, NumberStyles.None, CultureInfo.InvariantCulture, out int distance))
            {
                _session.SetPlannedDistanceKm(distance);
                success = true;
            }
            else if ((action is "model" or "model-reviewed") && int.TryParse(ModelInput, NumberStyles.None, CultureInfo.InvariantCulture, out int model))
            {
                _session.SetCargoModelIndex(model);
                success = true;
            }
            else if ((action is "urgency" or "urgency-reviewed") && int.TryParse(UrgencyInput, NumberStyles.None, CultureInfo.InvariantCulture, out int urgency))
            {
                _session.SetUrgency(urgency);
                success = true;
            }
            else if (action is "cargo" or "cargo-reviewed") { _session.SelectObservedCargo(SelectedCargo); success = true; }
            else if (action == "swap-route") { _session.SwapRoute(); success = true; }
            else if (action == "undo") success = _session.Undo();
            else if (action == "redo") success = _session.Redo();
        }
        catch (EditHistoryLimitException)
        {
            Message = _text.Get("editor.historyLimit");
            return;
        }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
        Message = _text.Get(success ? "jobDemo.applied" : "jobDemo.invalid");
        if (success)
        {
            _workspace.NotifyChanged();
            if (action is "apply" or "apply-reviewed" or "apply-both" or "apply-both-reviewed" or "cargo-details" or "cargo-details-reviewed") { _baseUnits = _lastUnits; SetProperty(ref _unitsInput, _lastUnits, nameof(UnitsInput)); }
            if (action is "distance" or "distance-reviewed" or "apply-both" or "apply-both-reviewed") { _baseDistance = _lastDistance; SetProperty(ref _distanceInput, _lastDistance, nameof(DistanceInput)); }
            if (action is "model" or "model-reviewed" or "cargo-details" or "cargo-details-reviewed") { _baseModel = _lastModel; SetProperty(ref _modelInput, _lastModel, nameof(ModelInput)); }
            if (action is "cargo" or "cargo-reviewed" or "cargo-details" or "cargo-details-reviewed") { _baseCargo = _lastCargo; SetProperty(ref _selectedCargo, _lastCargo, nameof(SelectedCargo)); }
            if (action is "urgency" or "urgency-reviewed") { _baseUrgency = _lastUrgency; SetProperty(ref _urgencyInput, _lastUrgency, nameof(UrgencyInput)); }
            NotifyDrafts();
        }
    }

    private void RefreshFromWorkspace()
    {
        string units = _session.UnitsCount.ToString(CultureInfo.InvariantCulture);
        string distance = _session.PlannedDistanceKm.ToString(CultureInfo.InvariantCulture);
        string model = _session.CargoModelIndex.ToString(CultureInfo.InvariantCulture);
        string cargo = _session.CargoReference;
        string urgency = _session.Urgency.ToString(CultureInfo.InvariantCulture);
        if (UnitsInput == _lastUnits || UnitsInput == units) { _baseUnits = units; SetProperty(ref _unitsInput, units, nameof(UnitsInput)); }
        if (DistanceInput == _lastDistance || DistanceInput == distance) { _baseDistance = distance; SetProperty(ref _distanceInput, distance, nameof(DistanceInput)); }
        if (ModelInput == _lastModel || ModelInput == model) { _baseModel = model; SetProperty(ref _modelInput, model, nameof(ModelInput)); }
        if (SelectedCargo == _lastCargo || SelectedCargo == cargo) { _baseCargo = cargo; SetProperty(ref _selectedCargo, cargo, nameof(SelectedCargo)); }
        if (UrgencyInput == _lastUrgency || UrgencyInput == urgency) { _baseUrgency = urgency; SetProperty(ref _urgencyInput, urgency, nameof(UrgencyInput)); }
        _lastUnits = units;
        _lastDistance = distance;
        _lastModel = model;
        _lastCargo = cargo;
        _lastUrgency = urgency;
        OnPropertyChanged(nameof(Cargo));
        OnPropertyChanged(nameof(CargoChoices));
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(ChangeSummary));
        OnPropertyChanged(nameof(Route));
        NotifyDrafts();
    }

    private void NotifyDrafts()
    {
        OnPropertyChanged(nameof(IsUnitsStale));
        OnPropertyChanged(nameof(IsDistanceStale));
        OnPropertyChanged(nameof(IsModelStale));
        OnPropertyChanged(nameof(IsEitherStale));
        OnPropertyChanged(nameof(UnitsDraftNotice));
        OnPropertyChanged(nameof(DistanceDraftNotice));
        OnPropertyChanged(nameof(ModelDraftNotice));
        OnPropertyChanged(nameof(IsCargoStale));
        OnPropertyChanged(nameof(CargoDraftNotice));
        OnPropertyChanged(nameof(IsCargoDetailsStale));
        OnPropertyChanged(nameof(IsUrgencyStale));
        OnPropertyChanged(nameof(UrgencyDraftNotice));
    }
}
