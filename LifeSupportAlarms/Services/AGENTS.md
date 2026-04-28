# AGENTS.md — Services layer

Instructions scoped to `LifeSupportAlarms/Services/`.
For project-wide context see [project AGENTS.md](../AGENTS.md) and [root AGENTS.md](../../AGENTS.md).

## Purpose

Services own orchestration logic — deciding **which alarms to create, update, or remove** based on computed resource times and user settings. They call `AlarmRepository` for all reads/writes and never touch `AlarmClockScenario` directly.

---

## `AlarmService`

Holds an `AlarmRepository` field (injected via constructor). Created once in `LifeSupportAlarmsCore.Start()` and reused across all poll ticks.

### `Sync(TrackedVessel vessel, VesselResourceTimes times, LifeSupportAlarmsSettings settings, double now, double leadTimeSecs)`

Dispatches the correct set of alarms for one vessel. Two modes depending on `settings.GroupAlarmsByVessel`:

**Grouped mode** (`GroupAlarmsByVessel = true`):
1. Delete all four individual-resource alarms (`[USILS-Supplies]`, `[USILS-EC]`, `[USILS-Hab]`, `[USILS-Home]`) for this vessel.
2. Walk the four enabled resources in order (Supplies → EC → Hab → Home) and find the earliest `timeLeft` among those where the corresponding `Enable*Alarm` setting is `true`.
3. `_repo.Upsert(vessel, "[USILS-Grouped]", title, earliest, now, leadTimeSecs, settings.AlarmAction)`
   - Title format: `"{vessel.Name} ({criticalLabel})"` where `criticalLabel` is the name of the earliest resource (`"Supplies"`, `"Electric Charge"`, `"Hab"`, or `"Home"`). If nothing is enabled or all are `PositiveInfinity`, Upsert will guard-delete the alarm.

**Individual mode** (`GroupAlarmsByVessel = false`):
1. Delete `[USILS-Grouped]` for this vessel.
2. For Supplies and EC: call `UpsertOrDelete` (upsert if enabled, delete if not).
3. For Hab and Home:
   - If `times.AnyHabPenalty == false`: delete both `[USILS-Hab]` and `[USILS-Home]` unconditionally (no kerbal has the penalty active).
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
| `[USILS-Supplies]` | `"{vesselName} Supplies"` | — |
| `[USILS-EC]` | `"{vesselName} Electric Charge"` | — |
| `[USILS-Hab]` | `"{vesselName} Hab"` | — |
| `[USILS-Home]` | `"{vesselName} Home"` | — |
| `[USILS-Grouped]` | — | `"{vesselName} ({criticalLabel})"` |

`TitleFor(vesselName, prefix)` derives individual titles by stripping the brackets, removing `USILS-`, and replacing `EC` with `Electric Charge`.

## Rules

- **No `AlarmClockScenario` calls** — all alarm I/O goes through `AlarmRepository`.
- **No resource-time computation** — that belongs in `TrackedVessel.GetResourceTimes()`.
- **No KSP scene or settings reads** — `settings` and `times` are passed in by `LifeSupportAlarmsCore`.
