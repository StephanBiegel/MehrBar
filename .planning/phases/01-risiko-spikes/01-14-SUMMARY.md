---
phase: 01-risiko-spikes
plan: 14
subsystem: testing
tags: [z-order, win-d, desktop-host, watcher, aero-peek, explorer-restart, display-change, spike-note, go-no-go]
status: complete

requires:
  - phase: 01-risiko-spikes
    provides: "01-10 ZOrderGuard (S0/S1/S2), Driver case zorder with the D-10 triggers; 01-13 valid oracle and visible-at-bottom baseline"
provides:
  - "spikes/tools/run-zorder.ps1: the whole Q2 matrix as one command (-DryRun for the pipeline check), follow-up runs no longer clobber an existing S0/S1 set"
  - "Real Q2 measurement on build 26200 under spikes/results/q2/ (S0/S1 matrix 72 runs, S2 follow-up for win-d and peek-key, redacted)"
  - "01-SPIKE-zorder-wind.md: decision GO with strategy S2, consequences for the ZOrderKeeper, two borderline cases for Stephan"
affects: [01-15, 01-16, 01-17, 01-18, phase-02-zorder-keeper]

actuals:
  tokens: 90000
  tasks: 3
  commits: 3
plan_head_before: 48b0ecb96bede76b565adc4061e88c0a706703f4
plan_head_after: df8150ffa3015a4a64682d131c19ce46321c9d7d

tech-stack:
  added: []
  patterns:
    - "A follow-up run of run-zorder.ps1 that lacks S0 or S1 writes runs/events/summary with a guard suffix and rewrites only its own per-guard tables"
    - "A trigger that shows no effect (no LivePreview window, gap 0) is not evidence of 'D-11 met'; the snapshots decide whether the trigger fired"

key-files:
  created:
    - spikes/tools/run-zorder.ps1
    - spikes/results/q2/runs.csv
    - spikes/results/q2/events.csv
    - spikes/results/q2/S0.md
    - spikes/results/q2/S1.md
    - spikes/results/q2/summary.md
    - spikes/results/q2/S2.md
    - spikes/results/q2/runs-s2.csv
    - spikes/results/q2/events-s2.csv
    - spikes/results/q2/summary-s2.md
    - .planning/phases/01-risiko-spikes/01-SPIKE-zorder-wind.md
  modified: []

key-decisions:
  - "Q2: GO with strategy S2 (watcher re-inserts the bar directly above the desktop host, never topmost). S0 fails Win+D and the taskbar corner (0/5 returned, gap about 3 s), S1 fails Win+D (3 samples in front of the witness in one repetition, in front in 3 of 5). Assumption A11 (no watcher needed on 26200) is refuted"
  - "S2 at Win+D is a borderline case (1 sample, 5 ms, not confirmed by the window order) and goes to Stephan per D-08; it is the only strategy without a clear violation"
  - "Aero Peek by key (Win+,) fails D-11 identically with S0, S1 and S2; judged ambiguous, not a NO-GO: the injected release leaves the dwm LivePreview overlay up (odd repetitions), even repetitions show no peek at all, and the cover test flags itself invalid. No layer strategy can change it. Decided by the hand check with real keys (H3)"
  - "Aero Peek by corner hover produced no LivePreview window in any run: the trigger had no effect, so its 'D-11 met' is empty"

patterns-established:
  - "Decision line may be GO with named open borderline cases when one strategy is the only candidate and the remaining uncertainty is strategy-independent"

requirements-completed: []

coverage:
  - id: D1
    description: "Q2 real run on the live desktop after Stephan's consent ('los', with resolution change): S0/S1 base matrix, Explorer restart, resolution change, S2 follow-up for win-d and peek-key; results committed redacted under spikes/results/q2/"
    verification:
      - kind: other
        ref: "Runs executed by the orchestrator from the main checkout (all steps exit 0, session before = after: TaskbarSd=absent explorer.exe=1 primary=1920x1080). In the worktree: cp of spikes/results/q2/* ; cmp runs.csv identical; grep for personal names/paths/window titles in the copied files found none; awk recomputation of max gap / returned / in-front per guard x trigger from runs.csv and runs-s2.csv matches the summary tables"
        status: pass
    human_judgment: false
  - id: D2
    description: "01-SPIKE-zorder-wind.md carries exactly one decision line (GO, S2), Folgen for Phase 2/3, and the Grenzfaelle for Stephan including H2 and H3"
    verification:
      - kind: other
        ref: "check-notes.ps1 could not run in the sandbox; checked manually against the script: six headings present, one line starting '**Entscheidung:**' matching 'GO' + text, Messwerte references spikes/results/q2/. The orchestrator runs the script after merge"
        status: unrun
    human_judgment: false
  - id: D3
    description: "Restoration: TaskbarSd back to the original (absent), explorer.exe running, display mode 1920x1080"
    verification:
      - kind: other
        ref: "Script lines 'Session before/after' identical in both runs; own check: reg query ... /v TaskbarSd reports the value not found, tasklist shows explorer.exe running"
        status: pass
    human_judgment: false
  - id: D4
    description: "run-zorder.ps1 fix so that a follow-up run does not clobber an S0/S1 result set"
    verification:
      - kind: other
        ref: "Reviewed by reading only; the sandbox refuses powershell, so the script was not executed after the edit. The orchestrator should run -DryRun (and ideally check that a '-Guards S2 -IncludeS2' dry-equivalent leaves runs.csv untouched)"
        status: unrun
    human_judgment: false

