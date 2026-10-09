# Feature Research

**Domain:** RocketDock-style Windows launcher dock (magnifying icon bar, launch-only, no task management)
**Researched:** 2026-10-09
**Confidence:** MEDIUM overall. HIGH for facts taken from the developer's own machine (RocketDock 1.3.5 registry, PROJECT.md reference measurements). MEDIUM for RocketDock option semantics (vendor help text, mirrored copy). LOW for competitor details and for anything labelled "from experience" (the gsd confidence seam rates plain web search/fetch as LOW; every web-derived claim below is tagged accordingly).

**Status legend** (used in the "V1?" columns):
- `V1` = already in PROJECT.md Active scope
- `Deferred Qn` = deliberately deferred in grill question n (do not re-add)
- `GAP` = not in scope today, but the developer would miss it on day one or it silently breaks a V1 feature -> recommend adding
- `Not planned` = outside the product concept

---

## Feature Landscape

### Table Stakes (Users Expect These)

Missing any of these = the dock feels broken or "not like RocketDock".

| Feature | Why Expected | Complexity | V1? | Notes |
|---------|--------------|------------|-----|-------|
| Magnifying zoom with smooth falloff | The defining feature; Core Value | HIGH | V1 | See "Magnification: behavior details" below. Calibrate against `samples/reference/` |
| Zoom ramp in/out (ZoomTicks 200 ms) on enter/leave | Without it the dock "pops"; RocketDock ramps | MEDIUM | V1 | Global factor 0..1 over `zoomDurationMs`; reverse from the *current* factor on re-enter, never restart |
| Hover label for the icon under the cursor | RocketDock shows white Tahoma text with outline + shadow | MEDIUM | V1 (Q3) | Must be clamped to the monitor and sit inside the window; see label details |
| Single left click launches | Docks launch on single click, not double | LOW | V1 | Fire on mouse-UP on the same item that got mouse-DOWN; suppress after a drag |
| Launch program / file / .lnk / folder / URL via the shell | The only job of the app | LOW | V1 | `UseShellExecute=true` (handles verbs, App Paths, .lnk, URLs, folders). Resolve `%ENV%` in path/args/working dir |
| Launch with arguments + working directory | Developer's own RocketDock data uses both (e.g. `-I CommandLine.txt`, WorkingDirectory) | LOW | V1 | Items store icon source separately from launch command (RocketDock: `FileName` vs `Command`) - MehrBar's `icon` field already does this |
| Run as administrator | RocketDock-era request; in V1 menu | LOW | V1 (Q13) | `ShellExecute` verb `runas`; swallow `ERROR_CANCELLED` (1223) when the user declines UAC - not an error |
| Unobtrusive launch-error feedback (missing file, access denied) | A silent failure looks like a hung dock | LOW-MEDIUM | V1 (concept section 12, form open) | Recommendation under "Launch behavior" below |
| Drop files/folders/shortcuts from Explorer onto the bar | The *only* way to fill the bar (no import, Q4) | MEDIUM | V1 (Q5, milestone 1) | Accept as `DragDropEffects.Link` only, never Move/Copy (source file must never be touched) |
| Drop insertion at pointer position, icons slide apart | RocketDock opens a gap and inserts there | MEDIUM | V1 (Q5, milestone 4) | Zoom must stay live during OLE drag (see dependencies) |
| Reorder by dragging | Expected in every dock | MEDIUM | V1 (milestone 4) | Drag threshold = `SystemParameters.MinimumHorizontalDragDistance`; a plain click must still launch |
| Remove item (context menu) | Menu replaces RocketDock's drag-off-to-delete | LOW | V1 | |
| Separator item: no zoom, no label, no click action, fixed width, deletable | In the developer's own setup (items 6, 13+) and visible in screenshots | MEDIUM | V1 (milestone 3) | See "Separator handling" below |
| Item context menu: Start / Run as admin / Open file path / Properties / Remove | Matches RocketDock (Icon Settings, Delete) plus modern needs | LOW | V1 (Q13) | "Open file path" = `explorer /select,"path"` |
| Bar context menu: Insert separator / Open config / Quit | Right-click on dock must offer settings and exit | LOW | V1 (Q13) | |
| Item properties: name, target, arguments, working dir, custom icon | RocketDock "Icon Settings" has Name, Target, Start In, Arguments, Run, Open Running | LOW-MEDIUM | V1 (Q13) | "Run" (normal/min/max window state; present as `ShowCmd` in his registry, all 0) is not planned - see anti-features |
| High-resolution, sharp icons incl. during zoom | RocketDock max 128 px; MehrBar targets 256 | MEDIUM | V1 | Load once at max size, scale with high-quality filter; `IconQuality=1` in his registry = always high quality, no setting needed |
| Skin: 9-slice background with outside margins, separator image | The look; Core Value is "RocketDock with CrystalXP.net" | MEDIUM | V1 (Q2, Q14) | Semantics of `Outside-*Margin` must be reverse-engineered (docs/skin-format.md) |
| Position: edge, alignment, offset, edgeOffset (negative allowed) | RocketDock: Screen Position, Centering, Edge Offset | LOW | V1 (Q7) | |
| Layering "Always on Bottom" | His `zOrderMode=2`; Windows hides the dock under fullscreen apps for free | MEDIUM | V1 (Q9, Q10) | Win+D survival is a named acceptance criterion |
| Click-through for transparent window areas | A transparent oversized window must not block the desktop | MEDIUM-HIGH | V1 | Named open technical point in the concept |
| Settings window, live apply, autosave, reset to defaults, start with Windows | RocketDock has the same (Defaults button, Run at Startup) | MEDIUM | V1 (Q14) | |
| Tray icon as escape hatch | Dock sits in the background; needs a recovery path | LOW | V1 (Q15) | RocketDock instead has hotkey Ctrl+Alt+R (see differentiators) |
| Single instance | Two docks fighting = broken | LOW | V1 | Second launch should signal the first (open settings) rather than just exit - cheap polish |
| Config robustness: atomic write, backup, unknown fields preserved | Config corruption is fatal for a dock people fill by hand | LOW-MEDIUM | V1 | |
| Sensible default item names | RocketDock took the file name; developer's list shows `cmake-gui.exe`, `Affinity.exe` (ugly) and hand-edited names | LOW | GAP (minor) | Default for a dropped item: `.lnk` -> link base name; `.exe` -> `FileVersionInfo.FileDescription`/`ProductName`, fallback file stem without extension. Saves the rename trip in Properties |
| Fallback icon when target is missing / unreadable | Icons are extracted at load; moved/uninstalled apps must not crash or leave a hole | LOW | GAP (minor) | Generic "missing" icon, item stays, launch shows the error feedback |
| Icon for URL items | URL entries have no file to extract an icon from; the concept does not say what to show | LOW-MEDIUM | GAP | Default: default-browser / Internet-shortcut shell icon; custom icon via Properties. Do NOT fetch favicons (network) |
| Double-launch protection | Habit from desktop double-clicking; slow apps start twice | LOW | GAP | Ignore further clicks on the same item for ~700-1000 ms after a launch |
| Minimal press/launch acknowledgement | Without any feedback a slow app (3-5 s) looks like a missed click -> user clicks again | LOW | GAP | Cheap V1 stand-in for the deferred bounce: icon dims/shrinks on mouse-down, short pulse on launch. Full "hop" stays Deferred Q3 |
| Context menus that close when clicking away | Menus on a `WS_EX_NOACTIVATE` window often fail to dismiss | MEDIUM | GAP (technical) | Not a feature but a V1 acceptance blocker; needs `SetForegroundWindow` before popup + `WM_NULL` after (classic tray-menu fix). Flag for the Win32 phase |

