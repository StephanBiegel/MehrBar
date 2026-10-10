---
phase: 01-risiko-spikes
plan: 08
subsystem: testing
tags: [dotnet10, cswin32, xunit-v3, mtp, tooling, spike-q7]

requires:
  - phase: 01-risiko-spikes
    provides: "Scaffold (01-01), base/host/input CsWin32 lists and spike projects (01-02, 01-03, 01-04)"
provides:
  - "spikes/results/q7-tooling.txt: captured SDK version, MTP test run, filter/list syntax trials, CsWin32 multi-list build summaries"
  - "01-SPIKE-tooling.md: Q7 note with decision GO (CLI part), VS Code Test Explorer queued as H6"
affects: [01-17, 01-18, phase-02, phase-03]

actuals:
  tokens: 5150
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "MTP single-test filter: dotnet test --filter-method \"*Name*\" / --filter-class \"*Class*\"; list with dotnet test --list-tests (not -- --list-tests)"
    - "Exit code 8 from dotnet test means the filter matched nothing; verify commands must treat it as failure"
    - "CsWin32 name lists per topic in several NativeMethods.txt files, included by one wildcard AdditionalFiles item"

key-files:
  created:
    - spikes/results/q7-tooling.txt
    - .planning/phases/01-risiko-spikes/01-SPIKE-tooling.md
  modified: []

key-decisions:
  - "Q7 decided GO for the CLI part: CsWin32 0.3.360 builds WindowHost and Driver with 0 warnings under -warnaserror with four NativeMethods.txt lists, dotnet test (MTP) runs 19/19 green"
  - "Filter syntax for later verify commands is --filter-method / --filter-class; --list-tests is the listing form"
  - "VSTest fallback B stays prepared but is not triggered; it is decided only if hand item H6 fails (switch belongs to plan 01-18)"

patterns-established:
  - "Verify commands parse the dotnet exit code (0 / 8), not the localized German console text"

requirements-completed: []

coverage:
  - id: D1
    description: "CsWin32 0.3.360 builds WindowHost and Driver warning-free with several per-topic NativeMethods.txt lists"
    verification:
      - kind: other
        ref: "dotnet build spikes/WindowHost -c Release -warnaserror --no-incremental && dotnet build spikes/Driver -c Release -warnaserror --no-incremental"
        status: pass
    human_judgment: false
  - id: D2
    description: "dotnet test via MTP is green and the single-test filter and list syntax are recorded"
    verification:
      - kind: other
        ref: "dotnet test; dotnet test --filter-method \"*Core_assembly*\"; dotnet test --list-tests"
        status: pass
    human_judgment: false
  - id: D3
    description: "01-SPIKE-tooling.md passes the note gate with exactly one decision line"
    verification:
      - kind: other
        ref: "powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/check-notes.ps1 -Note 01-SPIKE-tooling.md"
        status: pass
    human_judgment: false
  - id: D4
    description: "VS Code C# Dev Kit Test Explorer finds and runs ScaffoldTests and ZoomAnchorTests and loads MehrBar.sln"
    verification: []
    human_judgment: true
    rationale: "No CLI path for the Dev Kit UI; queued as hand item H6 in plan 01-17 (D-06)"

plan_head_before: bcd3f4fa4b1cb89df867124abcbed7b8c940d8d5
plan_head_after: 08861ff74b91bc83d3f21b804e1ef74fb88b8e35
commits: 2
duration: 3min
completed: 2026-10-10
status: complete
---

# Phase 1 Plan 08: Q7 Tooling Summary

**CsWin32 0.3.360 builds the spike projects warning-free from four per-topic NativeMethods.txt lists, xunit.v3.mtp-v2 runs 19/19 green via `dotnet test`, and the working MTP filter/list syntax is recorded; VS Code Test Explorer is queued as hand item H6.**

## Performance

- **Duration:** about 3 min
- **Started:** 2026-10-10T10:23:00Z
- **Completed:** 2026-10-10T10:27:00Z
- **Tasks:** 2
- **Files modified:** 2 (both created)

## Accomplishments
- `spikes/results/q7-tooling.txt` captures SDK 10.0.401 (global.json pin), the MTP test run (19 of 19), nine filter/list trials with exit codes, the four NativeMethods.txt lists, and `--no-incremental` Release builds of WindowHost and Driver with `-warnaserror` (0 warnings, 0 errors), plus a generated-sources check written to `obj/` and removed afterwards.
- Filter findings: `--filter-method` works both directly and after `--`; `--filter-class` works; a filter that matches nothing exits with code 8; the VSTest-style `--filter` was also accepted. Listing works with `dotnet test --list-tests` but `dotnet test -- --list-tests` lists nothing.
- `01-SPIKE-tooling.md` (German, six headings) carries the single decision line GO for the CLI part and the consequences for phases 2 and 3; it passes `check-notes.ps1`.

## Task Commits

1. **Task 1: End-to-end tooling capture -> q7-tooling.txt** - `c8e5f12` (feat)
2. **Task 2: The Q7 note** - `08861ff` (docs)

**Plan metadata:** committed separately after this file (docs: complete plan).

## Files Created/Modified
- `spikes/results/q7-tooling.txt` - raw command lines and outputs plus a hand-written readout block
- `.planning/phases/01-risiko-spikes/01-SPIKE-tooling.md` - Q7 go/no-go note

## Decisions Made
- GO for CsWin32 0.3.360 and xunit.v3.mtp-v2 on the CLI; the Test Explorer part is deliberately not decided here.
- Verify commands in later plans should use `--filter-method`/`--filter-class` and rely on the exit code (0, or 8 for an empty filter), because the console text is localized German.
- CsWin32 pin for the product moves into the root `Directory.Packages.props` together with MehrBar.App in phase 2; `.sln` stays.

## Deviations from Plan

None - plan executed exactly as written. Small additions inside the task: a `--no-incremental` build variant (so the source generator really runs) and a generated-sources check into `obj/`, both recorded in the capture file.

## Issues Encountered
None. One honest limit is written into the note: the generated-sources sample proves names from `base`, `input` and `oracles` individually; for the `host` list the sampled name fell into a shared file, so the warning-free `-warnaserror` builds are the actual proof for that list.

## Known Stubs
None.

## Threat Flags
None. The capture file contains only SDK/build/test output and local repo paths (T-01-27 accepted); no packages were added or changed (T-01-SC).

## User Setup Required
None - no external service configuration required.

## Hand Part
H6 (VS Code C# Dev Kit loads `MehrBar.sln`, Test Explorer shows and runs `ScaffoldTests` and `ZoomAnchorTests`) remains open for plan 01-17. Not faked here. If it fails, the VSTest fallback B (STACK.md section 12) is switched in plan 01-18.

## Next Phase Readiness
- Later verify commands can use the recorded filter syntax.
- No blockers.

---
*Phase: 01-risiko-spikes*
*Completed: 2026-10-10*

## Self-Check: PASSED

- FOUND: spikes/results/q7-tooling.txt
- FOUND: .planning/phases/01-risiko-spikes/01-SPIKE-tooling.md
- FOUND: c8e5f12 (merge-base ancestor of HEAD)
- FOUND: 08861ff (merge-base ancestor of HEAD)
- Acceptance criteria: q7-tooling.txt contains `filter-method`, `NativeMethods.txt`, `10.0.401`, names base/host/input/oracles lists; note has exactly one `**Entscheidung:**` line, references the txt, lists H6 under Grenzfälle für Stephan.
