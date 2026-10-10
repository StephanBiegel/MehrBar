---
phase: 01-risiko-spikes
plan: 04
subsystem: testing
tags: [safety-net, sendinput, low-level-hooks, registerhotkey, cswin32, spike-harness]

requires:
  - phase: 01-risiko-spikes
    provides: "Plan 01-02 Driver skeleton (case registry, partial CaseContext, exit codes) and the CsWin32 base list; plan 01-03 host-side CsWin32 list (merge proven)"
provides:
  - "Safety.Begin / SafetySession: emergency abort hotkey, abort on non-injected input, per-case timeout, LIFO restore actions, key-ups only for keys this process pressed"
  - "Input.Key/Chord/MoveAbs/Click/RightClick/PressedKeys over IInputSink (SendInputSink, RecordingSink, Input.UseSink)"
  - "CaseContext.BeginSafety(TimeSpan, bool watchRealInput = true) for every later input-sending case"
  - "Driver case safety-selftest (optional --with-hooks)"
  - "Third CsWin32 topic list spikes/Common/native/input/NativeMethods.txt (merged with base and host lists, builds clean)"
affects: [01-07, 01-09, 01-10, 01-11, 01-12, 01-17]

actuals:
  tokens: 8200
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Real SendInputSink refuses to run without an active SafetySession (InvalidOperationException before any SendInput call)"
    - "Hooks and hotkey live on one dedicated message-loop thread; low-level hook callbacks are [UnmanagedCallersOnly] static methods reaching the session through a static field, no console I/O inside the callback"
    - "Fail closed: Begin throws when neither Ctrl+Alt+Q nor Ctrl+Alt+Shift+Q can be registered"
    - "Self-tests prove hotkey ownership with an independent witness: a second RegisterHotKey for the same combination fails during the session and succeeds after Dispose"

key-files:
  created:
    - spikes/Common/Safety.cs
    - spikes/Common/Input.cs
    - spikes/Common/native/input/NativeMethods.txt
    - spikes/Driver/CaseContext.Safety.cs
    - spikes/Driver/Cases/HarnessSelfTests.cs
  modified: []

key-decisions:
  - "Restore actions may send input: while Dispose runs, the session's Restoring flag lifts the abort check and Safety.Wait degrades to a plain sleep, so a restore like Win+Shift+M is not blocked by the abort that triggered it"
  - "Abort and restore are separate: AbortForTest/hotkey/real-input/timeout only cancel the token (first reason wins); restore runs in Dispose (using scope, Ctrl+C, ProcessExit), which is idempotent and serialised"
  - "Mouse buttons are tracked as VK 1/2 in PressedKeys so a click interrupted between down and up is released as a button-up, not a key-up"
  - "Hook install uses a null module handle (GetModuleHandle(NULL)); verified working on this machine"

patterns-established:
  - "Every input-sending case starts with: using SafetySession safety = ctx.BeginSafety(timeout);"
  - "Input safety proof command: dotnet build spikes/Driver -c Release -warnaserror && Driver.exe safety-selftest"

requirements-completed: []

coverage:
  - id: D1
    description: "Safety net proven without real input: restore order 3-2-1, exactly one key-up for the fake modifier, no blind key-ups, further Input calls throw after abort, Dispose idempotent, real SendInputSink refuses to run without a session"
    verification:
      - kind: other
        ref: "dotnet build spikes/Driver -c Release -warnaserror && dotnet build spikes/WindowHost -c Release -warnaserror && Driver.exe safety-selftest (SAFETY OK, exit 0)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Abort hotkey registers (Ctrl+Alt+Q), is held by the session and released after Dispose, watcher thread exits; low-level keyboard/mouse hooks install and are removed (--with-hooks)"
    verification:
      - kind: other
        ref: "Driver.exe safety-selftest; Driver.exe safety-selftest --with-hooks (both SAFETY OK)"
        status: pass
    human_judgment: false
  - id: D3
    description: "The hotkey and the real-input watcher actually abort a running input sequence when Stephan presses keys or moves the mouse; timeout and Ctrl+C paths under a real run"
    verification: []
    human_judgment: true
    rationale: "Needs genuine physical input; by design no real input was injected or produced in this plan. Exercised by Stephan in hand part H1 (plan 01-17)."

duration: 10min
completed: 2026-10-10
plan_head_before: ba7ae949470642bf3637a64387bae33641c1314a
plan_head_after: 09c4bf69c67ecae76512c949b6b72d99ced747fe
commits: 2
status: complete
---

# Phase 1 Plan 04: Safety session and input helpers Summary

**Safety net for every input-sending Driver run: a SafetySession with Ctrl+Alt+Q abort hotkey, real-input watcher via low-level hooks, per-case timeout and LIFO restore, plus Input helpers (SendInput chords, absolute virtual-desktop mouse moves, clicks) that refuse to run without a session and check the abort token before every call, proven by a no-input `safety-selftest`.**

## Performance

- **Duration:** 10 min
- **Completed:** 2026-10-10
- **Tasks:** 2
- **Files:** 5 created

## Accomplishments

