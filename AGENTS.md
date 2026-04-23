# AGENTS.md

Instructions for AI coding agents working on the LifeSupportAlarms KSP mod.

## Project Overview

**LifeSupportAlarms** is a Kerbal Space Program plugin (C# / .NET 4.8) that reads life support expiry times from USI-LS (`LifeSupportManager`) and creates alarms in the stock KSP alarm clock (`AlarmClockScenario`) for all crewed vessels. It supports four resource types: Supplies, Electric Charge, Hab time, and Home time. Alarms fire a configurable number of hours before expiry.

- KSP version: 1.12.x
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

## Key APIs

_Expanded in Phase 2. See source files for details._

- `KSPAddon(KSPAddon.Startup.Flight, false)` — Flight-scene-only MonoBehaviour entry point
- `Debug.Log(string)` — KSP log output (appears in `KSP.log`)

## Commit & PR Policy

See [COMMIT_POLICY.md](COMMIT_POLICY.md).
