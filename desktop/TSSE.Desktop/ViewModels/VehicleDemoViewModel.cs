using System.Globalization;
using TSSE.Core.Editing;
using TSSE.Desktop.Services;
using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.Desktop.ViewModels;

/// <summary>Editable synthetic examples, with no file access.</summary>
public sealed class VehicleDemoViewModel : ObservableObject
{
    private readonly DemoWorkspace _workspace;
    private readonly IUiTextCatalog _text;
    private readonly LegacyVehiclePreview _truck;
    private readonly LegacyVehiclePreview _trailer;
    private readonly IReadOnlyList<VehicleDemoField> _truckFields;
    private readonly IReadOnlyList<VehicleDemoField> _trailerFields;
    private bool _isTrailer;
    private VehicleDemoField _selectedField;
    private string _valueInput = "";
    private string _message;
    private string _lastValue = "";
    private string _inputBase = "";
    private string _odometerInput = "12345";
    private string _lastOdometer = "12345";
    private string _odometerBase = "12345";
    private readonly Dictionary<(bool Trailer, string Field), (string Text, string Base)> _drafts = new();

    public VehicleDemoViewModel(IUiTextCatalog text, DemoWorkspace? workspace = null)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _workspace = workspace ?? new DemoWorkspace();
        _truck = _workspace.Workspace.Truck;
        _trailer = _workspace.Workspace.Trailer;
        _workspace.Changed += (_, _) => RefreshFromWorkspace();
        _truckFields = CreateFields(["fuel_relative", "engine_wear", "transmission_wear", "cabin_wear", "chassis_wear"]);
        _trailerFields = CreateFields(["cargo_damage", "trailer_body_wear", "chassis_wear"]);
        _selectedField = _truckFields[0];
        _message = _text.Get("vehicleDemo.ready");
        ActionCommand = new RelayCommand<string>(Execute);
        RefreshValue();
    }

    private LegacyVehiclePreview Session => _isTrailer ? _trailer : _truck;
    public string Heading => _text.Get("vehicleDemo.heading");
    public string Description => _text.Get("vehicleDemo.description");
    public string FieldLabel => _text.Get("vehicleDemo.field");
    public string ValueLabel => _text.Get("vehicleDemo.value");
    public string ApplyLabel => _text.Get("vehicleDemo.apply");
    public string OdometerLabel => _text.Get("vehicleDemo.odometer");
    public string OdometerDetail => _text.Get("vehicleDemo.odometerDetail");
    public string OdometerInput { get => _odometerInput; set { if (_odometerInput == _lastOdometer) _odometerBase = _lastOdometer; if (SetProperty(ref _odometerInput, value ?? "")) NotifyOdometerDraft(); } }
    public bool IsOdometerStale => OdometerInput != _lastOdometer && _odometerBase != _lastOdometer;
    public string OdometerDraftNotice => IsOdometerStale ? _text.Format("vehicleDemo.staleDraft", _lastOdometer) : "";
    public string UndoLabel => _text.Get("vehicleDemo.undo");
    public string RedoLabel => _text.Get("vehicleDemo.redo");
    public string RepairLabel => _text.Get("vehicleDemo.repair");
    public string RepairDetail => _text.Get("vehicleDemo.repairDetail");
    public string RefuelLabel => _text.Get("vehicleDemo.refuel");
    public string WheelRepairLabel => _text.Get("vehicleDemo.repairWheels");
    public string ChainRepairLabel => _text.Get("vehicleDemo.repairChain");
    public bool CanRepairChain => _isTrailer;
    public bool CanRefuel => !_isTrailer;
    public string PreviewLabel => _text.Get("vehicleDemo.preview");
    public string PreviewText => Session.PreviewText;
    public string ChangesLabel => _text.Get("editor.changes");
    public string SharedHistoryLabel => _text.Get("editor.sharedHistory");
    public string AccessoryHeading => _text.Get("accessory.heading");
    public string AccessoryDescription => _text.Get("accessory.description");
    public string AccessorySummary
    {
        get
        {
            try
            {
                var document = SiiSyntaxDocument.Parse(Session.PreviewText);
                var entries = LegacyAccessoryInspector.InspectDeclaredArray(document,
                    _isTrailer ? "trailer" : "vehicle", _isTrailer ? "demo.trailer" : "demo.truck");
                return entries.Count == 0 ? _text.Get("accessory.emptyList") :
                    string.Join(Environment.NewLine, entries.Select(entry =>
                        _text.Format("accessory.row", entry.Index, entry.RawReference,
                            _text.Get("accessory.state." + entry.State), entry.TargetBlockType ?? "—")));
            }
            catch (InvalidOperationException)
            {
                return _text.Get("accessory.unavailable");
            }
        }
    }
    public string ChangeSummary => DemoChangeSummary.Format(Session.Changes, _text.Get("editor.noChanges"));
    public string DraftNotice => IsStaleDraft ? _text.Format("vehicleDemo.staleDraft", _lastValue) : "";
    public string ApplyStaleLabel => _text.Get("vehicleDemo.applyStale");
    public bool IsStaleDraft => ValueInput != _lastValue && _inputBase != _lastValue;
    public IReadOnlyList<VehicleDemoField> Fields => _isTrailer ? _trailerFields : _truckFields;
    public RelayCommand<string> ActionCommand { get; }
    public string ValueInput
    {
        get => _valueInput;
        set
        {
            if (_valueInput == _lastValue) _inputBase = _lastValue;
            if (SetProperty(ref _valueInput, value ?? "")) NotifyDraftStatus();
        }
    }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public VehicleDemoField SelectedField
    {
        get => _selectedField;
        set
        {
            if (value is null || !Fields.Contains(value) || value == _selectedField) return;
            SaveDraft();
            if (SetProperty(ref _selectedField, value)) LoadField();
        }
    }

    public void SelectCategory(string category)
    {
        if (category is not ("Trucks" or "Trailers")) return;
        bool trailer = category == "Trailers";
        if (_isTrailer == trailer) return;
        SaveDraft();
        _isTrailer = trailer;
        _selectedField = Fields[0];
        OnPropertyChanged(nameof(Fields));
        OnPropertyChanged(nameof(CanRefuel));
        OnPropertyChanged(nameof(CanRepairChain));
        OnPropertyChanged(nameof(SelectedField));
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(ChangeSummary));
        OnPropertyChanged(nameof(AccessorySummary));
        Message = _text.Get("vehicleDemo.ready");
        LoadField();
    }

    private void Execute(string action)
    {
        bool success = false;
        try
        {
            if (action == "apply" && IsStaleDraft)
            {
                Message = _text.Get("vehicleDemo.staleBlocked");
                return;
            }
            if (action == "odometer" && IsOdometerStale)
            {
                Message = _text.Get("vehicleDemo.staleBlocked");
                return;
            }
            if ((action is "apply" or "apply-stale") && float.TryParse(ValueInput, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                Session.SetValue(SelectedField.Name, value);
                success = true;
            }
            else if ((action is "odometer" or "odometer-reviewed") && CanRefuel &&
                uint.TryParse(OdometerInput, NumberStyles.None, CultureInfo.InvariantCulture, out uint odometer))
            {
                _truck.SetOdometer(odometer);
                success = true;
            }
            else if (action == "undo") success = Session.Undo();
            else if (action == "redo") success = Session.Redo();
            else if (action == "repair")
            {
                Session.RepairKnownBodyFields();
                success = true;
            }
            else if (action == "wheels")
            {
                Session.RepairWheelWear();
                success = true;
            }
            else if (action == "chain" && CanRepairChain)
            {
                Session.RepairTrailerChainBody();
                success = true;
            }
            else if (action == "refuel" && CanRefuel)
            {
                Session.SetValue("fuel_relative", 1);
                success = true;
            }
        }
        catch (EditHistoryLimitException)
        {
            Message = _text.Get("editor.historyLimit");
            return;
        }
        catch (ArgumentOutOfRangeException) { }
        catch (InvalidOperationException) { }
        Message = _text.Get(success ? "vehicleDemo.applied" : "vehicleDemo.invalid");
        if (success)
        {
            _workspace.NotifyChanged();
            if (action is "apply" or "apply-stale")
            {
                _drafts.Remove((_isTrailer, SelectedField.Name));
                SetInput(_lastValue, _lastValue);
            }
            if (action is "odometer" or "odometer-reviewed")
            {
                _odometerBase = _lastOdometer;
                SetProperty(ref _odometerInput, _lastOdometer, nameof(OdometerInput));
                NotifyOdometerDraft();
            }
        }
    }

    private void RefreshValue()
    {
        _lastValue = Session.GetValue(SelectedField.Name).ToString("R", CultureInfo.InvariantCulture);
        SetInput(_lastValue, _lastValue);
    }

    private void RefreshFromWorkspace()
    {
        string currentOdometer = _truck.Odometer.ToString(CultureInfo.InvariantCulture);
        bool cleanOdometer = OdometerInput == _lastOdometer;
        _lastOdometer = currentOdometer;
        if (cleanOdometer || OdometerInput == currentOdometer)
        {
            _odometerBase = currentOdometer;
            SetProperty(ref _odometerInput, currentOdometer, nameof(OdometerInput));
        }
        NotifyOdometerDraft();
        string current = Session.GetValue(SelectedField.Name).ToString("R", CultureInfo.InvariantCulture);
        bool clean = ValueInput == _lastValue;
        _lastValue = current;
        if (clean || ValueInput == current) SetInput(current, current);
        else NotifyDraftStatus();
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(ChangeSummary));
        OnPropertyChanged(nameof(AccessorySummary));
    }

    private void SaveDraft()
    {
        var key = (_isTrailer, SelectedField.Name);
        if (ValueInput == _lastValue) _drafts.Remove(key);
        else _drafts[key] = (ValueInput, _inputBase);
    }

    private void LoadField()
    {
        _lastValue = Session.GetValue(SelectedField.Name).ToString("R", CultureInfo.InvariantCulture);
        if (_drafts.TryGetValue((_isTrailer, SelectedField.Name), out var draft) && draft.Text != _lastValue)
            SetInput(draft.Text, draft.Base);
        else
            SetInput(_lastValue, _lastValue);
    }

    private void SetInput(string value, string baseValue)
    {
        _inputBase = baseValue;
        SetProperty(ref _valueInput, value, nameof(ValueInput));
        NotifyDraftStatus();
    }

    private void NotifyDraftStatus()
    {
        OnPropertyChanged(nameof(IsStaleDraft));
        OnPropertyChanged(nameof(DraftNotice));
    }
    private void NotifyOdometerDraft()
    {
        OnPropertyChanged(nameof(IsOdometerStale));
        OnPropertyChanged(nameof(OdometerDraftNotice));
    }
    private IReadOnlyList<VehicleDemoField> CreateFields(string[] names) =>
        names.Select(name => new VehicleDemoField(name, _text.Get("vehicleDemo.field." + name))).ToArray();
}

public sealed record VehicleDemoField(string Name, string Label);
