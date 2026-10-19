# Phase 2: Grundgerüst - Research

**Researched:** 2026-10-19
**Domain:** C# / .NET 10 / WPF — nicht aktivierbares, transparentes Leistenfenster ganz unten (Win32-Interop über CsWin32), JSON-Konfiguration mit Vorwärtskompatibilität, OLE-Drop aus dem Explorer, natives Kontextmenü, Tray, Einzeldatei-Publish
**Confidence:** HIGH für Konfiguration, Paket-/Publish-Gerüst, Tray-API und Datei-Semantik (alles hier gebaut oder gemessen); MEDIUM für Drop auf die Alpha-1-Platte, Start per Klick aus dem Hintergrund und Menü-Wächter-Zusammenspiel (aus Phase-1-Messungen abgeleitet, am Produkt nicht gemessen — braucht Handprüfung am Desktop)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Übernommen aus Phase 1 (`01-SPIKE-ERGEBNISSE.md`, gesperrt)**
- **D-01:** Fensterebene über `ZOrderKeeper` nach Strategie S2: jede Z-Änderung in `WM_WINDOWPOSCHANGING` auf `HWND_BOTTOM`, Wächter mit `EVENT_SYSTEM_FOREGROUND` plus Takt 250 ms (100 ms, solange der Desktop gezeigt wird), bei gezeigtem Desktop direkt über den Desktop-Host, nie topmost; Host neu suchen bei `TaskbarCreated` und `WM_DISPLAYCHANGE`.
- **D-02:** Trefferfläche als Alpha-1-Platte unter den Symbolen; `TOOLWINDOW|NOACTIVATE`, `MA_NOACTIVATE`, `HWND_BOTTOM` vor dem ersten Bild. Rückfall H2 (Region).
- **D-03:** Kontextmenüs nativ nach M-C (`TrackPopupMenuEx`, Leiste als Besitzer, `SetForegroundWindow` davor, `WM_NULL` danach); vorheriges Vordergrundfenster ausdrücklich zurückgeben, Leiste danach auf `HWND_BOTTOM`.
- **D-04:** Zeichnen mit R1 (`OnRender`, alles neu) im transparenten WPF-Fenster, Hardware; Core-Layout über `ZoomEngine` mit Zoom-Fortschritt fest 0 (Layoutvertrag für Phase 3).
- **D-05:** IconCache auf 2–4 STA-Arbeitsthreads, `SIIGBF_ICONONLY`, einmal nach `Pbgra32` + `Freeze()`, Zeitlimit 3 s je Eintrag, Platzhalter während des Ladens.
- **D-06:** Tests mit xUnit v3 über VSTest (Rückfall B, seit 2026-10-19).

**Aussehen der Leiste ohne Skin**
- **D-07:** Hintergrund in Phase 2 ist der im Konzept vorgesehene **einfache halbtransparente Standard-Hintergrund** (später Rückfall, wenn ein Skin fehlt oder defekt ist).
- **D-08:** Die **leere Leiste** erscheint als kurzer Kasten in der Breite von etwa drei Symbolen, **ohne Hinweistext**.
- **D-09:** Symbole in **Grundgröße 32 px** wie bei RocketDock (Stephans bisherige Leiste).

**Klick auf einen Eintrag mit fehlendem Ziel**
- **D-10:** Es erscheint ein **Meldungsfenster** mit dem Fehler und den Knöpfen **„Entfernen“** und **„Behalten“**; „Entfernen“ löscht den Eintrag wie das Kontextmenü (inklusive Backup beim Speichern). Zusätzlich ein Eintrag im Log. Texte deutsch in Ressourcen-Dateien.

**Entfernen und Duplikate**
- **D-11:** „Entfernen“ im Eintrags-Kontextmenü löscht **sofort, ohne Rückfrage** (wie RocketDock); das Backup vor jedem Speichern fängt einen Fehlgriff ab.
- **D-12:** **Duplikate sind erlaubt:** Ein Programm, das schon auf der Leiste liegt, wird beim erneuten Hineinziehen oder Hinzufügen ein zweites Mal angehängt.

### Claude's Discretion
- Farbe, Deckkraft und Eckenradius des Standard-Hintergrunds; Abstand der Symbole; Wortlaut der Meldung (Phase 4 ersetzt den Hintergrund durch Skins).
- Ob das Meldungsfenster ein WPF-Fenster oder eine Windows-Meldung ist — Hauptsache, es nimmt der Leiste nicht dauerhaft die Ebene „ganz unten“.

