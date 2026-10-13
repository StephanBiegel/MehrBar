# Q4 pre-test: does PresentMon see the layered WPF window? (assumption A3)

Date: 2026-10-13, Windows 11 build 26200, PresentMon 2.6.0 (hash-verified by get-presentmon.ps1 right before use).

| Run | Window | PresentMon rows for WindowHost.exe |
|---|---|---|
| Pre-test, elevated, 10 s R1/20/hw, `--process_name WindowHost.exe` | transparent (`AllowsTransparency=true`, layered) | **0** (no CSV written at all) |
| Elevated matrix, 8 cells, same filter | transparent | **0** in every cell |
| Cross-check, no process filter, synthetic R1/20/hw, 14 s | transparent | 0 (only Code.exe appears, 306 rows) |
| Cross-check, no process filter, synthetic R1/20/hw, 14 s | **opaque** (`--transparent false`) | **370** (PresentMode "Composed: Copy with GPU GDI") |

Flags were checked against `PresentMon-2.6.0-x64.exe --help`: `--process_name`, `--output_file`, `--timed` and
`--terminate_after_timed` all exist. PresentMon writes no file when it sees no matching present.

**Result: A3 is confirmed. PresentMon does not see the layered (transparent) WPF window at all; it sees the same
window opaque.** The D-18 drop share of the transparent bar cannot be measured with PresentMon.

Side result (opaque control, synthetic cursor): PresentMon MsBetweenPresents p50 16.67 ms, p95 49.03 ms, 33 of 370
intervals between 25 and 250 ms (8.9 %); the host cadence of the same run shows 53 such gaps in 535 frames. Both
sources see the skipped frames, and they are spread evenly over the sweep period (not at the turning points).
