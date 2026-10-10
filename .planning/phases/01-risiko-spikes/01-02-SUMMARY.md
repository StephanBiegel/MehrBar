---
phase: 01-risiko-spikes
plan: 02
subsystem: testing
tags: [cswin32, wpf, dotnet10, central-package-management, spike-harness]

requires:
  - phase: 01-risiko-spikes
    provides: "Plan 01-01 scaffold (root Directory.Packages.props, global.json, ignore rules)"
provides:
  - "spikes/Directory.Packages.props: self-contained CsWin32 0.3.360 pin for everything under spikes/"
  - "Research-verified CsWin32 base name list (69 names) and NativeMethods.json (allowMarshaling false, useSafeHandles false)"
  - "RepoPaths (worktree-safe MainRoot, PrivateResults, DryResults, HostExe, PhaseDir) and Stats (nearest-rank percentile, Summarize)"
  - "WindowHost (WPF) skeleton building with CsWin32 under -warnaserror, --exit-after-ms"
  - "Driver (console) case registry, partial CaseContext, fixed exit codes, stats-selftest"
  - "spikes/README.md with the explicit Win32-in-spikes exception and safety rules"
affects: [01-03, 01-04, 01-06, 01-07, 01-08]

actuals:
  tokens: 6500
  tasks: 2
  commits: 2

tech-stack:
  added: ["Microsoft.Windows.CsWin32 0.3.360 (spikes only, PrivateAssets all)"]
  patterns:
    - "Spikes pin packages in their own nested Directory.Packages.props; it does not import the root file"
    - "CsWin32 lists: one NativeMethods.txt per topic under spikes/Common/native/<topic>/, a name in only one list"
    - "Driver cases: [DriverCase(\"name\")] + IDriverCase, found by reflection; CaseContext extended only via own partial files"

key-files:
  created:
    - spikes/Directory.Packages.props
    - spikes/Common/native/base/NativeMethods.txt
    - spikes/Common/NativeMethods.json
    - spikes/Common/RepoPaths.cs
    - spikes/Common/Stats.cs
    - spikes/WindowHost/WindowHost.csproj
    - spikes/WindowHost/app.manifest
    - spikes/WindowHost/App.xaml
    - spikes/WindowHost/App.xaml.cs
    - spikes/Driver/Driver.csproj
    - spikes/Driver/app.manifest
    - spikes/Driver/Program.cs
    - spikes/README.md
  modified: []

key-decisions:
  - "Nested spikes/Directory.Packages.props does not import the root props: MSBuild stops at the nearest file, so the root xunit pin never reaches the spikes (no NU1008) and deleting spikes/ leaves the product untouched"
  - "RepoPaths.cs carries an explicit 'using System.IO': WPF projects drop System.IO from the implicit usings, and the file is linked into both WPF and console projects"

patterns-established:
  - "Spike build/verify command: dotnet build spikes/<project> -c Release -warnaserror, then run the exe from bin/Release/net10.0-windows"

requirements-completed: []

coverage:
  - id: D1
    description: "WindowHost (WPF) builds warning-free with CsWin32 0.3.360 under -warnaserror and runs; GetWindowLong/SetWindowLong/SetWindowPos with WS_EX_NOACTIVATE and HWND_BOTTOM execute at runtime and the process exits 0 after --exit-after-ms 500"
    verification:
      - kind: other
        ref: "dotnet build spikes/WindowHost -c Release -warnaserror && spikes/WindowHost/bin/Release/net10.0-windows/WindowHost.exe --exit-after-ms 500"
        status: pass
    human_judgment: false
  - id: D2
    description: "Driver (console) builds warning-free, --list prints stats-selftest, stats-selftest exits 0 (percentiles 1..100, single element, empty input), unknown case exits 2"
    verification:
      - kind: other
        ref: "dotnet build spikes/Driver -c Release -warnaserror && spikes/Driver/bin/Release/net10.0-windows/Driver.exe stats-selftest"
        status: pass
    human_judgment: false
  - id: D3
    description: "spikes/README.md states the Win32 exception to the product rule, the abort key, ignored results folders and the Nachmessen pointer"
    verification:
      - kind: other
        ref: "grep -c -E 'App/Interop|Ctrl\\+Alt\\+Q|results/private|Nachmessen' spikes/README.md (5, required >= 4)"
        status: pass
    human_judgment: false

duration: 5min
completed: 2026-10-10
plan_head_before: 9f32a6643ea6d0732244ea3ccbb7d0232e6bdfde
plan_head_after: 52597f08f421458e4176b37b370014c982c4618e
commits: 2
status: complete
---

# Phase 1 Plan 02: Spike skeleton with CsWin32 Summary

**Self-contained spike skeleton under `spikes/`: CsWin32 0.3.360 pinned in its own `Directory.Packages.props`, a buildable WPF WindowHost and console Driver (asInvoker/PerMonitorV2, case registry with fixed exit codes, partial CaseContext), shared RepoPaths/Stats helpers and a README that declares the Win32-in-spikes exception.**

## Performance

- **Duration:** 5 min
- **Started:** 2026-10-10T07:56Z (approx.)
- **Completed:** 2026-10-10T08:01Z
- **Tasks:** 2
- **Files created:** 13