### Deferred Ideas (OUT OF SCOPE)
- Unaufdringliche Startfehler-Meldung als Feinschliff bleibt Phase 6; in Phase 2 gilt das Meldungsfenster aus D-10.
- Offen aus Phase 1 und hier zu prüfen, aber keine neue Fähigkeit: echte Netzdatei für Symbole, 125/150 % Skalierung, Symbole mit 64/128-px-Rahmen.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description (aus REQUIREMENTS.md) | Research Support |
|----|-----------------------------------|------------------|
| LEIS-01 | Beim ersten Start ohne Konfiguration erscheint eine leere Leiste oben mittig mit den Standardwerten (Q2, Q4) | `Defaults` in Core (eine Stelle), `config.json` wird beim ersten Start geschrieben (sonst hat „Konfiguration öffnen“ nichts zu öffnen); Abschnitt „Konfiguration“ |
| LEIS-02 | Die Leiste lässt sich am Rand ausrichten (Anfang/Mitte/Ende), entlang des Rands versetzen und mit `edgeOffset` vom Rand abrücken, auch negativ (Q7) | `BarPlacement`/`WindowPlan` in Core (reine Rechnung, vier Ränder), Fensterplatzierung in physischen Pixeln per `SetWindowPos`; Konvention für `offset` ist offene Frage 2 |
| LEIS-03 | Die Leiste liegt immer ganz unten in der Fensterreihenfolge, direkt über dem Desktop; jedes geöffnete Fenster überdeckt sie (Q10) | `ZOrderKeeper` S2 (instanzbasiert, nicht die statische Spike-Fassung) + Core-`ZOrderPolicy`-Zustandsautomat; Abschnitt „ZOrderKeeper“ |
| LEIS-04 | Ein Klick auf die Leiste holt sie nicht nach vorn und nimmt dem aktiven Fenster nicht den Fokus (Q10) | `WS_EX_TOOLWINDOW\|WS_EX_NOACTIVATE`, `MA_NOACTIVATE` (HostWindow.cs:143-145, 222-225); Menü-Sonderfall `ForegroundScope` |
| LEIS-05 | Die Leiste ist nach Win+D / „Desktop anzeigen“, nach einem Explorer-Neustart und nach einem Auflösungswechsel weiter sichtbar und an ihrem Platz | S2-Wächter + Neupositionierung bei `WM_DISPLAYCHANGE` (Core-Rechnung mit neuem Monitorrechteck), `TaskbarCreated` |
| LEIS-06 | Klicks neben die sichtbare Leiste (auch in den Bereich, in den gezoomte Symbole ragen könnten) erreichen den Desktop bzw. das Fenster darunter | Fenster = Ruhebreite + maximales Zoom-Wachstum, nur die Alpha-1-Platte/der Hintergrund fängt Klicks (Q1: Überhang-Spalt 20/20); `WindowPlan.MaxGrowth` aus `ZoomEngine` |
| LEIS-07 | Über das Tray-Menü „Leiste nach vorn“ steht die Leiste vorn, bis etwas abgelegt wurde oder 30 s vergangen sind (Q17) | `FrontMode` im Keeper (topmost, Wächter ruht, 30-s-Timer, Ende bei Drop/Timer/Beenden); Abschnitt „Leiste nach vorn“ |
| ITEM-01 | Ein Klick auf ein Programm, eine Datei oder Verknüpfung startet sie; bei `.lnk` wird der Pfad der Verknüpfung gespeichert (Store-Apps bleiben funktionsfähig) | `LaunchResolver` (Core, rein) + `Process.Start` mit `UseShellExecute=true`; `OpenFileDialog.DereferenceLinks=false` |
| ITEM-03 | Ein Ordner-Eintrag öffnet den Ordner im Explorer; Umgebungsvariablen in Pfaden werden aufgelöst | `Environment.ExpandEnvironmentVariables` zur Nutzungszeit, Pfade unexpandiert gespeichert |
| DND-01 | Dateien, Verknüpfungen und Ordner aus dem Explorer auf die Leiste ziehen hängt sie am Ende an; die Quelle wird weder verschoben noch kopiert (Q5) | `FileDrop`, `DragDropEffects.Link` (nie `Move`), `DropClassifier` in Core; Abschnitt „Drag & Drop“ |
| DND-02 | „Eintrag hinzufügen…“ im Leisten-Kontextmenü fügt über eine Dateiauswahl einen Eintrag hinzu (Q17) | `OpenFileDialog` mit explizitem, nicht-leistenseitigem Besitzer, `DereferenceLinks=false`, `Multiselect=true` |
| MENU-03 | Kontextmenüs schließen sich zuverlässig beim Klick daneben oder mit Escape, obwohl die Leiste nicht aktivierbar ist | M-C (Phase-1-belegt, auch von Hand); neu zu messen: Zusammenspiel mit Wächter/`HWND_BOTTOM`-Umschreibung |
| CONF-01 | Die Konfiguration liegt als JSON unter `%APPDATA%\MehrBar\` und übersteht Neustart der App und von Windows | `ConfigStore` + `FileConfigPersistence`; sofortiges Speichern bei diskreten Änderungen |
| CONF-02 | Vor jedem Speichern entsteht ein Backup, geschrieben wird atomar; unbekannte Felder und Eintragstypen bleiben beim Speichern erhalten | `File.Replace(tmp, config.json, config.bak.json)`; benutzerdefinierter `DockItemConverter` + `UnknownItem`; beides in dieser Sitzung per Probe belegt |
| APP-01 | Das Tray-Symbol bietet Konfiguration · Leiste ein-/ausblenden · Leiste nach vorn · Beenden; Doppelklick öffnet die Konfiguration; es übersteht einen Explorer-Neustart (Q15) | H.NotifyIcon.Wpf 2.4.1 (`ForceCreate(enablesEfficiencyMode: false)`!, Wiederholung bei Fehlschlag, `TaskbarCreated` ist eingebaut) |
| APP-05 | Ein Log unter `%APPDATA%\MehrBar\logs\` mit einfacher Rotation hält Fehler fest | handgeschriebener `RollingFileLog` in Core (1 MB × 4), drei Unhandled-Exception-Wege angeschlossen |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

Aus `./CLAUDE.md`, `./.claude/CLAUDE.md` und der globalen `~/.claude/CLAUDE.md` — gleiche Verbindlichkeit wie die gesperrten Entscheidungen:

- Mit dem Entwickler auf **Deutsch**; Code, Kommentare, Commit-Nachrichten **Englisch**; UI-Texte deutsch, aber von Anfang an in Ressourcen-Dateien (`.resx`).
- C# / **.NET 10** / WPF, Windows 10/11 x64, self-contained Einzeldatei-Publish.
- Alles ohne Oberfläche Testbare (Zoom, Layout, Config, Parser, Entscheidungslogik) in `MehrBar.Core` mit xUnit; **Win32-Aufrufe nur in `App/Interop`** (CsWin32 0.3.360, `allowMarshaling: false`, mehrere `NativeMethods.txt` nach Thema).
- Keine schweren Fremdbibliotheken; nur gepflegte NuGet-Pakete mit kompatibler Lizenz. **Fremde Skins und Screenshots nie ins Repo.**
- Dock läuft nie erhöht (`asInvoker`); Symbole, Zoom und Core-Layout achsenneutral.
- **GSD-Ablauf:** Repo-Änderungen nur über einen GSD-Befehl; Bildschirmläufe nur vom Desktop, nicht aus einer Hivemo-Sitzung (Windows-Sitzung 0).
- Unter-Agenten dürfen erheben und berichten, aber **nicht committen** (globale Regel); jede abschließende Antwort beginnt mit einem Rückbezug auf das Konkrete.

## Summary

Phase 1 hat Fenster, Ebene, Menü, Zeichnung und Symbole entschieden; offen für Phase 2 ist der **Zusammenbau**: ein neues Projekt `MehrBar.App` mit Interop-Schicht, dazu in Core alles, was ohne Oberfläche entscheidbar ist (Konfiguration, Platzierung, Layoutvertrag, Drop-Klassifizierung, Start-Auflösung, Z-Order-Zustandsautomat, Log). Die drei Dinge, die den Plan am stärksten prägen: (1) Der Spike-Wächter ist **statisch und einfach** (`static s_active`) und kennt keinen „vorn“-Modus — der Produkt-Keeper muss instanzbasiert sein und einen `FrontMode` bekommen, der den Wächter ruhen lässt und per `HWND_TOPMOST` über den Explorer hebt (LEIS-07). (2) Die Konfiguration braucht den **benutzerdefinierten Konverter** (die Attribut-Polymorphie verliert Daten); in dieser Sitzung wurde die komplette Round-Trip-Kette gebaut und lief — mit einer Einschränkung für die unveränderlichen Records aus ARCHITECTURE.md: die Erweiterungsdaten-Eigenschaft (`Extra`) muss `{ get; set; }` sein, nicht `init`, sonst wirft der Source-Generator zur Laufzeit. (3) `H.NotifyIcon.Wpf.TaskbarIcon.ForceCreate()` schaltet **standardmäßig den Effizienzmodus des ganzen Prozesses** ein (EcoQoS) — das würde den Zoom in Phase 3 drosseln —, und die Erzeugung wirft `InvalidOperationException`, wenn sie scheitert (hier gemessen in Sitzung 0 ohne Taskleiste; ebenso zu erwarten bei Autostart vor dem Explorer): immer `ForceCreate(enablesEfficiencyMode: false)` in try/catch mit Wiederholung.

Das Drag-and-Drop auf die Alpha-1-Platte, der Start per Klick aus dem Hintergrund (Vordergrundsperre) und die Dateiauswahl aus einem nicht aktivierbaren Fenster sind in Phase 1 **nicht** gemessen. Sie sind hier mit konkreten Gegenmaßnahmen (Drop-Effekt `Link`, `AllowSetForegroundWindow(ASFW_ANY)`, Dialog mit eigenem Besitzerfenster statt der Leiste) beschrieben, gehören aber früh in eine Handprüfung am Desktop; diese Sitzung hat keinen Desktop (`UserInteractive=False`, Sitzung 0, gemessen).

**Primary recommendation:** Core zuerst (TDD: Konfiguration/Store/Persistenz/Log/Layoutvertrag/Zustandsautomat), dann `BarWindow`+`ZOrderKeeper`+Platte als erster vertikaler Schnitt mit einem frühen **Handprüf-Haltepunkt** (Ebene, Klick-Durchlass, Drop auf die Platte), danach Tray/DnD/Menü/Start, zuletzt Einzeldatei-Publish mit Startzeitmessung.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Konfigurationsmodell, Defaults, Normalisierung, Serialisierung | Core (`Model/`, `Config/`) | — | rein, testbar; `System.Text.Json` ist im Framework |
| Speichern (tmp + `File.Replace` + Backup, Wiederholung) | Core (`Config/FileConfigPersistence`) | — | nur `System.IO`; mit Temp-Ordner testbar |
| Platzierung/Fensterplan (Ausrichtung, Versatz, `edgeOffset`, max. Zoom-Wachstum, Monitorklemme) | Core (`Layout/BarPlacement`, `WindowPlan`) | App/Interop (liefert Monitorrechteck, ruft `SetWindowPos`) | reine Mathematik, achsenneutral; Win32 liefert nur die Eingaben |
| Layoutvertrag (`RestLayout` → `FrameLayout`, Fortschritt 0) | Core (`Layout/`) | App/Rendering (zeichnet `FrameLayout`) | Phase 3 hängt nur Animator/Zeiger an |
| Z-Order-Entscheidung (Normal/Desktop gezeigt/Vorn/Versteckt) | Core (`ZOrderPolicy`, Zustandsautomat) | App/Interop (`ZOrderKeeper`: Haken, Takt, `SetWindowPos`) | Entscheidung testbar ohne Fenster; Win32 nur Beobachtung und Aktion |
| Fenster-Stile, `WM_WINDOWPOSCHANGING`-Umschreiben, `MA_NOACTIVATE`, Desktop-Host-Suche | App/Interop | — | Win32 |
| Trefferfläche (Alpha-1-Platte) | App/Rendering (`BarSurface` zeichnet sie) | OS (Hit-Test auf Pixelalpha) | belegt in Q1 |
| Zeichnen (Hintergrund, Symbole, Platzhalter) | App/Rendering (`OnRender`) | — | D-04 |
| Symbole laden (STA-Pool, `IShellItemImageFactory`, Pbgra32) | App/Shell + Interop | Core (`IconFrameHeuristic`, rein) | COM nur in App |
| Drop (OLE) | App/Bars (`DropTarget`, WPF-Ereignisse) | Core (`DropClassifier`, `DropEffectChooser`) | OLE ist UI; Klassifizierung rein |
| Start (Pfad auflösen, Existenz, Arbeitsordner) | Core (`LaunchResolver`) | App/Shell (`Process.Start`, `AllowSetForegroundWindow`) | Entscheidung rein, Ausführung App |
| Menüs (nativ M-C), Dateiauswahl, Meldungsfenster | App/Interop + App/Bars | — | UI/Win32 |
| Tray | App/Tray (H.NotifyIcon.Wpf hinter `ITrayIcon`) | Core (Wiederholungs-Zeitplan, falls rein extrahiert) | Rückfall Handgeschriebenes `Shell_NotifyIcon` bleibt eine Datei |
| Einzelinstanz, Lebenszyklus, Unhandled-Exceptions | App/Hosting (`Program.Main`) | — | Win32/WPF |
| Log (Rotation, Format) | Core (`RollingFileLog`, `IAppLog`) | App (verdrahtet die drei Fehlerereignisse) | rein, testbar |
| Publish (Einzeldatei, Startzeit) | App.csproj | `tools/`-Skript (Handlauf) | MSBuild |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK / Runtime | SDK 10.0.402 (aufgelöst), Runtime 10.0.12 | Bauen/Publish | `global.json` pinnt `10.0.401` mit `rollForward: latestFeature`; installiert ist nur 10.0.402 (`dotnet --list-sdks`: 9.0.318, 9.0.319, 10.0.402) [VERIFIED: dotnet --version im Repo = 10.0.402, 2026-10-19] |
| WPF (`net10.0-windows`) | im Framework | Fenster, `OnRender`, Drop-Ereignisse, Dialoge | gesperrt (Konzept) |
| `MehrBar.Core` (`net10.0`) | — | alle reinen Entscheidungen | Konzept, CLAUDE.md |
| Microsoft.Windows.CsWin32 | **0.3.360** (exakt gepinnt; auf NuGet gibt es bereits 0.3.363) | Win32/COM in `App/Interop` | gesperrt; Microsoft-Paket, `verified: true`, 3,98 Mio. Downloads [CITED: azuresearch-usnc.nuget.org/query?q=packageid:microsoft.windows.cswin32] |
| H.NotifyIcon.Wpf | **2.4.1** (stabil, 2025-12-01; 2.5.0 nur Beta) | Tray | STACK.md-Entscheidung; Mini-Aufwand (+0,6 MB laut STACK); Quelle gelesen und Kompilat/Publish in dieser Sitzung gebaut [VERIFIED: probe 2026-10-19] |
| System.Text.Json | im Framework (Source-Generator) | Konfiguration | kein Paket |
| `System.Resources.ResourceManager` + `.resx` | im Framework | deutsche UI-Texte | kein Codegenerator nötig, Probe unten |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit.v3.mtp-off / xunit.runner.visualstudio / Microsoft.NET.Test.Sdk | 4.0.1 / 4.0.0 / 18.10.1 (bereits in `Directory.Packages.props`) | Tests (VSTest) | Core-Tests; Filter `dotnet test --filter "FullyQualifiedName~…"` [VERIFIED: Lauf 28/28 grün in ~3 s, Filter 9/9, 2026-10-19] |
| — | — | Log | **kein** Paket (Serilog nur als dokumentierter Rückfall) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| H.NotifyIcon.Wpf | handgeschriebenes `Shell_NotifyIcon` (~150 Zeilen) | Rückfall hinter `ITrayIcon`; nur wenn H.NotifyIcon in der Handprüfung versagt |
| `[JsonPolymorphic]` | eigener `DockItemConverter` | Attribute verlieren unbekannte Typen/fordern `type` zuerst (STACK, Probe) — **nicht verwenden** |
| WinForms `NotifyIcon` | — | +33 MB, `Application`-Mehrdeutigkeit (STACK) — verworfen |
| WPF-Meldungsfenster (D-10) | `MessageBox`/`TaskDialogIndirect` | Win32-`MessageBox` kann „Entfernen/Behalten“ nicht beschriften; `TaskDialog` braucht Common-Controls-v6-Manifest; ein kleines WPF-Fenster ist einfacher und ressourcenfähig → **WPF-Fenster** |

**Installation (zentral über `Directory.Packages.props`, Projekte ohne `Version`):**
```xml
<!-- Directory.Packages.props: zu den drei Testpaketen ergänzen -->
<PackageVersion Include="Microsoft.Windows.CsWin32" Version="0.3.360" />
<PackageVersion Include="H.NotifyIcon.Wpf" Version="2.4.1" />
```
```bash
dotnet new wpf -n MehrBar.App -o src/MehrBar.App   # danach App.xaml/MainWindow.xaml löschen (code-getriebenes Fenster), csproj wie unten
dotnet sln MehrBar.sln add src/MehrBar.App/MehrBar.App.csproj
```
Version-Prüfung: H.NotifyIcon.Wpf 2.4.1 ist die letzte stabile Version (Versionsliste enthält danach nur `2.5.0-beta.*`/`-dev.*`), veröffentlicht 2025-12-01; Lizenz MIT; Abhängigkeit nur `H.NotifyIcon` 2.4.1 (→ `H.GeneratedIcons.System.Drawing`) [VERIFIED: api.nuget.org flatcontainer + nuspec, 2026-10-19].

## Package Legitimacy Audit

Der Seam `package-legitimacy check` kennt nur `npm|pypi|crates` (Aufruf mit `nuget` endete mit der Usage-Meldung). Prüfung deshalb manuell über die NuGet-API; Ergebnis der Regel nach: nicht über den Seam bestätigt.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| Microsoft.Windows.CsWin32 0.3.360 | NuGet | Versionsreihe 0.3.106 … 0.3.363 (Suchantwort) | 3,98 Mio. gesamt | github.com/Microsoft/CsWin32 | Seam n/a; manuell OK (Owner `Microsoft`, `verified: true`); bereits in `spikes/` gebaut | Approved (schon im Repo benutzt) |
| H.NotifyIcon.Wpf 2.4.1 | NuGet | Versionsreihe 1.x … 2.4.1; 2.4.1 vom 2025-12-01 | 446 k (H.NotifyIcon: 1,18 Mio.) | github.com/HavenDV/H.NotifyIcon (Tag v2.4.1, Commit `bcaaabec…` laut nuspec; Quelltext in dieser Sitzung gelesen) | Seam n/a; manuell plausibel (Owner `havendv`, `verified: false`, nur `lib/`-Ordner, **keine** `build/`/`tools/`-Skripte, Paketsignatur vorhanden); `[ASSUMED]`, weil Name aus STACK.md und nicht über Seam bestätigt | **Flagged — Planer fügt `checkpoint:human-verify` vor der ersten Installation ein** (Stephan bestätigt das Paket; Entscheidung steht bereits in STACK.md, der Aufwand ist eine Zeile) |

**Packages removed (SLOP):** keine  ·  **Flagged (SUS):** keines vom Seam, siehe Zeile oben als Regel-Vorbehalt.

Hinweis zu transitiven Paketen: `H.NotifyIcon` → `H.GeneratedIcons.System.Drawing` 2.4.1 (gleiche Autorenschaft); mit zentraler Paketverwaltung wurde das in der Probe ohne Zusatzeintrag aufgelöst.

## Architecture Patterns

### System Architecture Diagram

```
 Start (Main, STA)                                         Config-Datei %APPDATA%\MehrBar\config.json
  │ Mutex Local\MehrBar… ──(schon da)──> Exit                ▲ File.Replace(tmp, cfg, bak) / erster Save: File.Move
  ▼                                                          │
 AppHost ── RollingFileLog ◄── Dispatcher/AppDomain/Task-Fehler
  │
  ├─ ConfigStore (Core) ◄── load: json → Result{Config|Backup|Defaults, Fehler}
  │     │ Update(mutator) ─ Changed(old,new) ─► BarController
  │     └─ Persistence.Save (sofort bei diskreten Änderungen)
  │
  ├─ BarController (je BarConfig) ─ plant: RestLayout → FrameLayout(progress 0) → WindowPlan
  │     ├─ BarWindow (HWND: layered, TOOLWINDOW|NOACTIVATE, MA_NOACTIVATE, HWND_BOTTOM vor erstem Bild)
  │     │     ├─ BarSurface.OnRender: Platte(α=1) → Hintergrund → Symbole/Platzhalter   (kein Frame im Ruhezustand)
  │     │     ├─ Maus: Down latcht Item, Up auf demselben Item ─► Launch
  │     │     ├─ WM_RBUTTONUP ─► NativeMenu (M-C) ─► Entfernen | Hinzufügen… | Konfiguration
  │     │     └─ Drop: DragEnter/Over (Effects=Link) ─► Drop ─► DropClassifier ─► ConfigStore.Update (Anhängen)
  │     ├─ ZOrderKeeper (Haken+Takt+WM_WINDOWPOSCHANGING) ◄─ ZOrderPolicy (Core, Zustandsautomat)
  │     │      Normal ─(Host über Leiste)→ DesktopGezeigt ─(Fenster zurück)→ Normal;  Vorn(30 s) ← Tray „nach vorn“
  │     └─ WM_DISPLAYCHANGE / TaskbarCreated / DpiChanged ─► Neu-Platzieren + Host neu suchen
  │
  ├─ IconCache (2–4 STA-Threads, 3 s Limit) ─► gefrorene Pbgra32 ─► InvalidateVisual
  ├─ TrayIcon (H.NotifyIcon.Wpf, ForceCreate(false), Wiederholung)  ─► Konfiguration | ein/aus | nach vorn | Beenden
  └─ Dialogs: MissingTargetWindow (WPF, topmost, ohne Besitzer), OpenFileDialog (Besitzer = verstecktes Hilfsfenster)
