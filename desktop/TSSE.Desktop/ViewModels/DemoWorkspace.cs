using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.Desktop.ViewModels;

/// <summary>Shared synthetic-only workspace for one desktop catalog.</summary>
public sealed class DemoWorkspace
{
    private const string Fixture =
        "SiiNunit\n{\n economy : demo.economy {\n  bank: demo.bank\n  experience_points: 0\n  demo_unknown: \"preserved\"\n }\n bank : demo.bank {\n  money_account: 0\n }\n player : demo.player {\n  current_job: demo.job\n }\n player_job : demo.job {\n  cargo: cargo.synthetic\n  units_count: 3\n  planned_distance_km: 120\n  demo_unknown: \"preserved\"\n }\n vehicle : demo.truck {\n  fuel_relative: 0.5\n  engine_wear: 0.2\n  transmission_wear: 0.1\n  cabin_wear: 0\n  chassis_wear: 0.3\n  wheels_wear: 2\n  wheels_wear[0]: 0.2\n  wheels_wear[1]: 0.1\n  demo_unknown: \"preserved\"\n }\n trailer : demo.trailer {\n  cargo_damage: 0.1\n  trailer_body_wear: 0.2\n  chassis_wear: 0.3\n  wheels_wear: 1\n  wheels_wear[0]: 0.4\n  slave_trailer: demo.trailer2\n  demo_unknown: \"preserved\"\n }\n trailer : demo.trailer2 {\n  trailer_body_wear: 0.7\n  chassis_wear: 0.8\n  slave_trailer: null\n  demo_unknown: \"second\"\n }\n}\n";

    private static string CreateFixture() => Fixture
        .Replace("  experience_points: 0\n", "  experience_points: 0\n  adr: 0\n  long_dist: 0\n  heavy: 0\n  fragile: 0\n  urgent: 0\n  mechanical: 0\n", StringComparison.Ordinal)
        .Replace("  fuel_relative: 0.5\n",
            "  accessories: 3\n  accessories[0]: demo.accessory\n  accessories[1]: demo.mod\n  accessories[2]: demo.missing\n  odometer: 12345\n  odometer_float_part: &3f000000\n  fuel_relative: 0.5\n", StringComparison.Ordinal)
        .Replace("  cargo_damage: 0.1\n",
            "  accessories: 1\n  accessories[0]: demo.accessory\n  cargo_damage: 0.1\n", StringComparison.Ordinal)
        .Replace("\n}\n",
            "\n vehicle_accessory : demo.accessory {\n  data_path: \"synthetic\"\n }\n demo_mod_accessory : demo.mod {\n  data: \"unknown\"\n }\n}\n", StringComparison.Ordinal);

    public LegacySyntheticWorkspace Workspace { get; } = new(
        SiiSyntaxDocument.Parse(CreateFixture()), "demo.economy", "demo.player", "demo.truck", "demo.trailer");

    public event EventHandler? Changed;

    internal void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
