# Forgotten Three Kingdoms

A desktop card-battle prototype built with **Godot 4.6 .NET** and **C#**.

Core features:

- Turn-based card combat.
- In-run progression driven by characters, skills, equipment, buffs, and events.
- Chinese and English localization.
- Separate Trigger, Reward, Damage, and Presentation systems to reduce coupling.

The default resolution is `2560 x 1440`. The main UI font is `NotoSerifCJKsc-Black.otf`.

## Architecture

```text
Data
  |
  v
Logic
  |
  v
Presentation
  |
  v
UI
```

- **Data:** Static definitions for characters, enemies, equipment, skills, events, buffs, and stages.
- **Logic:** Rules for combat, damage, rewards, triggers, shops, inventory, and events.
- **Presentation:** Coordination of combat feedback, floating numbers, logs, and choice panels.
- **UI:** Godot Control nodes, scenes, buttons, cards, and panels.

The rules layer should not take on presentation responsibilities. Before adding a feature, identify whether it belongs to Data, Logic, Presentation, or UI, then place it in the appropriate module.

## Project Structure

```text
Assets/
  Fonts, images, and other assets.

Scenes/
  Godot scene files.

Scripts/
  C# game logic and UI controllers.

Scripts/Battle/
  Combat lifecycle and resolution effects.

Scripts/Damage/
  Damage, defense, dying, death, and damage modifiers.

Scripts/Equipment/
  Equipment trigger effects.

Scripts/Presentation/
  Visuals, animations, weapons, effects, audio, and UI presentation.

Scripts/Reactions/
  Real-time reaction window options.

Scripts/SkillEffects/
  Effects associated with skill definitions.

Scripts/Skills/
  Combat effects for character, boss, and event skills.
```

## Systems

### Battle

Manages the combat lifecycle, phase progression, action bar, target selection, logs, and presentation coordination.

Main entry points:

- `BattleManager`
- `BattleContext`
- `BattleResolver`
- `BattleLifecycleEffects`

### Trigger

Connects skills, equipment, buffs, and combat lifecycle effects to a shared `TriggerTiming` system.

Main entry points:

- `TriggerManager`
- `IBattleEffect`
- `EffectQueue`
- `TriggerTiming`
- `EffectPriority`

### Damage

Handles damage instances, defense windows, damage modifiers, HP loss, dying, and death.

Main entry points:

- `DamageEffects`
- `DamageModifierPipeline`
- `DamageType`

### Reward

Executes reward actions through a shared system so that events, shops, and debug tools do not modify player state directly.

Main entry points:

- `RewardManager`
- `RewardSequence`
- `RewardAction`

### Localization

Handles Chinese and English text lookup, language switching, and localized names and descriptions.

Main entry points:

- `Localization`
- `LocalizationGlossary`
- `LocalizationValidator`

### Presentation

Converts gameplay results into visible feedback for the player.

Main entry points:

- `PresentationManager`
- `PresentationEvent`
- `PresentationConfig`
- `CharacterPresenter`
- `WeaponPresenter`
- `EffectPresenter`
- `InteractionPresenter`
- `UIPresenter`

When adding animations or visual sequences, extend the Presentation layer rather than playing animations directly from Battle or Damage code.

### Tutorial

Manages tutorial steps, trigger conditions, overlay highlights, arrows, and training battles.

Main entry points:

- `TutorialManager`
- `TutorialDatabase`
- `TutorialStep`
- `TutorialBattleScene`

### Inventory

Manages owned equipment, inventory, equipped slots, selling, drag-and-drop interactions, and equipment details.

Main entry points:

- `InventoryManager`
- `InventoryController`
- `InventoryItemUI`
- `EquipmentSlotUI`

### Shop

Handles shop inventory generation, pricing, purchases, and item UI.

Main entry points:

- `ShopManager`
- `ShopController`
- `ShopSlotUI`

### Event

Manages map events, options, conditions, rewards, and transitions between events.

Main entry points:

- `EventManager`
- `EventDatabase`
- `EventController`
- `EventData`

## Adding a Character

1. Add character data to `CharacterDatabase`.
2. Add localized text for the character's name, description, faction, and other details.
3. If the character has default skills, add their definitions to `SkillDatabase`.
4. Implement combat effects in `Skills/` or `SkillEffects/` as appropriate.
5. Register combat effects through `TriggerManager`; avoid character-specific checks in `BattleManager`.
6. Update the codex, character selection, or relevant test entry points.

## Adding Equipment

1. Add a stable ID to `EquipmentIds`.
2. Add the equipment definition to `EquipmentDatabase`.
3. Add localized equipment names and descriptions.
4. Implement combat effects as `IBattleEffect` in `Scripts/Equipment/`.
5. For random drops, verify `CanAppearInRandomPool`, rarity, type, and acquisition source.
6. For shop availability, verify the shop pool filtering rules.

## Adding an Event

1. Add an event definition to `EventDatabase` or the appropriate event factory.
2. Prefer `RewardSequence` and `RewardAction` for event rewards.
3. Use `ChoicePanel` and `ChoiceProvider` when a choice is required.
4. Put all event text in Localization.
5. Define `RunOnce`, chapter, rarity, and hidden conditions in the event data.
6. Do not modify player data directly inside event options.

## Adding a Skill

1. Add a stable ID to `SkillIds`.
2. Add the skill definition to `SkillDatabase`.
3. Add localized skill names and descriptions.
4. Implement passive or combat effects as `IBattleEffect`.
5. Implement the appropriate UI and activation entry points for active, card-based, or display-only skills.
6. Register effects through `TriggerTiming`; avoid special-case branches in the main combat flow.

## Adding a Buff

1. Add a stable ID to `RunBuffIds` or the relevant buff definition module.
2. Add a definition to `RunBuffDatabase`.
3. Add localized names, descriptions, and tooltips.
4. Integrate combat buffs through `IBattleEffect` or the existing RunBuff pipeline.
5. Define the lifetime explicitly: current battle, next battle, entire run, or persistent data.

## Documentation Maintenance

When adding code, update the relevant documentation and metadata:

- File headers.
- XML documentation.
- Module READMEs.
- Localization keys.
- Corresponding rules in the architecture or contribution guidelines.

Keep this README aligned with the actual implementation; remove or revise descriptions of obsolete workflows.
