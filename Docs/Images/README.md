# Portfolio image provenance

These files are direct Godot viewport captures made while preparing the portfolio documentation. They were copied without retouching from the existing regression scenes' PNG output, not generated as mockups.

| File | Source scene | State shown |
| --- | --- | --- |
| [character-selection.png](character-selection.png) | [HeroChipVisualRegression](../../Scenes/Tests/HeroChipVisualRegression.tscn) | Instantiates the actual character-selection controller and selects the first unlocked chip. It checks unlocked-before-locked ordering. |
| [initial-fate.png](initial-fate.png) | [InitialFateVisualRegression](../../Scenes/Tests/InitialFateVisualRegression.tscn) | Instantiates the actual initial-event controller and displays three normally generated options. It checks centering, bounds, and no reroll on text refresh. |

Capture conditions: Godot 4.6.3 .NET, OpenGL compatibility rendering, 1280 × 720 output, Simplified Chinese. They are UI integration-test captures, not recorded gameplay sessions, completed runs, or historical 2.0 screenshots. The underlying visual assets include AI-generated art.

The production project normally uses its configured renderer; the compatibility renderer was used for these captures. Exact lighting can differ by renderer.

For the next captures, follow [SCREENSHOT_PLAN.md](../../SCREENSHOT_PLAN.md). Keep only a small selection in the landing page, and retain provenance whenever replacing an image.