## Accomplishments
- CsWin32 build path proven (input for the Q7 note in 01-08): both `spikes/WindowHost` and `spikes/Driver` build with 0 warnings / 0 errors under `-warnaserror` on SDK 10.0.401 using the research name list; WindowHost executes the generated `GetWindowLong`/`SetWindowLong`/`SetWindowPos` calls at runtime and exits 0.
- The solution build `dotnet build MehrBar.sln -warnaserror` is unaffected (0 warnings, 0 errors); the spikes are outside the solution.
- `Driver.exe --list` prints `stats-selftest`; `stats-selftest` passes (p50/p95/p99 on 1..100, single element, empty input with NaN values and Count 0); an unknown case exits 2.
- `RepoPaths` verified once with a temporary case: RepoRoot and MainRoot both resolve to the main checkout, `PrivateResults` is created on demand (temporary case and folder removed again).

## Task Commits

1. **Task 1: End-to-end CsWin32 spike build (tracer)** - `ad075b5` (feat)
2. **Task 2: spikes/README.md** - `52597f0` (docs)

Tracer gate: `human_verify_mode` is `end-of-phase` and the tracer `<verify>` is automated-only, so it was re-run after the commit (build with `-warnaserror`, run of WindowHost and Driver); it passed and expansion continued.

## Files Created
- `spikes/Directory.Packages.props` - central pin, CsWin32 0.3.360
- `spikes/Common/native/base/NativeMethods.txt`, `spikes/Common/NativeMethods.json` - generator input and options
- `spikes/Common/RepoPaths.cs`, `spikes/Common/Stats.cs` - shared helpers (linked into both projects)
- `spikes/WindowHost/{WindowHost.csproj,app.manifest,App.xaml,App.xaml.cs}` - WPF skeleton
- `spikes/Driver/{Driver.csproj,app.manifest,Program.cs}` - case registry, CaseContext, exit codes, stats-selftest
- `spikes/README.md` - purpose, Win32 exception, projects, folders, safety rules

## Decisions Made
- Nested `spikes/Directory.Packages.props` stays independent of the root file (see key-decisions); it answers the 01-01 note on RESEARCH Pitfall 10 / NU1008.
- Win32 style in `App.xaml.cs`: `GetWindowLong`/`SetWindowLong` (not the `Ptr` names) and the enum `WINDOW_EX_STYLE`, as the research traps require.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Missing `System.IO` in WPF projects**
- **Found during:** Task 1 (first WindowHost build)
- **Issue:** WPF projects drop `System.IO` from the implicit global usings, so the linked `RepoPaths.cs` failed with CS0103/CS0246 for `Path`, `File`, `Directory`, `DirectoryInfo`.
- **Fix:** Added an explicit `using System.IO;` to `RepoPaths.cs` (valid in both WPF and console projects).
- **Files modified:** `spikes/Common/RepoPaths.cs`
- **Verification:** Both projects build with 0 warnings / 0 errors.
- **Commit:** `ad075b5` (fixed before the first commit)

### Process note

**2. [Process] Protected-branch assertion not applied**
- Same situation as in 01-01: the dispatch states this is a sequential executor committing on `main` (branching_strategy none), so the `main` branch was used without the `git.allow_default_branch_commits` override. `.planning/config.json` was not touched.

---

**Total deviations:** 1 auto-fixed (Rule 3), 1 process note.
**Impact on plan:** None on scope; one line added to a shared helper.

## Issues Encountered
- A first acceptance grep for `Common\native` reported 0 because of backslash escaping in the grep pattern, not in the files; a fixed-string grep confirmed both csproj files contain it.
- Git prints LF-to-CRLF warnings for the new files (autocrlf); harmless.

## Known Stubs
None. `WindowHost` shows only an empty 10x10 transparent window by design; variants are added by later plans.

## Threat Flags
None. T-01-05: both manifests request `asInvoker`. T-01-SC: only `Microsoft.Windows.CsWin32` 0.3.360 is pinned exactly, from the default nuget.org source, `PrivateAssets="all"`, no other package.

## User Setup Required
None.

## Next Phase Readiness
- Harness plans can extend `CaseContext` in their own partial files (`CaseContext.Safety.cs` in 01-04, `CaseContext.Host.cs` in 01-07) and add new CsWin32 names only in their own `spikes/Common/native/<topic>/NativeMethods.txt`. Multi-list merging of two topic files is still unproven until 01-03 and 01-04 merge; 01-08 reports it.
- `INPUT`/`KEYBDINPUT`/`MOUSEINPUT` were not added to the base list; `SendInput` compiles without them, and the driver plans add the struct/enum names if they use them explicitly.
- `HostExe` assumes `dotnet build spikes/WindowHost -c Release` output (`bin/Release/net10.0-windows`).

## Self-Check: PASSED

- All 13 files exist; commits `ad075b5` and `52597f0` are on `main`.
- Acceptance criteria of both tasks re-run and passing (Packages.props pin, csproj contents, manifests, base list names, `partial class CaseContext`, `--list`, README grep count 5).
- Plan-level verification: both builds with `-warnaserror` green, `WindowHost.exe --exit-after-ms 500` exit 0, `Driver.exe stats-selftest` exit 0.

---
*Phase: 01-risiko-spikes*
*Completed: 2026-10-10*
