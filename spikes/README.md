# MehrBar spikes

Throw-away risk spikes for phase 1 ("Risiko-Spikes"). They measure the architecture-defining
unknowns on this machine (Windows 11, build 26200) and end in one go/no-go note each, before the
product skeleton is built. The spikes live **outside** `MehrBar.sln`, are committed so the numbers
can be re-measured, and are deleted or frozen after phase 2 (D-01).

## Architecture exception: Win32 in the spikes

The product rule "Win32 calls only in `src/MehrBar.App/Interop`" applies to the **product**. The
spikes under `spikes/` are an explicit exception to it: `WindowHost`, `Driver` and `Icons` call
Win32 directly through CsWin32 (source-generated P/Invoke), because they exist to probe those
APIs. **No spike code is moved into `src/` unreviewed.** Phase 2 re-implements the chosen
techniques in `App/Interop`, with tests in `MehrBar.Core` wherever the logic is free of Win32.

## Projects

| Folder | Purpose |
|--------|---------|
| `Common/` | Shared sources (`RepoPaths`, `Stats`) linked into the projects, and the CsWin32 lists. One list per topic under `Common/native/<topic>/NativeMethods.txt`; a name appears in exactly one list. `Common/NativeMethods.json` holds the generator options. |
| `WindowHost/` | The bar under test (WPF, `net10.0-windows`). Variants are added by later plans. `--exit-after-ms N` shuts it down after N ms. |
| `Driver/` | Input source and independent oracle (console). `Driver --list` prints the cases, `Driver <case> [--key value \| --flag]...` runs one. Exit codes: 0 ok, 1 criteria failed, 2 usage error, 3 aborted. |
| `Icons/` | Icon extraction spike. |
| `ZoomCheck/` | Zoom reference check against the original. |
| `tools/` | Scripts (`check-notes.ps1` gates the go/no-go notes) and the downloaded PresentMon. |

`spikes/Directory.Packages.props` pins the packages for everything under `spikes/` (CsWin32
0.3.360). It does not import the root `Directory.Packages.props`, so deleting `spikes/` leaves the
product untouched. Build with `dotnet build spikes/<project> -c Release -warnaserror`.

## Folders

| Path | Content | In git |
|------|---------|--------|
| `results/` | Small, redacted measurement files (no foreign window titles, no paths) | yes |
| `results/private/` | Screenshots, contact sheets, paths, window titles | ignored |
| `results/dry/` | Output of dry runs | ignored |
| `fixtures/` | Generated test fixtures | ignored |
| `tools/presentmon/` | Downloaded PresentMon, hash-checked | ignored |

Foreign skins and screenshots never enter the repository.

## Safety rules

Spike runs send real input to the interactive desktop (D-05).

- No input-sending run without Stephan's confirmation that his hands are off keyboard and mouse.
- Emergency abort: **Ctrl+Alt+Q** (fallback **Ctrl+Alt+Shift+Q**).
- Any real input during a run aborts it (exit code 3).
- Restore actions (display mode, window state, cursor) run in `finally`.
- Real-key cross-checks happen once, in the hand part at the end of the phase (D-06, D-07).

## Nachmessen

How to re-measure a spike: see the `## Aufbau` section of the matching `01-SPIKE-*.md` note in
`.planning/phases/01-risiko-spikes/`. Plan 01-18 consolidates the exact commands here; no other
plan edits this README.
