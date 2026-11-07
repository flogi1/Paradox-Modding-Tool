# Performance reference

What a PMT performance task contains, how to read the numbers, what the five hint rules check, and which patterns are
known to be expensive in Paradox scripts. Example: `examples/iv2-researcher/`.

## The profiler

The game's script profiler counts, for every place in the script (`file:line`), how often it ran and how long it took.
PMT measures like this (Europa Universalis V, checked with game version 1.4.1):

1. Start the game with `-debug_mode` and load a fixed, named save (never an autosave), so every run starts from the same state.
2. Console: `Script.Profiling.Restart`. The profiler counts cumulatively from the last Restart, also across playset
   changes, so every measurement starts with it.
3. Play a fixed number of months (12) at the highest speed.
4. Console: `Script.Profiling.Stop`. Only after Stop does the dump contain data.
5. Console: `Script.Profiling.DumpAll`. The game writes `logs/profiling_roots.csv`; PMT picks it up and stores it as a run.

Columns per line of the dump: **Calls** (how often the place ran), **Self** (time in the place itself, without the
script it calls), **Total** (time including everything it calls). Total is inclusive and cannot be added up across lines,
Self can. The dump names no unit; the values are probably seconds and PMT shows them raw. The kind of a place is its
script type: `script_value`, `trigger`, `effect`, `named_script_value`.

CK III has no dump command, Victoria 3 dumps are unusable in game version 1.13.11, HOI IV and EU IV have no script
profiler: tasks come from EU V runs.

## Run header

The section `## Messlauf` (English UI: `## Measurement run`) lists the run name, game, mod, date, months (planned and
counted from the dump), the save, the playset, the file state line and the noise note.

- **Months**: two runs are only comparable with the same number of months. "counted" is what the dump shows.
- **Dateistand** (file state): "wie gemessen" means the mod files are unchanged since the measurement. "seit der Messung
  geändert" means the code blocks in the task show the current files, not the measured ones: line numbers may be off and
  the hotspot may already be edited. Say so in your answer. "jetzt" and "unbekannt" call for the same caution.
- **Noise note**: the thresholds below.

## Origins

Every line has an origin by the file it stands in: **Mod** (file only in the active mod), **Mod und Vanilla** (the mod
overrides a vanilla file of the same path), **Vanilla** (game only), **Andere Mod** (file of another mod PMT knows). The
task ranks only the mod side (Mod and Mod und Vanilla). `## Zeiten je Herkunft` gives Self per origin; the mod side is
the sum of Mod and Mod und Vanilla. **Unbekannt** (unknown) marks a file PMT finds in no known folder (mod, other mods, game), so it cannot say whose it is.

## Noise and thresholds

- Identical runs vary 7 to 10 % in Self. A single run cannot prove an improvement of that size.
- When PMT compares two runs, a change counts only above the relative threshold **10 %** and above the absolute floor
  **0.05** (seconds). Below both it is "unchanged".
- If the Calls of a line change by more than **25 %**, other work ran ("Verhalten geändert", behavior changed) and the
  time is not comparable. Typical cause: the AI became passive because a daily action now runs monthly.

## What cannot be concluded from one run

- Whether a change helped: that needs a second run with the same save, months and playset.
- Whether a place is expensive in other situations: the save fixes the game state (countries, wars, year).
- Anything about months the run did not cover, or about code that did not run in the measured months.
- The cost split inside a block when only the top line is listed: Total of a line includes its callees; look at the
  callees' own lines (they are separate rows) and at the code block.
- Totals cannot be summed: a script value called by three hotspots is counted in all three.

## The five hint rules

Hints are produced by fixed rules from the dump and, when the run has one, the `error.log` of the measured session.
They are leads, not verdicts: verify each in the code block.

| Rule id | Severity | Flags | Exact condition |
|---|---|---|---|
| `error-spam` | warning | A place in the mod that fills the `error.log`. Every entry costs time to write. | At least **100** entries at the same `file:line` (`ErrorSpamMin`). Needs an `error.log`. The detail names the most common message and entries per minute. |
| `unguarded-var` | warning | A script value or trigger reads `var:NAME` without checking that it exists. If it is missing, each call writes an error. | In the top-level block, `var:NAME` is read before any `has_variable = NAME`, `exists = var:NAME` or `var:NAME ?=` (also `var:NAME.field ?=`). A guard inside `NOT` or `NOR` does not count; `global_var:` and `local_var:` are not checked. One hint per top-level block. |
| `frequent-tick` | warning or info | A heavy block that runs often. | A line with Total at least **0.5** (`FrequentTickMinTotal`) whose top-level block directly carries a tick marker from the game profile (EU V: `ai_tick = daily`, `ai_tick = monthly`) or is a frequent pulse. The effective interval is days per tick (`daily` = 1, `monthly` = 30, `yearly` = 365) times `ai_tick_frequency` (1 if absent). Warning if the interval is under **30 days** (`FrequentTickMaxPeriodDays`). Info if the interval is unknown (the frequency is a script value, not a number). Nested markers do not count. One hint per top-level block. |
| `vanilla-copy` | info | The hotspot is in a vanilla file the mod overrides. | Line origin is "Mod und Vanilla". One hint per file, at its most expensive line. The time may be in the copied vanilla part, not in the modder's own change. |
| `many-calls` | info | A script value that runs very often. | Script value (`script_value`, `named_script_value`) with more than **100000** calls per game year (`ManyCallsPerYear`), extrapolated from the counted months. |

