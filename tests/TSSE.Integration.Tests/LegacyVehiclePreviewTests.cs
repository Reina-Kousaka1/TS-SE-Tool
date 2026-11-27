using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;
using Xunit;

namespace TSSE.Integration.Tests;

public sealed class LegacyVehiclePreviewTests
{
    [Fact]
    public void OdometerEditPreservesFractionAndUnknownDataAndCanBeUndone()
    {
        const string input = "vehicle : t {\n odometer: 0012\n odometer_float_part: &3f000000\n mod: \"未知\"\n}\n";
        var preview = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(input), LegacyVehicleKind.Truck, "t");
        Assert.Equal(12u, preview.Odometer);
        preview.SetOdometer(12);
        Assert.Equal(input, preview.PreviewText);
        preview.SetOdometer(uint.MaxValue);
        string changed = input.Replace("odometer: 0012", "odometer: 4294967295", StringComparison.Ordinal);
        Assert.Equal(changed, preview.PreviewText);
        Assert.True(preview.Undo());
        Assert.Equal(input, preview.PreviewText);
        Assert.True(preview.Redo());
        Assert.Equal(changed, preview.PreviewText);
    }

    [Theory]
    [InlineData("odometer: -1")]
    [InlineData("odometer: 4294967296")]
    [InlineData("odometer: 1\n odometer: 2")]
    [InlineData("other: 1")]
    public void InvalidOrAmbiguousOdometerCannotBeOverwritten(string field)
    {
        string input = "vehicle : t {\n " + field + "\n}\n";
        var preview = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(input), LegacyVehicleKind.Truck, "t");
        Assert.Throws<InvalidOperationException>(() => preview.SetOdometer(0));
        Assert.Equal(input, preview.PreviewText);
        Assert.False(preview.CanUndo);
    }

    [Fact]
    public void TrailerCannotUseTruckOdometerMapping()
    {
        const string input = "trailer : t {\n odometer: 5\n}\n";
        var preview = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(input), LegacyVehicleKind.Trailer, "t");
        Assert.Throws<InvalidOperationException>(() => preview.SetOdometer(6));
        Assert.Equal(input, preview.PreviewText);
    }
    [Fact]
    public void TrailerChainRepairIsAtomicAndPreservesCargoAndUnknownFields()
    {
        const string input = "trailer : a {\n trailer_body_wear: 0.2\n chassis_wear: 0.3\n cargo_damage: 0.7\n slave_trailer: b\n}\ntrailer : b {\n trailer_body_wear: 0.4\n chassis_wear: 0.5\n mod: \"保留\"\n slave_trailer: null\n}\n";
        var preview = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(input), LegacyVehicleKind.Trailer, "a");
        preview.RepairTrailerChainBody();
        Assert.Contains("cargo_damage: 0.7", preview.PreviewText, StringComparison.Ordinal);
        Assert.Contains("mod: \"保留\"", preview.PreviewText, StringComparison.Ordinal);
        Assert.DoesNotContain("trailer_body_wear: 0.4", preview.PreviewText, StringComparison.Ordinal);
        Assert.True(preview.Undo());
        Assert.Equal(input, preview.PreviewText);
        Assert.False(preview.CanUndo);
        Assert.True(preview.Redo());
    }

    [Theory]
    [InlineData("slave_trailer: a")]
    [InlineData("slave_trailer: missing")]
    [InlineData("slave_trailer: null\n slave_trailer: null")]
    public void InvalidTrailerChainNeverPartiallyRepairs(string link)
    {
        string input = "trailer : a {\n trailer_body_wear: 0.2\n chassis_wear: 0.3\n " + link + "\n}\n";
        var preview = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(input), LegacyVehicleKind.Trailer, "a");
        Assert.Throws<InvalidOperationException>(() => preview.RepairTrailerChainBody());
        Assert.Equal(input, preview.PreviewText);
        Assert.False(preview.CanUndo);
    }
    [Theory]
    [InlineData(LegacyVehicleKind.Truck, "vehicle")]
    [InlineData(LegacyVehicleKind.Trailer, "trailer")]
    public void WheelRepairPreservesCountAndUnknownFieldsAndIsOneUndo(LegacyVehicleKind kind, string type)
    {
        string input = type + " : t {\n wheels_wear: 2\n wheels_wear[0]: &3f000000\n wheels_wear[1]: 0.25\n mod_data: \"保留\"\n}\n";
        var preview = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(input), kind, "t");
        preview.RepairWheelWear();
        string expected = input.Replace("&3f000000", "0", StringComparison.Ordinal)
            .Replace("wheels_wear[1]: 0.25", "wheels_wear[1]: 0", StringComparison.Ordinal);
        Assert.Equal(expected, preview.PreviewText);
        Assert.True(preview.Undo());
        Assert.Equal(input, preview.PreviewText);
        Assert.True(preview.Redo());
        Assert.Equal(expected, preview.PreviewText);
    }

    [Theory]
    [InlineData("wheels_wear: 2\n wheels_wear[0]: 0.2")]
    [InlineData("wheels_wear: 1\n wheels_wear[1]: 0.2")]
    [InlineData("wheels_wear: 2\n wheels_wear[0]: 0.2\n wheels_wear[0]: 0.3")]
    [InlineData("wheels_wear: 1\n wheels_wear[0]: NaN")]
    [InlineData("wheels_wear: 65")]
    public void InvalidWheelArraysFailWithoutChanges(string fields)
    {
        string input = "vehicle : t {\n " + fields + "\n}\n";
        var preview = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(input), LegacyVehicleKind.Truck, "t");
        Assert.Throws<InvalidOperationException>(() => preview.RepairWheelWear());
        Assert.Equal(input, preview.PreviewText);
        Assert.False(preview.CanUndo);
    }
    private const string Synthetic = "SiiNunit\n{\n vehicle : truck {\n  fuel_relative: &3f000000\n  engine_wear: 0.25\n  mod_field: \"保持\"\n }\n trailer : trailer1 {\n  trailer_body_wear: 0.5\n  cargo_damage: 0.75\n  accessory: custom.ref\n }\n}\n";

    [Fact]
    public void RepairGroupsScalarChangesAndLeavesWheelsAndFuelUntouched()
    {
        const string input = "vehicle : t {\n engine_wear: 0.1\n transmission_wear: 0.2\n cabin_wear: 0.3\n chassis_wear: 0.4\n wheels_wear[0]: 0.5\n fuel_relative: 0.25\n}\n";
        var preview = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(input), LegacyVehicleKind.Truck, "t");
        preview.RepairKnownBodyFields();
        Assert.Contains("engine_wear: 0\n", preview.PreviewText, StringComparison.Ordinal);
        Assert.Contains("transmission_wear: 0\n", preview.PreviewText, StringComparison.Ordinal);
        Assert.Contains("wheels_wear[0]: 0.5", preview.PreviewText, StringComparison.Ordinal);
        Assert.Contains("fuel_relative: 0.25", preview.PreviewText, StringComparison.Ordinal);
        Assert.True(preview.Undo());
        Assert.Equal(input, preview.PreviewText);
        Assert.False(preview.CanUndo);
        Assert.True(preview.Redo());
    }

    [Fact]
    public void RepairFailsAtomicallyWhenAnyRequiredFieldIsInvalid()
    {
        const string input = "vehicle : t {\n engine_wear: 0.1\n transmission_wear: 0.2\n cabin_wear: 2\n chassis_wear: 0.4\n}\n";
        var preview = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(input), LegacyVehicleKind.Truck, "t");
        Assert.Throws<InvalidOperationException>(() => preview.RepairKnownBodyFields());
        Assert.Equal(input, preview.PreviewText);
        Assert.False(preview.CanUndo);
    }

    [Fact]
    public void TrailerBodyRepairDoesNotChangeCargoOrLinkedTrailer()
    {
        const string input = "trailer : t {\n trailer_body_wear: 0.5\n chassis_wear: 0.2\n cargo_damage: 0.8\n slave_trailer: next\n}\n";
        var preview = new LegacyVehiclePreview(SiiSyntaxDocument.Parse(input), LegacyVehicleKind.Trailer, "t");
        preview.RepairKnownBodyFields();
        Assert.Contains("trailer_body_wear: 0\n", preview.PreviewText, StringComparison.Ordinal);
        Assert.Contains("chassis_wear: 0\n", preview.PreviewText, StringComparison.Ordinal);
        Assert.Contains("cargo_damage: 0.8", preview.PreviewText, StringComparison.Ordinal);
        Assert.Contains("slave_trailer: next", preview.PreviewText, StringComparison.Ordinal);
    }

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
