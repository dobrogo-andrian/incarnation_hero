"""Shared loader for the balance notebooks (tech 6.2).

Runs Sim.Cli bots into JSONL (one RunSummary per line, see src/Sim.Cli/Runs/RunSummary.cs) and
turns the lines into pandas frames. Data files are regenerated when Core, Sim.Cli or content is
newer than them, so a notebook never reports on stale rules.
"""

from __future__ import annotations

import json
import re
import subprocess
from dataclasses import dataclass
from pathlib import Path

import pandas as pd
import yaml

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "analysis" / "data"
CONTENT = ROOT / "content"
SOURCES = [ROOT / "src" / "Core", ROOT / "src" / "Sim.Cli", CONTENT]
BOTS = ("cautious", "fighter", "random")


def _newest_source() -> float:
    files = (f for d in SOURCES for f in d.rglob("*")
             if f.is_file() and f.suffix in {".cs", ".csproj", ".yaml"} and "obj" not in f.parts and "bin" not in f.parts)
    return max(f.stat().st_mtime for f in files)


def generate(bot: str, runs: int, seed_from: int, force: bool = False) -> Path:
    """Runs one bot through Sim.Cli unless an up-to-date data file already exists."""
    DATA.mkdir(parents=True, exist_ok=True)
    out = DATA / f"{bot}-{runs}-{seed_from}.jsonl"
    if force or not out.exists() or out.stat().st_mtime < _newest_source():
        subprocess.run(
            ["dotnet", "run", "--project", "src/Sim.Cli", "-c", "Release", "--",
             "run", "--bot", bot, "--runs", str(runs), "--seed-from", str(seed_from),
             "--content", "content", "--out", str(out)],
            cwd=ROOT, check=True, capture_output=True)
    return out


@dataclass
class Runs:
    """One row per run, plus long frames for the nested parts of RunSummary. All carry seed and bot."""
    runs: pd.DataFrame
    breakthroughs: pd.DataFrame
    activity_weeks: pd.DataFrame
    stones: pd.DataFrame
    pills: pd.DataFrame
    injuries: pd.DataFrame
    combat_groups: pd.DataFrame


def load(bots=BOTS, runs: int = 1000, seed_from: int = 1, force: bool = False) -> Runs:
    lines = [json.loads(line)
             for bot in bots
             for line in generate(bot, runs, seed_from, force).read_text(encoding="utf-8").splitlines()]

    def long(rows, columns):
        return pd.DataFrame(rows, columns=["seed", "bot", *columns])

    run_rows, breakthroughs, activities, stones, pills, injuries, groups = [], [], [], [], [], [], []
    for r in lines:
        key = (r["seed"], r["bot"])
        c, s, p, i = r["combat"], r["stones"], r["pills"], r["injuries"]
        run_rows.append({
            "seed": r["seed"], "bot": r["bot"], "end_cause": r["end_cause"],
            "aggression": r.get("aggression"), "death_age_years": r["death_age_years"], "final_stage": r["final_stage"], "weeks": r["weeks"],
            "healing_weeks": r["healing_weeks"], "events": sum(r["events"].values()),
            "rumors": sum(r["rumors"].values()), "breakthrough_attempts": len(r["breakthroughs"]),
            "fights": c["fights"], "won": c["won"], "fled": c["fled"], "beaten": c["beaten"], "died": c["died"],
            "combat_insight": c["insight"], "stones_start": s["start"], "stones_end": s["end"],
            "stones_earned": sum(s["earned"].values()), "stones_spent": sum(s["spent"].values()),
            "pills_bought": sum(p["bought"].values()), "pills_taken": sum(p["taken"].values()),
            "peak_toxins": p["peak_toxins"], "first_heavy_week": i["first_heavy_week"],
            "weeks_stalled_after_heavy": i["weeks_without_progress_after_heavy"],
            "progressed_after_heavy": i["progressed_after_heavy"],
        })
        breakthroughs += [(*key, *b.values()) for b in r["breakthroughs"]]
        activities += [(*key, a, w) for a, w in r["activity_weeks"].items()]
        stones += [(*key, flow, src, n) for flow in ("earned", "spent") for src, n in s[flow].items()]
        pills += [(*key, flow, item, n) for flow in ("bought", "taken") for item, n in p[flow].items()]
        injuries += [(*key, inj, v) for inj, v in i["peak"].items()]
        groups += [(*key, g, *v.values()) for g, v in c["by_group"].items()]

    bt_columns = list(lines[0]["breakthroughs"][0].keys()) if any(r["breakthroughs"] for r in lines) else []
    return Runs(
        runs=pd.DataFrame(run_rows),
        breakthroughs=long(breakthroughs, bt_columns),
        activity_weeks=long(activities, ["activity", "weeks"]),
        stones=long(stones, ["flow", "source", "amount"]),
        pills=long(pills, ["flow", "item", "count"]),
        injuries=long(injuries, ["injury", "peak"]),
        combat_groups=long(groups, ["group", "won", "fled", "beaten", "died"]),
    )


def _content_defs() -> list[dict]:
    defs = []
    for f in sorted(CONTENT.rglob("*.yaml")):
        doc = yaml.safe_load(f.read_text(encoding="utf-8"))
        defs += [d for d in (doc if isinstance(doc, list) else [doc]) if isinstance(d, dict) and "id" in d]
    return defs


_DEFS = {d["id"]: d for d in _content_defs()}


def label(key: str) -> str:
    """Ukrainian name of a content ID, activity key or end cause, falling back to the key itself."""
    if key in {"OldAge", "BreakthroughDeath", "CombatDeath"}:
        key = "str.end." + re.sub(r"(?<!^)(?=[A-Z])", "_", key).lower()
    elif key not in _DEFS and f"str.activity.{key}" in _DEFS:
        # Activity keys (travel, wait...) name a template with parameters; keep the part before them.
        key = f"str.activity.{key}"
    d = _DEFS.get(key, {})
    text = (d.get("name") or d.get("text") or {}).get("uk")
    return re.split(r",| \(", text)[0] if text else key


def ranks() -> list[str]:
    """Breakthrough ranks from worst to best, as config.breakthrough lists them."""
    return _DEFS["config.breakthrough"]["ranks"]
