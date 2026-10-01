# v0.4.0 Development Update — Combat, Progression, and Presentation

This update is a broad iteration pass across combat rules, bosses, equipment, events, progression, UI, and presentation for **Forgotten Three Kingdoms**.

## Combat and rules

- Corrected the damage pipeline so additive modifiers resolve before multipliers.
- Standardized attack attributes and improved damage-source reporting in battle logs.
- Reworked the card/skill choice pool so character skills can enter skill selections.
- Added skill-trigger feedback: a skill name and the owner's portrait now appear when a skill activates.
- Updated Peach/Wine rescue priority: passive survival effects such as **Unyielding**, **Nirvana**, and **Drumbeat** resolve before Peach/Wine rescue is considered.
- Fixed a number of trigger and state issues, including fixed-HP effects, **Po Jun**, **Beng Huai**, Charm shields, Time Hourglass, and the curse-to-evil-aura interaction.
- Clarified that reaching 44 Curse stacks grants an additional Evil Aura instead of replacing Curse.
- Corrected the **Cyclops** so it no longer reflects damage incorrectly.
- Added equipment-trigger records to the battle log and refreshed the damage-preview typography.

## Characters, skills, and enemies

- Strengthened the Chapter 2 boss roster and revised the Traitor boss encounter to reduce its overall pressure.
- Reworked Liu Bei, Guan Yu, and Zhang Fei AI so Guan Yu and Zhang Fei no longer overuse dangerous actions.
- Updated AI and/or skills for Royal Death Guard, Giant Pus Sac, Zhen Ji, Pang Tong, Mi Heng, and the Chapter 2 boss group.
- Fixed the Qixing Altar boss-enhancement path and corrected Pang Tong's Nirvana labeling.
- Added new and adjusted Pang Tong skills; revised Zhuque Feather Fan and Curse Blade behavior.
- Updated Giant Pus Sac AI so it can actively attack.
- Added portraits for every playable hero and improved hero-detail hover behavior.

## Equipment, shops, and rewards

- Strengthened **Thunder Spear** and **Fire-Pattern Silver Spear**.
- Reworked **Soul Stone**, **Red Hare**, and **Bloodsweat Horse**; Soul Stone sales can now yield special chips, Red Hare preserves its Lv Bu/Unparalleled upgrade path, and Bloodsweat Horse makes Nullify free.
- Added the 150-gold shop-side chip choice: choose one of three chips.
- Added item-detail tooltips when hovering price UI.
- Revised the item pool, equipment placement screen, and several equipment/event reward paths.
- Changed the Chapter 2 completion reward from an Attack Chip to a Defense/HP chip.
- Rebalanced **Baigong Duoshang** and **Post-Battle Dividend**.

## Events and progression

- Rebuilt the opening-fate event pool, including exclusivity rules for options that share the same letter marker.
- Added and adjusted initial events, the Unexpected Short Circuit event, and initial decks.
- Reduced unnecessary information in event choice screens while retaining the global top-left HUD.
- Fixed reforging feedback so a reforged result is not exposed prematurely.
- Added a Continue Game flow: returning to the main menu saves the suspended run and resume restores it.
- Lowered battle-option Power cost from 20 to 10.

## Interface and presentation

- Rebuilt the main menu and added a dynamic background treatment.
- Added resume support from the main menu and return-to-menu controls during a run.
- Reworked hero selection: the first click selects a hero, then a prominent **Awaken** button starts the run.
- Improved inventory/equipment UI, event HUD spacing, and hover coverage for the lower-right hero information panel.
- Added Chapter 4 Abyss combat portraits for Scout, Giant Maw, Abyss Symbiote, and Phantom Tentacle, using a restrained Cthulhu cyberpunk pixel-art style.

## Known issue

- Elite enemy portrait replacement is still incomplete and remains an active presentation task.

## Engineering notes

The current project includes regression coverage for the major systems touched by this pass, including damage preview, suspended-run restoration, boss definitions, event rewards, and chip behavior. See the [engineering notes](../Engineering.md) and [test scenes](../../Scenes/Tests) for source-linked implementation detail.
