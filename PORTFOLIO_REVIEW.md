# Portfolio review

## Audit scope and evidence

This review inventoried the repository's tracked top-level areas, existing Markdown documentation, source/configuration, art folders, test scenes, local Git history, and supplied development notes. Representative implementations were read directly for the systems featured in the README. It is not a line-by-line security review or a full playthrough.

The local history available at audit time contained two commits: the initial import and README translation. Existing uncommitted game/art changes were preserved. This documentation pass does not rename runtime files, change mechanics, delete assets, choose a license, publish a release, or push to GitHub.

### What was already present

- A technical English README, Chinese architecture/contribution documents, module READMEs, and an art-integration guide.
- C# combat, effects, reactions, AI, rewards, event/shop/progression, localization, and presentation code.
- Godot regression scenes, including Longdan damage, damage previews, matchup rules, and menu/continue state.
- A substantial local asset library, including generated art with generation notes, but no curated gameplay screenshot gallery found by the filename inventory.
- Separate settings/codex persistence and in-memory run/hero-unlock state.
- A Godot/.NET `.gitignore` excluding engine caches, bin/obj, local tooling, common credential files, and exported builds.

### Safe changes made

- Replaced the landing README with an English portfolio overview and direct code links.
- Preserved the previous technical README in [Docs/DevelopmentGuide.md](Docs/DevelopmentGuide.md).
- Added [engineering case studies](Docs/Engineering.md), a [devlog index](Docs/Devlog/README.md), a polished 2.0 log, and the other attachment's original notes plus a thematic summary.
- Added two genuine runtime UI captures with [provenance](Docs/Images/README.md); no synthetic gameplay images.
- Added a [capture plan](SCREENSHOT_PLAN.md) and this prioritized review.
- Built the working tree and ran selected regressions, with results and limitations documented in Engineering Notes.

## Game development recruiter

**What stands out:** A playable Godot/C# project with multi-enemy combat, reusable reactions, character/build systems, integrated art, and bug-focused tests. The Longdan and damage-preview examples provide specific interview entry points.

**What is missing:** A verified direct download, a short combat clip, a known-good build identifier, tested OS requirements, and a clear final title. Current screenshots show real UI but do not yet prove the complete player loop. A small, reliable demonstration is more persuasive than a longer list of features.

## Software engineering recruiter

**Strong evidence:** Timing/priority scheduling, explicit settlement ordering, preview side-effect tests, reusable reward/choice contracts, and a concrete scene-lifecycle bug investigation. Source links let an interviewer inspect the work.

**Gaps:** No checked-in CI workflow was found; only selected local regressions were run. Large coordinator classes and special cases mean the repository should not claim perfect separation of concerns. Durable run save/load is missing. Reproducibility of a clean clone still needs testing on a fresh machine.

**For data-related roles:** The AI has inspectable weights and conditions, and logs expose settlement behavior. There is no evidence here of an analytics product, SQL pipeline, ML model, player-study dataset, or measured balance experiment. Do not describe this as a data-science project. A later seeded simulation study with real recorded results could create a defensible data-oriented case study.

## Game designer

**Demonstrated:** Fixed actions emphasize resource allocation and counter choices rather than draw variance. Hero skills and equipment alter a common ruleset; initial-event costs and chapter progression introduce longer-term trade-offs. State-aware enemy rules support differentiated encounters.

**Needs documentation:** The intended player experience, why particular counter rules/costs were chosen, an annotated example turn, difficulty goals, and playtest observations. Historical balance notes show iteration, but do not establish that the game is balanced or that a change improved player outcomes.

## Repository hygiene findings

