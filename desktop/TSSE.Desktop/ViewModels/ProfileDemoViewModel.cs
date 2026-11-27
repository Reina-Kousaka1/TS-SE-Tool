using System.Globalization;
using TSSE.Core.Editing;
using TSSE.Desktop.Services;
using TSSE.GameAdapters.Editing;

namespace TSSE.Desktop.ViewModels;

/// <summary>Scratch values only; this view model never opens or writes a game save.</summary>
public sealed class ProfileDemoViewModel : ObservableObject
{
    private readonly DemoWorkspace _workspace;
    private readonly LegacyProfilePreview _session;
    private readonly IUiTextCatalog _text;
    private string _balanceInput = "0";
    private string _experienceInput = "0";
    private string _levelInput = "0";
    private LegacyLevelTable _levelTable = LegacyLevelTable.Ets2;
    private string _message;
    private string _lastBalance = "0";
    private string _lastExperience = "0";

    public ProfileDemoViewModel(IUiTextCatalog text, DemoWorkspace? workspace = null)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _workspace = workspace ?? new DemoWorkspace();
        _session = _workspace.Workspace.Profile;
        _workspace.Changed += (_, _) => RefreshFromWorkspace();
        _message = _text.Get("profileDemo.ready");
        ActionCommand = new RelayCommand<string>(Execute);
    }

    public string Heading => _text.Get("profileDemo.heading");
    public string Description => _text.Get("profileDemo.description");
    public string BalanceLabel => _text.Get("profileDemo.balance");
    public string ExperienceLabel => _text.Get("profileDemo.experience");
    public string ApplyLabel => _text.Get("profileDemo.apply");
    public string UndoLabel => _text.Get("profileDemo.undo");
    public string RedoLabel => _text.Get("profileDemo.redo");
    public string PreviewLabel => _text.Get("profileDemo.preview");
    public string PreviewText => _session.PreviewText;
    public string ChangesLabel => _text.Get("editor.changes");
    public string SharedHistoryLabel => _text.Get("editor.sharedHistory");
    public string ChangeSummary => DemoChangeSummary.Format(_session.Changes, _text.Get("editor.noChanges"));
    public string LevelHeading => _text.Get("profileDemo.levelHeading");
    public string LevelDescription => _text.Get("profileDemo.levelDescription");
    public string LevelLabel => _text.Get("profileDemo.levelLabel");
    public string LevelProgress
    {
        get
        {
            LegacyLevelProgress progress = LegacyLevelCalculator.GetProgress(_levelTable, _session.Current.Experience);
            return _text.Format("profileDemo.levelProgress", _levelTable.ToString().ToUpperInvariant(), progress.Level, progress.NextLevelExperience);
        }
    }
    public string LevelInput { get => _levelInput; set => SetProperty(ref _levelInput, value ?? string.Empty); }
    public string BalanceInput { get => _balanceInput; set => SetProperty(ref _balanceInput, value ?? string.Empty); }
    public string ExperienceInput { get => _experienceInput; set => SetProperty(ref _experienceInput, value ?? string.Empty); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public RelayCommand<string> ActionCommand { get; }

    private void Execute(string action)
    {
        if (action is "ets2" or "ats")
        {
            _levelTable = action == "ets2" ? LegacyLevelTable.Ets2 : LegacyLevelTable.Ats;
            OnPropertyChanged(nameof(LevelProgress));
            return;
        }
        bool success;
        try
        {
            ProfileValues current = _session.Current;
            success = action switch
            {
                "balance" when long.TryParse(BalanceInput, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long balance)
                    => Apply(current with { Balance = balance }),
                "experience" when uint.TryParse(ExperienceInput, NumberStyles.None, CultureInfo.InvariantCulture, out uint experience)
                    => Apply(current with { Experience = experience }),
                "level" when int.TryParse(LevelInput, NumberStyles.None, CultureInfo.InvariantCulture, out int level) && level <= 150
                    => Apply(current with { Experience = LegacyLevelCalculator.ExperienceForLevelEnd(_levelTable, level) }),
                "undo" => _session.Undo(),
                "redo" => _session.Redo(),
                _ => false
            };
        }
        catch (EditHistoryLimitException)
        {
            Message = _text.Get("editor.historyLimit");
            return;
        }
        catch (InvalidOperationException)
        {
            success = false;
        }
        Message = _text.Get(success ? "profileDemo.applied" : "profileDemo.invalid");
        if (success)
        {
            _workspace.NotifyChanged();
            if (action == "level")
                ExperienceInput = _session.Current.Experience.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void RefreshFromWorkspace()
    {
        ProfileValues values = _session.Current;
        string balance = values.Balance.ToString(CultureInfo.InvariantCulture);
        string experience = values.Experience.ToString(CultureInfo.InvariantCulture);
        if (BalanceInput == _lastBalance) BalanceInput = balance;
        if (ExperienceInput == _lastExperience) ExperienceInput = experience;
        _lastBalance = balance;
        _lastExperience = experience;
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(ChangeSummary));
        OnPropertyChanged(nameof(LevelProgress));
    }

    private bool Apply(ProfileValues values)
    {
        _session.Apply(values);
        return true;
    }
}
