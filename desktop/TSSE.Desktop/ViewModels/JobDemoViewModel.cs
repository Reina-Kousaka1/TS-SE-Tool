using System.Globalization;
using TSSE.Core.Editing;
using TSSE.Desktop.Services;
using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.Desktop.ViewModels;

/// <summary>In-memory synthetic example; never opens or saves a file.</summary>
public sealed class JobDemoViewModel : ObservableObject
{
    private const string Fixture = "SiiNunit\n{\n player : demo.player {\n  current_job: demo.job\n }\n player_job : demo.job {\n  cargo: cargo.synthetic\n  units_count: 3\n  planned_distance_km: 120\n  demo_unknown: \"preserved\"\n }\n}\n";
    private readonly IUiTextCatalog _text;
    private readonly LegacyJobPreview _session = new(SiiSyntaxDocument.Parse(Fixture), "demo.player");
    private string _unitsInput = "3";
    private string _distanceInput = "120";
    private string _message;

    public JobDemoViewModel(IUiTextCatalog text)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
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
            OnPropertyChanged(nameof(PreviewText));
            if (action is "apply" or "undo" or "redo")
                UnitsInput = _session.UnitsCount.ToString(CultureInfo.InvariantCulture);
            if (action is "distance" or "undo" or "redo")
                DistanceInput = _session.PlannedDistanceKm.ToString(CultureInfo.InvariantCulture);
        }
    }
}
