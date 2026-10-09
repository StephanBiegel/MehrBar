# Pitfalls Research

**Domain:** RocketDock-style Windows application launcher dock (C# / .NET 10 / WPF, layered transparent window, bottom z-layer, zoom animation)
**Researched:** 2026-10-09
**Confidence:** MEDIUM overall. HIGH where a primary source was read in this session (Dwayne Need's WPF layered-window post, Raymond Chen on Show Desktop, the Microsoft Q&A thread on Win+D, Rainmeter's `System.cpp`, and the project's own measurement table). MEDIUM where it rests on API documentation plus well-known behaviour. LOW where it is domain knowledge that must be proven by a spike. Each pitfall carries its own tag.

**Milestone references** use the concept's section 10: M1 Grundgerüst, M2 Zoom, M3 Skin, M4 Drag&Drop/Menüs, M5 Konfigfenster, M6 Feinschliff. "Spike" means a throwaway experiment of half a day to a day, run before or at the very start of the named milestone.

---

## HIGH-RISK items that can invalidate a concept decision

These need an early spike (call them S1 to S4). They should run before M1 is planned in detail, because their outcome changes M1's content.

| ID | Concept decision at risk | Why | Spike |
|----|--------------------------|-----|-------|
| S1 | Q9/Q10 "layer: bottom, survives Win+D" | The bar sits behind every window, so Win+D (or the show-desktop corner) is the ONLY way the developer ever sees it. A bottom window is covered when Windows raises the desktop window, and no `WM_WINDOWPOSCHANGING` arrives for that. The robustness work is currently in M6, which is far too late. | Layer spike (Pitfall 1) |
| S2 | Section 3 "AllowsTransparency" + section 6 "60 fps with 60+ icons" | WPF per-pixel-alpha windows are presented through a GPU-to-CPU readback plus `UpdateLayeredWindow`. Cost grows with window area. No published measurement exists for .NET 10. | Transparency/perf spike (Pitfall 3) |
| S3 | Q12 "zoom anchor = center, confirmed by measurement" | The project's own five-state measurement table fits a cursor-anchored push model with zero residual and does NOT fit a fixed-centre model. Core Value is "feels like RocketDock". | Math check in Core, no UI needed (Pitfall 4) |
| S4 | Q4/Q5 "fill the bar only by Drag&Drop from Explorer" + bottom layer | An Explorer window must be open to drag from, and it will usually cover the bar. A bar nobody can fill comfortably is a usability hole. | UX decision, not a spike (Pitfall 6) |

Also spike before M1 closes: S5 interaction (non-activating window + context menu + launching), and S6 icon pipeline (flags, alpha, latency). See Pitfalls 5 and 7.

---

## Critical Pitfalls

### Pitfall 1: "Always at bottom" does not survive Win+D, and `HWND_BOTTOM` is not "just above the desktop"

**Confidence:** HIGH for the mechanism (Raymond Chen, Microsoft Q&A thread, Rainmeter source). LOW for exact behaviour on Windows 11 25H2 (build 10.0.26200): must be tested, nobody documents it.

**What goes wrong:**
- Win+D (and the taskbar's show-desktop corner) first minimizes every window that has `WS_MINIMIZEBOX`, then moves the desktop window to the top of the z-order. A bar at the bottom of the z-order is then covered by the desktop and vanishes, exactly when the user wants it. `WS_EX_TOOLWINDOW` and `WS_EX_NOACTIVATE` do not protect against this.
- The tempting fix, handling `WM_WINDOWPOSCHANGING` and forcing `hwndInsertAfter = HWND_BOTTOM`, does not help: in the Microsoft Q&A thread a Spy++ trace showed the widget receives no `WM_WINDOWPOSCHANGING` at all during Show Desktop, because Windows moves the desktop, not the widget.
- `HWND_BOTTOM` puts the window below every other top-level window, which may include the desktop host (Progman/WorkerW). The working recipe in the Qt thread is the opposite: walk the z-order, find the lowest visible window above the full-screen desktop window, and set `hwndInsertAfter` to that window, so the bar sits directly above the desktop. Whether plain `HWND_BOTTOM` also works on current Windows 11 is not documented and must be tested.
- The shell window structure changed in Windows 11 24H2. Rainmeter's source now locates the "desktop icons host" as `WorkerW` before 24H2 and as `Progman` from 24H2 on. Code copied from old WorkerW/0x052C tutorials (including SetParent-into-WorkerW wallpaper tricks) is likely broken on the developer's own machine (build 26200).
- Explorer restarts (crash, `taskkill`, theme change) recreate the desktop host with new handles. Cached handles go stale.

**Why it happens:**
The z-order mechanics of the shell desktop are undocumented, and most blog posts are written for Windows 7/10. Developers test "sits behind Notepad" and stop before testing Win+D.

**How to avoid:**
1. Do not build the layer as "SetWindowPos once". Build it as a small `ZOrderKeeper` in `App/Interop` with three parts, modelled on Rainmeter (the best open reference):
   - Desktop-host lookup by structure at runtime (find the window owning `SHELLDLL_DefView`; handle both pre-24H2 `WorkerW` and 24H2+ `Progman`), never cached across Explorer restarts.
   - Placement: insert directly above the desktop host, or `HWND_BOTTOM`, whichever S1 shows correct on 25H2. Reapply on `WM_WINDOWPOSCHANGING` (rewrite `hwndInsertAfter`, clear `SWP_NOZORDER`) and after display changes.
   - Show-Desktop detection: a `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` hook (out-of-context, same thread) plus a slow fallback timer (Rainmeter polls at 250 ms, 100 ms while the desktop is shown). While "desktop shown" is detected, set the bar `HWND_TOPMOST`; when a normal window becomes foreground again, drop topmost and re-place at the bottom.
2. Add a safety net for the failure mode "stuck topmost": periodic check that if a non-desktop window is foreground and we are still `WS_EX_TOPMOST`, revert. A dock in front of all windows is a bug the developer will notice instantly.
3. Re-run everything on `TaskbarCreated` (registered window message) and on `WM_DISPLAYCHANGE`.
4. Alternatives, in order of preference if the above fails in S1: (a) the undocumented `WM_USER+83` hook injected into Explorer: reliable for every trigger but fragile across builds and invasive, so only as a last resort; (b) parenting the bar into the desktop host: fixes Win+D by construction but ties input queues with Explorer (a hung dock stalls the desktop), needs layered-child support (manifest must declare Windows 8+ compatibility), WPF's `Window` does not expect `WS_CHILD`, and the 24H2 hierarchy change makes it fragile. Not recommended as first choice.
5. The tray "Leiste ein-/ausblenden" is not a fix. Keep it as the emergency exit but do not count it toward Win+D.

**Warning signs:**
- Bar visible behind Notepad but gone after Win+D, or gone after the taskbar corner click, or gone after `Win+,` (Peek).
- Bar appears in front of windows after you restore them.
- Bar missing after `taskkill /f /im explorer.exe` and restart.
- Code contains only a single `SetWindowPos(HWND_BOTTOM)` call.

**Phase to address:** Spike S1 before M1. Implement `ZOrderKeeper` in M1 (move the Win+D / Explorer-restart acceptance test out of M6 into M1; the developer cannot see the bar otherwise). M6 only re-verifies after display changes.

**Test matrix for S1:** Win+D, taskbar corner, `Win+M`, Peek, maximize/restore windows over the bar, Alt+Tab (bar must not appear), virtual-desktop switch (see Pitfall 11), Explorer kill/restart, lock/unlock, display change.

---

### Pitfall 2: Transparent regions: the OS decides hit-testing from pixels, so hover breaks in gaps and the zoom flickers

**Confidence:** HIGH (Dwayne Need's WPF post, written by the WPF team; reconfirmed by multiple forum reports).

**What goes wrong:**
- The concept's open question is answered: yes, fully transparent pixels (alpha 0) of a WPF `AllowsTransparency` window pass mouse input to whatever is underneath. The OS raw input thread checks the layered bitmap's alpha; if the pixel is 100 % transparent it skips the window. WPF never sees the event. A brush of `Transparent` and a null brush are indistinguishable to Windows (both alpha 0), so WPF's own "Transparent is solid" hit-test rule does not help for the OS pass.
- Consequence for the dock: when the cursor is over a transparent pixel inside the dock's logical area (the gap between two zoomed icons, the overhang area below the background bar, rounded icon corners, a label's empty space), Windows routes the mouse to the desktop. The dock gets mouse-leave, collapses the zoom, the icon shrinks back under the cursor, the cursor is over opaque pixels again, the dock re-enters: flicker/oscillation. This is the single most common dock bug in this class of app.
- Conversely, a pixel with alpha 1 (of 255) captures the mouse. A skin with a nearly transparent drop shadow ("Outside-Margins" 20/13/23/10 in CrystalXP) will swallow clicks that the acceptance criterion says must reach the desktop.
- `WS_EX_TRANSPARENT` toggling is not needed and is a trap: it makes the whole window, including icons, click-through; toggling it per frame races with input and with WPF's own style handling. `SetWindowRgn` per frame is also a trap (expensive, flickers).

**Why it happens:**
Developers assume WPF hit-testing governs input; it only governs routing inside the HWND after the OS has already chosen the HWND.

**How to avoid:**
1. Decide the hit region explicitly and draw it: the "hot zone" is a Core-computed rectangle (resting bar rect plus, while zoomed, the current zoomed bar rect plus overhang). Paint it with `#01000000` (alpha 1) so the OS keeps delivering mouse messages across gaps. Everything outside the hot zone stays alpha 0 and is click-through.
2. Keep the hot zone tight at rest (just the visible bar). Grow it only while zoomed. This satisfies "clicks next to the visible bar reach the desktop" at rest, and accepts that the overhang under a zoomed icon is captured while zooming. Document that trade-off in the acceptance test.
3. Do not rely on mouse-leave alone. While zoomed, the animation loop already runs every frame: read `GetCursorPos`, test it against the Core hot-zone rect, and start the zoom-out from that. This is independent of OS pixel routing and cannot flicker.
4. Add a small hysteresis (a few px margin, or a 30 to 60 ms delay on leave) so a one-frame transient does not cancel the zoom.
5. Skin pixels: when drawing skin margins, clamp the drawn alpha of shadow regions outside the hot zone to 0 for hit purposes, or accept the capture. Make this an explicit decision in `docs/skin-format.md`.
6. Never toggle `WS_EX_TRANSPARENT` or reapply regions for this.

**Warning signs:**
- Zoom "stutters" when the cursor is in the lower part of the zoomed icons or between icons.
- Right-click on desktop just below the bar opens the dock's menu (or the opposite).
- `MouseLeave` fires while the cursor visibly still touches an icon.

**Phase to address:** Spike S2 (verify alpha-0 pass-through and alpha-1 capture on build 26200). Implement in M2 ("Klick-Durchlass" already listed there). Add a Core unit test for the hot-zone rect.

---

### Pitfall 3: `AllowsTransparency` performance: readback per frame, cost scales with window area (HIGH RISK, spike S2)

**Confidence:** HIGH for the mechanism (Dwayne Need). LOW for current numbers: all published figures are from the Vista/XP era (Vista: a full-screen constantly updating layered window "can consume as much as 30 % CPU"; XP: ~3 %), and no .NET 9/10-specific measurement was found. Treat it as unproven either way.

**What goes wrong:**
- WPF supports only the application-provided layered mode (`UpdateLayeredWindow`). It renders with DirectX, copies the surface to system memory, and hands a bitmap to `UpdateLayeredWindow` each update. The cost is proportional to the updated (dirty) area, plus a GPU-to-CPU readback, not just the number of icons. The concept sizes the window to the "maximally zoomed bar including labels", i.e. perhaps 1920 x 200 to 3840 x 300 px, repainted fully on every zoom frame (a moving zoom dirties the whole bar).
- Rough budget (my arithmetic, not a source): 1920 x 200 x 4 B is about 1.5 MB per frame, about 92 MB/s at 60 fps. Fine on modern PCIe. It becomes a risk at 4K width, on weak iGPUs, with remote desktop, or when WPF falls back to software rendering (then every icon is CPU-composited).
- Older MS measurements show hardware-accelerated layered windows can be slower than software for simple 2D, and resizing a layered window forced full redraws (about 8 fps vs 60). Therefore: never resize the layered window during zoom.
- A related recent report (Chinese-language blog posts, secondary) describes stutter and CPU spikes for `AllowsTransparency` windows that repaint a lot or contain many controls. Your custom-drawn single control avoids the "many controls" part.

**Why it happens:**
`AllowsTransparency="True"` is a one-line switch that hides a completely different presentation path.

**How to avoid:**
1. Run spike S2 first: 1920 x 220 and 3840 x 300 windows, 64 icons from 256 px bitmaps drawn in `OnRender`, scripted sinusoidal "mouse", measure with PresentMon or the WPF perf tools: frame time, CPU %, GPU %. Compare hardware vs `RenderOptions.ProcessRenderMode = SoftwareOnly`. Go/no-go for "60 fps" is a real number, not a feeling.
2. Make the window area as small as the design permits: no full-width label strip (draw the label inside the hot zone only, size the window height to bar + max zoom + label), and keep the window a fixed size (no per-frame resize).
3. Keep the render path trivially cheap: pre-sized frozen bitmaps, no effects (`DropShadowEffect` forces software or offscreen passes), no `Opacity` groups, no per-frame allocations, cached label geometry.
4. Put the renderer behind an interface in `App/Bars` (`IBarSurface`). The concept already isolates layout in Core; keeping the surface swappable makes the escape routes below survivable.
5. Fallback ladder if S2 fails, cheapest first:
   - Reduce dirty area (split into bar window + separate small label window).
   - Non-layered WPF window with `WindowChrome` glass (`GlassFrameThickness = -1`, `CompositionTarget.BackgroundColor = Transparent`, `DwmExtendFrameIntoClientArea`): stays on the hardware path (a community article describes upgrading the render target from X8 to A8 this way). Caveat: such a window is NOT alpha-hit-tested, so click-through must be recreated with `WM_NCHITTEST` and `HTTRANSPARENT`, which only passes through to windows of the same thread; for desktop/other-process windows you need `SetWindowRgn` or a layered window. This likely reintroduces Pitfall 2 in reverse. Treat as plan B only after a hit-test spike.
   - Replace the bar window (only) with a raw Win32 layered window rendered by Direct2D/DirectComposition (`WS_EX_NOREDIRECTIONBITMAP`); keep WPF for settings/dialogs. Large effort: decide this at S2, not at M6.

**Warning signs:**
- Frame time above 12 ms or CPU above 10 to 15 % of one core during zoom on the developer's machine.
- Visible tearing/stutter when many icons overlap; worse after RDP or on a laptop iGPU.
- Idle CPU not zero (a stuck `CompositionTarget.Rendering` subscription, see Pitfall 9).

**Phase to address:** Spike S2 before M1 planning is finalized (it decides the rendering technology). Re-measure at the end of M2 with 60 and 100 icons (M6 already lists 100+ icons).

---

### Pitfall 4: The zoom anchor decision (Q12 "center") is contradicted by the project's own measurements

**Confidence:** HIGH for the arithmetic (it comes from the table in `docs/PROJEKT_START.md`); MEDIUM for the interpretation (five states, one dock, rounded pixel values). This is the most important finding for the Core Value.

**What goes wrong:**
Q12's follow-up says the measurement "confirms variant A, the centre stays fixed (±40 px)". But the ±40 px is not noise; it follows a pattern. Convert the table to edge growth relative to the resting state (L 109, R 698):

| State | Left edge growth | Right edge growth | Centre shift |
|---|---|---|---|
| Mouse centre | -112 | +114 | +0 |
| Mouse on 2nd icon (left) | -82 | +114 | +16 |
| Mouse on right icon | -112 | +106 | -4 |
| Mouse on the last icon (far right) | -112 | +35 | -39 |

Under a true fixed-centre model, left and right growth must be equal in every state. They are not. What fits all four rows is a push model: the point under the cursor stays where it is (in resting coordinates), icons to its left are pushed left by the accumulated extra width of the zoom kernel on that side, icons to its right are pushed right by the accumulated extra on that side. When the cursor is far from an end, that side gets the full half-kernel (about 112 px), so the left edge sits at the same -3 in three different states while the right edge varies from 812 to 733. At the right end the kernel is truncated and the right side only grows 35. Centre shift = (right growth - left growth) / 2 reproduces +16 and -39 almost exactly (observed +16 and -39).

The cosine kernel with radius 2.5 also checks out against the data: f(1) = (cos(pi/2.5)+1)/2 = 0.654 gives 32 + 89 x 0.654 = 90 px (measured 90 to 95); f(2) = 0.095 gives 40 px (measured about 40); total extra width at the centre = 89 x 2.5 = 222 px (measured 226). So the radius and falloff in the concept are right; the anchor is the thing that is off.

If M2 implements `center` as written, the bar ends will look wrong exactly at the places the developer will notice (hovering the first/last icons: the bar slides 40 px differently than the original), and the "feels like RocketDock" Core Value is not met. The concept also lists "icon under cursor drifts away from cursor" as a risk of A. Under the push model that risk disappears by construction.

**Why it happens:**
The measurement was read for "does the centre stay roughly in place" instead of "do left and right growth match". Three states share the same left edge, which is the tell.

**How to avoid:**
1. Before writing the layout code, add a Core test that encodes the four growth rows above (tolerance about 6 px) and run both candidate anchors against it. Use the real (gitignored) screenshots if numbers differ.
2. Implement the layout as: rest positions fixed; kernel evaluated on distances from the cursor to REST centres (never from zoomed positions); per-icon extra width e_i = size_i - base_i; cumulative extra left/right of the cursor; positions = rest position shifted by the cumulative extra on the side it lies on, normalised so the cursor's rest coordinate maps to itself (X(cursor) = cursor). That is `zoomAnchor = cursor` in the concept's parameter, and it is the one that matches the data. Keep `center` as the alternative parameter.
3. Interpolate the cursor input smoothly (zoom duration 200 ms) so edges do not jump when the cursor enters at the bar ends.
4. Keep separators unzoomed but pushed (concept is right).

**Warning signs:**
- At M2 calibration, bar edges move symmetrically while the screenshots show asymmetric growth when the cursor is near an end.
- The icon under the cursor visibly slides away from the cursor near the ends.

**Phase to address:** S3 (an afternoon of arithmetic and a test) before M2 starts; re-open Q12 with the developer. Implementation and calibration in M2.

---

### Pitfall 5: Non-activating window: context menus, outside-click dismissal, and launched apps not coming to the front

**Confidence:** MEDIUM (documented Win32 rules; WPF-specific behaviour on a `WS_EX_NOACTIVATE` window is not documented, so a spike is needed).

**What goes wrong:**
- `WS_EX_NOACTIVATE` means the dock never becomes the foreground window, which is what the concept wants. But popup menus (WPF `ContextMenu` or native `TrackPopupMenu`) rely on the owner being foreground to know when to close: the classic native rule is "call `SetForegroundWindow` on the owner before `TrackPopupMenu`, or the menu will not dismiss on an outside click". On a non-activating owner, a WPF `ContextMenu` can stay open after clicking another app, ignore Esc, or not react to the keyboard.
- Fix for the menu is to activate a hidden helper (not the dock) around the menu. That makes the helper the foreground window; when the menu closes the user's previous app has lost focus, and keyboard input goes nowhere until they click. Save `GetForegroundWindow()` before and restore it after.
- Launching: a click on a non-activating dock still counts as input, but the dock is not the foreground process, so Windows' foreground-lock rules may let the launched app open behind other windows (taskbar flash instead of coming to front). This is untested; do not assume.
- Dialogs opened from the menu ("Eigenschaften", "Konfiguration") are normal activatable windows and are fine, but they must not be owned by the non-activating bar window (ownership ties z-order/activation to it).
- `Process.Start` with `UseShellExecute = false` cannot launch executables whose manifest requires elevation (error 740) and cannot resolve URLs or folders as the shell does.

**How to avoid:**
1. Spike S5 early (M1): WPF `ContextMenu` from the bar; open it, click another app, press Esc, use arrow keys; launch Notepad via a click and check that it comes to the front.
2. Use a hidden, tiny helper owner (`WS_EX_TOOLWINDOW`) for menus and dialogs; save and restore the previous foreground window.
3. Call `AllowSetForegroundWindow(ASFW_ANY)` before launching, or test whether it is even needed.
4. Always launch via `UseShellExecute = true` (or `ShellExecuteEx`); for "Als Administrator starten" use verb `runas`; catch `Win32Exception` with code 1223 (user cancelled the UAC prompt) and show nothing alarming.
5. For `.lnk` entries launch the `.lnk` itself (the concept already stores the `.lnk` path), so arguments and working directory come from the shortcut. Set `WorkingDirectory` to the target's folder only when launching raw files.

**Warning signs:**
- Context menu survives a click on the desktop.
- After using the menu, typing goes nowhere until a click.
- Programs launched from the dock open behind the current window.

**Phase to address:** Spike S5 at the start of M1; menus fully in M4 but the helper-owner pattern must be decided in M1. Revisit in M6.

---

### Pitfall 6: The bar sits behind every window, but the only way to fill it is dragging from an Explorer window

**Confidence:** HIGH that the tension exists (follows directly from Q4, Q5, Q9, Q10); the right resolution is a product decision.

**What goes wrong:**
To drag a file onto the bar, the Explorer window must be open, and an Explorer window almost always covers a bar at the top of the screen. In bottom layer the bar is hidden behind it. The developer would need to move Explorer out of the way every time. A bar nobody wants to fill gets abandoned. Win+D does not help while dragging, as there's no Explorer window left.

OLE details that also go wrong:
- Layered window drop hit-testing uses the same alpha pixel rule (Pitfall 2): the drop only reaches alpha > 0 pixels.
- During the OLE modal drag loop `WM_MOUSEMOVE` is not delivered to the target; hover feedback (zoom, insertion marker) must be driven from `DragEnter/DragOver/DragLeave` using screen coordinates, not from normal mouse events, or the zoom freezes or never leaves.
- You must set `e.Effects` (use `Link`, not `Copy`) in `DragEnter` and `DragOver`, or the cursor shows "not allowed".
- Formats to accept, in priority order: `FileDrop` (Explorer, `.lnk`, folders), `UniformResourceLocatorW` (browsers, links, favicons), plain `Text`/`UnicodeText` that parses as `http(s)://`, and `Shell IDList Array` for virtual items without a file path (Store apps, Start menu entries). Dragging a site icon from a browser address bar can deliver `FileGroupDescriptorW` plus `FileContents` (a virtual `.url` file); WPF's `DataObject.GetData("FileContents")` handles only the single-stream case, so multi-item virtual files need the raw COM `IDataObject`.
- Dropping from an elevated Explorer or onto an elevated MehrBar is blocked by UIPI (Pitfall 10).

**How to avoid:**
1. Decide now, at roadmap level, how the developer fills the bar given bottom layer. Cheapest options that keep the concept intact: a tray menu item "Leiste vorübergehend nach vorne" (temporary topmost, auto-revert on drop or after N seconds); and/or an "Eintrag hinzufügen..." file picker plus a "Pfad einfügen" clipboard path in the bar's context menu. The concept's "Hervorhol-Kürzel nicht in V1" means this is a deliberate scope exception to raise with the developer. A development-only `layer: normal` override is needed anyway for debugging.
2. Build drag hover on `DragOver` coordinates through the same Core hot-zone logic as normal hover.
3. M4's multi-format parsing: put it in Core as a pure function `ParseDropData(IDataObjectView)` so it is testable without UI.
4. Test the matrix: Explorer file, folder, `.lnk`, `.url`, Start-menu app (Win 11 "All apps" drag often disabled), browser link from page, browser tab URL, Chrome vs Edge vs Firefox.

**Warning signs:**
- The developer, while testing M1's "Hineinziehen", needs to rearrange windows every time.
- Dropping works from the desktop but not from an open Explorer window.
- The cursor shows the not-allowed sign over the bar while the layer is fine.

**Phase to address:** Product decision in roadmap creation; implementation of the temporary-front toggle in M1 (needed to test D&D at all); formats in M1 (FileDrop) and M4 (the rest).

---

### Pitfall 7: Shell icons: blurry upscales, lost alpha, shortcut arrows, hung paths, and startup latency

**Confidence:** MEDIUM. The API facts (`SHIL_JUMBO` = 256, `GetImage` returns what the source provides) are documented. The `.lnk` overlay and alpha-conversion problems are known community issues; the search could not confirm either from a primary source, so S6 must verify them on build 26200.

**What goes wrong:**
- Source ceiling: `SHIL_JUMBO` is 256 px, but if the executable only embeds 32/48 px images the shell scales up, and no API can create detail that does not exist. Zoomed to 121 px these icons look soft. RocketDock has the same limit; set expectations and check that your pipeline is not the cause (for example, requesting 48 px and stretching it).
- `GetImage` defaults to thumbnails for many file types (images, PDFs get a content thumbnail with a border). Use `SIIGBF_ICONONLY`. `SIIGBF_RESIZETOFIT` only shrinks. `SIIGBF_BIGGERSIZEOK` lets the shell return a different size than requested, so always read the real returned bitmap size.
- HBITMAP to `BitmapSource`: `Imaging.CreateBitmapSourceFromHBitmap` can lose or mishandle the alpha channel of the 32-bit bitmap returned by the shell, giving black backgrounds. Read the DIB bits yourself (`GetObject`/`GetDIBits`, top-down 32 bpp) and build a `BitmapSource` with the right format (premultiplied vs straight alpha matters; verify), then `Freeze()`.
- Shortcut overlays: icons requested for `.lnk` items may include the system shortcut arrow overlay (unverified for `GetImage`; verify). If so, resolve the shortcut (`IShellLink` icon location/target) and request the icon of the target, or request the target's parse name, rather than the `.lnk`.
- Store/UWP app shortcuts have no filesystem target (the `.lnk` holds only a shell item ID list). Reading `IShellLink.GetPath` returns empty. The concept's decision to store the `.lnk` path for launching is correct; for the icon use `SHCreateItemFromParsingName` on the `.lnk` itself, or on `shell:AppsFolder\<AUMID>` where the AUMID is known. Whether the AppsFolder parsing name resolves for every app is unverified.
- Threading: shell objects need an STA. Using `IShellItemImageFactory` from thread-pool (MTA) threads fails or deadlocks. Use a dedicated STA worker (one or two), `CoInitializeEx(COINIT_APARTMENTTHREADED)`.
- Latency: a disconnected network drive, sleeping USB disk or slow antivirus makes a single icon take seconds. Extracting 60 icons synchronously on the UI thread kills the "start under 1 second" goal.
- Variable density: at 125/150 % Windows scaling, request pixel sizes (not DIPs) = DIPs x DPI scale.

**How to avoid:**
1. Show the bar immediately with placeholder icons; load icons on a STA worker; swap in as they complete. Never block the first frame on icons.
2. Persist a disk cache (`%APPDATA%\MehrBar\iconcache`, PNG, keyed by path + last-write time + size) so the second start loads decoded icons without touching the shell. Per-item timeout and a fallback icon for hung paths.
3. Request 256 px once, then keep a small mip chain (see Pitfall 8).
4. Spike S6 on this set: exe with only a 48 px icon, exe with a 256 px icon, `.lnk` (arrow?), Store-app `.lnk`, folder, `.url`, a PDF, a network path. Record returned size, alpha, latency cold and warm.
5. Keep all of this in `App/Interop` and give Core only `IIconSource` so the layout stays testable.

**Warning signs:**
- Black or white boxes behind icons.
- Icons of images/PDFs look like document thumbnails.
- Startup time grows with the number of entries or hangs with a missing drive.
- Thread-apartment exceptions in the log.

**Phase to address:** Spike S6 before M1; pipeline in M1 (the concept already puts hi-res shell icons there); cache and timeouts in M6 at the latest, but the async design must be in M1.

---

### Pitfall 8: Downscaled 256 px icons shimmer, pixel snapping and 9-slice seams

**Confidence:** MEDIUM (standard WPF rendering behaviour; the exact visual result must be judged by eye).

**What goes wrong:**
- Concept section 6 says "load icons once at the highest resolution and only draw them scaled". At rest the icons are 32 px, an 8x downscale of a 256 px source. WPF's bilinear/`HighQuality` scaling of such large ratios aliases and shimmers, especially during animation. Drawing at fractional positions makes icons soft or "swim" by a subpixel.
- 9-slice backgrounds drawn with several `DrawImage` calls (or `ImageBrush` with viewboxes) at non-integer rectangles show hairline seams between slices, or edge bleed from bilinear sampling beyond the slice. The effect gets worse at 125/150 % DPI because DIP to pixel conversion produces fractional edges.
- Layout rounding (`UseLayoutRounding`) applies to the element tree; your custom `OnRender` drawing is in your hands.

**How to avoid:**
1. Keep a small mip chain per icon (for example 256, 128, 64, 48/32 px) pre-downscaled with a quality filter (WPF `TransformedBitmap` with Fant, or your own box/Lanczos on the STA/worker thread) and pick the next size above the target pixel size. At rest, draw at integer device-pixel size and integer position.
2. At the end of the zoom-out animation snap to exact integers; no resting fractional positions.
3. 9-slice: pre-cut slices into frozen `CroppedBitmap`s once per skin load, compute all slice rectangles in device pixels (`VisualTreeHelper.GetDpi`), snap them to integers, overlap by 1 px where slices meet, set `RenderOptions.SetBitmapScalingMode(NearestNeighbor)` for the fixed corner slices and `HighQuality`/`Linear` only for the stretched middle. Use `DrawingContext.PushGuidelineSet` for fixed edges where useful.
4. Verify visually against screenshots at 100 %, and sample 125/150 %.

**Warning signs:**
- Icons pulse or crawl while the bar settles.
- Thin light lines in the skin at certain widths while zooming (the bar width changes every frame, so seams appear only at some widths).
- Icons look softer than RocketDock's at rest.

**Phase to address:** Mip chain in M1 (icon cache design), 9-slice rules in M3, final polish in M6.

---

### Pitfall 9: Animation loop: `CompositionTarget.Rendering` details, GC pauses, and frame pacing on high-refresh displays

**Confidence:** HIGH for the event semantics (MS docs, references); MEDIUM for GC effect.

**What goes wrong:**
- `CompositionTarget.Rendering` is a static event. If you subscribe and forget to unsubscribe, the bar keeps rendering (breaking "no frames at rest") and leaks. Using lambdas makes `-=` a no-op.
- The event can fire more than once for the same frame; per-callback state advances run twice as fast. Frame count is not time.
- On a 120/144 Hz display the callback rate follows the display, so "zoom duration 200 ms" must be time-based, not "N frames". Use `RenderingEventArgs.RenderingTime` for `dt`, skip duplicate timestamps.
- Allocations in `OnRender` (new `Rect`/`Point` are structs and fine; new `FormattedText`, `Geometry`, `Pen`, `Brush`, lists) trigger gen0 GCs that appear as 5 to 20 ms hitches. Unfrozen Freezables get change-tracking overhead.
- `OnRender` + `InvalidateVisual` re-records the whole drawing every frame. With 60 icons this is cheap, but it must not trigger layout of other elements. Prefer one `DrawingVisual` (or a single `OnRender`) with no children in the layout tree.
- Label text with outline: building `FormattedText.BuildGeometry` every frame is expensive; cache the label geometry per item. Text on a per-pixel-alpha window is grayscale anti-aliased (no ClearType), so Tahoma 13 will look different from GDI RocketDock. `FontSize` in WPF is DIPs; RocketDock's 13 is a GDI size: check visually.

**How to avoid:**
1. One `ZoomAnimator` in Core: input (cursor x or null, `now`), output (per-item size/offset); time-based easing; pure and unit-testable. The App layer calls it from `Rendering`.
2. Subscribe on "first change", unsubscribe when the animation reaches its target (settled for 1 frame) and hot zone is empty. Use a named handler and one pairing method (`StartLoop`/`StopLoop`).
3. Freeze all brushes/pens/bitmaps; preallocate per-item cached label geometry; use `Span`/arrays reused between frames; run the loop in a `perf` test with `GC.CollectionCount(0)` before/after 10 s of zoom (expect 0 or near-0).
4. Measure idle: CPU 0 %, no `Rendering` callbacks, no timers (the 250 ms fallback timer from Pitfall 1 should be the only wakeup).

**Warning signs:**
- Idle CPU above 0.1 %, GPU "WPF" tag active in Task Manager while the mouse is away.
- Zoom takes 100 ms on a 144 Hz display and 200 ms on 60 Hz (or the reverse).
- Periodic micro-stutter in the first seconds of zoom (GC) that disappears after warm-up.

**Phase to address:** M2 (design `ZoomAnimator` and the loop lifecycle there); verify again in M6.

---

## Moderate Pitfalls

### Pitfall 10: Elevation, UAC, UIPI, autostart

**Confidence:** HIGH for UIPI (Microsoft/Helge Klein), MEDIUM for the rest.

**What goes wrong:**
- If MehrBar itself runs elevated (a manifest of `requireAdministrator`, or started from an elevated Visual Studio/terminal while developing), drag and drop from a non-elevated Explorer silently fails: the cursor looks normal but nothing arrives (UIPI blocks window messages from lower integrity levels). `ChangeWindowMessageFilterEx` workarounds exist but are incomplete (`WM_DROPFILES` alone is not enough, `WM_COPYGLOBALDATA` and sometimes more are needed) and weaken UIPI.
- "Als Administrator starten" for an item must not mean running the dock elevated. Use `runas` per launch.
- Autostart via `HKCU\...\Run` starts the process with the current directory set to `C:\Windows\System32`. Relative paths (`Skins\`, config) silently resolve wrong. Also Run-key apps can start before Explorer is fully up: desktop host and taskbar do not exist yet, the tray icon add fails, and `FindWindow` for the desktop host returns null.
- A scheduled task with "run with highest privileges" is the usual way to autostart elevated apps and would introduce the UIPI problem again. Do not.

**How to avoid:**
- Manifest `asInvoker`. Detect elevation at start (`WindowsIdentity`/token check) and log a warning plus a tray balloon: "Drag & drop from Explorer will not work when elevated".
- Always base paths on `Environment.ProcessPath` directory (or `AppContext.BaseDirectory`, see Pitfall 11), never on the current directory.
- `ZOrderKeeper` and tray must be retry-safe: if the desktop host is missing, retry with backoff and wait for `TaskbarCreated`.
- Autostart: `HKCU Run` entry with the quoted full exe path; verify the entry on each settings open.

**Phase to address:** M1 (manifest, base-path discipline), M5 ("Mit Windows starten"), M6 (test autostart on a real reboot).

---

### Pitfall 11: Self-contained single-file WPF: native extraction, startup time, trimming, and where "next to the exe" is

**Confidence:** MEDIUM (Microsoft docs; the interplay of WPF natives and startup time was not measured in the sources found).

**What goes wrong:**
- Single-file does not bundle native libraries by default; WPF depends on several (`PresentationNative`, `wpfgfx`, `D3DCompiler`, `vcruntime`, `PenImc`). With `IncludeNativeLibrariesForSelfExtract=true` these are extracted to a temp directory before the app starts: the first start (and starts after the cache is cleaned) pays an extraction cost and an antivirus scan, risking the "start under 1 second" goal. A pure single `.exe` is therefore in tension with fast start.
- `EnableCompressionInSingleFile` shrinks the exe but decompresses on every start; avoid it for the < 1 s goal (MEDIUM; confirm by measuring).
- Trimming and Native AOT are not supported for WPF: do not set `PublishTrimmed`.
- `Assembly.Location` is empty in single-file; `Directory.GetCurrentDirectory()` is unreliable (autostart: System32). `Skins\` "next to the exe" must be resolved via `Environment.ProcessPath`. With native self-extraction `AppContext.BaseDirectory` may point to the extraction directory; verify before relying on it.
- Self-contained WPF output is large (on the order of 100+ MB). Irrelevant for own use, but do not let it surprise a release later.
- ReadyToRun (`PublishReadyToRun=true`) is supported and reduces JIT time; it is the main lever for startup.

**How to avoid:**
1. In M1 do a real publish (`dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`) and measure time to first frame: cold (after reboot), warm, and with the extraction directory deleted. Keep a table in the repo.
2. Prefer: `PublishReadyToRun=true`, single file, native libraries extracted once per version to a stable location; or ship the native DLLs beside the exe (the concept already allows `Skins\` beside it) if extraction is slow. The "single .exe" requirement is satisfiable either way for the developer.
3. Never use Assembly.Location or CWD for resources; put a `PathProvider` in Core (injected) and a test for the autostart case.
4. Show the window before loading anything non-essential (icons, skin) to hit < 1 s to first paint.

**Phase to address:** M1 (first publish and measurement); M6 (release build re-measure).

---

### Pitfall 12: Per-Monitor DPI v2 with a layered WPF window

**Confidence:** MEDIUM (manifest rules documented; layered-window specifics from general knowledge, LOW). The developer's workstation is 100 %, so this is mainly a "design it right, sample test it" item.

**What goes wrong:**
- Without a manifest entry the process is not per-monitor aware; Windows bitmap-scales the whole bar (blurry, wrong sizes). Per-Monitor V2 is declared in `app.manifest` (`dpiAwareness` PerMonitorV2); do not assume the WPF default.
- `Window.Left/Top/Width/Height` are DIPs converted with the DPI of the monitor the window is currently on. If you position the bar onto a monitor with a different DPI, WPF may use the wrong scale for the first move. Do all placement in physical pixels with `SetWindowPos` in `Interop`.
- `WM_DPICHANGED` makes WPF resize the window to the suggested rectangle; for a bar whose size comes from Core, ignore the suggested rect, recompute layout, and re-request icons at the new pixel size.
- Fractional DPI (125 %, 175 %) turns "32 px" into 40 / 56 physical px; sizes like 121 become non-integers. Define sizes as DIPs in config and round once to device pixels in Core's layout.

**How to avoid:**
- Manifest in M1; `DpiScale` passed into Core layout as a parameter, not read from WPF inside Core (keeps it testable).
- Test 100 %, 125 %, 150 % (the concept already asks for a sample at 125/150); also test one DPI change while running (change scale in Settings without restarting the app).
- Treat multi-monitor mixed DPI as V2; keep the Interop physical-pixel from day one.

**Phase to address:** M1 (manifest, physical-pixel Interop), M3 (skin at 125/150), M6.

---

### Pitfall 13: Skin parsing and rendering assumptions

**Confidence:** MEDIUM (RocketDock skin format is only partly documented; concept already says to verify against the real skin).

**What goes wrong:**
- `Outside-*Margin` meaning is unclear; building on a guess causes a wrong hit region and misplaced icons. The concept already asks for `docs/skin-format.md` first. Keep that gate.
- INI parsing: old skins use Windows-1252, BOMs, case-variant keys, backslash or forward-slash paths, trailing spaces.
- PNG color profiles/gamma can shift colors; load with `BitmapCreateOptions.IgnoreColorProfile` and `BitmapCacheOption.OnLoad`, `Freeze()`.
- Skin images are 32-bit with alpha; premultiplication affects blend edges (halo on dark backgrounds). Compare with the screenshot.
- Rights: never commit third-party skins (already a project constraint); the test suite must not depend on `samples/skins/` being present (gitignored, absent in other clones): test the parser with a tiny synthetic skin in the repo, and test the fallback path explicitly.

**How to avoid:** synthetic test skin in `src/MehrBar.Tests`; fallback background verified; documented semantics before the 9-slice implementation.

**Phase to address:** M3.

---

### Pitfall 14: Explorer restart and the tray icon

**Confidence:** HIGH for `TaskbarCreated` (well-documented); MEDIUM for Windows 11 overflow behaviour.

**What goes wrong:**
- When Explorer restarts (crash, update, user action) it broadcasts the registered message `TaskbarCreated`; apps must re-add their tray icon with `Shell_NotifyIcon(NIM_ADD)`. Explorer also recreates the taskbar when monitor DPI changes, so the handler must be idempotent. A hand-rolled tray icon that misses this leaves the developer without the emergency exit that Q15 exists for. WinForms `NotifyIcon` handles it itself; a WPF-only approach needs a hook on the window's `HwndSource` and a registered message.
- On Windows 11 new tray icons often start in the overflow flyout. The "Notausgang" is then one click further away than the developer expects. Using a stable GUID for the icon helps Windows remember the pinned state (LOW; verify).
- Tray availability at autostart (Pitfall 10).

**How to avoid:** handle `TaskbarCreated` once in the Interop message sink and call a shared `RestoreShellIntegration()` that also re-applies the z-order keeper. Use the same hook for tray and layer.

**Phase to address:** M1 (tray and layer in the same milestone).

---

### Pitfall 15: Configuration: live saving, atomic replace, unknown fields, polymorphic types

**Confidence:** MEDIUM (System.Text.Json behaviour from knowledge).

**What goes wrong:**
- Q14 "every change live and saved" means a slider drag produces dozens of saves per second. Writing temp + replace + backup each time wears disk and races with antivirus locking the file.
- `File.Move` over an existing file throws; use `File.Replace` (atomic-ish on NTFS) or write temp then replace, with retry on `IOException` (AV/backup tools).
- "Unknown fields are preserved": `System.Text.Json` drops unknown properties unless you capture them with `[JsonExtensionData]` on every model type (including each polymorphic derived type). A newer version's unknown `type` such as `group` throws on deserialization unless unknown derived types are handled, so an older build would crash or wipe the file. The discriminator property normally has to come first in the JSON object; hand-edited files with a different order fail unless out-of-order metadata is enabled.

**How to avoid:** debounce saves (for example 500 ms after the last change plus on exit); retry on IO errors; keep original JSON text for unknown types (a fallback `UnknownItem` that holds a `JsonElement` and round-trips); property-order and unknown-field round-trip tests in `MehrBar.Tests`; backup rotation limited to the last good.

**Phase to address:** M1 (config), M5 (live save debounce).

---

## Minor Pitfalls

### Pitfall 16: Single instance
**What goes wrong:** A named `Mutex` check is fine, but a second launch (autostart plus manual start, or elevated vs not) must signal the first instance to open settings, not just exit silently, and the mutex name must be per-user (`Local\`).
**Prevention:** per-session mutex name; signal via a named event or window message; test starting a second copy from an elevated shell.

### Pitfall 17: Display change handling and virtual desktops
**What goes wrong:** `SystemEvents.DisplaySettingsChanged` leaks if not unhooked; it fires multiple times per change; position must be recomputed in physical pixels. Windows 11 virtual desktops: a normal window lives on one virtual desktop; the bar may vanish on other desktops. The documented `IVirtualDesktopManager` can only query/move windows, pinning requires undocumented COM interfaces that change per build (LOW; ask whether the developer uses virtual desktops).
**Prevention:** debounce display events (about 300 ms); keep Z-order keeper reapply on display change; defer pinning to V2 unless the developer needs it.

### Pitfall 18: Fullscreen apps and video
**What goes wrong:** Bottom layer already avoids covering games and video; but when the bar is temporarily topmost (Win+D handling, Pitfall 1) it must not appear over a fullscreen application (a stuck topmost is worst here).
**Prevention:** the safety check from Pitfall 1 plus not toggling topmost while a fullscreen window is foreground.

### Pitfall 19: Label measurement and outline
**What goes wrong:** Label width depends on text; recomputing `FormattedText` per hover causes allocation and jitter; very long names run off the window edge when the hovered icon is near the screen edge.
**Prevention:** cache per item; clamp label rect to the window; shorten with ellipsis.

### Pitfall 20: Mouse-down + drag threshold with a moving target
**What goes wrong:** As the cursor moves while pressing, icons move under it (push model), so a click can hit a neighbour or start an accidental drag.
**Prevention:** hit-test against the layout of the last rendered frame; latch the item at mouse-down; use `SystemParameters.MinimumHorizontalDragDistance`.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Single `SetWindowPos(HWND_BOTTOM)` for the layer | Works in the first demo | Disappears on Win+D, wrong after Explorer restart or 24H2 changes; M6 rewrite | Never as the final design; fine for the M1 walking skeleton if S1 is scheduled |
| Layout computed in WPF code-behind with `ItemsControl` | Fast to build | Breaks the 60 fps goal and the Core/App split; cannot be unit-tested | Never (project constraint) |
| Hard-coded horizontal axis in the layout | Saves an afternoon in V1 | The right-hand bar (the actual reason for the app) needs a rewrite | Never |
| Synchronous icon extraction at start | Simplest code | Startup grows with entries; hangs on a dead network path | Only in the very first spike |
| One 256 px bitmap scaled for all sizes | One code path | Shimmer and soft icons at rest | Only until the mip chain lands in M1 |
| Save on every settings change | Simplest live behaviour | Disk churn and AV-lock failures | Never; debounce |
| Copy WorkerW/0x052C wallpaper snippets | Looks like a solved problem | Stale on 24H2+ | Never without testing on build 26200 |
| Depend on `samples/skins` in tests | Real data | Tests fail on any clone without the gitignored folder | Never; use a synthetic skin |
| Run the dev session elevated | Avoids permission prompts | Hides UIPI drag-and-drop failure | Never for the dock process |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Desktop host (Progman/WorkerW) | Cache the handle; assume WorkerW | Find at runtime via `SHELLDLL_DefView`; refresh on `TaskbarCreated`; handle the 24H2 layout |
| Show Desktop | Hook `WM_WINDOWPOSCHANGING` only | `EVENT_SYSTEM_FOREGROUND` hook plus slow poll, toggle topmost while desktop is shown |
| Shell icons | `GetImage` on any thread, default flags, convert via `CreateBitmapSourceFromHBitmap` | STA worker, `SIIGBF_ICONONLY`, read DIB bits with alpha, check returned size |
| `.lnk` / Store apps | Resolve target path and launch that | Launch the `.lnk` itself; icon via shell item (parse the `.lnk` or AppsFolder AUMID) |
| Browser drag | Only handle `FileDrop` | Add `UniformResourceLocatorW`, text URL, IDList; set `DragDropEffects.Link` |
| Process launch | `Process.Start` with `UseShellExecute=false` | `UseShellExecute=true`; verb `runas`; catch error 1223 |
| Tray icon | Add once | Re-add on `TaskbarCreated`, idempotent |
| Autostart | Trust the current directory | Use `Environment.ProcessPath`; handle Explorer not ready |
| `CompositionTarget.Rendering` | Count frames, lambda subscription | Time-based, named handler, unsubscribe at rest |
| System.Text.Json | Assume unknown fields survive | `JsonExtensionData` + unknown-type fallback + round-trip tests |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Full-window dirty rect every zoom frame on a layered window | CPU/GPU load and stutter during zoom | Small fixed window, cheap render, measure in S2 | Wide windows (3840 px), weak GPU, remote desktop, software render |
| Per-frame allocation in `OnRender` | Periodic micro-stutters | Freeze, cache, reuse buffers; count gen0 GCs | After a few seconds of zooming, grows with icon count |
| Downscaling 256 px to 32 px every frame | Shimmer plus CPU | Mip chain pre-scaled | Always visible, worse at 100+ icons |
| Label outline geometry rebuilt per frame | Hitches when hovering | Cache per item | Long labels, fast mouse movement |
| Synchronous icon extraction | Slow or hung start | STA worker, placeholders, disk cache | Entry count > 30, any slow drive |
| Always-on animation subscription | Idle CPU not 0 | Subscribe on activity, unsubscribe at rest | Always (violates the idle goal) |
| Resize layered window per frame | Massive redraw cost | Fixed window size | Immediately |
| Extraction of native libraries every start | Slow start | Stable extraction dir or native DLLs beside the exe; R2R | Cold start, AV scanning |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Running the dock elevated | UIPI breaks D&D; every launched item inherits high integrity; the self-extract temp dir becomes an elevation target | `asInvoker`; elevate per item via `runas` only |
| `ChangeWindowMessageFilterEx` to make elevated D&D work | Weakens UIPI for the process | Do not run elevated in the first place |
| Expanding environment variables and executing config paths without validation | A tampered `config.json` launches arbitrary commands, but it is in the user's own profile, so risk is low | Treat the config as trusted-user data; no network sources; do not auto-run on config change |
| Loading fremde Skins/PNGs with full trust | Malformed image crashes decoder | Decode inside try/catch with fallback skin; keep WIC |
| Undocumented Explorer-injection hook (WM_USER+83) | Instability of the shell, AV detection | Last resort only, documented, behind a switch |
| Logging full paths and URLs | Privacy | Keep logs local, rotate |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Bar unreachable behind windows with no way to bring it forward | Looks "missing" | Tray emergency exit, Win+D support, temporary-front option |
| Zoom flicker over gaps | Feels broken | Pitfall 2 hot-zone design |
| Wrong anchor at bar ends | "Not like RocketDock" | Pitfall 4 push model checked against the table |
| Click-through only half works | Desktop right-click lands on the dock shadow | Tight hit region at rest |
| Launch error as modal box | Annoying | Short non-modal notice (concept already asks) |
| Blurry icons at rest | Looks cheap | Mip chain, pixel snapping |
| Tray icon hidden in overflow | No emergency exit visible | Tell the user once to pin it |
| Menu stays open / keyboard lost | Confusing | Helper owner and foreground restore |

## "Looks Done But Isn't" Checklist

- [ ] **Bottom layer:** Often missing Win+D, taskbar corner, Peek, Explorer restart, lock/unlock, display change. Verify each by hand on Windows 11 25H2.
- [ ] **No focus theft:** Often only tested with a click. Verify with typing in Notepad while clicking the bar, opening the context menu, and closing it (focus returns).
- [ ] **Click-through:** Often tested only far from the bar. Verify right-click on the desktop 1 px outside the bar, under the zoomed overhang, and over the skin's shadow margin.
- [ ] **Zoom:** Often tested only in the middle. Verify at both bar ends, rapid left-right sweeps, entering from the side, leaving diagonally, and against the four edge growth rows of the measurement table.
- [ ] **Idle:** Often shows 0 % CPU on average but wakes up. Verify no `Rendering` callbacks and no timers except the slow layer guard.
- [ ] **Icons:** Often tested with 5 apps. Verify 60+ entries, a missing path, a network path, a Store app, a `.lnk` (arrow?), cold start after reboot.
- [ ] **Drag & drop:** Often tested with one `.exe`. Verify folder, `.lnk`, `.url`, browser link, from an Explorer window that covers the bar, and elevated Explorer (should fail gracefully).
- [ ] **Single-file publish:** Often tested only via `dotnet run`. Verify the published exe after a reboot, with `Skins\` beside it, started from autostart (CWD = System32), start time < 1 s.
- [ ] **Autostart:** Verify after a real logon, Explorer not yet ready, tray icon present.
- [ ] **DPI:** Verify 125 % and 150 %, plus changing scale while running: no seams, no blur.
- [ ] **Config:** Verify a hand-edited file with an unknown `type`, unknown extra fields, reordered properties, and a locked file.
- [ ] **Fallback skin:** Verify the app starts without `Skins\` (a fresh clone).

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Layer logic wrong or fragile (1) | MEDIUM | Isolated in `ZOrderKeeper`; swap strategy (insert-above-desktop, foreground hook, topmost toggle, last resort Explorer hook) behind one interface |
| Hit-test/hover flicker (2) | LOW | Paint alpha-1 hot zone and use `GetCursorPos` leave test; Core change only |
| Layered WPF too slow (3) | HIGH | Renderer behind `IBarSurface`; fall back to smaller window, glass route, or Direct2D/DirectComposition bar window; Core layout reused |
| Wrong zoom anchor (4) | LOW if Core is clean | Anchor is a parameter; add the second implementation and the table test |
| Menu/focus problems (5) | LOW | Helper owner window, restore foreground |
| D&D unusable because bar hidden (6) | LOW | Temporary-front toggle, file picker |
| Icon pipeline slow/black (7) | MEDIUM | Switch to DIB read, STA worker, disk cache |
| Startup > 1 s (11) | MEDIUM | R2R, ship native DLLs beside exe, defer icons |
| DPI seams/blur (8, 12) | MEDIUM | Pixel snapping in Core layout, pre-cut slices |
| Config data lost (15) | LOW with backup | Restore `config.bak.json`; add round-trip tests |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| 1 Win+D / layer | Spike S1; M1 implement `ZOrderKeeper`; M6 recheck | Manual matrix on build 26200; scripted Explorer kill |
| 2 Transparent hit-testing | Spike S2; M2 | Right-click 1 px outside the bar reaches the desktop; zoom survives sweeps across gaps |
| 3 AllowsTransparency perf | Spike S2 (before M1 design freeze); M2 re-measure; M6 | PresentMon: frame time below 16 ms at 64 icons; documented numbers |
| 4 Zoom anchor | S3 before M2; M2 | Core test reproduces the four edge-growth rows within tolerance |
| 5 Non-activating menus/launch | Spike S5; M1 decide; M4 implement | Notepad keeps focus; menu closes on outside click; launched app comes to front |
| 6 Fill workflow / OLE | Roadmap decision; M1 (FileDrop + temporary front); M4 formats | Drop from covering Explorer window works; browser URL drop works |
| 7 Icons | Spike S6; M1 pipeline; M6 cache/timeouts | Test matrix of file types; cold start timing |
| 8 Mip chain, seams | M1 mip chain; M3 9-slice; M6 | Visual comparison at 100/125/150 % |
| 9 Animation loop | M2 | Zero idle callbacks; gen0 GC count in a 10 s zoom run |
| 10 Elevation/autostart | M1 manifest and paths; M5 autostart; M6 reboot test | Start from autostart with CWD = System32 works |
| 11 Single-file publish | M1 first publish; M6 | Time-to-first-frame table |
| 12 DPI v2 | M1 manifest/interop; M3; M6 | 125/150 % sample, live DPI change |
| 13 Skin parsing | M3 | Synthetic-skin unit tests; fallback test |
| 14 Explorer restart / tray | M1 | Kill Explorer: tray and layer both return |
| 15 Config | M1, M5 | Round-trip and debounce tests |
| 16 to 20 | M1 to M6 as noted | Checklist above |

**Suggested roadmap adjustments (for the synthesizer):**
1. Add a "Phase 0: Spikes" (S1 layer, S2 transparency/perf/hit-test, S3 zoom-anchor math, S5 interaction, S6 icons); each ends with a go/no-go note.
2. Move Win+D / Explorer-restart robustness from M6 to M1.
3. Re-open Q12 with the developer on the basis of Pitfall 4's table before M2.
4. Add a deliberate answer to Pitfall 6 (how the bar gets filled when it sits behind every window).
5. Move first publish and startup measurement into M1.

## Sources

HIGH (primary, read in this session):
- Dwayne Need (WPF team), "Transparent Windows in WPF": https://learn.microsoft.com/fr-fr/archive/blogs/dwayneneed/transparent-windows-in-wpf (layered window modes, hit testing of 100 % transparent pixels, readback performance, Vista/XP numbers, `PrintWindow` race)
- Raymond Chen, Show Desktop minimizes minimizable windows then raises the desktop: https://devblogs.microsoft.com/oldnewthing/20241021-00/?p=110393
- Microsoft Q&A, widget disappears on Win+D, no `WM_WINDOWPOSCHANGING`, `WM_USER+83` hook: https://learn.microsoft.com/en-us/answers/a/1887373
- Rainmeter `System.cpp` (desktop host detection WorkerW vs Progman from 24H2, foreground hook, 250 ms Show Desktop poll, topmost toggling): https://raw.githubusercontent.com/rainmeter/rainmeter/master/Library/System.cpp
- Qt mailing list, inserting a window just above the desktop via `WM_WINDOWPOSCHANGING`: https://lists.qt-project.org/pipermail/interest/2016-December/025300.html
- `docs/PROJEKT_START.md` measurement table (Q12 nachtrag) for Pitfall 4 (project's own data)

MEDIUM (documentation, secondary, or consistent community reports):
- Layered windows SW vs HW (WPF team, 2006): https://blogs.msdn.microsoft.com/seema/layered-windows-sw-is-sometimes-faster-than-hw
- `SHGetImageList` / `IShellItemImageFactory::GetImage`: https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishellitemimagefactory-getimage
- UIPI and drag and drop: https://learn.microsoft.com/pl-pl/archive/blogs/patricka/q-why-doesnt-drag-and-drop-work-when-my-application-is-running-elevated-a-mandatory-integrity-control-and-uipi and https://helgeklein.com/blog/how-to-enable-drag-and-drop-for-an-elevated-mfc-application-on-vistawindows-7/
- `CompositionTarget.Rendering` semantics: https://learn.microsoft.com/vi-vn/dotnet/desktop/wpf/graphics-multimedia/how-to-render-on-a-per-frame-interval-using-compositiontarget
- Single-file native libraries: https://learn.microsoft.com/en-us/answers/questions/990342/wpf-publishing-application-into-single-exe-file
- `TaskbarCreated` re-add pattern: https://classicvb.net/articles/qa200204.asp
- Glass/`WindowChrome` route (X8 to A8 render target, secondary): https://www.cnblogs.com/lindexi/p/22019848 ; glass frame docs: https://learn.microsoft.com/zh-tw/dotnet/desktop/wpf/graphics-multimedia/extend-glass-frame-into-a-wpf-application
- Windows 11 24H2 wallpaper-app breakage, Lively rewrite (confirms shell changes, not the mechanism): https://newreleases.io/project/github/lively-community/lively/release/v2.2.0.0

LOW (domain knowledge, no source confirmed in this session; each is tagged and has a spike): `GetImage` shortcut arrow overlay; HBITMAP alpha conversion pitfalls; WPF `ContextMenu` behaviour on `WS_EX_NOACTIVATE` windows; foreground-lock behaviour for apps launched from a non-activating window; per-monitor DPI specifics for layered WPF windows; virtual desktop behaviour; `EnableCompressionInSingleFile` start-up cost; `AppContext.BaseDirectory` with native self-extraction; `System.Text.Json` polymorphism edge cases.

Gaps: no .NET 9/10-era measurement of `AllowsTransparency` performance was found; the exact z-order behaviour of `HWND_BOTTOM` relative to the Windows 11 25H2 desktop host is undocumented. Both must be settled by spikes S1 and S2, not by further desk research.

---
*Pitfalls research for: RocketDock-style Windows launcher dock (WPF, .NET 10)*
*Researched: 2026-10-09*
