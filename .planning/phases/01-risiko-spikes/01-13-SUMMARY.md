---
phase: 01-risiko-spikes
plan: 13
subsystem: testing
tags: [hit-test, click-through, focus, z-order, oracle, spike-note, go-no-go]

requires:
  - phase: 01-risiko-spikes
    provides: "01-03/01-04/01-07/01-09 WindowHost variants H1-H3, Driver cases selftest/firstframe/hit/focus, SafetySession"
provides:
  - "spikes/tools/run-q1.ps1: the whole Q1 run as one command (-DryRun for the pipeline check)"
  - "Real Q1 measurement on build 26200 under spikes/results/q1/ (selftest, firstframe, hit, focus-witness, focus-notepad, summary, two z-order snapshots)"
  - "01-SPIKE-window-hit.md: decision GO with H1 (alpha-1 plate), consequences for BarWindow and the zoom overhang, hand checks for Stephan"
  - "Validated pixel oracle (A1) and D-12 baseline (bar visible at HWND_BOTTOM) for Q2/Q3"
affects: [01-14, 01-15, 01-16, 01-17, 01-18, phase-02-bar-window, phase-03-zoom]

actuals:
  tokens: 85000
  tasks: 3
  commits: 3
plan_head_before: 5526b2ba965d7cf2825d70ce9b38db30b27abe2b
plan_head_after: 1d20ae71cf3c1db3df142664eaf15a7b2b58182e

tech-stack:
  added: []
  patterns:
    - "Pixel-oracle runs need the top screen edge free of other docks: RocketDock tints the probe pixels (#FF16FF instead of #FF00FF) and fails the self-test"
    - "Run attribution by host rows: a click is 'passed through' when no lbuttondown row exists at the click position; foreground change away from the witness is the second proof"

key-files:
  created:
    - spikes/tools/run-q1.ps1
    - spikes/results/q1/selftest.csv
    - spikes/results/q1/selftest.zorder-covered.csv
    - spikes/results/q1/selftest.zorder-uncovered.csv
    - spikes/results/q1/firstframe.csv
    - spikes/results/q1/hit.csv
    - spikes/results/q1/focus-witness.csv
    - spikes/results/q1/focus-notepad.csv
    - spikes/results/q1/summary.md
    - .planning/phases/01-risiko-spikes/01-SPIKE-window-hit.md
  modified: []

key-decisions:
  - "Q1: GO with H1 (alpha-1 plate in the window). H1 and H2 both meet every D-08 threshold, H1 wins on simplicity; H2 (SetWindowRgn, median 0.7 ms per call) stays the measured fallback; H3 (WS_EX_TRANSPARENT poll) is rejected at 95/100 inside"
  - "Oracle A1 holds (5/5 visible, 5/5 covered) and the D-12 baseline holds (bar visible at HWND_BOTTOM 5/5): no follow-up spike needed"
  - "BarWindow creation order: ex-styles TOOLWINDOW|NOACTIVATE and SetWindowPos(HWND_BOTTOM, SWP_NOACTIVATE|SWP_SHOWWINDOW) in OnSourceInitialized plus a WM_WINDOWPOSCHANGING rewrite of WPF's first Show; MA_NOACTIVATE confirmed"
  - "Phase 3 must keep the hit area growing with the zoom overhang (the alpha-0 gap passes clicks through 20/20) - derived from the measurement, to be checked in zoom acceptance"

patterns-established:
  - "Measured-run history belongs in the note's Aufbau (attempt table with cause per failure), so aborted runs are not mistaken for instrument bugs"

requirements-completed: []

coverage:
  - id: D1
    description: "Q1 real run on the live desktop: oracle self-test, first frame at the bottom, hit/pass-through/move/focus for H1-H3, focus on witness and Notepad; results committed redacted under spikes/results/q1/"
    verification:
      - kind: other
        ref: "Driver.exe selftest/firstframe/hit/focus via run-q1.ps1 (attempt 3, all exit 0); bash loop test -f spikes/results/q1/{selftest,firstframe,hit,focus-witness,focus-notepad}.csv summary.md -> all present; cmp against the main-checkout originals -> identical"
        status: pass
    human_judgment: false
  - id: D2
    description: "01-SPIKE-window-hit.md carries exactly one decision line (GO, H1), Folgen for Phase 2/3 and the Grenzfaelle for Stephan"
    verification:
      - kind: other
        ref: "check-notes.ps1 -Note 01-SPIKE-window-hit.md (run by the orchestrator after merge: 'OK 01-SPIKE-window-hit.md', exit 0). Manual check against the script: six headings present, one **Entscheidung:** line matching 'GO' + text, Messwerte references spikes/results/q1/"
        status: pass
    human_judgment: false
  - id: D3
    description: "H1 hand checks: flash check over 10 host starts, real-mouse cross-check, emergency abort key"
    verification: []
    human_judgment: true
    rationale: "Flashing below one frame and real-hand behaviour cannot be observed by the instrument (D-06, D-07)"

duration: 55min
completed: 2026-10-12
status: complete
---

# Phase 1 Plan 13: Q1 hit area, pass-through, first frame, focus Summary

**Q1 measured on the real desktop (build 26200) and decided GO with H1: an alpha-1 plate in the transparent host window catches 100/100 inside clicks, lets 100/100 clicks 1 px outside pass in every category, keeps focus 300/300 and 60/60, and appears at the bottom 10/10.**

## Performance

