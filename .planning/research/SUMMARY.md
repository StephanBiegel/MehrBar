# Project Research Summary

**Project:** MehrBar (RocketDock-style launcher dock, C# / .NET 10 / WPF, self-contained single-file x64)
**Domain:** Windows launcher dock with magnifying zoom, bottom z-layer, RocketDock skin support
**Researched:** 2026-10-09
**Confidence:** MEDIUM overall. Stack and Core/App architecture are HIGH (probe-verified on this machine). Win+D z-order, layered-window present cost, and the zoom anchor are unproven and need spikes before the roadmap commits.

## Executive Summary

MehrBar is a personal launcher: one horizontal bar at the top of the screen (a right-edge bar comes in V2) whose icons grow under the cursor with a cosine falloff, show a label, accept Explorer drag & drop, and sit at the bottom of the window stack. The Core Value is that the zoom feels like RocketDock 1.3.5 with CrystalXP.net; everything else is parity. Docks of this family use a borderless per-pixel-alpha window larger than the visible bar, one custom-painted surface, one pure layout function fed by the pointer, and a thin shell layer. The concept's Core/App split already matches this.

Recommended approach: spike first. Four facts decide the architecture and are expensive to learn late: (1) whether a HWND_BOTTOM tool window survives Win+D on build 26200, (2) whether a ~1900 px layered WPF window holds 60 fps and its alpha-1 hit plate catches the pointer, (3) whether a context menu from a WS_EX_NOACTIVATE window dismisses correctly, (4) which zoom anchor matches the developer's own measurements. The concept puts Win+D robustness in M6 and the hit plate in M2; both should move into M1.

Main risks: the bar vanishing on Win+D or Explorer restart; hit-testing and flicker over transparent gaps; the zoom anchor (center is contradicted by the project's own measurement table, which fits a cursor-anchored push model); and a product gap where a bar behind every window is hard to fill by drag & drop.

## Key Findings

### Recommended Stack
- Microsoft.Windows.CsWin32 0.3.360 (pinned; allowMarshaling false; App/Interop only). Source-generated Win32/COM. Probe-verified.
- H.NotifyIcon.Wpf 2.4.1 behind a 3-method ITrayIcon seam: +0.6 MB, versus +33 MB for WinForms NotifyIcon. Fallback: hand-rolled Shell_NotifyIcon (~150 lines). Do not adopt 2.5.0 betas.
- System.Text.Json (in-box) with source generator and a custom JsonConverter<DockItem> plus UnknownItem. [JsonPolymorphic] alone throws on unknown or out-of-order type and drops it on save. Probe-verified.
- IShellItemImageFactory.GetImage at 256 px with SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK. Read DIB bits via GetObject/GetDIBits, convert to Pbgra32 once, Freeze(). Shell alpha is straight; drawing it directly gives dark fringes.
- Hand-written, no package: RocketDock INI parser (~40 lines, tolerant of mixed space/tab around =, BOM, Latin-1 fallback); .lnk via IShellLinkW + Load only (never Resolve); Local\ named mutex; HKCU Run key; File.Replace for atomic writes; ~80-line rotating logger.
- xunit.v3.mtp-v2 4.0.1 on net10.0 for Core tests, runner set in global.json. Fallback to VSTest (xunit.v3.mtp-off + xunit.runner.visualstudio + Microsoft.NET.Test.Sdk) if VS Code Test Explorer does not discover tests. Use xUnit Assert; avoid FluentAssertions 8.x and Moq.
- Publish: IncludeNativeLibrariesForSelfExtract=true (without it five WPF native DLLs stay loose). No compression (first run 5.4 s), no trimming or NativeAOT (NETSDK1168), no WinForms, no Assembly.Location. PublishReadyToRun decided by measurement in M6. Manifest PerMonitorV2, asInvoker. Publish settings live in the csproj: dotnet publish src/MehrBar.App -c Release.

### Expected Features
Table stakes (already in PROJECT.md Active): cosine zoom (r = 2.5 for ZoomWidth 4; probe-checked: 90 px at d=1, 40 px at d=2, ~222 px total growth vs 226 measured); zoom ramp (200 ms, reverses from current factor); hover label (Tahoma 13, white, outline 55 %, shadow 25 %, clamped to monitor); single click on mouse-up (no drag); launch via shell with runas and 1223 silenced; Explorer drop as Link only; separators (no zoom/label/click, fixed width, deletable); item menu (Start, Run as admin, Open file path, Properties, Remove); bar menu (Insert separator, Open config, Quit); always-on-bottom with no focus theft and Win+D survival; click-through; live settings with autosave, reset, start with Windows; tray escape hatch; single instance; atomic config with backup and unknown fields preserved.

Gaps to add to V1: double-launch debounce (700-1000 ms) and press feedback; non-modal launch-error feedback (label message ~3 s, logged, no MessageBox); default names from .lnk base name or FileDescription/ProductName; fallback icon for missing targets; default icon for URL items (no favicon fetch); context menus that dismiss on a non-activating window; second instance opens settings; label clamping included in window size.

Defer: hotkey reveal (Ctrl+Alt+R in RocketDock; likely the first request); launch hop effect; lock items; label outline/shadow colours; right-edge bar; multiple bars; groups; autohide; other layers; multi-monitor; import; other skins; localization; installer.

Out (do not re-add): running-app indicators, previews, minimize-to-dock, docklets, drag-off-to-delete, drop-onto-folder copy, favicon fetch, reflections/3D, per-item run state, modal error boxes, per-bar percent scaling.

### Architecture Approach
Core (net10.0, no WPF/Win32; own Span/RectD/SizeD) owns model, config store, layout, zoom, skin parsing, drop classification and launch decisions behind seams. App (net10.0-windows) owns the HWND, drawing, pointer, shell, tray, autostart and settings. Tests reference Core only. Win32 is confined to App/Interop.

Major components:
1. AxisFrame (Core): maps axis space (u along bar, v depth from edge) to window space per BarEdge. Only place that knows horizontal vs vertical. Axis-neutrality test across four edges in M2.
2. RestLayout / ZoomEngine / ZoomAnimator / FrameLayout (Core): pure function of rest layout, params, pointer u, progress. Distances use rest centres (no feedback). Hit-tests run against the displayed frame.
3. WindowPlan / BarPlacement / NineSlice / HitTest (Core): window = max zoomed extent + label overhang + skin outside margins, clamped to monitor. Recomputed on config/monitor/DPI/item change only, never per frame.
4. ConfigStore + BarConfigDiff + serializer (Core): immutable snapshot, Update as sole writer, Changed(old,new), change flags drive minimal rebuilds. Debounced 400-500 ms persist with File.Replace and .bak. On parse failure move aside to config.corrupt-<ts>.json; never overwrite silently.
5. RocketDockSkinLoader + BarMetrics + DefaultSkin (Core): tolerant parse, never throws, falls back to flat translucent rounded rect.
6. BarController / BarWindow / BarSurface / FrameClock (App): 3-4 DrawingVisual layers (HitPlate, Background, Icons, Label); clock subscribes CompositionTarget.Rendering only while active; pointer from polled GetCursorPos.
7. ZOrderKeeper / DesktopState (App/Interop): baseline HWND_BOTTOM with TOOLWINDOW|NOACTIVATE and MA_NOACTIVATE; hardening via WinEvent foreground hook plus 250 ms poll (Rainmeter approach); desktop host found by SHELLDLL_DefView owner (WorkerW pre-24H2, Progman 24H2+); never cached across Explorer restarts. Behind IZOrderPolicy.
8. IconCache (App): background 256 px load, mip pyramid (256/128/64/48/32 physical px), IconLevel.Choose in Core, frozen, placeholder until ready.
9. Drop pipeline: DropTarget reads FileDrop, UniformResourceLocatorW, URL text, IDList; DropClassifier in Core; M1 append, M4 insert via virtual gap. Reorder by capture, not DoDragDrop.
10. TrayIcon / SingleInstance / AppLifetime: custom Main takes the mutex before WPF; OnExplicitShutdown; single exit path; tray re-add on TaskbarCreated.

### Critical Pitfalls
1. Win+D (HIGH, spike S1). Show Desktop raises the desktop; a bottom-z bar disappears and gets no WM_WINDOWPOSCHANGING. Avoid: ZOrderKeeper with runtime host lookup, foreground hook plus poll, stuck-topmost safety net, re-run on TaskbarCreated and WM_DISPLAYCHANGE. Test: Win+D, taskbar corner, Win+M, Peek, Alt+Tab, Explorer kill/restart, lock/unlock, display change. Last resorts (Explorer WM_USER+83 hook, parenting into desktop host) are not recommended.
2. Hit-testing by alpha (HIGH, S2). Alpha-0 pixels pass clicks to the desktop, so zoom collapses over gaps and flickers. Avoid: alpha-1 hit plate over the displayed bar; GetCursorPos leave test in the tick; hysteresis of a few px or 30-60 ms; never toggle WS_EX_TRANSPARENT or regions per frame. Alpha 0x01 is a tunable constant (fallback 0x04-0x08).
3. Present cost (HIGH, unmeasured, S2). AllowsTransparency copies surface to system memory per update; no .NET 9/10 measurement exists. Avoid: small fixed window, no effects, frozen resources, measured go/no-go (<=12-16 ms frame, <=10-15 % CPU at 64 icons). Fallbacks: per-interval sized window; glass/WindowChrome (loses alpha hit-test); Direct2D/DirectComposition window. Renderer behind IBarSurface.
4. Zoom anchor (HIGH arithmetic, MEDIUM interpretation). Left/right growth differs across states in the reference table (centre state: -112/+114; far right: -112/+35). Fixed-centre needs equal sides; cursor push model reproduces all four rows, including centre shifts +16 and -39. The -3 left edge in three shots may be clipping, so re-shoot away from the border. Avoid: Core test on the four rows (+-6 px) before M2; zoomAnchor parameter with both center and cursor.
5. Non-activating menus and launch focus (MEDIUM, S5). WPF ContextMenu on a no-activate owner may not dismiss or take keys; native TrackPopupMenu needs SetForegroundWindow first. Launched apps may open behind. Avoid: decide menu tech in M1; if a hidden helper owner is used, save and restore foreground; dialogs not owned by the bar.
6. Filling the bar (product tension). Filling needs an Explorer window that covers the bar. See Decision 2.
7. Icon pipeline (MEDIUM, S6). Default flags give thumbnails or 44 px; CreateBitmapSourceFromHBitmap loses alpha; .lnk overlay unverified; Store-app .lnk has no filesystem target; shell calls need STA; dead network paths stall. Avoid: STA worker, placeholders, per-item timeout, DIB read with alpha check, disk cache by M6.

Also: named handler for CompositionTarget.Rendering, time-based with duplicate-timestamp skip; debounce config saves; physical-pixel Interop with DPI passed into Core; re-add tray and re-resolve desktop host on Explorer restart; autostart CWD is System32, so use Environment.ProcessPath.

## Implications for Roadmap

### Phase 0: Spikes (time-boxed, before detailed M1 planning)
Rationale: four architecture-shaping facts, cheap now, costly in M6.
Delivers: window host spike (alpha-1 plate catching clicks and MouseMove over ~1900x250 px; alpha-0 pass-through verified 1 px outside; HWND_BOTTOM + TOOLWINDOW|NOACTIVATE first show without flash; Win+D and taskbar-corner survival on build 26200 with and without watcher; WPF ContextMenu vs TrackPopupMenu dismissal and keys; Notepad focus and launch foreground; present cost at 60 fps, hardware vs SoftwareOnly). Anchor math as Core test (no UI). Icon spike (48 px-only exe, 256 px exe, .lnk overlay, Store .lnk, folder, .url, PDF, network path; size, alpha, cold/warm latency). Tooling: CsWin32 build; VS Code MTP discovery (switch to VSTest if it fails). Each ends with a go/no-go note.
Avoids: Pitfalls 1, 2, 3, 4, 5, 7.

### Phase 1: Walking skeleton with layer, Core contract, launching [M1, expanded]
Rationale: highest-risk host and z-order work first; a bar that disappears on Win+D is invisible to the developer. Core layout contract with progress fixed at 0 lets M2 be additive.
Delivers: solution skeleton (global.json, Directory.Packages.props, NativeMethods.txt, manifest, publish settings); Core model with polymorphic items, ConfigStore, debounced atomic persistence, Normalize(), Q2 defaults, round-trip tests; Core AxisFrame, BarMetrics (skinless), RestLayout, WindowPlan (max-growth, label overhang), FrameLayout, HitTest, IconLevel; App Main with single instance, log, lifetime, tray with TaskbarCreated re-add; BarWindow with ZOrderKeeper baseline (plus watcher if spike proves it); alpha-1 plate; BarSurface static render; IconCache with STA worker, placeholders, mip pyramid; ShellLauncher; append-only drop with FileDrop; Remove via item menu (tech from spike); temporary-front toggle; first publish and time-to-first-frame table; Skins\ loose beside exe.
Avoids: 1, 5, 6, 7, 8, 10, 11, 12, 14, 15.

### Phase 2: Zoom engine, animation loop, labels [M2]
Rationale: builds on the layout contract; anchor settled in Phase 0. Core Value lives here.
Delivers: ZoomKernel.RadiusFor(zoomWidth); ZoomEngine (ComputeInto allocation-free plus pure overload); ZoomAnimator with injectable time; ZoomAnchor with Bias and optional ClampToMonitor; FrameClock on-demand; labels with cached geometry and clamping; hit-test on displayed geometry with mouse-down latch; debounce and press feedback; axis-neutrality test across four edges; reference numbers as fixtures.
Avoids: 2, 4, 9, 19, 20. Research flag: MEDIUM (re-shot references; ZoomWidth 2/6/8 series to separate (zoomWidth+1)/2 from 0.625*zoomWidth).

### Phase 3: Skin [M3]
Delivers: docs/skin-format.md from real bg.png and separator.ini (gate before code); tolerant INI reader; RocketDockSkinLoader; SkinService with atomic reload; 9-slice with integer-snapped slices; separators (fallback line if no separator.ini); default-skin fallback; synthetic test skin in repo (never samples/skins).
Avoids: 8, 12, 13. Research flag: MEDIUM (Outside-*Margin and separator stretch unverified; hypothesis: bar rect expands by outside margins for painting, icons stay in inner rect).

### Phase 4: Insertion, reorder, URL drops, full menus [M4]
Delivers: InsertIndex and virtual gap in RestLayout; insertion marker; capture-based reorder with threshold; URL drops creating url items; DragOver-driven zoom; full menus; properties dialog with custom PNG/ICO. Format parsing in Core as ParseDropData.
Avoids: 6 (format matrix), 20. Standard patterns except the browser URL matrix.

### Phase 5: Settings and autostart [M5]
Delivers: lazy MVVM settings window over ConfigStore with echo suppression; reset to defaults; "Mit Windows starten" with self-healing HKCU Run; show StartupApproved\Run state but never write it; SessionEnding flush; second-instance opens settings.
Avoids: 10, 15. Standard patterns.

### Phase 6: Hardening, 100+ icons, release [M6]
Delivers: desktop-host re-resolve on Explorer restart and display change (debounced ~300 ms); stuck-topmost guard; 60/100+ icon PresentMon runs; disk icon cache with timeout; log rotation check; R2R decision by measurement; InvariantGlobalization only if startup misses budget; reboot autostart test; 125/150 % DPI and live DPI change; release publish check with Skins\ beside exe.
Avoids: 3, 9, 11, 12, 14, 17, 18.

### Phase Ordering Rationale
- Spikes first: Win+D, hit-testing, present cost, menus and anchor each change M1 content.
- Core model, config and layout contract before zoom, so M2 is additive.
- Zoom before skin: a fallback skin is enough to judge Core Value.
- Drop-insert after zoom: drag zoom reuses the engine's DragOver input.
- Settings late: a view over ConfigStore.

### Research Flags
- Needs spike or deeper research: Phase 0 (all); Phase 1 window host and ZOrderKeeper (HIGH); Phase 2 calibration (MEDIUM); Phase 3 skin semantics (MEDIUM); Phase 6 publish and perf (MEDIUM).
- Standard patterns: Phase 5; Phase 4 insertion and reorder (URL format matrix is a test pass, not research).

### Decisions Needed From Developer Before Roadmap Creation
1. Re-open Q12 (zoom anchor). The table fits cursor-anchored push with zero residual; fixed centre does not. Recommendation: re-open Q12; keep zoomAnchor as a parameter with both implementations; let the Core test on the table pick the default. center stays selectable.
2. Filling the bar while it sits behind every window. Options: temporary-front toggle (topmost until drop or N seconds); "Eintrag hinzufuegen..." file picker; "Pfad einfuegen" clipboard. Recommendation: temporary-front toggle in M1 (needed to test D&D at all) plus file picker.
3. Overflow when 60+ icons exceed monitor width (36 px pitch gives 2160 px > 1920). Options: shrink pitch with floor; scroll; cap length. Changes RestLayout and WindowPlan, so decide before M2. Recommendation: shrink pitch with a floor and a one-line settings notice when hit.
4. Drop onto an existing app icon. RocketDock opens that app with the file; the concept's insert rule conflicts. Recommendation: insert beside nearest boundary in V1; revisit if missed.

### Concept Changes Recommended
- Move Win+D, Show Desktop, Explorer restart and desktop-host work from M6 into M1; M6 only re-verifies.
- Move the hit plate from M2 into M1 (without it the window receives no clicks or drops).
- Add a Phase 0 spike set with go/no-go notes.
- First publish and startup measurement in M1.
- ConfigStore in M1, not M5.
- Settle menu technology in the M1 spike.
- Add the temporary-front toggle and the fill decision to M1.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | NuGet API versions (2026-10-09); core choices probe-verified. Open: VS Code MTP discovery, tray glyph, R2R benefit, 125/150 % scaling. |
| Features | MEDIUM | HIGH for developer's RocketDock registry and PROJECT.md data; MEDIUM for option semantics; LOW for competitor details and timings. |
| Architecture | MEDIUM-HIGH | Core/App split HIGH; Win+D mechanism MEDIUM-LOW until spiked; zoom anchor LOW until re-shot. |
| Pitfalls | MEDIUM | HIGH for mechanisms with primary sources (Dwayne Need, Raymond Chen, Microsoft Q&A, Rainmeter source, project measurement table); LOW for .NET 10 present cost, HBITMAP alpha, GetImage overlay, WPF menus on no-activate windows, foreground lock. |

Overall: MEDIUM. The stack is settled. Four spike outcomes (Win+D, hit-plate reliability, present cost, menu dismissal) and the anchor could each change the plan. None requires abandoning the concept, but M1 scope should not be committed before Phase 0 reports.

### Gaps to Address
- Win+D with HWND_BOTTOM on build 26200: undocumented; settle in window-host spike.
- Present cost on .NET 10: no measurement; spike with PresentMon, hardware vs SoftwareOnly.
- Hit-plate alpha: 0x01 is a secondary-source hypothesis; tune on target machine.
- Zoom anchor and radius: re-shoot references away from border; Zoom Width 2/6/8 series. See Decision 1.
- Skin Outside-*Margin and separators: write docs/skin-format.md before Phase 3; one skin sample only.
- Bar longer than monitor: policy undecided (Decision 3); blocks Phase 2 sizing.
- GetImage on .lnk overlay and AppsFolder parsing names: settle in S6.
- STJ polymorphism edge cases: add round-trip tests for unknown discriminator and out-of-order in M1.
- VS Code MTP discovery: switch to VSTest fallback if needed; CLI results identical.
- File.Replace under AV/OneDrive locks: retry on IOException.
- Launch-error form: label overlay plus log recommended; tray balloon optional.
- Virtual desktops: ask whether the developer uses them (Pitfall 17).

## Sources

Primary (HIGH): NuGet API (2026-10-09); local probe builds on this machine (icon factory, .lnk, manifest, five publish variants, H.NotifyIcon single-file, STJ polymorphism, xUnit MTP and VSTest); developer's HKCU\Software\RocketDock registry; .planning/PROJECT.md; docs/PROJEKT_START.md; Dwayne Need, "Transparent Windows in WPF" (learn.microsoft.com/en-us/archive/blogs/dwayneneed/transparent-windows-in-wpf); Raymond Chen Show Desktop posts (devblogs.microsoft.com/oldnewthing/20110617-00/?p=10403 and 20241021-00/?p=110393); Microsoft Learn single-file overview, DPI awareness, Run keys; xunit.net v3 getting started and 4.0.0 notes.

Secondary (MEDIUM): Rainmeter Library/System.cpp; Microsoft Q&A thread 1887373 (widget on Win+D); Qt interest list 2016-12 (insert above desktop); UIPI and drag & drop articles; CompositionTarget.Rendering docs; H.NotifyIcon repo (last push 2026-10-05); WPF team layered SW vs HW (2006); IShellItemImageFactory::GetImage docs.

Tertiary (LOW, caveats only): RocketDock 1.3.5 vendor help (mirror) and 1.3.0 changelog; deskmodder.de forum threads on skin margins; community reports on VS Test Explorer with xUnit v3 MTP, StartupApproved values, Run-key delay; Stack Overflow #01000000 report; Chinese-language blogs on AllowsTransparency and glass route; AlternativeTo listings.

---
Research completed: 2026-10-09. Ready for roadmap: yes, after Phase 0 outcomes or explicit carry-over of the open items above.