```

### Recommended Project Structure
```
src/
├── MehrBar.Core/                  # net10.0, keine Pakete
│   ├── Model/        DockConfig, GeneralConfig, BarConfig, LabelConfig, DockItem + AppItem/FolderItem/UrlItem/SeparatorItem/UnknownItem
│   ├── Config/       Defaults, ConfigSerializer (+ ConfigJsonContext, DockItemConverter), ConfigNormalizer,
│   │                 ConfigStore, IConfigPersistence, FileConfigPersistence, ConfigLoadResult
│   ├── Layout/       (vorhanden: ZoomEngine, ZoomKernel) + BarMetrics, RestLayout(Builder), FrameLayout, BarPlacement, WindowPlan, HitTest, AxisFrame
│   ├── Drop/         DropPayload, DropClassifier, DropEffectChooser, IFileSystem
│   ├── Launch/       LaunchResolver, LaunchPlan, ILauncher/IShell (Naht)
│   ├── Shell/        IconFrameHeuristic (Eckenalpha), PathText (Name aus Pfad)
│   ├── Windowing/    ZOrderPolicy (Zustandsautomat), FrontMode
│   └── Logging/      IAppLog, RollingFileLog
├── MehrBar.App/                   # net10.0-windows, UseWPF, kein App.xaml
│   ├── Hosting/      Program.cs (Main), SingleInstance, AppHost, AppLifetime, CrashHandlers
│   ├── Bars/         BarController, BarWindow, BarSurface, DropTarget, NativeMenu, DialogOwner
│   ├── Dialogs/      MissingTargetWindow (+ Strings)
│   ├── Shell/        IconCache, ShellLauncher
│   ├── Tray/         ITrayIcon, HNotifyTrayIcon
│   ├── Interop/      NativeMethods.json; Window/ Events/ Menu/ Monitor/ Shell/ NativeMethods.txt;
│   │                 WindowStyles, ZOrderKeeper, DesktopHost, Monitors, ShellIcons, ForegroundScope
│   ├── Resources/    Strings.resx  (neutral = Deutsch)
│   ├── Assets/       mehrbar.ico  (eigene Zeichnung, neu zu erstellen)
│   └── app.manifest  PerMonitorV2, asInvoker
└── MehrBar.Tests/    Config/, Layout/, Drop/, Launch/, Windowing/, Logging/, Shell/
```
Eine Liste je Thema, jeder Name nur in **einer** Liste (spikes/README); mehrdeutige Namen qualifizieren; `EmitCompilerGeneratedFiles` nie in den Projektordner (STACK, Probe); zum Nachsehen der erzeugten Namen höchstens nach `obj/` (in dieser Sitzung so benutzt, funktioniert).

### Pattern 1: Konfigurationsmodell und Konverter (belegt in dieser Sitzung)
**Was:** Unveränderliche `record`s, `System.Text.Json`-Source-Generator, eigener `JsonConverter<DockItem>`, `UnknownItem` hält das Roh-`JsonElement`.
**Wann:** immer für `config.json`. **Einschränkung für `record`s mit `init`-Eigenschaften (ARCHITECTURE.md Pattern 7):** `[JsonExtensionData]` muss auf einer **settable** Eigenschaft liegen (STACK.md zeigt es bei Klassen mit `set` bereits richtig).
```csharp
// Source: Probe 2026-10-19 (Temp-Projekt, .NET 10.0.402) — Laufzeitfehler bei `init`:
// "The extension data property 'Extra' on type 'GeneralConfig' cannot bind with a parameter in the deserialization constructor."
public sealed record GeneralConfig {
    public bool StartWithWindows { get; init; }
    public string SkinFolder { get; init; } = "Skins";
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }   // set, nicht init
}
public enum BarEdge { [JsonStringEnumMemberName("top")] Top, [JsonStringEnumMemberName("bottom")] Bottom,
                      [JsonStringEnumMemberName("left")] Left, [JsonStringEnumMemberName("right")] Right }
// Eigenschaft: [JsonConverter(typeof(JsonStringEnumConverter<BarEdge>))] public BarEdge Edge { get; init; } = BarEdge.Top;
// Optionen: WriteIndented, DefaultIgnoreCondition=WhenWritingNull, ReadCommentHandling=Skip, AllowTrailingCommas, PropertyNamingPolicy=CamelCase,
//           Converters = { new DockItemConverter() }  → new ConfigJsonContext(options)
```
Gemessen (Round-Trip einer handgeschriebenen Datei mit `"type"` nicht an erster Stelle): bekannte Typen (`app`, `folder`, `url`, `separator`) korrekt; `"type":"group"` samt verschachteltem `items` und `collapsed` kommt unverändert zurück; unbekannte Felder auf Wurzel-, `general`-, Leisten-, `label`- und Eintragsebene bleiben erhalten; `"type"` steht beim Schreiben zuerst; Enums werden als `"top"`/`"center"`/`"cursor"` geschrieben [VERIFIED: probe 2026-10-19]. Erweiterungsdaten landen beim Schreiben **hinter** den bekannten Feldern (Reihenfolge ändert sich, Inhalt nicht).
**Wichtige Lücke:** Ein ungültiger Enum-Wert (`"edge":"diagonal"`) oder falscher Werttyp (`"path":42`) wirft `JsonException` — die **ganze** Datei lädt nicht [VERIFIED: probe]. → Ladepolitik (unten) und für Enums ein tolerierender Konverter (unbekannter Wert → Standard + Log-Zeile).

### Pattern 2: ConfigStore und Persistenz
**Was:** `ConfigStore` hält den aktuellen unveränderlichen Stand, `Update(Func<DockConfig,DockConfig>)` löst `Changed(old,new)` aus; Persistenz hinter `IConfigPersistence`.
**Speichern (belegt):** `config.json.tmp` schreiben und mit `Flush(true)` auf Platte, dann
- Datei existiert: `File.Replace(tmp, cfg, bak)` — liefert atomar den Austausch **und** das Backup; ein vorhandenes `config.bak.json` wird überschrieben (v2→bak=v1, v3→bak=v2) [VERIFIED: probe; CITED: learn.microsoft.com/dotnet/api/system.io.file.replace].
- Datei fehlt (erster Save): `File.Replace` wirft `FileNotFoundException` [VERIFIED: probe] → `File.Move(tmp, cfg)` (kein Backup nötig).
- Ziel von anderem Prozess ohne `FileShare.Delete` offen: `IOException` („in use“) [VERIFIED: probe]; mit `FileShare.ReadWrite|Delete` klappt es → 3 Wiederholungen mit 50/100/200 ms, dann Fehler loggen und im Speicher weiterarbeiten (nie den Dock abstürzen lassen).
**Politik (Empfehlung):** Phase 2 speichert **sofort** bei jeder diskreten Änderung (Hinzufügen, Entfernen) — das erfüllt D-11/CONF-02 wörtlich (ein Backup *vor jedem* Speichern, Fehlgriff per Backup korrigierbar). Entprellung (400–500 ms) kommt mit den Reglern in Phase 6; der Store bekommt dafür jetzt nur die Naht (`ISaveScheduler`), keine Logik.
**Laden:** Ergebnis `ConfigLoadResult { Config, Source = File|Backup|Defaults, Error? }`. Datei fehlt → Standardwerte **und sofort schreiben** (sonst findet „Konfiguration öffnen“ nichts). Datei defekt → Kopie `config.corrupt-<zeitstempel>.json`, dann `config.bak.json`, dann Standardwerte; eine defekte Datei nie stillschweigend überschreiben. `Normalize()` klemmt Werte (`zoomSize ≥ iconSize`, `zoomWidth ≥ 1`, `spacing ≥ 0`, unbekanntes `layer` → `bottom`). Dateigröße vor dem Parsen begrenzen (z. B. 10 MB).
**Hinweis:** Die Datei wird nur beim Start gelesen. Wer `config.json` bei laufender App bearbeitet, wird vom nächsten Speichern überschrieben → Arbeitsablauf in Phase 2: „Konfiguration“ öffnen, speichern, **MehrBar neu starten**; Datei-Beobachter ist Phase 6.

### Pattern 3: Layoutvertrag mit Fortschritt 0 und Fensterplan
**Was:** `RestLayout` (Positionen entlang der Achse) → `FrameLayout.Compute(rest, ZoomParams, pointerU?, progress)`; mit `progress = 0` ist `ZoomSize' = IconSize`, damit `growth = 0` und jede Extra-Länge 0 — `ZoomEngine.Compute` liefert dann exakt die Ruhepositionen (`starts[i] = Start − 0 + 0`, siehe ZoomEngine.cs:122-130). Phase 3 ergänzt nur Animator und Zeiger. Die Platzierung rechnet Core:
- `BarPlacement.Compute(RectI monitor, BarConfig, restLength, depth, maxGrowthBefore/After, overhang)` → `WindowPlan { RectI WindowRect, PointD BarOriginInWindow, RectD BarRect }`.
- `maxGrowth` per Abtasten der Zeigerposition (Schritt ≤ ¼ Pitch) mit `ZoomEngine`, Maximum von `ExtraBefore`/`ExtraAfter`; Näherung bei den Standardwerten: Σ der Zusatzlängen ≈ (121−32)·(1+2·0,672+2·0,120) ≈ 230 px je Seite [berechnet aus der Kernelformel mit r = 2,58, nicht gemessen] — das Fenster ist also ~460 px breiter als die Leiste, **bleibt aber ab Phase 2 so groß** (damit Phase 3 additiv ist und LEIS-06 „auch künftige Zoom-Fläche“ greift).
- Tiefe (Top-Rand): `edgeOffset + Hintergrundhöhe + (zoomSize − iconSize)` (+ Beschriftungsreserve in Phase 3). Fensterbreite auf den Monitor klemmen (Q18).
- `edgeOffset` negativ: Fenster beginnt über dem Bildschirmrand (`y = monitor.top + edgeOffset`). Layered-Fenster mit negativer Koordinate sind erlaubt; Sichtprüfung am Desktop [ASSUMED].
- Pitch/Spacing: Die Kalibrierung benutzt `Pitch = 32 = IconSize` (RocketDockReference.cs:20,40); das Konzept-Beispiel nennt `"spacing": 4` → offene Frage 1.
Mindestlänge der leeren Leiste: `MinSlots = 3` (D-08), Anzeige als Kasten ohne Text.