### Differentiators (Competitive Advantage)

For MehrBar the only real differentiator is fidelity of the zoom feel plus being lightweight/stable on Windows 11. Everything else is parity.

| Feature | Value Proposition | Complexity | V1? | Notes |
|---------|-------------------|------------|-----|-------|
| Pixel-faithful RocketDock zoom (falloff, anchoring, timing) | The reason the app exists; RocketDock is abandoned (last build 1.3.5) and ages badly on Win11/high DPI | HIGH | V1 (Core Value) | Table stake for *this* project, differentiator vs. every other dock |
| Per-Monitor-DPI v2 + crisp 256 px icons | RocketDock is not DPI-aware (blurry at 125/150 %) | MEDIUM | V1 | |
| 60 fps with 60+ icons, zero idle CPU, <1 s start | Competing docks (Nexus, ObjectDock) are heavier; AlternativeTo users call RocketDock lightweight and ObjectDock not (LOW confidence, anecdotal) | MEDIUM | V1 | Custom `OnRender` control, no frames at rest |
| Axis-neutral layout (vertical bar on the right edge) | The actual reason for the project | MEDIUM | Architecture V1, UI Deferred Q6 | RocketDock itself supports left/right docks with rotated skins |
| `zoomAnchor` parameter (`center` / `cursor`) | Lets the developer tune feel after trying it (Q12) | LOW once layout is abstract | V1 | |
| Drop a file ONTO an existing app icon = open with that app | Real RocketDock behavior (docs: "dropping a file or folder onto an application item launches that app with the file"; folder item = copy into folder) | MEDIUM | Not planned / candidate | Conflicts with "drop = insert". Pick one rule for V1 (insert) and note the RocketDock behavior is intentionally absent; revisit if the developer misses it. Never implement "copy into folder item" (destructive surprise) |
| Zoom-style variants: "Bubble" (default) vs "Plateau" (flat peak) | RocketDock 1.3.0 added Plateau (LOW: changelog one-liner only) | LOW if falloff is a strategy | Deferred (not asked) | Do not build; keep falloff behind an interface so it is possible |
| Hotkey to toggle/reveal the bar (RocketDock: Ctrl+Alt+R, set in his registry) | With `layer: bottom` the bar is covered by every maximized window; a reveal key would be the natural fix | MEDIUM | Deferred (Q10 "kein Hervorhol-Kuerzel", concept V2 item 11) | Worth re-evaluating after real use: it is the main usability weakness of layer=bottom |
| Launch "hop"/bounce effect (his `IconActivationFX=2`) | Visual confirmation of launch | MEDIUM | Deferred Q3 ("Start-Effekt") | See GAP: minimal press feedback in V1 |
| Lock items (prevent accidental drag/reorder; Ctrl overrides) | RocketDock "Lock Items", cheap protection once reorder exists | LOW | Not planned (candidate) | His `LockIcons=0`; only add if accidental drags happen |
| Background / icon opacity, Zoom-Opaque | RocketDock options `ThemeOpacity`, `IconOpacity`, `ZoomOpaque` (his: 100/100/1 = effectively off) | LOW | Not planned | Values are the no-op defaults for him; skip |
| Autohide with delay/duration | RocketDock `AutoHide*` | MEDIUM | Deferred Q9 | His `AutoHide=0` |
| Layers normal/top, popup-on-mouseover | RocketDock layering options | LOW-MEDIUM | Deferred Q10 | |
| Groups / sub-docks / stacks | Winstep Nexus, ObjectDock sub-docks, macOS stacks | HIGH | Deferred Q8 | Polymorphic `type` keeps the door open |
| Multiple bars UI, multi-monitor, AppBar space reservation | ObjectDock/Nexus multi-dock | HIGH | Deferred Q1, Q7 | |
| Import RocketDock items | Migration convenience | LOW-MEDIUM | Deferred Q4 | Registry layout is simple (`N-Title`, `N-Command`, `N-Arguments`, `N-WorkingDirectory`, `N-FileName` = icon source, `N-IsSeparator`, `count`) if ever wanted |
| Portable mode (settings in INI next to exe) | RocketDock offers it | LOW | Deferred (V2 item 11) | |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Running-application indicators, minimize-to-dock, window previews | Standard in macOS Dock, ObjectDock, Nexus, RocketDock options | Turns a launcher into a taskbar replacement: window hooks, shell integration, huge pitfall surface. Explicitly out (PROJECT.md). His registry has `ShowRunning=0`, `ManageWindows=0`, `OpenRunning=0`, so he does not use them | Launch only |
| Docklets / widgets (clocks, CPU meters, RocketDock `DockletFile`) | RocketDock/ObjectDock ecosystem | Needs a plugin ABI (native DLLs, 32-bit), security and stability burden. Explicitly out | None |
| "Open running instance instead of launching again" | RocketDock `OpenRunning` | Requires process/window matching; he has it off | Double-launch debounce only |
| Dragging an item off the bar to delete it (no undo) | RocketDock/macOS behavior | One careless drag silently destroys hand-built config; his bar has 19 hand-picked items | Context-menu Remove; dropping outside the bar cancels the drag and the item returns |
| Dropping onto a folder item copies/moves the file into it; dropping onto Recycle-Bin item deletes | RocketDock drop targets | Destructive file operations from a launcher; also forces special "Recycle Bin"/system item types | Drop = always "add launcher" |
| Special system item types (Recycle Bin, Quit, Dock Settings as items) | RocketDock "Add Item" menu | Extra types with special rendering and state (full/empty bin) for little value | Tray menu has Settings/Quit; bar menu has them too |
| Favicon fetching for URL items | Pretty icons | Network access, caching, privacy, failure modes | Default browser icon + custom icon override |
| Per-bar percent scaling | Present in some docks | Double control next to icon size; decided out (Q11) | Per-Monitor DPI v2 |
| Skin editor / multiple built-in skins / skin downloader | Skins are RocketDock's community appeal | Large UI surface; licensing of third-party skins unresolved (Q2) | Load skins from `Skins\`; documentation only |
| Radial/circular launcher mode (the "Circle Dock" paradigm) | Different launcher metaphor (LOW confidence: not researched in depth) | Different interaction model, nothing shared with the zoom bar | Not applicable |
| Reflection, 3D tilt, icon rotation effects (ObjectDock/Nexus "live reflections") | Eye candy in commercial docks | Cost frame time at 60+ icons; breaks the 9-slice skin look | Faithful RocketDock look only |
| Per-item "Run: minimized/maximized" window state | RocketDock `ShowCmd`, all 0 in his data | Rarely useful; extra field in dialog/model | Use Arguments |
| Modal message boxes for launch errors | Easy to code | Steals focus (the bar is non-activating by design), blocks UI thread | Inline, non-modal feedback |

