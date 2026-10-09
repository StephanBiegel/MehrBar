<!-- GSD:project-start source:PROJECT.md -->

## Project

**MehrBar**

MehrBar ist ein schlanker Programmstarter für Windows im Stil von RocketDock: eine Leiste am Bildschirmrand, deren Symbole beim Überfahren mit der Maus stufenlos vergrößern, mit Beschriftung, Trennlinien, Drag & Drop und RocketDock-Skin-Unterstützung. Version 1 ist für den Eigengebrauch des Entwicklers (Stephan) und ersetzt seine RocketDock-Leiste oben am Bildschirm; der eigentliche Anlass ist eine zweite Leiste am rechten Rand, die nach V1 kommt.

Vollständiges Konzept mit Begründung jeder Entscheidung: `docs/PROJEKT_START.md` (Hauptteil Abschnitte 1–12, Grill-Protokoll Q1–Q20).

**Core Value:** **Der Zoom fühlt sich an wie RocketDock mit CrystalXP.net** — flüssig, vertraut, mit demselben Abfall und derselben Verankerung. Ohne das ist MehrBar kein Ersatz; bei Abwägungen geht der Zoom vor.

### Constraints

- **Tech stack**: C# / .NET 10 (LTS, SDK 10.0.401 installiert) / WPF — Festlegung im Konzept
- **Plattform**: Windows 10/11 x64, self-contained single-file .exe, `Skins\` als Ordner daneben
- **Architektur**: Alles ohne Oberfläche Testbare (Zoom, Layout, Config, Skin-Parser) in `MehrBar.Core` mit xUnit-Tests; Win32 nur in `App/Interop` — damit Zoom-Mathematik testbar und die rechte Leiste ohne Umbau möglich ist
- **Achsenneutralität**: Zoom und Layout rechnen entlang einer abstrakten Leistenachse — die rechte Leiste ist der eigentliche Anlass
- **Rendering**: eigenes leichtgewichtiges Control (`DrawingVisual`/`OnRender`), keine ItemsControl mit vielen Einzel-Controls — für flüssigen Zoom
- **Performance**: 60 fps beim Zoom mit 60+ Symbolen, Start < 1 s, im Ruhezustand keine Frames
- **Abhängigkeiten**: keine schweren Fremdbibliotheken, nur gepflegte NuGet-Pakete mit kompatibler Lizenz
- **Sprache**: Code, Kommentare, Commits Englisch; UI Deutsch, aber in Ressourcen-Dateien
- **Rechte**: Fremde Skins und Screenshots nie ins Repo

<!-- GSD:project-end -->

<!-- GSD:stack-start source:research/STACK.md -->

## Technology Stack

- Package versions and publish dates were queried from the NuGet API on 2026-10-09 (primary source).
- Most behavioral claims were **verified by building and running throw-away probes** on this machine (Windows 11, SDK 10.0.401, runtime 10.0.12, 100 % scaling): a WPF app with CsWin32, shell icon extraction, `.lnk` reading, PerMonitorV2 manifest, five publish variants, H.NotifyIcon, WinForms size delta, an xUnit v3 project in two runner modes, and a System.Text.Json round-trip test. These are tagged **[probe]**.
- Claims taken from Microsoft Learn / xunit.net pages are tagged **[docs]**; community-sourced or unverified claims are tagged **[community]** or **[unverified]** and are never load-bearing.
- The GSD confidence seam rates plain web results LOW and web results that were cross-checked MEDIUM. Nothing below depends on a LOW-only source.

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| .NET SDK / runtime | SDK 10.0.401 (pin in `global.json`), runtime 10.0.12 | Build + self-contained runtime | Installed and verified; every probe below ran on it. Pin the SDK so GSD agents and the developer build identically. |
| WPF (`net10.0-windows`) | in-box | UI, transparent window, custom `OnRender` control | Fixed by concept. App project only. |
| `MehrBar.Core` / `MehrBar.Tests` target | `net10.0` (NOT `-windows`) | Pure logic, testable without WPF | Keeps Zoom/Layout/Config/Skin-parser free of Windows assemblies; test project runs on plain `net10.0`. |
| **Microsoft.Windows.CsWin32** | **0.3.360** (MIT, MS-owned, published 2026-10-08) | Source-generated Win32/COM P/Invoke in `App/Interop` | Verified end-to-end for every shell API this project needs (see "Win32 access"). Generates only the APIs listed in `NativeMethods.txt`, so zero runtime dependency. Pin exactly; 0.3.x ships almost daily and generated signatures can shift between versions. |
| System.Text.Json | in-box (10.0.x), **no package reference** | Config persistence | In the shared framework; source generator available; extension-data + custom converter give full unknown-field preservation (probe-verified). |
| **H.NotifyIcon.Wpf** | **2.4.1** (MIT, 2025-12-01) | Tray icon (emergency exit) | Adds ~0.6 MB to the single-file exe vs. **+33 MB for WinForms `NotifyIcon`** [probe]. WPF-native `ContextMenu`. Repo active (last push 2026-10-05). Stay on stable 2.4.1; 2.5.0 is still beta. |
| **xunit.v3.mtp-v2** | **4.0.1** (Apache-2.0, 2026-09-12) | Unit tests for `MehrBar.Core` | Current xUnit generation; `dotnet test` verified passing on SDK 10.0.401 [probe]. xUnit v2 (2.9.3, last release 2025-01-08) is legacy. |

### Hand-written (deliberately NO package)

| Component | Where | Why no library |
|-----------|-------|----------------|
| RocketDock `.ini` parser | `Core/Skins` | ~40 lines; the real skin files are trivial (see below). A Win32 `GetPrivateProfileString` call would break Core's testability. |
| Rolling file logger | `Core` (System.IO only) | ~80 lines; "simple rotation" is the concept's requirement. Serilog is the documented fallback. |
| `.lnk` reader | `App/Interop` | `IShellLinkW` + `IPersistFile` via CsWin32, probe-verified. Every NuGet `.lnk` library is stale (Securifybv.ShellLink: one release, 2019). |
| Single-instance guard | `App` | Named `Mutex`, 10 lines. |
| Autostart | `App/Interop` | `Microsoft.Win32.Registry` (in-box) HKCU Run key. |
| Atomic config write | `Core/Config` | `File.Replace(tmp, config.json, config.bak.json)` (in-box). |

### Supporting Libraries / Dev Tools

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.NET.Test.Sdk | 18.10.1 (MIT) | VSTest host | **Only** in the VSTest fallback (see xUnit section). |
| xunit.runner.visualstudio | 4.0.0 (Apache-2.0, 2026-08-15) | VSTest adapter | **Only** in the VSTest fallback. |
| xunit.v3.mtp-off | 4.0.1 | xUnit v3 without MTP | **Only** in the VSTest fallback (replaces `xunit.v3.mtp-v2`). |
| coverlet.collector | 10.1.0 (MIT) | Coverage | Optional; only if coverage is wanted. Not needed for V1. |
| Serilog 4.4.0 + Serilog.Sinks.File 7.0.0 | Apache-2.0 | Logging | **Fallback only** if the hand-rolled logger proves insufficient (structured/async logging). |

## Decisions per Question

### 1. High-resolution shell icon extraction

| Item | IShellItemImageFactory (ICONONLY) | SHGetImageList JUMBO | Notes |
|------|-----------------------------------|----------------------|-------|
| notepad.exe / cmd.exe / firefox.exe / mmc.exe | 256x256, full alpha | pixel-identical | both fine for plain files |
| folder, `win.ini` | 256x256 | identical | |
| `.lnk` (custom test link) | 256x256, target's icon, **no shortcut-arrow overlay** | identical | |
| Store app via `shell:AppsFolder\<AUMID>` | 256x256, content fills frame | 256x256 but differently framed (smaller, padded) | factory gives the better result |
| Same Store app, **without** `ICONONLY` | **44x44** | n/a | the default flag returns a small thumbnail |
| `img0.jpg`, **without** `ICONONLY` | 256x160 photo thumbnail | n/a | default flags return thumbnails, not icons |
- **Always pass `SIIGBF_ICONONLY`.** Without it you get thumbnails / 44 px images.
- **Request 256 once and scale in WPF**; ~256 KB per icon, ~15 MB at 60 icons.
- The returned `HBITMAP` carries **straight (non-premultiplied) alpha** (color channel > alpha on thousands of edge pixels). Read pixels with `GetObject` + `GetDIBits` (top-down 32 bpp, `biHeight = -h`), build a `BitmapSource` as `Bgra32`, **convert once to `Pbgra32` and `Freeze()`**. Drawing straight alpha while scaling produces dark fringes. Always `DeleteObject` the HBITMAP.
- **Works from a thread-pool (MTA) thread** (verified) as well as the STA UI thread. So load icons in parallel in the background and show placeholders; do not block the UI thread (60 icons x ~10-30 ms plus warm-up is 1-2 s total).
- `.lnk` and `.exe`: pass the path as stored (the `.lnk` path itself, per concept section 8); the shell resolves the link's icon location.
- Custom PNG/ICO per entry: WPF's `BitmapDecoder` (pick the largest frame of an ICO). No library.

### 2. CsWin32 vs. hand-written P/Invoke

- **Ambiguous names must be qualified** in `NativeMethods.txt` (`IImageList` exists in `Windows.Win32.UI.Controls` and `Windows.Win32.System.Mmc` -> error PInvoke007).
- Types you only *use* (`BITMAP`) must be listed too, otherwise a different, inaccessible `BITMAP` binds.
- **Never set `EmitCompilerGeneratedFiles` with an output path inside the project folder** - the `*.g.cs` files get globbed back in and produce hundreds of duplicate-type errors. Use an `obj/` path or leave it off.
- Always add `using Windows.Win32.System.Com;` etc. for COM helpers (`IPersistFile`, `CLSCTX`, `STGM`).

### 3. Tray icon

| Option | Cost / verdict |
|--------|----------------|
| WinForms `NotifyIcon` | `UseWindowsForms` raises the self-contained single-file exe **139.8 MB -> 173.0 MB (+33 MB)** [probe] because the WinForms assemblies get bundled; it also injects `global using System.Windows.Forms` + `System.Drawing`, which makes `Application` ambiguous in every WPF file (**CS0104, probe-verified**; needs `<Using Remove=...>`). Menu is a WinForms `ContextMenuStrip`, visually foreign to WPF. Reject. |
| **H.NotifyIcon.Wpf 2.4.1** | **+0.6 MB**; `TaskbarIcon` created + `ForceCreate()`/`Dispose()` ran without exception in the single-file build [probe; the visible tray glyph and menu popup were not inspected - verify in milestone 1]. WPF `ContextMenu`, MIT, depends only on `H.NotifyIcon`. |
| Hand-rolled `Shell_NotifyIcon` via CsWin32 | ~150 lines incl. hidden `HwndSource`, `TaskbarCreated` re-registration (Explorer restart), `WM_CONTEXTMENU` + foreground-window dance. Zero dependency but you re-implement known gotchas. **This is the fallback** if H.NotifyIcon ever misbehaves - hence the seam. |

### 4. `.lnk` parsing

### 5. RocketDock INI parsing

- Trim key and value; split on the **first** `=`; case-insensitive section and key lookup.
- Skip blank lines and lines starting with `;` or `#` (assumed for other skins - unverified).
- Accept missing trailing newline, duplicate keys (last wins), unknown keys (ignored).
- Encoding: sniff BOM (UTF-8 / UTF-16 LE/BE); with no BOM decode as UTF-8, falling back to `Encoding.Latin1` on invalid bytes. Only one skin was available, so other skins' encodings are unverified - cover with tests using BOM and Latin-1 fixtures.
- Missing/invalid numeric -> documented default, never throw (concept: broken skin falls back to default background, log a warning).

