# Screenshot and short-video plan

The README currently uses two real runtime UI captures. Their [provenance](Docs/Images/README.md) is recorded. They establish visual direction but do not yet demonstrate the central combat loop.

Save new approved captures in `Docs/Images/`. Capture the game viewport only, at a consistent 16:9 resolution, with readable text. Use English for the main recruiter-facing gallery where practical. Do not overlay invented damage numbers, hide a failure by compositing screens, or present concept art as gameplay. If a debug tool stages a scenario, disclose that in the caption.

## Still images, in priority order

| Priority / filename | Screen or state and what must be visible | Why it matters |
| --- | --- | --- |
| P0 — `Docs/Images/combat-system.png` | An ordinary multi-enemy battle with the selected target, HP/resources, fixed action bar, and a recent meaningful combat result. Hide debug windows; retain the normal HUD. | Demonstrates the core playable system rather than only menus. Make this the lead image once captured. |
| P0 — `Docs/Images/reaction-window.png` | Zhao Yun using Dodge against an attack, with the Longdan response choices visible and affordable/unaffordable states readable. | Connects an unusual combat mechanic to the documented reaction architecture. |
| P1 — `Docs/Images/roguelike-map.png` | A mid-run map with completed/current/available nodes and resources visible. Use the current click-node interface. | Shows progression and the choices outside battle; avoids advertising the retired WASD/E design. |
| P1 — `Docs/Images/boss-encounter.png` | A live boss fight with the boss name, distinct portrait, target information, and player build visible. | Shows content integration and encounter identity, not just isolated artwork. |
| P1 — `Docs/Images/build-inspection.png` | One useful equipment or skill tooltip alongside the player's build, without clipped text. | Makes equipment/skill interactions tangible. |
| P2 — `Docs/Images/main-menu.png` | Full menu with title, current version, and scene background; no editor chrome or unrelated HUD. | Establishes presentation quality and consistent branding. |
| P2 — replace `Docs/Images/character-selection.png` and `Docs/Images/initial-fate.png` | English-language versions of the existing screens, captured in normal flow; unlocked and locked chips, or all three complete fate descriptions. | Improves readability for English-speaking reviewers and replaces test-harness captures with normal-flow examples. |

Use three or four strong stills in the final README; link any larger gallery separately. Do not add blank image links until the files exist.

## Two recommended motion captures

1. **Combat response, 8–15 seconds:** select Dodge as Zhao Yun, reveal an enemy attack, open Longdan, choose a legal counter, show HP/log feedback and return to the next action. Save `Docs/Images/longdan-reaction.gif`, or an MP4 hosted with the eventual play page. An edited clip may remove waiting time but should not splice incompatible states.
2. **Scene-state continuity, 8–15 seconds:** show a recognizable mid-battle HP/resource state, press Esc, return to menu, choose Continue, and show the same encounter/state. Save `Docs/Images/continue-run.gif`. Caption it explicitly as **in-session resume**, not a disk save/load demonstration.

Prefer readable, short clips over a large animated hero banner. Avoid rapid flashing in the selected sequence and preserve enough time to understand the action.

## Capture checklist

- [ ] Confirm the final public title and displayed version before recording.
- [ ] Verify English text fits; do not edit missing translations into a screenshot.
- [ ] Capture current code and record the commit/build identifier once committed.
- [ ] Check image readability at GitHub content-column width.
- [ ] Keep a source recording for each edited clip.
- [ ] Record any developer-mode setup in the image provenance note.
- [ ] Add the verified itch.io URL once the downloadable build has been tested.