### Pattern 4: ZOrderKeeper (instanzbasiert) + Core-Zustandsautomat
**Was:** Der Spike (`ZOrderGuard.cs`) ist das belegte Vorbild, aber **kein** Kopiervorlage-Code: `private static ZOrderGuardVariant? s_active;` (Zeile 74) erlaubt genau eine Leiste. Produkt: ein dienstweiter Keeper mit **einem** WinEvent-Haken und **einem** Takt, der registrierte Leistenfenster der Reihe nach bedient (`APP-06`: mehrere Leisten kommen in Phase 3/V2), oder `ConcurrentDictionary<HWND, ZOrderKeeper>` für die `[UnmanagedCallersOnly]`-Rückrufe. Rückrufe dürfen nie Ausnahmen nach außen lassen (ZOrderGuard.cs:195-201).
**Beizubehalten, wörtlich aus dem belegten Spike** (ZOrderGuard.cs:35-44):
```csharp
private const int FastIntervalMs = 100;
private const int SlowIntervalMs = 250;
private const SET_WINDOW_POS_FLAGS PlaceFlags =
    SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER |
    SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOSENDCHANGING;
```
- Immer: `WM_WINDOWPOSCHANGING` → `hwndInsertAfter = HWND_BOTTOM`, außer `SWP_NOZORDER` oder schon `HWND_BOTTOM`, und außer „angehoben/vorn“ (ZOrderGuard.cs:252-264). Dieser Haken ersetzt in Phase 2 auch den Sonderpfad `PinBottomDuringInitialShow` des Hosts (HostWindow.cs:237-249): Haken **vor** dem ersten Show anhängen (`OnSourceInitialized`), dann wird auch WPFs eigenes `SWP_SHOWWINDOW` umgeschrieben. Das erste Bild unten ist in Phase 1 nur mit dem Sonderpfad gemessen → Log-Zeile `first-frame bottom=…` und Handprüfung.
- Haken: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, … WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS)` auf dem UI-Thread; `DispatcherTimer(DispatcherPriority.Send)` 250/100 ms; Host-Suche je Aufruf frisch (`EnumWindows` + `FindWindowEx(… "SHELLDLL_DefView")`, DesktopHost.cs:27,45) oder mit Zwischenspeicher, der per `IsWindow`+Klasse geprüft und bei `TaskbarCreated`/`WM_DISPLAYCHANGE` verworfen wird (Leerlast-Optimierung, in Phase 2 messen).
- Desktop gezeigt → Leiste direkt über den Host (nächstes nicht-topmost-Fenster davor oder `HWND_NOTOPMOST`, ZOrderGuard.cs:310-335), danach sofort `HWND_BOTTOM`. „Gewöhnliches Fenster“ schließt eigene Prozess-ID, `Progman`/`WorkerW`/`Shell_TrayWnd`/`Shell_SecondaryTrayWnd` aus (DesktopHost.cs:138) — **Dialoge und das Tray-Menü der eigenen App werden deshalb nicht als „Fenster sind zurück“ gezählt**, genau richtig.
- Beenden/Wächterfehler: Leiste auf `HWND_BOTTOM` zurück.

**Core-`ZOrderPolicy` (rein, neu):** Eingaben `Observation { HostFound, HostAboveBar, OrdinaryWindowAboveHost }`, Befehle `RaiseFront(now)`, `EndFront(reason)`, `Hide`, `Show`, `Tick(now)`; Zustände `Normal`, `DesktopLifted`, `Front(until)`, `Hidden`; Ausgabe `PlaceBottom | PlaceAboveHost | PlaceTopmost | DropFromTopmost | None`. In `Front` und `Hidden` wertet der Wächter **nicht** aus. Das ist die testbare Hälfte von LEIS-03/05/07.

### Pattern 5: „Leiste nach vorn“ (LEIS-07)
Warum topmost: Wer aus einem Explorer-Fenster zieht, aktiviert es und hebt es über jede nicht-topmost-Leiste; nur `HWND_TOPMOST` hält die Leiste darüber. Ablauf: Tray-Befehl → ggf. einblenden → `Front`: `SetWindowPos(HWND_TOPMOST, …, SWP_NOACTIVATE | NOMOVE | NOSIZE)`, Wächter ruht, `Umschreiben auf BOTTOM` aus, 30-s-Timer. **Ende** bei: jedem `Drop` auf der Leiste (auch wenn der Inhalt nicht passt — „etwas abgelegt“), Ablauf der 30 s, erneutem Tray-Befehl, Beenden. Ende heißt zweistufig `HWND_NOTOPMOST` dann `HWND_BOTTOM` (das ist der im Spike gemessene Standardweg `--guard-drop two-step`, ZOrderGuard.cs:337-347; die Einstufen-Variante ist nicht für diesen Fall gemessen). `DragLeave` beendet **nicht** (der Nutzer kann noch einmal ansetzen). Nach dem Ende Wächter sofort einmal auswerten.

### Pattern 6: Menü M-C mit `ForegroundScope`
Reihenfolge wie MenuNative.cs:131-162: vorheriges Vordergrundfenster merken → `SetForegroundWindow(Leiste)` → `TrackPopupMenuEx(TPM_RETURNCMD|TPM_RIGHTBUTTON|TPM_NONOTIFY)` → `PostMessage(WM_NULL)` → **Phase-2-Pflicht (D-03):** vorheriges Fenster zurückgeben (nur wenn gültig und nicht eigenes Fenster) und `SetWindowPos(HWND_BOTTOM)`. Der Befehl wird **nach** der Rückgabe des Vordergrunds ausgeführt, außer er öffnet ein eigenes Fenster (Dateiauswahl, Meldung) — dann den Vordergrund erst nach dem Schließen zurückgeben. Neu zu messen (Phase-1-Lücken): bleibt die Leiste mit dem Umschreib-Haken auch **während** des Menüs unten, schließt das Menü dabei weiter bei Klick daneben, und missdeutet der Wächter die Aktivierung nicht (`SKIPOWNPROCESS` überspringt Ereignisse des eigenen Prozesses; die Rückgabe an das alte Fenster ist ein Fremd-Ereignis, ändert aber keinen Zustand). `EndMenu` von außen (Explorer-Neustart/Anzeigewechsel bei offenem Menü) ist unbemessen → Handfall, Rückfall `EndMenu()` im Rückruf [ASSUMED].

### Pattern 7: Drag & Drop (WPF-Ereignisse auf dem Fenster)
`AllowDrop = true` am `BarWindow`; `DragEnter`/`DragOver`/`Drop`/`DragLeave`. **Effekt:** `Link` wählen, wenn `e.AllowedEffects` es enthält, sonst `Copy`, **nie `Move`**; in `DragEnter` **und** `DragOver` setzen (sonst Verboten-Cursor), `e.Handled = true`. Grund: Gibt das Ziel `Move` zurück, versteht die Quelle das als Aufforderung, die Originale zu löschen (Raymond Chen: `DROPEFFECT_MOVE` heißt, das Ziel hat nur kopiert und will, dass die Quelle das Original löscht; ein Avidemux-Forenbericht beschreibt gelöschte Quelldateien nach einem Drop mit gehaltener Umschalttaste) [CITED: devblogs.microsoft.com/oldnewthing/20041207-00 — nur über eine Suchzusammenfassung gelesen, MEDIUM]. Für `Link`/`Copy` macht die Quelle nichts; die Aufnahme in die Leiste ist reine Konfiguration. Formate in Phase 2: nur `DataFormats.FileDrop` (`string[]`, `.lnk` kommt als Pfad der Verknüpfung [ASSUMED], nicht aufgelöst); alles andere → `Effects.None`. Lesen mit try/catch (manche Quellen werfen). `Drop` hält kurz (Explorer wartet): klassifizieren (Ordner → `FolderItem`, sonst `AppItem`), Name = Dateiname ohne Endung (Dateibeschreibung der .exe ist ITEM-07/Phase 3), per `ConfigStore.Update` **anhängen** (Duplikate erlaubt, D-12), Symbol anfordern. Kein `Move`-Zucken: mehrere Dateien in Reihenfolge. Das Drop-Ziel gilt nur über den Fensterpixeln mit Alpha > 0 (Platte/Hintergrund) — die Q1-Messung (`WindowFromPoint` außen = `Progman`) stützt das, **DnD selbst ist unbemessen** → Handprüfpunkt H-DND; Fallbacks in Reihenfolge: Alpha der Platte auf 4–8 anheben (ARCHITECTURE.md Pattern 5), `SetWindowRgn` (H2).

### Pattern 8: Start per Klick
`LaunchResolver` (Core, rein): Pfad aus dem Eintrag → `Environment.ExpandEnvironmentVariables` (injizierbar) → `LaunchPlan { FileName, WorkingDirectory, Kind = File|Folder|Link|Passthrough|Missing }`. Regeln: Präfix `shell:` oder `http(s)://` → `Passthrough` (keine Existenzprüfung); sonst `Directory.Exists` → Ordner (`UseShellExecute` öffnet den Ordner im Explorer), `File.Exists` → Datei, sonst `Missing` (→ D-10-Dialog + Log). `.lnk`: Pfad der Verknüpfung starten, **keinen** Arbeitsordner setzen (liefert die Verknüpfung); übrige Dateien: Arbeitsordner = Ordner der Datei. Ausführung: `Process.Start(new ProcessStartInfo(plan.FileName){ UseShellExecute = true, WorkingDirectory = … })`, vorher `AllowSetForegroundWindow(ASFW_ANY)` (das gestartete Programm soll nach vorn kommen; Quelle: PITFALLS 5 Punkt 3, **ungemessen**), `Win32Exception` fangen (Fehlercode 1223 = UAC abgebrochen wäre nur bei `runas`, Phase 5), loggen. Start im UI-Thread (Phase 7 entscheidet über Auslagerung bei hängenden Netzpfaden). Store-App-Verknüpfungen (`.lnk` mit IDList) startet `ShellExecute` selbst; die Symbole kommen laut Q6 vom `.lnk` ohne Pfeil.

### Pattern 9: Dialoge ohne die Leiste als Besitzer
Die Leiste darf kein Besitzer eines Dialogs sein: Besitzer/besessen sind in der Z-Reihenfolge gekoppelt, und `ShowDialog()` ohne Besitzer nimmt `GetActiveWindow()` — also **die Leiste**, wenn sie gerade Menü-Vordergrund ist: „IntPtr hwndOwner = UnsafeNativeMethods.GetActiveWindow(); if (hwndOwner == IntPtr.Zero) if (Application.Current != null) hwndOwner = Application.Current.ParkingHwnd;“ und `ShowDialog(Window owner)` benutzt `new WindowInteropHelper(owner).Handle` [CITED: source.dot.net/PresentationFramework/Microsoft/Win32/CommonDialog.cs.html]. → Für die Dateiauswahl ein **verstecktes, nie gezeigtes** WPF-Fenster als Besitzer (`new WindowInteropHelper(w).EnsureHandle()`), `dlg.ShowDialog(w)`; `DereferenceLinks = false` setzen (sonst liefert die Auswahl das Ziel statt der `.lnk`; die Dokumentation nennt den Standard `false`, ältere Quellen und Erfahrung sprechen für `true` — explizit setzen und mit einer `.lnk` prüfen) [CITED: learn.microsoft.com/dotnet/api/microsoft.win32.filedialog.dereferencelinks]; `Multiselect = true`. Das D-10-Meldungsfenster: kleines WPF-`Window` (`ShowInTaskbar=false`, `Topmost=true`, `WindowStartupLocation=CenterScreen`, **kein** Besitzer), `ShowDialog()`; `Topmost` und `Activate()` im `Loaded`, damit es trotz Vordergrundsperre sichtbar wird (Klick auf eine nicht aktivierbare Leiste macht den Prozess nicht zum Vordergrundprozess) [ASSUMED, Handprüfung]. Ergebnis `Entfernen` → derselbe Befehl wie im Kontextmenü.

