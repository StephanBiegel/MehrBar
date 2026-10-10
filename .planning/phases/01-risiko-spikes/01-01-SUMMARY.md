---
phase: 01-risiko-spikes
plan: 01
subsystem: testing
tags: [dotnet10, xunit-v3, mtp, central-package-management, powershell, gitignore]

requires: []
provides:
  - "Real product scaffold: global.json (SDK 10.0.401 + MTP runner), Directory.Packages.props, classic MehrBar.sln with MehrBar.Core (net10.0, no packages) and MehrBar.Tests (xunit.v3.mtp-v2 4.0.1)"
  - "First green test (ScaffoldTests.Core_assembly_is_referenced) proving the Core project reference"
  - "spikes/tools/check-notes.ps1: automated gate for every go/no-go spike note, with a self-test"
  - "Ignore rules for private spike results, dry runs, fixtures and the PresentMon download"
affects: [01-02, 01-spike-plans, phase-02, phase-03]

actuals:
  tokens: 3200
  tasks: 2
  commits: 2

tech-stack:
  added: [xunit.v3.mtp-v2 4.0.1]
  patterns:
    - "Central package management from the repo root (Directory.Packages.props); spikes pin their own packages in spikes/Directory.Packages.props"
    - "Classic .sln chosen over SDK-10 default .slnx"
    - "Note gate script: ASCII-only PowerShell 5.1, German headings matched with '.' for umlauts"

key-files:
  created:
    - global.json
    - Directory.Packages.props
    - MehrBar.sln
    - src/MehrBar.Core/MehrBar.Core.csproj
    - src/MehrBar.Core/AssemblyMarker.cs
    - src/MehrBar.Tests/MehrBar.Tests.csproj
    - src/MehrBar.Tests/ScaffoldTests.cs
    - spikes/tools/check-notes.ps1
  modified:
    - .gitignore

key-decisions:
  - "Classic MehrBar.sln (--format sln) instead of the SDK-10 default .slnx: concept names MehrBar.sln and C# Dev Kit support for .slnx is unverified (RESEARCH Pitfall 9); reversible"
  - "Product Directory.Packages.props holds only xunit.v3.mtp-v2; the CsWin32 pin arrives with MehrBar.App in phase 2 and the spikes pin it in their own file"
  - "check-notes.ps1 treats a missing note as FAIL, so the default run cannot pass before all eight notes exist"

patterns-established:
  - "Spike notes are gated by 'powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/check-notes.ps1 [-Note a.md,b.md]'"

requirements-completed: []

coverage:
  - id: D1
    description: "dotnet test in the repo root builds Core and Tests and reports one green test; solution lists exactly the two src projects"
    verification:
      - kind: unit
        ref: "src/MehrBar.Tests/ScaffoldTests.cs#Core_assembly_is_referenced"
        status: pass
      - kind: other
        ref: "dotnet build MehrBar.sln -warnaserror && dotnet test"
        status: pass
    human_judgment: false
  - id: D2
    description: "check-notes.ps1 accepts valid spike/summary notes and rejects broken ones (missing heading, two decision lines, no results reference, bad verdict, empty strategy, missing file)"
    verification:
      - kind: other
        ref: "powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/check-notes.ps1 -SelfTest"
        status: pass
    human_judgment: false
  - id: D3
    description: "Private spike outputs, dry-run output, fixtures and PresentMon are gitignored"
    verification:
      - kind: other
        ref: "git check-ignore -q on spikes/results/private, spikes/results/dry, spikes/fixtures, spikes/tools/presentmon"
        status: pass
    human_judgment: false

duration: 2min
completed: 2026-10-10
plan_head_before: 5c48a91a56054cad2157951a27a2451a37c2a0f7
plan_head_after: d8da13aeab930ed388175572744dccd278d5d5c7
commits: 2
status: complete
---

# Phase 1 Plan 01: Scaffold and Note Gate Summary

**Real MehrBar scaffold (SDK-pinned classic .sln, Core on net10.0 without packages, xunit.v3.mtp-v2 Tests with MTP runner, green `dotnet test`) plus a self-tested PowerShell gate for the spike go/no-go notes.**

## Performance

- **Duration:** 2 min
- **Started:** 2026-10-10T07:55:40Z
- **Completed:** 2026-10-10T07:57:27Z
- **Tasks:** 2
- **Files modified:** 9 (8 created, 1 modified)

