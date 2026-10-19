# Phase 2: Grundgerüst - Pattern Map

**Mapped:** 2026-10-19
**Files analyzed:** 52 (neu) plus 2 geänderte (`Directory.Packages.props`, `MehrBar.sln`)
**Analogs found:** 31 / 52 (21 ohne Code-Analog, siehe unten; dort gilt RESEARCH.md)

**Wichtig vorab:**
- `src/` enthält heute nur `Core/Layout/ZoomEngine.cs`, `ZoomKernel.cs`, `AssemblyMarker.cs` und Tests für den Zoom. Fast alles in Phase 2 ist neu; die Analoga stammen deshalb zu großen Teilen aus `spikes/`.
- **Spike-Code ist Wegwerfcode: als Muster lesen, nicht verlinken, nicht referenzieren, nicht kopieren (`spikes/README.md`: "No spike code is moved into src/ unreviewed").** Die Spikes haben ein eigenes `spikes/Directory.Packages.props`; das Produkt hat nie eine `ProjectReference` oder `Compile Include` auf `spikes/`.
- Tracked-Source-Prüfung: alle genannten Analoga sind per `git ls-files` getrackt (Stichprobe `spikes/Icons/ShellIcon.cs`; Liste von `git ls-files` geprüft). Es gibt kein gitignored Mirror-Verzeichnis in den Pfaden.
- Zeilennummern beziehen sich auf den Stand vom 2026-10-19.

## File Classification

Pfade relativ zu `src/`. Match: exact = gleiche Rolle und Datenfluss; role = gleiche Rolle; partial = anderer Zweck, gleiches Muster; none = kein Analog.

### Core (`MehrBar.Core`, net10.0, keine Pakete)

| Neue Datei | Rolle | Datenfluss | Nächstes Analog | Match |
|---|---|---|---|---|
| `Core/Model/*.cs` (DockConfig, GeneralConfig, BarConfig, LabelConfig, DockItem + AppItem/FolderItem/UrlItem/SeparatorItem/UnknownItem) | model | CRUD | `Core/Layout/ZoomEngine.cs` Z.20-47 (`record`/`readonly record struct`, XML-Doku, Namespace) | partial (Stil), JSON-Teil: none, RESEARCH Pattern 1 |
| `Core/Config/Defaults.cs`, `ConfigNormalizer.cs` | utility | transform | `Core/Layout/ZoomEngine.cs` (statische reine Klasse, `ArgumentOutOfRange...`-Guards Z.81-83) | partial |
| `Core/Config/ConfigSerializer.cs`, `ConfigJsonContext`, `DockItemConverter.cs` | utility/model | transform | none | none, RESEARCH Pattern 1 |
| `Core/Config/ConfigStore.cs`, `IConfigPersistence.cs`, `ConfigLoadResult.cs` | service | CRUD, event-driven (`Changed`) | none | none, RESEARCH Pattern 2 |
| `Core/Config/FileConfigPersistence.cs` | service | file-I/O | none | none, RESEARCH Pattern 2 |
| `Core/Layout/BarMetrics`, `RestLayout(Builder)`, `FrameLayout`, `AxisFrame` | utility | transform | `Core/Layout/ZoomEngine.cs` (`Compute`, `RestItem`, `ZoomParams`) | exact (liefert die Eingabe-/Ausgabetypen) |
| `Core/Layout/BarPlacement.cs`, `WindowPlan.cs` | utility | transform | `Core/Layout/ZoomEngine.cs` + `Tests/Fixtures/RocketDockReference.cs` (Maße) | role |
| `Core/Layout/HitTest.cs` | utility | transform | `Core/Layout/ZoomEngine.cs` | role |
| `Core/Drop/DropClassifier`, `DropEffectChooser`, `IFileSystem` | utility | transform | `Core/Layout/ZoomKernel.cs` (reine statische Funktion) | partial |
| `Core/Launch/LaunchResolver`, `LaunchPlan`, `IShell` | utility | request-response | none | none, RESEARCH Pattern 8 |
| `Core/Shell/IconFrameHeuristic`, `PathText` | utility | transform | `spikes/Icons/IconMetrics.cs` Z.82-90 (Eckenalpha) | role (Spike, Logik neu in Core) |
| `Core/Windowing/ZOrderPolicy`, `FrontMode` | utility (Zustandsautomat) | event-driven | `spikes/WindowHost/Variants/ZOrderGuard.cs` Evaluate/Lift/Drop Z.272-347 (Logik als Muster) | role |
| `Core/Logging/IAppLog`, `RollingFileLog` | service | file-I/O | none | none, RESEARCH "Rotierendes Log" |

### App (`MehrBar.App`, net10.0-windows)

