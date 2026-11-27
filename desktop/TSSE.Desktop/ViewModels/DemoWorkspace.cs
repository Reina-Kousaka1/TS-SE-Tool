using TSSE.GameAdapters.Editing;
using TSSE.Savegames.Syntax;

namespace TSSE.Desktop.ViewModels;

/// <summary>Shared synthetic-only workspace for one desktop catalog.</summary>
public sealed class DemoWorkspace
{
    private const string Fixture =
        "SiiNunit\n{\n economy : demo.economy {\n  bank: demo.bank\n  experience_points: 0\n  demo_unknown: \"preserved\"\n }\n bank : demo.bank {\n  money_account: 0\n }\n player : demo.player {\n  current_job: demo.job\n }\n player_job : demo.job {\n  cargo: cargo.synthetic\n  units_count: 3\n  planned_distance_km: 120\n  demo_unknown: \"preserved\"\n }\n vehicle : demo.truck {\n  fuel_relative: 0.5\n  engine_wear: 0.2\n  transmission_wear: 0.1\n  cabin_wear: 0\n  chassis_wear: 0.3\n  wheels_wear: 2\n  wheels_wear[0]: 0.2\n  wheels_wear[1]: 0.1\n  demo_unknown: \"preserved\"\n }\n trailer : demo.trailer {\n  cargo_damage: 0.1\n  trailer_body_wear: 0.2\n  chassis_wear: 0.3\n  wheels_wear: 1\n  wheels_wear[0]: 0.4\n  slave_trailer: demo.trailer2\n  demo_unknown: \"preserved\"\n }\n trailer : demo.trailer2 {\n  trailer_body_wear: 0.7\n  chassis_wear: 0.8\n  slave_trailer: null\n  demo_unknown: \"second\"\n }\n}\n";

    private static string CreateFixture() => Fixture
        .Replace("  cargo: cargo.synthetic\n", "  cargo: cargo.synthetic\n  cargo_model_index: 0\n  urgency: 0\n  source_company: company.demo_source\n  target_company: company.demo_target\n", StringComparison.Ordinal)
        .Replace("  current_job: demo.job\n", "  current_job: demo.job\n  assigned_truck: demo.truck\n  assigned_trailer: demo.trailer\n  trucks: 2\n  trucks[0]: demo.truck\n  trucks[1]: demo.truck2\n  trailers: 2\n  trailers[0]: demo.trailer\n  trailers[1]: demo.trailer2\n", StringComparison.Ordinal)
        .Replace("  experience_points: 0\n", "  experience_points: 0\n  adr: 0\n  long_dist: 0\n  heavy: 0\n  fragile: 0\n  urgent: 0\n  mechanical: 0\n", StringComparison.Ordinal)
        .Replace("  fuel_relative: 0.5\n",
            "  accessories: 3\n  accessories[0]: demo.accessory\n  accessories[1]: demo.mod\n  accessories[2]: demo.missing\n  license_plate: \"DEMO|synthetic\"\n  odometer: 12345\n  odometer_float_part: &3f000000\n  fuel_relative: 0.5\n", StringComparison.Ordinal)
        .Replace("  cargo_damage: 0.1\n",
            "  accessories: 1\n  accessories[0]: demo.accessory\n  license_plate: \"TRAILER|synthetic\"\n  odometer: 500\n  odometer_float_part: &3f000000\n  cargo_damage: 0.1\n", StringComparison.Ordinal)
        .Replace(" trailer : demo.trailer2 {\n", " trailer : demo.trailer2 {\n  accessories: 0\n  odometer: 900\n  odometer_float_part: 0\n  cargo_damage: 0.25\n  wheels_wear: 1\n  wheels_wear[0]: 0.6\n", StringComparison.Ordinal)
        .Replace(" trailer : demo.trailer {\n", " vehicle : demo.truck2 {\n  accessories: 0\n  odometer: 700\n  odometer_float_part: 0\n  fuel_relative: 0.25\n  engine_wear: 0.4\n  transmission_wear: 0\n  cabin_wear: 0\n  chassis_wear: 0.2\n  wheels_wear: 1\n  wheels_wear[0]: 0.1\n  demo_unknown: \"second truck\"\n }\n trailer : demo.trailer {\n", StringComparison.Ordinal)
        .Replace("\n}\n",
            "\n job_offer_data : demo.offer {\n  cargo: cargo.alternate\n }\n job_offer_data : demo.offer2 {\n  cargo: cargo.synthetic\n }\n job_offer_data : demo.offer3 {\n  cargo: cargo.third\n }\n vehicle_accessory : demo.accessory {\n  data_path: \"synthetic\"\n }\n demo_mod_accessory : demo.mod {\n  data: \"unknown\"\n }\n}\n", StringComparison.Ordinal);

    public LegacySyntheticWorkspace Workspace { get; } = new(
        SiiSyntaxDocument.Parse(CreateFixture()), "demo.economy", "demo.player", "demo.truck", "demo.trailer");

    public event EventHandler? Changed;

    internal void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
