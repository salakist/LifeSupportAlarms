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

### `AlarmSpec`

Desired alarm state — what we want a KSP alarm to be. Built via static factories; never wraps an existing alarm.

| Property | Type | Notes |
|---|---|---|
| `Prefix` | `string` | e.g. `AlarmPrefixes.Supplies` |
| `Title` | `string` | Human-readable title computed at construction |
| `TimeLeft` | `double` | Seconds until resource expires |
| `VesselPersistentId` | `uint` | Matches `AlarmTypeRaw.vesselId` |
| `VesselGuid` | `Guid` | Stored in alarm `description` |

**Factories**

- `AlarmSpec.ForResource(vessel, prefix, timeLeft)` — title derived from prefix via switch lookup (see `ResourceTitle` helper).
- `AlarmSpec.ForGrouped(vessel, timeLeft, criticalLabel)` — title is `"{vessel.Name} ({criticalLabel})"` or just `vessel.Name` if `criticalLabel` is empty.

**`ResourceTitle(vesselName, prefix)`** — private static. Maps prefix to label:

| Prefix | Label |
|---|---|
| `AlarmPrefixes.Supplies` | `"Supplies"` |
| `AlarmPrefixes.EC` | `"Electric Charge"` |
| `AlarmPrefixes.Hab` | `"Hab"` |
| `AlarmPrefixes.Home` | `"Home"` |
| any other | `prefix.Trim('[',']').Replace("USILS-","")` |

### `FoundAlarm`

Wraps an existing KSP alarm found by `AlarmRepository.Find()`. Only `AlarmRepository` constructs instances.

| Property | Type | Source |
|---|---|---|
| `Raw` | `AlarmTypeRaw` | The underlying KSP alarm |
| `Prefix` | `string` | As passed to `Find` |
| `Title` | `string` | `Raw.title` |
| `Ut` | `double` | `Raw.ut` |
| `VesselPersistentId` | `uint` | `Raw.vesselId` |

No mutation methods. Pass `found.Raw` to `AlarmClockScenario.DeleteAlarm` when deleting.

### `AlarmPrefixes`

Static class of canonical prefix constants.

| Constant | Value |
|---|---|
| `Supplies` | `"[USILS-Supplies]"` |
| `EC` | `"[USILS-EC]"` |
| `Hab` | `"[USILS-Hab]"` |
| `Home` | `"[USILS-Home]"` |
| `Grouped` | `"[USILS-Grouped]"` |
| `AllResources` | `string[]` of the four non-grouped prefixes |
| `All` | `string[]` of all five prefixes |

Use `AlarmPrefixes.All` for `DeleteAll`, `AlarmPrefixes.AllResources` when iterating per-resource alarms only.

### `AlarmAction`

`internal enum AlarmAction { DoNothing = 0, KillWarp = 1, PauseGame = 2 }`

Mirrors the `int` stored in `LifeSupportAlarmsSettings.AlarmAction` (which must stay `int` because KSP's `CustomIntParameterUI` requires it). Cast at the `AlarmService` boundary: `(AlarmAction)settings.AlarmAction`.

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