| Neue Datei | Rolle | Datenfluss | Nächstes Analog | Match |
|---|---|---|---|---|
| `App/MehrBar.App.csproj` | config | - | `spikes/WindowHost/WindowHost.csproj` + RESEARCH "App.csproj" | exact |
| `App/app.manifest` | config | - | `spikes/WindowHost/app.manifest` | exact |
| `App/Interop/NativeMethods.json` | config | - | `spikes/Common/NativeMethods.json` | exact |
| `App/Interop/{Window,Events,Menu,Monitor,Shell}/NativeMethods.txt` | config | - | `spikes/Common/native/{base,host,...}/NativeMethods.txt`, `spikes/Icons/NativeMethods.txt` | exact |
| `App/Interop/WindowStyles.cs` | utility | request-response | `spikes/WindowHost/HostWindow.cs` Z.143-159 | role |
| `App/Interop/ZOrderKeeper.cs` | service | event-driven | `spikes/WindowHost/Variants/ZOrderGuard.cs` (S2) | exact (Logik); instanzbasiert statt `static s_active` |
| `App/Interop/DesktopHost.cs` | utility | request-response | `spikes/Common/DesktopHost.cs` | exact |
| `App/Interop/Monitors.cs` | utility | request-response | `spikes/Common/native/host/NativeMethods.txt` (`MonitorFromWindow`, `GetMonitorInfo`) | partial |
| `App/Interop/ShellIcons.cs` | utility | file-I/O | `spikes/Icons/ShellIcon.cs` | exact |
| `App/Interop/ForegroundScope.cs` | utility | request-response | `spikes/WindowHost/Variants/MenuNative.cs` Z.131-181 | role |
| `App/Bars/BarWindow.cs` | component | event-driven | `spikes/WindowHost/HostWindow.cs` | exact |
| `App/Bars/BarSurface.cs` | component | transform (Zeichnen) | `spikes/WindowHost/Variants/PlateH1.cs` (Platte) + `Variants/RenderCost.cs` R1 | role |
| `App/Bars/BarController.cs` | controller | event-driven | none (`ConfigStore.Changed` neu) | none |
| `App/Bars/NativeMenu.cs` | component | request-response | `spikes/WindowHost/Variants/MenuNative.cs` | exact |
| `App/Bars/DropTarget.cs` | component | event-driven | none (nur Snippet in RESEARCH "Drop-Handler") | none |
| `App/Bars/DialogOwner.cs`, `App/Dialogs/MissingTargetWindow` | component | request-response | none (RESEARCH Pattern 9) | none |
| `App/Shell/IconCache.cs` | service | batch (Pool) | `spikes/Icons/ShellIcon.cs` (Einzelaufruf, Thread je Aufruf) | role |
| `App/Shell/ShellLauncher.cs` | service | request-response | none | none |
| `App/Tray/ITrayIcon.cs`, `HNotifyTrayIcon.cs` | provider | event-driven | none | none, RESEARCH Pattern 10 |
| `App/Hosting/Program.cs`, `SingleInstance`, `AppHost`, `AppLifetime`, `CrashHandlers` | config/service | event-driven | `spikes/WindowHost/App.xaml.cs` | role |
| `App/Resources/Strings.resx`, `Strings.cs` | utility | transform | none (RESEARCH "Ressourcen ohne Codegenerator") | none |

### Tests (`MehrBar.Tests`) und Sonstiges

| Neue Datei | Rolle | Datenfluss | Nächstes Analog | Match |
|---|---|---|---|---|
| `Tests/Config/*Tests.cs`, `Layout/*Tests.cs`, `Windowing/*`, `Drop/*`, `Launch/*`, `Logging/*`, `Shell/*` | test | - | `Tests/ZoomAnchorTests.cs`, `Tests/ScaffoldTests.cs` | exact |
| `Tests/Fixtures/config.future.json` u. a. | test fixture | - | `Tests/Fixtures/RocketDockReference.cs` (Fixture-Ordner, `internal static`) | role |
| `Directory.Packages.props` (ändern) | config | - | selbst (Z.1-10) | exact |
| `MehrBar.sln` (ändern) | config | - | `dotnet sln add` | exact |
| `tools/phase2-smoke.ps1` (optional) | utility | batch | `spikes/README.md` (Nachmess-Befehle), `spikes/Driver/HostProcess.cs` | partial |

## Pattern Assignments

### `src/MehrBar.Core/**` allgemein (Stil, XML-Doku, Namespace)

**Analog:** `src/MehrBar.Core/Layout/ZoomEngine.cs`, `ZoomKernel.cs`

Namespace-Konvention `MehrBar.Core.<Ordner>` (Datei: `namespace MehrBar.Core.Layout;` file-scoped), `public`, englische XML-Doku, `ImplicitUsings`/`Nullable` an (`MehrBar.Core.csproj`, keine Pakete):

```csharp
// ZoomEngine.cs:20-47 (Typen, auf die Phase 2 aufbaut)
public readonly record struct ZoomParams(double IconSize, double ZoomSize, double Radius);
public readonly record struct RestItem(double Start, double Length, bool Zooms);
public sealed record ZoomResult(double ExtraBefore, double ExtraAfter,
    IReadOnlyList<double> DisplayedStarts, IReadOnlyList<double> DisplayedLengths);
```
```csharp
// ZoomEngine.cs:74-83 (Signatur und Argumentprüfung)
public static ZoomResult Compute(IReadOnlyList<RestItem> items, double pitch, ZoomParams p,
    double pointerU, ZoomAnchorMode anchor)
{
    ArgumentNullException.ThrowIfNull(items);
    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pitch, 0);
    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(p.IconSize, 0);
```

**Anwendung Layoutvertrag (D-04):** `FrameLayout.Compute(rest, ZoomParams, pointerU?, progress)` ruft `ZoomEngine.Compute` mit `p.ZoomSize` so, dass bei `progress = 0` `ZoomSize' = IconSize` (Wachstum 0, exakt Ruhepositionen). Nicht neu rechnen, nur umhüllen. `Pitch = IconSize = 32` stammt aus `Tests/Fixtures/RocketDockReference.cs:20,40`. Offene Frage 1 in RESEARCH (Konzept-Beispiel `spacing: 4`) vor dem Plan klären.
**Achsenneutral:** `BarPlacement`/`WindowPlan` rechnen entlang `AxisFrame`, nie mit `Width`/`Height` im Kern.

---

### `src/MehrBar.Core/Config/*` (model + service, CRUD + file-I/O)

**Analog:** keines im Repo. Verbindliche Vorlage ist RESEARCH.md Pattern 1 (Z.214-232) und Pattern 2 (Z.234-242). Der Planer kopiert von dort, nicht aus dem Code.

