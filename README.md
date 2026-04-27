# LifeSupportAlarms

A [Kerbal Space Program](https://www.kerbalspaceprogram.com/) mod that automatically creates stock alarm clock entries when your crew are running low on life support resources.

## What it does

When you have vessels with crew out in space, LifeSupportAlarms watches all of them and creates alarms in the stock KSP Alarm Clock before their [USI Life Support](https://github.com/UmbraSpaceIndustries/USI-LS) resources run out. Alarms are kept up to date as resources are consumed — no manual setup required.

Monitored resources:
- **Supplies** — food/water consumed by crew over time
- **Electric Charge** — power required to keep life support running
- **Hab** — how long crew can tolerate living in a cramped vessel before penalties kick in
- **Home** — how long crew can stay off Kerbin before homesickness penalties apply

Alarms fire a configurable lead time before the resource actually expires, so you have time to act.

## Requirements

- [USI Life Support](https://forum.kerbalspaceprogram.com/topic/105202-*/) (USI-LS)

## Installation

Copy the `LifeSupportAlarms` folder into your `GameData` folder.

## Configuration

Settings are found in-game under **Settings → Difficulty → Life Support Alarms**.

| Setting | Default | Description |
|---|---|---|
| Enable Alarms | On | Master switch. Turn off to disable all alarms and clear existing ones. |
| Lead Time (hours) | 6 | How far ahead of resource expiry the alarm fires. |
| Alarm Action | Kill Warp | What happens when an alarm triggers: Do Nothing, Kill Warp, or Pause Game. |
| Supplies Alarm | On | Create alarms for Supplies depletion. |
| Electric Charge Alarm | Off | Create alarms for Electric Charge depletion. |
| Hab Alarm | On | Create alarms for Hab time expiry. |
| Home Alarm | On | Create alarms for Home time expiry. |
| Group Alarms by Vessel | On | Show one alarm per vessel (for the most critical resource) instead of one alarm per resource per vessel. |

## How it works

Every 10 seconds, the mod checks all crewed vessels against USI-LS data to calculate how long each resource will last. If a resource will run out within the lead time window, an alarm is created or updated. Alarms are automatically removed when they are no longer relevant (resource replenished, crew evacuated, etc.).

Alarms work in both the **Flight** scene and the **Tracking Station**.

## Alarm title format

- **Separate alarms** (default): `Vessel Name Supplies`, `Vessel Name Electric Charge`, etc.
- **Grouped alarms**: `Vessel Name (Supplies)` — showing whichever resource expires soonest.
