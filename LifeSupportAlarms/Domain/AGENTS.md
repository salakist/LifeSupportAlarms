# AGENTS.md — Domain layer

Instructions scoped to `LifeSupportAlarms/Domain/`.
For project-wide context see [project AGENTS.md](../AGENTS.md) and [root AGENTS.md](../../AGENTS.md).

## Purpose

Domain objects own data and computation. This layer has **no knowledge of the alarm clock** (`AlarmClockScenario` must never be referenced here) and **no knowledge of dispatch logic** (that belongs in `Services/`).

## Classes

### `TrackedVessel`

The central domain object. Wraps a KSP `Vessel` and its USI-LS `VesselSupplyStatus`.

**Constructor**: `internal TrackedVessel(Vessel vessel, VesselSupplyStatus supply)`
Created only by `VesselRepository`.

**Identity properties**

| Property | Type | Source |
|---|---|---|
| `Name` | `string` | `vessel.vesselName` |
| `PersistentId` | `uint` | `vessel.persistentId` — used as the alarm `vesselId` key |
| `Id` | `Guid` | `vessel.id` — stored in alarm `description` |

**`GetResourceTimes(settings, cfg, now)`** → `VesselResourceTimes`

Computes remaining time for each enabled resource. Calls three private helpers:
- `ComputeSuppliesLeft(ratePerSec, now)` — if parts have Supplies use `amount / rate`; otherwise fall back to `SuppliesLeft - (now - LastFeeding)` from USI-LS.
- `ComputeECLeft(ratePerSec, now)` — same pattern with `ElectricCharge` / `ECLeft` / `LastECCheck`.
- `ComputeHabHome(cfg, now, out hab, out home, out anyPenalty)` — iterates `vessel.GetVesselCrew()`, skips kerbals where `GetNoHomeEffect == 0`, computes `hab = CachedHabTime - (now - TimeEnteredVessel)` and `home = MaxOffKerbinTime - now`, discards values that are "indefinite".

**`IsIndefinite(crewMember, timeLeft, cfg)`** — file-private static. Returns `true` when USI-LS would treat the time as indefinite:
- `timeLeft >= cfg.PermaHabTime`
- or kerbal has `ExplorerSkill` and `timeLeft >= cfg.ScoutHabTime`

**`GetResourceAmount(resName)`** — private. Sums `PartResource.amount` across all parts where `flowState == true`.

### `LifeSupportAlarm`

Read-only DTO wrapping an `AlarmTypeRaw`. Only `AlarmRepository` creates instances.

| Property | Notes |
|---|---|
| `Raw` | The underlying `AlarmTypeRaw`; exposed `internal` so `AlarmRepository` can call `AlarmClockScenario.DeleteAlarm(alarm.Raw)` |
| `Prefix` | e.g. `[USILS-Supplies]` |
| `Title` | `Raw.title` |
| `Ut` | `Raw.ut` |
| `VesselId` | `Raw.vesselId` |

**No mutation methods.** All writes go through `AlarmRepository`.

### `VesselResourceTimes`

`internal readonly struct` — value object passed from `TrackedVessel.GetResourceTimes()` to `AlarmService.Sync()`.

| Field | Semantics |
|---|---|
| `SuppliesLeft` | Seconds until supplies run out; `double.PositiveInfinity` if alarm disabled or resource is unlimited |
| `ECLeft` | Seconds until EC runs out; `double.PositiveInfinity` if alarm disabled or unlimited |
| `EarliestHab` | Minimum hab time remaining across all affected crew; `double.PositiveInfinity` if no hab penalty or indefinite |
| `EarliestHome` | Minimum home time remaining across all affected crew; `double.PositiveInfinity` if no hab penalty or indefinite |
| `AnyHabPenalty` | `true` when at least one kerbal has hab/home penalties active; `false` → both Hab and Home alarms must be removed |

## USI-LS API used in this layer

- `LifeSupportManager.Instance.VesselSupplyInfo` — do **not** null-check `Instance` with `==`; it uses a lazy getter. Use `ReferenceEquals(LifeSupportScenario.Instance, null)` in the outer guard instead.
- `VesselSupplyStatus` fields: `VesselId` (string guid), `NumCrew`, `SuppliesLeft`, `LastFeeding`, `ECLeft`, `LastECCheck`, `RecyclerMultiplier`, `CachedHabTime`
- `LifeSupportManager.Instance.FetchKerbal(ProtoCrewMember)` → `LifeSupportStatus` fields: `TimeEnteredVessel`, `MaxOffKerbinTime`
- `LifeSupportManager.GetNoHomeEffect(string kerbalName)` — returns `0` when hab/home penalties are disabled for that kerbal; skip them entirely.
- `LifeSupportConfig` (via `LifeSupportScenario.Instance.settings.GetSettings()`): `SupplyAmount`, `ECAmount`, `PermaHabTime`, `ScoutHabTime`