Die wichtigsten Stolperstellen (alle in RESEARCH belegt):
- `[JsonExtensionData] ... Extra { get; set; }`, **nie `init`** (Laufzeitfehler beim ersten Serializer-Zugriff). Ein Test serialisiert jeden Typ einmal.
- Kein `[JsonPolymorphic]`; eigener `DockItemConverter`, `UnknownItem(JsonElement)`; `type` an beliebiger Stelle lesbar, beim Schreiben zuerst.
- `File.Replace(tmp, cfg, bak)`, erster Save `File.Move`; 3 Wiederholungen 50/100/200 ms.
- Ungültiger Enum-Wert wirft sonst für die ganze Datei: tolerierender Enum-Konverter + Ladepolitik Datei → Backup → Defaults, `config.corrupt-<zeitstempel>.json`.

---

### `src/MehrBar.Tests/**/*Tests.cs` (test)

**Analog:** `src/MehrBar.Tests/ZoomAnchorTests.cs`, `ScaffoldTests.cs`, `MehrBar.Tests.csproj`

**Muster** (ZoomAnchorTests.cs:1-20): `using` auf Core-Namespace und `MehrBar.Tests.Fixtures`, `namespace MehrBar.Tests;` (bisher flach, für Phase 2 Unterordner `MehrBar.Tests.Config` usw. ist möglich, `FullyQualifiedName~`-Filter aus RESEARCH setzen genau das voraus), `public class XTests`, `[Fact]`, Konstanten oben mit Kommentar zur Toleranz, `Assert.InRange`/`Assert.Equal`; `using Xunit;` kommt über `<Using Include="Xunit" />` im csproj:
```csharp
// ZoomAnchorTests.cs:9-21
public class ZoomAnchorTests
{
    private const double Tolerance = 6;
    private static readonly ZoomParams Measured =
        new(RocketDockReference.IconSize, RocketDockReference.ZoomSize, RocketDockReference.MeasuredRadius);

    [Fact]
    public void Rest_layout_reproduces_reference_extent() { ... Assert.InRange(...) }
```
- Testnamen: `Snake_case_beschreibt_Verhalten` (so heute im Repo), Doku-Kommentar nennt die Anforderung (Q-Nummer). Für Phase 2 die Req-ID (z. B. `LEIS-02`) in den Summary-Kommentar.
- Fixtures: `internal static class` in `Tests/Fixtures/` (RocketDockReference.cs:17). JSON-Fixtures als Zeichenketten oder als Dateien mit `CopyToOutputDirectory` (neu zu entscheiden; heute keine Dateifixtures).
- Testprojekt: `OutputType Exe`, `net10.0`, drei Paketreferenzen ohne `Version` (zentral), `ProjectReference` auf Core. **Keine neuen Pakete, kein Test-Projekt für die App** (Logik steckt in Core).
- Dateisystem-Tests (`FileConfigPersistenceTests`, `RollingFileLogTests`): eigener Temp-Ordner je Test (`Path.Combine(Path.GetTempPath(), ...)`) und Aufräumen; das ist ein neues Muster, kein Analog im Repo.
- Zeitabhängiges (`FrontModeTests`): injizierte Uhr (`TimeProvider`), kein `Thread.Sleep`.
- Prüfen über **Anzahl** der gelaufenen Tests (01-SPIKE-tooling.md): Filter ohne Treffer meldet unter VSTest keinen Fehler.

---

### `src/MehrBar.App/MehrBar.App.csproj` (config)

**Analog:** `spikes/WindowHost/WindowHost.csproj` (Lesemuster) und RESEARCH "App.csproj" (Z.371-406, in der Probe gebaut und publiziert; maßgeblich).

Übernehmen aus dem Spike:
```xml
<OutputType>WinExe</OutputType>
<TargetFramework>net10.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
<ApplicationManifest>app.manifest</ApplicationManifest>
...
<PackageReference Include="Microsoft.Windows.CsWin32" PrivateAssets="all" />
...
<ProjectReference Include="..\..\src\MehrBar.Core\MehrBar.Core.csproj" />   <!-- Spike-Pfad; im Produkt: ..\MehrBar.Core\... -->
```
Abweichungen im Produkt (nicht aus dem Spike): kein `<Compile Include="..\Common\*.cs">`; `AdditionalFiles Include="Interop\**\NativeMethods.txt"` und `Interop\NativeMethods.json` statt `..\Common\native\**`; `RootNamespace MehrBar.App`, `AssemblyName MehrBar`, `NoWarn CA1416`, Release-only `RuntimeIdentifier`/`SelfContained`/`PublishSingleFile`/`IncludeNativeLibrariesForSelfExtract`; zusätzlich `H.NotifyIcon.Wpf`. `EnableCompressionInSingleFile` nie. Das SDK bindet `*.resx` automatisch ein (kein `EmbeddedResource Include`, sonst NETSDK1022). Kein `App.xaml` als `ApplicationDefinition`; eigener `Main`.

---

### `Directory.Packages.props` (Wurzel, ändern)

**Analog:** sich selbst (Z.1-10) und `spikes/Directory.Packages.props` (enthält den bisherigen CsWin32-Pin).

Vorhandenes Muster, nur Zeilen ergänzen (Reihenfolge alphabetisch wie heute):
```xml
<ItemGroup>
  <PackageVersion Include="H.NotifyIcon.Wpf" Version="2.4.1" />
  <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.10.1" />
  <PackageVersion Include="Microsoft.Windows.CsWin32" Version="0.3.360" />
  <PackageVersion Include="xunit.runner.visualstudio" Version="4.0.0" />
  <PackageVersion Include="xunit.v3.mtp-off" Version="4.0.1" />
</ItemGroup>
```
`spikes/Directory.Packages.props` bleibt unverändert (eigenständig, Kommentar dort: Löschen der Spikes lässt das Produkt unberührt). H.NotifyIcon.Wpf: `checkpoint:human-verify` vor der ersten Installation (RESEARCH, Package Legitimacy Audit).

---

