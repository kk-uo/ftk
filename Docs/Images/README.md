# Portfolio image provenance

These files are direct Godot viewport captures made while preparing the portfolio documentation. They were copied without retouching from the existing regression scenes' PNG output, not generated as mockups.

| File | Source scene | State shown |
| --- | --- | --- |
| [combat-system.png](combat-system.png) | [PortfolioCombatCapture](../../Scenes/Tests/PortfolioCombatCapture.tscn) | Instantiates the real `Battle.tscn` with the standard Gate Guard debug encounter. It shows the normal battle HUD and fixed action-card bar. |
| [character-selection.png](character-selection.png) | [HeroChipVisualRegression](../../Scenes/Tests/HeroChipVisualRegression.tscn) | Instantiates the actual character-selection controller and selects the first unlocked chip. It checks unlocked-before-locked ordering. |
| [initial-fate.png](initial-fate.png) | [InitialFateVisualRegression](../../Scenes/Tests/InitialFateVisualRegression.tscn) | Instantiates the actual initial-event controller and displays three normally generated options. It checks centering, bounds, and no reroll on text refresh. |

Capture conditions: Godot 4.6.3 .NET, Metal rendering, 1280 × 720 output, and the in-game `en_US` localization table. These are direct runtime captures, not mockups, composites, or standalone background art. The battle capture uses a declared debug encounter so that the standard live battle UI has a reproducible opponent; it is not presented as a completed run. The underlying visual assets include AI-generated art.

The production project normally uses its configured renderer. Exact lighting can differ when captured on another renderer or platform.

For the next captures, follow [SCREENSHOT_PLAN.md](../../SCREENSHOT_PLAN.md). Keep only a small selection in the landing page, prioritize English real-gameplay frames, and retain provenance whenever replacing an image.
