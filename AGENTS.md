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
├── .editorconfig
├── .gitignore
├── AGENTS.md                        ← this file
├── COMMIT_POLICY.md
├── README.md
├── LifeSupportAlarms.dll            ← Release build output (gitignored)
├── LifeSupportAlarms.sln
└── LifeSupportAlarms/               ← C# project folder
    ├── LifeSupportAlarms.csproj
    ├── LifeSupportAlarmsAddon.cs    ← KSPAddon scene stubs + LifeSupportAlarmsScenarioRegistrar
    ├── LifeSupportAlarmsCore.cs     ← MonoBehaviour base; pure poll loop only
    ├── LifeSupportAlarmsSettings.cs ← GameParameters difficulty settings page
    ├── Domain/
    │   ├── TrackedVessel.cs         ← wraps Vessel + VesselSupplyStatus; owns GetResourceTimes()
    │   ├── LifeSupportAlarm.cs      ← read-only DTO wrapping AlarmTypeRaw
    │   └── VesselResourceTimes.cs  ← value object: computed remaining times per resource
    ├── Repositories/
    │   ├── VesselRepository.cs      ← GetCrewedVessels() iterator over USI-LS supply data
    │   └── AlarmRepository.cs       ← CRUD wrapper over AlarmClockScenario
    └── Services/
        └── AlarmService.cs          ← grouped-vs-individual dispatch; ClearAll()
```

## Architecture

The plugin uses a 3-layer design. Each layer has its own `AGENTS.md` with detailed contracts.

- **`LifeSupportAlarmsCore`** (project root) — pure poll loop, no domain logic. See [LifeSupportAlarms/AGENTS.md](LifeSupportAlarms/AGENTS.md).
- **Domain** — `TrackedVessel`, `LifeSupportAlarm`, `VesselResourceTimes`. See [Domain/AGENTS.md](LifeSupportAlarms/Domain/AGENTS.md).
- **Repositories** — `VesselRepository`, `AlarmRepository`. See [Repositories/AGENTS.md](LifeSupportAlarms/Repositories/AGENTS.md).
- **Services** — `AlarmService`. See [Services/AGENTS.md](LifeSupportAlarms/Services/AGENTS.md).

## Build Commands

From the repo root (solution directory):
```
dotnet build LifeSupportAlarms\LifeSupportAlarms.csproj /p:Configuration=Release /v:minimal
```

The Release output path is `../` relative to the project folder, so `LifeSupportAlarms.dll` lands directly in `GameData/LifeSupportAlarms/` where KSP can load it.

Do **not** use `msbuild.exe` from `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\` — it only understands C# 5 and will reject modern syntax. Always use `dotnet build`.

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

See the sub-folder `AGENTS.md` files for layer-specific API details. A few project-wide notes:

- `Debug.Log(string)` — KSP log output (appears in `KSP.log`); prefix every message with `[LifeSupportAlarms]`.
- `LifeSupportAlarmsSettings.Instance` — convenience accessor; returns `null` when no game is loaded.
- `GameParameters.CustomParameterNode` — base class for Difficulty settings pages; `HighLogic.CurrentGame.Parameters.CustomParams<T>()` to read at runtime.

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