### `src/MehrBar.App/Interop/**/NativeMethods.txt` und `NativeMethods.json` (config)

**Analog:** `spikes/Common/NativeMethods.json`, `spikes/Common/native/*/NativeMethods.txt`, `spikes/Icons/NativeMethods.txt`

`NativeMethods.json` unverändert übernehmen:
```json
{ "$schema": "https://aka.ms/CsWin32.schema.json", "allowMarshaling": false, "useSafeHandles": false }
```
Namen aus den Spike-Listen, die das Produkt braucht (jede Zeile nur in **einer** Liste; mehrdeutige qualifizieren, z. B. `Windows.Win32.UI.Controls.IImageList`):
- Fenster/Ebene (`native/base`, `native/host`): `WM_WINDOWPOSCHANGING`, `WINDOWPOS`, `SetWindowPos`, `GetWindowLong`, `SetWindowLong`, `HWND_BOTTOM`, `HWND_TOPMOST`, `HWND_NOTOPMOST`, `WINDOW_EX_STYLE`, `SET_WINDOW_POS_FLAGS`, `WINDOW_LONG_PTR_INDEX`, `GET_WINDOW_CMD`, `GetWindow`, `EnumWindows`, `FindWindowEx`, `GetClassName`, `IsWindowVisible`, `IsIconic`, `GetWindowThreadProcessId`, `DwmGetWindowAttribute`, `DWMWINDOWATTRIBUTE`, `MA_NOACTIVATE`, `WM_MOUSEACTIVATE`, `RegisterWindowMessage`, `WTSRegisterSessionNotification`, `WTSUnRegisterSessionNotification`, `AllowSetForegroundWindow`, `MonitorFromWindow`, `GetMonitorInfo`, `GetSystemMetrics`, `SetWindowRgn`/`CreateRectRgn` (nur H2-Rückfall).
- Ereignisse: `SetWinEventHook`, `UnhookWinEvent`, `EVENT_SYSTEM_FOREGROUND`, `WINEVENT_OUTOFCONTEXT`.
- Menü: `CreatePopupMenu`, `AppendMenu`, `TrackPopupMenuEx`, `DestroyMenu`, `PostMessage`, `TRACK_POPUP_MENU_FLAGS`, `MENU_ITEM_FLAGS`, `GetForegroundWindow`, `SetForegroundWindow`, `WM_NULL`.
- Symbole (`spikes/Icons/NativeMethods.txt`): `SHCreateItemFromParsingName`, `IShellItemImageFactory`, `SIIGBF`, `GetObject`, `BITMAP` (muss gelistet sein, sonst bindet ein unzugänglicher `BITMAP`), `GetDIBits`, `BITMAPINFO`, `DIB_USAGE`, `DeleteObject`, `GetDC`, `ReleaseDC`, `CoInitializeEx`, `CoUninitialize`, `COINIT`. `IShellLinkW`/`IPersistFile` sind Phase 5, **nicht** jetzt.
- Nicht übernehmen: Eingabe-/Oracle-Teile des Treibers (`SendInput`, `SetCursorPos`, `ChangeDisplaySettingsEx`, `LockWorkStation` ...).
- `EmitCompilerGeneratedFiles` nie ins Projekt (STACK).

---

### `src/MehrBar.App/app.manifest` (config)

**Analog:** `spikes/WindowHost/app.manifest` (komplett, 21 Zeilen). Für das Produkt 1:1 übernehmen: `asInvoker`, `supportedOS {8e0f7a12-...}`, `dpiAware true/pm`, `dpiAwareness PerMonitorV2`. Der Kommentar dort ("Normal integrity: UIPI and focus rules must match the real bar") bleibt sinngemäß. Kein `SetProcessDpiAwareness` im Code.

---

### `src/MehrBar.App/Bars/BarWindow.cs` (component, event-driven)

**Analog:** `spikes/WindowHost/HostWindow.cs` (**Spike, als Muster lesen, nicht verlinken**). Abweichungen: kein `HostOptions`/`RunLog`/`IHostVariant`/`MessageCounters`-Plumbing; Größe und Position kommen aus Core `WindowPlan`.

**Fenster-Eigenschaften** (HostWindow.cs:101-112, Konstruktor):
```csharp
WindowStyle = WindowStyle.None;
AllowsTransparency = true;                 // Spike: options.Transparent
Background = Brushes.Transparent;
ShowInTaskbar = false;
ShowActivated = false;
Topmost = false;
ResizeMode = ResizeMode.NoResize;
SnapsToDevicePixels = true;
UseLayoutRounding = true;
```
**OnSourceInitialized: Reihenfolge ist belegt** (HostWindow.cs:129-161):
```csharp
_source = (HwndSource)PresentationSource.FromVisual(this)!;
Hwnd = new HWND(_source.Handle);
double scale = _source.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
Width = pixelWidth / scale;  Height = pixelHeight / scale;
_root.LayoutTransform = new ScaleTransform(1.0 / scale, 1.0 / scale);   // 1 WPF-Einheit = 1 Gerätepixel

int exStyle = PInvoke.GetWindowLong(Hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
WINDOW_EX_STYLE wanted = WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
PInvoke.SetWindowLong(Hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle | (int)wanted);

PInvoke.SetWindowPos(Hwnd, HWND.HWND_BOTTOM, x, y, w, h,
    SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);   // endgültige Größe, vor dem ersten Bild
_source.AddHook(WndProc);
```
**WndProc: Nicht-Aktivierung** (HostWindow.cs:222-225):
```csharp
case PInvoke.WM_MOUSEACTIVATE:
    handled = true;
    return (IntPtr)PInvoke.MA_NOACTIVATE;
```
**Erst-Show-Umschreiben** (HostWindow.cs:237-249) ist im Spike ein Sonderpfad. **Im Produkt ersetzt ihn der `WM_WINDOWPOSCHANGING`-Haken des ZOrderKeeper**, der vor dem ersten Show in `OnSourceInitialized` angehängt wird (RESEARCH Pattern 4). Nicht beides übernehmen.