### Pattern 10: Tray (H.NotifyIcon.Wpf)
Code-getrieben, kein XAML: `new TaskbarIcon { ToolTipText, Icon (System.Drawing.Icon), ContextMenu (WPF) }`; **`ForceCreate(enablesEfficiencyMode: false)`** — der Standard `true` ruft `EfficiencyModeUtilities.SetEfficiencyMode(true)` und setzt den Prozess in den Windows-11-Effizienzmodus [VERIFIED: TaskbarIcon.cs v2.4.1 `public void ForceCreate(bool enablesEfficiencyMode = true)`; README „TaskbarIcon.ForceCreate(bool enablesEfficiencyMode = true) // default value“]. Der Konstruktor ruft zwar im `Loaded`-Handler `ForceCreate(enablesEfficiencyMode: false)` auf, aber nur, wenn das Element in einem Visual-Baum liegt; im Code-Aufbau also **selbst** aufrufen. In Sitzung 0 (keine Taskleiste) wirft `ForceCreate` `InvalidOperationException: TryCreate failed.` als unbehandelte Ausnahme (Einzeldatei-Probe; die Probe setzte zusätzlich eine feste `Id`) [VERIFIED: probe]; `Run`-Schlüssel-Autostart kann vor dem Explorer starten (PITFALLS 10) → try/catch, loggen, Wiederholung per `DispatcherTimer` (z. B. alle 2 s bis 60 s); `TaskbarCreated` baut das Symbol selbst neu (`OnTaskbarCreated`: `TryRemove()` + `Create()` im try/catch, TaskbarIcon.cs) — Explorer-Neustart ist damit abgedeckt, aber nur, wenn das Symbol einmal erzeugt wurde. Doppelklick: `TrayMouseDoubleClick` (Ereignis kompiliert) → „Konfiguration“. `Id` **nicht** setzen: der Standard ist eine aus dem Prozesspfad abgeleitete GUID (`CreateUniqueGuidForProcessPath`), eine feste GUID ist an einen Pfad gebunden (Entwicklungs- und Publish-Pfad unterscheiden sich) [VERIFIED: TaskbarIcon.Properties.cs Beschreibung; TrayIcon.cs:170,257-259]. Menü schließt bei Klick daneben, weil H.NotifyIcon das Popup in den Vordergrund holt (`SetForegroundWindow(handle)` in `ShowContextMenu`); Escape ist Handprüfpunkt. Tray-Menü-Texte: „Konfiguration“, „Leiste ausblenden/einblenden“, „Leiste nach vorn“, „Beenden“. `ITrayIcon` bleibt als 3-Methoden-Naht (Rückfall `Shell_NotifyIcon`).

### Pattern 11: Einzelinstanz, Start, Ende
`Program.Main` (`[STAThread]`, **eigener** Einstieg, kein `App.xaml` als `ApplicationDefinition`): `new Mutex(true, @"Local\MehrBar.SingleInstance.<feste GUID>", out created)` in statischem Feld; nicht erzeugt → still beenden, **bevor** WPF lädt. Phase 6 ergänzt das Signal an die laufende Instanz (APP-02). Danach: Log öffnen → Handler → `new Application { ShutdownMode = OnExplicitShutdown }` → Konfiguration laden → `BarWindow` erzeugen und zeigen (Symbole später) → Tray **nach** dem ersten Bild per `Dispatcher.BeginInvoke(…, Background)` → `app.Run()`. Ende (`AppLifetime.Exit`): Tray (`Dispose`), Keeper (`Haken lösen`, Leiste auf `HWND_BOTTOM`), Log, `Shutdown()`. Zwei Diagnoseargumente für Handläufe: `--data-dir <pfad>` (statt `%APPDATA%\MehrBar`) und `--exit-after-ms <n>`; sie machen einen isolierten Rauchtest vom Desktop möglich, ohne Stephans echte Konfiguration anzufassen.

### Pattern 12: Fenster-Erzeugung (aus dem Spike übernehmen)
Reihenfolge in `OnSourceInitialized` (HostWindow.cs:129-161 belegt): HWND holen → physische Größe in DIP umrechnen und `Root.LayoutTransform = new ScaleTransform(1/scale, 1/scale)` (1 WPF-Einheit = 1 Gerätepixel; Zeichnen und Treffer rechnen in Pixeln, HostWindow.cs:136-140) → `GWL_EXSTYLE |= TOOLWINDOW|NOACTIVATE` (143-145) → `SetWindowPos(HWND_BOTTOM, x, y, w, h, SWP_NOACTIVATE | SWP_SHOWWINDOW)` mit der **endgültigen** Größe (153-159) → Hook (`MA_NOACTIVATE` bei `WM_MOUSEACTIVATE`, 222-225; `WM_WINDOWPOSCHANGING`, `WM_DISPLAYCHANGE`, `TaskbarCreated`) → Keeper starten. `ShowActivated=false`, `ShowInTaskbar=false`, `Topmost=false`, `AllowsTransparency=true`, `WindowStyle=None`, `ResizeMode=NoResize`, `UseLayoutRounding`, `SnapsToDevicePixels`. Ein-/Ausblenden **nativ** (`ShowWindow(SW_HIDE)` / `SW_SHOWNA` + `SetWindowPos(HWND_BOTTOM, …)`), nicht über `Window.Show()`/`Visibility` (WPFs Show hob die Leiste in Q1 auf Z-Index 21); Keeper und Takt dabei ruhen lassen. Größenänderung beim Hinzufügen/Entfernen: eine `SetWindowPos`-Aufruf mit Position **und** Größe.
Neupositionierung bei `WM_DISPLAYCHANGE`: Primärmonitor frisch über `MonitorFromPoint(0,0, MONITOR_DEFAULTTOPRIMARY)` + `GetMonitorInfo` (`rcMonitor`), Plan neu rechnen, `SetWindowPos`; sofort und 300 ms später noch einmal (Mehrfach-Meldungen); keine `SystemEvents.DisplaySettingsChanged` (Leck, PITFALLS 17). `WM_DPICHANGED`: WPFs Vorschlagsrechteck verwerfen, Plan neu rechnen, Skalierung neu lesen.

### Pattern 13: IconCache (D-05) und Zeichnen (D-04, D-07, D-08)
**IconCache** (App/Shell, Vorbild `spikes/Icons/ShellIcon.cs`): feste Zahl (2–4) **dedizierter STA-Threads** mit Arbeitswarteschlange (nicht ein Thread je Aufruf wie im Spike; MTA liefert für `.url` eine leere Seite, Q6). Je Auftrag: `CoInitializeEx(COINIT_APARTMENTTHREADED)` → `SHCreateItemFromParsingName(expandierterPfad, IShellItemImageFactory)` → `GetImage(256×256, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK)` (ShellIcon.cs:100-103) → `GetObject`+`GetDIBits` (top-down, `biHeight = -h`) → Straight-Alpha-Bytes → **einmal** `Pbgra32`, `Freeze()`, `HBITMAP` immer `DeleteObject`. Zeitlimit 3 s je Eintrag: der Auftraggeber gibt auf (Platzhalter bleibt), der hängende Thread wird durch einen frischen ersetzt (sonst schrumpft der Pool bei jedem Netzpfad-Hänger). Ersatzregel 1 (Q6): `corner_alpha_max > 0` bei 256 px (vier 4×4-Ecken, `IconMetrics.cs:82-90`) → erneut mit 48 px anfordern und in WPF skalieren; die Prüfung ist eine reine Funktion (`IconFrameHeuristic`, Core) mit Test; Vorsicht, ein vollflächiges Symbol kann fälschlich anschlagen — mit echten Symbolen prüfen (Q6). Schlüssel = expandierter Pfad. Ergebnis → `Dispatcher.BeginInvoke(InvalidateVisual)`. Fehlendes Ziel/Fehler → Platzhalter bleibt, **nicht wiederholen** (Ersatzsymbol ist ITEM-06, Phase 7). Ein Eintrag bleibt auch mit Platzhalter klickbar. Startzeit: Symbole nie vor dem ersten Bild laden.
**Zeichnen** (`BarSurface : FrameworkElement`, R1): ein `OnRender` je Anforderung, `InvalidateVisual()` nur bei Änderung (Konfiguration, Symbol eingetroffen, Größe); **keine** `CompositionTarget.Rendering`-Abonnements in Phase 2 (Ruhe = 0 Frames, ZOOM-06/Phase 3 baut den Frame-Takt an). Schichten von unten nach oben: (1) Platte `Color.FromArgb(1, 0, 0, 0)` über `BarRect` (mindestens `MinSlots` breit; PlateH1.cs:46), (2) halbtransparenter Standard-Hintergrund mit Eckenradius (Vorschlag: ARGB `#B3202020`, Radius 8, 1 px Rand `#33FFFFFF`, innere Ränder 6 px — reine Empfehlung im Rahmen von „Claude's Discretion“), (3) Symbole 32×32 mit `RenderOptions.BitmapScalingMode = HighQuality` an Gerätepixeln, (4) Platzhalter (selbst gezeichnetes neutrales Rechteck, kein fremdes Bildmaterial). Items, die nicht dargestellt werden (`url`, `separator`, `UnknownItem`), liefert `RestLayout` nicht.

### Anti-Patterns to Avoid
- **Spike-Code unverändert in `src/`** (README: „No spike code is moved into src/ unreviewed“); `static s_active` und `HostOptions`-Plumbing nicht übernehmen.
- **`[JsonPolymorphic]`**, `init`-only `Extra`, `[JsonSourceGenerationOptions(Converters=…)]` bei eigenen Optionen (STACK/Probe).
- **`TaskbarIcon.ForceCreate()` ohne Argument**, `WindowExtensions.Hide/Show` aus H.NotifyIcon (setzen Effizienzmodus).
- **Dialoge mit der Leiste als Besitzer**; `ShowDialog()` ohne Besitzer, solange die Leiste aktiv sein kann.
- **`File.Move` über eine bestehende Datei, `File.Replace` ohne vorhandenes Ziel.**
- **`Window.Show()/Hide()` für Ein-/Ausblenden der Leiste.**
- **Zoom-Fortschritt hartcodiert im Renderer** statt `FrameLayout(progress)`.
- **`SetProcessDpiAwareness` im Code** (Manifest genügt).
- **Pfade expandiert speichern** (`%USERPROFILE%` bleibt im JSON stehen).
- **`Assembly.Location` / aktuelles Verzeichnis** für Pfade (Einzeldatei: leer; Autostart: System32).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Atomares Speichern + Backup | eigene Rename-Kette | `File.Replace(tmp, cfg, bak)` (+ `File.Move` beim ersten Save) | ein Aufruf = Austausch und Backup; Semantik in der Probe belegt |
| Polymorphe Einträge mit unbekannten Typen | Reflection/Typnamen | Konverter + `UnknownItem(JsonElement)` (STACK, Probe) | Attribute verlieren Daten |
| Win32-Signaturen | `DllImport` von Hand | CsWin32 0.3.360 + `NativeMethods.txt` je Thema | HRESULT/`BOOL`/Flags-Fehler vermeiden |
| Tray-Symbol | `Shell_NotifyIcon` | H.NotifyIcon.Wpf 2.4.1 (hinter `ITrayIcon`) | `TaskbarCreated`, Foreground-Tanz eingebaut |
| Menü, das beim Klick daneben schließt | WPF-`ContextMenu` auf der Leiste | natives `TrackPopupMenuEx` (M-C) | M-A scheitert an 4/8 Pflichtpunkten (Q3) |
| Shell-Symbole in 256 px | `SHGetFileInfo`/`ExtractIcon` | `IShellItemImageFactory` (`ICONONLY\|BIGGERSIZEOK`) | Alpha/Größe (Q6) |
| Einzelinstanz | Prozessliste | `Local\`-Mutex | Rennen/Fremdsitzungen |
| `.lnk`-Auflösung beim Hinzufügen | `IShellLink` für Start | nicht nötig: `.lnk` selbst starten | Phase 5 braucht `IShellLink` nur für die Eigenschaften |
| Dateiauswahl | eigener Baum | `Microsoft.Win32.OpenFileDialog` | Standarddialog, `DereferenceLinks=false` |
| Log-Bibliothek | Serilog | ~80 Zeilen `RollingFileLog` | Konzept: „einfache Rotation“ |

**Key insight:** Alles Entscheidbare ohne Fenster ist hier eine reine Core-Funktion mit Test; die App-Schicht führt nur aus. Das ist auch der einzige Weg, die Phase in einer Hivemo-Sitzung zu verifizieren.

## Common Pitfalls

### Pitfall 1: Effizienzmodus durch `ForceCreate()`
**What goes wrong:** Standardaufruf drosselt den ganzen Prozess (EcoQoS); Phase 3 ruckelt unerklärlich. **Why:** `enablesEfficiencyMode = true` ist der Standardwert (Quelle und README). **Avoid:** `ForceCreate(enablesEfficiencyMode: false)`. **Warning signs:** Task-Manager zeigt das Blatt-Symbol („Effizienzmodus“) beim Prozess.

### Pitfall 2: Tray-Erzeugung wirft, wenn die Taskleiste fehlt
**What goes wrong:** `ForceCreate` → `InvalidOperationException: TryCreate failed.` → unbehandelt Absturz, oder kein Notausgang. Gemessen in Sitzung 0 (keine Taskleiste); die Probe setzte zusätzlich eine feste `Id` — die Ursache ist nicht getrennt, die Taskleiste ist die naheliegende. Autostart vor dem Explorer (PITFALLS 10) ist derselbe Fall. **Avoid:** try/catch + Wiederholung; `TaskbarCreated` ergänzt; `Id` nicht setzen. **Verified:** Probe in Sitzung 0 [VERIFIED: probe 2026-10-19].

### Pitfall 3: `init`-Erweiterungsdaten
Siehe Pattern 1 — der Fehler tritt erst beim ersten Zugriff auf `ConfigJsonContext.DockConfig` zur Laufzeit auf, nicht beim Build. Ein Core-Test, der **jeden** Typ einmal (de)serialisiert, fängt das früh.

### Pitfall 4: Ein Tippfehler macht die ganze Konfiguration unlesbar
Falscher Enum-Text oder Werttyp → `JsonException` für die gesamte Datei [VERIFIED: probe]. **Avoid:** tolerierende Enum-Konverter, Ladepolitik mit Sicherungskopie `config.corrupt-*.json`/Backup/Defaults, nie stillschweigend überschreiben; Test mit defekten Fixtures.

### Pitfall 5: Ebene springt beim Menü/Dialog
Aktivieren der Leiste für das Menü hebt sie nach vorn (A5 bestätigt, Q3); ein Dialog mit der Leiste als Besitzer koppelt Z-Reihenfolge. **Avoid:** Umschreib-Haken immer aktiv, nach dem Menü ausdrücklich `HWND_BOTTOM`, Dialoge ohne Leistenbesitzer. **Warning signs:** nach „Eintrag hinzufügen…“ liegt die Leiste vor Fenstern.

### Pitfall 6: Wächter denkt, „Fenster sind zurück“, weil eigene Fenster darüber liegen
Eigene Dialoge/Tray-Menü dürfen die Desktop-Erkennung nicht beeinflussen — die belegte Erkennung schließt die eigene Prozess-ID aus (DesktopHost.cs:132-135); beim Umbau erhalten und testen (Zustandsautomat-Test mit „nur eigenes Fenster über dem Host“).

### Pitfall 7: Drop-Effekt `Move`
Folge: Explorer löscht die Quelldatei nach dem Drop. **Avoid:** `DropEffectChooser` (Core) wählt `Link` → `Copy` → `None`, hat einen Test, der `Move` ausschließt. **Warning:** Cursor zeigt „Verschieben“ bei gehaltener Umschalttaste — trotzdem `Link` zurückgeben.

### Pitfall 8: Drop nur auf Pixel mit Alpha > 0
Die Platte deckt die Leistenfläche; der leere Kasten (D-08) muss ebenfalls eine Platte haben, sonst ist die leere Leiste kein Ziel. **Avoid:** Platte aus `BarRect` (mindestens `MinSlots`), unabhängig vom Hintergrund zeichnen.

### Pitfall 9: Autostart-Arbeitsverzeichnis, Einzeldatei, `Assembly.Location`
`Environment.ProcessPath`/`AppContext.BaseDirectory` für Pfade; Konfiguration über `Environment.GetFolderPath(ApplicationData)`. Nie das aktuelle Verzeichnis.

### Pitfall 10: „Entfernen“ trifft den falschen Eintrag, weil ausgeblendete Einträge existieren
Die Leiste zeigt in Phase 2 nur `app`/`folder`; `url`, `separator` und `UnknownItem` bleiben in der Liste (Erhalt!), werden aber nicht gezeichnet. Entfernen und Treffer müssen deshalb über **Eintrags-Identität/Index in der Gesamtliste** laufen, nicht über den Index der sichtbaren Symbole. Ein Test mit gemischter Liste.

### Pitfall 11: Resx wird doppelt eingebunden
Das SDK nimmt `*.resx` automatisch als `EmbeddedResource`; ein eigener `<EmbeddedResource Include="Strings.resx"/>` bricht den Build mit NETSDK1022 [VERIFIED: probe]. Ressourcenname = `RootNamespace` + Ordner + Datei, in der Probe `jsonprobe.Strings.resources`.

### Pitfall 12: CA1416 für `GetDpiForMonitor` (Windows 8.1+)
`<SupportedOSPlatformVersion>10.0</SupportedOSPlatformVersion>` bricht mit NETSDK1135 (darf `TargetPlatformVersion` 7.0 nicht übersteigen) [VERIFIED: probe]; ein `[assembly: SupportedOSPlatform("windows10.0.19041")]` beseitigte die Warnung **nicht**; `<NoWarn>$(NoWarn);CA1416</NoWarn>` im App-Projekt ergab 0 Warnungen [VERIFIED: probe].

### Pitfall 13: Konfiguration bei laufender App bearbeitet
Siehe Pattern 2; im Plan als Hinweis in der „Konfiguration“-Meldung/Dokumentation, kein Code.

## Code Examples

### App.csproj (kompiliert und publiziert in der Probe)
```xml
<!-- Source: Probe 2026-10-19 (WPF + CsWin32 0.3.360 + H.NotifyIcon.Wpf 2.4.1); Einzeldatei 140.426.420 Bytes, keine losen Dateien -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <NoWarn>$(NoWarn);CA1416</NoWarn>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <RootNamespace>MehrBar.App</RootNamespace>
    <AssemblyName>MehrBar</AssemblyName>
    <DebugType>embedded</DebugType>
  </PropertyGroup>
  <!-- Nur Release: Debug/dotnet run bleibt frameworkabhängig und schnell; `dotnet publish -c Release` liefert eine Datei -->
  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <!-- PublishReadyToRun erst nach Messung (siehe Validation); EnableCompressionInSingleFile NIE (STACK: 5,4 s Erststart) -->
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Windows.CsWin32" PrivateAssets="all" />
    <PackageReference Include="H.NotifyIcon.Wpf" />
  </ItemGroup>
  <ItemGroup>
    <AdditionalFiles Include="Interop\**\NativeMethods.txt" />
    <AdditionalFiles Include="Interop\NativeMethods.json" />
    <ProjectReference Include="..\MehrBar.Core\MehrBar.Core.csproj" />
  </ItemGroup>
</Project>
```
`Skins\` wird erst in Phase 4 relevant. Manifest: `asInvoker`, `dpiAwareness PerMonitorV2` (Vorlage `spikes/WindowHost/app.manifest`; dort `dpiAware` = `true/pm`, STACK.md nennt `true` — beides gültig, `true/pm` aus dem belegten Spike übernehmen). Die bedingte Release-Eigenschaft funktioniert mit `dotnet publish -c Release` inklusive Restore [VERIFIED: probe].

### Ressourcen ohne Codegenerator
```csharp
// Source: Probe 2026-10-19 — Resources/Strings.resx (neutral = Deutsch), Zugriff:
internal static class Strings {
    private static readonly ResourceManager Rm = new("MehrBar.App.Resources.Strings", typeof(Strings).Assembly);
    public static string Get(string key) => Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    public static string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
```
Schlüssel (Vorschlag): `Tray_Config`, `Tray_HideBar`, `Tray_ShowBar`, `Tray_Front`, `Tray_Exit`, `Menu_AddEntry`, `Menu_OpenConfig`, `Menu_Exit`, `Menu_Remove`, `Dlg_Missing_Title`, `Dlg_Missing_Text` (`{0}` = Pfad), `Dlg_Missing_Remove`, `Dlg_Missing_Keep`, `Dlg_AddEntry_Title`, `Dlg_AddEntry_Filter`. Umlaute/Anführungszeichen blieben in der Probe erhalten.

### Drop-Handler
```csharp
// Source: Muster nach PITFALLS.md Pitfall 6 + Raymond Chen (DROPEFFECT_MOVE); Core entscheidet, WPF liefert nur Daten
private void OnDragOver(object s, DragEventArgs e) {
    bool files = e.Data.GetDataPresent(DataFormats.FileDrop);
    e.Effects = files ? ToWpf(DropEffectChooser.Choose(FromWpf(e.AllowedEffects))) : DragDropEffects.None; // Link > Copy > None, nie Move
    e.Handled = true;           // gleiches in DragEnter
}
private void OnDrop(object s, DragEventArgs e) {
    IReadOnlyList<string> paths = TryGetFileDrop(e.Data);               // try/catch, string[] → Liste
    var items = DropClassifier.Classify(paths, _fileSystem);            // Core: Ordner → FolderItem, sonst AppItem; Name ohne Endung
    _store.Update(c => c.WithBar(_barId, b => b with { Items = b.Items.AddRange(items) }));   // anhängen, Duplikate erlaubt
    e.Effects = DragDropEffects.Link; e.Handled = true;
    _keeper.EndFront("drop");
}
```

### Tray mit Wiederholung
```csharp
// Source: Probe 2026-10-19 (Fehler "TryCreate failed." ohne Taskleiste) + TaskbarIcon.cs v2.4.1
bool TryCreate() {
    try { _tray.ForceCreate(enablesEfficiencyMode: false); return true; }
    catch (Exception ex) { _log.Warn("tray: create failed, will retry", ex); return false; }
}
// Start: nach dem ersten Bild per Dispatcher(Background); bei false DispatcherTimer 2 s, höchstens 30 Versuche; TaskbarCreated baut H.NotifyIcon selbst neu.
```

### Rotierendes Log (Spezifikation aus STACK, hier konkretisiert)
`IAppLog { Debug/Info/Warn/Error(string, Exception?) }`; `%APPDATA%\MehrBar\logs\mehrbar.log`; ab 1 MB nach `mehrbar.1.log` … `mehrbar.4.log` rotieren (beim Start und je Schreibvorgang geprüft); ein `StreamWriter` mit `FileShare.ReadWrite`, `AutoFlush = true`; ISO-8601; wirft nie; Zeilenumbrüche in Meldungen entschärfen (Log-Einschleusung). Pflichtzeilen: `startup version=… os=… dpi=… monitor=…`, `config-loaded source=File|Backup|Defaults`, `bar-ready elapsed_ms=<seit Prozessstart>` (Startzeitmessung), `self-check exstyle=… bottom=…` (wie HostWindow.cs:273-302), `first-frame zindex=… bottom=…`, Wächter-Übergänge (`desktop-shown/hidden`, `zpos`), `launch-failed`, `drop files=<n>`.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `[JsonPolymorphic]` für Konfig-Einträge | eigener Konverter + `UnknownItem` | Probe 2026-10-09/19 | Daten gehen sonst verloren |
| `xunit.v3.mtp-v2` | `xunit.v3.mtp-off` über VSTest | 2026-10-19 (D-06) | Testexplorer findet die Tests; Filter `--filter "FullyQualifiedName~…"` |
| WinForms `NotifyIcon` | H.NotifyIcon.Wpf 2.4.1 | STACK 2026-10-09 | −33 MB, aber `ForceCreate(false)` beachten |
| Einzeldatei nur mit `PublishSingleFile` | + `IncludeNativeLibrariesForSelfExtract` | STACK/Probe | sonst 5 lose native DLLs |

**Deprecated/outdated:** ARCHITECTURE.md Pattern 7 („immutable records“) ist für `Extra` so nicht lauffähig (siehe Pattern 1 hier); Spike-Host-Plumbing (`HostOptions`, `static s_active`); CsWin32-Wechsel auf 0.3.363 nicht ohne Not (Pin-Regel).

## Validation Architecture

> `workflow.nyquist_validation` ist `true` in `.planning/config.json` (gelesen); `security_enforcement: true`, `security_asvs_level: 1`.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 4.0.1 (`xunit.v3.mtp-off`) über VSTest; Hilfspakete `xunit.runner.visualstudio` 4.0.0, `Microsoft.NET.Test.Sdk` 18.10.1 |
| Config file | `src/MehrBar.Tests/MehrBar.Tests.csproj` (`OutputType=Exe`, `net10.0`); `global.json` ohne `test`-Block |
| Quick run command | `dotnet test src/MehrBar.Tests --nologo --filter "FullyQualifiedName~<Namensteil>"` (z. B. `~Config`, `~Layout`, `~Windowing`) |
| Full suite command | `dotnet test --nologo` (Basis heute: 28 Tests, ~3 s, alle grün) und `dotnet build MehrBar.sln -c Release` (App baut, 0 Warnungen) |

Verifikationsbefehle prüfen die **Anzahl** der gelaufenen Tests (unter VSTest meldet ein Filter ohne Treffer nur „keine Tests“, der Exitcode ist dafür nicht belastbar, 01-SPIKE-tooling.md); Skripte werten den Exitcode für Erfolg/Fehler, nicht den deutschen Text.

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| LEIS-01 | Defaults: top/center/offset 0/edgeOffset −5/32/121/4/200/Tahoma 13/`CrystalXP.net`; Erststart schreibt `config.json` | unit | `dotnet test --filter "FullyQualifiedName~DefaultsTests\|FullyQualifiedName~ConfigStoreTests"` | ❌ Wave 0 |
| LEIS-02 | `BarPlacement` je Rand/Ausrichtung/Versatz, `edgeOffset` negativ, Monitorwechsel (1920→1600) | unit | `--filter "FullyQualifiedName~BarPlacementTests"` | ❌ Wave 0 |
| LEIS-02 | `edgeOffset`/`align`/`offset` überstehen Speichern→Laden | unit | `--filter "FullyQualifiedName~ConfigSerializerTests"` | ❌ Wave 0 |
| LEIS-03/05 | `ZOrderPolicy`: Normal→DesktopLifted→Normal; nur eigenes Fenster über Host ≠ „Fenster zurück“; Host verschwunden (Explorer-Neustart) | unit | `--filter "FullyQualifiedName~ZOrderPolicyTests"` | ❌ Wave 0 |
| LEIS-03/04 | Stile/`bottom`-Zeile im Log, Leiste hinter Fenstern, Fokus bleibt | **manuell am Desktop** (H-ZORDER, H-FOCUS) | Rauchtest `tools/phase2-smoke.ps1` liest `self-check`/`first-frame` aus dem Log (optional) | ❌ |
| LEIS-05 | Win+D, „Desktop anzeigen“-Ecke, Explorer-Neustart, Auflösungswechsel | **manuell** (H-WIND) | Vergleichswerte aus `spikes/results/q2/s2-followup/` | — |
| LEIS-06 | Fenster = Ruhebreite + Wachstum; Platte ⊂ Fenster; Klick-Durchlass | unit (Plan) + **manuell** (H-CLICK) | `--filter "FullyQualifiedName~WindowPlanTests"` | ❌ Wave 0 |
| LEIS-07 | `FrontMode`: Ende bei Drop/30 s/Beenden, Zustandsfolge `Front→Normal` mit injizierter Uhr | unit | `--filter "FullyQualifiedName~FrontModeTests"` | ❌ Wave 0 |
| LEIS-07 | Leiste steht vor dem Explorer, Rückkehr nach Drop und nach 30 s | **manuell** (H-FRONT) | — | — |
| ITEM-01 | `LaunchResolver`: `.lnk`-Pfad bleibt, `shell:`/`http` Durchlass, Arbeitsordner-Regel, `Missing` | unit | `--filter "FullyQualifiedName~LaunchResolverTests"` | ❌ Wave 0 |
| ITEM-01 | exe, Datei, Store-App-`.lnk` starten und kommen nach vorn | **manuell** (H-LAUNCH) | — | — |
| ITEM-03 | `%USERPROFILE%` expandiert, nicht gespeichert; Ordner → `Folder`-Plan | unit | `--filter "FullyQualifiedName~LaunchResolverTests"` | ❌ Wave 0 |
| DND-01 | `DropClassifier` (Datei/Ordner/`.lnk`/mehrere, Reihenfolge, Duplikate); `DropEffectChooser` nie `Move` | unit | `--filter "FullyQualifiedName~Drop"` | ❌ Wave 0 |
| DND-01 | Drop aus Explorer: Eintrag erscheint, Quelle unverändert | **manuell** (H-DND) | — | — |
| DND-02 | Dateiauswahl liefert `.lnk` unaufgelöst; Eintrag angehängt | **manuell** (H-ADD) | — | — |
| MENU-03 | Menü schließt bei Klick daneben/Escape (Leisten- und Eintragsmenü), Leiste bleibt unten, Fokus kehrt zurück | **manuell** (H-MENU), Vergleich 01-SPIKE-menu | — | — |
| CONF-01 | Round-Trip, Erststart-Datei, Normalize, defekte Datei → corrupt-Kopie/Backup/Defaults | unit | `--filter "FullyQualifiedName~Config"` | ❌ Wave 0 |
| CONF-01 | Überlebt App-Neustart und Windows-Neustart | **manuell** (H-PERSIST) | — | — |
| CONF-02 | `File.Replace` + `config.bak.json` vor jedem Save, erster Save ohne Backup, gesperrtes Ziel → Wiederholung, unbekannte Felder/Typen/`type` an beliebiger Stelle bleiben | unit (Temp-Ordner) | `--filter "FullyQualifiedName~FileConfigPersistenceTests\|FullyQualifiedName~ConfigSerializerTests"` | ❌ Wave 0 |
| APP-01 | Tray-Einträge, Doppelklick, Explorer-Neustart (Symbol kommt zurück) | **manuell** (H-TRAY) | — | — |
| APP-05 | `RollingFileLog`: Rotation bei 1 MB, behält 4, wirft nie, Zeilenumbruch-Entschärfung | unit | `--filter "FullyQualifiedName~RollingFileLogTests"` | ❌ Wave 0 |
| APP-05 | Fehler (z. B. fehlendes Ziel) steht im Log unter `%APPDATA%\MehrBar\logs\` | **manuell** (H-LOG) | — | — |
| (Ergänzung) | Item-Treffer/Entfernen über Gesamtlisten-Index mit versteckten Typen | unit | `--filter "FullyQualifiedName~HitTestTests"` | ❌ Wave 0 |
| (Ergänzung) | `IconFrameHeuristic`: Eckenalpha > 0 → 48-px-Anforderung | unit | `--filter "FullyQualifiedName~IconFrameHeuristicTests"` | ❌ Wave 0 |
| (Ergänzung) | `FrameLayout(progress 0)` == `RestLayout`; Achsenneutralität (optional vorgezogen) | unit | `--filter "FullyQualifiedName~FrameLayoutTests"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test --nologo --filter "FullyQualifiedName~<betroffener Bereich>"` (< 10 s)
- **Per wave merge:** `dotnet test --nologo` + `dotnet build MehrBar.sln -c Release`
- **Phase gate:** voll grün + `dotnet publish src/MehrBar.App -c Release` (eine `MehrBar.exe`) + Handprüfliste unten abgehakt, vor `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `src/MehrBar.Tests/Config/{DefaultsTests,ConfigSerializerTests,ConfigStoreTests,FileConfigPersistenceTests}.cs` — CONF-01/02, LEIS-01/02
- [ ] `src/MehrBar.Tests/Layout/{BarPlacementTests,WindowPlanTests,FrameLayoutTests,HitTestTests}.cs` — LEIS-02/06, Layoutvertrag
- [ ] `src/MehrBar.Tests/Windowing/{ZOrderPolicyTests,FrontModeTests}.cs` — LEIS-03/05/07
- [ ] `src/MehrBar.Tests/Drop/{DropClassifierTests,DropEffectChooserTests}.cs` — DND-01
- [ ] `src/MehrBar.Tests/Launch/LaunchResolverTests.cs` — ITEM-01/03
- [ ] `src/MehrBar.Tests/Logging/RollingFileLogTests.cs` — APP-05
- [ ] `src/MehrBar.Tests/Shell/IconFrameHeuristicTests.cs`
- [ ] Fixtures als Zeichenketten/Dateien unter `src/MehrBar.Tests/Fixtures/` (z. B. `config.future.json` mit `group`, unbekannten Feldern, `type` nicht zuerst; defekte Dateien)
- [ ] Keine neuen Pakete; das App-Projekt bekommt **keine** eigene Testprojekt-Pflicht (Logik steckt in Core)
- [ ] Optional: `tools/phase2-smoke.ps1` (vom Desktop): startet `MehrBar.exe --data-dir %TEMP%\mb-smoke --exit-after-ms 5000`, wertet `bar-ready elapsed_ms`, `self-check`, `first-frame` im Log aus (Exitcode)

### Handprüfung nur am Desktop (nicht aus Hivemo/Sitzung 0)
Diese Sitzung hat gemessen `UserInteractive=False`, `SessionId=0`; `ForceCreate` scheitert dort, Fensterläufe sind sinnlos. Vor Handläufen RocketDock beenden (Lehre aus Q1: es färbt den oberen Rand und liegt über derselben Fläche).

| ID | Prüfpunkt | Erwartung |
|----|-----------|-----------|
| H-FIRST | Erster Start ohne `config.json` | leerer Kasten oben mittig (~3 Symbole breit), 5 px über den Rand geschoben, `config.json` angelegt |
| H-Z | Fenster öffnen/maximieren, Alt+Tab | Leiste hinter jedem Fenster, nicht in Alt+Tab; erstes Bild nie über Fenstern (10 Starts) |
| H-FOCUS | Notepad aktiv, Klick auf Leiste/Rechtsklick | Notepad bleibt aktiv |
| H-CLICK | Klicks 1 px neben der Leiste und im Bereich unter dem Kasten | erreichen Desktop/Fenster darunter |
| H-WIND | Win+D, Ecke „Desktop anzeigen“, Win+M, Explorer-Neustart, Auflösung 1920×1080↔1600×900 | Leiste sichtbar und am Platz (Vergleich 236/271 ms Lücke aus Q2) |
| H-FRONT | Tray „Leiste nach vorn“, Explorer-Fenster darüber, Datei ziehen | Leiste vorn; nach Drop bzw. 30 s wieder unten |
| H-DND | Exe, Ordner, `.lnk`, mehrere gleichzeitig aus dem Explorer | angehängt, Quelle unverändert (auch mit gehaltener Umschalttaste) |
| H-ADD | Rechtsklick Leiste → „Eintrag hinzufügen…“ mit `.lnk` | `.lnk`-Pfad gespeichert, Leiste bleibt unten, Dialog erscheint vorn |
| H-MENU | beide Menüs: Klick daneben, Escape, Pfeile/Enter | schließt; Fokus kehrt zurück; Leiste unten |
| H-LAUNCH | exe, Datei, Store-App-`.lnk`, Ordner, `%USERPROFILE%`-Pfad (von Hand eingetragen) | startet, Fenster kommt nach vorn |
| H-MISSING | Eintrag mit nicht vorhandenem Ziel anklicken | Meldungsfenster vorn, „Entfernen/Behalten“, Log-Zeile; Leiste danach unten |
| H-PERSIST | Beenden, neu starten, Windows neu starten | Einträge und Werte da; `config.bak.json` vorhanden; Hand-Einträge (`group`, unbekanntes Feld) nach einem Hinzufügen weiter in der Datei |
| H-TRAY | Konfiguration (Editor), ein-/ausblenden, nach vorn, Beenden, Doppelklick, Explorer-Neustart | alles wirkt; Symbol kommt zurück; kein Geister-Symbol nach Beenden |
| H-ONCE | zweiter Start | zweite Instanz beendet sich still |
| H-PUBLISH | `dotnet publish src/MehrBar.App -c Release`, Datei starten (kalt/warm) | eine `.exe`, `bar-ready elapsed_ms` im Log notiert (Ziel < 1000, bindend erst Phase 7) |
| H-SCALE (Stichprobe) | 125 % (und 150 %) einstellen, App neu starten | Leiste an Ort, Symbole nicht unscharf — Befund notieren, Behebung Phase 7 |

## Security Domain

### Applicable ASVS Categories (Level 1)

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | nein | lokale Einzelplatz-App, keine Konten |
| V3 Session Management | nein | — |
| V4 Access Control | ja (klein) | `asInvoker`; **nie** erhöht (UIPI-Schutz erhalten, kein `ChangeWindowMessageFilterEx`); Mutex im `Local\`-Namensraum |
| V5 Input Validation | ja | Konfiguration: `System.Text.Json` mit festem Typ-Schalter (kein Typname aus der Datei), Größenlimit, `Normalize()`; Drop-/Dialogpfade: nur als Zeichenketten, Existenzprüfung, keine Argumente aus dem Drop; Log: Zeilenumbrüche entschärfen |
| V6 Cryptography | nein | keine Geheimnisse, nichts selbst gebaut |
| V8 Data Protection | gering | `%APPDATA%\MehrBar` liegt im Benutzerprofil (Benutzer-ACL); keine Geheimnisse in Config/Log |
| V12 Files | ja | temporäre Datei im selben Ordner, `File.Replace`; nie Pfade aus der Konfiguration relativ zum aktuellen Verzeichnis auflösen |

### Known Threat Patterns for diesen Stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Manipulierte `config.json` startet beliebige Programme | Tampering | gleiche Vertrauensgrenze wie das Benutzerprofil; nur `UseShellExecute` mit dem Eintragspfad, **keine** Shell-Zeichenkettenbildung, keine Argumente in Phase 2 |
| Typ-Injektion über JSON-Typnamen | Tampering | feste Whitelist im `DockItemConverter`, sonst `UnknownItem` (unverändert zurückgeschrieben, nie instanziiert) |
| Defekte/riesige Konfiguration → Absturz oder Datenverlust | DoS/Tampering | Größenlimit, Ladepolitik mit `corrupt`-Kopie und Backup, nie überschreiben |
| Log-Einschleusung über Dateinamen | Tampering | Steuerzeichen entfernen |
| Mutex-Besetzung durch anderen Prozess der Sitzung | DoS | akzeptiert (gleiche Sitzung = gleicher Benutzer) |
| Drop `Move` löscht Fremddateien | Tampering | `DropEffectChooser` nie `Move`, Test |
| Erhöhter Start blockiert Drop (UIPI) | — | Erhöhung erkennen und im Log warnen (Empfehlung PITFALLS 10) |

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Bauen/Test/Publish | ✓ | 10.0.402 (`global.json` 10.0.401, `latestFeature`) | — |
| win-x64 Runtime-Pack + crossgen2 | Einzeldatei, ReadyToRun | ✓ (NuGet-Cache `microsoft.netcore.app.runtime.win-x64`, `…windowsdesktop.app.runtime.win-x64`, `…crossgen2.win-x64`, je 10.0.12) | 10.0.12 | online Restore |
| NuGet (Netz) | CsWin32, H.NotifyIcon.Wpf | ✓ (Probe-Restore ok) | — | Cache |
| Interaktiver Desktop | alle Handprüfungen | ✗ in dieser Sitzung (`UserInteractive=False`, Sitzung 0) | — | Stephan am Desktop; Core-Tests laufen überall |
| PowerShell 5.1 | Rauchtest-/Mess-Skripte | ✓ | — | — |
| RocketDock | muss für Handläufe **beendet** sein | läuft evtl. | — | beenden |

**Missing dependencies with no fallback:** interaktiver Desktop für alle Fenster-/Tray-/DnD-Prüfungen — blockiert die Hand-Abnahme, nicht die Entwicklung.
**Missing dependencies with fallback:** keine.

## Open Questions

1. **`spacing` (Symbolabstand) — Standardwert 0 oder 4?**
   - Bekannt: Kalibrierung und Core-Test rechnen mit `Pitch = IconSize = 32`, Trennlinien 16 (RocketDockReference.cs:20,40,43); das Konzept-JSON zeigt `"spacing": 4` als Beispiel. Abstand ist in Phase 2 „Claude's Discretion“.
   - Unklar: ob 4 px Abstand die Phase-3-Kalibrierung verschieben (Pitch 36).
   - Empfehlung: Feld `spacing` im Modell, **Standard 0** (kalibrierungstreu), Slotlänge = `iconSize + spacing`; Stephan kann später per Konfiguration ändern. Im Plan festschreiben.
2. **Konvention für `offset` bei `align = start|center|end`.** Empfehlung: `offset` ist ein vorzeichenbehafteter Versatz entlang +u (links→rechts bzw. oben→unten), nach der Ausrichtung addiert. Enum-Namen `start`/`center`/`end` sind aus dem Konzept abgeleitet [ASSUMED]. Mit Test festschreiben.
3. **Nicht darstellbare Eintragstypen (`url`, `separator`, `UnknownItem`) in Phase 2.** Empfehlung: erhalten, nicht zeichnen, Treffer/Entfernen über Gesamtlisten-Index; eine Log-Zeile je Typ beim Laden.
4. **Ladepolitik bei defekter Datei und tolerierende Enums.** Empfehlung wie Pattern 2; Stephans Nicken genügt (Standardfall bleibt der gesunde Pfad).
5. **Drop auf die Alpha-1-Platte und Start aus dem Hintergrund sind unbemessen.** Empfehlung: ein früher Handprüf-Haltepunkt direkt nach dem ersten vertikalen Schnitt (BarWindow + Platte + Keeper + Drop-Stub): Datei darüber ziehen, Klick auf einen Eintrag, „Eintrag hinzufügen…“. Fallbacks siehe Pattern 7/8.
6. **Abnahme des Produkt-Keepers.** `Driver`/`run-zorder.ps1` messen den *Spike*-Host (READY-Zeile, Sentinel-Quadrate, CSV-Protokoll, `HostProcess.Start(exe)`), nicht das Produkt. Empfehlung: Produkt per Handliste (H-WIND) und Wächter-Übergängen im Log abnehmen; die Anbindung des Drivers an `MehrBar.exe` (Sentinel-Quadrate, READY-Protokoll) kostet mehr als sie bringt — nur auf ausdrücklichen Wunsch.
7. **Taskleiste/Monitore auf Stephans Rechner.** Platzierung nach `rcMonitor` (nicht `rcWork`) — ob die Taskleiste oben liegt, ist unbekannt (vermutlich unten); das `LivePreview`-Fenster der Q2-Notiz maß 3840×1080, die Notizen sprechen von „ein Monitor“ — Primärmonitor immer über `MonitorFromPoint` bestimmen, nie über Bildschirmbreite.
8. **Anwendungs-Icon.** Es gibt noch keins (`Assets/mehrbar.ico`). Eigene Zeichnung erstellen (kein fremdes Material), z. B. per Skript aus PNG-Frames 16/32/48/256; Tray- und Exe-Icon.
9. **`ReadyToRun`.** Erst nach Messung (kalt/warm × an/aus) entscheiden; crossgen2 liegt im Cache.
10. **„Konfiguration“ öffnet `.json` im Standardprogramm.** Falls keine Zuordnung besteht (Win32-Fehler `ERROR_NO_ASSOCIATION` 1155) Rückfall `notepad.exe <pfad>`; unklar, welches Programm Stephans Rechner für `.json` hat (Browser wäre unpraktisch).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `.lnk`-Dateien kommen im `FileDrop` als Pfad der Verknüpfung (nicht aufgelöst) | Pattern 7 | Eintrag zeigt auf das Ziel statt auf die `.lnk` (Store-Apps brechen) — Test H-DND |
| A2 | OLE-Drop trifft das Layered-Fenster nur über Pixeln mit Alpha > 0 und funktioniert über der Alpha-1-Platte | Pattern 7 | DnD zielt ins Leere — Fallback Alpha 4–8 / Region |
| A3 | `AllowSetForegroundWindow(ASFW_ANY)` vor dem Start lässt das Programm nach vorn kommen; ein Klick auf die nicht aktivierbare Leiste reicht als „letzte Eingabe“ | Pattern 8 | gestartete Fenster öffnen hinter anderen — Test H-LAUNCH |
| A4 | `Topmost`+`Activate()` bringt das D-10-Fenster trotz Vordergrundsperre nach vorn | Pattern 9 | Meldung bleibt unsichtbar hinter Fenstern — Test H-MISSING |
| A5 | Verstecktes, nie gezeigtes WPF-Fenster (`EnsureHandle`) taugt als Besitzer der Dateiauswahl (Dialog erscheint vorn, zentriert ok) | Pattern 9 | Dialog hinter Fenstern — Alternative: kurz sichtbares 1×1-Fenster |
| A6 | Mit dem Umschreib-Haken bleibt die Leiste auch während `SetForegroundWindow` unten und das Menü schließt weiter | Pattern 6 | Leiste hebt sich beim Menü — ausdrückliches `HWND_BOTTOM` danach (D-03) fängt es ab |
| A7 | `EndMenu()` im Rückruf schließt ein offenes Menü bei Explorer-Neustart/Anzeigewechsel | Pattern 6 | Menü bleibt hängen — Randfall, Handprüfung |
| A8 | Fenster mit negativer Y-Koordinate (`edgeOffset` −5) werden vom DWM/WPF nicht auf den Bildschirm geklemmt | Pattern 3 | Leiste sitzt 5 px zu tief — H-FIRST |
| A9 | Symbole bei 32 px aus 256-px-Bitmaps mit `BitmapScalingMode.HighQuality` sind scharf genug (sonst vorgefertigte Zwischenstufe) | Rendering | weiche/flimmernde Symbole — Pyramide/`TransformedBitmap` |
| A10 | `align`-Werte heißen `start`/`center`/`end` | Konfiguration | Konfig-Format ändert sich nachträglich |
| A11 | `Environment.ExpandEnvironmentVariables` genügt (kein `Environment.GetFolderPath`-Sonderfall); unaufgelöste `%X%` bleiben stehen → `Missing` | Pattern 8 | falscher „Ziel fehlt“-Dialog |
| A12 | Ein Hinweis „Konfiguration nur bei beendeter App bearbeiten“ reicht in Phase 2 (kein Datei-Beobachter) | Pattern 2 | überschriebene Handänderungen — Backup fängt es teilweise |
| A13 | H.NotifyIcon.Wpf 2.4.1 ist unbedenklich (Name aus STACK.md, nicht über den Seam bestätigt) | Package Audit | Tausch auf handgeschriebenes `Shell_NotifyIcon` hinter `ITrayIcon` |
| A14 | Fensterbreite ~460 px über der Leiste (2× ≈ 230 px) bei den Standardwerten | Pattern 3 | nur Größenordnung; `WindowPlan.MaxGrowth` rechnet es exakt |

## Sources

### Primary (HIGH confidence)
- Repo, gelesen in dieser Sitzung: `02-CONTEXT.md`, `REQUIREMENTS.md`, `ROADMAP.md` (Phase 2), `STATE.md`, `01-SPIKE-ERGEBNISSE.md`, `01-SPIKE-window-hit.md`, `-zorder-wind.md`, `-menu.md`, `-icons.md`, `-tooling.md`, `-render-cost.md`, `docs/PROJEKT_START.md`, `.planning/research/{ARCHITECTURE,STACK,PITFALLS}.md`, `spikes/README.md`, `spikes/WindowHost/{HostWindow.cs,Variants/ZOrderGuard.cs,Variants/MenuNative.cs,Variants/PlateH1.cs,app.manifest,WindowHost.csproj}`, `spikes/Common/{DesktopHost.cs,RunLog.cs,native/*/NativeMethods.txt,NativeMethods.json}`, `spikes/Icons/{ShellIcon.cs,IconMetrics.cs}`, `spikes/Driver/HostProcess.cs`, `src/MehrBar.Core/Layout/{ZoomEngine,ZoomKernel}.cs`, `src/MehrBar.Tests/*`, `Directory.Packages.props`, `global.json`.
- Eigene Proben (2026-10-19, Temp-Projekte außerhalb des Repos, danach gelöscht): JSON/Konverter/Resx/`File.Replace` (net10.0), WPF + CsWin32 + H.NotifyIcon.Wpf Build/Publish/Start (net10.0-windows).
- H.NotifyIcon v2.4.1 Quelltext: `raw.githubusercontent.com/HavenDV/H.NotifyIcon/v2.4.1/src/libs/H.NotifyIcon.Shared/TaskbarIcon{,.ContextMenu.Wpf,.Dispose,.IconSource,.Properties}.cs`, `…/H.NotifyIcon/Core/TrayIcon.cs`; NuGet-Paket README.
- NuGet-API (`azuresearch-usnc.nuget.org`, `api.nuget.org` flatcontainer/registration).

### Secondary (MEDIUM confidence)
- https://source.dot.net/PresentationFramework/Microsoft/Win32/CommonDialog.cs.html — `ShowDialog()`-Besitzerwahl (`GetActiveWindow`, `ParkingHwnd`).
- https://learn.microsoft.com/dotnet/api/system.io.file.replace — `File.Replace`-Verhalten (Volume, Backup überschrieben).
- https://learn.microsoft.com/dotnet/api/microsoft.win32.filedialog.dereferencelinks — Eigenschaftsbeschreibung (Standardangabe fragwürdig, explizit setzen).
- https://devblogs.microsoft.com/oldnewthing/20041207-00/?p=37123 — `DROPEFFECT_MOVE` (über Suchzusammenfassung gelesen).

### Tertiary (LOW confidence)
- Aussagen zu Vordergrundsperre/`AllowSetForegroundWindow`, `EndMenu`, OLE-Drop auf Layered-Fenster: Windows-Wissen, in Phase 1 nicht gemessen → Annahmenliste.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Pakete gebaut, publiziert, Versionen per NuGet-API geprüft (außer Seam-Lücke für NuGet).
- Architecture: MEDIUM-HIGH — Vorlage ist Phase-1-Spike; Produkt-Keeper/Front-Modus/Menü-Wächter-Zusammenspiel neu.
- Pitfalls: HIGH für die gemessenen (Effizienzmodus, `TryCreate failed`, `init`-Extra, `File.Replace`, NETSDK1022/1135), MEDIUM für Drop/Vordergrund.

**Research date:** 2026-10-19
**Valid until:** 2026-11-18 (CsWin32 0.3.x erscheint fast täglich — Pin bleibt; H.NotifyIcon 2.5.0 stabil könnte erscheinen, nicht übernehmen ohne Messung)
