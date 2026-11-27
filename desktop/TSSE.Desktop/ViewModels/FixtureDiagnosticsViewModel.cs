using System.Text;
using TSSE.Desktop.Services;
using TSSE.Savegames.Diagnostics;

namespace TSSE.Desktop.ViewModels;

/// <summary>Fixed in-memory examples only; does not accept paths or user save data.</summary>
public sealed class FixtureDiagnosticsViewModel : ObservableObject
{
    private readonly IUiTextCatalog _text;
    private FixtureDiagnosticsExample _selected;
    private SiiFixtureReport _report;

    public FixtureDiagnosticsViewModel(IUiTextCatalog text)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        Examples = new[] { "text", "opaque", "unclosed", "encrypted", "binary", "threenk" }
            .Select(key => new FixtureDiagnosticsExample(key, _text.Get("fixture.example." + key))).ToArray();
        _selected = Examples[0];
        _report = Inspect(_selected.Key);
    }

    public string Heading => _text.Get("fixture.heading");
    public string Description => _text.Get("fixture.description");
    public string SelectionLabel => _text.Get("fixture.selection");
    public IReadOnlyList<FixtureDiagnosticsExample> Examples { get; }
    public FixtureDiagnosticsExample Selected
    {
        get => _selected;
        set
        {
            if (value is null || !Examples.Contains(value) || value == _selected) return;
            _selected = value;
            _report = Inspect(value.Key);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(Signature));
            OnPropertyChanged(nameof(RoundTrip));
            OnPropertyChanged(nameof(Error));
        }
    }

    public string Status => _text.Format("fixture.status", _text.Get("fixture.status." + _report.Status));
    public string Signature => _text.Format("fixture.signature", _text.Get("fixture.signature." + _report.SignatureHint));
    public string RoundTrip => _text.Format("fixture.roundtrip", _text.Get(_report.ByteRoundTripEqual switch
    {
        true => "fixture.yes", false => "fixture.no", _ => "fixture.notAvailable"
    }));
    public string Error => _report.SyntaxError is null ? _text.Get("fixture.noError") :
        _text.Format("fixture.error", _report.SyntaxError, _report.ErrorLine ?? 0);

    private static SiiFixtureReport Inspect(string key) => SiiFixtureInspector.Inspect(Encoding.UTF8.GetBytes(key switch
    {
        "text" => "SiiNunit\n{\n demo : sample {\n  unknown_field: \"synthetic\"\n }\n}\n",
        "opaque" => "SiiNunit\n{\n demo : sample {\n  unknown syntax\n }\n}\n",
        "unclosed" => "SiiNunit\n{\n demo : sample {\n  value: 1\n",
        "encrypted" => "ScsC synthetic prefix only",
        "binary" => "BSII synthetic prefix only",
        "threenk" => "3nK\u0001synthetic prefix only",
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    }));
}

public sealed record FixtureDiagnosticsExample(string Key, string Label);
