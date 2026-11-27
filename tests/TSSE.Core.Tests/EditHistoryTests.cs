using TSSE.Core.Editing;
using Xunit;

namespace TSSE.Core.Tests;

public sealed class EditHistoryTests
{
    private sealed record Snapshot(string Name, long Balance);

    [Fact]
    public void InvalidEditDoesNotChangeHistory()
    {
        var history = new EditHistory<Snapshot>(new("Profil 🚛", 0), s => s.Balance >= 0);
        Assert.False(history.TryApply(new("Profil 🚛", -1)));
        Assert.Equal(0, history.Current.Balance);
        Assert.False(history.IsDirty);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void UndoRedoAndBranchingPreserveSnapshots()
    {
        var history = new EditHistory<Snapshot>(new("A", 0), s => s.Balance >= 0);
        Assert.True(history.TryApply(new("B", long.MaxValue)));
        Assert.True(history.IsDirty);
        Assert.True(history.Undo());
        Assert.False(history.IsDirty);
        Assert.True(history.Redo());
        Assert.Equal(long.MaxValue, history.Current.Balance);
        Assert.True(history.Undo());
        Assert.True(history.TryApply(new("C", 1)));
        Assert.False(history.CanRedo);
        Assert.Equal("C", history.Current.Name);
    }

    [Fact]
    public void ARejectedOrUnchangedEditDoesNotDiscardRedo()
    {
        var history = new EditHistory<Snapshot>(new("A", 0), s => s.Balance >= 0);
        Assert.True(history.TryApply(new("B", 5)));
        Assert.True(history.Undo());
        Assert.False(history.TryApply(new("bad", -1)));
        Assert.True(history.TryApply(new("A", 0)));
        Assert.True(history.CanRedo);
        Assert.True(history.Redo());
        Assert.Equal(new Snapshot("B", 5), history.Current);
    }
}
