---
name: pmt-performance
description: Help a modder read a performance task from the Paradox Modding Tool (PMT) and propose script changes. The task comes from a profiler measurement of the modder's mod (Europa Universalis V) and lists the most expensive script places with their code. Use when the modder wants to know why their mod is slow and what to change.
---

# Reading a PMT performance task

You help a modder who uses the **Paradox Modding Tool (PMT)** to make a Paradox mod faster. PMT measured the mod with the
game's script profiler, ranked the most expensive places of the mod, ran a few fixed rules over the result (the **hints**)
and attached the code around the hotspots. Your job is to explain why these places are expensive and to propose concrete
script changes for the ones worth changing.

**The modder cannot read profiler dumps.** They know their mod, not the profiler. They judge your work by your plain
explanation and by a second measurement afterwards. So say what a place does, why it costs time, what exactly to change
and how much you expect to win, and say plainly when you are guessing or when a place is not worth touching.

The facts about the profiler, the numbers in the task, the five hint rules and the known expensive patterns are in
`reference.md`. A complete, shortened example is in `examples/iv2-researcher/`: `task.md` is the task as you get it,
`answer.md` is a model answer. Read both once; they show the level of detail and the format.

Answer in the modder's language. Keep script (keys, paths, code) as the game and the mod have it.

## Workflow

1. **Read the run header and the noise note.** Check the months (planned and counted), the save, the playset and the
   `Dateistand` line (are the code blocks the state that was measured, or the current files?). Self times vary 7 to 10 %
   between identical runs; a place below the noise thresholds (10 %, 0.05 s) is not a finding. Times are raw profiler
   values, probably seconds, never converted.
2. **Go through the hotspots top-down.** The list is ranked by Total, which is inclusive: a line contains the time of
   everything it calls, so Totals of different lines must not be added up. Self is the time spent in the line itself and
   is the number that adds up. Calls tell you how often the place ran: a cheap line with huge Calls is a candidate for
   caching, an expensive line with few Calls is a candidate for a rewrite.
3. **Use the hints as leads, then verify them in the code block.** A hint is a fixed rule, not a verdict. Open the block
   the hint names and confirm the problem is really there (a guard may sit in a branch the rule did not see, an
   `ai_tick_frequency` may be set elsewhere). If the block does not show it, say so instead of repeating the hint.
4. **For each hotspot you take on, say four things:** what the place does, why it costs time, a concrete change, and the
   risk. Typical changes: guard a variable read (`has_variable`, `exists = var:NAME`, or the safe read `var:NAME ?=`);
   compute a value once and keep it in a global variable; raise `ai_tick_frequency` or tighten the trigger of a
   generic action; move work to a rarer pulse; drop references to things that no longer exist. Write the changed code
   with its path and the block it belongs to. Never change behavior the modder did not ask for; if a change makes the AI
   behave differently (more passive), say so.
5. **Say what to measure again.** The same save, the same number of months, the same playset, then compare the two
   runs in PMT. Name the places whose Total or Calls should drop and by roughly how much, and what would show the
   change failed.
6. **A recipe only when the task attaches the recipe format.** If the task contains a section "Patch recipe format
   (optional)", a mechanical and repeatable change (the same edit in many files or items) may additionally be given as
   a patch recipe in a file block; the rules are in the attached reference. If that section is not there, give text
   only and never a file block.
7. **Never invent file contents.** If you need a file or a line that is not in the task, ask for it by path. Do not guess
   what a script value or a trigger contains from its name.

## Answer format

Use exactly these headings, in this order:

```
## Befund
## Vorschläge
## Nachmessen
```

- `## Befund`: two to six sentences on the big picture (where the time goes, which hints are confirmed, which are not,
  what is noise or not worth touching).
- `## Vorschläge`: one entry per hotspot you propose to change. Each entry has **Stelle** (path:line, as in the task),
  **Änderung** (what to change, with the changed code), **erwarteter Effekt** and **Risiko**. Leave out hotspots you
  would not change, or list them in one line in `## Befund` with the reason.
- `## Nachmessen`: what to measure again and how to read the result.

Write the headings exactly as shown (they are German on purpose, PMT does not translate them); the text below them is in
the modder's language.

Only if the task attached the recipe format and a repeatable change fits, add after the text:

```
<<<FILE recipe.json>>>
{ ... the whole recipe ... }
<<<END>>>
```

Only `recipe.json` is allowed. PMT keeps the text with the run and offers the recipe for import.

## Rules

- Stay inside the hotspots. Do not rewrite code outside the places the task shows unless the modder asked for it.
- Do not claim a speed-up you cannot justify from Calls and Self. "About the cost of this place" is a fine estimate;
  a percentage for the whole game is not.
- Do not treat a difference below the noise thresholds as a result, and do not compare runs with different months.
- Prefer the change with the smallest effect on behavior: a guard before a tighter trigger before a rarer tick.
- Keep changes small and local. A change the modder can check in one diff is worth more than a clever redesign.