**Erstes Bild loggen** (HostWindow.cs:304-324): `CompositionTarget.Rendering += OnFirstRendering`, nach einmaligem Aufruf abmelden; Logzeile `first-frame zindex=… bottom=…` via `ZOrderProbe.Position`/`FirstBlockingWindowBelow` (HostWindow.cs:328-411). Die Probe-Hilfsklasse (`EnumWindows` mit `[UnmanagedCallersOnly]`, `DwmGetWindowAttribute(DWMWA_CLOAKED)`) als Muster für die `self-check`-/`first-frame`-Zeilen nutzen. Keine dauerhaften `CompositionTarget.Rendering`-Abonnements (Ruhezustand = 0 Frames).

**Neu gegenüber dem Spike (kein Analog):** `AllowDrop`/Drop-Ereignisse, Ein-/Ausblenden nativ (`ShowWindow SW_HIDE`/`SW_SHOWNA` plus `SetWindowPos HWND_BOTTOM`, nie `Show()`), `WM_DISPLAYCHANGE`/`WM_DPICHANGED` Neuplatzierung (RESEARCH Pattern 12), Größenänderung mit einem `SetWindowPos` für Position **und** Größe.

---

### `src/MehrBar.App/Interop/ZOrderKeeper.cs` (service, event-driven)

**Analog:** `spikes/WindowHost/Variants/ZOrderGuard.cs` Strategie S2 (**Spike**; Logik belegt, Struktur nicht übernehmen: `static s_active` erlaubt nur eine Leiste und Optionen-Plumbing entfällt).

**Konstanten wörtlich übernehmen** (ZOrderGuard.cs:35-44):
```csharp
private const int FastIntervalMs = 100;
private const int SlowIntervalMs = 250;
private const SET_WINDOW_POS_FLAGS PlaceFlags =
    SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER |
    SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOSENDCHANGING;
```
**Haken und Takt** (ZOrderGuard.cs:111-135):
```csharp
_taskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");
ctx.AddHook(WndProc);
_wtsRegistered = PInvoke.WTSRegisterSessionNotification(ctx.Hwnd, PInvoke.NOTIFY_FOR_THIS_SESSION);
_foregroundHook = PInvoke.SetWinEventHook(
    PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_SYSTEM_FOREGROUND, HMODULE.Null,
    &ForegroundEventProc, 0, 0,
    PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);
_timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(SlowIntervalMs) };
_timer.Tick += (_, _) => Evaluate("timer");
```
**Rückrufe dürfen nie Ausnahmen entlassen** (ZOrderGuard.cs:191-203):
```csharp
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
private static void ForegroundEventProc(HWINEVENTHOOK hook, uint evt, HWND hwnd, int idObject, int idChild, uint eventThread, uint eventTime)
{
    try { s_active?.Evaluate("foreground"); }
    catch { /* An exception must never leave an UnmanagedCallersOnly method. */ }
}
```
Produkt: `s_active` ersetzen durch `ConcurrentDictionary<HWND, ZOrderKeeper>` oder einen dienstweiten Keeper mit Liste der Leisten (RESEARCH Pattern 4).

**Umschreiben in WM_WINDOWPOSCHANGING** (ZOrderGuard.cs:229-264):
```csharp
if (message == PInvoke.WM_WINDOWPOSCHANGING) EnforceBottom((WINDOWPOS*)lParam);
else if (message == PInvoke.WM_DISPLAYCHANGE) Reassert("display-change");
else if (_taskbarCreated != 0 && message == _taskbarCreated) Reassert("taskbar-created");

private void EnforceBottom(WINDOWPOS* position)
{
    if (_lifted || position->flags.HasFlag(SET_WINDOW_POS_FLAGS.SWP_NOZORDER) || position->hwndInsertAfter == HWND.HWND_BOTTOM)
        return;
    position->hwndInsertAfter = HWND.HWND_BOTTOM;
}
```
Produkt: Bedingung `_lifted` wird zu "Zustand `Front` oder `DesktopLifted`" aus dem Core-`ZOrderPolicy`.

**Wächter-Auswertung** (ZOrderGuard.cs:272-308): Host frisch suchen; `shown = host.Found && (_lifted ? !HasOrdinaryWindowAbove(...) : IsAbove(bar, host))`; Übergänge `desktop-shown` → `Lift(host)` + Takt 100 ms, `desktop-hidden` → `Drop` + Takt 250 ms. Diese **Entscheidung** wandert in Core `ZOrderPolicy` (Eingabe `Observation`, Ausgabe `PlaceBottom|PlaceAboveHost|PlaceTopmost|DropFromTopmost|None`); der Keeper ist nur Beobachter und Ausführer.

**Direkt über den Host heben, nie topmost** (ZOrderGuard.cs:321-334):
```csharp
HWND insertAfter = HWND.HWND_NOTOPMOST;
for (HWND up = PInvoke.GetWindow(host.Hwnd, GET_WINDOW_CMD.GW_HWNDPREV); up != HWND.Null;
     up = PInvoke.GetWindow(up, GET_WINDOW_CMD.GW_HWNDPREV))
{
    var exStyle = (WINDOW_EX_STYLE)(uint)PInvoke.GetWindowLong(up, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
    if (up != _ctx!.Hwnd && !exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_TOPMOST)) { insertAfter = up; break; }
}
Place(insertAfter, "above-host", ...);
```
**Ende von "Lift" (S2)**: direkt `Place(HWND.HWND_BOTTOM, ...)` (ZOrderGuard.cs:337-342). Für `FrontMode` (topmost, LEIS-07) endet es dagegen **zweistufig**: erst `HWND_NOTOPMOST`, dann `HWND_BOTTOM` (ZOrderGuard.cs:345-346, "two-step" = gemessener Standard).
**Neu, ohne Analog:** `FrontMode` (30-s-Timer, Wächter ruht), Core-Zustandsautomat; Detach muss die Leiste auf `HWND_BOTTOM` zurücklegen (ZOrderGuard.cs:177-181 als Muster).

