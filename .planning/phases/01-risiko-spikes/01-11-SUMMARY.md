---
phase: 01-risiko-spikes
plan: 11
subsystem: testing
tags: [context-menu, trackpopupmenu, noactivate, driver-case, spike-harness, cswin32, wpf]

requires:
  - phase: 01-risiko-spikes
    provides: "01-03 WindowHost variant registry and witness role, 01-04 SafetySession/Input, 01-07 HostProcess/ZOrder/Foreground/DesktopPrep"
provides:
  - "Host variants M-A (WPF ContextMenu), M-B (WPF ContextMenu with temporary activation and focus handback), M-C (native TrackPopupMenuEx, bar as owner), M-D (native, invisible helper owner)"
  - "Host log events menu-ready, menu-open, menu-items, menu-window, menu-item, menu-launch, menu-close, menu-zpos, fg-before, fg-bar-activated, fg-owner-activated, fg-restored"
  - "Driver case `menu`: eleven checks per variant, per-check CSV, German summary table, BMP screenshot per open menu, --manual observation mode, --dry-run self-test"
  - "Fourth CsWin32 topic list spikes/Common/native/q3/NativeMethods.txt (WM_NULL, GetMenuItemRect)"
affects: [01-15, 01-17]

actuals:
  tokens: 31000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Menu variants combine with the plate: `--variant H1,M-A`; the self-test option `--selftest-menu true` only ever runs for non-activating M-A, the other three refuse it with a menu-selftest-refused row"
    - "WPF popup geometry is read from the popup window's Win32 rectangle plus in-popup offsets, logged right after layout and again after 250 ms; the Driver uses the newest row"
    - "Driver checks start from a normalised state: menu closed (Escape, click, restart bar), bar re-pinned to HWND_BOTTOM if lifted, witness in front"

key-files:
  created:
    - spikes/WindowHost/Variants/MenuWpf.cs
    - spikes/WindowHost/Variants/MenuNative.cs
    - spikes/Driver/Cases/MenuCase.cs
    - spikes/Common/native/q3/NativeMethods.txt
  modified: []

key-decisions:
  - "Item rectangles of the WPF menu are not taken from PointToScreen: at 130 ms after open it reported rectangles shifted up and left of the popup (793,304 instead of 964,403 for a menu opened at 960,400), and only the 250 ms later row is right. The host logs both phases and the Driver waits for a row at least 150 ms after menu-open"
  - "The case exits 0 when at least one variant passes the six mandatory D-15 points (close desktop/window/bar, Escape, arrows+Enter, program in front), otherwise 1; failing checks of a single variant are data, not harness errors"
  - "Native variants take --menu-restore-focus true (Driver: --restore-focus true) as an opt-in fallback measurement; default is the pure technique from the plan"
  - "Item 4 'Schliessen' only logs menu-item id=4; the launched witness gets --exit-after-ms 60000 as a safety net in case the Driver is killed"

patterns-established:
  - "Driver menu CLI: `menu --variant M-A|M-B|M-C|M-D|all [--dry-run] [--manual --seconds N] [--restore-focus true] [--out csv] [--summary-md md]`; `--out` is the per-check CSV, the event log is `<out>-events.csv`"
  - "Per-check CSV: variant,check,pass,ms,fg_before_class,fg_after_class,bar_zindex,notes"

requirements-completed: []

coverage:
  - id: D1
    description: "M-A end to end without input or activation: the host opens its WPF ContextMenu by itself 500 ms after start, logs menu-open, menu-items, menu-window, menu-zpos and menu-close, and the foreground window stays unchanged"
    verification:
      - kind: other
        ref: "dotnet build spikes/WindowHost -c Release -warnaserror && dotnet build spikes/Driver -c Release -warnaserror && Driver.exe menu --variant M-A --dry-run --summary-md spikes/results/dry/q3-tracer.md -> MENU DRY OK M-A, exit 0 (run five times)"
        status: pass
    human_judgment: false
  - id: D2
    description: "All four variants registered and constructible: --list-variants prints M-A..M-D; the dry run starts each variant (M-B/M-C/M-D refuse the self-test because they activate) and reports MENU DRY OK for every one"
    verification:
      - kind: other
        ref: "WindowHost.exe --list-variants | grep -c -x -E 'M-A|M-B|M-C|M-D' -> 4; Driver.exe menu --variant all --dry-run -> MENU DRY OK M-A, M-B, M-C, M-D, exit 0"
        status: pass
    human_judgment: false
  - id: D3
    description: "The eleven SC3/D-15 checks (open, close-desktop, close-window, close-bar, close-escape, keys-enter, mouse-select, launch-front, reopen, z-during, focus-after) give the right verdict for each variant on this machine; the native item rectangles (GetMenuItemRect from a worker thread) and the WPF popup placement under a real mouse right-click are correct; the optics of the menus"
    verification: []
    human_judgment: true
    rationale: "Needs real mouse/keyboard input and window activation, which are reserved for plan 01-15 after the hands-off checkpoint; real-key cross-check and the screenshots (private/q3-menu-*.bmp) are for Stephan's hand part (D-07, H4)"

