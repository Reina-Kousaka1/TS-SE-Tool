using System.Globalization;

namespace TSSE.Core.Editing;

/// <summary>
/// A deliberately detached, in-memory profile editing exercise. The values are not
/// mapped to a game file and carry no assertion of game-version compatibility.
/// </summary>
public sealed class ProfileEditSession
{
    private readonly EditHistory<ProfileValues> history;

    public ProfileEditSession(ProfileValues initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        history = new EditHistory<ProfileValues>(initial, static _ => true);
    }

    public ProfileValues Current => history.Current;
    public bool IsDirty => history.IsDirty;
    public bool CanUndo => history.CanUndo;
    public bool CanRedo => history.CanRedo;

    /// <summary>Accepts a signed base-10 integer without grouping or whitespace.</summary>
    public bool TrySetBalance(string? input)
    {
        if (!long.TryParse(input, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
            return false;
        return history.TryApply(Current with { Balance = value });
    }

    /// <summary>Accepts an unsigned base-10 integer without grouping or whitespace.</summary>
    public bool TrySetExperience(string? input)
    {
        if (!uint.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out uint value))
            return false;
        return history.TryApply(Current with { Experience = value });
    }

    public bool Undo() => history.Undo();
    public bool Redo() => history.Redo();
}

/// <summary>Values for an isolated editor session, not a complete game profile.</summary>
public sealed record ProfileValues(long Balance, uint Experience);
