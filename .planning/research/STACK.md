# Stack Research

**Domain:** RocketDock-style Windows application launcher dock (C# / .NET 10 / WPF, self-contained single-file x64)
**Researched:** 2026-10-09
**Confidence:** HIGH for everything marked "probe-verified" (executed on this machine); MEDIUM where only documentation was read; see per-item tags.

**Scope note:** The base stack (C# / .NET 10 LTS / WPF, self-contained single-file x64) is fixed by the concept and is NOT re-litigated. This document decides the libraries, Win32 access strategy and build settings *inside* that stack.

**How this was researched (read this to calibrate trust):**
- Package versions and publish dates were queried from the NuGet API on 2026-10-09 (primary source).
- Most behavioral claims were **verified by building and running throw-away probes** on this machine (Windows 11, SDK 10.0.401, runtime 10.0.12, 100 % scaling): a WPF app with CsWin32, shell icon extraction, `.lnk` reading, PerMonitorV2 manifest, five publish variants, H.NotifyIcon, WinForms size delta, an xUnit v3 project in two runner modes, and a System.Text.Json round-trip test. These are tagged **[probe]**.
- Claims taken from Microsoft Learn / xunit.net pages are tagged **[docs]**; community-sourced or unverified claims are tagged **[community]** or **[unverified]** and are never load-bearing.
- The GSD confidence seam rates plain web results LOW and web results that were cross-checked MEDIUM. Nothing below depends on a LOW-only source.

---

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

Central package management (`Directory.Packages.props`) is recommended: three projects, five pins, one place to bump CsWin32 deliberately.

---

## Decisions per Question

### 1. High-resolution shell icon extraction

**Decision: `IShellItemImageFactory.GetImage` (256x256, `SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK`) via CsWin32. Confidence: HIGH [probe].**

Probe compared `IShellItemImageFactory` against `SHGetFileInfo(SYSICONINDEX)` + `SHGetImageList(SHIL_JUMBO)` + `IImageList.GetIcon` on exe, folder, data file, `.lnk`, and a Store app (`shell:AppsFolder\...!App`):

| Item | IShellItemImageFactory (ICONONLY) | SHGetImageList JUMBO | Notes |
|------|-----------------------------------|----------------------|-------|
| notepad.exe / cmd.exe / firefox.exe / mmc.exe | 256x256, full alpha | pixel-identical | both fine for plain files |
| folder, `win.ini` | 256x256 | identical | |
| `.lnk` (custom test link) | 256x256, target's icon, **no shortcut-arrow overlay** | identical | |
| Store app via `shell:AppsFolder\<AUMID>` | 256x256, content fills frame | 256x256 but differently framed (smaller, padded) | factory gives the better result |
| Same Store app, **without** `ICONONLY` | **44x44** | n/a | the default flag returns a small thumbnail |
| `img0.jpg`, **without** `ICONONLY` | 256x160 photo thumbnail | n/a | default flags return thumbnails, not icons |

Why the factory wins: one COM call from path to bitmap (jumbo needs two steps and an index lookup); any requested size (not fixed 256); handles `shell:AppsFolder` item names directly. Both methods are equally fast per icon (6-30 ms after warm-up; the first call costs ~100-150 ms for shell warm-up; Store apps 60-300 ms).

Rules that fell out of the probe (all [probe]):
- **Always pass `SIIGBF_ICONONLY`.** Without it you get thumbnails / 44 px images.
- **Request 256 once and scale in WPF**; ~256 KB per icon, ~15 MB at 60 icons.
- The returned `HBITMAP` carries **straight (non-premultiplied) alpha** (color channel > alpha on thousands of edge pixels). Read pixels with `GetObject` + `GetDIBits` (top-down 32 bpp, `biHeight = -h`), build a `BitmapSource` as `Bgra32`, **convert once to `Pbgra32` and `Freeze()`**. Drawing straight alpha while scaling produces dark fringes. Always `DeleteObject` the HBITMAP.
- **Works from a thread-pool (MTA) thread** (verified) as well as the STA UI thread. So load icons in parallel in the background and show placeholders; do not block the UI thread (60 icons x ~10-30 ms plus warm-up is 1-2 s total).
- `.lnk` and `.exe`: pass the path as stored (the `.lnk` path itself, per concept section 8); the shell resolves the link's icon location.
- Custom PNG/ICO per entry: WPF's `BitmapDecoder` (pick the largest frame of an ICO). No library.

Core pattern (compiles and ran in the probe; `NativeMethods.json` below):

```csharp
unsafe static HBITMAP GetShellBitmap(string path, int px)
{
    PInvoke.SHCreateItemFromParsingName<IShellItemImageFactory>(path, null, out var f).ThrowOnFailure();
    try
    {
        f->GetImage(new SIZE { cx = px, cy = px },
                    SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_BIGGERSIZEOK, out HBITMAP hb);
        return hb;                      // caller: GetObject/GetDIBits, then DeleteObject
    }
    finally { f->Release(); }
}
```

**Not recommended:** `SHGetImageList` jumbo (two-step, fixed size classes, worse Store-app framing); `Icon.ExtractAssociatedIcon` / `System.Drawing` (32 px only); `Imaging.CreateBitmapSourceFromHIcon` (loses the 256 frame selection control). `Imaging.CreateBitmapSourceFromHBitmap` was not tested here - the `GetDIBits` path was, so use that.

### 2. CsWin32 vs. hand-written P/Invoke

**Decision: CsWin32 0.3.360, confined to `App/Interop`. Confidence: HIGH [probe].**

Rationale: the project needs a *COM-heavy* subset (IShellItemImageFactory, IShellLinkW, IPersistFile, later IDropTarget-adjacent shell items) plus ~15 flat APIs (SetWindowPos, window-style get/set, DPI, monitors, GDI). Hand-writing COM vtables/`GeneratedComInterface` for these is exactly the error-prone work CsWin32 removes, and it emits only what `NativeMethods.txt` names, so there is no runtime cost or dependency (`PrivateAssets=all`). Plain `[LibraryImport]` is fine for 2-3 flat calls but not for the COM surface.

Verified configuration (this exact combination compiled and ran):

```xml
<!-- App.csproj -->
<PropertyGroup><AllowUnsafeBlocks>true</AllowUnsafeBlocks></PropertyGroup>
<ItemGroup>
  <PackageReference Include="Microsoft.Windows.CsWin32" PrivateAssets="all" />
</ItemGroup>
```
```json
// NativeMethods.json
{ "$schema": "https://aka.ms/CsWin32.schema.json", "allowMarshaling": false, "useSafeHandles": false }
```
`allowMarshaling: false` generates blittable pointer/vtable COM structs (`IShellItemImageFactory*`, manual `Release()`), no RCW/`[ComImport]` marshalling. Wrap each use in `try/finally`. (The default marshalled mode was not evaluated; the verified mode is the recommended one.)

`NativeMethods.txt` entries confirmed to generate and compile: `SHCreateItemFromParsingName`, `IShellItemImageFactory`, `SHGetFileInfo`, `SHGetImageList`, `Windows.Win32.UI.Controls.IImageList`, `IMAGE_LIST_DRAW_STYLE`, `GetObject`, `BITMAP`, `GetDIBits`, `GetDC`, `ReleaseDC`, `DeleteObject`, `GetIconInfo`, `DestroyIcon`, `IShellLinkW`, `ShellLink`, `IPersistFile`, `STGM`, `CoCreateInstance`, `GetDpiForSystem`, `GetThreadDpiAwarenessContext`, `AreDpiAwarenessContextsEqual`. Expected for later phases (not yet probed): `SetWindowPos`, `HWND_BOTTOM`, `SWP_*`, `GetWindowLongPtr`/`SetWindowLongPtr`, `WS_EX_NOACTIVATE`/`TOOLWINDOW`/`TRANSPARENT`, `GetDpiForWindow`, `MonitorFromWindow`, `GetMonitorInfo`, `ShellExecuteEx` (verb `runas`), `SHObjectProperties`, `RegisterWindowMessage`.

CsWin32 pitfalls found [probe]:
- **Ambiguous names must be qualified** in `NativeMethods.txt` (`IImageList` exists in `Windows.Win32.UI.Controls` and `Windows.Win32.System.Mmc` -> error PInvoke007).
- Types you only *use* (`BITMAP`) must be listed too, otherwise a different, inaccessible `BITMAP` binds.
- **Never set `EmitCompilerGeneratedFiles` with an output path inside the project folder** - the `*.g.cs` files get globbed back in and produce hundreds of duplicate-type errors. Use an `obj/` path or leave it off.
- Always add `using Windows.Win32.System.Com;` etc. for COM helpers (`IPersistFile`, `CLSCTX`, `STGM`).

### 3. Tray icon

**Decision: H.NotifyIcon.Wpf 2.4.1 behind a 3-method `ITrayIcon` seam in `App/Tray`. Confidence: MEDIUM-HIGH.**

| Option | Cost / verdict |
|--------|----------------|
| WinForms `NotifyIcon` | `UseWindowsForms` raises the self-contained single-file exe **139.8 MB -> 173.0 MB (+33 MB)** [probe] because the WinForms assemblies get bundled; it also injects `global using System.Windows.Forms` + `System.Drawing`, which makes `Application` ambiguous in every WPF file (**CS0104, probe-verified**; needs `<Using Remove=...>`). Menu is a WinForms `ContextMenuStrip`, visually foreign to WPF. Reject. |
| **H.NotifyIcon.Wpf 2.4.1** | **+0.6 MB**; `TaskbarIcon` created + `ForceCreate()`/`Dispose()` ran without exception in the single-file build [probe; the visible tray glyph and menu popup were not inspected - verify in milestone 1]. WPF `ContextMenu`, MIT, depends only on `H.NotifyIcon`. |
| Hand-rolled `Shell_NotifyIcon` via CsWin32 | ~150 lines incl. hidden `HwndSource`, `TaskbarCreated` re-registration (Explorer restart), `WM_CONTEXTMENU` + foreground-window dance. Zero dependency but you re-implement known gotchas. **This is the fallback** if H.NotifyIcon ever misbehaves - hence the seam. |

Because the tray is the *emergency exit*, keep the seam so swapping is a one-file change. Use `Icon` from an embedded `.ico` resource. Do not adopt 2.5.0 betas.

### 4. `.lnk` parsing

**Decision: `IShellLinkW` + `IPersistFile.Load` via CsWin32, in `App/Interop`; **no** NuGet package. Confidence: HIGH [probe].**

Probe read a real `.lnk`: `GetPath` -> `C:\Windows\System32\notepad.exe`, `GetArguments` -> `foo.txt`, `GetWorkingDirectory` -> `C:\Windows`. Use `Load` only - **do not call `Resolve`** (it can search disks and show UI). For Store-app shortcuts `GetPath` returns empty (target lives in the property store) - which is why the concept stores the `.lnk` path and launches *that* via `ShellExecute`; the parsed values are only for pre-filling the properties dialog and the default `name` (file name without `.lnk`). Icon: do not use `GetIconLocation` - ask `IShellItemImageFactory` for the `.lnk` itself (verified correct icon, no arrow).

Reject: Securifybv.ShellLink (one release, 2019), WindowsAPICodePack (abandoned), `WScript.Shell` COM late binding (needs `dynamic`/type-library, slower, extra COM registration dependency).

### 5. RocketDock INI parsing

**Decision: hand-written tolerant parser in `Core/Skins` (~40 lines) returning `Dictionary<string, Dictionary<string,string>>` with case-insensitive comparers; typed accessors on top. Confidence: HIGH for the format seen, MEDIUM for unseen skins.**

Evidence from the real `CrystalXP.net` files (hex-inspected): plain ASCII, `CRLF`, **mixed spaces and tabs around `=`** (`Image =\t\tsep.png`), **no trailing newline**, hyphenated keys (`Outside-LeftMargin`). Required behavior:
- Trim key and value; split on the **first** `=`; case-insensitive section and key lookup.
- Skip blank lines and lines starting with `;` or `#` (assumed for other skins - unverified).
- Accept missing trailing newline, duplicate keys (last wins), unknown keys (ignored).
- Encoding: sniff BOM (UTF-8 / UTF-16 LE/BE); with no BOM decode as UTF-8, falling back to `Encoding.Latin1` on invalid bytes. Only one skin was available, so other skins' encodings are unverified - cover with tests using BOM and Latin-1 fixtures.
- Missing/invalid numeric -> documented default, never throw (concept: broken skin falls back to default background, log a warning).

Reject: `GetPrivateProfileString` (Win32 in Core breaks the architecture rule; ANSI/Unicode file-encoding quirks), `ini-parser-netstandard` 2.5.3 (netstandard-era, stale, brings its own object model), `Microsoft.Extensions.Configuration.Ini` 10.0.12 (pulls the Configuration stack for a 40-line job, and flattens sections to `Section:Key` strings).

### 6. JSON polymorphism with preserved unknown fields

**Decision: System.Text.Json with the **source generator** (`JsonSerializerContext`), `[JsonExtensionData]` on every model class, and a **custom `JsonConverter<DockItem>`** with an `UnknownItem` that keeps the raw element. Do NOT rely on `[JsonPolymorphic]`/`[JsonDerivedType]` alone. Confidence: HIGH [probe].**

What the probe proved about the attribute-only approach (the obvious choice in the concept):

| Scenario | Result |
|----------|--------|
| `"type"` not the first property in an item (hand-edited file) | **Exception** unless `AllowOutOfOrderMetadataProperties = true` (default false) |
| Unknown discriminator (`"type":"group"` from a newer build) with **abstract** base | **`NotSupportedException`** - the whole config fails to load |
| Unknown discriminator with `IgnoreUnrecognizedTypeDiscriminators = true` and a **concrete** base | Loads, but on save the item is written **without** `"type":"group"` -> **silent data loss / corrupted file** |

What the custom-converter design proved (full round trip, all [probe]):
- `"type"` accepted in any position; known types round-trip; **`"type":"group"` with nested `items` is written back byte-for-byte**; unknown fields at root, bar and item level survive via `[JsonExtensionData] Dictionary<string, JsonElement>`.
- `"type"` must be removed from a known item's extension data after reading and re-emitted first on write, otherwise it is duplicated.

```csharp
public abstract class DockItem { public string? Name { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; } }
public sealed class AppItem : DockItem { public string? Path { get; set; } /* args, workingDir, icon */ }
public sealed class UrlItem : DockItem { public string? Url { get; set; } }
public sealed class SeparatorItem : DockItem { }
public sealed class UnknownItem : DockItem { public JsonElement Raw { get; init; } }   // keeps future types verbatim

public sealed class DockItemConverter : JsonConverter<DockItem>
{
    public override DockItem? Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
    {
        using var doc = JsonDocument.ParseValue(ref r);
        var el = doc.RootElement.Clone();
        var ctx = (ConfigJsonContext)o.TypeInfoResolver!;            // source-gen context supplies type info
        DockItem? item = (el.TryGetProperty("type", out var tp) ? tp.GetString() : null) switch {
            "app"       => el.Deserialize(ctx.AppItem),
            "url"       => el.Deserialize(ctx.UrlItem),
            "separator" => el.Deserialize(ctx.SeparatorItem),
            _           => new UnknownItem { Raw = el, Name = el.TryGetProperty("name", out var n) ? n.GetString() : null } };
        item?.Extra?.Remove("type");
        return item;
    }
    public override void Write(Utf8JsonWriter w, DockItem v, JsonSerializerOptions o)
    {
        if (v is UnknownItem u) { u.Raw.WriteTo(w); return; }
        var ctx = (ConfigJsonContext)o.TypeInfoResolver!;
        var (tag, el) = v switch {
            AppItem a       => ("app", JsonSerializer.SerializeToElement(a, ctx.AppItem)),
            UrlItem x       => ("url", JsonSerializer.SerializeToElement(x, ctx.UrlItem)),
            SeparatorItem s => ("separator", JsonSerializer.SerializeToElement(s, ctx.SeparatorItem)),
            _ => throw new NotSupportedException() };
        w.WriteStartObject(); w.WriteString("type", tag);
        foreach (var p in el.EnumerateObject()) if (p.Name != "type") p.WriteTo(w);
        w.WriteEndObject();
    }
}
// Register on the OPTIONS instance: new ConfigJsonContext(new JsonSerializerOptions { Converters = { new DockItemConverter() }, ... })
// ([JsonSourceGenerationOptions(Converters=...)] is ignored when you pass your own options - probe-verified: it fails with "abstract type" error.)
[JsonSerializable(typeof(DockConfig))] [JsonSerializable(typeof(AppItem))] [JsonSerializable(typeof(UrlItem))] [JsonSerializable(typeof(SeparatorItem))]
public partial class ConfigJsonContext : JsonSerializerContext { }
```

Options to set: `WriteIndented = true`, `DefaultIgnoreCondition = WhenWritingNull` (the probe emitted `"name": null` for separators), `ReadCommentHandling = Skip`, `AllowTrailingCommas = true` (hand-editing friendly), `PropertyNamingPolicy = CamelCase`. Enums (`edge`, `align`, `layer`, `zoomAnchor`) as strings via `JsonStringEnumConverter` (generic `JsonStringEnumConverter<T>` for source-gen; not probed - check in milestone 1). Source generation is chosen over reflection for startup (< 1 s budget) and because the converter needs the context's type info; `PublishTrimmed` is impossible for WPF anyway, so this is a startup/robustness choice, not a trimming one.

Persistence rules (concept section 5): write to `config.json.tmp` (flush to disk), then `File.Replace(tmp, config.json, config.bak.json)`; first-ever save uses `File.Move`. `File.Replace` is the in-box atomic-ish NTFS `ReplaceFile` and produces the required backup in one step [docs; not exercised in the probe]. Debounce live-config autosaves (~300-500 ms) so slider drags do not write on every tick. Store paths *unexpanded* (`%USERPROFILE%\...`), expand with `Environment.ExpandEnvironmentVariables` at use time.

### 7. Single instance

**Decision: named `Mutex` in the `Local\` namespace, held in a static field for the process lifetime. Confidence: MEDIUM-HIGH (standard pattern; not probed).**

```csharp
static Mutex? _single;
protected override void OnStartup(StartupEventArgs e)
{
    _single = new Mutex(initiallyOwned: true, @"Local\MehrBar.SingleInstance.5D1F0B7E-...", out bool created);
    if (!created) { /* optional: signal first instance */ Shutdown(); return; }
    base.OnStartup(e);
}
```
- `Local\` (per logon session), not `Global\` - correct for fast user switching and needs no privileges.
- Checking in `OnStartup` is fine: only the *second* launch pays the WPF-init cost (~100-200 ms [unverified]); a custom `Main` is unnecessary complexity.
- Optional nicety: the second instance sets a named `EventWaitHandle`; the first opens the config window. Not required by V1.
- Reject: `Process.GetProcessesByName` (race, matches renamed exes and other users' sessions), `Microsoft.VisualBasic.ApplicationServices` (pulls the VB runtime), third-party single-instance packages.

### 8. Logging with rotation

**Decision: tiny hand-written logger in `Core` (no package). Confidence: MEDIUM-HIGH.**

Spec: `IAppLog { Debug/Info/Warn/Error(string, Exception?) }` + `RollingFileLog` writing to `%APPDATA%\MehrBar\logs\mehrbar.log`; **size-based rotation** (1 MB, keep 4 -> `mehrbar.1.log` ... `mehrbar.4.log`), checked at startup and per write; one lock-guarded `StreamWriter` opened with `FileShare.ReadWrite` (lets the developer tail it) and `AutoFlush = true` (crash-safe); ISO-8601 timestamps; never throws (logging must not crash the dock). Wire `AppDomain.UnhandledException`, `Dispatcher.UnhandledException`, `TaskScheduler.UnobservedTaskException` to it. Testable with a temp directory. Embed PDBs (`<DebugType>embedded</DebugType>`) so logged stack traces carry line numbers in the single-file exe [docs].

| Option | Verdict |
|--------|---------|
| **Hand-rolled** | Chosen. Matches "simple rotation", keeps Core dependency-free. |
| Serilog 4.4.0 + Serilog.Sinks.File 7.0.0 | **Fallback.** Apache-2.0, mature. Cost: two packages; Sinks.File 7.0.0 (2025-04) has no `net10.0` TFM (uses its `net9.0` asset - works); Serilog 4.4.0 targets `net10.0`. Adopt only if structured/async logging becomes a need. |
| NLog 6.2.1 | BSD-3, actively maintained, but a larger configuration surface than this app needs. |
| Microsoft.Extensions.Logging | Abstractions only - no file provider in the box, so you still need a third-party sink. Reject. |

### 9. Autostart

**Decision: per-user registry Run key `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `MehrBar` = quoted `Environment.ProcessPath`. Confidence: HIGH for choice, MEDIUM for edge behavior.**

- No admin rights, no extra dependency (`Microsoft.Win32.Registry` is in-box), trivially reversible; the "Mit Windows starten" checkbox maps 1:1 to value present/absent.
- **Quote the path** and use `Environment.ProcessPath` (not `Assembly.Location`, which is empty in single-file [probe]). The Run value is a command line, documented limit 260 chars [docs].
- **Self-heal:** when the setting is on, rewrite the value at each start so a moved/renamed exe does not leave a dead entry.
- **Respect Task Manager:** Task Manager's disable toggle lives in `HKCU\...\Explorer\StartupApproved\Run` (binary value; first byte `02` = enabled, otherwise disabled [community]). Read it and show the checkbox as "disabled by Windows" instead of fighting it; never write that key.
- **Expect a delay:** Microsoft documents that the system may delay Run-key programs to avoid hurting the foreground experience [docs]; the dock may appear several seconds after logon (RocketDock behaves the same). Do **not** touch the global `Explorer\Serialize\StartupDelayInMSec` tweak.
- **Task Scheduler rejected:** needs the Task Scheduler COM API or shelling out to `schtasks.exe` (or a library), and its main advantage (elevation, precise delay) is *undesirable* here - a dock running elevated cannot receive drag & drop from a normal-integrity Explorer (UIPI) [unverified for this app; standard Windows behavior]. Startup-folder shortcut rejected: needs `IShellLink` creation and offers nothing over the Run key.

### 10. Per-Monitor-DPI v2 manifest for WPF

**Decision: embed an application manifest with `<dpiAwareness>PerMonitorV2</dpiAwareness>`. Confidence: HIGH [probe].**

Probe: `AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), PER_MONITOR_AWARE_V2)` returned **True** with this manifest in the self-contained single-file exe and **False** in the control build without it. Microsoft recommends the manifest over the API call [docs].

```xml
<!-- src/MehrBar.App/app.manifest -->
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />   <!-- Windows 10 / 11 -->
    </application>
  </compatibility>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true</dpiAware>
      <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">true</longPathAware>
    </windowsSettings>
  </application>
</assembly>
```
```xml
<ApplicationManifest>app.manifest</ApplicationManifest>
```
(`dpiAware=true` is the pre-1607 fallback; on Windows 10 1607+ it is ignored when `dpiAwareness` is present [docs]. The tested manifest has no `trustInfo` block, so the exe runs `asInvoker`; if you add one, keep `requestedExecutionLevel level="asInvoker"` - the dock must never run elevated.) Do **not** call `SetProcessDpiAwareness*` from code.

WPF consequences: WPF measures in DIPs and rescales per monitor; read the scale with `VisualTreeHelper.GetDpi(visual)` and react in `OnDpiChanged`. Because `GetImage` is requested at 256 px and drawn scaled, icons stay sharp at 125/150 % without re-extraction. Set `UseLayoutRounding`/`SnapsToDevicePixels` deliberately on the custom control; the 9-slice skin should be snapped to device pixels, not DIPs.

### 11. Single-file publish caveats with WPF

**Decision: put the publish settings in `App.csproj` (not only on the command line) and include `IncludeNativeLibrariesForSelfExtract=true`. Confidence: HIGH [probe].**

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <PublishReadyToRun>true</PublishReadyToRun>        <!-- measure in milestone 6; see table -->
  <DebugType>embedded</DebugType>
  <ApplicationManifest>app.manifest</ApplicationManifest>
  <ApplicationIcon>Assets\mehrbar.ico</ApplicationIcon>
</PropertyGroup>
```

Measured on the probe app (empty WPF window + shell-icon work, self-contained win-x64):

| Variant | Result [probe] |
|---------|----------------|
| `PublishSingleFile` only (**the command in the concept**) | `.exe` 131.6 MB **plus 5 loose files**: `D3DCompiler_47_cor3.dll`, `PenImc_cor3.dll`, `PresentationNative_cor3.dll`, `vcruntime140_cor3.dll`, `wpfgfx_cor3.dll`. **Not a single file.** |
| + `IncludeNativeLibrariesForSelfExtract=true` | **One 139.8 MB `.exe`** (+ `.pdb` unless embedded). Natives are extracted on first run to `%TEMP%\.net\<app>\<hash>\`. Warm run ~0.33-0.34 s, first run ~0.45 s (probe incl. icon work). |
| + `PublishReadyToRun=true` | 155.7 MB; warm 0.31-0.34 s (negligible on a tiny probe; the real app has more JIT work - **measure** cold start in milestone 6, keep if it helps). |
| + `EnableCompressionInSingleFile=true` | 69.5 MB, but **first run 5.4 s** and warm runs ~0.6 s (about 2x slower). **Reject** - violates the < 1 s start requirement. |
| `PublishTrimmed=true` | **Build error NETSDK1168**: WPF is not supported with trimming. Not an option (confirms: no trimming, no NativeAOT). |

Runtime caveats [probe unless noted]:
- `AppContext.BaseDirectory` = the exe's folder (also with native self-extract) -> use it, or `Path.GetDirectoryName(Environment.ProcessPath)`, for `Skins\`. `Assembly.Location` is **empty** and triggers analyzer warning IL3000; never use it [docs + probe].
- `Skins\` must stay a loose folder next to the exe: add it as `Content` with `CopyToOutputDirectory`/`CopyToPublishDirectory` (the build step that copies `samples/skins/`). Non-assembly content is not bundled by default; mark with `ExcludeFromSingleFile=true` to be explicit [docs; verify in milestone 3].
- The extraction folder is hash-keyed per build; each new exe build leaves a stale `%TEMP%\.net\MehrBar\<hash>` dir (the extracted natives, ~8 MB). Harmless, but a future installer/uninstaller should not assume the folder is absent.
- Do not expect `dotnet publish -c Release` to emit the bundle for `Core`/`Tests`: set the RID/self-contained properties **only in App.csproj**.
- Change the publish command in the concept to `dotnet publish src/MehrBar.App -c Release` (properties now live in the csproj); the old command line produces loose DLLs unless `-p:IncludeNativeLibrariesForSelfExtract=true` is added.
- Setting `InvariantGlobalization` or `SatelliteResourceLanguages` could trim size/start slightly; not evaluated - only try if startup misses the budget.

### 12. xUnit version for .NET 10

**Decision: `xunit.v3.mtp-v2` 4.0.1 on `net10.0`, `OutputType=Exe`, runner selected in `global.json`. Confidence: HIGH for CLI, MEDIUM for VS Code Test Explorer.**

Both variants were built and run on SDK 10.0.401 [probe]:

**A (recommended) - Microsoft Testing Platform**
```json
// global.json (repo root)
{ "sdk": { "version": "10.0.401", "rollForward": "latestFeature" },
  "test": { "runner": "Microsoft.Testing.Platform" } }
```
```xml
<!-- MehrBar.Tests.csproj -->
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
<ItemGroup>
  <Using Include="Xunit" />
  <PackageReference Include="xunit.v3.mtp-v2" />
  <ProjectReference Include="..\MehrBar.Core\MehrBar.Core.csproj" />
</ItemGroup>
```
`dotnet test` -> 3/3 passed in ~1 s. (The xunit.net getting-started page names the core package `xunit.v3.core.mtp-v2`; the `xunit.v3.mtp-v2` meta-package used by the official template also resolves and works.)

**B (verified fallback) - VSTest mode**, if C# Dev Kit's Test Explorer misbehaves with MTP: delete the `test` block from `global.json` and use
`Microsoft.NET.Test.Sdk` 18.10.1 + `xunit.v3.mtp-off` 4.0.1 + `xunit.runner.visualstudio` 4.0.0 -> 3/3 passed. A three-line change.

Why A is default: MTP is the xUnit v3 / .NET 10 default direction; on SDK 10 an MTP-v2 project can *only* run in MTP mode and needs the `global.json` opt-in or `dotnet test` errors with "VSTest is not supported" [docs]. Why the caveat: the developer works in VS Code + C# Dev Kit, and Microsoft's own VS Code testing page does not mention MTP/xUnit v3; 2026 community reports describe Test Explorer hangs with xUnit v3 MTP in *Visual Studio* [community, LOW]. **Action: in milestone 1, open the Test Explorer once; if tests are not discovered, switch to B.** GSD agents and CI use `dotnet test` and are unaffected either way.

Test hygiene: use xUnit's built-in `Assert` (with `Assert.Equal(expected, actual, precision)` for the zoom maths). **Avoid FluentAssertions 8.x** (license moved to a commercial model with v8, 2025) [community/MEDIUM]; AwesomeAssertions 9.6.0 (Apache-2.0 fork) exists if fluent style is wanted, but it is unnecessary here. Avoid Moq (SponsorLink history); Core's seams (`ILauncher`, `IAppLog`, file system) get hand-written fakes. 4.0 breaking changes are mostly extensibility APIs; ordering is now layered and parallelization opt-outs cannot be re-enabled lower down [docs] - irrelevant for a small pure-logic suite.

---

## Installation

```bash
# One-time repo scaffolding (pins SDK + selects MTP runner)
# global.json: see section 12;  Directory.Packages.props: central versions below

# Core (net10.0): NO package references

# App (net10.0-windows)
dotnet add src/MehrBar.App package Microsoft.Windows.CsWin32 --version 0.3.360
dotnet add src/MehrBar.App package H.NotifyIcon.Wpf --version 2.4.1

# Tests (net10.0, OutputType Exe)
dotnet add src/MehrBar.Tests package xunit.v3.mtp-v2 --version 4.0.1

# VSTest fallback only
# dotnet add src/MehrBar.Tests package Microsoft.NET.Test.Sdk --version 18.10.1
# dotnet add src/MehrBar.Tests package xunit.v3.mtp-off --version 4.0.1
# dotnet add src/MehrBar.Tests package xunit.runner.visualstudio --version 4.0.0
```

```xml
<!-- Directory.Packages.props -->
<Project>
  <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.Windows.CsWin32" Version="0.3.360" />
    <PackageVersion Include="H.NotifyIcon.Wpf" Version="2.4.1" />
    <PackageVersion Include="xunit.v3.mtp-v2" Version="4.0.1" />
  </ItemGroup>
</Project>
```
(With central package management the project-level `PackageReference` items carry no `Version`; the `dotnet add package` lines above are the one-time bootstrap, after which versions live only in `Directory.Packages.props`.)

Release: `dotnet publish src/MehrBar.App -c Release` (all settings in `App.csproj`).

---

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

**If C# Dev Kit Test Explorer does not discover MTP tests:**
- Switch Tests to the VSTest fallback (xunit.v3.mtp-off + runner.visualstudio + Test.Sdk, remove `test.runner` from `global.json`).
- Because it is verified working and a three-line change; CLI results are identical.

**If H.NotifyIcon.Wpf misbehaves (popup focus, Explorer restart):**
- Replace the `ITrayIcon` implementation with a ~150-line `Shell_NotifyIcon` class in `App/Interop`.
- Because the tray is the emergency exit and must be dependable; the seam makes this local.

**If cold start exceeds 1 s on the real app:**
- First enable/verify `PublishReadyToRun`, then source-generated JSON (already chosen), then try `InvariantGlobalization`/`TieredPGO=false`; never compression.
- Because those are the measured/known levers; compression was measured to hurt.

**If a later phase needs more Win32 (Win+D survival, hit-testing, AppBar):**
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

---
*Stack research for: RocketDock-style Windows launcher dock (.NET 10 / WPF)*
*Researched: 2026-10-09*
