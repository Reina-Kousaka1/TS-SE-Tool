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
    private string _baseBalance = "0";
    private string _baseExperience = "0";

    public ProfileDemoViewModel(IUiTextCatalog text, DemoWorkspace? workspace = null)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _workspace = workspace ?? new DemoWorkspace();
        _session = _workspace.Workspace.Profile;
        Skills = new SkillDemoViewModel(_workspace, _text);
        _workspace.Changed += (_, _) => RefreshFromWorkspace();
        _message = _text.Get("profileDemo.ready");
        ActionCommand = new RelayCommand<string>(Execute);
    }

    public string Heading => _text.Get("profileDemo.heading");
    public SkillDemoViewModel Skills { get; }
    public string Description => _text.Get("profileDemo.description");
    public string BalanceLabel => _text.Get("profileDemo.balance");
    public string ExperienceLabel => _text.Get("profileDemo.experience");
    public string ApplyLabel => _text.Get("profileDemo.apply");
    public string ApplyBothLabel => _text.Get("profileDemo.applyBoth");
    public string ApplyBothReviewedLabel => _text.Get("profileDemo.applyBothReviewed");
    public string DiscardBalanceLabel => _text.Get("profileDemo.discardBalance");
    public string DiscardExperienceLabel => _text.Get("profileDemo.discardExperience");
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
    public string BalanceInput { get => _balanceInput; set { if (_balanceInput == _lastBalance) _baseBalance = _lastBalance; if (SetProperty(ref _balanceInput, value ?? "")) NotifyDrafts(); } }
    public string ExperienceInput { get => _experienceInput; set { if (_experienceInput == _lastExperience) _baseExperience = _lastExperience; if (SetProperty(ref _experienceInput, value ?? "")) NotifyDrafts(); } }
    public bool IsBalanceStale => BalanceInput != _lastBalance && _baseBalance != _lastBalance;
    public bool IsExperienceStale => ExperienceInput != _lastExperience && _baseExperience != _lastExperience;
    public bool IsEitherStale => IsBalanceStale || IsExperienceStale;
    public string BalanceDraftNotice => IsBalanceStale ? _text.Format("editor.staleDraft", _lastBalance) : "";
    public string ExperienceDraftNotice => IsExperienceStale ? _text.Format("editor.staleDraft", _lastExperience) : "";
    public string ApplyReviewedLabel => _text.Get("editor.applyReviewed");
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public RelayCommand<string> ActionCommand { get; }

    private void Execute(string action)
    {
        if (action == "discard-balance")
        {
            _baseBalance = _lastBalance;
            SetProperty(ref _balanceInput, _lastBalance, nameof(BalanceInput));
            NotifyDrafts();
            Message = _text.Get("profileDemo.draftDiscarded");
            return;
        }
        if (action == "discard-experience")
        {
            _baseExperience = _lastExperience;
            SetProperty(ref _experienceInput, _lastExperience, nameof(ExperienceInput));
            NotifyDrafts();
            Message = _text.Get("profileDemo.draftDiscarded");
            return;
        }
        if (action is "ets2" or "ats")
        {
            _levelTable = action == "ets2" ? LegacyLevelTable.Ets2 : LegacyLevelTable.Ats;
            OnPropertyChanged(nameof(LevelProgress));
            return;
        }
        if ((action == "balance" && IsBalanceStale) || (action == "experience" && IsExperienceStale) ||
            (action == "both" && IsEitherStale))
        {
            Message = _text.Get("editor.staleBlocked");
            return;
        }
        bool success;
        try
        {
            ProfileValues current = _session.Current;
            success = action switch
            {
                "both" or "both-reviewed" when long.TryParse(BalanceInput, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long bothBalance) &&
                    uint.TryParse(ExperienceInput, NumberStyles.None, CultureInfo.InvariantCulture, out uint bothExperience)
                    => Apply(new ProfileValues(bothBalance, bothExperience)),
                "balance" or "balance-reviewed" when long.TryParse(BalanceInput, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long balance)
                    => Apply(current with { Balance = balance }),
                "experience" or "experience-reviewed" when uint.TryParse(ExperienceInput, NumberStyles.None, CultureInfo.InvariantCulture, out uint experience)
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
            if (action is "balance" or "balance-reviewed" or "both" or "both-reviewed") { _baseBalance = _lastBalance; SetProperty(ref _balanceInput, _lastBalance, nameof(BalanceInput)); }
            if (action is "experience" or "experience-reviewed" or "both" or "both-reviewed") { _baseExperience = _lastExperience; SetProperty(ref _experienceInput, _lastExperience, nameof(ExperienceInput)); }
            NotifyDrafts();
        }
    }

    private void RefreshFromWorkspace()
    {
        ProfileValues values = _session.Current;
        string balance = values.Balance.ToString(CultureInfo.InvariantCulture);
        string experience = values.Experience.ToString(CultureInfo.InvariantCulture);
        if (BalanceInput == _lastBalance || BalanceInput == balance) { _baseBalance = balance; SetProperty(ref _balanceInput, balance, nameof(BalanceInput)); }
        if (ExperienceInput == _lastExperience || ExperienceInput == experience) { _baseExperience = experience; SetProperty(ref _experienceInput, experience, nameof(ExperienceInput)); }
        _lastBalance = balance;
        _lastExperience = experience;
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(ChangeSummary));
        OnPropertyChanged(nameof(LevelProgress));
        NotifyDrafts();
    }

    private void NotifyDrafts()
    {
        OnPropertyChanged(nameof(IsBalanceStale));
        OnPropertyChanged(nameof(IsExperienceStale));
        OnPropertyChanged(nameof(IsEitherStale));
        OnPropertyChanged(nameof(BalanceDraftNotice));
        OnPropertyChanged(nameof(ExperienceDraftNotice));
    }

    private bool Apply(ProfileValues values)
    {
        _session.Apply(values);
        return true;
    }
}
