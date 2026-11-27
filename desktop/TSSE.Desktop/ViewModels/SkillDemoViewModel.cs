using System.Globalization;
using TSSE.Core.Editing;
using TSSE.Desktop.Services;
using TSSE.GameAdapters.Editing;

namespace TSSE.Desktop.ViewModels;

/// <summary>Synthetic profile skills only; no file or game-save access.</summary>
public sealed class SkillDemoViewModel : ObservableObject
{
    private readonly DemoWorkspace _workspace;
    private readonly LegacyProfilePreview _profile;
    private readonly IUiTextCatalog _text;
    private readonly Dictionary<string, (string Text, string Base)> _drafts = new(StringComparer.Ordinal);
    private SkillDemoField _selected;
    private string _input = "0";
    private string _last = "0";
    private string _base = "0";
    private string _message;

    public SkillDemoViewModel(DemoWorkspace workspace, IUiTextCatalog text)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _profile = workspace.Workspace.Profile;
        Fields = new[] { "adr", "long_dist", "heavy", "fragile", "urgent", "mechanical" }
            .Select(name => new SkillDemoField(name, _text.Get("skillDemo.field." + name))).ToArray();
        _selected = Fields[0];
        _last = _profile.GetSkill(_selected.Name).ToString(CultureInfo.InvariantCulture);
        _base = _input = _last;
        _message = _text.Get("skillDemo.ready");
        ActionCommand = new RelayCommand<string>(Execute);
        _workspace.Changed += (_, _) => Refresh();
    }

    public string Heading => _text.Get("skillDemo.heading");
    public string Description => _text.Get("skillDemo.description");
    public string FieldLabel => _text.Get("skillDemo.fieldLabel");
    public string ValueLabel => _text.Get(Selected.Name == "adr" ? "skillDemo.adrValue" : "skillDemo.levelValue");
    public string ApplyLabel => _text.Get("profileDemo.apply");
    public string ApplyReviewedLabel => _text.Get("editor.applyReviewed");
    public IReadOnlyList<SkillDemoField> Fields { get; }
    public RelayCommand<string> ActionCommand { get; }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public bool IsStale => Input != _last && _base != _last;
    public string DraftNotice => IsStale ? _text.Format("editor.staleDraft", _last) : "";
    public string Input { get => _input; set { if (_input == _last) _base = _last; if (SetProperty(ref _input, value ?? "")) NotifyDraft(); } }
    public SkillDemoField Selected
    {
        get => _selected;
        set
        {
            if (value is null || !Fields.Contains(value) || value == _selected) return;
            if (Input == _last) _drafts.Remove(_selected.Name);
            else _drafts[_selected.Name] = (Input, _base);
            _selected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ValueLabel));
            _last = _profile.GetSkill(value.Name).ToString(CultureInfo.InvariantCulture);
            if (_drafts.TryGetValue(value.Name, out var draft) && draft.Text != _last)
                SetInput(draft.Text, draft.Base);
            else
                SetInput(_last, _last);
        }
    }

    private void Execute(string action)
    {
        if (action == "apply" && IsStale) { Message = _text.Get("editor.staleBlocked"); return; }
        if (action is not ("apply" or "reviewed") ||
            !byte.TryParse(Input, NumberStyles.None, CultureInfo.InvariantCulture, out byte value))
        { Message = _text.Get("skillDemo.invalid"); return; }
        try
        {
            _profile.SetSkill(Selected.Name, value);
            _workspace.NotifyChanged();
            _drafts.Remove(Selected.Name);
            SetInput(_last, _last);
            Message = _text.Get("skillDemo.applied");
        }
        catch (EditHistoryLimitException) { Message = _text.Get("editor.historyLimit"); }
        catch (ArgumentOutOfRangeException) { Message = _text.Get("skillDemo.invalid"); }
        catch (InvalidOperationException) { Message = _text.Get("skillDemo.invalid"); }
    }

    private void Refresh()
    {
        string current = _profile.GetSkill(Selected.Name).ToString(CultureInfo.InvariantCulture);
        bool clean = Input == _last;
        _last = current;
        if (clean || Input == current) SetInput(current, current);
        else NotifyDraft();
    }

    private void SetInput(string value, string baseValue)
    {
        _base = baseValue;
        SetProperty(ref _input, value, nameof(Input));
        NotifyDraft();
    }

    private void NotifyDraft() { OnPropertyChanged(nameof(IsStale)); OnPropertyChanged(nameof(DraftNotice)); }
}

public sealed record SkillDemoField(string Name, string Label);
