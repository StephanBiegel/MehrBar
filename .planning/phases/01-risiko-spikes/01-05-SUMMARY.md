---
phase: 01-risiko-spikes
plan: 05
subsystem: layout
tags: [zoom, anchor, cursor, dotnet10, xunit-v3, powershell, measurement, spike-q5]

requires:
  - phase: 01-risiko-spikes
    provides: "Scaffold (plan 01-01): MehrBar.Core, MehrBar.Tests with xunit.v3.mtp-v2, check-notes.ps1 note gate"
provides:
  - "MehrBar.Core.Layout: ZoomKernel (cosine falloff), ZoomEngine (cursor/center anchor, rest-space distances, scaling separator slots), ZoomAnchorMode, ZoomParams, RestItem, ZoomResult"
  - "RocketDockReference fixture: rest geometry, per-row pointer fractions, radius and separator-line positions measured from the five reference screenshots"
  - "ZoomAnchorTests: cursor-anchor prediction of the four reference rows, separator-line prediction, center falsification control, radius reproducibility, fixed-separator miss"
  - "spikes/tools/measure-zoom-fixture.ps1 and spikes/ZoomCheck (report-only residuals and sensitivity), spikes/results/q5-zoom-residuals.csv"
  - "01-SPIKE-zoom-anchor.md: Q5 note, decision GO"
affects: [phase-02, phase-03, 01-spike-ergebnisse]

actuals:
  tokens: 23600
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Core zoom math is axis-neutral and pure; rest-space distances, displayed-space output"
    - "Measured fixture with a source comment per value; expected bar edges only used as the thing compared against"
    - "Falsification control in the same test class (center anchor must miss; fixed separators must miss)"

key-files:
  created:
    - src/MehrBar.Core/Layout/ZoomKernel.cs
    - src/MehrBar.Core/Layout/ZoomEngine.cs
    - src/MehrBar.Tests/Fixtures/RocketDockReference.cs
    - src/MehrBar.Tests/ZoomAnchorTests.cs
    - spikes/tools/measure-zoom-fixture.ps1
    - spikes/ZoomCheck/ZoomCheck.csproj
    - spikes/ZoomCheck/Program.cs
    - spikes/results/q5-zoom-residuals.csv
    - .planning/phases/01-risiko-spikes/01-SPIKE-zoom-anchor.md
  modified: []

key-decisions:
  - "Q5 GO for anchor cursor: all uncropped edges within 4.8 px, separator lines within 5.1 px, center misses 17.6 / 37.9 px, r = 2.58 measured from the neighbours"
  - "Separator slots scale with the zoom factor (RestItem.Zooms = true for separators; the line image itself does not scale): found from the 63 px glyph gap in screenshot 4 after the plan's fixed-separator hypothesis missed by 44 px"
  - "Left edges of -3 are real, not a crop: the first separator line moved exactly 112 px in all three rows; the tests still treat them as a bound as planned"
  - "ZoomSize stays 121 (layout slot of the Nachtrag) although visible cursor glyphs measure 110-115 px"

patterns-established:
  - "Screenshots are aligned by correlating the background below the bar; the shift is independent of every bar edge"
  - "Spike check tools link the test fixture as a compile item (spike depends on product, never the reverse)"

requirements-completed: []

