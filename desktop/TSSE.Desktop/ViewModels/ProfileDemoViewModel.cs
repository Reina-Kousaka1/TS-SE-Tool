using System.Globalization;
using TSSE.Core.Editing;
using TSSE.Desktop.Services;

namespace TSSE.Desktop.ViewModels;

/// <summary>Scratch values only; this view model never opens or writes a game save.</summary>
public sealed class ProfileDemoViewModel : ObservableObject
{
    private readonly ProfileEditSession _session = new(new ProfileValues(0, 0));
    private readonly IUiTextCatalog _text;
    private string _balanceInput = "0";
    private string _experienceInput = "0";
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
    public string BalanceInput { get => _balanceInput; set => SetProperty(ref _balanceInput, value ?? string.Empty); }
    public string ExperienceInput { get => _experienceInput; set => SetProperty(ref _experienceInput, value ?? string.Empty); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public RelayCommand<string> ActionCommand { get; }

    private void Execute(string action)
    {
        bool success = action switch
        {
            "balance" => _session.TrySetBalance(BalanceInput),
            "experience" => _session.TrySetExperience(ExperienceInput),
            "undo" => _session.Undo(),
            "redo" => _session.Redo(),
            _ => false
        };
        Message = _text.Get(success ? "profileDemo.applied" : "profileDemo.invalid");
        if (success && action is "undo" or "redo")
        {
            BalanceInput = _session.Current.Balance.ToString(CultureInfo.InvariantCulture);
            ExperienceInput = _session.Current.Experience.ToString(CultureInfo.InvariantCulture);
        }
    }
}
