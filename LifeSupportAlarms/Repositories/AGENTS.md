# AGENTS.md — Repositories layer

Instructions scoped to `LifeSupportAlarms/Repositories/`.
For project-wide context see [project AGENTS.md](../AGENTS.md) and [root AGENTS.md](../../AGENTS.md).

## Purpose

Repositories are **pure CRUD wrappers** over KSP singletons. No business logic, no dispatch decisions. They read/write external state and return domain objects or nothing.

---

## `VesselRepository`

Stateless read accessor over USI-LS vessel data.

**`GetCrewedVessels()`** → `IEnumerable<TrackedVessel>`

Iterator rules (in order):
1. Iterate `LifeSupportManager.Instance.VesselSupplyInfo`.
2. Skip entries where `vsl.NumCrew == 0`.
3. Resolve `Vessel` from `FlightGlobals.Vessels` by matching `vessel.id.ToString() == vsl.VesselId`.
4. Skip if no match found (vessel may have been destroyed or not yet loaded).
5. `yield return new TrackedVessel(vessel, vsl)`.

No caching — resolved fresh each poll tick. Do not add filtering logic; filtering belongs in `AlarmService`.

---

## `AlarmRepository`

Pure CRUD over `AlarmClockScenario`. The only class allowed to call `AlarmClockScenario.AddAlarm` / `DeleteAlarm`.

### Alarm identity

An alarm managed by this plugin is a `AlarmTypeRaw` where:
- `description` starts with one of the five prefixes: `[USILS-Supplies]`, `[USILS-EC]`, `[USILS-Hab]`, `[USILS-Home]`, `[USILS-Grouped]`
- `vesselId` matches the vessel's `persistentId`

Both together are unique. Format of `description`: `"{prefix}:{vessel.Id}"`.

### `Find(uint vesselPersistentId, string prefix)` → `LifeSupportAlarm?`

Iterates `AlarmClockScenario.Instance.alarms.Values`, casts to `AlarmTypeRaw`, matches `vesselId` and `description.StartsWith(prefix)`. Returns a `LifeSupportAlarm` wrapping the match, or `null`.

### `Upsert(TrackedVessel vessel, string prefix, string title, double timeLeft, double now, double leadTimeSecs, int alarmAction)`

Guards (delete and return early if any applies):
1. `double.IsPositiveInfinity(timeLeft)` — resource is indefinite or disabled
2. `double.IsNaN(timeLeft)` — invalid computation result
3. `timeLeft <= 0` — resource already expired
4. `alarmUT = now + timeLeft - leadTimeSecs; alarmUT <= now` — alarm would fire in the past

No-op check: if an existing alarm is found and `Math.Abs(existing.Ut - alarmUT) < 60.0` and `existing.Title == title`, skip the write entirely (avoids spamming AddAlarm every poll).

Create sequence:
```csharp
// 1. Delete stale existing alarm if present
if (existing != null) AlarmClockScenario.DeleteAlarm(existing.Raw);

// 2. Build and add new alarm
AlarmTypeRaw alarm = new() { description, ut, vesselId, actions };
AlarmClockScenario.AddAlarm(alarm);

// 3. Set title AFTER AddAlarm (AddAlarm resets title to vessel name)
alarm.title = title;

// 4. Fake-alarm trick: forces the alarm-list UI to refresh the displayed title
AlarmTypeRaw fake = new() { ut = alarm.ut + 1, actions = { deleteWhenDone = true, message = No } };
AlarmClockScenario.AddAlarm(fake);
AlarmClockScenario.DeleteAlarm(fake);
```

`alarmAction` mapping:

| Setting value | `AlarmActions.WarpEnum` |
|---|---|
| 2 | `PauseGame` |
| 1 | `KillWarp` |
| 0 (default) | `DoNothing` |

### `Delete(uint vesselPersistentId, string prefix)`

`Find` + `AlarmClockScenario.DeleteAlarm(found.Raw)` if found. No-op if not found.

### `DeleteAll(IEnumerable<uint> vesselIds)`

Calls `Delete` for every combination of vessel ID × all five prefixes. Used by `AlarmService.ClearAll()`.

## KSP Alarm Clock API

- `AlarmClockScenario.Instance.alarms` — `Dictionary<Guid, AlarmTypeBase>` of all alarms.
- `AlarmClockScenario.AddAlarm(AlarmTypeBase)` — registers the alarm; resets `title` to vessel name as a side-effect.
- `AlarmClockScenario.DeleteAlarm(AlarmTypeBase)` — removes by reference.
- `AlarmTypeRaw` — generic alarm type. Key fields: `description`, `ut`, `vesselId`, `title`, `actions.warp`, `actions.message`, `actions.deleteWhenDone`.
