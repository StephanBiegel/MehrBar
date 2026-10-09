# Architecture Research

**Domain:** RocketDock-style Windows application launcher dock (C# / .NET 10 / WPF), magnifying icon bar on a screen edge
**Researched:** 2026-10-09
**Confidence:** MEDIUM-HIGH overall. Structure, data flow, and Core/App boundaries are HIGH (derived from the concept's fixed constraints plus WPF layered-window mechanics from primary sources). The "survive Win+D" mechanism is MEDIUM-LOW until spiked on this machine (Windows 11 build 26200). Zoom-anchor behaviour is LOW until calibrated against `samples/reference/`.

Sources marked [src] below were read during this research; everything else is engineering judgement from the concept and general WPF/Win32 knowledge and is flagged as such.

---

## Standard Architecture

Docks of this family (RocketDock, ObjectDock, Nexus, Rainmeter-style overlays) share one shape: a borderless per-pixel-alpha window that is larger than the visible bar, one custom-painted surface (no per-icon controls), a per-frame layout function fed by the pointer position, and a thin shell layer for icons, launching, and z-order. MehrBar's Core/App split maps onto this cleanly: the per-frame layout function and everything that decides "where does each pixel go" is pure and lives in Core; the App only owns an HWND, a paint surface, and Win32.

### System Overview

```
 App (WPF, net10.0-windows)                                    Core (net10.0, no WPF/Win32)
┌──────────────────────────────────────────────────────────┐  ┌──────────────────────────────────────┐
│ Hosting:  Program.Main -> SingleInstance -> App           │  │ Model:    DockConfig / BarConfig /    │
│           AppHost (composition root, no DI container)     │  │           DockItem (polymorphic)      │
│           TrayIcon, Autostart, Log                        │  │ Config:   ConfigStore (immutable      │
├──────────────────────────────────────────────────────────┤  │           snapshot + Changed event),  │
│ Bars (one per BarConfig):                                 │  │           BarConfigDiff, JSON I/O,    │
│   BarController  <------ ConfigStore.Changed              │  │           atomic write, migration     │
│     |  owns                                              │  │ Layout:   AxisFrame, RestLayout,      │
│     +- BarWindow  (HWND: layered, NOACTIVATE, bottom)    │  │           ZoomEngine, ZoomAnimator,   │
│     +- BarSurface (DrawingVisual layers: plate,           │  │           BarPlacement, WindowPlan,   │
│     |              background, icons, label)             │  │           NineSlice, HitTest,         │
│     +- PointerSource (GetCursorPos + events)              │  │           LabelPlacement             │
│     +- DropTarget (OLE) + ReorderDrag (capture)           │  │ Skins:    RocketDockSkinLoader (INI   │
│   FrameClock (shared; CompositionTarget.Rendering        │  │           -> SkinInfo), ISkin metrics │
│                subscribed only while a bar is active)    │  │ Drop:     DropPayload, DropClassifier │
│   IconCache (async load, frozen bitmap pyramid)          │  │ Launch:   Launcher + IShell seam      │
│   SkinService (decode PNG -> frozen sources, 9 slices)   │  │ Icons:    IconLevel.Choose (pure)     │
├──────────────────────────────────────────────────────────┤  └──────────────────────────────────────┘
│ Settings window (MVVM) -- writes ONLY via ConfigStore    │            ^
├──────────────────────────────────────────────────────────┤            | references (App -> Core only)
│ Interop (only place with P/Invoke; CsWin32-generated):    │            |
│   WindowStyles, ZOrderKeeper, DesktopState, Monitors,     │  ┌──────────────────────────────────────┐
│   Dpi, ShellIcons (IShellItemImageFactory), ShellLink,    │  │ MehrBar.Tests (xUnit) -> Core only   │
│   TrayNative (Shell_NotifyIcon), CursorPos, Autostart(reg)│  └──────────────────────────────────────┘
└──────────────────────────────────────────────────────────┘
```

Dependency rule: `Tests -> Core <- App`. Core has no reference to WindowsBase/PresentationCore (so no `System.Windows.Rect/Point`; Core defines its own tiny `Span`, `RectD`, `SizeD` structs) and targets plain `net10.0` (not `net10.0-windows`), which makes it testable anywhere and mechanically enforces the boundary.

### Component Responsibilities

| Component | Layer | Responsibility | Typical Implementation |
|-----------|-------|----------------|------------------------|
| `DockConfig/BarConfig/DockItem` | Core/Model | Immutable persisted state; polymorphic items; unknown-field preservation | C# `record`s, `System.Text.Json` with `[JsonPolymorphic]` + custom converter for unknown `type`, `[JsonExtensionData]` |
| `ConfigStore` | Core/Config | Single source of truth at runtime: holds current immutable snapshot, `Update(Func<DockConfig,DockConfig>)`, raises `Changed(old,new)`, debounced persist | Plain class, UI-thread only, `IConfigPersistence` seam (file impl uses `File.Replace` for atomic swap + backup) |
| `BarConfigDiff` | Core/Config | Classifies what changed between two `BarConfig`s as `[Flags] BarChange` so consumers do minimal work | Pure static function, unit-tested |
| `AxisFrame` | Core/Layout | Maps axis space `(u along edge, v = depth away from edge)` to window space `(x,y)` and back for a given `BarEdge`; the ONLY place that knows about horizontal vs vertical | `readonly struct`, ~60 lines |
| `RestLayout` | Core/Layout | Positions/sizes of all items in resting state along `u`; rest length; input to everything else | Built from items + `BarMetrics`; rebuilt only on config/skin change |
| `ZoomEngine` | Core/Layout | `(RestLayout, ZoomParams, pointerU?, progress) -> FrameLayout`; cosine kernel, separators don't zoom, pluggable anchor | Pure static function; allocation-free variant writing into a reusable `FrameLayout` |
| `ZoomAnimator` | Core/Layout | Time-based progress 0..1 toward target (pointer inside/outside), eased, injectable time, reports `IsSettled` | Small state machine, tested with fake clock |
| `BarPlacement/WindowPlan` | Core/Layout | Edge/align/offset/edgeOffset + monitor rect + max zoomed extent + label/skin margins -> window rect and bar origin inside window | Pure; recomputed only on config/monitor/DPI/item-count change, never per frame |
| `NineSlice` | Core/Layout | `(srcSize, margins, destRect) -> 9 (srcRect,dstRect) pairs`, pixel-snapped | Pure; App only blits the pairs |
| `HitTest` | Core/Layout | Item under pointer from the **displayed** `FrameLayout`; insertion index for drops | Pure |
| `RocketDockSkinLoader` | Core/Skins | Parse `background.ini` / `separator.ini` into `SkinInfo` (paths + margins + outside margins); tolerant; never throws to caller | Own ~100-line INI reader (BOM, ANSI/UTF-8, case-insensitive keys) |
| `DropClassifier` | Core/Drop | `DropPayload` (paths, URLs) -> `DockItem`s; file vs folder vs `.lnk` vs `.url` via `IFileSystem` seam | Pure + fake FS in tests |
| `Launcher` | Core/Launch | Decide how to start an item (shell execute, runas, explorer /select, URL) and surface errors as results, not exceptions | Uses `IShell` seam; App implements with `Process.Start`/`ShellExecuteEx` |
| `BarController` | App/Bars | Per-bar orchestration: reacts to `Changed`, owns window/surface/pointer/drop, runs one `Tick` per frame | Plain class; the only place that wires Core engines to WPF |
| `BarWindow` | App/Bars | HWND host: layered transparent window, tool-window + no-activate styles, z-order hooks, DPI changes | `Window` with `AllowsTransparency`, `WindowStyle=None`, `ShowActivated=false`, `HwndSource` hook |
| `BarSurface` | App/Bars | Custom-drawn element: renders a `FrameLayout`, nothing else | `FrameworkElement` hosting a few `DrawingVisual`s |
| `FrameClock` | App/Bars | Shared render loop; subscribes `CompositionTarget.Rendering` only while active | Singleton-per-UI-thread, named handler, `-=` before `+=` |
| `IconCache` | App/Shell | Async load of shell icons at 256 px, build bitmap pyramid, frozen `BitmapSource`s, placeholder while loading | `ConcurrentDictionary<IconKey, Lazy<Task<IconSet>>>`, thread-pool load, `Freeze()` |
| `ZOrderKeeper` | App/Interop | Keeps bar at bottom, re-asserts after shell events, Show-Desktop handling | `WM_WINDOWPOSCHANGING` veto + `SetWinEventHook` + timer fallback |
| `TrayIcon` | App/Tray | Notification icon, menu, `TaskbarCreated` re-add | Own thin `Shell_NotifyIcon` wrapper on a message-only window |
| `SingleInstance` | App/Hosting | One process per user session; second launch signals first | Named `Mutex` + named `EventWaitHandle` (or pipe), checked before WPF starts |
| `SettingsWindow` + VM | App/Settings | Edits one bar's config live | MVVM; VM calls `ConfigStore.Update`, listens to `Changed` |

---

## Recommended Project Structure

Extends the concept's section 4 layout (kept as-is; additions marked `+`).

```
src/
├── MehrBar.Core/                     # net10.0, zero UI/Win32 references
│   ├── Model/                        # DockConfig, GeneralConfig, BarConfig, LabelConfig, DockItem + subtypes
│   │                                 # + UnknownItem (round-trips unknown "type" verbatim)
│   ├── Config/                       # ConfigStore, BarConfigDiff, ConfigSerializer, IConfigPersistence,
│   │                                 # FileConfigPersistence, Migrations/, Defaults (single place for Q2 defaults)
│   ├── Layout/
│   │   ├── Geometry.cs               # + Span, RectD, SizeD (own types; no WindowsBase)
│   │   ├── AxisFrame.cs              # + BarEdge <-> axis-space mapping
│   │   ├── BarMetrics.cs             # + padding along/across axis, separator metrics, outside margins (from skin or default)
│   │   ├── RestLayout.cs
│   │   ├── ZoomParams.cs / ZoomKernel.cs / ZoomAnchor.cs
│   │   ├── ZoomEngine.cs / FrameLayout.cs
│   │   ├── ZoomAnimator.cs
│   │   ├── BarPlacement.cs / WindowPlan.cs
│   │   ├── LabelPlacement.cs
│   │   ├── NineSlice.cs
│   │   └── HitTest.cs
│   ├── Skins/                        # SkinInfo, RocketDockSkinLoader, IniReader, DefaultSkin (flat translucent fallback)
│   ├── Drop/                         # + DropPayload, DropClassifier, IFileSystem
│   ├── Icons/                        # + IconKey, IconLevel (choose pyramid level for target px)
│   └── Launch/                       # Launcher, IShell, LaunchResult
├── MehrBar.App/                      # net10.0-windows, UseWPF
│   ├── Hosting/                      # + Program.cs (custom Main), SingleInstance, AppHost, AppLifetime, Autostart, Log
│   ├── Bars/                         # BarController, BarWindow, BarSurface, PointerSource, DropTarget, ReorderDrag
│   ├── Rendering/                    # + FrameClock, IconPainter, NineSliceRenderer, LabelRenderer (cached geometry)
│   ├── Shell/                        # + IconCache, ShellLauncher (IShell impl), ItemFactory (.lnk resolve)
│   ├── Settings/                     # SettingsWindow, SettingsViewModel (MVVM)
│   ├── Tray/                         # TrayIcon
│   ├── Interop/                      # NativeMethods.txt (CsWin32), WindowStyles, ZOrderKeeper, DesktopState,
│   │                                 # Monitors, Dpi, ShellIcons, ShellLink, Cursor, TrayNative
│   ├── Resources/                    # Strings.de.resx (UI text from day one)
│   └── app.manifest                  # PerMonitorV2, no UAC elevation
└── MehrBar.Tests/                    # xUnit; Core only; calibration fixtures from samples/reference as numbers (not images)
```

### Structure Rationale

- **Core never sees pixels of WPF, only numbers.** Everything the renderer needs is a list of `(srcRect, dstRect)` / `(iconIndex, rect)` records. This is what makes "the right bar later" a Core-only change plus one rotation in the renderer.
- **`Interop/` is flat and boring.** One file per concern, no logic beyond marshalling, so the Win32 surface is auditable in one place. Use Microsoft's `Microsoft.Windows.CsWin32` source generator (MIT, Microsoft-maintained) with a `NativeMethods.txt` list instead of hand-written `DllImport`s: fewer signature bugs (HRESULT/`BOOL` marshalling, `SetWindowPos` flags), no runtime dependency (generator is build-time only). MEDIUM confidence on exact current version; check at install time.
- **`Rendering/` separate from `Bars/`:** `Bars` is wiring/lifecycle, `Rendering` is stateless drawing helpers. Keeps `BarController` readable.
- **`Hosting/`:** custom `Main` is required so the single-instance check runs before WPF initialises (second launch must exit in a few ms, and must not flash anything).

---

## Architectural Patterns

### Pattern 1: Axis-neutral layout via an `AxisFrame` (axis space vs window space)

**What:** All Core layout math happens in a 2-D *axis space*: `u` runs along the bar (left->right on a horizontal bar, top->bottom on a vertical bar), `v` is **depth measured from the screen edge, increasing away from it**. Icons always grow toward +v (away from the edge), exactly what the concept requires ("Symbole wachsen vom Bildschirmrand weg"). `AxisFrame(BarEdge)` converts rects to window space with one small switch:

| Edge | window x | window y |
|------|----------|----------|
| Top | `u` | `v` |
| Bottom | `u` | `H - v` (flip) |
| Left | `v` | `u` |
| Right | `W - v` (flip) | `u` |

Rects stay axis-aligned, so conversion is "swap axes and maybe flip", not a general matrix. Pointer input is converted the other way once per frame (`FromWindow`). `Layout/` never contains the words `X`, `Y`, `Width`, `Height`; it uses `Along/Across` or `U/V`.

**When to use:** Always. Cheapest moment to do this is before the first line of layout code.

**Trade-offs:**
- (+) Right-edge bar = pass `BarEdge.Right`; zoom math, anchoring, hit-testing, drop-index, window sizing are reused and already tested.
- (+) Unit tests can run the same fixture on all four edges and assert equivalent results (a mechanical axis-neutrality test).
- (-) Things that are *not* axis-neutral must be handled explicitly and are the real V2 work: (1) the skin background must be rotated for vertical bars (render by pushing a rotate transform around the 9-slice draw; icons are drawn upright at their mapped rects), (2) label sizes must be converted (`AxisFrame.ToAxisSize(w,h)`): on a vertical bar the label's *depth* is its text width, so the window needs depth for the longest label, and on a horizontal bar labels near the ends overhang the bar along `u`, so the window needs extra length too, (3) hit plate and window plan must account for both.

**Example:**
```csharp
// Core/Layout - illustrative, not final API
public enum BarEdge { Top, Bottom, Left, Right }

public readonly record struct AxisFrame(BarEdge Edge)
{
    public bool IsHorizontal => Edge is BarEdge.Top or BarEdge.Bottom;

    public RectD ToWindow(Span u, Span v, SizeD window) => Edge switch
    {
        BarEdge.Top    => new RectD(u.Start, v.Start, u.Length, v.Length),
        BarEdge.Bottom => new RectD(u.Start, window.Height - v.End, u.Length, v.Length),
        BarEdge.Left   => new RectD(v.Start, u.Start, v.Length, u.Length),
        BarEdge.Right  => new RectD(window.Width - v.End, u.Start, v.Length, u.Length),
        _ => throw new ArgumentOutOfRangeException()
    };

    public (double U, double V) FromWindow(double x, double y, SizeD window) => Edge switch
    {
        BarEdge.Top    => (x, y),
        BarEdge.Bottom => (x, window.Height - y),
        BarEdge.Left   => (y, x),
        BarEdge.Right  => (y, window.Width - x),
        _ => throw new ArgumentOutOfRangeException()
    };
}
```

### Pattern 2: Rest-space input, displayed-space output (no feedback loop)

**What:** `ZoomEngine` is a pure function of `(RestLayout, params, pointerU, progress)`. The zoom factor `f_i` of each item is computed from the distance between the pointer and that item's **rest** centre (concept section 6; otherwise the displacement feeds back and the bar "swims"). The output `FrameLayout` holds the **displayed** rect per item. Hit-testing for clicks/drops runs against the last `FrameLayout` that was actually drawn ("click hits what is visibly under the pointer"), never against the rest layout.

**Kernel and calibration:** `f = (cos(pi*d/r)+1)/2` for `d<r`, `d` in units of `(iconSize+spacing)`, `size = iconSize + (zoomSize-iconSize)*f*progress`. Sanity check done during this research: with `r = 2.5`, `d=1 -> f=0.65 -> 32+89*0.65 ~ 90 px` and `d=2 -> f=0.095 -> ~40 px`, matching the measured 90-95 px / ~40 px from the reference screenshots. So the mapping `zoomWidth(4) -> r(2.5)` is plausible; keep it behind one function (`ZoomKernel.RadiusFor(zoomWidth)`) so calibration is a one-line change plus fixture update. [reasoning, HIGH for the arithmetic, MEDIUM for what RocketDock really does]

**Anchor as a strategy, not an enum branch:** `ZoomAnchor { Mode: Center|Cursor, Bias: 0..1 }`. After placing items sequentially to get `newLength`, the new start is `start = u* - lambda * newLength` where `(u*, lambda)` is `(restCenter, 0.5)` for Center, and `(pointerRestU, (pointerRestU-restStart)/restLength)` for Cursor, blended by `Bias`. Observation from the reference table worth feeding into calibration (LOW confidence, 5 screenshots): the left edge reads `-3` in three of four zoomed shots, which looks like **clamping to the screen/crop edge**, and the centre moves *opposite* to the pointer (404 -> 420 when pointer is left, -> 365 when far right), i.e. RocketDock behaves like a *partial* cursor anchor, not a pure centre anchor. Building `Bias` and a `ClampToMonitor` option in from the start costs ~10 lines and avoids a rewrite if the pure-centre model fails the "feels like RocketDock" test (Core Value).

**When to use:** always; this is the heart of the product.

**Trade-offs:** allocation-free variant (reuse a `FrameLayout` with preallocated `ItemFrame[]`) avoids GC at 60 fps with 60+ icons, at the price of mutable output; keep the pure `Compute(...)` overload for tests and an `ComputeInto(FrameLayout)` for the hot path, both calling the same core.

### Pattern 3: Pull-based, self-stopping frame loop

**What:** One `FrameClock` for the whole UI thread. It subscribes `CompositionTarget.Rendering` (a **static** event, per [src] Microsoft docs and community notes it keeps firing as long as it is subscribed, even when your control is not visible) only while at least one `BarController` reports `Active`. Each tick calls `controller.Tick(renderingTime)`, which returns `Active` or `Settled`. When all controllers are `Settled`, the clock unsubscribes. Idle state = zero subscribed handlers = WPF produces no frames and the process sits at ~0% CPU (the retained `DrawingVisual` content stays composited by DWM).

**Wake sources** (each calls `clock.Request(controller)`): pointer enters the hit plate, `DragEnter/DragOver`, `ConfigStore.Changed` that affects visuals, icon finished loading, skin reloaded, DPI/monitor change.

**Rules (from [src] docs and judgement):**
- Use a **named** handler and `-=` before `+=`; guard against double subscription (event is static and survives window close).
- Use `RenderingEventArgs.RenderingTime` as the clock, not a count of calls. The docs describe it as the time the *next* frame is expected, and the handler may be called more than once per frame when the visual tree changes; **skip the tick if `RenderingTime` equals the previous one**.
- Do not change layout (Measure/Arrange) inside the handler; only re-record `DrawingVisual`s (docs: layout in the handler causes an extra layout pass).
- Settle criterion per bar: `progress == 0 && pointer outside && !dropActive`. While the pointer is inside and progress is 1 the bar stays `Active` (user is interacting; ticks that produce an identical `FrameLayout` skip re-recording).
- Pointer truth comes from `GetCursorPos` polled inside `Tick` (converted via `AxisFrame.FromWindow`), with window mouse events used only to wake the loop. Reason: a layered window's `MouseLeave` can lag when the hit area shrinks under a stationary cursor; polling makes "pointer left" deterministic. During OLE drag the cursor position comes from `DragOver` args instead (mouse events are suppressed during drag).

**Example:**
```csharp
sealed class FrameClock
{
    readonly HashSet<IFrameClient> _active = new();
    TimeSpan _last = TimeSpan.MinusOne;
    bool _subscribed;

    public void Request(IFrameClient c) { _active.Add(c); Subscribe(); }

    void Subscribe()
    {
        if (_subscribed) return;
        CompositionTarget.Rendering -= OnRendering;   // belt and braces
        CompositionTarget.Rendering += OnRendering;
        _subscribed = true;
    }

    void OnRendering(object? s, EventArgs e)
    {
        var t = ((RenderingEventArgs)e).RenderingTime;
        if (t == _last) return;                        // duplicate call within one frame
        _last = t;
        _active.RemoveWhere(c => c.Tick(t) == FrameState.Settled);
        if (_active.Count == 0) { CompositionTarget.Rendering -= OnRendering; _subscribed = false; }
    }
}
```

### Pattern 4: Custom-drawn surface with a few retained `DrawingVisual` layers

**Decision: one `FrameworkElement` (`BarSurface`) that hosts 3-4 `DrawingVisual`s, not `OnRender`, not an `ItemsControl`.**

| Option | Verdict | Reason |
|--------|---------|--------|
| `ItemsControl` + one control per icon | Reject | Layout/measure per item per frame, styles/templates, hit-testing tree; contradicts the concept; no way to get 60 fps/60+ icons with animation of every item's position |
| Single `OnRender` override + `InvalidateVisual()` per frame | Acceptable fallback | Simplest. But `InvalidateVisual` also dirties arrange/layout; cannot re-record layers independently |
| **`FrameworkElement` + `VisualCollection` of `DrawingVisual`s** | **Recommend** | No layout work per frame; re-record only the layers that changed; `DrawingVisual.RenderOpen()` records into WPF's retained display list, DWM/MIL composes. Per-frame cost is O(visible items) `DrawImage` calls |
| Per-icon `DrawingVisual` with `TranslateTransform`/`ScaleTransform` | Reject | With anchor `center`, *every* item's position changes each frame (the whole bar shifts), so no saving, and 60 visuals add retained-tree overhead |
| Direct2D/DirectComposition custom window | Reject for V1 | Large amount of interop and no WPF; revisit only if the layered-window present cost fails the spike |

Layers (bottom to top): **HitPlate** (alpha ~1 rectangle, see Pattern 5; re-record when bar length or rest depth changes), **Background** (9-slice skin; re-record when bar length changes, i.e. each zoom frame), **Icons** (every active frame), **Label** (every active frame while visible; text geometry is cached per item/size, only its offset changes).

Drawing rules:
- Each icon is drawn from an **already-frozen** `BitmapSource` with `RenderOptions.SetBitmapScalingMode(layer, BitmapScalingMode.HighQuality)`.
- **Bitmap pyramid:** downscaling a 256 px bitmap to 32-40 px in one GPU step aliases/shimmers. `IconCache` pre-builds levels (e.g. 256, 128, 64, 48, 32 *physical* px) once at load on the background thread; `IconLevel.Choose(targetPhysicalPx)` (pure, in Core, testable) returns the smallest level >= target. [judgement, MEDIUM; verify visually in the zoom milestone]
- Snap resting rects to integer device pixels so the 32 px state is crisp; during zoom, sub-pixel positions are fine.
- Draw 9-slice sub-rects at integer pixel boundaries (computed in `NineSlice`) to avoid hairline seams between slices.
- **No `DropShadowEffect`/`BitmapEffect` on the window or layers.** Label outline = cached `FormattedText.BuildGeometry` drawn with a `Pen`; shadow = the same geometry drawn once more offset, low alpha. Effects on layered windows are a known performance trap.
- Pass the real `PixelsPerDip` to `FormattedText`.

### Pattern 5: Max-extent transparent window + alpha-1 hit plate for click-through

**Facts established from primary sources** ([src] Dwayne Need, "Transparent Windows in WPF", Microsoft archive):
1. `AllowsTransparency=True` makes WPF use a layered window in *application-provided content* mode (`UpdateLayeredWindow`). The OS raw-input thread hit-tests **against the bitmap's alpha**: a pixel with 100% transparency is skipped and the click goes to whatever window is beneath. So the concept's open question ("Klick-Durchlass") is answered: **fully transparent pixels already pass clicks through, no extra work and no `WS_EX_TRANSPARENT` toggling needed.**
2. The flip side: for WPF to receive *any* mouse message, the pixel under the pointer must be at least partially opaque. WPF's own hit testing is independent (a `Transparent` brush counts as solid inside WPF), so `Background="Transparent"` alone does **not** make the area clickable. Recommended technique from the same source: a nearly transparent brush.
3. Layered window with child HWNDs does not work (not needed here; never host child HWNDs/WebViews).
4. `WM_NCHITTEST -> HTTRANSPARENT` does not help for a layered window across threads/processes; the alpha test happens before the app is asked.

**Design that follows:**
- **Window = max zoomed extent**, computed once per config/monitor/DPI/item-count change by `WindowPlan` (Core): along `u`: `restLength + 2*maxGrowth + labelOverhang + skinOutsideMargins`, clamped to the monitor; across `v`: `edgeOffset + restDepth(with skin margins) + (zoomSize - iconSize) + labelDepth + shadow`. **Never resize or move the window per frame.** At centre anchor the window need not move at all; the conservative `2*maxGrowth` makes `cursor`/`bias` anchors safe without code changes. `maxGrowth` comes from a Core function that scans pointer positions across the rest layout and takes the maximum of `sum(size_i - iconSize)`, so it stays correct when items, sizes, or `zoomWidth` change.
- **HitPlate layer:** a rectangle painted with `Color.FromArgb(1, 0, 0, 0)` covering the *currently displayed bar rect* (bar length x rest depth) plus the bounding rect of the zoomed icons. Everything else stays alpha 0 and is click-through. The label is **not** part of the plate (the pointer over the label area should reach the desktop). The plate is also what makes the pointer-enter wake-up work in the idle state, because with a pure alpha-0 window the window never receives the first `MouseMove`.
- The alpha value is a named constant (`HitPlateAlpha = 0x01`). A Stack Overflow report (read via search summary, not verified first-hand) says `#01000000` was clickable on some machines and click-through on others; keep the constant tunable and verify on the target machine in the spike (fallback value 0x04-0x08; at 3% black it is invisible on a typical wallpaper).
- **Plan B if the plate proves unreliable:** window is `WS_EX_TRANSPARENT` by default (fully click-through); a `GetCursorPos` check (already in `Tick`, plus a 50 ms `DispatcherTimer` while idle... only if needed) removes `WS_EX_TRANSPARENT` while the pointer is over the plate. Costs a timer wakeup at idle, so only adopt if Plan A fails.
- **Present cost is bounded by dirty rects:** [src] the WPF team notes WPF updates the layered window by dirty rect; also notes layered windows pay a GPU->system memory->GDI copy and that a large, fully animated window is the worst case. A 1900x250 px region per frame is small on modern hardware, but this is the single biggest *unmeasured* performance risk, so it belongs in the first spike (see Build Order).
- Bars wider than the monitor (60+ icons at 36 px pitch = 2160 px) need a defined policy (clamp window; scale pitch down or scroll). Decide early; it changes `RestLayout`, not the window code.

### Pattern 6: Z-order "always at bottom" as an isolated keeper component

**What the sources say** ([src] Rainmeter `System.cpp` as summarised by a fetch; [src] Raymond Chen, "Old New Thing" 2011-06-17; [src] Microsoft Q&A thread via search):
- Windows offers **no flag** that exempts a normal window from Show Desktop (Win+D). Only windows that are part of the desktop layer (gadgets, historically; "on desktop" skins in Rainmeter today) survive it.
- Rainmeter, the best-known implementation of this exact requirement, keeps "On bottom" skins at `HWND_BOTTOM` (`SetWindowPos(..., HWND_BOTTOM, ...)`) but does **not** try to keep them visible during Show Desktop; for the survive-Win+D class ("On desktop") it:
  1. detects the Show-Desktop state by looking for its own hidden window relative to the **desktop-icons host window** (on Windows 11 24H2+ that host is `Progman` containing `SHELLDLL_DefView`; on older builds it is the `WorkerW` containing `SHELLDLL_DefView`),
  2. is notified by `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, ... out-of-context)` and backs this up with a timer (250 ms normally, 100 ms while the desktop is shown),
  3. while the desktop is shown it places a helper window directly above the desktop host (walking `GW_HWNDPREV` to the lowest topmost window and inserting after it), then reorders its windows relative to that helper; when the desktop is not shown the helper goes back to `HWND_BOTTOM`.
- A third-party NuGet `ZposDesktop` (MIT, native DLL + C# wrapper, 1.1.0 from Sept 2025, ~234 downloads) implements the same idea. **Do not depend on it** (tiny adoption, native DLL next to a single-file exe); read it as a second reference implementation if the in-house version struggles.
- Reparenting the window into `Progman/WorkerW` (the old "desktop widget" trick) is fragile across Windows builds (the WorkerW structure changed in Windows 8 and again in 11 24H2) and is not recommended.

**Design:** `ZOrderKeeper` (App/Interop) with three escalating responsibilities, built in this order so each can be tested independently:
1. **Baseline (Milestone 1):** window created hidden, styles applied in `SourceInitialized` (`WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`, no `WS_EX_TOPMOST`), `ShowActivated=false`, first `SetWindowPos(HWND_BOTTOM, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE)` *before* first show, then show without activation (avoids one visible frame at the top of the z-order). Handle `WM_MOUSEACTIVATE -> MA_NOACTIVATE`. Handle `WM_WINDOWPOSCHANGING` in the `HwndSource` hook: if `hwndInsertAfter != HWND_BOTTOM` and the change was not requested by the keeper, set `SWP_NOZORDER` in the `WINDOWPOS` struct (cheap veto that stops WPF/other code raising the window).
2. **Show Desktop survival (spike in Milestone 1, harden in Milestone 6):** `DesktopState` watcher using the Rainmeter approach (WinEvent foreground hook + timer fallback, host window resolved per Windows build, cached and re-resolved on failure). On "desktop shown" place the bar directly above the desktop host; on "windows visible again" return to `HWND_BOTTOM`. **The acceptance criterion "übersteht Win+D" is not guaranteed by `HWND_BOTTOM` alone; per Rainmeter's design it needs this watcher.** Treat the exact mechanism as MEDIUM-LOW until demonstrated on this machine (Windows 11 build 26200 is on the 24H2+ path).
3. **Robustness (Milestone 6):** re-assert on `WM_DISPLAYCHANGE`/DPI change/Explorer restart (`TaskbarCreated` registered message), re-resolve the desktop host after Explorer restart, and guard against re-assert loops (the veto must ignore the keeper's own calls via a flag).

Keep the keeper behind `IZOrderPolicy` so V2 `layer: normal|top` are just other policies (`top` = `WS_EX_TOPMOST`, `normal` = no enforcement).

### Pattern 7: Immutable config snapshot + store + diff = live settings

**What:** Persisted state is an immutable record tree. `ConfigStore` holds the current snapshot and is the **only** way to change it:

```csharp
store.Update(c => c.WithBar("b1", b => b with { ZoomSize = 140 }));   // raises Changed(old, new)
```

Everything else is a subscriber: each `BarController` filters `Changed` by its bar id, runs `BarConfigDiff.Classify(oldBar, newBar)` and does the minimum:

| `BarChange` flag | Triggered by | Reaction |
|------------------|--------------|----------|
| `Placement` | edge, align, offset, edgeOffset, monitor | recompute `WindowPlan`, reposition window, re-assert z-order |
| `Metrics` | iconSize, spacing | rebuild `RestLayout`, recompute `WindowPlan`, rebuild icon pyramid levels |
| `Zoom` | zoomSize, zoomWidth, zoomDurationMs, zoomAnchor | update `ZoomParams`/`ZoomAnimator`, recompute `WindowPlan` (max growth) |
| `Label` | show/font/size | invalidate cached label geometry |
| `Skin` | skin name / `general.skinFolder` | `SkinService.Load` (falls back to default skin), recompute metrics + `WindowPlan` |
| `Items` | add/remove/reorder/edit | rebuild `RestLayout`, request icons, recompute `WindowPlan` |
| `Layer` | layer | swap `IZOrderPolicy` |

Then `clock.Request(this)` renders the change on the next frame. The settings window is just another writer: its VM sets a property -> `store.Update(...)`; it also listens to `Changed` so "Zurücksetzen auf Standardwerte" and external changes reflect in the controls (ignore echoes by value comparison to avoid slider feedback loops). **Persist with debounce** (e.g. 400-500 ms after the last change, flush on exit and on `SessionEnding`) so dragging a slider does not write 60 files per second; rendering is *not* debounced.

**Persistence details:**
- Write temp file in the same directory, then `File.Replace(temp, config.json, config.bak.json)`: one call gives atomic swap *and* the concept's "Backup vor jedem Speichern". (NTFS; same volume required, which is guaranteed by writing to the same folder.)
- On load failure: rename the broken file to `config.corrupt-<timestamp>.json`, try `config.bak.json`, else defaults. **Never silently overwrite a file that failed to parse.**
- `[JsonExtensionData] Dictionary<string, JsonElement>? Extra` on every record that can gain fields (forward-compat, concept requirement).
- Polymorphic items: `[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]` + `[JsonDerivedType]` per known type. For an **unknown** `type` (e.g. a `group` written by a newer build, or an older build reading a newer file) `IgnoreUnrecognizedTypeDiscriminators` falls back to the base type but loses the original JSON on re-save. Recommended: a small custom `JsonConverter<DockItem>` that reads the discriminator and, for unknown values, stores the raw `JsonElement` in `UnknownItem` and writes it back verbatim. Cover with a round-trip unit test (MEDIUM; verify exact STJ behaviour when implementing).
- Give each item a stable `id` (short GUID) in the JSON: needed for icon cache keys, reorder, future move-between-bars/groups. It is an additive field, so it does not conflict with the concept's sample.
- `Normalize()` clamps live values (e.g. `zoomSize >= iconSize`, `zoomWidth >= 1`) before they reach the engines; the settings UI can then not produce a state that breaks layout.

### Pattern 8: Skin pipeline (parse in Core, decode in App, slice math in Core)

```
Skins\<name>\background.ini, separator.ini, *.png
   -> Core.RocketDockSkinLoader.Load(dir)  -> SkinInfo { BgImagePath, Margins(L,T,R,B), OutsideMargins(L,T,R,B),
                                                        SepImagePath, SepTopMargin, SepBottomMargin }  (or SkinLoadResult.Failed -> DefaultSkin)
   -> App.SkinService.Decode(SkinInfo)     -> frozen BitmapSource(s), pixel sizes   (BitmapCacheOption.OnLoad: do not lock files)
   -> Core.BarMetrics.From(skinInfo, bitmapSizes, iconSize, spacing, dpi)   -> padding + outside margins feed RestLayout and WindowPlan
   -> per frame: Core.NineSlice.Compute(bgPixelSize, margins, destRect = barRect expanded by OutsideMargins)
   -> App.NineSliceRenderer draws the 9 pairs (integer-snapped), optionally rotated for vertical bars (V2)
```

- The meaning of `Outside-*Margin` is **not yet established** (concept section 7 says to verify against the real skin). Working hypothesis, consistent with "background keeps its height; zoomed icons overhang": the PNG contains drop-shadow/glow *outside* the nine-slice's logical frame, so the destination rect for the image is the bar rect **expanded** by the outside margins, and the icons sit inside the inner (margin) rect. Treat this as unverified until `docs/skin-format.md` is written from the actual `bg.png`; the architecture only requires that `BarMetrics` expose both "logical bar rect" and "painted rect", and that `WindowPlan` includes the painted rect so the shadow is not clipped.
- Separator: image drawn at fixed (skin-defined) thickness along `u`, stretched across `v` by its top/bottom margins; separator never zooms, only shifts.
- Failure policy (concept): missing/defective skin -> `DefaultSkin` (flat translucent rounded rect drawn with geometry, no image), one log line, no dialog. Skin reload must be atomic (build the new decoded skin fully, then swap the reference).
- Locate `Skins\` via `Path.GetDirectoryName(Environment.ProcessPath)` (or `AppContext.BaseDirectory`; [src] Microsoft single-file docs recommend `AppContext.BaseDirectory` for "files next to the executable"). `Assembly.Location` is empty in single-file apps. Verify once on the actual published artifact, because native-library self-extraction changes where the runtime unpacks things.

### Pattern 9: Icon cache

- **Key:** `IconKey(source: path|customIconPath, lnkIndexIfAny, kind)`; value: `IconSet { Level[] frozen bitmaps, pixel sizes }`. Items reference icons by key, never by `BitmapSource`, so reload/invalidate is one dictionary operation.
- **Load path (background thread, never UI):** `SHCreateItemFromParsingName` -> `IShellItemImageFactory.GetImage(size 256x256, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK)` -> `HBITMAP` -> copy pixels into a `byte[]` -> `BitmapSource.Create(... Bgra32/Pbgra32 ...)` -> build pyramid -> `Freeze()` -> post to UI. Using `Imaging.CreateBitmapSourceFromHBitmap` directly is the well-known way to lose or mangle alpha; copy the DIB bits explicitly and check premultiplication (verify against a real `.exe` and `.lnk`; MEDIUM). Always `DeleteObject` the HBITMAP; COM calls need an MTA/STA-appropriate thread (use a dedicated STA worker or `Task.Run` with explicit apartment handling; do not call shell COM from random thread-pool threads without checking).
- **Alternative** (`SHGetImageList(SHIL_JUMBO)` + `IImageList::GetIcon`) works but needs HICON->BitmapSource conversion and gives up per-item parsing-name handling; keep as fallback if `IShellItemImageFactory` returns blurry upscales for small-icon-only executables.
- `.lnk` handling per concept: store the `.lnk` path (keeps Store-app shortcuts working), ask the shell for the `.lnk`'s icon; resolve target/arguments/working dir with `IShellLink` only for the *Eigenschaften* dialog and for default naming (`ShellLink` in Interop, `ItemFactory` in App/Shell).
- **Custom icon** (PNG/ICO): `BitmapDecoder` with `BitmapCacheOption.OnLoad`, largest frame for ICO, same pyramid.
- **Placeholder:** the item is drawn with a neutral placeholder (or nothing) until its `IconSet` arrives; arrival calls `clock.Request` for one frame. Never block startup on icons (start < 1 s).
- **Memory:** 256x256x4 = 256 KB base + ~1/3 for lower levels; 100 items ~ 35 MB, acceptable. Cache invalidation: on custom-icon edit, on `DpiChanged` (levels are chosen in physical px, so only re-choose, do not reload), on file change (not needed in V1).
- Everything stored must be `Freeze()`-d `BitmapSource`: required to share across threads, and cheaper to render.

### Pattern 10: Drag & drop pipeline (external OLE) and reorder (internal capture)

```
OLE DataObject (Explorer / browser)                     internal: mouse down on item
   |                                                       |  moved > SystemParameters.MinimumHorizontal/VerticalDragDistance
   v                                                       v
App.DropTarget  (Window.AllowDrop, DragEnter/Over/Leave/Drop)    App.ReorderDrag (mouse capture, no OLE)
   | reads DataObject formats:                               |
   |  FileDrop (string[]),                                   |
   |  UniformResourceLocatorW / UniformResourceLocator,      |
   |  UnicodeText (URL text), FileGroupDescriptorW (virtual .url) |
   v                                                       |
Core.DropPayload { Paths[], Urls[] }                       |
   v                                                       |
Core.DropClassifier(IFileSystem) -> DockItem[]  (app/folder/url; .lnk stays as .lnk path)
   |                                                       |
   +---- pointer (from DragOver args) ---> BarController.PointerSource override --> ZoomEngine keeps zooming
   |         Core.HitTest.InsertIndex(frameLayout, pointerU) -> index (M1: always Append; M4: real index, gap/marker)
   v                                                       v
ConfigStore.Update(bar => bar with { Items = bar.Items.Insert(index, newItems) })  -> Changed -> normal flow
```

- OLE drag over a layered window works only on non-transparent (hit-plate) pixels, same rule as mouse; the HitPlate therefore covers the drop zone.
- Mouse events are suppressed while an OLE drag is in progress, so zoom during drag must be driven from `DragOver` positions (this is why `PointerSource` is an abstraction with two implementations: polled cursor and drag position).
- Use capture-based reorder instead of `DoDragDrop` for internal moves: no OLE round trip, no format plumbing, and a small movement threshold keeps single clicks launching items (concept section 8).
- Insertion feedback: inject a virtual gap item into `RestLayout` (items slide apart) rather than painting a marker over the zoomed icons; keeps zoom and insertion in the same math and testable.
- Dropped files from an **elevated** Explorer into a non-elevated MehrBar are blocked by UIPI; MehrBar must not run elevated (manifest `asInvoker`). "Als Administrator starten" is a per-launch `runas`, not a process mode.
- `NoActivate` windows still accept OLE drops; no activation needed.

### Pattern 11: Tray, single instance, application lifetime

- **Process start order (`Program.Main`, `[STAThread]`, custom entry point instead of `App.xaml` as `ApplicationDefinition`):** (1) create/open named `Mutex` `Local\MehrBar.<hash-of-sid>` (treat `AbandonedMutexException` as acquired); (2) if already running: signal the first instance via named `EventWaitHandle` (meaning "open settings" or "show bars") and exit **before** WPF loads; (3) otherwise create `App`, `AppHost`, run.
- **Lifetime:** `ShutdownMode = OnExplicitShutdown` (no main window; closing the settings window must not end the app). Single exit path `AppLifetime.Exit()`: flush `ConfigStore`, dispose `TrayIcon` (`NIM_DELETE`, otherwise a ghost icon stays until hover), dispose `ZOrderKeeper` hooks, release mutex, `Shutdown()`. Hook `SessionEnding` for a synchronous flush on logoff/shutdown. Tray "Leiste ein-/ausblenden" toggles `Visibility`/stops the clock; it does not close windows.
- **Global error handling:** `DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` -> log; the dock should stay alive after a bad icon or launch failure (concept section 12).
- **Tray:** WPF has no built-in notify icon. Options: (a) WinForms `NotifyIcon` via `UseWindowsForms` (works, pulls WinForms into the bundle, DPI/menu quirks), (b) `H.NotifyIcon.Wpf` (maintained third-party, MIT; not verified in this research), (c) **recommended:** a ~150-line own wrapper over `Shell_NotifyIcon` + a message-only window in `Interop/TrayNative`, because the project already needs a message loop hook for `TaskbarCreated` (Explorer restart re-add is on the Milestone 6 list), wants no heavy dependencies, and the V1 tray is minimal (3 menu entries, double-click). The same wrapper can raise `NIF_INFO` balloons, a cheap answer to the open "unaufdringliche Startfehler-Meldung" question.
- **Autostart:** `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value pointing at `Environment.ProcessPath`; verify/repair the path at startup when the setting is on (the exe may have moved).
- **Context menus on a non-activatable window:** a WPF `ContextMenu` opened from a `WS_EX_NOACTIVATE` window can misbehave (no deactivation event to dismiss on outside click, no keyboard focus). Native `TrackPopupMenu` (with the `SetForegroundWindow` trick) dismisses correctly but is not WPF-styled. This is a decision with Milestone 1 impact ("Entfernen" needs a menu) and should be settled in the first spike (see Build Order).

---

## Data Flow

### Per-frame flow (hot path)

```
 wake: MouseEnter/Move on HitPlate | DragOver | ConfigChanged | IconLoaded | SkinChanged | DpiChanged
   |
   v
 FrameClock.Request(controller) --> subscribe CompositionTarget.Rendering (if not already)
   |
   v   every render tick (skip if RenderingTime unchanged)
 BarController.Tick(t):
   pointerWin = PointerSource.Get()                      (GetCursorPos -> window-local px -> DIP; or DragOver pos)
   (u, v)     = AxisFrame.FromWindow(pointerWin, windowSize)
   inside     = HitTest.InHoverZone(lastFrame or rest, u, v)
   progress   = ZoomAnimator.Step(t, inside)              (eased, time-based)
   frame      = ZoomEngine.ComputeInto(rest, zoomParams, anchor, inside ? u : null, progress, frameBuf)
   if frame != lastFrame:  BarSurface.Render(frame, hoveredIndex)   (re-record only changed layers)
   lastFrame  = frame
   return (progress == 0 && !inside && !dropActive) ? Settled : Active
   |
   v
 all Settled --> unsubscribe  ==> idle: no frames, ~0% CPU
```

### Input -> action flow

```
Mouse down/up on surface -> HitTest(lastFrame, u, v) -> item index (same item on down+up, no drag threshold) -> Launcher.Launch(item)
   -> IShell (Process.Start / ShellExecuteEx / explorer /select) -> LaunchResult -> error => tray balloon + log (never throw)
Right click -> item or bar menu -> command -> ConfigStore.Update / Launcher / SettingsWindow.Show / AppLifetime.Exit
```

### Config / state flow

```
                         ┌─────────────── SettingsWindow VM ───────────────┐
                         │ set property -> store.Update(c => c with {...}) │
                         └───────────────────────┬─────────────────────────┘
 Drop / Reorder / Menus --> store.Update(...) ---+--> ConfigStore (immutable snapshot)
                                                  |        |  Changed(old,new)
                                                  |        +--> BarController (filter by bar id)
                                                  |        |       BarConfigDiff -> BarChange flags -> minimal rebuild -> FrameClock.Request
                                                  |        +--> SettingsViewModel (re-sync controls)
                                                  |        +--> TrayIcon (visibility state)
                                                  +--(debounced 400 ms, flush on exit)--> FileConfigPersistence
                                                         temp file -> File.Replace(config.json, config.bak.json)
 Runtime-only state (hover index, zoom progress, current FrameLayout, window handles) lives in BarController, is never persisted
 and never flows back into ConfigStore.
```

### Key Data Flows

1. **First start:** no `config.json` -> `Defaults.NewDock()` (empty top-center bar with Q2 values) -> `ConfigStore` -> `BarController` -> `WindowPlan` (empty bar still has a sensible minimum length so there is something to drop onto) -> window shown at bottom of z-order -> debounced first save.
2. **Drop a file:** OLE DataObject -> `DropPayload` -> `DropClassifier` -> `ItemFactory` (name, `.lnk` kept as `.lnk`) -> `ConfigStore.Update(append)` -> `Changed` (`Items`) -> `RestLayout` rebuilt, `WindowPlan` recomputed, icon request issued -> placeholder frame now, real icon next frame after async load.
3. **Slider in settings:** VM -> `Update` -> diff (`Zoom`) -> controller recomputes params/window plan -> frame requested -> user sees effect live; disk write 400 ms after the last tick.
4. **DPI/monitor change:** `WM_DPICHANGED`/`WM_DISPLAYCHANGE` -> `Dpi`/`Monitors` interop -> controller recomputes `WindowPlan` in new px, re-chooses icon pyramid levels (no reload), `ZOrderKeeper` re-asserts.
5. **Show Desktop / Explorer restart:** `DesktopState` (WinEvent hook + timer) -> `ZOrderKeeper` repositions; `TaskbarCreated` -> `TrayIcon.Readd`, desktop host re-resolved.

---

## Scaling Considerations

Scale here means icon count and bar count, not users.

| Scale | Architecture Adjustments |
|-------|--------------------------|
| 1 bar, <= 30 icons | Everything above as designed; trivially under frame budget. |
| 1 bar, 60-100+ icons (acceptance criterion) | Per-frame cost = `ZoomEngine` O(n) float math + n `DrawImage` calls on frozen bitmaps; fine. Real risks are (1) window wider than the monitor, (2) base-size downscale aliasing (pyramid), (3) layered-window present cost for a ~1900 px wide window. Use the allocation-free `ComputeInto`; cull items outside the window. Only items within the zoom radius change **size**, but all items after them change **position**, so full re-record each active frame is correct, not wasteful. |
| N bars (V2: right bar, bars per edge) | One `BarController` + `BarWindow` per `BarConfig`. Share `FrameClock`, `IconCache`, `SkinService`, `ConfigStore`. Windows of different bars may overlap in their transparent regions with no conflict (alpha-0 passes input to whatever is beneath, including the sibling bar). Z-order keeper manages the set. |
| Multiple monitors (V2+) | `monitor` id already in the model; `BarPlacement` takes a monitor rect + DPI; `WindowPlan` per bar. No new architecture. |

### Scaling Priorities

1. **First bottleneck:** layered-window present cost per frame (GPU->system->GDI copy) for the large window. Mitigate by keeping dirty regions tight (re-record only changed layers, keep the hit plate static), measure in the first spike; if it fails, fall back to a smaller window sized to the *current* extent per active interval (resize only on zoom start/stop with a margin, never per frame) before considering anything exotic.
2. **Second bottleneck:** icon scaling quality/cost (aliasing at 32 px from 256 px sources) -> bitmap pyramid.

---

## Anti-Patterns

### Anti-Pattern 1: Per-icon controls or storyboards

**What people do:** `ItemsControl` of `Image`s with `ScaleTransform` animations per icon.
**Why it's wrong:** with centre anchoring every item moves each frame; layout/animation clocks per element, 60+ elements -> missed frames; also impossible to keep a single consistent layout function (the thing that needs calibrating and testing).
**Do this instead:** one surface, one pure layout function, one clock.

### Anti-Pattern 2: Resizing/moving the window per frame

**What people do:** keep the window tight around the current bar and `SetWindowPos` each frame.
**Why it's wrong:** layered-window resize forces full-surface realloc/repaint, causes flicker and DWM jitter, and fights the z-order keeper.
**Do this instead:** fixed max-extent window, recomputed only on config/monitor/DPI/item changes; the hit plate defines the interactive area.

### Anti-Pattern 3: Zoom factor from displayed positions

**What people do:** measure pointer distance against icons' current (already shifted) centres.
**Why it's wrong:** positive feedback -> bar swims/jitters (named in concept sections 6 and Q12).
**Do this instead:** compute `d` against rest centres; compare pointer to the displayed layout only for hit-testing.

### Anti-Pattern 4: Writing config on every change / mutating shared config objects

**What people do:** two-way bind sliders straight to a mutable config object, save in the setter.
**Why it's wrong:** dozens of disk writes per second, torn state visible to the renderer, no way to diff what changed, hard to test.
**Do this instead:** immutable snapshots, single `ConfigStore.Update`, diff-driven reaction, debounced persistence.

### Anti-Pattern 5: Win32 or WPF types leaking into Core

**What people do:** `System.Windows.Rect`/`Point` in layout code, `Process.Start` inside the model, `HWND` in config.
**Why it's wrong:** tests need a Windows desktop session; axis-neutrality erodes (WPF `Rect` is x/y-shaped).
**Do this instead:** Core structs `Span/RectD/SizeD`, `IShell`/`IFileSystem`/`IConfigPersistence` seams, target plain `net10.0`.

### Anti-Pattern 6: Relying on `HWND_BOTTOM` alone for "survives Win+D"

**What people do:** call `SetWindowPos(HWND_BOTTOM)` once and assume done.
**Why it's wrong:** per Raymond Chen there is no opt-out from Show Desktop for ordinary windows, and the best-known implementation (Rainmeter) uses a desktop-state watcher plus z-order reshuffling for exactly this requirement.
**Do this instead:** isolate in `ZOrderKeeper`, prove the mechanism early, harden later.

### Anti-Pattern 7: Leaving `CompositionTarget.Rendering` subscribed

**What people do:** subscribe once in the constructor.
**Why it's wrong:** static event keeps WPF rendering at vsync forever -> idle CPU/GPU wakeups, violates "im Ruhezustand keine Frames".
**Do this instead:** subscribe on demand, unsubscribe on `Settled`, add a debug counter (frames since last input) to prove idle in testing.

### Anti-Pattern 8: Loading shell icons on the UI thread or unfrozen

**What people do:** `SHGetFileInfo` in the layout path, `BitmapImage` without `Freeze()`.
**Why it's wrong:** UI stalls (slow network/shell extensions), cross-thread exceptions, slower rendering.
**Do this instead:** async load in `IconCache`, `Freeze()`, placeholder.

### Anti-Pattern 9: `Background="Transparent"` and assuming input works

**What people do:** make the window "invisible" yet expect mouse events.
**Why it's wrong:** layered-window hit test is on pixel alpha before WPF is consulted [src].
**Do this instead:** deliberate alpha-1 hit plate over exactly the interactive area; alpha-0 elsewhere for click-through.

### Anti-Pattern 10: Shadow/outline via `DropShadowEffect`

**What people do:** apply effects to the label or window for the outline/shadow look.
**Why it's wrong:** effect passes on a large layered surface are costly and can push rendering to software paths.
**Do this instead:** draw outline/shadow as cached geometry.

---

## Integration Points

### External Services / OS APIs

| API | Used by | Integration Pattern | Notes |
|-----|---------|---------------------|-------|
| `SetWindowPos`, `SetWindowLongPtr`, `WM_WINDOWPOSCHANGING`, `WM_MOUSEACTIVATE` | `WindowStyles`, `ZOrderKeeper` | `HwndSource.AddHook` | `WS_EX_LAYERED` is set by WPF and cannot be changed afterwards (WPF rejects it) [src]; only add `TOOLWINDOW`/`NOACTIVATE` |
| `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`, `FindWindow(Ex)`, `GetWindow(GW_HWNDPREV)` | `DesktopState` | out-of-context hook on UI thread + timer fallback | Host window differs on 24H2+ (`Progman`) vs older (`WorkerW`) [src Rainmeter]; re-resolve after Explorer restart |
| `IShellItemImageFactory`, `SHCreateItemFromParsingName` | `ShellIcons` | COM via CsWin32, background thread | Verify alpha handling and `.lnk` icon (arrow overlay or not) on real files |
| `IShellLink` / `IPersistFile` | `ShellLink` | COM, STA | Only for properties dialog and default name |
| `Shell_NotifyIcon`, `RegisterWindowMessage("TaskbarCreated")` | `TrayNative` | message-only window | Re-add on `TaskbarCreated`; `NIM_DELETE` on exit |
| `GetCursorPos`, `MonitorFromWindow`, `GetMonitorInfo`, `GetDpiForMonitor`/`WM_DPICHANGED` | `Cursor`, `Monitors`, `Dpi` | polled / message hook | Manifest `PerMonitorV2` so WPF delivers `DpiChanged`; window positioning in physical px via `SetWindowPos`, layout in DIPs |
| Registry `HKCU\...\Run` | `Autostart` | BCL `Registry` | Repair path if exe moved |
| `Process.Start` (`UseShellExecute=true`, verb `runas`), `explorer.exe /select,` , `ShellExecuteEx "properties"` | `ShellLauncher : IShell` | BCL / P/Invoke | Handle `Win32Exception` (UAC cancel = 1223) as non-error |
| `File.Replace` | `FileConfigPersistence` | BCL | Atomic swap with backup |
| `CompositionTarget.Rendering` | `FrameClock` | WPF event | Static; subscribe on demand [src] |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| App -> Core | direct calls to pure functions / interfaces | Core never calls back except through events on `ConfigStore` and interface seams |
| `ConfigStore` -> everyone | `Changed(old,new)` event, UI thread only | Subscribers must be unsubscribed on disposal (bar removal in V2) |
| `BarController` <-> `BarWindow`/`BarSurface` | method calls (`Render(frame)`, `ApplyPlan(plan)`) and a few events (`PointerWake`, `Dropped`, `Clicked`) | Surface holds no layout logic and no config |
| `BarController` -> `Interop` | through small interfaces (`IZOrderPolicy`, `IMonitorService`, `IPointerSource`) | Lets controller logic be tested with fakes if wanted |
| `SettingsViewModel` -> rest | only `ConfigStore` | Settings never touches windows or surfaces |
| Core `Launcher` -> OS | `IShell` | Errors returned as `LaunchResult`, reported by App (balloon + log) |

---

## Suggested Build Order (compared with the concept's section 10)

Dependency chain (A -> B means B needs A):

```
Spike: window host (layered + hit plate + bottom + NOACTIVATE + context-menu + present cost)
   decides -> Pattern 5/6/11 details
Core: Model + ConfigStore + BarConfigDiff + serializer
Core: Geometry + AxisFrame + BarMetrics(default) + RestLayout + WindowPlan(+max growth) + HitTest
   -> ZoomEngine + ZoomAnimator (needs RestLayout)
   -> BarController + BarSurface + FrameClock (needs FrameLayout contract)
SkinInfo/NineSlice (needs BarMetrics hooks) -> SkinService -> Background layer
IconCache (needs only IconKey) -> Icons layer
DropPayload/Classifier + ItemFactory (needs Model + ConfigStore) -> Drop append -> Insert index/reorder (needs HitTest + RestLayout gap)
SettingsViewModel (needs ConfigStore + Diff only)
ZOrderKeeper hardening / DesktopState / Explorer restart (needs the window host to exist)
```

**Concept order:** M1 Skeleton -> M2 Zoom -> M3 Skin -> M4 Drag&Drop+Menus -> M5 Settings -> M6 Polish. It is sound at the top level; the recommended changes are about *what must already exist in M1* so M2-M5 become additive instead of rewrites:

1. **Insert a time-boxed "Window host spike" at the start of M1 (or as M0).** One throwaway-quality window proving, on this machine: (a) `AllowsTransparency` window ~1900x250 px with alpha-0 click-through and an alpha-1 plate that reliably catches clicks and `MouseMove`; (b) `HWND_BOTTOM` + `WS_EX_TOOLWINDOW|NOACTIVATE` + first-show without flash, click does not raise or steal focus; (c) behaviour under Win+D (does it vanish? does the Rainmeter-style watcher bring it back?); (d) a context menu (WPF `ContextMenu` vs native `TrackPopupMenu`) that dismisses correctly from a no-activate window; (e) per-frame present cost at 60 fps with a repainting dummy (CPU/GPU %, dropped frames). These five facts decide Patterns 5, 6, and 11; each is cheap to test now and expensive to discover in M6. The concept lists (a) and (c) as "open technical points" for later; they are architecture-shaping and should move first.
2. **M1 should already contain the Core layout contract, with zoom progress fixed at 0.** Build `AxisFrame`, `RestLayout`, `BarMetrics` (default skinless), `WindowPlan` (including max-growth, label overhang, outside margins) and `FrameLayout` in M1 and have the renderer draw a `FrameLayout`. Then M2 only adds `ZoomEngine`, `ZoomAnimator`, `FrameClock` wiring, label, and the plate refinement. Without this, M1 grows a hand-rolled static layout that M2 throws away, and the window size would have to be re-derived once zoom arrives.
3. **Put `ConfigStore` (immutable + `Changed` + `BarConfigDiff` + debounced atomic persistence) in M1, not M5.** M1 already needs add-item and remove-item to write config; if those write through `Update(...)`, the M5 settings window is only a view-model. Also build `Normalize()`/defaults here.
4. **Settle menu technology in M1** (spike item d), because "Entfernen" is M1 scope.
5. **Hit plate belongs in M1, not M2.** In M1 the bar must receive clicks and drops over its whole area; the concept lists click-through under M2, but the plate is what makes the window usable at all.
6. **Skin hooks in `BarMetrics` from M1; M3 supplies real values.** Window sizing depends on skin outside margins and inner padding, so `WindowPlan` must take them as inputs from the start (default skin = fixed numbers), otherwise M3 changes window geometry and layout after they were "done".
7. **Drop pipeline classes in M1 as *append-only*, insertion index in M4.** `DropPayload -> DropClassifier -> ItemFactory -> ConfigStore.Update` is identical for M4; only `HitTest.InsertIndex` and the gap in `RestLayout` are new. Keep `PointerSource` abstract from M1 so DragOver can drive zoom later.
8. **Z-order: baseline in M1, Win+D watcher proven in the spike and wired in M1/M2 if cheap, hardened in M6.** The concept parks Win+D in M6; the mechanism, not the polish, is the risk.
9. **Tray, single instance, lifetime in M1 is correct** (tray "Beenden" is the dev-time exit for a window that hides behind everything).
10. **Calibration (M2):** keep the reference numbers as xUnit fixtures (rest/centre/edges table and the 121/90-95/40 px profile), include `Bias`/`ClampToMonitor` options (Pattern 2); only the *visual* "feels like RocketDock" check is manual.
11. **Add an axis-neutrality test in M2:** run the same zoom fixture through all four `BarEdge` values and assert mapped results are equivalent. This is the cheapest guarantee that the V2 right bar needs no Core rework; actually rendering a vertical bar can wait for V2 (needs rotated skin background and label-depth sizing).

Proposed phase skeleton for the roadmap (maps onto M1-M6):

| Phase | Contents | Notes |
|-------|----------|-------|
| 0/1a | Solution, Core model + `ConfigStore` + serializer (TDD), `Program.Main` + `SingleInstance` + log + `AppLifetime` + tray | Pure Core work can start before any WPF window exists |
| 1b | **Window host spike**, then `BarWindow` + `ZOrderKeeper` baseline + hit plate + static `FrameLayout` render + `IconCache` + launch + drop-append + remove | Research flag: HIGH (Show Desktop, context menu, present cost) |
| 2 | `ZoomEngine`/`ZoomAnimator`/`FrameClock`, labels, calibration, idle-frame proof | Research flag: MEDIUM (calibration vs screenshots; pyramid quality) |
| 3 | Skin: `docs/skin-format.md`, loader, 9-slice, separators, default skin, `Outside-*Margin` semantics | Research flag: MEDIUM (field semantics unverified) |
| 4 | Insert index + gap, reorder, URL drops, full menus, properties dialog, custom icons | Standard patterns |
| 5 | Settings window (MVVM over `ConfigStore`), reset to defaults, autostart | Low risk if Pattern 7 is in place |
| 6 | Win+D / Explorer restart / resolution change hardening, 100+ icons, error surfaces, log rotation, publish (R2R, single-file, native-lib extraction, startup < 1 s) | Research flag: MEDIUM (publish settings, see below) |

### Publish notes affecting architecture (MEDIUM)

- Self-contained single-file WPF: managed DLLs are bundled; WPF's native DLLs are not unless `IncludeNativeLibrariesForSelfExtract=true`, in which case they are extracted to `%TEMP%\.net\...` on first run ([src] Microsoft single-file docs). First-run extraction costs startup time; later runs reuse it. Measure both against the < 1 s target.
- ReadyToRun is the practical startup lever for WPF (an old .NET Core 3.0 sample dropped from 1.9 s to 1.3 s [src, directional only]); do **not** enable `EnableCompressionInSingleFile` (docs: decompression cost at startup) and do **not** enable trimming for a WPF app (heavy reflection/XAML loading). Native AOT for WPF is an experimental third-party fork only [src]; out of scope.
- `Skins\` as a loose folder works with single-file; resolve via `Environment.ProcessPath`'s directory or `AppContext.BaseDirectory`, never `Assembly.Location` (empty) [src].
- Avoid XAML-heavy startup: the bar window is code-driven (no big templates); the settings window is created lazily on first open to protect the startup budget.

---

## Open Questions / Spike List for Phase Research

| # | Question | Why it matters | Where it lands |
|---|----------|----------------|----------------|
| 1 | Does an `HWND_BOTTOM` tool-window disappear on Win+D on build 26200, and does the Rainmeter-style watcher restore it reliably (Progman host path)? | Acceptance criterion; z-order architecture | Spike, then M6 |
| 2 | Is alpha `0x01` a reliable hit plate here, or is `0x04-0x08` / Plan B needed? | Click-through + hover wake | Spike |
| 3 | Per-frame cost of a ~1900x250 layered WPF window on this GPU/driver (and the target hardware) | 60 fps criterion | Spike |
| 4 | WPF `ContextMenu` vs native menu from a no-activate window | M1 "Entfernen" | Spike |
| 5 | `Outside-*Margin` semantics, separator geometry | Window plan, skin rendering | M3, `docs/skin-format.md` |
| 6 | Anchor model: pure centre vs partial cursor anchor vs screen clamping | Core Value (feel) | M2 calibration |
| 7 | Bar longer than monitor (60+ icons x 36 px > 1920): shrink pitch, scroll, or cap? | RestLayout and WindowPlan policy | Decide before M2 |
| 8 | `IShellItemImageFactory` output: alpha/premultiplication, `.lnk` overlay, quality for small-icon exes | Icon crispness | M1 icon work |
| 9 | STJ behaviour for polymorphic items with unknown discriminator + extension data round-trip | Forward compatibility requirement | M1 config tests |
| 10 | Form of the non-intrusive launch-error message (tray balloon vs overlay) | Concept section 12 | M1/M4 |

---

## Sources

- Dwayne Need, "Transparent Windows in WPF" (Microsoft archive, 2008): layered window modes, alpha-based OS hit testing, WPF hit testing independent of transparency, dirty-rect updates, performance notes. HIGH for mechanism (stable platform behaviour), MEDIUM for performance numbers (old hardware). https://learn.microsoft.com/en-us/archive/blogs/dwayneneed/transparent-windows-in-wpf
- Seema Ramchandani, "Layered windows... SW is sometimes faster than HW" and related WPF layered-window notes (via search summary). MEDIUM. https://blogs.msdn.microsoft.com/seema/layered-windows-sw-is-sometimes-faster-than-hw
- Raymond Chen, "Old New Thing", 2011-06-17 (no way for a normal window to opt out of Show Desktop). HIGH. https://devblogs.microsoft.com/oldnewthing/20110617-00/?p=10403
- Rainmeter `Library/System.cpp` (desktop-state detection, helper window, `HWND_BOTTOM`, WinEvent hook, timers; read via fetch summary, not line-by-line). MEDIUM. https://raw.githubusercontent.com/rainmeter/rainmeter/master/Library/System.cpp
- ZposDesktop NuGet package (MIT; same technique as Rainmeter, third-party, low adoption). LOW-MEDIUM, reference only. https://www.nuget.org/packages/ZposDesktop
- Microsoft Learn, "Create a single file for application deployment" (native library extraction, API incompatibilities, `AppContext.BaseDirectory`, compression cost, R2R/trim combination). HIGH. https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- Microsoft Learn, "How to: Render on a Per Frame Interval Using CompositionTarget" and RenderingEventArgs reference (Rendering event semantics, `RenderingTime` = next frame time, layout in handler, static event) via search summaries. MEDIUM-HIGH. https://learn.microsoft.com/vi-vn/dotnet/desktop/wpf/graphics-multimedia/how-to-render-on-a-per-frame-interval-using-compositiontarget
- Hanselman, "Learning WPF with BabySmash - Making a Transparent Window with Substance" and a Stack Overflow thread on `#01000000` reliability (via search summary). LOW-MEDIUM, corroborates the alpha-1 plate technique and its caveat. https://www.hanselman.com/blog/LearningWPFWithBabySmashMakingATransparentWindowWithSubstance.aspx
- Project documents: `.planning/PROJECT.md`, `docs/PROJEKT_START.md` (sections 4-7, 10, Q6/Q7/Q10/Q12 and the reference measurement table). Primary for all constraints and numbers.

---
*Architecture research for: RocketDock-style launcher dock (MehrBar), WPF / .NET 10*
*Researched: 2026-10-09*
