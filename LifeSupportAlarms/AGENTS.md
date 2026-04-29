# AGENTS.md — LifeSupportAlarms C# project

Instructions scoped to the `LifeSupportAlarms/` C# project folder.
For project-wide context (build, KSP environment, commit policy) see the [root AGENTS.md](../AGENTS.md).

## Folder map

| Path | Purpose |
|---|---|
| `LifeSupportAlarmsAddon.cs` | KSPAddon scene stubs; add no logic here |
| `LifeSupportAlarmsCore.cs` | `MonoBehaviour` base; **pure poll loop only** |
| `LifeSupportAlarmsSettings.cs` | `GameParameters` difficulty page |
| `Domain/` | Domain objects — see [Domain/AGENTS.md](Domain/AGENTS.md) |
| `Domain/AlarmAction.cs` | Enum: `DoNothing` / `KillWarp` / `PauseGame` |
| `Domain/AlarmPrefixes.cs` | Canonical prefix constants + `AllResources` / `All` arrays |
| `Domain/AlarmSpec.cs` | Desired alarm state; `ForResource` / `ForGrouped` factories |
| `Domain/FoundAlarm.cs` | Wraps existing `AlarmTypeRaw`; only `AlarmRepository` constructs |
| `Domain/TrackedVessel.cs` | Central domain object; owns `GetResourceTimes()` |
| `Domain/VesselResourceTimes.cs` | Value object: computed remaining times per resource |
| `Repositories/` | CRUD wrappers — see [Repositories/AGENTS.md](Repositories/AGENTS.md) |
| `Services/` | Dispatch logic — see [Services/AGENTS.md](Services/AGENTS.md) |

## C# conventions

- **Language**: SDK-style project targeting `net48` (`<TargetFramework>net48</TargetFramework>`) → .NET 4.8 runtime, C# 13 compiler (SDK .NET 10). Modern syntax is fine: switch expressions, pattern matching, collection expressions, expression-bodied members, target-typed `new`.
- **`record struct` is banned**: requires `IsExternalInit` which .NET 4.8 does not provide. Use `readonly struct` with an explicit constructor instead.
- **Namespaces follow folders**: `LifeSupportAlarms` (root files), `LifeSupportAlarms.Domain`, `LifeSupportAlarms.Repositories`, `LifeSupportAlarms.Services`.
- **`internal` by default**: everything is `internal` or `private` unless a KSP reflection requirement forces `public`. `MonoBehaviour` subclasses must be `public`.
- **Style rules at build time**: `EnforceCodeStyleInBuild=true` + `dotnet_analyzer_diagnostic.category-Style.severity = warning` surfaces all IDE style rules as build warnings. Active suppressions in `.editorconfig`:
  - `csharp_prefer_braces = false` — brace-free guard clauses (`if (x) continue;`) are idiomatic and allowed.
  - `IDE0058 = none` — KSP API methods (`AddAlarm`, `DeleteAlarm`) return `bool`; we intentionally discard it.
  - `IDE0051 = none` — `PollLifeSupport` is invoked by Unity `InvokeRepeating` via string reflection; it is not unused.
- **No unnecessary `using` directives**: `IDE0005` is active via `category-Style`. Child namespaces implicitly see their parent namespace — no `using LifeSupportAlarms;` needed in `Domain/`, `Repositories/`, or `Services/`.

## LifeSupportAlarmsCore — poll loop rules

`PollLifeSupport()` must stay a pure coordinator:
1. Call `ValidatePrerequisites` — return early if not ready.
2. If alarms disabled → `_alarmService.ClearAll()` and return.
3. Resolve `now`, `leadTimeSecs`, `cfg`.
4. Iterate `_vesselRepo.GetCrewedVessels()`.
5. For each vessel: `vessel.GetResourceTimes(…)` → `_alarmService.Sync(…)`.

No resource calculations, no alarm CRUD, no KSP alarm clock calls belong here.

## LifeSupportAlarmsAddon — scene entry points

`LifeSupportAlarmsFlightAddon` and `LifeSupportAlarmsTrackingAddon` are empty subclasses of `LifeSupportAlarmsCore`. Add **no** logic to them.

`LifeSupportAlarmsScenarioRegistrar` (SpaceCentre startup) patches `LifeSupportScenario` target scenes so data is available in the Tracking Station. It has no connection to alarm management.

## Logging

Use `Debug.Log(string)` for all KSP log output. Prefix every message:
```csharp
Debug.Log("[LifeSupportAlarms] ...");
```
Logs appear in `KSP.log` at `<KSP_root>/KSP.log`.