---

### `src/MehrBar.App/Interop/DesktopHost.cs` (utility, request-response)

**Analog:** `spikes/Common/DesktopHost.cs` (Spike). Fast 1:1 als Muster brauchbar, Namespace und `internal static unsafe class` anpassen.

**Frische Suche, kein Cache** (Z.30-53): `EnumWindows(&CollectWindow, (LPARAM)GCHandle.ToIntPtr(handle))` und je Fenster `FindWindowEx(window, HWND.Null, "SHELLDLL_DefView", null)`. Grund im Kommentar: Explorer-Neustart erzeugt den Host neu, ~18 `WorkerW`. Optionaler Cache nur mit `IsWindow`+Klassenprüfung und Verwerfen bei `TaskbarCreated`/`WM_DISPLAYCHANGE` (RESEARCH, in Phase 2 messen).

**"Gewöhnliches Fenster"** (Z.110-139): sichtbar, nicht minimiert, nicht cloaked, nicht topmost, nicht eigene Prozess-ID, Klasse nicht `Progman|WorkerW|Shell_TrayWnd|Shell_SecondaryTrayWnd`:
```csharp
string className = ClassOf(window);
return className is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd");
```
Die Eigene-PID-Regel (Z.130-135) muss bleiben: eigene Dialoge und Tray-Menü dürfen die Desktop-Erkennung nicht beeinflussen (RESEARCH Pitfall 6); als Core-`ZOrderPolicyTests` abdecken.
Rückruf: `[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])] static BOOL CollectWindow(HWND, LPARAM)` mit `GCHandle` auf `List<HWND>` (Z.141-150).

---

### `src/MehrBar.App/Bars/NativeMenu.cs` und `Interop/ForegroundScope.cs` (component, request-response)

**Analog:** `spikes/WindowHost/Variants/MenuNative.cs` Z.115-188 (M-C; Spike). Abweichung: `MenuCommon`, `MenuZWatcher`, Log-Zeilen, `_restoreFocus` als Schalter entfallen; **Fokus-Rückgabe und `HWND_BOTTOM` sind im Produkt Pflicht (D-03)**, im Spike optional.

**Reihenfolge belegt** (MenuNative.cs:123-186):
```csharp
HMENU menu = PInvoke.CreatePopupMenu();
try {
    PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, (nuint)itemId, text);
    HWND foregroundBefore = PInvoke.GetForegroundWindow();
    PInvoke.SetForegroundWindow(_owner);              // Pflicht laut TrackPopupMenu: Besitzer muss Vordergrund sein
    BOOL command = PInvoke.TrackPopupMenuEx(menu,
        (uint)(TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD | TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON | TRACK_POPUP_MENU_FLAGS.TPM_NONOTIFY),
        x, y, _owner, null);
    PInvoke.PostMessage(_owner, PInvoke.WM_NULL, default, default);   // sonst öffnet das nächste Menü und schließt sofort
    int id = command.Value;
    if (foregroundBefore != HWND.Null && foregroundBefore != _owner)
        PInvoke.SetForegroundWindow(foregroundBefore);                // Produkt: Pflicht, danach HWND_BOTTOM
} finally { PInvoke.DestroyMenu(menu); _tracking = false; }
```
Auslöser im Spike (Z.101-113): `WM_RBUTTONUP`, Position über `GetMessagePos`, nur innerhalb der Platte; Wiedereintritt per `_tracking`-Flag verhindern. Produkt: Treffer-Entscheidung (Leisten- vs. Eintragsmenü) über Core `HitTest` (Gesamtlisten-Index/Eintrags-Identität, RESEARCH Pitfall 10). Befehle mit eigenem Fenster (Dateiauswahl, Meldung): Vordergrund erst nach Schließen zurückgeben. `ForegroundScope` kapselt "merken, setzen, zurückgeben" als `IDisposable`.
Menütexte aus `Strings` (`Menu_AddEntry`, `Menu_OpenConfig`, `Menu_Remove`, `Menu_Exit`), nicht fest verdrahtet wie im Spike.

---

### `src/MehrBar.App/Bars/BarSurface.cs` (component, transform)

**Analog:** `spikes/WindowHost/Variants/PlateH1.cs` Z.42-72 (Platte) und `Variants/RenderCost.cs` R1 (OnRender-Grundmuster; nur Lesemuster, 606 Zeilen, vor Übernahme per `Grep "OnRender"` gezielt lesen).

**Platte** (PlateH1.cs:46, 55-60): Alpha-1-Fläche macht das transparente Fenster klickbar/drop-fähig:
```csharp
public static readonly Color PlateColor = Color.FromArgb(1, 0, 0, 0);
// Spike als Rectangle mit Fill = Frozen(PlateColor); Produkt: im OnRender zuerst
// dc.DrawRectangle(Frozen(PlateColor), null, barRect);     // barRect >= MinSlots (3) breit (D-08)
```
Schichten (RESEARCH Pattern 13): (1) Platte, (2) Standard-Hintergrund `#B3202020`, Radius 8, Rand 1 px `#33FFFFFF`, innen 6 px (Vorschlag im Rahmen "Claude's Discretion"), (3) Symbole 32x32 mit `RenderOptions.BitmapScalingMode = HighQuality`, (4) Platzhalter. `InvalidateVisual()` nur bei Änderung. Die Platte immer aus `BarRect` zeichnen, auch für die leere Leiste (Pitfall 8). Brushes `Freeze()`n (Spike nutzt `Frozen(...)`-Helfer).

