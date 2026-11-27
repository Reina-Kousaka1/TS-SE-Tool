using TSSE.Core.Editing;
using Xunit;

namespace TSSE.Core.Tests;

public sealed class ProfileEditSessionTests
{
    [Fact]
    public void EditsAreValidatedAtomicallyAndCanBeUndoneAndRedone()
    {
        var session = new ProfileEditSession(new ProfileValues(-4, 12));
        Assert.True(session.TrySetBalance(long.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.True(session.TrySetExperience("4294967295"));
        Assert.Equal(new ProfileValues(long.MinValue, uint.MaxValue), session.Current);
        Assert.False(session.TrySetExperience("4294967296"));
        Assert.False(session.TrySetBalance("9223372036854775808"));
        Assert.False(session.TrySetBalance(" 5"));
        Assert.False(session.TrySetExperience("-1"));
        Assert.Equal(new ProfileValues(long.MinValue, uint.MaxValue), session.Current);
        Assert.True(session.Undo());
        Assert.Equal(new ProfileValues(long.MinValue, 12), session.Current);
        Assert.True(session.Undo());
        Assert.False(session.IsDirty);
        Assert.True(session.Redo());
        Assert.True(session.TrySetExperience("42"));
        Assert.False(session.CanRedo);
        Assert.Equal(new ProfileValues(long.MinValue, 42), session.Current);
    }

    [Fact]
    public void NullEmptyAndNonAsciiDigitsAreRejected()
    {
        var session = new ProfileEditSession(new ProfileValues(0, 0));
        Assert.False(session.TrySetExperience(null));
        Assert.False(session.TrySetBalance(""));
        Assert.False(session.TrySetExperience("１２"));
        Assert.False(session.IsDirty);
    }
}
