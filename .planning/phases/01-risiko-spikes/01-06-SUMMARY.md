---
phase: 01-risiko-spikes
plan: 06
subsystem: icons
tags: [shell-icons, cswin32, alpha, sta, spike-q6, wpf-imaging, powershell]

requires:
  - phase: 01-risiko-spikes
    provides: "spikes/Common (RepoPaths, Stats, NativeMethods.json), spikes/Directory.Packages.props with the CsWin32 pin, check-notes.ps1 note gate"
provides:
  - "spikes/Icons console spike: ShellIcon (IShellItemImageFactory, ICONONLY, GetDIBits, hard timeout), IconMetrics (alpha oracle, single Pbgra32 conversion, frozen), StoreLink, ContactSheet, LatencyBench"
  - "make-icon-fixtures.ps1: self-made fixtures under gitignored spikes/fixtures/icons"
  - "q6-icons.csv (13 cases, STA and MTA, 48 px size sweep) and q6-latency.csv (cold, warm x20, parallel 8, timeout)"
  - "01-SPIKE-icons.md: Q6 note, decision GO with STA worker pool and two fallback rules"
affects: [phase-02, phase-03, 01-spike-ergebnisse, hand-part-H7]

actuals:
  tokens: 17500
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Icon extraction on a fresh worker thread with fixed apartment and a hard Join timeout; a timed-out worker is abandoned (background thread)"
    - "Redacted committed CSV (short codes only) plus a private CSV with the full parsing names"
    - "CsWin32 no-marshalling COM: SHCreateItemFromParsingName takes Guid* and void**, GetImage and SetIDList are void and throw COMException"

key-files:
  created:
    - spikes/Icons/Icons.csproj
    - spikes/Icons/NativeMethods.txt
    - spikes/Icons/Program.cs
    - spikes/Icons/ShellIcon.cs
    - spikes/Icons/IconMetrics.cs
    - spikes/Icons/StoreLink.cs
    - spikes/Icons/ContactSheet.cs
    - spikes/Icons/LatencyBench.cs
    - spikes/tools/make-icon-fixtures.ps1
    - spikes/results/q6-icons.csv
    - spikes/results/q6-latency.csv
    - .planning/phases/01-risiko-spikes/01-SPIKE-icons.md
  modified: []

key-decisions:
  - "Q6 GO: 12 of 13 cases return a 256 px bitmap with straight alpha and transparent corners (or a fallback rule); the unreachable path ends in a timeout after 3.0 s and does not block a concurrent request"
  - "IconCache thread model is STA (small pool of dedicated workers): MTA gives identical pixels except for .url, where it silently returns a blank page and ignores IconFile; no latency advantage for MTA"
  - "Fallback rule for sources that only embed 48 px: the shell does not upscale but draws the icon small in a frame from 64 px up; if corner alpha > 0 at 256, request 48 and scale in WPF"
  - "Every shell bitmap has straight alpha; convert once to Pbgra32 and freeze (rawfringe up to 111 versus 0.0 after conversion)"

patterns-established:
  - "Alpha oracle as an automatic check (all-opaque, corner alpha, straight ratio, composite fringe) with an eye-check sheet in the private folder"

requirements-completed: []

coverage:
  - id: D1
    description: "256 px shell icons with ICONONLY and clean straight alpha for exe (48 px only, 256 px), lnk (file, Store), AppsFolder, folder, url (with and without IconFile), pdf, network roots; unreachable UNC bounded by a hard timeout"
    verification:
      - kind: other
        ref: "spikes/Icons/bin/Release/net10.0-windows/Icons.exe --all --out spikes/results/q6-icons.csv --sheet (exit 0; 13 cases, net-unreachable ok=false, timeout, 3001 ms)"
        status: pass
    human_judgment: true
    rationale: "The automatic oracle proves valid straight alpha and transparent corners; whether the icons look clean on light, dark and checkerboard (H7) needs Stephan's eye on the private contact sheet"
  - id: D2
    description: "STA versus MTA measured over 20 warm repetitions and 5 cold child processes, parallel 8 and timeout with a concurrent request"
    verification:
      - kind: other
        ref: "spikes/Icons/bin/Release/net10.0-windows/Icons.exe --latency --out spikes/results/q6-latency.csv (exit 0; 14 matching STA/MTA rows for exe-notepad, folder, lnk-store)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Q6 go/no-go note with one decision line and H7 queued for the hand part"
    verification:
      - kind: other
        ref: "powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/check-notes.ps1 -Note 01-SPIKE-icons.md"
        status: pass
    human_judgment: true
    rationale: "Real network file (net-file) and the contact sheet need Stephan (H7); the heuristic for framed 48 px icons is only validated on the one synthetic fixture"

