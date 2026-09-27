# AGENTS.md

## Project Overview

This project is a card battle prototype built with:

* Godot 4.6 .NET
* C#
* Desktop platform
* Chinese UI

All code should be written in C# and follow Godot 4.6 best practices.

---

## Development Principles

* Prefer simple and maintainable code.
* Avoid over-engineering.
* Use clear class names and folder structures.
* Keep UI logic and game logic separated.
* Do not introduce unnecessary plugins or external dependencies.
* Explain major architecture decisions in comments.

---

## UI Requirements

### Language

The entire game must display in Simplified Chinese.

No garbled text or missing glyphs are allowed.

### Font

Use:

NotoSerifCJKsc-Black.otf

as the default UI font.

All Labels, Buttons, Card Titles and Descriptions must use this font.

### Resolution

Default resolution:

2560 × 1440

Project Settings:

* Display → Window → Width = 2560
* Display → Window → Height = 1440
* Content Scale Mode = Canvas Items
* Content Scale Aspect = Expand

### Font Sizes

* HP: 32px
* Mana: 28px
* Card Title: 24px
* Card Description: 18px
* Buttons: 22px

### Visual Style

The UI should resemble a modern digital card game.

Requirements:

* Clear Chinese text
* High readability
* Large interface elements
* Rounded corners
* Clean layout
* Dark background
* Strong contrast

Do not generate debug-style UI.

---

## Game Design

Create a turn-based card battle game.

### Players

There are two battle sides:

* Player
* Enemy

Initial stats:

* HP = 40 for both sides in the current whiteboard battle mode
* Mana = 1
* Mana does not have a maximum value

Display HP and Mana for both sides.

---

## Character System

Characters are managed through `CharacterData`, `CharacterInstance`, and `CharacterDatabase`.

Do not hardcode character HP, gender, name, or default skill lists in `BattleManager`.

`CharacterData` contains:

* `Id`
* `Name`
* `Gender`
* `MaxHp`
* `SkillIds`

Current characters:

* `lvbu`: 吕布, Male, 40 HP, 无双
* `machao`: 马超, Male, 40 HP, 必中
* `zhaoyun`: 赵云, Male, 40 HP, 龙胆
* `diaochan`: 貂蝉, Female, 30 HP, 闭月
* `luxun`: 陆逊, Male, 30 HP, 连营

`CharacterInstance` is retained for future character selection and character-specific battle state. In the current whiteboard battle mode, combat HP is initialized to 40 directly on `Player`; battle code must not mutate `CharacterData`.

Default skills are not loaded from the selected character at battle start yet. Both sides start without skills, and the skill debug panel is the only way to add or remove runtime skills during the current prototype phase.

The current battle uses a whiteboard runtime character with 40 HP and no skills. Character names, portraits, and default skills are not shown or applied in the battle UI. Future character selection should only change selected character ids, not character data.

---

## Card Classification

Cards expose both `CardCategory` and `CardSubType`.

Categories:

* `Attack`: 杀, 火杀, 雷杀, 必中杀
* `Defense`: 闪
* `Recovery`: 桃, 酒
* `Trick`: 顺手牵羊, 无懈可击
* `Resource`: 费
* `Enhancement`: 酒

酒 intentionally has both `Recovery` and `Enhancement` categories.

## Card Pool

The card pool contains nine playable card types plus the reserved 必中杀 subtype:

### 杀

Cost:

1 Mana

Effect:

Deal 10 damage to the opponent.

Rules:

* Cannot be played without sufficient Mana.
* Consumes 1 Mana.

---

### 火杀

Cost:

1 Mana

Effect:

Deal 10 damage to the opponent.

Rules:

* Attack card.
* 克制普通杀.
* Can be blocked by 闪.

---

### 雷杀

Cost:

2 Mana

Effect:

Deal 10 damage to the opponent.

Rules:

* Attack card.
* 克制火杀.
* Cannot be blocked by 闪.
* Is countered by 普通杀.

---

Attack cards also expose `AttackType`:

* `Sha`: 杀
* `FireSha`: 火杀
* `ThunderSha`: 雷杀
* `DirectSha`: 必中杀

### 必中杀

Cost:

1 Mana

Effect:

Special Sha attack. Deal 10 damage, benefits from 酒 damage, and is in scope for future “all Sha” effects such as 无双.

Rules:

* `CardCategory = Attack`
* `AttackType = DirectSha`
* 克制 杀, 火杀, 雷杀.
* Ignores 闪, 桃护盾, and 酒护盾.
* Can be cancelled by 无懈可击 before damage.
* It is shown in the action area only while the player has the 必中 skill.

