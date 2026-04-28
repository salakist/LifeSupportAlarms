# AGENTS.md

Instructions for AI coding agents working on the LifeSupportAlarms KSP mod.

## Project Overview

**LifeSupportAlarms** is a Kerbal Space Program plugin (C# / .NET 4.8) that reads life support expiry times from USI-LS (`LifeSupportManager`) and creates alarms in the stock KSP alarm clock (`AlarmClockScenario`) for all crewed vessels. It supports four resource types: Supplies, Electric Charge, Hab time, and Home time. Alarms fire a configurable number of hours before expiry.

- KSP version: 1.12.x
- Active in **Flight** and **Tracking Station** scenes
- No dependency on AlarmEnhancements.dll — both mods write to the same stock KSP alarm clock API independently

## Repository Layout

```
GameData/LifeSupportAlarms/          ← git root, KSP mod folder, solution root
├── .git/
├── .gitignore
├── README.md
├── AGENTS.md                        ← this file
├── COMMIT_POLICY.md
├── LifeSupportAlarms.dll            ← Release build output (gitignored)
├── LifeSupportAlarms.sln
└── LifeSupportAlarms/               ← C# project folder
    ├── LifeSupportAlarms.csproj
    ├── LifeSupportAlarmsAddon.cs
    └── LifeSupportAlarmsSettings.cs (added in Phase 3)
```

## Build Commands

Open `LifeSupportAlarms.sln` in Visual Studio and build with **Release** configuration.  
The Release output path is set to `../` relative to the project folder, so `LifeSupportAlarms.dll` lands directly in `GameData/LifeSupportAlarms/` where KSP can load it.

Alternatively, from the solution directory:
```
msbuild LifeSupportAlarms.sln /p:Configuration=Release
```

## KSP Environment

- KSP installation: `C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\`
- Managed DLLs: `<KSP_root>\KSP_x64_Data\Managed\`
- USILifeSupport.dll: `<KSP_root>\GameData\UmbraSpaceIndustries\LifeSupport\USILifeSupport.dll`
- HintPath depth from .csproj: 3× `..` reaches KSP root

## Testing Instructions

1. Build Release and confirm `LifeSupportAlarms.dll` appears in `GameData/LifeSupportAlarms/`
2. Launch KSP and load a save with a crewed vessel
3. Enter the Flight scene
4. Check `KSP.log` for `[LifeSupportAlarms] Loaded`
5. Open **Settings → Difficulty** and verify a "Life Support Alarms" section exists with the three controls
6. Change Lead Time and confirm alarm UTs shift on the next poll

## Key APIs

- `KSPAddon(KSPAddon.Startup.Flight, false)` / `KSPAddon.Startup.TrackingStation` — scene entry points. Both are thin subclasses of `LifeSupportAlarmsCore`. All logic lives in the core class.
- `Debug.Log(string)` — KSP log output (appears in `KSP.log`)
- `LifeSupportManager.Instance.VesselSupplyInfo` — list of `VesselSupplyStatus` for all tracked vessels
- `VesselSupplyStatus` fields: `VesselId`, `VesselName`, `NumCrew`, `SuppliesLeft`, `LastFeeding`, `ECLeft`, `LastECCheck`, `RecyclerMultiplier`, `CachedHabTime`
- `LifeSupportStatus` fields: `TimeEnteredVessel`, `MaxOffKerbinTime` (per-kerbal)
- `LifeSupportManager.GetNoHomeEffect(kerbalName)` — returns 0 when hab/home penalties are disabled for that kerbal
- `AlarmClockScenario.AddAlarm(AlarmTypeBase)` / `DeleteAlarm(AlarmTypeBase)` — stock alarm clock CRUD
- `AlarmTypeRaw` — generic alarm type used by this plugin; set `description`, `ut`, `vesselId`, `actions`, then assign `title` **after** `AddAlarm` (AddAlarm resets title to vessel name)
- Alarm identity: `description` starts with prefix `[USILS-Supplies]`, `[USILS-EC]`, `[USILS-Hab]`, `[USILS-Home]`, or `[USILS-Grouped]`; combined with `vesselId` for uniqueness
- Grouped mode (`GroupAlarmsByVessel = true`): one `[USILS-Grouped]` alarm per vessel showing the earliest-expiring enabled resource; title format `"{vesselName} ({criticalResource})"`. Individual resource alarms removed. Separate mode removes `[USILS-Grouped]`.
- `GameParameters.CustomParameterNode` — base class for Difficulty settings pages; `HighLogic.CurrentGame.Parameters.CustomParams<T>()` to read at runtime
- **C# language level**: The csproj sets `<LangVersion>latest</LangVersion>` and uses `<FrameworkPathOverride>` to build against .NET Framework 4.8 with the modern Roslyn compiler (`dotnet build`). Current SDK is .NET 10, which gives **C# 13**. Modern syntax (switch expressions, pattern matching, record types, etc.) is fully supported. Use `dotnet build` — do **not** use the old `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe`, which only supports C# 5.

## Commit & PR Policy

Full details in [COMMIT_POLICY.md](COMMIT_POLICY.md). The rules below are the ones most commonly violated — treat them as a pre-commit checklist.

### Required commit message format

```
<type>(<scope>): <description> [copilot]
```

Every AI-assisted commit **must** include all three parts. No exceptions.

- **type**: `feat` | `fix` | `docs` | `chore` | `refactor` | `test`
- **scope**: the component changed, e.g. `plugin` | `settings` | `build` | `docs`
- **description**: short imperative phrase, no trailing period
- **`[copilot]`**: mandatory author tag on the subject line for all AI-assisted commits

**Correct examples**
```
feat(plugin): add grouped alarm mode per vessel [copilot]
fix(plugin): correct AlarmAction=2 pause behaviour [copilot]
chore(build): enable C# latest via dotnet build [copilot]
docs(agents): add pre-commit checklist [copilot]
```

**Wrong — do not do these**
```
Fix AlarmAction=2 not pausing the game          ← no type/scope, no [copilot]
chore: enable C# latest via dotnet build        ← no scope, no [copilot]
feat(plugin): add grouped alarm mode [copilot]. ← trailing period
```

### Pre-commit build check

Run this and confirm zero errors, zero warnings before every commit:
```
dotnet build LifeSupportAlarms\LifeSupportAlarms.csproj /p:Configuration=Release /v:minimal
```
