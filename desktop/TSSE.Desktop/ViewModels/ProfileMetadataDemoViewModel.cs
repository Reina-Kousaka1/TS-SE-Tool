using TSSE.Desktop.Services;
using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.Desktop.ViewModels;

/// <summary>Isolated example of profile metadata, never a real profile or file.</summary>
public sealed class ProfileMetadataDemoViewModel : ObservableObject
{
    private readonly IUiTextCatalog _text;
    private readonly LegacyProfileMetadataPreview _preview = new(SiiSyntaxDocument.Parse(
        "SiiNunit\n{\n user_profile : demo.metadata {\n  profile_name: Example_1\n  cached_experience: 100\n  cached_distance: 200\n  unknown_mod: \"preserved\"\n }\n}\n"), "demo.metadata");
    private string _input = "Example_1";
    private string _lastName = "Example_1";
    private string _baseName = "Example_1";
    private string _message;

    public ProfileMetadataDemoViewModel(IUiTextCatalog text)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _message = _text.Get("profileMetadata.ready");
        ActionCommand = new RelayCommand<string>(Execute);
    }

    public string Heading => _text.Get("profileMetadata.heading");
    public string Description => _text.Get("profileMetadata.description");
    public string NameLabel => _text.Get("profileMetadata.name");
    public string ApplyLabel => _text.Get("profileMetadata.apply");
    public string UndoLabel => _text.Get("profileDemo.undo");
    public string RedoLabel => _text.Get("profileDemo.redo");
    public string ApplyReviewedLabel => _text.Get("editor.applyReviewed");
    public string DiscardDraftLabel => _text.Get("profileMetadata.discardDraft");
    public string CachedValues => _text.Format("profileMetadata.cached", _preview.CachedExperience, _preview.CachedDistance);
    public string CurrentName => _text.Format("profileMetadata.current", _preview.Name);
    public string Input { get => _input; set { if (_input == _lastName) _baseName = _lastName; if (SetProperty(ref _input, value ?? "")) NotifyDraft(); } }
    public bool IsStale => Input != _lastName && _baseName != _lastName;
    public string DraftNotice => IsStale ? _text.Format("editor.staleDraft", _lastName) : "";
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public RelayCommand<string> ActionCommand { get; }

    private void Execute(string action)
    {
        if (action == "discard-draft")
        {
            _baseName = _lastName;
            SetProperty(ref _input, _lastName, nameof(Input));
            NotifyDraft();
            Message = _text.Get("profileMetadata.draftDiscarded");
            return;
        }
        if (action == "apply" && IsStale)
        {
            Message = _text.Get("editor.staleBlocked");
            return;
        }
        bool success;
        try
        {
            success = action switch
            {
                "apply" or "apply-reviewed" => Apply(),
                "undo" => _preview.Undo(),
                "redo" => _preview.Redo(),
                _ => false
            };
        }
        catch (ArgumentException) { success = false; }
        catch (InvalidOperationException) { success = false; }
        Message = _text.Get(success ? "profileMetadata.applied" : "profileMetadata.invalid");
        if (!success) return;
        string current = _preview.Name;
        if (action is "apply" or "apply-reviewed" || Input == _lastName || Input == current)
        {
            _baseName = current;
            SetProperty(ref _input, current, nameof(Input));
        }
        _lastName = current;
        OnPropertyChanged(nameof(CurrentName));
        NotifyDraft();
    }

    private bool Apply() { _preview.SetName(Input); return true; }
    private void NotifyDraft()
    {
        OnPropertyChanged(nameof(IsStale));
        OnPropertyChanged(nameof(DraftNotice));
    }
}