duration: about 2 h (measurement by the orchestrator, evaluation and note here)
completed: 2026-10-12
---

# Phase 1 Plan 14: Q2 Layer vs Win+D and Relatives Summary

**Decision GO with strategy S2 (watcher re-inserts the bar above the desktop host, no topmost): S0 and S1 are measured out on build 26200; two borderline cases (S2 at Win+D, Win+, peek) go to Stephan.**

## What was done

- **Task 1** (commit 4b57d0f, earlier): `run-zorder.ps1` and the note skeleton; dry run exit 0.
- **Task 2:** checkpoint resolved by Stephan with "los" (with resolution change). Consent and conditions are in the note's Aufbau.
- **Task 3:** the orchestrator ran the matrix (S0/S1 base, Explorer, display; then S2 for win-d and peek-key). Here: verified all numbers from the CSVs, evaluated D-11 per guard x trigger, decided, completed the note, committed results (df8150f), fixed the script clobber (1cd20c9).

## Results (verified from runs.csv / runs-s2.csv)

| Trigger | S0 | S1 | S2 |
|---|---|---|---|
| win-d | gap 2968 ms, 0/5 back: fail | gap 218 ms, 5/5, in front 0,0,3,1,1 samples (max 43 ms): fail | gap 200 ms, 5/5, in front 0,0,0,0,1 (5 ms, not confirmed by z-order): borderline |
| corner | 2934 ms, 0/5: fail | 218 ms, 5/5, 0 in front: pass | not measured |
| win-m, alt-tab | pass | pass | not measured |
| explorer (3) | bar never hidden, z-order restored about 10 ms after TaskbarCreated: pass | same | not measured |
| display (3) | gap 17 ms: pass | same | not measured |
| peek (corner hover) | no effect (no LivePreview window): empty | same | - |
| peek-key | 2899 ms, 2/5: fail, ambiguous | 2868 ms, 2/5 | 2884 ms, 2/5 |

## peek-key analysis (why this is not a NO-GO)

In repetitions 1, 3, 5 a `LivePreview` window (owner dwm, 3840x1080) sits at z-index 0 from T+0.5 s on, the bar stays at the bottom, no guard transition, 0 samples in front; the gap lasts to the end of the 3 s window and the cover test (3 s more) flags itself `cover-invalid`, so the overlay was up at least about 4 s after the injected release. Repetitions 2 and 4 show no peek at all. Identical for S0, S1, S2, and Peek hides every window including topmost ones. That points at the injected key release not ending the peek (instrument/trigger artefact) but does not prove it, so it is queued as a mandatory hand check (H3) rather than decided.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Instrument bug] run-zorder.ps1 clobbered an existing S0/S1 result set**
- **Found during:** Task 3, S2 follow-up run (reported by the orchestrator)
- **Issue:** A second run into the same OutDir replaced runs.csv, events.csv, summary.md and deleted S0.md/S1.md.
- **Fix:** Runs without both S0 and S1 write `runs-<guards>.csv`, `events-<guards>.csv`, `summary-<guards>.md`; the per-guard table of a guard that is not part of the run is never deleted. The documentation block of the script and the note's Aufbau mention it.
- **Files modified:** spikes/tools/run-zorder.ps1
- **Commit:** 1cd20c9
- **Limitation:** reviewed by reading only, not executed (the sandbox refuses powershell).

### Other

- Plan step 1 and 3 (running the script) were executed by the orchestrator in the main checkout because the worktree sandbox refuses powershell; the measured files were copied byte-identically. No result was altered.
- S2 was run only for win-d and peek-key (plan step 3: only where S1 fails). Hence the open item "S2 for the other triggers not measured".

## Known Stubs

None.

## Threat Flags

None. The plan's mitigations held: consent recorded, restoration verified (TaskbarSd absent before and after, explorer.exe running, 1920x1080), desktop-icon backups stay under the ignored spikes/results/private/.

## What Stephan has to decide

1. **S2 at Win+D:** accept the one-sample (5 ms) borderline case, or re-measure with more repetitions.
2. **Win+, (Aero Peek by key):** try it with real keys (H3). If the bar returns immediately after release, it was a measurement artefact; if not, decide between "Windows behaviour, accept" and D-12 (NO-GO, follow-up spike).
3. Hover-peek on the corner: confirm by hand whether it exists on this system at all.
4. Optionally release another hands-off window to measure S2 for the remaining triggers (corner, win-m, alt-tab, explorer, display), plus H2 lock/unlock.

## Self-Check: PASSED

- Result files, note, script present in the worktree; commits 1cd20c9 and df8150f are on the branch.
- check-notes.ps1 and the edited run-zorder.ps1 were not executed (sandbox); stated above.