---

## Magnification: Behavior Details

Everything here serves the Core Value. Confidence note per item.

### Falloff curve (MEDIUM-HIGH, verified numerically against his measurements)

- Vendor help text: *"Zoom Width: the number of icons that change size during a zoom"* (LOW per seam, but consistent with the measurements below).
- The PROJEKT_START formula `f = (cos(pi * d / r) + 1) / 2` for `d < r`, else 0, with `r = 2.5` for `ZoomWidth = 4`, was checked here against the reference table:
  - `iconSize 32`, `zoomSize 121` (delta 89 px). `d = 0 -> 121 px`; `d = 1 -> f = 0.654 -> 90 px`; `d = 2 -> f = 0.095 -> 40.5 px`. Matches "gezoomt 121, Nachbarn ~90-95, uebernaechste ~40".
  - Summed growth with the cursor on a centre icon: 89 + 2*58.2 + 2*8.5 = ~222 px vs. measured bar growth 815 - 589 = 226 px (1.6 % off). Mouse on 2nd icon: predicted ~214 vs measured 196; far-right icon: predicted ~156 vs measured 147. Deviations of 6-8 % at the ends are explained by the cursor not sitting exactly on the icon centre in the screenshots. Conclusion: cosine hump with r = 2.5 is a good model; no need for gaussian.
