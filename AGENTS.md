# VehiclePreSelection Agent Notes

## Project Overview

- Cities: Skylines II hybrid C# + UI mod.
- C# project: `VehiclePreSelection/VehiclePreSelection.csproj`.
- UI project: `VehiclePreSelection/UI`, generated from the official `create-csii-ui-mod` template.
- The UI binding group is `vehiclePreSelection`; keep binding names and JSON payload shapes backward-compatible.

## Build And Update Commands

- From the repository root: `dotnet build .\VehiclePreSelection.sln -c Release`.
- From `VehiclePreSelection/UI`: `npm.cmd run update` to sync official UI template files.
- From `VehiclePreSelection/UI`: `npm.cmd exec tsc -- --noEmit` for TypeScript validation.
- From `VehiclePreSelection/UI`: `npm.cmd run build` for the UI production bundle.
- On Windows, use `npm.cmd`, not bare `npm`, to avoid PowerShell execution policy issues.

## Logging

- Mod diagnostics should go to the mod logger under the game's `Logs` directory, not intentionally to `Player.log`.
- Route code through `Mod.LogEssential`, `Mod.LogDiagnostic`, or `Mod.LogException`; avoid direct `Mod.log.Info` outside the wrapper.
- Release builds should only emit essential load/dispose, system creation/disposal, and failure logs. Debug builds emit route planning, selector choice, restore/apply, random-color, persisted success, count, and entity diagnostics through `[Conditional("DEBUG")]` `LogDiagnostic` calls.
- Keep all mod diagnostics at `Info` level. Use `[ERROR]` in the message/wrapper for failure severity instead of warning/error logger levels.
- Inspect the mod log first for ordinary behavior. Inspect `Player.log` only for startup/load failures, Unity/game-level exceptions, crashes, or errors that may not reach the mod logger.

## Architecture Notes

- `RouteSelectionContext` is the shared route classification helper for `TransportLineData` and `WorkRouteData`.
- `VehiclePrefabLookup` centralizes prefab-name resolution using official `TransportVehicleSelectData.GetEntityQueryDesc()` and `WorkVehicleSelectData.GetEntityQueryDesc()` query descriptions.
- `VehicleModelSelectionWriter` is the shared writer for `DynamicBuffer<VehicleModel>` selections.
- `RouteVehicleSelectionUISystem` should keep using official `TransportVehicleSelectData` / `WorkVehicleSelectData` `ListVehicles` and `SelectVehicle` APIs for available/default vehicles.
- `RouteVehicleSelectionApplySystem` applies persisted choices to newly created route entities and should share lookup/context/writer helpers with the UI system.
- `RouteColorRandomizationUtils` derives route color families from `RouteSelectionContext`; do not change persisted color keys without a migration.
- Frontend UI extends official `cs2/modding` injection points and resolves vanilla components through `VanillaComponentResolver`. Keep fallback guards for missing vanilla modules.

## Compatibility Contracts

- Persisted file path: `Application.persistentDataPath/ModsData/VehiclePreSelection/vehicle-selections.json`.
- Persisted schema fields: `routes`, `colors`, `routeKey`, `primary`, `secondary`, `key`, `enabled`.
- Keep frontend binding names stable: `isPlanningRoute`, `routePrefab`, `supportsSecondarySelection`, `availablePrimaryVehiclesJson`, `availableSecondaryVehiclesJson`, `selectedPrimaryIndicesJson`, `selectedSecondaryIndicesJson`, `currentPrimaryVehicle`, `currentSecondaryVehicle`, `autoRandomColorEnabled`.

## Official References

- Paradox Wiki `Modding Toolchain`, oldid 6258, retrieved via Cities2-MCP on 2026-06-04: https://cs2.paradoxwikis.com/Modding_Toolchain
- Paradox Wiki `UI Modding`, oldid 6262, retrieved via Cities2-MCP on 2026-06-04: https://cs2.paradoxwikis.com/UI_Modding
- Paradox Wiki `Systems` UISystemBase snippet, retrieved via Cities2-MCP on 2026-06-04: https://cs2.paradoxwikis.com/Systems