duration: 55min
completed: 2026-10-12
plan_head_before: 5de59efe33785ddf29ee36115d1244f307ed7d3d
plan_head_after: 66a77e15b058e63a612125fd1d5bef2ebcfd0fee
commits: 2
status: complete
---

# Phase 1 Plan 11: Q3 menu variants and the `menu` case Summary

**Four menu variants at the non-activating bar (WPF ContextMenu plain and with temporary activation, native TrackPopupMenuEx with the bar or an invisible helper as owner) and a Driver `menu` case with all eleven SC3/D-15 checks, a German summary table and one screenshot per menu; only the input-free M-A self-test and start-only checks of the other three ran here.**

## Performance

- **Duration:** ~55 min including an interruption between the two tasks (start time not recorded at the beginning; estimated)
- **Completed:** 2026-10-12
- **Tasks:** 2 (1 tracer, 1 auto)
- **Files:** 4 created

## Accomplishments

- `Driver.exe menu --variant M-A --dry-run` prints `MENU DRY OK M-A` with exit 0: the host opened its WPF menu 500 ms after start at the plate centre, closed it from code after 400 ms (`reason=programmatic`), and the foreground window was unchanged. `--variant all --dry-run` also passes for M-B, M-C and M-D (host starts, `menu-ready` row, self-test refused because these variants activate a window, no `menu-open`).
- Check list in `MenuRun`: `open` (menu-open within 1 s and near the click, plus a BMP of the open menu), `close-desktop`, `close-window`, `close-bar`, `close-escape`, `keys-enter` (Down, Down, Return gives `menu-item id=2`), `mouse-select` (item 1), `launch-front` (item 3, launched pid must own the foreground within 2 s, then killed), `reopen` (opens #2 and #3 must stay open 500 ms), `z-during` (bar must not be lifted above a visible window during or after the menu, A5), `focus-after` (foreground is the witness again). Mandatory per D-15: the close checks, Escape, keys-enter, launch-front.
- Between checks the Driver closes a stuck menu (Escape, click on the witness, finally restarts the bar), re-pins a lifted bar to `HWND_BOTTOM` and brings the witness back to the front, so one failing variant does not colour the next check.
- Safety: real runs open a `SafetySession` (5 min), call `DesktopPrep.ClearTopArea`, register an Escape for a menu still open and a kill of launched witnesses as restores; nothing of this ran here.
- Item rectangle source is checked for plausibility (on screen, near the click) with fallbacks (#32768 window split in four, then an estimate) and the source is written into the check notes.

## Task Commits

1. **Task 1: WPF variants M-A/M-B, `menu --dry-run` tracer** - `f435ca2` (feat)
2. **Task 2: Native variants M-C/M-D, complete check list, screenshot, manual mode** - `66a77e1` (feat)

Tracer gate: `human_verify_mode` is `end-of-phase` and the tracer `<verify>` is automated-only, so the builds and `menu --variant M-A --dry-run` were re-run (passed) before expansion continued.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] WPF menu item rectangles from PointToScreen were wrong**
- **Found during:** Task 1 (dry run, host log)
- **Issue:** `PointToScreen` of the items right after open gave rectangles up and to the left of the popup (e.g. 793,304-951,326 for a popup at 960,400), so a mouse click on "item 1" would have missed.
- **Fix:** Rectangles are now the popup's Win32 window rectangle plus the item's offset inside the popup root, logged once at Loaded priority and once 250 ms after open (the later one is correct: 964,403-1122,425). A `menu-window` row records the popup rectangle. The Driver uses the newest `menu-items` row at least 150 ms after `menu-open`.
- **Files modified:** spikes/WindowHost/Variants/MenuWpf.cs
- **Commit:** f435ca2

**2. [Rule 2 - Missing critical] Safety net for the launched witness**
- **Issue:** Item 3 starts a process the Driver does not own; an aborted Driver would leave it on the developer's desktop.
- **Fix:** The launched witness gets `--exit-after-ms 60000`, the Driver registers a restore that kills launched pids (only processes named WindowHost) and Dispose does the same.
- **Files modified:** MenuWpf.cs, MenuCase.cs
- **Commit:** f435ca2, 66a77e1

### Additions (not deviations)

- Host options beyond the plan: `--selftest-menu-at x,y` (self-test position), `--menu-restore-focus true` (native focus handback, off by default). Driver: `--restore-focus true`.
- Shared helpers (`MenuCommon`, `MenuZWatcher`) live in MenuWpf.cs because both menu files need them; the plan's file list is otherwise unchanged. The q3 CsWin32 list has only two names because everything else (menu API, GDI capture, TPM/MF flags) already sat in the base, host and q2 lists.
- The BMP screenshot uses `BitBlt` + `GetDIBits` with a hand-written header, as planned; it is only exercised in a real run.

### Process note

**3. [Process] Protected-branch assertion not applied** - dispatch states this is a sequential executor on `main` (branching_strategy none), as in the earlier plans. `.planning/config.json`, `milestone.lock` and `state.json` were not touched.

---

**Total deviations:** 2 auto-fixed (1 Rule 1, 1 Rule 2), 1 process note. **Impact on plan:** none on scope; both fixes make the real run in 01-15 trustworthy.

## Issues Encountered

- The first Bash call to write MenuNative.cs failed on a heredoc quoting problem; the files were written with the Write tool instead. No effect on the result.
- A throw-away experiment screenshot of the developer's desktop (to check the menu placement) caught the developer's content by accident; it was deleted immediately and not repeated. The placement was then verified from host logs only.
- The dry runs briefly show the bar and, for M-A, a menu at the top of the screen (about 400 ms); no input was sent and the foreground stayed unchanged in every run.
- Git prints LF-to-CRLF warnings (autocrlf); harmless.

## Known Stubs

None. Item 4 "Schliessen" only logs `menu-item id=4` by design (the plan defines no further effect).

## Threat Flags

None. T-01-13 mitigated as planned: only the non-activating M-A self-test runs here; activating variants refuse `--selftest-menu`; real runs sit inside a SafetySession with an Escape/kill restore. T-01-14 accepted as planned (fixed `Environment.ProcessPath`, no user-supplied path). T-01-SC: no packages added.

## Next Phase Readiness

- 01-15 can run `Driver.exe menu --variant all --out spikes/results/q3-menu.csv --summary-md ...` after the hands-off checkpoint, and `menu --variant <v> --manual --seconds 60` for the hand part (real keys, D-07). Screenshots land in `spikes/results/private/q3-menu-<variant>.bmp`.
- Not yet exercised (needs real input or activation): every real-run check, `GetMenuItemRect` from the worker thread, WPF popup placement under a real right-click (programmatic placement was measured only), whether `SetForegroundWindow` on the NOACTIVATE bar succeeds (M-B/M-C), and the assumption A5 (bar lifted by activation).

## Self-Check: PASSED

- Files exist: MenuWpf.cs, MenuNative.cs, MenuCase.cs, native/q3/NativeMethods.txt (all present on disk).
- Commits `f435ca2` and `66a77e1` are ancestors of HEAD; `commits: 2` measured from the ledger (`5de59ef..HEAD`).
- Acceptance criteria re-run: MenuWpf.cs contains `HostVariant("M-A", "M-B")`, `ContextMenu`, `SetForegroundWindow`, `UseShellExecute = true`; dry-run host CSV has `menu-open` and `menu-close` rows; MenuNative.cs contains `CreatePopupMenu`, `TrackPopupMenuEx`, `TPM_RETURNCMD`, `WM_NULL`, `DestroyMenu`, `GetMenuItemRect`; MenuCase.cs has `Check(...)` entries for all eleven ids and two `OnRestore` registrations (Escape for an open menu, kill of launched witnesses); `comm -12` of the q3 list against the other lists prints nothing.
- Plan-level verification: both builds 0 warnings / 0 errors under `-warnaserror`; `--list-variants | grep -c` prints 4; `menu --variant M-A --dry-run` exit 0 with `MENU DRY OK M-A`; dry summary contains `| Variante |`.
