# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

Wuxia/xianxia turn-based roguelike (working title "incarnation_hero"). Solo developer, target is a public Steam demo. Stage 0 has started: the projects are scaffolded, but there is no game code yet. Design docs are Markdown in `docs/` (Ukrainian), one file per section. Start from the index `docs/README.md` and read only the files you need.

- `docs/gdd/`: game design document (GDD), the source of truth for gameplay rules. Section refs like "§4.5" point here (`grep "§4.5" docs/gdd`).
- `docs/tech/`: technical design (stack, architecture, roadmap, risks). Tech sections have no `§` and are cited as "tech 2.8". Decisions D* and working assumptions A* are in `docs/tech/decisions.md`; open questions Q* are in `docs/tech/open-questions.md`.
- `.claude/agents/game-designer.md`: design-partner subagent for GDD sessions.
- `sources/archive/*.docx`: the original Word files. Read-only, don't edit or use as a source.

If code contradicts the docs, or a task touches an open question (Q*), point it out and don't decide it silently. When a design decision changes, edit the matching `.md` file. When a Q is resolved, move it into `decisions.md` as a D or A entry.

## Stack (decided)

- Godot 4.x **.NET edition, C# only**. Never write GDScript and never use Godot 3 APIs (e.g. no `TileMap`, use `TileMapLayer`). AI assistants commonly mix these up.
- Pure C# `Core` class library with **zero Godot dependencies**. The Godot project references it via `ProjectReference`.
- Content in YAML (YamlDotNet), saves in JSON (System.Text.Json) with `schema_version` and a checksum.
- Tests in xUnit/NUnit with fixed seeds. Python (pandas, Jupyter) is used only for balance analysis under `/analysis`, never in the game.

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
/src/Sim.Cli         headless bot runs and metrics export (JSONL/CSV)
/src/Tools.Validate  (planned) content validator CLI, may become a Sim.Cli subcommand
/game                Godot project: UI, combat scene, input, audio only
/content             (planned) YAML: balance/, cultivation/, knowledge/, combat/, world/, events/, rumors/, injuries/, items/
/loc                 (planned) localization tables (uk, en, zh) as CSV
/analysis            (planned) Python notebooks
/docs                design docs: README.md (index), gdd/, tech/; ai-guidelines.md planned
```

Planned Sim.Cli usage: `sim run --runs 5000 --bot cautious --seed-from 1 --out runs.jsonl` and `sim arena --a grp.X --b grp.Y --n 2000`. Bots: `cautious`, `fighter`, `pill_rusher`, `random`.

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
- **Localization:** Ukrainian is the primary language and is written inline in YAML (`name: { uk: "..." }`). Core returns keys with parameters, not finished strings, and the presentation layer resolves them. Plural rules are per language (Ukrainian has 3 forms). Name generators emit case forms that templates use: `{npc.nom}`, `{npc.gen}`, `{npc.dat}`.
- **Saves are ironman:** one slot, autosave after every command, commit-before-reveal (any random or new-information result reaches disk before the UI shows it), write to a temp file then swap atomically, keep one backup used only if the checksum fails. The soul profile (`MetaProfile`) is a separate file and is always migrated. Run saves may be invalidated by updates.
- **Regression:** golden tests (fixed seed + fixed bot → hash of the final state). A hash change means the rules changed, and the hash should only be updated deliberately.

## Development plan

Stage 0 is the foundation: RNG, GameState, save/load, YAML loader and validator, a Godot shell. Stage 1 is the macro loop with formula combat plus Sim.Cli, and it is validated **before** any real combat work starts. Stage 2 is the hex combat prototype, stage 3 integration, stage 4 content/meta/crisis, stage 5 demo release. Keep work scoped to the current stage.
