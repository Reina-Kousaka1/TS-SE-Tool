# TS SET
Truck Simulator Save Editor Tool

## Description
Small tool for editing save files of Euro Truck Simulator 2 and American Truck Simulator.

## Next Generation — Phase 2 in progress

The combined .NET 10 / Avalonia rebuild is being developed alongside the existing Windows .NET Framework application. The new desktop app has navigation and interface previews, but it does not discover or read real profiles and does not edit game saves. See the [Phase 2 status](docs/architecture/phase-2-foundation.md) and [Avalonia architecture](docs/architecture/avalonia-desktop.md). Run it with the .NET 10 SDK using `dotnet run --project desktop/TSSE.Desktop/TSSE.Desktop.csproj`.

## OS
Windows x64

### Dependence
.NET Framework 4.7.2

## You can:
* add Custom paths for save files.
* edit Local and Steam save files.
* edit Player level and skill.
* edit and share saved User Colors for truck and trailer.
* edit amount of Money on account.
* visit Cities and be able to grab cargo from discovered cities.
* buy and\or upgrade Garages.
* repair and\or refuel your Truck.
* Share truck paint job.
* repair Trailer.
* create custom jobs for Freight market.
* make basic edits to Cargo market.
* share Truck position.
* share GPS paths.
* share Multiple Truck positions as one Convoy Control pack.

## Short term goals:
* finish sharing functions for truck parts.
* add editing and share functions for trailers.
 

## Long term goals:
* add the ability to creat jobs for Cargo market (have couple ideas)
* get map data from game\game generated files.
* scan mods for data (trucks, cargo...)