- `Driver.exe safety-selftest` prints `SAFETY OK` (exit 0) with 22 checks; both `spikes/Driver` and `spikes/WindowHost` build under `-warnaserror` with the shared Common sources and the third CsWin32 list merged (closes the open question from 01-02/01-03: three topic lists coexist without duplicates).
- Restore path: three restore actions run 3-2-1; a fake modifier pressed through `Input.Key` produces exactly one key-up on Dispose; a chord sends downs in order and ups reversed; after `AbortForTest` every `Input.Key/MoveAbs/Click` throws `OperationCanceledException` and nothing reaches the sink.
- Guard rail: the real `SendInputSink` throws `InvalidOperationException` when no session is active, so a forgotten `BeginSafety` cannot inject input.
- Watcher: hotkey registers on a dedicated message-loop thread, is provably held (second registration fails) and released (succeeds after Dispose), the thread exits. `--with-hooks` installs and removes the real `WH_KEYBOARD_LL`/`WH_MOUSE_LL` hooks (null module handle works on build 26200) without injecting anything.
- Absolute mouse moves use `MOUSEEVENTF_MOVE|ABSOLUTE|VIRTUALDESK` with `(px - origin) * 65535 / (extent - 1)` over `SM_*VIRTUALSCREEN`, clamped; the arithmetic is implemented but not exercised until a case moves the cursor.

## Task Commits

1. **Task 1: End-to-end safety path (tracer)** - `2ceddab` (feat)
2. **Task 2: Abort hotkey and real-input watcher** - `09c4bf6` (feat)

Tracer gate: `human_verify_mode` is `end-of-phase` and the tracer `<verify>` is automated-only, so the build and `safety-selftest` were re-run before the commit; it passed and expansion continued.

## Deviations from Plan

### Auto-fixed Issues

None in the sense of defects; two small additions beyond the letter of the plan, both Rule 2 (safety correctness, T-01-03):

**1. [Rule 2 - Missing critical] Real sink refuses to run without a session**
- **Issue:** The plan only requires that Input checks the session token; with no session, nothing stopped a case from injecting unguarded input.
- **Fix:** `SendInputSink` throws before any SendInput call when `Safety.Current` is null. Covered by a self-test check.
- **Files:** `spikes/Common/Input.cs`, `spikes/Driver/Cases/HarnessSelfTests.cs`
- **Commit:** `2ceddab`

**2. [Rule 2 - Missing critical] Fail closed without a hotkey; restore may send input**
- **Issue:** (a) A session with no emergency hotkey (both combinations taken, e.g. by other parallel sessions) would run unguarded. (b) Restore actions that send input (Win+Shift+M, second Win+D) would be blocked by the very abort that triggers them.
- **Fix:** `Safety.Begin` throws when neither combination registers; `Restoring` lifts the abort check during Dispose and `Safety.Wait` becomes a plain sleep then.
- **Files:** `spikes/Common/Safety.cs`
- **Commit:** `09c4bf6`

### Additions (not deviations)

- `Safety.Begin` has an optional fourth parameter `Action<string>? logger` (default stderr) so the session can log its own startup line; `CaseContext.BeginSafety` passes `Console.WriteLine`.
- `safety-selftest --with-hooks` (opt-in): installs the real low-level hooks for a moment. Not in the default run because the plan wants the self-test with `watchRealInput: false`.

### Process note

**3. [Process] Protected-branch assertion not applied** - dispatch states this is a sequential executor on `main` (branching_strategy none), as in 01-01..01-03. `.planning/config.json` not touched.

---

**Total deviations:** 2 auto-added (Rule 2), 1 process note.
**Impact on plan:** None on scope; both additions tighten the safety net and are covered by self-test checks.

## Issues Encountered

- Hotkey `Ctrl+Alt+Q` was free on this machine (no other session held it); the fallback combination is implemented but not exercised.
- Git prints LF-to-CRLF warnings (autocrlf); harmless.
- `with-hooks` briefly sat in the live input path (milliseconds); no input was sent or consumed.

## Known Stubs

None.

## Threat Flags

None. T-01-03 mitigated as planned (hotkey, real-input abort, timeout, LIFO restore on Dispose/ProcessExit/Ctrl+C, key-ups only for tracked keys), proven by `safety-selftest` without real input; the hotkey and hooks reacting to real physical input remain for hand part H1 (D3 above). T-01-SC: no packages added.

## Next Phase Readiness

- Input-sending cases (01-07 onward) open with `using SafetySession safety = ctx.BeginSafety(timeout);` and call `Input.*`; they register undo steps with `safety.OnRestore(...)`. The timeout, Ctrl+C and ProcessExit paths are implemented but only exercised through `AbortForTest`/Dispose in the self-test.
- Not yet exercised: real physical abort (hotkey press, mouse movement), the 20 ms chord gaps against a live desktop, UIPI refusal against an elevated foreground window (surfaces as `IOException` from the sink).

## Self-Check: PASSED

- Files exist: all 5 listed under key-files.created.
- Commits `2ceddab` and `09c4bf6` are ancestors of HEAD; `commits: 2` measured from the ledger.
- Acceptance criteria re-run: Input.cs contains `MOUSEEVENTF_VIRTUALDESK`, `PressedKeys`, `RecordingSink`; Safety.cs contains `OnRestore`, `AbortForTest`, `RegisterHotKey`, `WH_KEYBOARD_LL`, `WH_MOUSE_LL`, `LLMHF_INJECTED`, `PostThreadMessage` and no `RunLog`; HarnessSelfTests.cs uses `RecordingSink` and `watchRealInput: false`; `comm -12` of the base/host lists against the input list prints nothing.
- Plan-level verification: both builds 0 warnings / 0 errors, `safety-selftest` and `safety-selftest --with-hooks` end in `SAFETY OK`.

---
*Phase: 01-risiko-spikes*
*Completed: 2026-10-10*