- **Ignored versus tracked:** Local Windows exports and Godot/.NET caches exist. A tracked-path scan found none of the checked cache/build/export patterns (`.godot`, bin/obj, .exe/.pck/.zip, .DS_Store, .log) in Git. They were not deleted.
- **Secrets:** A limited filename/content-pattern scan found no matches for common GitHub/OpenAI/AWS token patterns or private-key headers in the scanned source/text area. No credential-bearing lines were printed. This is not a security guarantee or a history-wide secret scan.
- **Licensing:** No repository-wide LICENSE or tracked font-license file was found. Code licensing is the owner's decision. Review font/third-party/generated asset provenance and required notices before redistribution; do not apply an assumed code license to all art.
- **Naming:** The portfolio brief says Forgot Kingdom; runtime branding says Forgotten Three Kingdoms / 旧日三国; internal names include rouge3c. The discrepancy is disclosed, not silently changed.
- **Version history:** The author calls one log 2.0, while the menu shows v0.4.0. No release date/tag was inferred. The long attachment is intentionally unversioned.
- **Documentation drift:** ARCHITECTURE.md still describes parts of presentation as scaffolding without real animation, while current code includes runtime effects. It is retained as earlier guidance and should be updated separately after a focused review.
- **Assets:** Some generated boss variants are retained alongside current art. Do not bulk-delete on filename alone; first audit runtime references and provenance notes.
- **GitHub About:** Remote repository description/topics were not inspected or changed. Suggested description below is ready to paste after naming confirmation.
- **Publication:** No itch.io URL was found in the inspected local documentation/code. Presence of local exported builds does not verify a public release.

Suggested repository description:

> Solo-developed Godot/C# strategy card roguelike with simultaneous actions, character-driven builds, and cyberpunk Three Kingdoms-inspired art.

Suggested topics: `godot`, `csharp`, `card-game`, `roguelike`, `game-development`, `portfolio`.

## Prioritized next steps

### P0 — Before sharing with recruiters

- [ ] **Owner:** supply the itch.io URL; verify download, startup, and a short run on the advertised OS. Add a real “Play” link only afterward.
- [ ] **Owner:** confirm the public title, version scheme, developer name/contact, and which code/art may be redistributed.
- [ ] **Owner + project:** select code licensing and collect required third-party asset/font notices. Do not assume generated or bundled assets have identical terms.
- [ ] **Capture:** add a normal combat screenshot and Longdan clip from [the plan](SCREENSHOT_PLAN.md).
- [ ] **Verify:** clone into a fresh folder, import/build, and test start → character → fate → map → battle → menu → continue. Record actual outcomes and known issues.
- [ ] **Publish deliberately:** review pending changes, commit the complete dependency set, and then push. The current tree includes pre-existing uncommitted art/runtime changes; documentation/screenshots can be ahead of remote main until those are included.

### P1 — Strong improvement

- [ ] Add a reproducible headless regression runner and CI with a pinned .NET Godot version; keep visual tests separate.
- [ ] Document a representative turn with its counter rules, costs, and outcome; distinguish design intent from tested player response.
- [ ] Capture map/build screens and recapture the two UI examples in English during normal play.
- [ ] Reconcile architecture documentation with current runtime boundaries and prioritize small extractions from large managers.
- [ ] Add a release checklist and known-issues page, including in-session-only Continue and hero-unlock persistence limits.
- [ ] Confirm dates/versions for historical notes without rewriting them to imply a Git history that does not exist.

### P2 — Nice to have

- [ ] Record a 60–90 second narrated walkthrough linked from the play page.
- [ ] Add measured balance experiments using seeded encounters and a documented sampling method; publish data and limitations if pursuing data internships.
- [ ] Extend regression checks to additional viewport sizes and language combinations.
- [ ] Consolidate redundant generated assets only after a reference/provenance audit.

## Owner input still needed

1. Verified itch.io/download link and supported platforms.
2. Final public title and meaning of the 2.0 versus v0.4.0 labels.
3. Public developer/contact details and preferred attribution.
4. License choice and third-party asset provenance.
5. Confirmed dates/version for the longer devlog, if known; otherwise retain “unspecified.”
