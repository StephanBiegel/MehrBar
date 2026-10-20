---
phase: 02-grundger-st
plan: 06
subsystem: drop
tags: [drag-drop, classifier, drop-effect, file-system-seam, tdd]

requires:
  - phase: 02-grundger-st
    provides: "02-05 DockItem model; 02-02 DropTarget"
provides:
  - "MehrBar.Core.IO: IFileSystem, PhysicalFileSystem.Instance"
  - "MehrBar.Core.Drop: DropClassifier.Classify/DisplayNameFor, DropEffects, DropEffectChooser.Choose"
  - "FakeFileSystem test helper (DropClassifierTests.FakeFileSystem) for 02-09"
affects: [02-07, 02-09, 02-13]

actuals:
  tasks: 2
  commits: 1

key-files:
  created:
    - src/MehrBar.Core/IO/IFileSystem.cs
    - src/MehrBar.Core/Drop/DropClassifier.cs
    - src/MehrBar.Core/Drop/DropEffectChooser.cs
    - src/MehrBar.Tests/Drop/DropClassifierTests.cs
    - src/MehrBar.Tests/Drop/DropEffectChooserTests.cs
  modified: []

key-decisions:
  - "FakeFileSystem is a nested internal class of DropClassifierTests so 02-09 can reuse it"

requirements-completed: []

coverage:
  - id: D1
    description: "Classifier and effect chooser"
    verification:
      - kind: other
        ref: "drop.trx 10 x Passed (6 classifier + 4 chooser); Release build 0 warnings; suite 69/69"
        status: pass
    human_judgment: false

completed: 2026-10-20
status: complete
---

# Phase 2 Plan 06: Drop decisions in Core Summary

**Dropped paths become AppItem/FolderItem entries in order with duplicates kept, and the drop effect is decided in Core as link, else copy, else none — never move; 10 tests.**

## Task Commits

- this commit: `feat(02-06): drop classifier, drop effect chooser and file system seam`

## Deviations from Plan

- Both TDD tasks in one commit; DropTarget stays unchanged until 02-07, as planned.

## Self-Check: PASSED
