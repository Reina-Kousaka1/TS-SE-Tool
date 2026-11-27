using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacyVehiclePreviewTests
{
    private const string Synthetic = "SiiNunit\n{\n vehicle : truck {\n  fuel_relative: &3f000000\n  engine_wear: 0.25\n  mod_field: \"保持\"\n }\n trailer : trailer1 {\n  trailer_body_wear: 0.5\n  cargo_damage: 0.75\n  accessory: custom.ref\n }\n}\n";

    [Fact]
    public void TruckFuelEditPreservesUnknownFieldsAndSupportsUndoRedo()
    {
        var preview = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(Synthetic), LegacyVehicleKind.Truck, "truck");
        Assert.Equal(0.5f, preview.GetValue("fuel_relative"));
        preview.SetValue("fuel_relative", 0.5f);
        Assert.Equal(Synthetic, preview.PreviewText);
        preview.SetValue("fuel_relative", 1f);
        string changed = Synthetic.Replace("fuel_relative: &3f000000", "fuel_relative: 1", StringComparison.Ordinal);
        Assert.Equal(changed, preview.PreviewText);
        Assert.True(preview.Undo());
        Assert.Equal(Synthetic, preview.PreviewText);
        Assert.True(preview.Redo());
        Assert.Equal(changed, preview.PreviewText);
    }

    [Fact]
    public void TrailerEditDoesNotChangeTruckCargoOrAccessories()
    {
        var preview = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(Synthetic), LegacyVehicleKind.Trailer, "trailer1");
        Assert.Equal(0.5f, preview.GetValue("trailer_body_wear"));
        preview.SetValue("trailer_body_wear", 0);
        Assert.Equal(Synthetic.Replace("trailer_body_wear: 0.5", "trailer_body_wear: 0", StringComparison.Ordinal), preview.PreviewText);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    public void InvalidValuesLeavePreviewUnchanged(float value)
    {
        var preview = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(Synthetic), LegacyVehicleKind.Truck, "truck");
        Assert.Throws<ArgumentOutOfRangeException>(() => preview.SetValue("fuel_relative", value));
        Assert.Equal(Synthetic, preview.PreviewText);
        Assert.False(preview.IsDirty);
    }

    [Theory]
    [InlineData("&7f800000")]
    [InlineData("&7fc00000")]
    [InlineData("&xyz00000")]
    [InlineData("2")]
    public void UnsupportedOriginalValuesAreNotOverwritten(string raw)
    {
        string text = Synthetic.Replace("&3f000000", raw, StringComparison.Ordinal);
        var preview = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(text), LegacyVehicleKind.Truck, "truck");
        Assert.Throws<InvalidOperationException>(() => preview.SetValue("fuel_relative", 1));
        Assert.Equal(text, preview.PreviewText);
    }

    [Fact]
    public void MissingAmbiguousOrWrongCategoryFieldsFailClosed()
    {
        var truck = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(Synthetic), LegacyVehicleKind.Truck, "truck");
        Assert.Throws<ArgumentException>(() => truck.SetValue("cargo_damage", 0));
        Assert.Throws<InvalidOperationException>(() => truck.SetValue("cabin_wear", 0));
        string duplicate = Synthetic.Replace("engine_wear: 0.25", "engine_wear: 0.25\n  engine_wear: 0.2", StringComparison.Ordinal);
        var ambiguous = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(duplicate), LegacyVehicleKind.Truck, "truck");
        Assert.Throws<InvalidOperationException>(() => ambiguous.SetValue("engine_wear", 0));
        Assert.Equal(duplicate, ambiguous.PreviewText);
    }
}
