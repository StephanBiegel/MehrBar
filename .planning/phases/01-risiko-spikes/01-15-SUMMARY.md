---
phase: 01-risiko-spikes
plan: 15
subsystem: testing
tags: [menu, trackpopupmenu, contextmenu, foreground, z-order, assumption-a5, spike-note, go-no-go]
status: complete

requires:
  - phase: 01-risiko-spikes
    provides: "01-11 menu instrument (MenuWpf, MenuNative, MenuCase, variants M-A..M-D); 01-13 valid oracle; 01-14 Q2 note and run-zorder.ps1"
provides:
  - "spikes/tools/run-menu.ps1: the whole Q3 run as one command (-DryRun for the pipeline check)"
  - "Real Q3 measurement on build 26200 under spikes/results/q3/ (4 variants x 11 checks, one measurement each)"
  - "01-SPIKE-menu.md: decision GO with menu technique M-C (native TrackPopupMenuEx, bar as owner), A5 confirmed, side effects and consequences for phase 2/5"
  - "S2 follow-up of Q2 under spikes/results/q2/s2-followup/ folded into 01-SPIKE-zorder-wind.md (borderline 1 and 4 resolved)"
affects: [01-16, 01-17, 01-18, phase-02-menu, phase-02-zorder-keeper]

actuals:
  tokens: 60000
  tasks: 3
  commits: 5
plan_head_before: 0430fa602a720cee6e69bf2408b43e73b0c4acde
plan_head_after: f65b2fcea6e18c8280baa1ebabf9c6b230616b6a

tech-stack:
  added: []
  patterns:
    - "Decision rule by simplicity order (M-A < M-C < M-B < M-D): first variant with all eight mandatory points wins; informative failures (z-during, focus-after) are recorded as side effects, not as reasons to move to a more complex variant"
    - "The driver's own mandatory=n/6 counter omits open and reopen; the eight-point verdict is read from menu.csv"

key-files:
  created:
    - spikes/tools/run-menu.ps1
    - spikes/results/q3/menu.csv
    - spikes/results/q3/menu.md
    - spikes/results/q3/menu-events.csv
    - spikes/results/q2/s2-followup/summary-s2.md
    - spikes/results/q2/s2-followup/S2.md
    - spikes/results/q2/s2-followup/runs-s2.csv
    - spikes/results/q2/s2-followup/events-s2.csv
    - .planning/phases/01-risiko-spikes/01-SPIKE-menu.md
  modified:
    - .planning/phases/01-risiko-spikes/01-SPIKE-zorder-wind.md
    - .planning/phases/01-risiko-spikes/01-14-SUMMARY.md

key-decisions:
  - "Q3: GO with menu technique M-C. M-A (WPF ContextMenu without activation) passes 4 of 8 mandatory points (fails close-desktop, close-window, close-escape, keys-enter); M-C, M-B and M-D pass 8/8, M-C is the simplest of them"
  - "A5 confirmed: SetForegroundWindow on the NOACTIVATE bar (M-B, M-C) lifts it above visible windows during the menu and it stays raised after closing; a helper-window owner (M-D) or no activation (M-A) leaves it at the bottom. Measured with the raw host, i.e. without the Q2 S0 base that rewrites z-changes to HWND_BOTTOM"
  - "Side effects of M-C recorded, not decisive: bar stays raised (z-during fail) and the foreground stays with the bar (focus-after fail). Phase 2 restores the previous foreground window; effect with the Q2 keeper is unmeasured"
  - "Q2 stays GO with S2: the follow-up (win-d, corner, win-m, alt-tab 20 runs each, explorer and display 3 each) meets D-11 everywhere, 0 samples in front of the witness, largest gap 271 ms"

patterns-established:
  - "Grenzfaelle for a spike with a clear measurement: list the hand check (H4) and the unmeasured interplay, state explicitly that no measured value is within 10 % of its limit"

requirements-completed: []

coverage:
  - id: D1
    description: "Q3 real run on the live desktop after Stephan's consent ('los'): four variants, 11 checks each; results committed under spikes/results/q3/"
    verification:
      - kind: other
        ref: "Run executed by the orchestrator from the main checkout (driver exit 0, 62 s, 'No WindowHost process left running'). In the worktree: menu.csv has 44 rows (4 x 11); per-variant counts of the eight mandatory points recomputed by hand from menu.csv (M-A 4/8, M-B 8/8, M-C 8/8, M-D 8/8) and consistent with the driver's mandatory=2/6, 6/6, 6/6, 6/6"
        status: pass
    human_judgment: false
  - id: D2
    description: "01-SPIKE-menu.md carries exactly one decision line (GO, M-C), Folgen for phase 2/3 and Grenzfaelle including H4"
    verification:
      - kind: other
        ref: "check-notes.ps1 not runnable here (powershell refused in the sandbox); checked by reading the script: six headings present, one line starting '**Entscheidung:**' matching 'GO' + text (grep -c = 1), Messwerte section references spikes/results/q3/. The orchestrator runs it after merge"
        status: unrun
    human_judgment: false
  - id: D3
    description: "01-SPIKE-zorder-wind.md updated with the S2 follow-up and still passes the note rules"
    verification:
      - kind: other
        ref: "Table values recomputed with awk from runs-s2.csv (max gap, returned, in-front per trigger) and equal to summary-s2.md; same manual rule check as D2 (one decision line, six headings, spikes/results/ reference); check-notes.ps1 unrun in the sandbox"
        status: unrun
    human_judgment: false
  - id: D4
    description: "Real-keyboard check and look of the menus (H4) queued for Stephan"
    verification:
      - kind: other
        ref: "Listed as item 1 under 'Grenzfaelle fuer Stephan' in 01-SPIKE-menu.md with the Driver command per variant"
        status: unrun
    human_judgment: true

