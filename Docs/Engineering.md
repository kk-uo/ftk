# Engineering notes

[Project overview](../README.md) · [Portfolio review](../PORTFOLIO_REVIEW.md)

This is a source-checked walkthrough of the current working tree, not a claim that the architecture is perfectly modular. The project is a Godot 4.6.3 .NET desktop game targeting .NET 8. Rules and definitions are C#; scenes, assets, and bilingual JSON text complete the runtime.

## 1. Simultaneous actions with explicit settlement order

**Problem:** When both sides choose a card, cost payment, defensive layers, counters, attacks, healing, and resource stealing can interfere with one another. Multi-enemy encounters add per-target state.

**Design:** A central battle-resolution effect coordinates the exchange. Individual effects declare a trigger timing and priority rather than relying solely on registration order.

**Implementation:** [BattlePhaseResolutionEffect](../Scripts/Battle/BattleResolver.cs) snapshots pre-action resources, pays costs, prepares defenses, resolves counters/actions, then applies resource/healing, wine, and steal handling. [TriggerManager and EffectQueue](../Scripts/TriggerManager.cs) collect matching effects, sort them by priority, and record execution information. [BattleContext](../Scripts/BattleContext.cs) carries the encounter, actions, and round result.

**Result:** The settlement sequence and effect order can be inspected in one place and reproduced in targeted tests. [CardMatchupCoverageRegression](../Scripts/Tests/CardMatchupCoverageRegression.cs) checks that matchup descriptions and actual rules agree; [UnifiedShieldRegression](../Scripts/Tests/UnifiedShieldRegression.cs) contains shield cases.

**Trade-off:** The resolver still contains skill-specific branches. This is a practical shared pipeline, not a fully generic rules engine or a claim of deterministic replay.

## 2. Damage previews without spending the real attack

**Problem:** A card's printed base damage is insufficient once equipment, skills, elemental bonuses, multipliers, and target conditions interact. Running the full attack just to show a tooltip could consume state.

**Design:** Reuse preview-aware modifier calculations in a temporary context while avoiding live damage application and resource-consuming phases.

**Implementation:** [DamagePreviewService](../Scripts/DamagePreview/DamagePreviewService.cs) evaluates targets and builds a breakdown. Its temporary context shares real unit references; it is not a deep-cloned simulation. Preview safety therefore depends on participating effects respecting preview behavior. [DamageModifierPipeline](../Scripts/DamageModifier.cs) collects additive bonuses before processing multipliers/reductions/caps.

**Result:** The preview can show target-specific totals with the same modifier rules used by combat. [DamagePreviewRegression](../Scripts/Tests/DamagePreviewRegression.cs) checks modifier interactions, multi-target totals, one-shot equipment preservation, and no live-state side effects for its covered cases.

**Trade-off:** This is not a guarantee that all future effects are automatically preview-safe. New effects need a matching regression; random cancellation/defense behavior is not a full predicted next-turn simulation.

## 3. Reusable reactions: Zhao Yun's Longdan

**Problem:** A skill can offer a paid or free counterattack after an exchange. A separate blocking dialog per skill would duplicate input, timing, affordability, and fallback behavior.

**Design:** Represent a response with an interface, priority, and selectable options; let the common reaction window/action bar render the choice.

**Implementation:** [IReaction, ReactionOption, and ReactionQueue](../Scripts/Reaction.cs) hold the contract and options. [LongdanReaction](../Scripts/Reactions/LongdanReaction.cs) implements the skill-specific response, while [ReactionWindow](../Scripts/ReactionWindow.cs) handles the shared timed UI.

**Result:** Reaction rules can be exercised without clicking through a full game. [LongdanReactionDamageRegression](../Scripts/Tests/LongdanReactionDamageRegression.cs) reproduces a Dodge action without an explicit target and verifies both counterattack damage and restoration of damage invalidated by the response.

**Trade-off:** Timing, cancellation, and lethal-damage responses still need explicit coordination with the battle flow; the queue alone does not solve them.

## 4. State-aware enemy behavior

**Problem:** Enemies must respect available actions and resources while feeling different from one another, without simply reading the player's future decision.

**Design:** Start with configurable action weights, adjust for state, filter legal choices, and sample an action. Add specific behavior for encounters that require it.

**Implementation:** [EnemyAI](../Scripts/EnemyAI.cs) exposes an evaluation path and uses HP, resources, statuses, action history, and enemy definitions. [EnemyActionWeights](../Scripts/EnemyActionWeights.cs) stores the weights. Special handling exists for enemies such as the Phantom Tentacle and for temporary skill states.

**Result:** The code provides inspectable reasons for changing action preferences; it is a weighted/rule-based system, not trained AI. Example regression sources include [ShuHanBossAiRegression](../Scripts/Tests/ShuHanBossAiRegression.cs) and [WulongGuanxingRegression](../Scripts/Tests/WulongGuanxingRegression.cs).