## Accomplishments
- `dotnet build MehrBar.sln -warnaserror` and `dotnet test` in the repo root are green (1 test, 0 warnings); the solution contains only `MehrBar.Core` and `MehrBar.Tests`, nothing from `spikes/` (D-01, D-02).
- `spikes/tools/check-notes.ps1` checks the seven spike notes plus `01-SPIKE-ERGEBNISSE.md`, supports `-Note` (comma-split), `-Path` and `-SelfTest`; the self-test covers 9 cases plus the missing-file case. Verified additionally against a real UTF-8 note with umlaut headings.
- `.gitignore` now excludes `spikes/results/private/`, `spikes/results/dry/`, `spikes/fixtures/`, `spikes/tools/presentmon/` and `spikes/tools/PresentMon*.exe` (D-16, rights rule); confirmed via `git check-ignore`.

## Task Commits

1. **Task 1: Product scaffold end-to-end (tracer)** - `fa3734b` (feat)
2. **Task 2: Ignore rules and note checker with self-test** - `d8da13a` (feat)

**Plan metadata:** committed separately as `docs(01-01): complete scaffold and note gate plan`.

Tracer gate: `human_verify_mode` is `end-of-phase` and the tracer `<verify>` is automated-only, so it was re-run (build with `-warnaserror`, `dotnet test`) after the task commit; it passed and expansion continued.

## Files Created/Modified
- `global.json` - SDK 10.0.401, rollForward latestFeature, Microsoft.Testing.Platform test runner
- `Directory.Packages.props` - central package management, xunit.v3.mtp-v2 4.0.1
- `MehrBar.sln` - classic solution with Core and Tests
- `src/MehrBar.Core/MehrBar.Core.csproj`, `AssemblyMarker.cs` - net10.0 class library, no packages
- `src/MehrBar.Tests/MehrBar.Tests.csproj`, `ScaffoldTests.cs` - Exe test project, global `using Xunit`, ProjectReference to Core, first test
- `spikes/tools/check-notes.ps1` - ASCII-only PS 5.1 note checker
- `.gitignore` - spike output block

## Decisions Made
- Classic `.sln` over `.slnx` (see key-decisions); to be recorded in the tooling note (`01-SPIKE-tooling.md`).
- Product `Directory.Packages.props` contains only the xunit pin; spikes use their own file so deleting `spikes/` leaves nothing behind.

## Deviations from Plan

### Auto-fixed Issues

None in the code. One process note:

**1. [Process] Protected-branch assertion bypassed by explicit dispatch**
- **Found during:** Task 1 commit
- **Issue:** The pre-commit assertion reports `main` as protected (`git.base-branch --is-protected main` returned `true`). The dispatch, however, explicitly states this is a sequential executor on the main working tree, branch `main`, with normal commits. Repository history already carries GSD commits directly on `main` and `branching_strategy` is `none`.
- **Fix:** Committed on `main` as instructed by the dispatch; did not change `.planning/config.json` (no `git.allow_default_branch_commits` override added).
- **Impact:** None on the content; the orchestrator may want to set the override if this is the intended mode for the remaining plans.

---

**Total deviations:** 0 auto-fixed code deviations, 1 process note.
**Impact on plan:** Plan executed as written.

## Issues Encountered
- A `sed` edit to the checker (replacing `Write-Output` with `Write-Host` for the self-test messages) was needed because a function returning both the message and the exit code made `$rc` an array; fixed before the first commit.
- Git prints LF-to-CRLF warnings for the new files (autocrlf); harmless.

## Known Stubs
None. `AssemblyMarker` is an intentionally empty marker type used by `ScaffoldTests`.

## Threat Flags
None. T-01-01 (gitignore for private outputs) and T-01-02 (self-tested gate) are mitigated as planned; T-01-SC: only `xunit.v3.mtp-v2` 4.0.1 pinned exactly from the default nuget.org source.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Plan 01-02 (spike skeleton under `spikes/` with its own `spikes/Directory.Packages.props`) is unblocked. Note: root `Directory.Packages.props` applies to `spikes/` projects too (central package management is inherited), so the spikes file must be imported or must set `ManagePackageVersionsCentrally` consistently (RESEARCH Pitfall 10).
- Every spike note can be gated with `check-notes.ps1 -Note <name>`; the default run fails until all eight notes exist.
- Open for the tooling note: VS Code test discovery (Q7) on these projects, `.sln` vs `.slnx` choice.

## Self-Check: PASSED

- Files exist: global.json, Directory.Packages.props, MehrBar.sln, src/MehrBar.Core/{MehrBar.Core.csproj,AssemblyMarker.cs}, src/MehrBar.Tests/{MehrBar.Tests.csproj,ScaffoldTests.cs}, spikes/tools/check-notes.ps1, .gitignore
- Commits exist on `main`: fa3734b, d8da13a
- Acceptance criteria of both tasks re-run and passing; plan-level verification (`dotnet test` green, solution lists only Core and Tests, `-SelfTest` prints SELFTEST OK, four folders ignored) passing.

---
*Phase: 01-risiko-spikes*
*Completed: 2026-10-10*