duration: about 1 h in this continuation (measurement by the orchestrator, evaluation and notes here)
completed: 2026-10-12
---

# Phase 1 Plan 15: Q3 Menu Technique at the Non-Activating Bar Summary

**Decision GO with menu technique M-C (native TrackPopupMenuEx, bar as owner, SetForegroundWindow before and WM_NULL after): the WPF ContextMenu without activation fails four of eight mandatory points, A5 is confirmed, and the S2 follow-up closes two Q2 borderline cases.**

## What was done

- **Task 1** (commit 35206ce, earlier): `run-menu.ps1` and the note skeleton; dry run exit 0.
- **Task 2:** checkpoint resolved by Stephan with "los".
- **Task 3** (this continuation): the orchestrator ran the real Q3 run (62 s, exit 0, no "Launched" process left) and, in the same hands-off window, the S2 follow-up of Q2. Here: read all rows from `menu.csv` and `menu-events.csv`, applied the decision rule, completed `01-SPIKE-menu.md`, folded the follow-up into `01-SPIKE-zorder-wind.md` and added an addendum to `01-14-SUMMARY.md`. No source file was changed.

## Results (read from menu.csv)

Mandatory points (D-15; the driver's own counter covers six, `open` and `reopen` were read by hand):

| Variant | open | close-desktop | close-window | close-bar | escape | keys-enter | launch-front | reopen | total |
|---|---|---|---|---|---|---|---|---|---|
| M-A | 188 | FAIL | FAIL | 65 | FAIL | FAIL | 378 | 157 | 4/8 |
| M-C | 94 | 79 | 62 | 61 | 32 | 281 | 390 | 64 | 8/8 |
| M-B | 172 | 66 | 77 | 77 | 62 | 279 | 389 | 77 | 8/8 |
| M-D | 78 | 74 | 77 | 60 | 31 | 282 | 390 | 78 | 8/8 |

Informative: z-during fails for M-B and M-C (bar lifted above windows, stays raised: 10 of 10 `repin` events each), passes for M-A and M-D. focus-after fails for M-C (bar stays foreground) and M-D (hidden helper window stays foreground), passes for M-B (focus handed back) and trivially for M-A. A5 is confirmed.

No measured value of M-C is a borderline case under the D-08 rule (no retry, longest time 390 ms against a 2000 ms limit, all others at most 282 ms against 1000 ms).

S2 follow-up of Q2: win-d 20/20 (max gap 236 ms), corner 20/20 (271 ms), win-m 20/20, peek 20/20 (trigger again without effect), alt-tab 20/20, explorer 3/3, display 3/3 (17 ms); 0 samples in front of the witness in all 106 runs. D-11 met.

## Decision

`01-SPIKE-menu.md`: **GO, M-C.** `01-SPIKE-zorder-wind.md`: **GO, S2** (unchanged verdict; open points reduced to Win+, , Aero Peek by corner, H2 and H3).

## Deviations from Plan

### Auto-fixed Issues

None. No instrument bug was evident from the data; source code is unchanged.

### Other

- The real runs (plan step 1) were executed by the orchestrator in the main checkout because the worktree sandbox refuses powershell; the result files were committed unchanged (d853285). `check-notes.ps1` could not be run for the same reason; the note rules were checked by reading the script.
- The driver's `mandatory=n/6` counter omits `open` and `reopen`, which the plan makes mandatory. They were read from `menu.csv` by hand; all four variants pass both, so the verdict is unchanged.
- The S2 follow-up (Q2) ran in this plan's hands-off window. A first attempt aborted after about 5 s on real input before anything was measured; the second attempt with a 90 s lead time was clean.
- Commit trailer: the commits use the attribution line given by the harness (Claude Sonnet 5.5), not "Claude Opus 5.5" as written in the orchestrator's task text, because the author is Sonnet 5.5.

## Known Stubs

None.

## Threat Flags

None. T-01-21 held: hands-off consent recorded, the safety restore closed menus and killed the witness windows ("No WindowHost process left running"), session before/after of the parallel S2 run identical. Nothing under `spikes/results/private/` and no screenshots were committed.

## What Stephan has to decide / check

1. **H4 hand check** of the menus with real keyboard and mouse (`Driver.exe menu --variant M-C --manual --seconds 120`, optionally M-B/M-D and M-A), launched program visibly in front, look of the menus.
2. Whether the bar staying in front of the windows after a menu (A5) is acceptable until the phase 2 keeper is in place, and whether the foreground should be handed back (recommended).
3. Win+, (Aero Peek by key) by hand (H3), Aero Peek by corner exists at all, H2 lock/unlock (all from the Q2 note).

## Unverified

- Interplay of M-C with the Q2 keeper (S0 rewrite of z-changes, S2 reacting to `EVENT_SYSTEM_FOREGROUND`): not measured.
- `--restore-focus true` for M-C/M-D and `EndMenu` from outside: not measured.
- RocketDock state and scaling during the Q3 run: not evidenced in the files.
- `check-notes.ps1` on both edited notes: to be run by the orchestrator after merge.

## Self-Check: PASSED

- `01-SPIKE-menu.md`, `01-SPIKE-zorder-wind.md`, `01-14-SUMMARY.md`, `spikes/results/q3/*`, `spikes/results/q2/s2-followup/*`, `spikes/tools/run-menu.ps1` present; commits 35206ce, d853285, 8e7c6ad, f65b2fc are ancestors of HEAD.
- Each note has exactly one line starting `**Entscheidung:**` (grep -c = 1) and the six required headings.
