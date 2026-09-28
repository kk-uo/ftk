# Update 2.0 — Visual foundations and character progression

*Author-provided version label. Release date and release tag were not supplied.*

This update focused on visual integration, map interaction, and character progression, alongside a fix to Zhao Yun's Longdan reaction.

## Presentation and art integration

- Added effect presets and integration hooks for UI and art assets.
- Added default weapon visuals and default attack effects.
- Added map backgrounds.
- Added Zhao Yun's portrait and in-game full-body artwork.
- Added artwork for the initial event on the map.

## Exploration and progression

- Added direct character movement on the map, with **E** to enter nearby events.
- Added a character unlock system.
- Removed Zuo Ci and Jia Xu from the character-selection interface.

## Combat fix

- Fixed a bug affecting Zhao Yun's **Longdan** reaction.

## Where this work leads in the current project

[EffectPreset](../../Scripts/Presentation/Effects/EffectPreset.cs) and the [visual profile database](../../Scripts/Presentation/CombatVisualProfile/CombatVisualProfileDatabase.cs) provide inspectable examples of reusable visual definitions. [HeroUnlockProgress](../../Scripts/HeroUnlock/HeroUnlockProgress.cs) tracks unlock conditions. [LongdanReactionDamageRegression](../../Scripts/Tests/LongdanReactionDamageRegression.cs) now checks specific reaction-damage cases.

These links show current implementations, not a preserved 2.0 code snapshot. The latest map uses clicking rather than movement/E interaction; see the [map history](../../Scripts/Map/README.md). Hero unlock progress currently survives new runs in the same process, not application restarts.

## Original author notes

The notes below are retained for traceability; the sections above edit their English without inventing additional features. “ui and art upset port” has been interpreted as UI/art integration support, not an export platform.

```text
In 2.0 update. add effect preset, also provide ui and art upset port.
player can control character move on map and enter event by pressing E
add map background
add attack default weapon and default effect

add unlock character system
add zhaoyun protrait
add zhaoyun full-body in game
fix zhaoyun longdan reaction bug
remove zuoci and jiaxu from character selecting interface
add initial event picture on the map
```
