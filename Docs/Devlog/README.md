# Development log

[Project overview](../../README.md) · [Engineering notes](../Engineering.md)

These notes preserve author-provided development history. They are not automatically generated release notes, verified release dates, or a promise that every historical rule remains in the current build.

| Record | Scope | Provenance |
| --- | --- | --- |
| [v0.4.0 Development Update](update-0.4.0.md) | Combat, boss/AI tuning, equipment, events, UI, resume flow, and Chapter 4 pixel-art portraits | Curated from the author's current update notes and the implemented project changes. [itch.io-ready version](itchio-v0.4.0.md). |
| [Update 2.0](update-2.0.md) | Effect presets, visual integration, map exploration, hero unlocks, Zhao Yun, Longdan | Version label and bullet list supplied by the author; date not supplied. |
| [Unversioned update notes](unversioned-updates.md) | Combat, enemies, progression, equipment, events, UI, localization, art | Separate author-provided attachment; version/date not supplied. [Verbatim source](source-notes-unversioned.txt). |

The relative chronology of the two supplied records has not been established. Do not assign the second record to version 3.0 or 4.0 without author confirmation.

## Historical versus current behavior

- Update 2.0 introduced movement and E-key interaction on the map. The current [map implementation](../../Scripts/Map/README.md) explicitly documents replacing that with mouse-selected nodes.
- Some reward amounts, balancing details, and tutorial entry behavior in the longer notes were revised later. Read the source for current rules.
- Current menu branding/version remains Forgotten Three Kingdoms / v0.4.0. The portfolio title is Forgot Kingdom. These labels have not been normalized by this documentation update.
- The local Git history available during this audit contains an initial import and a README translation, not the pre-import development sequence. No dates or release tags have been reconstructed from that history.

## Existing project documentation

The earlier materials remain in their original locations:

- [Architecture](../../ARCHITECTURE.md) — original architectural guidance in Chinese; some framework-status statements predate current implementations.
- [Contribution rules](../../CONTRIBUTING.md) and [archived technical README](../DevelopmentGuide.md).
- [Combat matchup rules](../CombatMatchupRules.md).
- [Scene structure](../SceneStructure.md) and [battle UI standard](../BattleUI_Standard.md).
- [Art pipeline](../Art/README.md), [naming conventions](../Art/02_Naming_Convention.md), and [integration checklist](../Art/Developer_Checklist.md).
- [Menu art provenance](../../Assets/Backgrounds/MainMenu/README.md) and [boss visual documentation](../../Assets/Bosses/BattlePortraits/Generated/README.md).

## Future entries

Place future logs in this directory as `update-<confirmed-version>.md`. Include the actual release date only when known, player-facing changes, technical decisions, known issues, and links to relevant commits/tests. Keep supplied raw notes alongside edited summaries when their meaning or chronology is uncertain.
