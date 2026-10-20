---
phase: 02-grundger-st
plan: 05
subsystem: config
tags: [config, json, source-generator, polymorphism, defaults, normalizer, tdd]

requires:
  - phase: 02-grundger-st
    provides: "02-01 BarEdge, BarAlign"
provides:
  - "MehrBar.Core.Model: DockConfig, GeneralConfig, BarConfig, LabelConfig, BarLayer; DockItem with AppItem, FolderItem, UrlItem, SeparatorItem, UnknownItem"
  - "DockItemConverter (fixed type list, unknown and broken items kept raw), TolerantEnumConverter<T>"
  - "ConfigSerializer.Serialize/Deserialize with warnings (source-generated ConfigJsonContext)"
  - "Defaults (single source, spacing 0, autostart off) and ConfigNormalizer (clamps and repairs with config-clamped/config-repaired warnings)"
affects: [02-06, 02-07, 02-08, 02-09]

actuals:
  tasks: 2
  commits: 1

key-files:
  created:
    - src/MehrBar.Core/Model/DockConfig.cs
    - src/MehrBar.Core/Model/DockItem.cs
    - src/MehrBar.Core/Config/DockItemConverter.cs
    - src/MehrBar.Core/Config/TolerantEnumConverter.cs
    - src/MehrBar.Core/Config/ConfigSerializer.cs
    - src/MehrBar.Core/Config/Defaults.cs
    - src/MehrBar.Core/Config/ConfigNormalizer.cs
    - src/MehrBar.Tests/Fixtures/ConfigFixtures.cs
    - src/MehrBar.Tests/Config/ConfigSerializerTests.cs
    - src/MehrBar.Tests/Config/DefaultsTests.cs
    - src/MehrBar.Tests/Config/ConfigNormalizerTests.cs
  modified: []

key-decisions:
  - "Enums are written camelCase by TolerantEnumConverter (no JsonStringEnumMemberName attributes on the Layout enums)"
  - "ZoomWidth and label font size are doubles, sizes and durations ints"
  - "A non-object entry in items throws (root-level error for the load policy of 02-07); a known type with a wrong value kind becomes UnknownItem with one warning"

requirements-completed: []

coverage:
  - id: D1
    description: "Serializer round trip without data loss, tolerant enums, raw unknown items"
    verification:
      - kind: other
        ref: "ConfigSerializerTests 11/11 (config.trx)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Defaults from one place, normalisation with warnings"
    verification:
      - kind: other
        ref: "DefaultsTests 3/3, ConfigNormalizerTests 5/5; no JsonPolymorphic, no init-only Extra; Release build 0 warnings, suite 53/53"
        status: pass
    human_judgment: false

completed: 2026-10-20
status: complete
---

# Phase 2 Plan 05: Configuration format Summary

**The config.json model with a forward-compatible item converter, tolerant enums, one source of defaults and a normaliser is in Core and covered by 19 tests: future types and unknown fields round-trip unchanged, a typo in an enum or one broken item no longer costs the whole file.**

## Task Commits

- this commit: `feat(02-05): configuration model, serializer, defaults and normaliser` (both TDD tasks)

## Deviations from Plan

- RED and GREEN were written in one go and committed together; all tests passed on the first run.
- Both tasks in one commit.

## Self-Check: PASSED
