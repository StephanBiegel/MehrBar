---
phase: 02-grundger-st
plan: 02
subsystem: app-shell
tags: [tracer, drag-drop, shell-execute, file-picker]

requires:
  - phase: 02-grundger-st
    provides: "02-01 BarWindow, BarSurface, BarHwnd, BarPlacement"
provides:
  - "BarController (provisional, paths in memory): placement with growth from three slots, AppendPaths, RemoveAt, OnSlotClicked, OnContextRequested"
  - "DropTarget: FileDrop with link effect, else copy, never move; tolerant read"
  - "ShellLauncher.Launch(string path, Action removeEntry): stable signature, ShellExecute after AllowSetForegroundWindow"
  - "DialogOwner: hidden owner window, OpenFileDialog with DereferenceLinks=false and multi-select"
  - "BarSurface slots with placeholders, SlotAt; BarWindow events SlotClicked, ContextRequested, PathsDropped"
affects: [02-03, 02-06, 02-07, 02-09, 02-13]

actuals:
  tasks: 2
  commits: 1

key-files:
  created:
    - src/MehrBar.App/Bars/BarController.cs
    - src/MehrBar.App/Bars/DropTarget.cs
    - src/MehrBar.App/Bars/DialogOwner.cs
    - src/MehrBar.App/Shell/ShellLauncher.cs
  modified:
    - src/MehrBar.App/Bars/BarWindow.cs
    - src/MehrBar.App/Bars/BarSurface.cs
    - src/MehrBar.App/Hosting/Program.cs

key-decisions:
  - "BarWindow raises PathsDropped from its DropTarget; the controller owns the path list and the placement"
  - "Placeholder per slot: rounded rectangle #40FFFFFF, inset 2 px"

requirements-completed: []

coverage:
  - id: D1
    description: "Build without warnings, tests green, grep checks of both tasks"
    verification:
      - kind: other
        ref: "dotnet build -c Release -warnaserror 0 warnings; dotnet test 34/34; all grep checks OK"
        status: pass
    human_judgment: false
  - id: D2
    description: "Drop from Explorer, click start, file picker on the real desktop"
    verification: []
    human_judgment: true
    rationale: "Checkpoint 02-03 on the desktop; this session runs in session 0"

completed: 2026-10-20
status: complete
---

# Phase 2 Plan 02: Thin interaction path Summary

**The tracer bar now takes file drops from Explorer with the link effect (never move), grows by one 32 px slot per entry beyond three, starts an entry on click through ShellExecute and opens a file picker with a hidden owner on right click.**

## Task Commits

- this commit: `feat(02-02): drop, click to start and file picker on the tracer bar` (both tasks)

## Deviations from Plan

- Both tasks in one commit (same files, built and checked together).
- Start check only from session 0 (`--exit-after-ms 1500`, exit 0); drop, start and picker are checked in 02-03.

## Self-Check: PASSED
