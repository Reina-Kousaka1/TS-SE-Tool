using System.Globalization;
using TSSE.Core.Editing;
using TSSE.Desktop.Services;
using TSSE.GameAdapters.Editing;

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
    public string ChangeSummary => DemoChangeSummary.Format(Session.Changes, _text.Get("editor.noChanges"));
    public IReadOnlyList<VehicleDemoField> Fields => _isTrailer ? _trailerFields : _truckFields;
    public RelayCommand<string> ActionCommand { get; }
    public string ValueInput { get => _valueInput; set => SetProperty(ref _valueInput, value ?? ""); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public VehicleDemoField SelectedField
    {
        get => _selectedField;
        set
        {
            if (value is not null && Fields.Contains(value) && SetProperty(ref _selectedField, value))
                RefreshValue();
        }
    }

    public void SelectCategory(string category)
    {
        if (category is not ("Trucks" or "Trailers")) return;
        bool trailer = category == "Trailers";
        if (_isTrailer == trailer) return;
        _isTrailer = trailer;
        OnPropertyChanged(nameof(Fields));
        OnPropertyChanged(nameof(CanRefuel));
        OnPropertyChanged(nameof(CanRepairChain));
        _selectedField = Fields[0];
        OnPropertyChanged(nameof(SelectedField));
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(ChangeSummary));
        Message = _text.Get("vehicleDemo.ready");
        RefreshValue();
    }

    private void Execute(string action)
    {
        bool success = false;
        try
        {
            if (action == "apply" && float.TryParse(ValueInput, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                Session.SetValue(SelectedField.Name, value);
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
            if (action == "apply") ValueInput = _lastValue;
        }
    }

    private void RefreshValue()
    {
        _lastValue = Session.GetValue(SelectedField.Name).ToString("R", CultureInfo.InvariantCulture);
        ValueInput = _lastValue;
    }

    private void RefreshFromWorkspace()
    {
        string current = Session.GetValue(SelectedField.Name).ToString("R", CultureInfo.InvariantCulture);
        if (ValueInput == _lastValue) ValueInput = current;
        _lastValue = current;
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(ChangeSummary));
    }
    private IReadOnlyList<VehicleDemoField> CreateFields(string[] names) =>
        names.Select(name => new VehicleDemoField(name, _text.Get("vehicleDemo.field." + name))).ToArray();
}

public sealed record VehicleDemoField(string Name, string Label);
