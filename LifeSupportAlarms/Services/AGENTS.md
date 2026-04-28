# AGENTS.md — Services layer

Instructions scoped to `LifeSupportAlarms/Services/`.
For project-wide context see [project AGENTS.md](../AGENTS.md) and [root AGENTS.md](../../AGENTS.md).

## Purpose

Services own orchestration logic — deciding **which alarms to create, update, or remove** based on computed resource times and user settings. They call `AlarmRepository` for all reads/writes and never touch `AlarmClockScenario` directly.

---

## `AlarmService`

Holds an `AlarmRepository` field (injected via constructor). Created once in `LifeSupportAlarmsCore.Start()` and reused across all poll ticks.

### `Sync(TrackedVessel vessel, VesselResourceTimes times, LifeSupportAlarmsSettings settings, double now, double leadTimeSecs)`

Dispatches to `SyncGrouped` or `SyncIndividual` based on `settings.GroupAlarmsByVessel`. Cast `(AlarmAction)settings.AlarmAction` once per method at the top of each private handler (not in `Sync` itself).

### `SyncGrouped(...)`

1. Delete all per-resource alarms via `foreach (string prefix in AlarmPrefixes.AllResources) _repo.Delete(vessel.PersistentId, prefix)`.
2. Build a `(bool enabled, double time, string label)[]` candidates table (one row per resource); iterate to find the row with the smallest `time` among enabled entries.
3. If `earliest < double.PositiveInfinity`: `_repo.Upsert(AlarmSpec.ForGrouped(vessel, earliest, criticalLabel), ...)`.
4. Else: `_repo.Delete(vessel.PersistentId, AlarmPrefixes.Grouped)`.

### `SyncIndividual(...)`

1. Delete `AlarmPrefixes.Grouped` for this vessel.
2. For Supplies and EC: call `UpsertOrDelete`.
3. For Hab and Home:
   - If `times.AnyHabPenalty == false`: delete both `AlarmPrefixes.Hab` and `AlarmPrefixes.Home` unconditionally.
   - If `times.AnyHabPenalty == true`: call `UpsertOrDelete` for each, respecting `settings.EnableHabAlarm` / `settings.EnableHomeAlarm`.

### `ClearAll()`

Deletes all plugin-managed alarms from every vessel. Called when `settings.EnableAlarms == false`.

Steps:
1. Guard: if `AlarmClockScenario.Instance == null`, return immediately.
2. Collect `persistentId` for every vessel in `FlightGlobals.Vessels`.
3. Call `_repo.DeleteAll(ids)`.

### Alarm title conventions

| Prefix | Individual mode title | Grouped mode title |
|---|---|---|
| `AlarmPrefixes.Supplies` | `"{vesselName} Supplies"` | — |
| `AlarmPrefixes.EC` | `"{vesselName} Electric Charge"` | — |
| `AlarmPrefixes.Hab` | `"{vesselName} Hab"` | — |
| `AlarmPrefixes.Home` | `"{vesselName} Home"` | — |
| `AlarmPrefixes.Grouped` | — | `"{vesselName} ({criticalLabel})"` |

Title construction is owned by `AlarmSpec.ResourceTitle` (individual) and `AlarmSpec.ForGrouped` (grouped). `AlarmService` does not build title strings directly.

## Rules

- **No `AlarmClockScenario` calls** — all alarm I/O goes through `AlarmRepository`.
- **No resource-time computation** — that belongs in `TrackedVessel.GetResourceTimes()`.
- **No KSP scene or settings reads** — `settings` and `times` are passed in by `LifeSupportAlarmsCore`.