- **Duration:** about 55 min for this continuation (interpretation, note, results commit); the real measurement ran earlier under the orchestrator
- **Completed:** 2026-10-12
- **Tasks:** 3 (Task 1 tracer: run pipeline and note skeleton; Task 2 checkpoint: hands-off window; Task 3: results, decision, note)
- **Files:** 10 created

## Accomplishments

- Oracle self-test passed on the real desktop (bar uncovered: sentinel visible 5/5; witness over the bar: cover colour 5/5), so pixel results for Q2/Q3 can be trusted under the stated condition.
- D-12 baseline holds: the bar is visible at `HWND_BOTTOM` above the desktop, 5/5; no follow-up spike (decimal phase 1.1) is needed.
- Hit area, per variant (100 inside / 100 outside, 20 per outside category, seed 4711):

| Variant | inside caught | outside passed (links/rechts/oben/unten/Ueberhang) | move inside/outside | focus (hit) | verdict |
|---|---|---|---|---|---|
| H1 | 100/100 | 20/20 in each of the five | 24 / 0 | 100/100 | meets every threshold |
| H2 | 100/100 | 20/20 in each of the five | 24 / 0 | 100/100 | meets every threshold; region = plate (SIMPLEREGION), churn median 706 us, p95 2100 us, max 2946 us |
| H3 | 95/100 | 20/20 in each of the five | 22 / 0 | 95/100 | fails inside >= 99 %; entry latency median 17.1 ms, p95 33.9 ms |

- Focus on an active witness 300/300 and on Notepad 60/60 for all three variants; first frame at the bottom 10/10 for all three.
- The per-category outside numbers were recounted independently from `hit.csv` (host row `arrived=False`), matching `summary.md`: 0 host clicks outside in every category and the gap, for H1, H2 and H3.
- Note written in German with exactly one decision line, the BarWindow consequences (creation order, MA_NOACTIVATE, no region or transparent toggling) and what Phase 3's zoom overhang needs from the hit area.

## Task Commits

1. **Task 1: run pipeline and note skeleton** - `0ba25be` (feat, from the first agent run)
2. **Task 2:** checkpoint (hands-off window), resolved by Stephan; no commit
3. **Task 3 (results):** `3b7def8` (test) - eight redacted result files, byte-identical to the originals
4. **Task 3 (note):** `1d20ae7` (docs) - 01-SPIKE-window-hit.md with decision, Folgen, Grenzfaelle

`commits: 3` in the frontmatter is measured with `git rev-list --count 5526b2b..1d20ae7` (before this SUMMARY commit).

## Run History (facts for the record)

| Attempt | Outcome | Cause |
|---|---|---|
| 1 | self-test visible 0/5 (read #FF16FF instead of #FF00FF); hit then aborted on real input | Stephan's running RocketDock at the top edge tinted the probe pixels; Stephan also touched the mouse |
| 2 | RocketDock closed: self-test 5/5 + 5/5; hit aborted after 97 clicks on real input | Stephan confirmed he touched the PC (intended safety, not an instrument bug) |
| 3 | complete, all exit 0 | official results |

Lesson carried to Q2, Q3 and the D-17 runs: close other docks and top-edge tools before pixel-oracle runs.

## Deviations from Plan

### Auto-fixed Issues

None - no instrument code was changed (`HitRegion.cs` and `Q1Cases.cs` untouched).

### Execution deviations (not rule-based)

**1. Real run executed by the orchestrator from the main checkout.**
- **Reason:** the worktree sandbox refuses powershell. The results were copied byte-identically into the worktree (`cmp` verified) and checked for redaction: no window titles, no personal paths; foreign windows appear only as class `other` with empty process, own windows as `HwndWrapper[WindowHost...]`, the desktop as `Progman`.

**2. `check-notes.ps1` was not run by the executor.**
- **Reason:** powershell is blocked in the sandbox. The note rules were checked manually against the script (six headings, exactly one `**Entscheidung:** GO <text>` line, `spikes/results/q1/` referenced in Messwerte). The orchestrator ran it after the merge: `OK 01-SPIKE-window-hit.md`, exit 0.

**3. Notepad cycle count.** The plan states "20 cycles" for Notepad; each cycle holds three actions (left click, right click, move), so the table reports 60 single checks (60/60); the witness step 100 cycles = 300/300.

## Issues Encountered

- The first two attempts failed for environmental reasons (RocketDock tint, real input), see Run History.
- The `--show-mode wpf` baseline (plain WPF Show lifts the bar above other windows, per the host code comment) was not re-measured in this run; the note says so.

## Known Stubs

None.

## Threat Flags

None - no new network, auth or file-access surface; only result files and a note were added.

## Items for Stephan (also in the note, section Grenzfaelle)

1. Flash check: 10 host starts, `Driver.exe firstframe --variants H1 --launches 10 --manual`.
2. Real-mouse cross-check: `Driver.exe hit --variants H1 --manual --seconds 60`.
3. Abort key Ctrl+Alt+Q (alt. Ctrl+Alt+Shift+Q) once during a run.
4. Measured at 100 % scaling, one monitor, bar at the top, RocketDock closed; spot check at 125/150 % and the right-hand bar come with Phase 2.
5. Close RocketDock before every further pixel-oracle run.

## Self-Check: PASSED

- Result files in the worktree: all eight present and `cmp`-identical to the originals.
- Commits `3b7def8` and `1d20ae7` are ancestors of HEAD.
- `spikes/results/private` untracked and ignored; STATE.md and ROADMAP.md untouched.
