---
phase: 01-risiko-spikes
plan: 18
subsystem: planning
tags: [summary-note, go-no-go, vstest-fallback, re-measure]

requires:
  - phase: 01-risiko-spikes
    provides: "seven final notes after the hand pass (01-17), frame-pacing note (01-19)"
provides:
  - "01-SPIKE-ERGEBNISSE.md: GO for planning phase 2, scope changes per spike, open points before phase 3"
  - "VSTest fallback B in the product test project (H6 failed with MTP, passes with VSTest)"
  - "spikes/README.md section Nachmessen with every run command of phase 1"
affects: [phase-02-bar-window, phase-03-zoom]

actuals:
  tasks: 3
  commits: 2
plan_head_before: 33a89a7
plan_head_after: HEAD

key-files:
  created:
    - .planning/phases/01-risiko-spikes/01-SPIKE-ERGEBNISSE.md
  modified:
    - global.json
    - Directory.Packages.props
    - src/MehrBar.Tests/MehrBar.Tests.csproj
    - spikes/README.md
    - .planning/phases/01-risiko-spikes/01-SPIKE-tooling.md
    - .planning/phases/01-risiko-spikes/01-HANDTEIL.md
    - spikes/results/handteil/summary.csv

key-decisions:
  - "Phase 1 summary: GO - phase 2 can be planned; all seven notes GO (H1, S2, M-C, R1/hw, cursor, STA, VSTest)"
  - "Tests run on VSTest (xunit.v3.mtp-off 4.0.1, xunit.runner.visualstudio 4.0.0, Microsoft.NET.Test.Sdk 18.10.1); global.json no longer selects MTP; VS Code Test Explorer finds and runs the tests (Stephan, 2026-10-19)"
  - "Open before phase 3: 64 icons on screen, zoom recalibration with new reference shots, hit area in the zoom overhang, 125/150 % and the second bar"

requirements-completed: []

coverage:
  - id: D1
    description: "Summary note with seven rows, scope changes, open points, one decision line"
    verification:
      - kind: other
        ref: "check-notes.ps1 (all eight notes OK)"
        status: pass
    human_judgment: false
  - id: D2
    description: "VSTest fallback B applied because H6 failed"
    verification:
      - kind: other
        ref: "dotnet test: 28/28 green on VSTest; Test Explorer confirmed by Stephan"
        status: pass
    human_judgment: true
    rationale: "Test Explorer UI can only be checked by hand"
  - id: D3
    description: "Re-measure commands consolidated in spikes/README.md"
    verification:
      - kind: other
        ref: "README Nachmessen contains run-zorder.ps1 and run-render-matrix.ps1"
        status: pass
    human_judgment: false

completed: 2026-10-19
status: complete
---

# Phase 1 Plan 18: Summary note and VSTest fallback Summary

**Phase 1 closes with GO for planning phase 2: every spike is GO, the tests run on VSTest because the VS Code Test Explorer did not find the MTP tests, and the open points before phase 3 are listed in 01-SPIKE-ERGEBNISSE.md.**

## Accomplishments

- `01-SPIKE-ERGEBNISSE.md` with one row per spike, the phase-2 scope changes (ZOrderKeeper S2, alpha-1 plate, native menu M-C, R1/hw with ComputeSmooth, STA IconCache, VSTest) and the open points before phase 3.
- Fallback B applied and confirmed in VS Code; tooling note, checklist and `summary.csv` updated (H6 passes with VSTest).
- `spikes/README.md` lists every run command, including the desktop-only rule for input-sending runs.

## Task Commits

- `796053d` build: switch the tests to VSTest (fallback B)
- this commit: summary note, README, H6 result, plan summary

## Deviations from Plan

- Task order: the fallback (Task 2) ran first, because H6 had just failed; the summary note followed.
- The tooling note's filter syntax was rewritten for VSTest (`--filter`), since the MTP syntax no longer applies.

## Self-Check: PASSED

- check-notes.ps1: all eight notes OK; `dotnet test` 28/28 green.