### 6. JSON polymorphism with preserved unknown fields

| Scenario | Result |
|----------|--------|
| `"type"` not the first property in an item (hand-edited file) | **Exception** unless `AllowOutOfOrderMetadataProperties = true` (default false) |
| Unknown discriminator (`"type":"group"` from a newer build) with **abstract** base | **`NotSupportedException`** - the whole config fails to load |
| Unknown discriminator with `IgnoreUnrecognizedTypeDiscriminators = true` and a **concrete** base | Loads, but on save the item is written **without** `"type":"group"` -> **silent data loss / corrupted file** |
- `"type"` accepted in any position; known types round-trip; **`"type":"group"` with nested `items` is written back byte-for-byte**; unknown fields at root, bar and item level survive via `[JsonExtensionData] Dictionary<string, JsonElement>`.
- `"type"` must be removed from a known item's extension data after reading and re-emitted first on write, otherwise it is duplicated.

### 7. Single instance

- `Local\` (per logon session), not `Global\` - correct for fast user switching and needs no privileges.
- Checking in `OnStartup` is fine: only the *second* launch pays the WPF-init cost (~100-200 ms [unverified]); a custom `Main` is unnecessary complexity.
- Optional nicety: the second instance sets a named `EventWaitHandle`; the first opens the config window. Not required by V1.
- Reject: `Process.GetProcessesByName` (race, matches renamed exes and other users' sessions), `Microsoft.VisualBasic.ApplicationServices` (pulls the VB runtime), third-party single-instance packages.

### 8. Logging with rotation

| Option | Verdict |
|--------|---------|
| **Hand-rolled** | Chosen. Matches "simple rotation", keeps Core dependency-free. |
| Serilog 4.4.0 + Serilog.Sinks.File 7.0.0 | **Fallback.** Apache-2.0, mature. Cost: two packages; Sinks.File 7.0.0 (2025-04) has no `net10.0` TFM (uses its `net9.0` asset - works); Serilog 4.4.0 targets `net10.0`. Adopt only if structured/async logging becomes a need. |
| NLog 6.2.1 | BSD-3, actively maintained, but a larger configuration surface than this app needs. |
| Microsoft.Extensions.Logging | Abstractions only - no file provider in the box, so you still need a third-party sink. Reject. |

### 9. Autostart

- No admin rights, no extra dependency (`Microsoft.Win32.Registry` is in-box), trivially reversible; the "Mit Windows starten" checkbox maps 1:1 to value present/absent.
- **Quote the path** and use `Environment.ProcessPath` (not `Assembly.Location`, which is empty in single-file [probe]). The Run value is a command line, documented limit 260 chars [docs].
- **Self-heal:** when the setting is on, rewrite the value at each start so a moved/renamed exe does not leave a dead entry.
- **Respect Task Manager:** Task Manager's disable toggle lives in `HKCU\...\Explorer\StartupApproved\Run` (binary value; first byte `02` = enabled, otherwise disabled [community]). Read it and show the checkbox as "disabled by Windows" instead of fighting it; never write that key.
- **Expect a delay:** Microsoft documents that the system may delay Run-key programs to avoid hurting the foreground experience [docs]; the dock may appear several seconds after logon (RocketDock behaves the same). Do **not** touch the global `Explorer\Serialize\StartupDelayInMSec` tweak.
- **Task Scheduler rejected:** needs the Task Scheduler COM API or shelling out to `schtasks.exe` (or a library), and its main advantage (elevation, precise delay) is *undesirable* here - a dock running elevated cannot receive drag & drop from a normal-integrity Explorer (UIPI) [unverified for this app; standard Windows behavior]. Startup-folder shortcut rejected: needs `IShellLink` creation and offers nothing over the Run key.

### 10. Per-Monitor-DPI v2 manifest for WPF

### 11. Single-file publish caveats with WPF

| Variant | Result [probe] |
|---------|----------------|
| `PublishSingleFile` only (**the command in the concept**) | `.exe` 131.6 MB **plus 5 loose files**: `D3DCompiler_47_cor3.dll`, `PenImc_cor3.dll`, `PresentationNative_cor3.dll`, `vcruntime140_cor3.dll`, `wpfgfx_cor3.dll`. **Not a single file.** |
| + `IncludeNativeLibrariesForSelfExtract=true` | **One 139.8 MB `.exe`** (+ `.pdb` unless embedded). Natives are extracted on first run to `%TEMP%\.net\<app>\<hash>\`. Warm run ~0.33-0.34 s, first run ~0.45 s (probe incl. icon work). |
| + `PublishReadyToRun=true` | 155.7 MB; warm 0.31-0.34 s (negligible on a tiny probe; the real app has more JIT work - **measure** cold start in milestone 6, keep if it helps). |
| + `EnableCompressionInSingleFile=true` | 69.5 MB, but **first run 5.4 s** and warm runs ~0.6 s (about 2x slower). **Reject** - violates the < 1 s start requirement. |
| `PublishTrimmed=true` | **Build error NETSDK1168**: WPF is not supported with trimming. Not an option (confirms: no trimming, no NativeAOT). |
- `AppContext.BaseDirectory` = the exe's folder (also with native self-extract) -> use it, or `Path.GetDirectoryName(Environment.ProcessPath)`, for `Skins\`. `Assembly.Location` is **empty** and triggers analyzer warning IL3000; never use it [docs + probe].
- `Skins\` must stay a loose folder next to the exe: add it as `Content` with `CopyToOutputDirectory`/`CopyToPublishDirectory` (the build step that copies `samples/skins/`). Non-assembly content is not bundled by default; mark with `ExcludeFromSingleFile=true` to be explicit [docs; verify in milestone 3].
- The extraction folder is hash-keyed per build; each new exe build leaves a stale `%TEMP%\.net\MehrBar\<hash>` dir (the extracted natives, ~8 MB). Harmless, but a future installer/uninstaller should not assume the folder is absent.
- Do not expect `dotnet publish -c Release` to emit the bundle for `Core`/`Tests`: set the RID/self-contained properties **only in App.csproj**.
- Change the publish command in the concept to `dotnet publish src/MehrBar.App -c Release` (properties now live in the csproj); the old command line produces loose DLLs unless `-p:IncludeNativeLibrariesForSelfExtract=true` is added.
- Setting `InvariantGlobalization` or `SatelliteResourceLanguages` could trim size/start slightly; not evaluated - only try if startup misses the budget.

### 12. xUnit version for .NET 10

## Installation

# One-time repo scaffolding (pins SDK + selects MTP runner)

# global.json: see section 12;  Directory.Packages.props: central versions below

# Core (net10.0): NO package references

# App (net10.0-windows)

# Tests (net10.0, OutputType Exe)

# VSTest fallback only

# dotnet add src/MehrBar.Tests package Microsoft.NET.Test.Sdk --version 18.10.1

# dotnet add src/MehrBar.Tests package xunit.v3.mtp-off --version 4.0.1

# dotnet add src/MehrBar.Tests package xunit.runner.visualstudio --version 4.0.0

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| `IShellItemImageFactory` | `SHGetImageList(SHIL_JUMBO)` | Never needed here; consider only if a future requirement wants the shell's *system image list index* semantics (e.g. overlay icons). |
| CsWin32 (`allowMarshaling:false`) | Hand-written `[LibraryImport]` / `[GeneratedComInterface]` | If CsWin32's churn becomes a nuisance after V1: freeze the generated code you use into `Interop/` and drop the package. |
| H.NotifyIcon.Wpf | Hand-rolled `Shell_NotifyIcon` | If H.NotifyIcon stops being maintained or misbehaves on Explorer restart. |
| H.NotifyIcon.Wpf | WinForms `NotifyIcon` | Only if the app already needs WinForms for something else (it does not). |
| Custom `JsonConverter<DockItem>` | `[JsonPolymorphic]` attributes | Only when *all* types are known, discriminator order is guaranteed and data loss on unknown types is acceptable - not this project (forward-compat is a stated requirement). |
| Hand-rolled logger | Serilog 4.4.0 + Sinks.File 7.0.0 | When structured logs, async sinks, or time-based rolling are needed. |
| HKCU Run key | Task Scheduler logon task | Only if a precise start delay or elevation were required (neither is). |
| `xunit.v3.mtp-v2` (MTP) | `xunit.v3.mtp-off` + VSTest | If VS Code Test Explorer cannot discover MTP tests. |
| xunit.v3 4.0.1 | xunit 2.9.3 | Never for new code; v2 had no release since 2025-01. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| WinForms `NotifyIcon` / `UseWindowsForms` | +33 MB exe; CS0104 `Application` ambiguity via implicit usings [probe] | H.NotifyIcon.Wpf |
| `EnableCompressionInSingleFile` | 5.4 s first run, ~2x slower warm start [probe] | Uncompressed single file |
| `PublishTrimmed` / NativeAOT | NETSDK1168: WPF unsupported [probe] | Plain self-contained, optionally ReadyToRun |
| Single-file without `IncludeNativeLibrariesForSelfExtract` | 5 loose native DLLs [probe] | Add the property |
| `Assembly.Location` / `GetExecutingAssembly().Location` | Empty string in single-file [probe] | `AppContext.BaseDirectory`, `Environment.ProcessPath` |
| SIIGBF default flags (no `ICONONLY`) | Thumbnails / 44 px images [probe] | `SIIGBF_ICONONLY \| SIIGBF_BIGGERSIZEOK` |
| Drawing straight-alpha shell bitmaps directly | Dark fringes when scaled; shell alpha is non-premultiplied [probe] | Convert to `Pbgra32` once, freeze |
| `[JsonPolymorphic]` alone for `DockItem` | Throws on unknown/out-of-order discriminator or silently drops `"type"` [probe] | Custom converter + `UnknownItem` |
| `[JsonSourceGenerationOptions(Converters=...)]` when passing own options | Ignored -> "abstract type" error [probe] | Add the converter to the `JsonSerializerOptions` you pass to the context |
| `EmitCompilerGeneratedFiles` into the project dir | Duplicate-type build errors with CsWin32 [probe] | `obj/` path or off |
| `GetPrivateProfileString` for skin INIs | Win32 in Core; encoding quirks | Hand-written parser |
| Task Scheduler for autostart | Complexity; elevation breaks drag & drop (UIPI) | HKCU Run key |
| `Process.GetProcessesByName` for single instance | Races, wrong-session matches | Named `Local\` Mutex |
| gong-wpf-dragdrop 4.0.0 (or similar) | Built for `ItemsControl`; the dock uses a custom-rendered control | WPF `DragDrop`/`IDataObject` (`FileDrop`, `UniformResourceLocatorW`) |
| FluentAssertions 8.x | License change (commercial model since v8) [MEDIUM] | xUnit `Assert` |
| xunit 2.9.3 (v2) | Legacy line, no 2026 releases | xunit.v3.mtp-v2 |
| Running the dock elevated | Breaks UIPI drag & drop from normal Explorer; unnecessary | Keep `asInvoker`; "Als Administrator starten" per entry via `runas` |

## Stack Patterns by Variant

- Switch Tests to the VSTest fallback (xunit.v3.mtp-off + runner.visualstudio + Test.Sdk, remove `test.runner` from `global.json`).
- Because it is verified working and a three-line change; CLI results are identical.
- Replace the `ITrayIcon` implementation with a ~150-line `Shell_NotifyIcon` class in `App/Interop`.
- Because the tray is the emergency exit and must be dependable; the seam makes this local.
- First enable/verify `PublishReadyToRun`, then source-generated JSON (already chosen), then try `InvariantGlobalization`/`TieredPGO=false`; never compression.
- Because those are the measured/known levers; compression was measured to hurt.
- Add the names to `NativeMethods.txt`; do not hand-roll signatures.
- Because the generator handles `IntPtr`/`HWND` width and COM lifetimes correctly.

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| Microsoft.Windows.CsWin32 0.3.360 | SDK 10.0.401, `net10.0-windows` WPF | Built and ran [probe]. Needs `AllowUnsafeBlocks`; `PrivateAssets=all`. Pin exactly - near-daily 0.3.x releases. |
| H.NotifyIcon.Wpf 2.4.1 | `net10.0-windows7.0` target present | Depends on H.NotifyIcon 2.4.1 + `Microsoft.WindowsDesktop.App.WPF`; ran in self-contained single-file [probe]. |
| xunit.v3.mtp-v2 4.0.1 | `net10.0`, SDK 10 `dotnet test` with `global.json` MTP runner | MTP v1 dropped in 4.0; `OutputType=Exe` required. |
| xunit.v3.mtp-off 4.0.1 | xunit.runner.visualstudio 4.0.0, Microsoft.NET.Test.Sdk 18.10.1 | VSTest mode; do not combine with `test.runner=MTP`. |
| `net10.0` Core | WPF App `net10.0-windows` | App may reference Core; Core must not reference App/WPF. |
| System.Text.Json (in-box 10.0.x) | source generator + custom converter | `[JsonSourceGenerationOptions]` converters are not applied when options are passed explicitly. |
| WPF + single-file | `IncludeNativeLibrariesForSelfExtract` | Without it, 5 native DLLs stay beside the exe. |

## Sources

- NuGet API (`api.nuget.org` flat container + registration, queried 2026-10-09) - all versions, dates, license expressions above. HIGH (primary registry data).
- **Probe results (executed locally, scratchpad probes; Windows 11, SDK 10.0.401, runtime 10.0.12)** - icon extraction comparison, `.lnk` reading, PerMonitorV2 with/without manifest, five single-file publish variants, WinForms size delta, H.NotifyIcon in single-file, STJ polymorphism/round-trip tests, xUnit v3 in MTP and VSTest modes. HIGH (observed behavior); caveat: one machine, 100 % scaling, one skin sample.
- https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview - native libraries, `IncludeNativeLibrariesForSelfExtract`, `EnableCompressionInSingleFile`, `Assembly.Location`, `AppContext.BaseDirectory` (page dated 2026-03-25). MEDIUM [docs], corroborated by probe.
- https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process - manifest `dpiAwareness`/`dpiAware` semantics. MEDIUM [docs], corroborated by probe.
- https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys - Run key semantics, 260-char limit, system may delay Run programs. MEDIUM [docs].
- https://xunit.net/docs/getting-started/v3/getting-started and https://xunit.net/releases/v3/4.0.0 - v3 package layout, MTP-v2 default, `global.json` runner selection on .NET 10, 4.0 breaking changes. MEDIUM [docs], corroborated by probe.
- https://code.visualstudio.com/docs/csharp/testing - C# Dev Kit supports xUnit/NUnit/MSTest; no mention of MTP. LOW/MEDIUM (absence of evidence).
- Community sources (startup-delay and `StartupApproved` behavior; 2026 reports of Visual Studio Test Explorer + xUnit v3 MTP hangs) - LOW; used only for caveats, never for decisions.
- https://github.com/HavenDV/H.NotifyIcon - repository activity (last push 2026-10-05) via GitHub API. MEDIUM.

## Gaps (carry into phase research)

- **VS Code C# Dev Kit + xUnit v3 MTP Test Explorer** - unverified; resolve in milestone 1 (fallback B ready).
- **Visible tray glyph / menu popup with H.NotifyIcon** - only constructed/disposed in the probe; confirm visually, incl. Explorer restart (milestone 1/6).
- **Other RocketDock skins' INI encodings/comment styles** - one skin sample only.
- **R2R benefit on the real app** and **`Skins\` content handling in publish** - measure in milestones 3 and 6.
- **`File.Replace` atomic write semantics under AV/OneDrive** - documented, not exercised.
- **CsWin32 `allowMarshaling:true` mode** - not evaluated; the verified `false` mode is the recommendation.
- **125/150 % scaling** - only 100 % tested (matches the developer's setup; concept calls for a spot check).

<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->

## Conventions

Conventions not yet established. Will populate as patterns emerge during development.
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->

## Architecture

Architecture not yet mapped. Follow existing patterns found in the codebase.
<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->

## Project Skills

| Skill | Description | Path |
|-------|-------------|------|
| grill-me | Interview the user relentlessly about a plan or design until reaching shared understanding, resolving each branch of the decision tree. Use when user wants to stress-test a plan, get grilled on their design, or mentions "grill me". | `.claude/skills/grill-me/SKILL.md` |
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->

## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd-fast` for a trivial task inline, with no subagents and no PLAN.md
- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->

## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