---

### 闪

Cost:

0 Mana

Effect:

Play one defense card for the current simultaneous reveal.

闪 blocks 杀 and 火杀.

闪 cannot block 雷杀.

---

### 费

Cost:

0 Mana

Effect:

Gain +1 Current Mana.

Mana is not restored or increased automatically between turns.

---

### 桃

Cost:

2 Mana

Effect:

If 桃 blocks damage this round, prevent one damage instance and do not heal.

If 桃 does not block damage this round, heal 10 HP up to the 30 HP maximum.

---

### 酒

Cost:

1 Mana

Effect:

Gain one wine status for next round and one universal shield for this round.

Rules:

* 酒 can stack by repeated clicks within the stack window.
* Each unbroken wine layer gives +1 attack damage next round.
* Wine status lasts for one round only.
* If a wine shield blocks damage, the corresponding wine status is removed and does not carry into next round.

---

### 顺手牵羊

Cost:

1 Mana

Effect:

Steal all current Mana from the opponent.

Rules:

* The 1 Mana label is a resource threshold, not a consumed cost.
* Resolution succeeds only if the user had more than 0 Mana before battle resolution, or the user gained Mana from `费` during the same round.
* If the user had 0 Mana before resolution and did not gain Mana from their own `费`, 顺手牵羊 fails and transfers nothing.
* A failed 顺手牵羊 logs `顺手牵羊发动失败` and `未满足资源窃取条件`.
* When it succeeds, transfer the opponent's current Mana without subtracting the user's original Mana.
* Does not provide attack or defense.
* Does not stack.

---

### 无懈可击

Cost:

0.5 Mana

Effect:

Counter 顺手牵羊, 无懈可击, 必中杀, and 雷杀.

Rules:

* Trick card.
* Does not block 杀, 火杀, 闪, 桃, 酒, or 费.
* Does not stack.

---

## Turn System

Each turn:

1. StartPhase / 回合前: trigger start-of-round effects, generated effects, buffs, and turn counters. No card can be played in this phase.
2. BattlePrePhase / 战斗回合前: the player chooses one fixed action from 杀, 火杀, 雷杀, 闪, 桃, 酒, 顺手牵羊, 无懈可击, 费. Enemy AI chooses one action, stack input resolves, and legality is checked. This phase does not deal damage.
3. BattlePhase / 战斗回合: both cards are revealed, then attack, defense, resource, heal, and normal battle effects resolve.
4. BattlePostPhase / 战斗回合后: trigger future post-combat responses such as after-damage or after-hit skills.
5. EndPhase / 回合后: clear temporary states, expire buffs, clear wine damage status, and trigger end-of-turn effects.

There is no manual end-turn button and no separate enemy turn.

Display the current round number and phase.

`BattleContext.TurnCounter` starts at 1, increments once after a complete StartPhase → BattlePrePhase → BattlePhase → BattlePostPhase → EndPhase loop, and resets to 1 in `RestartBattle()`.

---

## Trigger System

The battle flow is driven by `TriggerManager.RaiseTrigger(TriggerTiming timing, BattleContext context)`.

`RaiseTrigger` must collect matching effects, enqueue them into `EffectQueue`, sort by `EffectPriority`, then execute them.

Future cards, skills, buffs, debuffs, enchantments, passive effects, and hero skills must be implemented as trigger effects with:

* `TriggerTiming Timing`
* `EffectPriority Priority`
* `Execute(BattleContext context)`

Effect priority order:

1. `Immediate`
2. `Highest`
3. `High`
4. `Mid`
5. `Low`
6. `Lowest`

Set `BattleContext.EnableEffectDebugLog` to true to record timing/priority execution order in trigger logs for debugging.

Available timings:

* `OnGameStart`
* `OnTurnStart`
* `OnBattlePrePhase`
* `OnCardSelected`
* `OnResourceChanged`
* `OnBattleReveal`
* `OnBattlePhase`
* `OnBeforeDamage`
* `OnDamage`
* `OnDamageTaken`
* `OnBattleEnd`
* `OnBattlePostPhase`
* `OnDying`
* `OnDeath`
* `OnTurnEnd`

Do not add new cross-system direct calls for future effects; register a trigger effect instead.

---

## Skill System

Skills are split by category:

* General skill
* Character-exclusive skill

Skills have rarity:

* Common
* Rare
* Legendary

Skills have one or more kinds:

* Passive
* Active
* Card
* Enchantment

Player skills are displayed on the right side of the player panel as small circular buttons. The button text uses the first two Chinese characters of the skill name until skill art is added.

Clicking a skill button opens a skill detail popup.

