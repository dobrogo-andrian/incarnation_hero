# CLAUDE.md

## Project state

Wuxia/xianxia turn-based roguelike (working title "incarnation_hero"). Solo developer, target is a public Steam demo. Design docs are Markdown in `docs/` (Ukrainian), one file per section. Start from the index `docs/README.md` and read only the files you need.

- `docs/progress.md`: current stage, next step, stage checklist, gotchas from earlier sessions, short session log. See Workflow.
- `docs/gdd/`: game design document (GDD), the source of truth for gameplay rules. Section refs like "§4.5" point here (`grep "§4.5" docs/gdd`).
- `docs/tech/`: technical design (stack, architecture, roadmap, risks). Tech sections have no `§` and are cited as "tech 2.8". Decisions D* and working assumptions A* are in `docs/tech/decisions.md`; open questions Q* are in `docs/tech/open-questions.md`.
- `.claude/agents/game-designer.md`: design-partner subagent for GDD sessions.
- `sources/archive/*.docx`: the original Word files. Read-only, don't edit or use as a source.

If code contradicts the docs, or a task touches an open question (Q*), point it out and don't decide it silently. When a design decision changes, edit the matching `.md` file. When a Q is resolved, move it into `decisions.md` as a D or A entry.

## Workflow

1. **Start of session:** read `docs/progress.md`. Items marked done there are trusted: don't re-verify earlier steps or stages unless something is actually broken. Take the next step from it, and read only the GDD/tech sections that step needs.
2. **Clarify before building, not during.** Before starting a step, list every question whose answer would change what gets built (design rules, data shape, module boundaries, anything the docs leave open) and ask the user then, in one batch. Never invent a significant design decision to fill a gap. Before any larger module, agree its scope and key details with the user first. Once the step is underway, finish it: questions that come up mid-step and don't block it get a reasonable, easily reversible default, are noted in `docs/progress.md`, and are raised at the end of the session. Stop mid-step only if a question truly blocks the work.
3. **One small step at a time**, inside the current stage (tech 9). A step is one checklist item or a clear part of one. Don't build ahead for later stages.
4. **Verified means:** `dotnet build IncarnationHero.sln` has no errors and `dotnet test IncarnationHero.sln` is fully green. New Core logic comes with tests (fixed seeds). Changes in `game/` additionally need the user to confirm they run in the Godot editor, because Claude can't see the game window.
5. **After a verified step:** update `docs/progress.md` (checklist, next step, gotchas, one log line), commit to `main` and push to `origin main`. Docs and code go in the same commit. Never commit or push a red build. Never force-push. Work directly in the main checkout on `main`: no worktrees, no feature branches (background sessions are configured for this in `.claude/settings.json`). If the push is rejected, `git pull --rebase`, re-run the tests, then push.
6. **Stage done:** when every "Готово, коли" criterion of the stage in tech 9 is met, mark the stage done in `docs/progress.md` and set up the next stage's checklist there.

Code quality: aim for minimal redundancy and maximal maintainability. Prefer one shared mechanism over parallel copies, keep modules small with clear boundaries, and shape code so the next feature is an extension (new data, a new type registered in one place), not a rewrite. Don't add speculative abstractions for features that aren't planned yet.

Conventions: code, identifiers, comments and commit messages are in English; `docs/` and player-facing text are in Ukrainian. Commit subject is imperative ("Add YAML content loader"), and the body explains why.

## Stack (decided)

- Godot 4.x **.NET edition, C# only**. Never write GDScript and never use Godot 3 APIs (e.g. no `TileMap`, use `TileMapLayer`). AI assistants commonly mix these up.
- Pure C# `Core` class library with **zero Godot dependencies**. The Godot project references it via `ProjectReference`.
- Content in YAML (YamlDotNet), saves in JSON (System.Text.Json) with `schema_version` and a checksum.
- Tests in xUnit with fixed seeds. Python (pandas, Jupyter) is used only for balance analysis under `/analysis`, never in the game.

## Commands

Godot 4.7.2 .NET. Every project targets `net8.0`, because Godot's generated `game/incarnation_hero.csproj` requires it and Core must match. Do not raise the TFM unless Godot changes it.

```
dotnet build IncarnationHero.sln
dotnet test IncarnationHero.sln
dotnet test src/Core.Tests --filter "FullyQualifiedName~RngTests"   # single test class/method
dotnet run --project src/Sim.Cli -- <args>
```

`IncarnationHero.sln` (root) contains all projects. `game/incarnation_hero.sln` is Godot's own solution, which the editor uses to build. Leave it alone: the Godot project already pulls in Core through `ProjectReference`. The Godot project only has `Debug`/`ExportDebug`/`ExportRelease` configurations, so the root solution maps `Release` to `Debug` for it.

## Layout