duration: 12min
completed: 2026-10-10
plan_head_before: 40e5c4f6f72bd4e0de04308b36430c8f378f0610
plan_head_after: dae178dedfb98cea0846fdd462ae71d4f5fe9482
commits: 3
status: complete
---

# Phase 1 Plan 06: Icon Spike (Q6) Summary

**Shell icons at 256 px with ICONONLY return straight-alpha bitmaps with clean transparent corners for every item type; the IconCache must use STA workers because `.url` icons come back blank on MTA, and 48 px-only sources need a re-request at 48 px.**

## Performance

- **Duration:** about 12 min (start time not captured by a command; taken from commit times)
- **Completed:** 2026-10-10
- **Tasks:** 3
- **Files:** 12 created, 0 modified

## Accomplishments

- One path from `notepad.exe` to a frozen Pbgra32 bitmap with alpha metrics in a CSV row (tracer), then 13 cases: exe with 48 px and 256 px, `.lnk` to a file and to a Store app (generated through `IShellLinkW.SetIDList`), `shell:AppsFolder\<AUMID>`, folder, `.url` with and without `IconFile`, PDF, `L:`/`M:` roots (one icon query each, no listing) and an unreachable UNC path.
- All twelve returned bitmaps are 256x256 with straight alpha (`straight_ratio` above 0), corners transparent except the framed 48 px case; `fringe` after the single conversion is 0.0 everywhere while the raw data would score up to 111. No shortcut arrow on `.lnk`; the PDF gives the Acrobat icon, not a thumbnail.
- STA versus MTA answered: identical pixels everywhere except `.url`; MTA returns a blank page and ignores `IconFile`. Warm cost is 2 to 6 ms in both models, cold 30 to 55 ms, eight concurrent items take 32 ms against 62 to 68 ms sequentially.
- The unreachable path ends in `timeout` after 3001 ms (max 3014 over five runs) and a concurrent `exe-notepad` request needs 6 to 9 ms meanwhile.
- Q6 note decided GO with the thread model, per-item timeout, fallback rules and H7 for Stephan.

## Task Commits

1. **Task 1: Tracer, notepad to 256 px Pbgra32 with alpha metrics (tracer)** - `9208a67` (feat)
2. **Task 2: All item types, fixtures, Store link, network roots, contact sheet** - `a052cdf` (feat)
3. **Task 3: Latency, threading and the Q6 note** - `dae178d` (feat)

Tracer gate: `human_verify_mode` is `end-of-phase` and the tracer `<verify>` is automated-only, so it was re-run (build with `-warnaserror`, `Icons.exe --case exe-notepad`, grep of the 256x256 row) before the expansion tasks; it passed.

**Plan metadata:** committed separately as `docs(01-06): complete icon spike plan`.

## Files Created/Modified

- `spikes/Icons/*` - the spike (project, name list, CLI and case catalogue, extraction, alpha oracle, Store link, contact sheet, latency bench)
- `spikes/tools/make-icon-fixtures.ps1` - ASCII-only generator for all fixtures into `spikes/fixtures/icons/` (ignored)
- `spikes/results/q6-icons.csv`, `q6-latency.csv` - redacted results (no path column, short codes only)
- `.planning/phases/01-risiko-spikes/01-SPIKE-icons.md` - Q6 note (German), decision GO

## Decisions Made