---

### `src/MehrBar.App/Shell/IconCache.cs` und `Interop/ShellIcons.cs` (service, batch / file-I/O)

**Analog:** `spikes/Icons/ShellIcon.cs` (Spike; bei 188 Zeilen vollständig gelesen: Z.60-187). Abweichung: Spike nutzt einen Thread je Aufruf mit Zeitlimit (Z.60-64 `timeout`/`no-result`); Produkt = **2-4 dedizierte STA-Threads mit Warteschlange**, Zeitlimit 3 s je Eintrag, hängender Thread wird ersetzt (D-05).

**Kern pro Auftrag** (ShellIcon.cs:76-103):
```csharp
HRESULT co = PInvoke.CoInitializeEx(null, COINIT.COINIT_APARTMENTTHREADED);   // STA zwingend: MTA liefert für .url leere Seite (Q6)
uninitialize = co.Succeeded;                       // S_OK und S_FALSE brauchen CoUninitialize, RPC_E_CHANGED_MODE nicht
void* raw = null;  Guid iid = IShellItemImageFactory.IID_Guid;
fixed (char* path = parsingName) { hr = PInvoke.SHCreateItemFromParsingName(path, null, &iid, &raw); }
factory = (IShellItemImageFactory*)raw;
// CsWin32 projiziert GetImage als void und wirft COMException bei Fehler-HRESULT
factory->GetImage(new SIZE { cx = size, cy = size }, SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_BIGGERSIZEOK, &hbm);
```
**Aufräumen immer** (Z.123-139): `DeleteObject(hbm)`, `factory->Release()`, `CoUninitialize()` im `finally`.
**Pixel lesen** (Z.142-187): `GetObject` → `BITMAP`, `BITMAPINFO` mit `biHeight = -h` (top-down), `biBitCount = 32`, `GetDC(HWND.Null)`/`ReleaseDC`, `GetDIBits(..., DIB_USAGE.DIB_RGB_COLORS)`; Zeilenzahl `lines != h` = Fehler. Ergebnis: Straight-Alpha-BGRA.
**Danach im Produkt (nicht im Spike):** `BitmapSource.Create` als `Bgra32`, einmal nach `Pbgra32` konvertieren (`FormatConvertedBitmap`), `Freeze()`, Dispatcher-`BeginInvoke(InvalidateVisual)`. Schlüssel = expandierter Pfad. Fehler/Ziel fehlt: Platzhalter bleibt, nicht wiederholen. Eckenalpha-Prüfung (Ersatzregel 1): reine Funktion `IconFrameHeuristic` in Core, Vorlage `spikes/Icons/IconMetrics.cs:82-90`.
COM-Zeiger und `unsafe`: Klasse `internal static unsafe`, wie im Spike.

---

### `src/MehrBar.App/Hosting/Program.cs`, `AppHost.cs`, `CrashHandlers.cs` (service, event-driven)

**Analog:** `spikes/WindowHost/App.xaml.cs` (252 Zeilen, Spike; **nicht** vollständig gelesen, vor Einsatz gezielt per Grep nach `Mutex|DispatcherUnhandled|OnStartup|Shutdown` lesen). Das Produkt hat **kein `App.xaml`**, eigenes `[STAThread] Main` (RESEARCH Pattern 11). Mutex, Tray, Log, Unhandled-Exceptions-Wege haben **kein belegtes Analog im Repo**; Vorlagen sind RESEARCH Pattern 11 und STACK.md Abschnitt 7.

Drei Fehlerwege anschließen (RESEARCH APP-05): `Application.DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`; alle loggen und den Dock nie absichtlich beenden.

---

### `src/MehrBar.App/Tray/HNotifyTrayIcon.cs` (provider, event-driven)

**Analog:** keines im Repo. Vorlage RESEARCH Pattern 10 und "Tray mit Wiederholung" (Z.437-445):
```csharp
bool TryCreate() {
    try { _tray.ForceCreate(enablesEfficiencyMode: false); return true; }     // niemals ForceCreate() ohne Argument
    catch (Exception ex) { _log.Warn("tray: create failed, will retry", ex); return false; }
}
```
Wiederholung per `DispatcherTimer` (2 s, höchstens 30 Versuche); `Id` nicht setzen; `ITrayIcon` als 3-Methoden-Naht (Rückfall `Shell_NotifyIcon`).

---

### `src/MehrBar.App/Bars/DropTarget.cs` (component, event-driven)

**Analog:** keines im Repo (Spikes haben kein Drag and Drop). Vorlage: RESEARCH "Drop-Handler" (Z.421-435) und Pattern 7. Pflicht: `DropEffectChooser` (Core) liefert `Link > Copy > None`, **nie `Move`**; in `DragEnter` und `DragOver` setzen, `e.Handled = true`; `Drop` hält kurz; Anhängen per `ConfigStore.Update`, Duplikate erlaubt (D-12), danach `FrontMode` beenden.

---

### `src/MehrBar.App/Dialogs/MissingTargetWindow.cs`, `Bars/DialogOwner.cs`, `Resources/Strings.*`

**Analog:** keines. Vorlage RESEARCH Pattern 9 (verstecktes Hilfsfenster als Besitzer für `OpenFileDialog`, `DereferenceLinks = false`, `Multiselect = true`; Meldungsfenster `Topmost`, ohne Besitzer, `CenterScreen`) und "Ressourcen ohne Codegenerator" (Z.409-418, `ResourceManager("MehrBar.App.Resources.Strings", ...)`).

---

### `src/MehrBar.Core/Logging/RollingFileLog.cs`, `Launch/*`, `Windowing/*`

