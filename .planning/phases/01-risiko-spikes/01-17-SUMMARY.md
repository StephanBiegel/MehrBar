---
phase: 01-risiko-spikes
plan: 17
subsystem: testing
tags: [hand-part, real-input, d-06, d-07, d-08, spike-notes]

requires:
  - phase: 01-risiko-spikes
    provides: "seven spike notes with provisional decisions, Driver/Icons/WindowHost manual modes"
provides:
  - "spikes/tools/handteil.ps1: interactive hand pass H1-H7 + borderline decisions (-List, -Step, -From)"
  - "spikes/tools/collect-decisions.ps1: decision lines, variants and borderline items of the seven notes (text, -AsJson, -AsObject)"
  - "01-HANDTEIL.md: checklist with Stephan's results"
  - "spikes/results/handteil/summary.csv: step,item,result"
  - "Seven notes final under D-06/D-07/D-08 (H6 still open in the tooling note)"
affects: [01-18, phase-02-bar-window]

actuals:
  tasks: 3
  commits: 9
plan_head_before: 50b1050
plan_head_after: HEAD

key-files:
  created:
    - spikes/tools/handteil.ps1
    - spikes/tools/collect-decisions.ps1
    - spikes/results/handteil/summary.csv
    - .planning/phases/01-risiko-spikes/01-HANDTEIL.md
  modified:
    - spikes/Driver/Cases/ZOrderCase.cs
    - spikes/Driver/Cases/Q1Cases.cs
    - spikes/WindowHost/Variants/PlateH1.cs
    - the seven 01-SPIKE-*.md notes

key-decisions:
  - "Hand pass H1-H5 and H7 passed; the hand confirms every automated verdict (D-07): H1 plate, S2 guard, M-C menu, R1/hw rendering"
  - "Win+, (Aero Peek per key) was a measurement artefact of the injected key release: the bar returns at once with real keys"
  - "Q4 decision UNKLAR -> GO R1/hw: drop share measured on screen with RocketDock as the benchmark instead of 1 % (Stephan, D-08)"
  - "Q5: GO stays, r recalibrated in phase 3, divider rule accepted (Stephan, D-08)"
  - "H6 (VS Code Test Explorer) not conclusive: the script's 'code' start closed at once; Stephan checks by hand, 01-18 applies fallback B if it fails"

requirements-completed: []

coverage:
  - id: D1
    description: "Hand pass prepared end to end (scripts, checklist)"
    verification:
      - kind: other
        ref: "handteil.ps1 -List prints H1-H7; collect-decisions.ps1 prints 7 decision lines"
        status: pass
    human_judgment: false
  - id: D2
    description: "Stephan's hand pass H1-H7 and borderline decisions"
    verification:
      - kind: other
        ref: "spikes/results/handteil/summary.csv (rows for H1-H7 and G)"
        status: pass
    human_judgment: true
    rationale: "Eye, real keys and mouse, secure desktop; H6 still open"
  - id: D3
    description: "Hand results and decisions applied to the seven notes"
    verification:
      - kind: other
        ref: "check-notes.ps1 on the seven notes: all OK; no provisional decision text left"
        status: pass
    human_judgment: false

completed: 2026-10-19
status: complete
---

# Phase 1 Plan 17: Hand pass H1-H7 Summary

**Stephan's single hand pass confirmed every automated verdict with real input; the notes are final, Q4 turned from UNKLAR to GO (R1, hardware, on-screen drop share against RocketDock), and only the VS Code Test Explorer check (H6) remains open.**

## Accomplishments

- Hand-part runner and decision collector written; checklist `01-HANDTEIL.md` filled with Stephan's results.
- H1 no flash, real clicks (93 caught inside, foreground stayed on the desktop), abort key; H2 unlock; H3 six real-key triggers; H4 M-C in all points, M-A fails by hand as measured; H5 smooth at 20 and 64 icons; H7 contact sheet clean.
- Borderline decisions by voice: on-screen drop share with RocketDock as benchmark; zoom GO with recalibration in phase 3; divider rule accepted. Everything else is noted as open for phase 2 (125/150 %, second bar, network file, 64/128 px icon frames).

## Deviations from Plan

- **Hand console started by Stephan:** the Hivemo session runs in Windows session 0 (no desktop, no abort hotkey), so the console could not be opened by the executor.
- **Fixes found during the pass** (Rule 1):
  - JSON over stdout broke on the console code page -> decisions read in-process (`4b0f66d`).
  - The bar sits at the bottom of the z-order, so steps that need to see it minimize all windows; H1a uses `firstframe --manual` (`caf17b0`).
  - The H1 plate is alpha 1 and invisible -> `--plate-outline on` for the manual hit check, hit area unchanged (`12b0a58`).
  - Driver output was swallowed by `Out-Null` (`afc9549`).
  - Emergency abort did not cut a manual z-order window short; `GapSampler.Complete` now honours the safety token (`c9352ff`).
  - `-From` to continue without overwriting finished answers (`ae1f1d7`).
- Borderline decisions were taken by voice instead of in step G; recorded in the private answers file and in `summary.csv`.

## Items for Stephan

1. H6: open VS Code on the repo yourself and check that the Test Explorer lists and runs the tests.
2. Optional: a real network file for H7 (`Icons.exe --case net-file --netfile <path> --threads sta`).

## Self-Check: PASSED

- check-notes.ps1: all seven notes OK; no "Messung nach Haltepunkt ausstehend" left.
- summary.csv has rows for H1-H7 and G.
