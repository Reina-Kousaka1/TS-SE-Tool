using System.Globalization;
using TSSE.Core.Editing;
using TSSE.Desktop.Services;
using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.Desktop.ViewModels;

/// <summary>Scratch values only; this view model never opens or writes a game save.</summary>
public sealed class ProfileDemoViewModel : ObservableObject
{
    private const string SyntheticDocument =
        "SiiNunit\n{\n economy : demo.economy {\n  bank: demo.bank\n  experience_points: 0\n  demo_unknown: \"preserved\"\n }\n bank : demo.bank {\n  money_account: 0\n }\n}\n";
    private readonly LegacyProfilePreview _session = new(SiiSyntaxDocument.Parse(SyntheticDocument), "demo.economy");
    private readonly IUiTextCatalog _text;
    private string _balanceInput = "0";
    private string _experienceInput = "0";
    private string _levelInput = "0";
    private LegacyLevelTable _levelTable = LegacyLevelTable.Ets2;
    private string _message;

    public ProfileDemoViewModel(IUiTextCatalog text)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
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
        catch (InvalidOperationException)
        {
            success = false;
        }
        Message = _text.Get(success ? "profileDemo.applied" : "profileDemo.invalid");
        if (success)
        {
            OnPropertyChanged(nameof(PreviewText));
            OnPropertyChanged(nameof(LevelProgress));
            if (action == "level")
                ExperienceInput = _session.Current.Experience.ToString(CultureInfo.InvariantCulture);
        }
        if (success && action is "undo" or "redo")
        {
            BalanceInput = _session.Current.Balance.ToString(CultureInfo.InvariantCulture);
            ExperienceInput = _session.Current.Experience.ToString(CultureInfo.InvariantCulture);
        }
    }

    private bool Apply(ProfileValues values)
    {
        _session.Apply(values);
        return true;
    }
}