**Analog:** keines im Repo; Vorlagen in RESEARCH ("Rotierendes Log" Z.447-448; Pattern 8; Pattern 4/5). Test-getrieben schreiben (Wave 0 in RESEARCH Validation).

## Shared Patterns

### Win32 nur über CsWin32 in `App/Interop`
**Quelle:** `spikes/Common/NativeMethods.json`, `spikes/WindowHost/WindowHost.csproj`
**Gilt für:** alle Dateien in `MehrBar.App`, die `Windows.Win32` verwenden. Core enthält nie `using Windows.Win32`. Typisches `using`-Kopfstück der Spike-Dateien als Muster:
```csharp
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;   // + .Graphics.Dwm, .UI.Shell je nach Datei
```
`unsafe`-Klassen als `internal sealed unsafe class` / `internal static unsafe class`.

### Rückrufe aus Win32 (`UnmanagedCallersOnly`)
**Quelle:** `ZOrderGuard.cs:191-203`, `DesktopHost.cs:141-150`
**Gilt für:** WinEvent-Rückrufe, `EnumWindows`. Statische Methode, `[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]`, `try/catch` um alles, Zustand per `GCHandle` oder statischer Registratur, nie Exceptions nach außen.

### Fehlerbehandlung und Log
**Quelle:** Spike-Log-Muster `_log.Write(...)` (nur als Vorbild des Zeilenformats); Produkt: `IAppLog` (Core) neu.
**Gilt für:** App-Schicht und `ConfigStore`. Regeln: Logger wirft nie; Fehler beim Speichern/Start/Tray werden geloggt und der Dock arbeitet weiter; Pflichtzeilen siehe RESEARCH (`startup`, `config-loaded`, `bar-ready elapsed_ms`, `self-check`, `first-frame`, `desktop-shown/hidden`, `launch-failed`, `drop files=`).

### Zeitgesteuertes und Z-Order-sensibles Verhalten
**Quelle:** `ZOrderGuard.cs:35-44`, `DesktopHost.cs:20-24`
**Gilt für:** `BarWindow`, `NativeMenu`, Dialoge, `FrontMode`. Jede Z-Änderung der Leiste läuft über den Keeper; nie `Window.Show()/Hide()`; Dialoge nie mit der Leiste als Besitzer.

### Pfade und Umgebung
**Gilt für:** alle Dateien mit Dateizugriff. `Environment.GetFolderPath(ApplicationData)`, `Environment.ProcessPath`, `AppContext.BaseDirectory`; nie `Assembly.Location` oder das aktuelle Verzeichnis. Pfade im JSON bleiben **unexpandiert**, Expansion zur Nutzungszeit. Diagnose-Argumente `--data-dir`, `--exit-after-ms` für isolierte Handläufe.

### Sprache
Code, Kommentare, Commits Englisch (XML-Doku wie `ZoomEngine.cs`); UI-Texte Deutsch ausschließlich in `Resources/Strings.resx`.

### Tests
**Quelle:** `ZoomAnchorTests.cs`, `MehrBar.Tests.csproj`
Alles Entscheidbare ohne Fenster als Core-Test; Verifikation über Testanzahl; Quick-Run `dotnet test src/MehrBar.Tests --nologo --filter "FullyQualifiedName~<Teil>"`.

## No Analog Found

Planer nutzt RESEARCH.md (Verweis in Klammern).

| Datei | Rolle | Datenfluss | Grund |
|---|---|---|---|
| `Core/Config/ConfigSerializer`, `ConfigJsonContext`, `DockItemConverter`, Model-Records | model/utility | transform | kein JSON im Repo (Pattern 1) |
| `Core/Config/ConfigStore`, `FileConfigPersistence`, `ConfigLoadResult` | service | CRUD/file-I/O | keine Persistenz im Repo (Pattern 2) |
| `Core/Launch/*` | utility | request-response | (Pattern 8) |
| `Core/Logging/*` | service | file-I/O | (Rotierendes Log) |
| `Core/Windowing/ZOrderPolicy`, `FrontMode` | utility | event-driven | Logik nur im Spike-Keeper verflochten; als Zustandsautomat neu (Pattern 4/5) |
| `App/Bars/DropTarget` | component | event-driven | Spikes ohne DnD (Pattern 7) |
| `App/Bars/BarController`, `Shell/ShellLauncher` | controller/service | event-driven | neu |
| `App/Tray/*` | provider | event-driven | (Pattern 10) |
| `App/Hosting/SingleInstance`, `AppLifetime` | service | event-driven | (Pattern 11, STACK Abschnitt 7) |
| `App/Dialogs/*`, `DialogOwner`, `Resources/Strings.*` | component/utility | request-response | (Pattern 9, Ressourcen) |
| `App/Assets/mehrbar.ico` | asset | - | eigene Zeichnung, nichts Fremdes ins Repo |

## Metadata

**Analog search scope:** `src/` (komplett, 10 getrackte Dateien), `spikes/WindowHost`, `spikes/Icons`, `spikes/Common` (inkl. `native/*/NativeMethods.txt`), `spikes/Directory.Packages.props`, `Directory.Packages.props`, alle csproj.
**Gelesen:** HostWindow.cs (komplett), ZOrderGuard.cs Z.30-360, MenuNative.cs Z.100-200, PlateH1.cs Z.25-85, ShellIcon.cs Z.60-187, DesktopHost.cs Z.15-150, ZoomEngine.cs Z.1-83, ZoomAnchorTests.cs Z.1-40, ScaffoldTests.cs, drei csproj, Manifest, NativeMethods-Listen.
**Nicht gelesen (nur per Verweis, vor Einsatz gezielt lesen):** `RenderCost.cs`, `App.xaml.cs`, `IconMetrics.cs`, `spikes/Driver/*`, `ZOrderGuard.cs` ab Z.360 (Reassert, Place).
**Pattern extraction date:** 2026-10-19
