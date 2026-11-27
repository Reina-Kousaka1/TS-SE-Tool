namespace TSSE.Core.Editing;

/// <summary>
/// Tracks immutable editor snapshots in memory. It does not read or write save files.
/// Callers must supply immutable values and validate every proposed snapshot.
/// </summary>
public sealed class EditHistory<T> where T : notnull
{
    private readonly Func<T, bool> isValid;
    private readonly List<T> states;
    private int position;

    public EditHistory(T initial, Func<T, bool> isValid)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(isValid);
        if (!isValid(initial)) throw new ArgumentException("Invalid initial snapshot.", nameof(initial));
        this.isValid = isValid;
        states = [initial];
    }

    public T Current => states[position];
    public bool CanUndo => position > 0;
    public bool CanRedo => position + 1 < states.Count;
    /// <summary>True unless the current snapshot is the original session snapshot.</summary>
    public bool IsDirty => position != 0;

    public bool TryApply(T next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (!isValid(next)) return false;
        if (EqualityComparer<T>.Default.Equals(Current, next)) return true;
        if (CanRedo)
        {
            states.RemoveRange(position + 1, states.Count - position - 1);
        }
        states.Add(next);
        position++;
        return true;
    }

    public bool Undo()
    {
        if (!CanUndo) return false;
        position--;
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;
        position++;
        return true;
    }
}