`TopRows` = 200: the rules look at the 200 most expensive mod-side lines and error places only. The task lists the top
of that ranking (usually 30 rows).

## Known expensive patterns and their fixes

These come from the mod IV2 (Idea Variation 2), measured over 12 months. Each is a pattern to look for in the code
blocks, not a rule to apply blindly.

**1. Unguarded reads of researcher references (the largest win).** A script value read `var:Iv2_researcher_adm`
(a reference to a character) for every country and call, although most countries have none. Every call wrote an error
(thousands per minute) and ran the whole value. Fix: guard with the safe read, so a missing variable gives a quiet
`no`/`0`:

```
var:Iv2_researcher_adm ?= { is_alive = yes }
```

and gate auto-modifiers and effects that use the researcher with `has_variable = Iv2_researcher_adm`. `?=` evaluates
the block only when the variable exists; `has_variable` and `exists = var:NAME` check explicitly. A guard must stand
textually before the first read, in the same top-level block, outside `NOT` and `NOR`.

**2. Dead references.** A variable holding a character stays set when the character dies or leaves. Every reader then
works with a dead reference or errors. Fix: clear references when the holder changes (for example in
`on_character_moved_country`, clear the reference of the old country) and drop references to dead characters in an
existing monthly effect.

**3. Caps and sums recomputed on every call.** A value like a research cap changes rarely but was a script value
called hundreds of thousands of times per year (`many-calls`). Fix: compute it once per month in an existing monthly
effect and store it in a global variable; the callers read the variable, guarded with `?=`. Check that nothing else
changes the inputs between the monthly updates, or accept a delay of up to one month and say so.

**4. Heavy work on a daily AI tick.** Generic actions with `ai_tick = daily` run for every country every day. The
effective interval is the tick times `ai_tick_frequency`: `ai_tick = daily` with `ai_tick_frequency = 180` runs every
180 days and is fine. Without it, or with a small value, the action runs often. Fixes, in order of preference:
- tighten the trigger (`ai_will_do`, `allow`, `potential`) so that most countries are filtered out by the cheapest
  check first (a flag or variable test before a list iteration);
- raise `ai_tick_frequency`;
- move the work to a monthly pulse.

Lowering the frequency can make the AI passive. IV2 tried monthly ticks for nine daily AI actions: the AI became much
more passive, so it was dropped. Prefer narrower triggers over rarer ticks, and warn the modder when a change affects
AI behavior. After such a change compare the effect Calls first (did the AI still act?), then Self and Total.

**5. Merging lists for little gain.** IV2 merged nine AI lists into two: behavior-neutral but saved only about 0.3 s per
year. Do not propose restructurings whose gain is below the noise.

**6. Erroring blocks.** An `ai_will_do` block or an `allow` that errors on every evaluation costs time for the error
and returns nothing useful. Fix the error (guard the read), or drop the block if it never worked.

**7. Mod copies of vanilla files** (`vanilla-copy`). A file copied from vanilla and edited keeps all vanilla lines. If
the hotspot lies in a vanilla line, changing the modder's own part gains nothing. Say so, and ask the modder before
proposing a different way to apply their change.

## Task sections (glossary)

A task has this layout. Headings follow the modder's PMT language; the German ones are shown, with the English ones
in brackets.

- `## Messlauf` (`## Measurement run`): run header and noise note, see above.
- `## Zeiten je Herkunft` (`## Times by origin`): Self per origin and the mod-side sum.
- `## Hotspots (Mod und beide)` (`## Hotspots (mod and both)`): the ranked mod-side lines. Columns `#  Stelle  Art  Calls
  Self  Total`: rank, `path:line`, kind, Calls, Self, Total. The block that contains the line is in a code block below.
- `## Hinweise` (`## Hints`): the hint lines as `[Warnung|Hinweis] path:line — title`, an indented detail, and a note
  when some rules could not run (no `error.log` for a manually chosen dump).
- `## Betroffene Dateien` (`## Affected files`): the mod files behind the hotspots and hints.
- `## Was die KI liefern soll` (`## What the AI should deliver`): what the modder expects from you: an assessment per
  hotspot, concrete script proposals, preferred means, what to measure again.
- `## What the modder wants`: the modder's own words, if any.
- `----- hotspot block: path:start-end -----` ... `----- end -----`: the code of the block around a hotspot or hint,
  with a `Note:` line ("Hotspot Nr. 3, Total ...", "Hinweis: ..."). A block may be cut ("Block auf 120 von N Zeilen
  gekürzt"); the rest is not shown, so do not guess it.
- `----- mod file: ... -----` and `----- game file: ... -----`: whole files the modder ticked. `----- code block: ... -----`:
  excerpts the modder added.
- `# Patch recipe format (optional)`: present only if the modder allows a recipe.