coverage:
  - id: D1
    description: "Core zoom kernel and engine (cursor/center anchor, rest-space distances) with the measured rest geometry reproduced (109..698, groups 6/6/3/1, separators at 323.5/531.5/643.5)"
    verification:
      - kind: unit
        ref: "src/MehrBar.Tests/ZoomAnchorTests.cs#Rest_layout_reproduces_reference_extent"
        status: pass
      - kind: unit
        ref: "src/MehrBar.Tests/ZoomAnchorTests.cs#Kernel_matches_measured_falloff"
        status: pass
      - kind: unit
        ref: "src/MehrBar.Tests/ZoomAnchorTests.cs#Center_anchor_keeps_bar_middle_fixed"
        status: pass
    human_judgment: false
  - id: D2
    description: "The four reference rows are predicted by anchor cursor within +-6 px from independently measured inputs; separator lines of every screenshot as a second check"
    verification:
      - kind: unit
        ref: "src/MehrBar.Tests/ZoomAnchorTests.cs#Cursor_anchor_predicts_reference_row"
        status: pass
      - kind: unit
        ref: "src/MehrBar.Tests/ZoomAnchorTests.cs#Cursor_anchor_predicts_separator_lines"
        status: pass
      - kind: unit
        ref: "src/MehrBar.Tests/ZoomAnchorTests.cs#Measured_radius_is_the_mean_of_the_neighbour_samples"
        status: pass
    human_judgment: false
  - id: D3
    description: "The center anchor and the fixed-separator model demonstrably fail on the same inputs (the rows can discriminate)"
    verification:
      - kind: unit
        ref: "src/MehrBar.Tests/ZoomAnchorTests.cs#Center_anchor_misses_reference_rows"
        status: pass
      - kind: unit
        ref: "src/MehrBar.Tests/ZoomAnchorTests.cs#Fixed_separators_miss_the_rows_next_to_a_separator"
        status: pass
    human_judgment: false
  - id: D4
    description: "Q5 go/no-go note with measured inputs, residuals, sensitivity and one decision line"
    verification:
      - kind: other
        ref: "powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/check-notes.ps1 -Note 01-SPIKE-zoom-anchor.md"
        status: pass
    human_judgment: true
    rationale: "The GO rests on a point estimate with a thin margin (5.1 of 6 px) and on a separator rule found after the first miss; Stephan decides whether to accept it (listed in the note under Grenzfaelle)"

duration: 19min
completed: 2026-10-10
plan_head_before: 279020e67f0dc2ea3cef86e3f3ec849d19a2079e
plan_head_after: 6d6f83bed7e7cdb1728f4e5952832a61fb2589f9
commits: 3
status: complete
---

# Phase 1 Plan 05: Zoom Anchor Spike (Q5) Summary

**Core zoom math (cosine kernel, `cursor`/`center` anchor, separator slots that scale with the zoom) predicts the four RocketDock reference rows within 5.1 px from inputs measured off the screenshots, while `center` misses by up to 38 px; note decision GO.**

## Performance

- **Duration:** 19 min
- **Started:** 2026-10-10T08:17Z (approximate, start time was not captured by a command)
- **Completed:** 2026-10-10T08:36Z
- **Tasks:** 3
- **Files:** 9 created, 0 modified

## Accomplishments