```
/src/Core            game rules, state, systems, RNG, content loading
/src/Core.Tests      xUnit
/src/Sim.Cli         headless bot runs and metrics export (JSONL/CSV); commands and bots: tech 6.1
/src/Tools.Validate  (planned) content validator CLI, may become a Sim.Cli subcommand
/game                Godot project: UI, combat scene, input, audio only
/content             (planned) YAML content and balance; folder layout: tech 3.1
/loc                 (planned) localization tables (uk, en, zh) as CSV
/analysis            (planned) Python notebooks
/docs                design docs: README.md (index), progress.md, gdd/, tech/
```

## Architecture rules

Core idea: the game is a simulation and Godot is only a window into it. The same Core runs in tests, in Sim.Cli and in the game.

- **Command → Core → SimEvents.** The UI sends typed Commands (`StartMeditation(direction, weeks)`, `Travel(zoneId)`, `CombatMove(hex)`, …). Core validates each one and either executes it or rejects it with a reason. Core then emits typed SimEvents (`WeekPassed`, `InjuryReceived`, …). State is plain serializable data. All logic lives in systems.
- **Mode FSM in Core:** Overworld → EventDialog → Combat → RunEnd. Each mode accepts only its own commands.
- **Time:** 1 tick = 1 week, and age is an integer count of weeks. Core runs weeks until an interrupt fires or the activity ends. Time acceleration exists only in the UI. Weekly order: world systems, then player activity progress, then the event roll, then interrupt collection.
- **Truth vs knowledge:** Core holds the true state. The UI reads **only `PlayerView`**, never other entities' true state. `Observe(viewer, target, aspect) → Observed<T>` returns either a value or a range, based on Perception and the stage gap.
- **Determinism:** use a custom PRNG (xoshiro256** or PCG, seeded through SplitMix64), never `System.Random`. Keep separate streams (worldgen, events, rumors, npc, loot, combat, ai), and give each combat its own subseed. Core must not use `DateTime.Now`, static global state, iteration over Dictionary/HashSet that affects results, or parallelism. Round doubles explicitly before they are written to state. All RNG states are saved.
- **Single modifier system** `Modifier { StatId, Op (Add|Mul|Override|Cap), Value, SourceId, Duration?, StackRule }` covers injuries, buffs, elements, methods and items. The UI builds "where does this number come from" tooltips straight from modifier sources.
- **Cultivation tracks** are parameterized by vector (`CultivationTrack { VectorId, StageIndex, StageProgress, LastBreakthroughQuality }`). The MVP has only `qi`. Don't build body/will systems, just don't block them (risk R12, over-engineering).
- **Breakthrough forecast** must call the same function as the real breakthrough, with uncertain inputs. Never duplicate that logic.
- **Combat** is isolated: it is built from participant snapshots and returns a result. A save during combat is the subseed plus a command log. Auto-resolve is the same combat simulation with AI on both sides, not a separate formula. The one exception is the stage-1 formula resolver behind the same interface. Hex grid uses axial coordinates (cube coordinates for math, following Red Blob Games). AI is utility-based, with archetype weights stored in data.
- **Content:** formulas live in code, coefficients and content live in YAML. There is no scripting language: conditions and effects are a fixed set of typed operations extended in C#. Every object has a stable string ID (`tech.spear.piercing_dragon`, `enemy.beast.iron_boar`), and state refers to content only by ID. Anything that grants soul resonance needs category tags (`weapon:spear`, `element:fire`, `school:*`, `lang:*`).
- **Localization:** Ukrainian is the primary language and is written inline in YAML (`name: { uk: "..." }`). Core returns keys with parameters, not finished strings, and the presentation layer resolves them. No plural engine for now: Ukrainian text with numbers is phrased to avoid number agreement ("Тижнів: 5"), see A9. Name generators emit case forms that templates use: `{npc.nom}`, `{npc.gen}`, `{npc.dat}`.
- **Saves are ironman:** one slot, autosave after every command, commit-before-reveal (any random or new-information result reaches disk before the UI shows it), write to a temp file then swap atomically, keep one backup used only if the checksum fails. The soul profile (`MetaProfile`) is a separate file and is always migrated. Run saves may be invalidated by updates.
- **Regression:** golden tests (fixed seed + fixed bot → hash of the final state). A hash change means the rules changed, and the hash should only be updated deliberately.

## Development plan

Stage 0 is the foundation: RNG, GameState, save/load, YAML loader and validator, a Godot shell. Stage 1 is the macro loop with formula combat plus Sim.Cli, and it is validated **before** any real combat work starts. Stage 2 is the hex combat prototype, stage 3 integration, stage 4 content/meta/crisis, stage 5 demo release. Done criteria per stage: tech 9. Where we are: `docs/progress.md`.