See key-decisions. The STA decision supersedes the MTA suggestion of `STACK.md` for items with icon handlers and confirms Pitfall 7 in that respect.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] CsWin32 COM projection differs from the plan's assumption**
- **Found during:** Task 1 build
- **Issue:** `SHCreateItemFromParsingName` takes `Guid*`/`void**`, and `GetImage`/`SetIDList`/`Save` are void and throw `COMException` instead of returning an HRESULT.
- **Fix:** Pointer overload for the factory creation; `GetImage` and the Store-link calls wrapped in `catch (COMException)` that records the HRESULT as a short code.
- **Files modified:** `ShellIcon.cs`, `StoreLink.cs`
- **Commit:** `9208a67`, `a052cdf`

**2. [Rule 2 - Missing critical measurement] STA and MTA rows plus a 48 px size sweep in `q6-icons.csv`**
- **Found during:** Task 2
- **Issue:** The plan lists one thread model for `--all`; a first run on MTA showed identical blank `.url` icons, and the `exe-only48` result (corner alpha 66) needed a documented fallback rule, which requires knowing which request size is clean.
- **Fix:** `--all` defaults to both apartments (network roots and the unreachable path only once, on MTA); three extra rows (`exe-only48` at 48/64/128, note code `sweep`); the contact sheet shows the STA result and the 48 px request.
- **Files modified:** `Program.cs`, `ContactSheet.cs`
- **Commit:** `a052cdf`

**3. [Rule 2 - Added columns] Extra metrics beyond the plan's fixed columns**
- **Issue:** The `fringe` metric on converted data is 0 by construction, so it alone cannot show what the conversion fixes; the soft-edge share shows blur.
- **Fix:** `rawfringe` and `edge` as short numeric codes in the `note` column (header stays exactly as specified); `q6-latency.csv` has two trailing columns `size` and `hash` (header still contains the required prefix) to record size and pixel mismatches between STA and MTA.
- **Commit:** `9208a67`, `dae178d`

**Total deviations:** 3 (1 blocking tooling difference, 2 added measurements). **Impact:** none on scope; the additions make the decision and its fallback rules measurable.

### Process notes

- The protected-branch assertion was bypassed as instructed by the dispatch (sequential executor on `main`).
- `L:` and `M:` (company shares) were touched exactly as the plan allows: one `SHCreateItemFromParsingName` plus `GetImage` on the root each, 6 to 11 ms, nothing listed or written. `net-file` (a real file on a share) was not run; it is H7 for Stephan.
- No real mouse or keyboard input was sent; no window was shown (the contact sheet is rendered offscreen).

## Issues Encountered

- A scratch Python edit mangled backslashes in the UNC string; fixed with a verbatim string literal.
- Latencies were measured while the developer used the machine, so they are informative only (as the plan states).

## Known Stubs

None.

## Threat Flags

None. T-01-07: only fixed drive roots or an explicit `--netfile`, no enumeration, no writes. T-01-08: the committed CSVs contain no path, UNC name or user folder (grep clean); contact sheet and private CSV sit in the ignored `spikes/results/private/`. T-01-09: hard 3 s timeout measured. T-01-SC: no new packages (the generated fixture projects have no package references).

## User Setup Required

None.

## Next Phase Readiness

- Phase 2 IconCache: STA worker pool, 3 s timeout per item, convert once to Pbgra32 and freeze, framed-icon rule (re-request 48 px), placeholder while loading.
- Open for the hand part (H7): eye check of the private contact sheet and one real network file via `Icons.exe --case net-file --netfile <path> --threads sta`.
- Not measured: icons with native 64 or 128 px frames, 125/150 % scaling.

## Self-Check: PASSED

- Files exist: all twelve `key-files.created` present on disk; fixtures ignored (`git check-ignore` exit 0).
- Commits `9208a67`, `a052cdf`, `dae178d` are ancestors of HEAD.
- Acceptance criteria re-run: header of `q6-icons.csv` exact, no paths in the committed CSV, `net-unreachable` ok false at 3001 ms, contact sheet present, `q6-latency.csv` header contains the required prefix, `check-notes.ps1 -Note 01-SPIKE-icons.md` prints OK.
