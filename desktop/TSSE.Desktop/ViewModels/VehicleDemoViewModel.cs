using System.Globalization;
using TSSE.Core.Editing;
using TSSE.Desktop.Services;
using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.Desktop.ViewModels;

/// <summary>Editable synthetic examples, with no file access.</summary>
public sealed class VehicleDemoViewModel : ObservableObject
{
    private const string Fixture = "SiiNunit\n{\n vehicle : demo.truck {\n  fuel_relative: 0.5\n  engine_wear: 0.2\n  transmission_wear: 0.1\n  cabin_wear: 0\n  chassis_wear: 0.3\n  wheels_wear: 2\n  wheels_wear[0]: 0.2\n  wheels_wear[1]: 0.1\n  demo_unknown: \"preserved\"\n }\n trailer : demo.trailer {\n  cargo_damage: 0.1\n  trailer_body_wear: 0.2\n  chassis_wear: 0.3\n  wheels_wear: 1\n  wheels_wear[0]: 0.4\n  demo_unknown: \"preserved\"\n }\n}\n";
    private readonly IUiTextCatalog _text;
    private readonly LegacyVehiclePreview _truck = new(SiiSyntaxDocument.Parse(Fixture), LegacyVehicleKind.Truck, "demo.truck");
    private readonly LegacyVehiclePreview _trailer = new(SiiSyntaxDocument.Parse(Fixture), LegacyVehicleKind.Trailer, "demo.trailer");
    private readonly IReadOnlyList<VehicleDemoField> _truckFields;
    private readonly IReadOnlyList<VehicleDemoField> _trailerFields;
    private bool _isTrailer;
    private VehicleDemoField _selectedField;
    private string _valueInput = "";
    private string _message;

    public VehicleDemoViewModel(IUiTextCatalog text)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
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
    public bool CanRefuel => !_isTrailer;
    public string PreviewLabel => _text.Get("vehicleDemo.preview");
    public string PreviewText => Session.PreviewText;
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
        _selectedField = Fields[0];
        OnPropertyChanged(nameof(SelectedField));
        OnPropertyChanged(nameof(PreviewText));
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
            OnPropertyChanged(nameof(PreviewText));
            RefreshValue();
        }
    }

    private void RefreshValue() => ValueInput = Session.GetValue(SelectedField.Name).ToString("R", CultureInfo.InvariantCulture);
    private IReadOnlyList<VehicleDemoField> CreateFields(string[] names) =>
        names.Select(name => new VehicleDemoField(name, _text.Get("vehicleDemo.field." + name))).ToArray();
}

public sealed record VehicleDemoField(string Name, string Label);