- `dotnet build MehrBar.sln -warnaserror` and `dotnet test` are green (19 tests, none skipped); `MehrBar.Core` stays on `net10.0` without packages; the solution lists only Core and Tests, `spikes/ZoomCheck` stays outside.
- Measured inputs, none taken from the bar edges: raster 32 and separator slot 16 (from the three separator lines), margins 14.5, background-aligned image shifts, pointer fraction per row (0.489 / 0.067 / 0.933 / 0.657, from the cursor icon's own displacement), radius 2.58 (from four neighbour sizes, spread 2.41 to 2.72).
- Cursor anchor, baseline: edges −3.0 to +1.1 px off (cropped edges as bounds), worst edge 4.8 px (Excel, right), 12 separator-line positions within 5.1 px; `center` misses the 2nd-icon row by 17.6 px and the Excel row by 37.9 px.
- Sensitivity is reported honestly: the verdict flips at `r` ± 0.1, `f` + 0.02 or zoom size ± 4 (see Deviations and the note); the anchor itself stays `cursor` in every variant (at most 14.4 px against at least 38 px).

## Task Commits

1. **Task 1: Zoom kernel and engine with measured rest geometry (tracer)** - `efb37b8` (feat)
2. **Task 2: The four rows as a prediction, center control, ZoomCheck** - `fce9676` (feat)
3. **Task 3: Q5 note** - `6d6f83b` (docs)

Tracer gate: `human_verify_mode` is `end-of-phase` and the tracer `<verify>` is automated-only, so it was re-run (build with `-warnaserror`, `dotnet test`, grep of the three test names) after the task commit; it passed and the expansion tasks followed.

**Plan metadata:** committed separately as `docs(01-05): complete zoom anchor plan`.

## Files Created/Modified

- `src/MehrBar.Core/Layout/ZoomKernel.cs`, `ZoomEngine.cs` - pure axis-neutral zoom layout (`ZoomEngine.Compute`)
- `src/MehrBar.Tests/Fixtures/RocketDockReference.cs` - measured fixture, one source comment per value
- `src/MehrBar.Tests/ZoomAnchorTests.cs` - 19 test cases (cursor prediction, separator lines, center control, radius, fixed-separator miss)
- `spikes/tools/measure-zoom-fixture.ps1` - separator lines, background alignment, glyph boxes, pointer fractions, radius, separator positions (raw output only under `spikes/results/private/`)
- `spikes/ZoomCheck/ZoomCheck.csproj`, `Program.cs` - report-only residual and sensitivity check
- `spikes/results/q5-zoom-residuals.csv` - 400 rows of numbers
- `.planning/phases/01-risiko-spikes/01-SPIKE-zoom-anchor.md` - Q5 note (German), decision GO

## Decisions Made

See key-decisions. The separator rule is the notable one: it changes the plan's interface comment (`Zooms = false` for separators) to `Zooms = true` in the fixture; `ZoomEngine` supports both through the flag.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Model] Separator slots must scale with the zoom**
- **Found during:** Task 2 (first run of the four rows)
- **Issue:** With fixed-length separators (the plan's `Zooms = false`), the Publisher row misses its right edge by 44 px and the Excel row pushes its left edge 29 px right of the crop bound; the measured glyph gap between Publisher and Excel in screenshot 4 is 63 px against 18 px at rest.
- **Fix:** Extra room of a zooming slot is `Length * (ZoomSize/IconSize - 1) * Factor(d, r)` and separators zoom (spacing only). No free parameter; applied to all rows. The plan's literal model is kept as test `Fixed_separators_miss_the_rows_next_to_a_separator` and as variant `fixed-separators` in the CSV.
- **Files modified:** `ZoomEngine.cs`, `RocketDockReference.cs`, `ZoomAnchorTests.cs`
- **Commit:** `fce9676` (rule already in the engine of `efb37b8`)

**2. [Rule 2 - Missing check] Second independent prediction added**
- **Found during:** Task 2
- **Issue:** The plan checks only bar edges; the separator lines of each screenshot are a measurement independent of the edges and test the layout more strictly.
- **Fix:** `Cursor_anchor_predicts_separator_lines` (12 values, ±6 px) and `Measured_radius_is_the_mean_of_the_neighbour_samples` (radius reproducible in code).
- **Commit:** `fce9676`

### Process notes

- The pre-commit protected-branch assertion was bypassed as instructed by the dispatch (sequential executor on `main`, `branching_strategy: none`).
- The ZoomSize of 121 (Nachtrag) is kept as given; visible cursor glyphs measure 110 to 115 px. Recorded in the note, not changed.

**Total deviations:** 2 (1 model correction, 1 added check). **Impact:** The decision GO depends on the separator rule being accepted; the note lists it with the thin accuracy margin under "Grenzfälle für Stephan".

## Issues Encountered

- The first measurement script run mis-parsed glyph centres because German number formatting put a decimal comma into the CSV; fixed by invariant culture.
- A scratch heredoc with the test edit failed in the shell; the test file was written in full instead.

## Known Stubs

None.

## Threat Flags

None. T-01-06 (screenshots are third-party): the script reads `samples/reference` in place and writes raw profiles and boxes only to `spikes/results/private/`; `git ls-files samples` prints nothing; the committed CSV and fixture contain numbers only. T-01-SC: no new packages.

## User Setup Required

None.

## Next Phase Readiness

- Phase 2 can use the rest layout (progress 0): raster 32, separator slot 16, margins 14.5; `ZoomEngine` is ready for phase 3.
- Phase 3 must recalibrate `r` and the zoom size against the full-screen re-shots and the zoomWidth 2/6/8 series (STATE pending todo); the kernel shape (neighbours r ≈ 2.72, next-but-one r ≈ 2.43) needs a look there.
- 01-SPIKE-ERGEBNISSE (summary note) can quote the Q5 line directly.

## Self-Check: PASSED

- Files exist: ZoomKernel.cs, ZoomEngine.cs, RocketDockReference.cs, ZoomAnchorTests.cs, measure-zoom-fixture.ps1, ZoomCheck.csproj, Program.cs, q5-zoom-residuals.csv, 01-SPIKE-zoom-anchor.md
- Commits exist on `main`: efb37b8, fce9676, 6d6f83b
- Acceptance criteria of all three tasks re-run and passing; plan-level verification (`dotnet test` 19 green, `check-notes.ps1 -Note 01-SPIKE-zoom-anchor.md` OK, ZoomCheck build with `-warnaserror` and run, solution lists only the two src projects, `git ls-files samples` empty, no `Skip = ` in the zoom tests).

---
*Phase: 01-risiko-spikes*
*Completed: 2026-10-10*
