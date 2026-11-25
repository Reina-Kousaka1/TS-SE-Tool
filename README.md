# TS SET
Truck Simulator Save Editor Tool

## Repository direction

This repository retains its existing Windows save editor as a legacy reference while Phase 2 builds a separate .NET 10 / Avalonia generation. The legacy feature list below describes only the existing Windows application; it does not imply that those features are present in Next Generation.

## Next Generation — Phase 2 in progress

The combined .NET 10 / Avalonia rebuild is being developed alongside the existing Windows .NET Framework application. The new desktop app has navigation and interface previews, but it does not discover or read real profiles and does not edit game saves. See the [Phase 2 status](docs/architecture/phase-2-foundation.md) and [Avalonia architecture](docs/architecture/avalonia-desktop.md). Run it with the .NET 10 SDK using `dotnet run --project desktop/TSSE.Desktop/TSSE.Desktop.csproj`.

## Existing Windows application (legacy)

The legacy application remains in the repository as a reference and is not the new Avalonia desktop app.

- **Target OS:** Windows x64
- **Runtime:** .NET Framework 4.7.2

### Documented legacy capabilities

The following list describes the legacy application only. It is not a Next Generation feature checklist.

* Add custom paths for save files.
* Edit local and Steam save files.
* Edit player level and skill.
* Edit and share saved user colors for trucks and trailers.
* Edit the amount of money on account.
* Visit cities and access cargo from discovered cities.
* Buy or upgrade garages.
* Repair or refuel a truck.
* Share truck paint jobs.
* Repair trailers.
* Create custom Freight Market jobs.
* Make basic Cargo Market edits.
* Share truck positions and GPS paths.
* Share multiple truck positions as a Convoy Control pack.

### Legacy roadmap (historical)

These items are retained from the old README and are not the Phase 2 roadmap.

* Finish sharing functions for truck parts.
* Add editing and sharing functions for trailers.
* Add Cargo Market job creation.
* Get map data from game-generated files.
* Scan mods for data such as trucks and cargo.