**Trade-off:** These examples do not establish a measured win-rate distribution, optimal strategy, or overall difficulty balance. Seeded simulation and playtest evidence remain future work.

## 5. Run choices and progression

**Problem:** Events, shops, initial choices, and boss rewards all need to change run state and sometimes request another player choice.

**Design:** Separate reward descriptions and candidate generation from their presentation.

**Implementation:** [RewardSystem](../Scripts/RewardSystem.cs) defines reward actions/sequences/results; [ChoiceProviders](../Scripts/ChoiceProviders.cs) builds candidates; [ChoicePanel](../Scripts/ChoicePanel.cs) displays them. [GameManager](../Scripts/GameManager.cs) owns run progression; [EventDatabase](../Scripts/EventDatabase.cs), [ShopManager](../Scripts/ShopManager.cs), and [InitialEventRandomizer](../Scripts/InitialEventRandomizer.cs) provide content and selection rules.

**Result:** Several reward sources share execution concepts and choice UI. [BossEquipmentRewardRegression](../Scripts/Tests/BossEquipmentRewardRegression.cs) and [EventChapterIsolationRegression](../Scripts/Tests/EventChapterIsolationRegression.cs) are examples of focused checks.

**Design consequence:** Build choices operate at different timescales: resource spending within a round, HP/equipment trade-offs within a run, and hero-unlock conditions across runs in the same application session.

**Trade-off:** Not all content is declarative, and GameManager remains large. Hero unlocks are tracked in [HeroUnlockProgress](../Scripts/HeroUnlock/HeroUnlockProgress.cs) memory; they are not currently a complete persistent progression save.

## 6. Return to menu without losing the live scene

**Problem:** Returning to the main menu must retain the live encounter without leaving the player's status panel visible over the menu.

**Design:** Keep the run scene mounted but hidden and disabled, preserving its process mode and independently layered UI visibility.

**Implementation:** [MainFlow](../Scripts/MainFlow.cs) stores the suspended screen, recursively hides CanvasLayer nodes, hides tooltips, and restores each layer's prior visibility when resuming. A parent Control's visibility does not hide an independent CanvasLayer.

**Result:** [ContinueGameRegression](../Scripts/Tests/ContinueGameRegression.cs) verifies the screen survives menu navigation, previously hidden layers remain hidden, and the battle status layer restores correctly.

**Limit:** This is in-session suspension, not serialization. Closing the application loses the suspended run. [CodexService](../Scripts/Codex/CodexService.cs) writes separate collection data, and [Localization](../Scripts/Localization.cs) persists language preference; neither saves the full run.

## 7. Visual integration and localization

[EffectPreset](../Scripts/Presentation/Effects/EffectPreset.cs), [EffectDatabase](../Scripts/Presentation/Effects/EffectDatabase.cs), and [CombatVisualProfileDatabase](../Scripts/Presentation/CombatVisualProfile/CombatVisualProfileDatabase.cs) make groups of effects and attack visuals reusable by ID. Some visuals also remain in BattleManager partial classes; complete separation is an architectural direction, not an accomplished fact.

The main menu combines a clean background plate with an independent cape layer, regional mesh motion, lighting, procedural weather, and a CRT overlay. See [asset provenance and implementation notes](../Assets/Backgrounds/MainMenu/README.md). These are lightweight 2D techniques, not full character skeletal animation.

[Localization](../Scripts/Localization.cs) loads English and Simplified Chinese JSON tables, uses fallback lookups, and notifies listeners on language changes. These hooks support localized UI; they do not prove that every content string has been audited.

## Verification

During this documentation audit, the existing working tree was built and the following selected regressions were run on macOS with Godot 4.6.3 .NET. These results are local checks, not CI, a full test-suite run, or a coverage measurement.

| Check | Observed result |
| --- | --- |
| `dotnet build rouge3c.sln --no-restore -v:minimal` | Succeeded, 0 warnings, 0 errors |
| `LongdanReactionDamageRegression.tscn` | PASS, 8 assertions |
| `DamagePreviewRegression.tscn` | PASS, 61 assertions |
| `CardMatchupCoverageRegression.tscn` | PASS, 47 assertions |
| `ContinueGameRegression.tscn` | PASS |
| `HeroChipVisualRegression.tscn` | PASS; rendered capture inspected |
| `InitialFateVisualRegression.tscn` | PASS; rendered capture inspected |

Example commands, after asset import and build:

```sh
godot --headless --path . res://Scenes/Tests/ContinueGameRegression.tscn
godot --headless --path . res://Scenes/Tests/CardMatchupCoverageRegression.tscn
# Visual capture requires a rendering window, not --headless.
godot --path . --rendering-method gl_compatibility --resolution 1280x720 res://Scenes/Tests/HeroChipVisualRegression.tscn
```

Replace `godot` with the installed .NET editor executable if it is not on PATH. Some tests initialize global game state; use a disposable test profile for a broader suite rather than assuming all tests are isolated from user data.