- Hypothesis to resolve in the calibration phase: how `r` derives from `zoomWidth`. Candidates: `r = (zoomWidth + 1) / 2` (gives 2.5 for 4, and "4 icons change size" for a cursor between two icons) or `r = 0.625 * zoomWidth`. One data point cannot separate them. **Cheap fix:** set RocketDock's Zoom Width to 2, 6 and 8 and screenshot once each; the candidates diverge immediately.
- The unit of `d` is the rest *pitch* (icon width + spacing), measured between rest centres. Separators have a different pitch; using px distance divided by base pitch handles that naturally.
- The cursor position is compared with REST centres (Q12 follow-up). Never feed zoomed positions back into the falloff (runaway oscillation).
- "Bubble" vs "Plateau" (flat peak across the centre icon) exist in RocketDock 1.3.0 (LOW). Keep the falloff behind one function; do not offer a setting.

### Zoom width semantics

`zoomWidth` = total width of the zone in icons (diameter), not radius. Radius `r` follows from it. Settings UI label should stay "Zoom-Breite" with the RocketDock numeric meaning so existing intuition (4) carries over.

### Anchoring (MEDIUM, with a data caveat)

- Perpendicular axis: icons grow away from the screen edge; the background keeps its height; zoomed icons protrude past it. The icon's edge-side baseline stays fixed (for a top bar the icon top stays put, plus `edgeOffset`).
- Along the axis: Q12 chose `center` (bar centre fixed). Measured centres: 404 (rest), 404, 420, 400, 365.
- Caveat for the calibration phase: three of four zoomed rows report left edge = -3. An identical value is suspicious: it is likely the screenshot border clipping the left edge, which would make the "centre" column unreliable for those rows. The far-right row (centre 365, a 39 px drift, and growth 112 px left vs. 35 px right) is not explained by pure centre anchoring *or* pure cursor anchoring. Re-shoot the reference with the dock moved away from the image border (or use a wider crop) before trusting ±40 px as the tolerance. Keep `zoomAnchor` a parameter, as planned.
- The growing bar width is `restWidth + sum(size_i - base)`; split equally left/right under `center`.

