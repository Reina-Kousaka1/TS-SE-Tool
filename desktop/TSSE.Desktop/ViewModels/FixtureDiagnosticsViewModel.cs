using System.Globalization;
using System.Text;
using TSSE.Desktop.Services;
using TSSE.GameAdapters.Compatibility;
using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Diagnostics;
using TSSE.Savegames.Syntax;

namespace TSSE.Desktop.ViewModels;

/// <summary>In-memory format examples and explicit read-only inspection results.</summary>
public sealed class FixtureDiagnosticsViewModel : ObservableObject
{
    private readonly IUiTextCatalog _text;
    private FixtureDiagnosticsExample _selected;
    private SiiFixtureReport _report;
    private string _importResult;

    public FixtureDiagnosticsViewModel(IUiTextCatalog text)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        Examples = new[] { "text", "legacy-info", "coaches", "opaque", "duplicates", "unclosed", "unexpected-close", "encrypted", "binary", "threenk" }
            .Select(key => new FixtureDiagnosticsExample(key, _text.Get("fixture.example." + key))).ToArray();
        _selected = Examples[0];
        _report = Inspect(_selected.Key);
        _importResult = _text.Get("fixture.import.none");
    }

    public string Heading => _text.Get("fixture.heading");
    public string Description => _text.Get("fixture.description");
    public string ImportLabel => _text.Get("fixture.import.button");
    public string ImportResult => _importResult;
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
            OnPropertyChanged(nameof(BinaryHeader));
            OnPropertyChanged(nameof(RoundTrip));
            OnPropertyChanged(nameof(Error));
            OnPropertyChanged(nameof(Duplicates));
            OnPropertyChanged(nameof(LegacyVersion));
            OnPropertyChanged(nameof(CoachSummary));
        }
    }

    public string Status => _text.Format("fixture.status", _text.Get("fixture.status." + _report.Status));
    public string Signature => _text.Format("fixture.signature", _text.Get("fixture.signature." + _report.SignatureHint));
    public string BinaryHeader => _report.BinaryFormatVersion is uint version
        ? _text.Format("fixture.binaryHeaderVersion", version, _text.Get("fixture.binaryHeader." + _report.BinaryHeader))
        : _text.Get("fixture.binaryHeader." + _report.BinaryHeader);
    public string RoundTrip => _text.Format("fixture.roundtrip", _text.Get(_report.ByteRoundTripEqual switch
    {
        true => "fixture.yes", false => "fixture.no", _ => "fixture.notAvailable"
    }));
    public string Error => _report.SyntaxError is null ? _text.Get("fixture.noError") :
        _report.ErrorColumn is int column
            ? _text.Format("fixture.errorColumn", _report.SyntaxError, _report.ErrorLine ?? 0, column)
            : _text.Format("fixture.error", _report.SyntaxError, _report.ErrorLine ?? 0);
    public string Duplicates => _report.DuplicateBlockIdOccurrences is int ids && _report.DuplicateFieldOccurrences is int fields
        ? _text.Format(_report.DuplicateInspectionComplete == false ? "fixture.duplicatesIncomplete" : "fixture.duplicates", ids, fields)
        : _text.Get("fixture.duplicatesUnavailable");
    public string LegacyVersion
    {
        get
        {
            if (Selected.Key != "legacy-info") return _text.Get("fixture.versionUnavailable");
            var result = LegacySaveVersionInspector.Inspect(SiiSyntaxDocument.Parse(ExampleText(Selected.Key)));
            return result.Status == LegacySaveVersionStatus.LegacyFieldFound
                ? _text.Format("fixture.versionValue", result.Version!)
                : _text.Get("fixture.versionUnavailable");
        }
    }

    public string CoachSummary
    {
        get
        {
            if (Selected.Key != "coaches") return _text.Get("fixture.coach.unavailable");
            LegacyCoachEconomySnapshot sample = new LegacyCoachProfileInspector(
                SiiSyntaxDocument.Parse(ExampleText(Selected.Key)), "coach-economy").Snapshot;
            string xp = sample.Counters["bus_experience_points"]?.ToString(CultureInfo.InvariantCulture)
                ?? _text.Get("fixture.coach.metricUnavailable");
            string stops = sample.Stops.Count.ToString(CultureInfo.InvariantCulture);
            string entries = sample.JobLogEntryCount?.ToString(CultureInfo.InvariantCulture)
                ?? _text.Get("fixture.coach.metricUnavailable");
            string busCount = sample.Player?.BusIds.Count.ToString(CultureInfo.InvariantCulture)
                ?? _text.Get("fixture.coach.metricUnavailable");
            string jobState = sample.Player?.CurrentBusJob?.State.ToString().ToLowerInvariant()
                ?? _text.Get("fixture.coach.metricUnavailable");
            string myVehiclesMode = sample.Player?.MyVehiclesMode ?? _text.Get("fixture.coach.metricUnavailable");
            string assignedVehiclesMode = sample.Player?.AssignedVehiclesMode ?? _text.Get("fixture.coach.metricUnavailable");
            string coachSkills = FormatCandidateCoachSkills(sample.CoachSkills);
            return _text.Format("fixture.coach.summary", xp, stops, entries, busCount, jobState, myVehiclesMode, assignedVehiclesMode, coachSkills);
        }
    }

    /// <summary>Reports format decoding for bytes explicitly selected by the user. Never writes or retains input bytes.</summary>
    public void InspectSelectedBytes(ReadOnlySpan<byte> bytes)
    {
        try
        {
            using SiiDecodedSave decoded = SiiSaveContainerDecoder.Decode(bytes);
            string format = _text.Get("fixture.import.format." + decoded.Format);
            int blocks = decoded.TextDocument?.Blocks.Count ?? decoded.BinaryBlocks?.Count ?? 0;
            string container = decoded.ContainerAuthenticationUnverified
                ? _text.Get("fixture.import.container.encrypted")
                : decoded.ThreeNkDecoded ? _text.Get("fixture.import.container.3nk")
                : _text.Get("fixture.import.container.plain");
            string profile = _text.Get("fixture.import.profile.unavailable");
            if (decoded.TextDocument is not null)
            {
                try
                {
                    var values = LegacyProfileReadInspector.InspectText(decoded.TextDocument);
                    profile = _text.Format("fixture.import.profile.values", values.Balance, values.Experience,
                        values.CoachExperience?.ToString(CultureInfo.InvariantCulture) ?? _text.Get("fixture.import.profile.noCoachField"));
                }
                catch (InvalidDataException)
                {
                    // Format decoding is still useful; profile mapping is deliberately fail-closed.
                }
                profile += " " + InspectCoachFields(decoded.TextDocument);
            }
            else if (decoded.Format == SiiPayloadFormat.Binary)
            {
                try
                {
                    var values = LegacyProfileReadInspector.InspectBinary(decoded.BinaryBlocks!);
                    profile = _text.Format("fixture.import.profile.values", values.Balance, values.Experience,
                        values.CoachExperience?.ToString(CultureInfo.InvariantCulture) ?? _text.Get("fixture.import.profile.noCoachField"));
                }
                catch (InvalidDataException)
                {
                    // Format decoding is still useful; profile mapping is deliberately fail-closed.
                }
                profile += " " + InspectBinaryCoachCounters(decoded.BinaryBlocks!);
            }
            SetImportResult(_text.Format("fixture.import.success", format, container, blocks) + " " + profile);
        }
        catch (SiiSaveContainerException ex)
        {
            SetImportResult(_text.Format("fixture.import.failure", _text.Get("fixture.import.error." + ex.Error)));
        }
        catch (ExperimentalBinarySiiException)
        {
            SetImportResult(_text.Format("fixture.import.failure", _text.Get("fixture.import.error.UnsupportedPayload")));
        }
        catch (Exception ex) when (ex is InvalidDataException or SiiSyntaxException or ArgumentException)
        {
            SetImportResult(_text.Get("fixture.import.failure.generic"));
        }
    }

    public void ReportImportReadFailure() => SetImportResult(_text.Get("fixture.import.readFailure"));
    public void ReportImportTooLarge() => SetImportResult(_text.Get("fixture.import.tooLarge"));

    private void SetImportResult(string result)
    {
        _importResult = result;
        OnPropertyChanged(nameof(ImportResult));
    }

    private string InspectCoachFields(SiiSyntaxDocument document)
    {
        SiiBlock[] economies = document.Blocks.Where(block => block.Type == "economy").ToArray();
        if (economies.Length != 1 || string.IsNullOrWhiteSpace(economies[0].Id))
            return _text.Get("fixture.import.coach.unavailable");
        int fields = economies[0].Fields.Count(field => field.Name.StartsWith("bus_", StringComparison.Ordinal));
        if (fields == 0) return _text.Get("fixture.import.coach.unavailable");
        try
        {
            LegacyCoachEconomySnapshot snapshot = new LegacyCoachProfileInspector(document, economies[0].Id!).Snapshot;
            int metrics = snapshot.Counters.Count(pair => pair.Value.HasValue);
            int coachSkills = snapshot.CoachSkills.Count(pair => pair.Value.HasValue);
            return _text.Format("fixture.import.coach.historical", metrics, snapshot.Stops.Count,
                snapshot.JobLogEntryCount?.ToString(CultureInfo.InvariantCulture) ?? _text.Get("fixture.import.profile.noCoachField"),
                coachSkills, FormatCandidateCoachSkills(snapshot.CoachSkills));
        }
        catch (InvalidOperationException)
        {
            return _text.Get("fixture.import.coach.unavailable");
        }
    }

    private string FormatCandidateCoachSkills(IReadOnlyDictionary<string, int?> skills)
    {
        string summary = string.Join(", ", LegacyCoachSkillCatalog.FieldNames
            .Where(name => skills[name].HasValue)
            .Select(name => $"{name}={skills[name]!.Value.ToString(CultureInfo.InvariantCulture)}"));
        return summary.Length == 0 ? _text.Get("fixture.coach.skills.none") : summary;
    }

    private string FormatCandidateCoachSkillTypes(IReadOnlyDictionary<string, uint?> types)
    {
        string summary = string.Join(", ", LegacyCoachSkillCatalog.FieldNames
            .Where(name => types[name].HasValue)
            .Select(name => $"{name}=0x{types[name]!.Value:x8}"));
        return summary.Length == 0 ? _text.Get("fixture.coach.skills.none") : summary;
    }

    private string InspectBinaryCoachCounters(IReadOnlyList<ExperimentalBinaryBlock> blocks)
    {
        try
        {
            IReadOnlyDictionary<string, long?> counters = LegacyCoachProfileInspector.InspectBinaryCounters(blocks);
            string Number(string name) => counters[name]?.ToString(CultureInfo.InvariantCulture)
                ?? _text.Get("fixture.import.profile.noCoachField");
            string countersText = _text.Format("fixture.import.coach.binaryCounters",
                Number("bus_experience_points"), Number("bus_total_distance"), Number("bus_finished_job_count"),
                Number("bus_cancelled_job_count"), Number("bus_total_passengers"), Number("bus_total_stops"),
                Number("bus_game_time"), Number("bus_playing_time"));
            IReadOnlyDictionary<string, uint?> types = LegacyCoachProfileInspector.InspectBinaryCandidateSkillTypeCodes(blocks);
            return countersText + " " + _text.Format("fixture.import.coach.binarySkillTypes", FormatCandidateCoachSkillTypes(types));
        }
        catch (InvalidOperationException)
        {
            return _text.Get("fixture.import.coach.unavailable");
        }
    }

    private static SiiFixtureReport Inspect(string key) => SiiFixtureInspector.Inspect(Encoding.UTF8.GetBytes(ExampleText(key)));
    private static string ExampleText(string key) => key switch
    {
        "text" => "SiiNunit\n{\n demo : sample {\n  unknown_field: \"synthetic\"\n }\n}\n",
        "legacy-info" => "SiiNunit\n{\n save_container : demo {\n  version: 55\n  unknown_field: \"synthetic\"\n }\n}\n",
        "coaches" => "SiiNunit\n{\n economy : coach-economy {\n  bus_experience_points: 4321\n  long_duty: 2\n  tight_schedule: 1\n  satisfaction_decrease: 0\n  low_acceleration: 3\n  satisfaction_increase: 1\n  ecodriving: 2\n  bus_stops: 1\n  bus_stops[0]: coach-stop-sample\n  bus_job_log: coach-log-sample\n  player: coach-player-sample\n }\n player : coach-player-sample {\n  buses: 1\n  buses[0]: coach-vehicle-sample\n  current_bus_job: coach-job-sample\n  my_vehicles_mode: truck\n  assigned_vehicles_mode: coach\n }\n vehicle : coach-vehicle-sample {\n  future_coach_field: preserved\n }\n bus_job : coach-job-sample {\n  future_job_field: preserved\n }\n bus_stop : coach-stop-sample {\n  discovered: true\n  lines_offer: 1\n  lines_offer[0]: coach-line-sample\n }\n bus_job_log : coach-log-sample {\n  version: 1\n  entries: 1\n  entries[0]: synthetic-entry\n }\n}\n",
        "opaque" => "SiiNunit\n{\n demo : sample {\n  unknown syntax\n }\n}\n",
        "duplicates" => "SiiNunit\n{\n demo : sample {\n  value: 1\n  value: 2\n }\n other : sample {\n }\n}\n",
        "unclosed" => "SiiNunit\n{\n demo : sample {\n  value: 1\n",
        "unexpected-close" => "}",
        "encrypted" => "ScsC synthetic prefix only",
        "binary" => "BSII\u0002\u0000\u0000\u0000",
        "threenk" => "3nK\u0001synthetic prefix only",
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };
}

public sealed record FixtureDiagnosticsExample(string Key, string Label);