Skills from the selected character are equipped automatically at battle start.

Skills are tested through the top-bar `技能调试` panel:

* Add an extra skill from the current skill pool.
* Remove any currently owned player skill.
* Added skills take effect immediately when their trigger effect checks the player's owned skill list.
* The debug panel does not save data; restarting the battle or closing the game clears manually added extra skills, while character default skills are reloaded.

Current skills:

### 无双

* Character-exclusive: 吕布
* Rarity: Legendary
* Kind: Passive / Enchantment
* Current behavior: display only; does not modify damage.
* Future effect when enabled: `OnDamage` / `Lowest`, all kill-family attack card final damage ×2.

### 闭月

* Character-exclusive: 貂蝉
* Rarity: Rare
* Kind: Passive
* Trigger: `OnBattlePostPhase`
* Priority: `Highest`
* Current behavior: when manually added through the skill debug panel, gain 1 Mana during BattlePostPhase on even-numbered turns only.

### 必中

* Character-exclusive: 马超
* Rarity: Rare
* Kind: Card
* Current behavior: display only; does not modify combat yet.
* Future direction: used as a card skill around the 必中杀 system.

### 连营

* Character-exclusive: 陆逊
* Rarity: Rare
* Kind: Passive
* Trigger: `OnResourceChanged` to prepare the skill, `OnBattlePrePhase` to activate the next-round free kill chance
* Priority: `High`
* Current behavior: when manually added through the skill debug panel, the first time the player spends Mana down to 0 in a battle, gain `连营已准备`. At the next BattlePrePhase, gain one free use of ordinary `杀` for that round only. The free chance applies only to the first ordinary `杀`, does not apply to 火杀 / 雷杀 / 必中杀 or other cards, and expires at TurnEnd if unused.

### 龙胆

* Character-exclusive: 赵云
* Rarity: Rare
* Kind: Passive / Card
* Trigger: `OnBattlePostPhase`
* Priority: `High`
* Current behavior: when manually added through the skill debug panel, if the player used `闪` against an attack this round, enqueue a realtime `ReactionWindow` asking whether to use `杀`, `火杀`, `雷杀`, or `放弃`. `杀` costs 0, while `火杀` and `雷杀` each cost 1 Mana and are disabled when unaffordable. Choosing an attack invalidates the player's damage from the original exchange and recalculates the confrontation as the selected attack card against the enemy attack.

---

## Reaction Window System

Response skills, counterattack skills, equipment triggers, and special settlements must use the shared Reaction Window System.

Core types:

* `IReaction`: declares `Timing`, `Priority`, `Title`, and `Options`
* `ReactionOption`: one selectable response option with enabled state and resolution callback
* `ReactionQueue`: collects reactions and resolves them by `EffectPriority`
* `ReactionWindow`: generic realtime response control for all reaction choices
* `BattleRules.CanPlayReactionCard`: reaction-specific card availability; do not reuse normal action-card affordability for reaction choices

Flow:

1. Battle effects detect a valid response during their `TriggerTiming`.
2. They enqueue an `IReaction` into `BattleContext.Reactions`.
3. `BattleManager` processes `ReactionQueue` after `OnBattlePostPhase` effects are collected.
4. `ReactionWindow` displays the skill activation text and `ReactionProgressBar` in the lower-middle battlefield area, runs a 1.5 second countdown, and renders selectable options through ReactionMode in the action bar.
5. `BattleManager` enters ReactionMode and rebuilds the bottom ActionBar from `ReactionOption.CardType`, not from normal action cards.
6. Enabled options are highlighted; disabled options are greyed out with text such as `火杀（费用不足）`.
7. The selected `ReactionOption` resolves immediately and writes battle / skill / system logs.
8. If the timer expires, the fallback `放弃` option resolves automatically.

Do not add one-off popups such as `ShowLongdanWindow`; new response content should create a new `IReaction` and enqueue it. Do not use blocking modal windows for reactions.

---

## Restart

The top bar contains a `重新开始` button.

Clicking it calls `BattleManager.RestartBattle()` and resets the current battle without reloading the app.

---

## Battle Feedback And Logs

The top bar buttons are ordered:

* `技能调试`
* `战斗日志`
* `重新开始`

Normal combat flow should not pause to show a large settlement result panel.

After both sides reveal cards, show a lightweight non-blocking center battlefield popup for about 1.5 seconds:

```text
敌方：火杀

玩家：雷杀
```

The next BattlePrePhase may begin while this reveal popup is still fading. It must not lock input or delay the next action.

Use floating popups for immediate feedback:

* Damage near the avatar: `-10`
* Healing near the avatar: `+10`
* Mana gain/loss near the mana label: `+1费`, `-1费`

Each popup floats upward, fades out, starts with a slight scale pop, and lasts about 1 second. Damage and healing popups should be very prominent, around 48-72px, with outline and shadow.

All full settlement details must be written to `BattleLogWindow`.

`BattleLogWindow` uses the V2 card log layout:

* Top tools: search input and `导出日志`
* Tabs: `战斗`, `技能`, `系统`
* Each round is a collapsed card by default, e.g. `▶ 第26回合`
* The `战斗` tab shows only player card, enemy card, damage, healing, mana changes, and battle results
* Each battle card has `▶ 查看详细调试信息` to reveal TriggerTiming / EffectPriority / EffectQueue / card order / skill order
* The `技能` tab shows skill trigger records such as `【闭月】`
* The `系统` tab shows internal TriggerTiming and priority debug records
* Search must match card names, skill names, TriggerTiming names, and result text
* Export writes a complete `battle_log.txt` containing all battle, skill, and system details

Each round log should include:

* Round number
* Player and enemy cards
* Round result text
* TriggerTiming / EffectPriority debug order
* Card settlement order
* Skill trigger order

The battlefield left-side turn counter area also shows the latest three battle reports. The main battle area should only show lightweight prompts, recent reports, floating feedback, and game-over text, not full settlement logs.

---

## Dying System

When HP reaches 0 or below, the character enters `OnDying` first.

Rescue order during `OnDying`:

1. Use 桃 if current Mana is at least 2.
2. If 桃 cannot be used, use 酒 if current Mana is at least 1 and `HasUsedWineRevive` is false.
3. If neither rescue succeeds, trigger `OnDeath`.

桃 rescue costs 2 Mana, heals 10 HP, and leaves dying state if HP becomes greater than 0.

酒 rescue costs 1 Mana, heals 10 HP, can only be used once per game, and sets `HasUsedWineRevive` to true.

If the character is rescued above 0 HP during `OnDying`, the game continues.

If HP is still 0 after `OnDying`, `OnDeath` triggers. Only `OnDeath` marks the character truly dead and checks victory.

---

## Action Interaction

Players must be able to:

* Click one of eight fixed action cards.
* Stack paid cards by clicking the same card again within 0.5 seconds.
* Resolve immediately when the stack window expires or when the next stack is unaffordable.
* Reveal both sides and resolve automatically.

Requirements:

* Clear click feedback.
* Disable action cards while reveal and resolution are running.
* Do not allow 0-cost cards 闪 or 费 to stack.
* Do not allow 顺手牵羊 to stack.
* Do not use drag-and-drop interaction.

---

## Action System

There is no deck or hand system.

The action area always displays:

* 杀
* 火杀
* 雷杀
* 必中杀, only while the player has 必中
* 闪
* 桃
* 酒
* 顺手牵羊
* 无懈可击
* 费

Do not display deck size, hand size, draw buttons, or random card choices.

---

## Mana Display

Create a dedicated mana display.

Requirements:

* Show current mana.
* Update in real time.

Example:

Mana: 3

Both Player and Enemy should have mana indicators.

---

## Enemy AI

Simple AI:

The enemy uses a simple weighted decision pool:

* 0 Mana: strongly prefer 费, then 闪, then any affordable reaction card.
* 1 Mana: randomly consider 杀, 闪, 费, 顺手牵羊, and 无懈可击.
* 2+ Mana: add 火杀, 雷杀, 桃, and 酒 to the decision pool.
* Low HP increases 桃 weight.
* High opponent Mana increases 顺手牵羊 weight.
* Repeatedly choosing the same card reduces that card's next weight, so the AI should not loop 闪 forever.

Keep AI simple and weighted-random; do not add complex prediction yet.

---

## Recommended Project Structure

Scenes/

* Main.tscn
* Battle.tscn
* Card.tscn

Scripts/

* BattleManager.cs
* TriggerTiming.cs
* TriggerManager.cs
* BattleContext.cs
* BattleAction.cs
* RoundResult.cs
* BattleRules.cs
* Skill.cs
* Player.cs
* Card.cs
* EnemyAI.cs
* CardUI.cs

Assets/

* Fonts/
* Sprites/

---

## Coding Rules

* Use partial classes.
* Use nullable reference types when possible.
* Use signals/events instead of tight coupling.
* Avoid magic numbers.
* Create constants for damage, HP and mana values.
* Keep methods small and focused.
* Comment only when useful.

Before implementing a feature, first inspect the existing project structure and reuse existing code whenever possible.