### Animation timing (MEDIUM for structure, LOW for easing)

- `ZoomTicks = 200` ms in his registry; help text: "how long, in milliseconds, items take to finish growing".
- Recommended model (from experience, not documented): two independent mechanisms.
  1. A global zoom factor `g` 0..1 ramps over `zoomDurationMs` after entering the bar, and back down after leaving; reversal continues from the current `g`.
  2. Within the bar the falloff follows the cursor *immediately* (no lag), scaled by `g`.
- Easing: unknown; start with smoothstep/ease-out, then tune by screen-recording the original and frame-stepping (two or three recordings suffice).
- No frames at rest: stop the render loop when `g == 0` and the cursor is outside.
- Pitfall: the same ramp must be used for the label (see below) so it does not appear before the icon is large.

### Hover region and edge behavior (MEDIUM, from experience)

- Hover region = union of the background rect and the currently visible icons (protruding parts of zoomed icons count). Entering uses the rest bar; leaving uses the current (zoomed) bar. Otherwise the zoom flickers at the lower/outer edge.
- With a negative `edgeOffset` (-5) the bar must still reach the physical screen edge row so top-edge flick-to-click (Fitts's law) works; verify the window really extends to y = 0.
- Hit testing for clicks uses the visible (zoomed) geometry (concept section 6).

### Labels (MEDIUM; style values HIGH from his registry)

| Aspect | Value / Behavior | Source |
|--------|------------------|--------|
| Font | Tahoma, size 13 px (`FontSize=-13`), white (`FontColor=16777215`) | his registry (HIGH) |
| Outline | black, 55 % opacity | `FontOutlineColor=0`, `FontOutlineOpacity=55` |
| Shadow | black, 25 % opacity | `FontShadowColor=0`, `FontShadowOpacity=25` |
| Hide labels | single toggle (`HideLabels`) | registry |
| Shown for | only the hovered icon, on the side away from the edge, centred on the (zoomed) icon | Q3 |
| Clamp | horizontally clamp to the monitor so labels near the bar ends are not cut off; the window must be tall/wide enough to contain them | GAP-level detail, not yet in concept |
| Separator / gap hover | no label; fade label when cursor leaves all icons | from experience |
| Long names | single line, no wrap; trim with ellipsis at a sane max width | from experience |
| Timing | follows the zoom ramp (no label while `g` is small) | from experience |
| Rendering | outline via `FormattedText.BuildGeometry` stroke + offset shadow; cache the geometry per item and font | implementation hint |

The concept (Q3) defers outline/shadow colours to V2; their opacities from his registry should become the fixed defaults (55 / 25).

---

## Launch Behavior: Details

| Aspect | Recommended behavior | Confidence |
|--------|----------------------|------------|
| Trigger | Left button up on same item as button down; never on down; ignore if the pointer moved beyond the drag threshold | MEDIUM |
| Mechanism | `Process.Start` with `UseShellExecute = true`, `WorkingDirectory` = item's working dir, else target's directory; arguments and paths expanded for `%ENV%` | HIGH (platform behavior) |
| Items | `.lnk` stored as the .lnk path (Store apps keep working, concept section 8); folders open in Explorer; URL opens default browser | HIGH |
| Admin | verb `runas`; on `Win32Exception.NativeErrorCode == 1223` do nothing | HIGH |
| Elevation inheritance | Run MehrBar unelevated. If it were elevated, every launched app would inherit elevation, and UIPI would block Explorer drops. Detect and warn in the log | MEDIUM |
| Foreground | A `WS_EX_NOACTIVATE` window may still hand foreground rights to the launched app because it received the last input event, but this must be tested: failure mode is the new app opening behind other windows or only flashing in the taskbar | LOW-MEDIUM, needs test |
| Error feedback | Non-modal. Suggested: replace the label text with a short red/yellow message for ~3 s plus a brief horizontal shake of the icon; always write the log. No MessageBox | design recommendation |
| Missing target at click time | Same feedback; offer "Properties" via the item menu, not an automatic dialog | design recommendation |
| Slow-start feedback | Press-dim on mouse down and a debounce window (~700-1000 ms) per item | design recommendation |
| Launching while zoom is mid-animation | Allowed; click maps to visible geometry at that frame | MEDIUM |

---

## Drag & Drop: Details

Reference RocketDock behaviors (vendor help, LOW per seam) and the MehrBar decision:

| Behavior | RocketDock | MehrBar recommendation |
|----------|-----------|------------------------|
| Drop file/folder/shortcut on empty area | Inserts item at drop position | V1 milestone 1: append at end (Q5); milestone 4: insert at pointer with sliding gap and an insertion marker |
| Drop on an existing app icon | Launches that app with the file | Not in V1. Dropping on any icon inserts before/after it (nearest boundary) |
| Drop on a folder item | Copies file into the folder | Never |
| Drop on Recycle Bin item | Deletes | Item type does not exist |
| Drag item off the bar | Removes it | Not in V1 (see anti-features); drag out and release = cancel |
| Ctrl while dragging with Lock Items | Override lock | Only relevant if Lock is ever added |
| Drag a `.url` / browser link / address-bar text | Not supported in plain RocketDock (LOW) | Milestone 4: accept `UniformResourceLocatorW`, `UniformResourceLocator`, `text/x-moz-url` and plain-text URLs; create a `url` item with host as default name |
| Drop effect | n/a | Always `Link` so Explorer never offers move/copy, and a Shift/Ctrl drop can never delete or copy the source |
| Multiple files in one drop | Adds all | Add all, in the order given |
| Duplicate target | Allowed | Allow (do not dedupe) |
| Zoom during drag | Dock reacts while dragging | Required in milestone 4 (see dependencies) |

Reorder specifics (from experience, MEDIUM):
- Pick-up needs a few px of movement; the picked icon follows the cursor, neighbors slide to open the gap with a short ease (~150 ms), drop animates into the slot.
- Releasing outside the bar returns the item. Escape cancels.
- Insertion index is computed from the *visible* (zoomed) geometry: nearest boundary between item midpoints.
- OLE drag-over does not produce mouse-move messages; the zoom engine must accept pointer coordinates from `DragOver` as well as from normal mouse moves.
- UIPI: an unelevated MehrBar cannot receive drops from an elevated Explorer window; document, do not engineer around.
- `.lnk` drops: store the `.lnk` path (Store apps), read target/args/icon for display only. Default name: link base name.

---

## Separator Handling: Details

- Data: an item `{type: separator}`; own place in the order; polymorphic like other items.
- Skin: `separator.ini` -> `Image`, `TopMargin`, `BottomMargin` (CrystalXP.net: both 0). The image is drawn at the separator's slot, vertically stretched between the margins against the background's inner height. Exact stretch/scale rules (and whether width scales with icon size) must be read off the real skin and documented in `docs/skin-format.md`. User forum reports (deskmodder.de, LOW) confirm that separator and background interact: a 1 px gap is typically fixed by shifting the background image, so test both together.
- Behavior (HIGH for no-zoom; his screenshots confirm separators keep their size and are only shifted):
  - never zooms, no label, no click action, no hover target;
  - occupies a fixed slot width (skin image width) in the rest layout and therefore acts as a wider pitch gap for the falloff distance;
  - is pushed along by neighbors' growth like any slot;
  - can be moved by drag and removed (right-click -> Remove; the item menu for a separator contains only Remove).
- Insertion from the bar context menu: at the nearest boundary to the right-click point, measured in the visible (zoomed) layout. Right-click does not need to change zoom state.
- Edge cases to specify: leading/trailing separators (allow; render), consecutive separators (allow), separator-only bar (valid), skin without `separator.ini` (draw a thin line fallback, do not crash).
- Separators in his data: items 6, 13 and others -> he groups by separators; insert-and-reorder must therefore work early.

---

## Feature Dependencies

```
Core/Layout (rest layout, axis-neutral)
    └──requires──> Zoom falloff + anchoring (pure functions, tests)
                       └──requires──> Render loop with zoom factor g (ramp)
                                          ├──requires──> Hover label (follows g)
                                          └──requires──> Hit testing on visible geometry
                                                             └──requires──> Click launch (mouse-up semantics)

Transparent oversized window ──requires──> Click-through for empty pixels
Non-activating bottom window ──requires──> Context-menu dismissal fix (SetForegroundWindow/WM_NULL)
                             ──requires──> Foreground handoff test for launched apps

Explorer drop (append) ──requires──> Item model + shell icon loader
    └──enhanced by──> Insertion marker + sliding gap
            ├──requires──> Zoom engine driven by DragOver coordinates
            └──requires──> Reorder (same layout animation)
Browser URL drop ──requires──> URL item type + URL default icon

Skin loader (9-slice + outside margins) ──requires──> docs/skin-format.md (reverse engineering)
    └──requires──> Separator rendering (separator.ini)
Separator insert/move/delete ──requires──> Skin separator rendering AND Item model polymorphism

Settings window (live) ──requires──> Config model + atomic save + layout re-evaluation on change
Launch error feedback ──requires──> Label rendering (reuse) AND logging
Double-launch debounce ──enhances──> Launch pipeline
Hotkey reveal (deferred) ──mitigates──> layer=bottom weakness
Drop-onto-app = open-with ──conflicts──> Drop = insert
Drag-off-to-remove ──conflicts──> accidental-edit safety (Lock)
```

### Dependency Notes

- **Zoom engine must be input-source agnostic:** mouse moves and OLE `DragOver` both feed it. Otherwise milestone 4's "zoom stays active during drag" forces a rewrite of milestone 2.
- **Labels require the ramp factor:** a label drawn before icons are large looks wrong; build both in milestone 2.
- **Separator rendering requires the skin loader (milestone 3), but the separator *item* is needed for layout earlier:** model + rest layout with a plain fallback line in milestone 1/2, skin image in 3.
- **Launch error feedback reuses the label renderer:** schedule after labels.
- **Context-menu dismissal on a non-activating window** affects milestone 1 (Remove) already; solve it where the menus first appear.

---

## MVP Definition

### Launch With (V1) - already scoped; this research confirms the scope and adds the GAP items

- [ ] Everything in PROJECT.md Active - confirmed as the right table stakes
- [ ] GAP: double-launch debounce (LOW) + minimal press/launch acknowledgement (LOW)
- [ ] GAP: non-modal launch-error feedback specified (label message + shake + log) (LOW-MEDIUM)
- [ ] GAP: label clamping inside the monitor, label geometry included in window size (LOW)
- [ ] GAP: URL item default icon (LOW); missing-target fallback icon (LOW)
- [ ] GAP: default item name from file description / link name (LOW)
- [ ] GAP: context menus dismissable on the non-activating window (MEDIUM, technical)
- [ ] GAP: second start signals first instance to open settings (LOW)
- [ ] Calibration chores: re-shoot reference with the dock away from the image border; measure Zoom Width 2/6/8; screen-record the zoom ramp

### Add After Validation (V1.x)

- [ ] Hotkey to toggle/reveal the bar - trigger: the developer finds layer=bottom too hard to reach (most likely first complaint)
- [ ] Launch "hop" effect (Deferred Q3) - trigger: press feedback feels insufficient
- [ ] Lock items - trigger: accidental reorders
- [ ] Label outline/shadow colour settings (Deferred Q3)

### Future Consideration (V2+)

- [ ] Right-edge vertical bar (the real reason for the project, Q6), then multi-bar UI (Q7), groups (Q8), autohide (Q9), layers (Q10), multi-monitor/AppBar (Q1), import (Q4), skins (Q2), localization, installer

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Zoom falloff + anchoring + ramp | HIGH | HIGH | P1 |
| Label (clamped, styled) | HIGH | MEDIUM | P1 |
| Click launch (mouse-up, args, cwd) | HIGH | LOW | P1 |
| Explorer drop (append) | HIGH | MEDIUM | P1 |
| Skin 9-slice + separator | HIGH | MEDIUM | P1 |
| Layer bottom / no focus theft / click-through | HIGH | MEDIUM-HIGH | P1 |
| Settings live | MEDIUM-HIGH | MEDIUM | P1 |
| Context menus + Properties | MEDIUM-HIGH | LOW-MEDIUM | P1 |
| Launch-error feedback | MEDIUM-HIGH | LOW-MEDIUM | P1 |
| Double-launch debounce, press feedback | MEDIUM | LOW | P1 |
| Insertion marker, reorder, URL drop | MEDIUM-HIGH | MEDIUM | P1 (milestone 4) |
| Default names, fallback icons, URL icon | MEDIUM | LOW | P2 |
| Hotkey reveal | MEDIUM-HIGH (after use) | MEDIUM | P2 |
| Launch hop effect | LOW-MEDIUM | MEDIUM | P3 |
| Lock items | LOW | LOW | P3 |
| Plateau variant, opacity options | LOW | LOW | P3 |
| Docklets, running indicators, previews, groups (now) | n/a | HIGH | Not planned / later |

---

## Competitor Feature Analysis

| Feature | RocketDock 1.3.5 (HIGH for his config, LOW web) | macOS Dock (LOW, experience) | ObjectDock / Winstep Nexus (LOW, catalog descriptions) | MehrBar approach |
|---------|------|------|------|------|
| Magnification | Bubble + Plateau, Zoom/Size/Width/Duration, quality | Smooth hump, slider for size/magnification, bar grows | Zoom plus many effects | RocketDock numbers, cos hump, calibrated |
| Labels | Outline+shadow, font selectable | Rounded tooltip above the icon | Configurable | RocketDock style |
| Launch feedback | Icon activation FX (his: 2) | Bounce | Many effects (bounce, etc.) | Press-dim now, hop later |
| Drag/drop | Insert, open-with, copy-into-folder, drag-off delete | Insert, spring-loaded folders, drag-off "poof" | Insert, sub-docks | Insert only, Link effect |
| Separators | Item with skin image | Spacer tiles | Separators | Item, no zoom |
| Running state | Optional indicators, minimize-to-dock | Always (dots) | Optional | Never |
| Plugins | Docklets | None | Docklets / widgets | Never |
| Multiple docks | Single | Single | Multiple, sub-docks, tabs (paid tiers) | One bar, model ready |
| Autohide | Yes | Yes | Yes | Deferred Q9 |
| Layering | Top / Normal / Bottom | Fixed | Several | Bottom only |
| DPI | Not Per-Monitor | Native | Varies | Per-Monitor v2 |
| Cost / weight | Free, light, discontinued | Built in | Nexus freemium, ObjectDock paid | Free, light |

---

## Open Questions for the Developer / Later Phases

1. What do his `HoverFX=1` and `IconActivationFX=2` actually do on screen? Observe RocketDock (hover highlight? bounce?) so the Deferred Q3 effects and "press feedback" can mirror it. Values are in `HKCU\Software\RocketDock`; semantics could not be confirmed from documentation.
2. Is `MouseActivate=0` relevant to anything he relies on? (unknown meaning)
3. Which rule for dropping onto an existing icon: insert (recommended) or RocketDock's open-with?
4. Re-shot reference screenshots (anchoring caveat above) and the Zoom Width 2/6/8 series for `r` derivation.

## Sources

- Local, HIGH: `HKCU\Software\RocketDock` and `...\Icons` on the developer's machine (settings values, item structure, separators, arguments/working dirs); `.planning/PROJECT.md`; `docs/PROJEKT_START.md` (Q1-Q16, measured table).
- Vendor help text for RocketDock 1.3.5 (mirrored copy): https://discmaster.textfiles.com/file/36965/maximum-cd-2010-01.iso/DiscContents/RocketDock-v1.3.5.exe/{app}/Help/English/index.html?html=true (settings semantics, drop behaviors, Lock Items, layering) - LOW per confidence seam, corroborated by registry keys.
- RocketDock 1.3.0 changelog (Plateau, Zoom Opaque, Hover/Icon Activation FX): https://www.neowin.net/news/rocketdock-130-beta-1/ via search snippets (page itself returned 403) - LOW.
- Skin margins/separator interaction (forum reports): https://deskmodder.de/phpBB3/viewtopic.php?p=234301 and related threads - LOW.
- Competitor catalogue descriptions (ObjectDock, Nexus, RocketDock): https://www.alternativeto.net/software/rocketdock/ , https://www.alternativeto.net/software/nexus/ , https://www.alternativeto.net/software/objectdock/ - LOW.
- Cosine-falloff reference implementation (generic docks): https://pub.dev/packages/flutter_mac_dock - LOW, used only to cross-check the shape.
- Items tagged "from experience" / "design recommendation" are the researcher's synthesis (Windows shell behavior, dock UX conventions), not sourced.

---
*Feature research for: RocketDock-style Windows launcher dock (MehrBar)*
*Researched: 2026-10-09*
