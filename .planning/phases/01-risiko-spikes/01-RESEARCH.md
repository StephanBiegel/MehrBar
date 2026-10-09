# Phase 1: Risiko-Spikes - Research

**Researched:** 2026-10-09
**Domain:** Wegwerf-Spikes für ein transparentes WPF-Fenster am Desktop-Boden (Win32-Fensterebene, Klick-Durchlass, Menüs ohne Aktivierung, Zeichenkosten), Shell-Symbole, Zoom-Mathematik als Core-Test, .NET-10-Werkzeugkette
**Confidence:** MEDIUM-HIGH (Messmethodik und Werkzeuge HIGH, am Rechner geprüft; die *Ergebnisse* der Spikes sind per Definition offen)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Spike-Code: Ort, Schnitt, Zeitlimit**
- **D-01:** Spike-Code liegt in `spikes/` im Repo, außerhalb der Solution, und wird committet (nachmessen bleibt möglich). Nach Phase 2 löschen oder eingefroren lassen.
- **D-02:** Die Zoom-Mathematik (Verankerung `cursor`/`center`) und ihr Test entstehen gleich im **echten** `src/MehrBar.Core` + `src/MehrBar.Tests`. Phase 1 legt dafür das Minimalgerüst an: Solution, `global.json` (SDK 10.0.401, MTP-Runner), `Directory.Packages.props`, Core (`net10.0`, ohne Pakete), Tests (`net10.0`, `OutputType Exe`, xunit.v3.mtp-v2 4.0.1). Die Testerkennung in VS Code (Frage 7) wird an diesem echten Projekt geklärt. Phase 3 baut darauf auf, statt neu zu schreiben. — **Reversibility:** costly — Phase 2 und 3 setzen auf diese Projektstruktur und das Testgerüst auf.
- **D-03:** Schnitt: **eine** WPF-Wegwerf-App für Fragen 1–4 (gleiches Fenster; Varianten per Schalter, Kommandozeile oder Tasten umschaltbar), ein eigener kleiner Symbol-Spike für Frage 6, Zoom als Core-Test (Frage 5). CsWin32 wird im Fenster-Spike gebaut (Frage 7).
- **D-04:** Zeitlimit **pro Spike**, etwa eine Sitzung je Frage. Ist das Budget ohne klares Ergebnis aufgebraucht, wird „No-go“ oder „unklar, Ausweichweg X“ notiert, statt weiterzubohren. Ausnahme: siehe D-11.

**Prüfung und Entscheidung**
- **D-05:** Tests laufen **möglichst automatisiert**: Ein Skript bzw. die Spike-App löst die Fälle selbst aus (SendInput für Win+D/Win+M/Alt+Tab, Ecke „Desktop anzeigen“ per Maus, Explorer beenden und neu starten, Auflösungswechsel per `ChangeDisplaySettingsEx`, Notepad starten) und prüft per Win32 (Sichtbarkeit, `IsWindowVisible`/Cloaking, Z-Order, Vordergrundfenster, Fokus), mit Protokoll und Zeitstempeln.
- **D-06:** Was sich nicht verlässlich automatisieren lässt (Sperren/Entsperren, „ohne Aufblitzen“, Menüs mit Tastatur, gestartetes Programm kommt nach vorn), kommt in einen **kurzen Handteil**, eine Checkliste für Stephan in einem Durchgang am Phasenende (passt zu `human_verify_mode: end-of-phase`).
- **D-07:** **Der echte Weg gewinnt:** Jeder automatisierte Fall wird einmal von Hand mit echtem Tastendruck gegengeprüft. Weicht das Ergebnis ab, gilt das Handergebnis, und der Fall wandert in den Handteil.
- **D-08:** Go/No-go: **Klare Kriterien entscheiden selbst.** Trifft ein Messwert die Schwelle eindeutig, ist die Notiz endgültig; nur Grenzfälle gehen an Stephan.
- **D-09:** Notizformat: **eine Datei je Spike** im Phasenordner (z. B. `01-SPIKE-window-hit.md`, `01-SPIKE-zorder-wind.md`, `01-SPIKE-menu.md`, `01-SPIKE-render-cost.md`, `01-SPIKE-zoom-anchor.md`, `01-SPIKE-icons.md`, `01-SPIKE-tooling.md`) mit Frage, Aufbau, Messwerten, Entscheidung und Folgen für Phase 2/3. Dazu eine Sammelnotiz mit allen Ergebnissen und den Änderungen am Umfang von Phase 2.
- **D-10:** Testmatrix Ebene: Win+D, Ecke „Desktop anzeigen“, Win+M, Explorer-Neustart, Sperren/Entsperren **plus** Aero Peek, Alt+Tab, Auflösungswechsel; jeweils **mit und ohne** Wächter. Ruhezustand und RDP gehören nicht dazu.

**Rückfallgrenzen**
- **D-11:** Win+D: Kurzes Verschwinden **bis etwa 0,5 s** ist hinnehmbar, wenn die Leiste **von selbst** zurückkommt und dabei **nicht vor andere Fenster springt**. Längeres oder dauerhaftes Verschwinden ist No-go.
- **D-12:** Ebene `bottom` ist nicht verhandelbar. Geht „ganz unten“ auf Build 26200 grundsätzlich nicht zuverlässig, gibt es **keinen** Ausweichweg über eine andere Ebene oder Autohide; stattdessen wird ein zweiter, gezielter Spike angesetzt (die harten Wege der Recherche: Einhängen in das Desktop-Fenster, Explorer-Hook). — **Reversibility:** one-way — eine andere Ebene würde Q10 und Stephans Arbeitsweise umstoßen.
- **D-13:** Zeichenkosten: Das **Go für Phase 2** hängt an der realen Leiste (~20 Symbole, Q18). Verfehlen 64 Symbole die Ziele, ist das **kein** No-go, sondern ein offener Punkt mit notiertem Ausweichweg (z. B. kleineres Fenster/Dirty-Rect, anderer Zeichenweg), der **vor Phase 3** gelöst sein muss, weil ZOOM-06 dort 60 fps mit 60+ Symbolen verlangt.
- **D-14:** Trefferfläche: Der Spike misst die Ausweichwege **gleich mit**: Alpha-1-Fläche, Hit-Region per `SetWindowRgn` und dynamisch umgeschaltetes `WS_EX_TRANSPARENT` (Konzept § 6). Das beste Verfahren gewinnt.
- **D-15:** Menüs: Schaffen weder WPF-ContextMenu noch TrackPopupMenu alle Punkte, darf sich die Leiste **nur für die Dauer des Menüs aktivieren** (SetForegroundWindow-Trick) und gibt den Fokus danach zurück. Pflicht bleiben: Schließen bei Klick daneben und mit Escape, Pfeiltasten/Enter, gestartetes Programm kommt nach vorn.

**Messung der Zeichenkosten**
- **D-16:** Werkzeug: **eigene Messung plus PresentMon.** Die Spike-App misst die Render-Zeit je Frame selbst (Stopwatch an `CompositionTarget.Rendering`) und die CPU über die Prozesszeit; PresentMon (MIT, Intel, eine einzelne .exe) misst unabhängig, was der DWM tatsächlich darstellt. PresentMon wird in `spikes/` heruntergeladen, aber **nicht** committet (gitignore).
- **D-17:** Ablauf: Ein Skript fährt die Maus per SendInput reproduzierbar über die Leiste, mit fester Dauer (etwa 30 s je Lauf). Matrix: **20 und 64 Symbole × Hardware und SoftwareOnly**. Ausgewertet werden Median, 95. und 99. Perzentil der Frame-Zeit sowie das CPU-Mittel.
- **D-18:** Schwelle Frame-Zeit: **95. Perzentil ≤ 16,7 ms**, höchstens ~1 % ausgefallene Frames (laut PresentMon).
- **D-19:** Schwelle CPU: **≤ 15 % Anteil an der Gesamt-CPU** wie in der Task-Manager-Spalte; der Mehraufwand von `dwm.exe` während des Zooms wird mit protokolliert und in der Notiz genannt.

### Claude's Discretion
- Konkretes Budget je Spike innerhalb von „etwa einer Sitzung“, Reihenfolge der Spikes, Dateinamen der Notizen
- Wie die Varianten im Fenster-Spike umgeschaltet werden (Kommandozeile, Tasten, Konfigurationsdatei)
- Wie die Zeichenvarianten im Spike konkret aussehen (z. B. `DrawingVisual`/`OnRender` gegen `WriteableBitmap`), solange Hardware gegen SoftwareOnly gemessen wird
- Umfang des Symbol-Spikes jenseits der Roadmap-Liste (Zeitlimit für Netzpfade, Kalt-/Warm-Latenz messen, wie in der Recherche skizziert)

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

Phase 1 trägt **keine** Anforderungs-ID (Spike senkt das Risiko für Phase 2 und 3). Stattdessen die fünf Erfolgskriterien der Roadmap:

| Kriterium | Beschreibung (Kurz) | Research-Unterstützung |
|-----------|---------------------|------------------------|
| SC1 | Trefferfläche Alpha 1, Klick-Durchlass 1 px daneben, ganz unten ohne Aufblitzen, kein Fokusdiebstahl | Abschnitt „Q1 Fenster-Host“ |
| SC2 | Win+D, Ecke, Win+M, Explorer-Neustart, Sperren: mit/ohne Wächter; Ebenenstrategie gewählt | Abschnitt „Q2 Ebene und Wächter“ |
| SC3 | WPF-ContextMenu gegen TrackPopupMenu am NOACTIVATE-Fenster; Menütechnik entschieden | Abschnitt „Q3 Menütechnik“ |
| SC4 | 64 Symbole bei 60 fps: Frame-Zeit und CPU, Hardware gegen SoftwareOnly; Zeichentechnik entschieden | Abschnitt „Q4 Zeichenkosten“ |
| SC5 | Core-Test Zoom ±6 px; Symbol-Spike 256 px sauberes Alpha; CsWin32-Build und VS-Code-Testerkennung geklärt | Abschnitte „Q5“, „Q6“, „Q7“ |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

Aus `./CLAUDE.md`, `./.claude/CLAUDE.md` und der globalen `~/.claude/CLAUDE.md`; behandelt wie gesperrte Entscheidungen:

- Mit Stephan auf **Deutsch**; Code, Kommentare, Commit-Nachrichten **Englisch**; UI-Texte Deutsch in Ressourcen-Dateien (für Spikes nicht relevant, für `src/` ja).
- **Core** ist `net10.0` (nicht `-windows`), ohne Pakete, ohne WPF/Win32; Tests in `MehrBar.Tests` (xunit.v3.mtp-v2 4.0.1, `OutputType Exe`, `Assert` statt FluentAssertions/Moq). Win32 nur in `App/Interop` (im Spike: im Spike-Projekt, nicht in Core).
- CsWin32 **0.3.360** exakt gepinnt, `allowMarshaling:false`, `PrivateAssets=all`; `EmitCompilerGeneratedFiles` nie in den Projektordner.
- Nicht verwenden: WinForms `NotifyIcon`, `EnableCompressionInSingleFile`, `PublishTrimmed`, `Assembly.Location`, `[JsonPolymorphic]` allein, FluentAssertions 8.x, gong-wpf-dragdrop.
- **Fremde Skins und Screenshots nie ins Repo** (`samples/skins/`, `samples/reference/` sind gitignored). Folge für Phase 1: Kontaktbögen mit Stephans echten Symbolen, Screenshots und Roh-Logs mit Dateipfaden/Fensternamen gehören in einen gitignorierten Ordner (`spikes/results/private/`), die Notizen verweisen nur darauf.
- GSD-Workflow: Dateiänderungen nur über GSD-Befehle; Unter-Agenten dürfen lesen/erheben, **schreibende Arbeit im Arbeitsbaum und Commits bleiben bei der ausführenden Sitzung**.
- Jede abschließende Antwort beginnt mit konkretem Rückbezug (welches Modul/welche Datei).

## Summary

Phase 1 ist ein Mess- und Entscheidungsprojekt, kein Bauprojekt. Sieben Fragen (Q1–Q7) brauchen je eine Go/No-go-Notiz. Die Stack-Recherche des Projekts ist solide und wird **wiederverwendet**: CsWin32, MTP-xUnit, die Symbol-Extraktion über `IShellItemImageFactory` und die Single-File-Eigenheiten sind probe-verifiziert; hier fehlen nur noch die vier Spike-Fragen am Fenster (Q1–Q4), die Zoom-Fixtures (Q5), die nicht geprobten Dateitypen (Q6) und die VS-Code-Erkennung (Q7). Der wichtigste Planungsgedanke: **Die Spikes brauchen eine Messmaschine, nicht nur ein Fenster.** Empfohlen ist ein Zweiprozess-Aufbau: ein **Host** (die WPF-Wegwerf-App, Prüfling) und ein **Driver** (C#-Konsole, löst Eingaben per SendInput aus und ist unabhängiges Orakel). So zählt die CPU des Treibers nicht in die Messung, und die Orakel (Pixelprobe am Bildschirm, Z-Order-Schnappschuss, Vordergrundfenster, Nachrichtenzähler im Host) sind für alle Fragen dieselben.

Drei Befunde ändern die Planung unmittelbar. (1) Das Zoom-Kriterium „vier Messreihen ±6 px“ ist **nicht ohne Fixture-Arbeit testbar**: Die Tabelle nennt nur Ränder; Rastermaß (Slot-Breite je Symbol), Trennlinienbreite und die Mausposition je Zustand fehlen, und der linke Rand −3 steht in drei Zeilen identisch (vermutlich Bildbeschnitt, also nur eine Untergrenze). Ein Nachbau am Rechner ergibt: Das Cursor-Verankerungsmodell (Kernel `cos`, `r = 2,5`, Zusatzbreite wird proportional zur Cursorposition im Symbol links/rechts verteilt) trifft alle **unbeschnittenen** Seiten auf ≤ 3 px, wenn der Slot ≈ 34–38 px und die Trennlinie klein ist; mit „Slot = 32 + Trennlinie 16“ trifft es die Reihen **nicht**. Der Test muss also Fixtures aus `rocketdock-zoom-1.png` messen und die beschnittenen Ränder als „≥“-Grenze behandeln. (2) Die CPU-Schwelle D-19 ist auf diesem Rechner (i7-12700, 20 logische Prozessoren) praktisch nicht falsifizierbar: 15 % der Gesamt-CPU sind drei volle Kerne; ein einzelner ausgelasteter UI-Thread wären nur 5 %. Die Messung soll deshalb **zusätzlich Kernäquivalente** berichten (D-19 bleibt Schwelle). (3) PresentMon sieht ein WPF-Fenster mit `AllowsTransparency` (Layered Window per `UpdateLayeredWindow`) womöglich nicht als Present-Ereignis; das ist nirgends belegt. Deshalb ist ein 5-Minuten-Vorversuch Pflicht, und die eigene Messung plus `DwmGetCompositionTimingInfo(NULL)` (globale DWM-Zähler) sind der Ersatz.

Für die Win+D-Frage gibt es ein konkretes, beschriebenes Vorbild (Rainmeter `System.cpp`, GPL: Verfahren nachbauen, **keinen Code übernehmen**): Erkennung „Desktop gezeigt“ = das Desktop-Symbol-Hostfenster liegt in der Z-Reihenfolge **über** dem eigenen Bodenfenster; Reaktion = `HWND_TOPMOST` solange gezeigt, sonst `HWND_BOTTOM`; Auslöser = `EVENT_SYSTEM_FOREGROUND`-Hook plus 250-ms-Timer (100 ms während „gezeigt“). Genau dieses Verfahren trägt ein Risiko, das D-11 benennt (Leiste springt beim Wiederherstellen kurz vor andere Fenster) — die Messung muss diese Dauer erfassen. Alle Automatisierungen laufen auf Stephans echtem Desktop; dafür sind Haltepunkte (Rechner in Ruhe lassen, Arbeit speichern, Notabbruch) zwingender Teil des Plans.

**Primary recommendation:** Zuerst das Gerüst (D-02) und den Host mit Driver und Orakeln bauen (Q7 fällt dabei ab), dann Q1 → Q2/Q3/Q4 als **getrennte Variantendateien** im Host (parallelisierbar ohne Dateikonflikte), Q5 und Q6 unabhängig daneben; alle Handprüfungen in **einem** Durchgang am Ende.

## Architectural Responsibility Map

Hier bedeutet „Ebene“ die Rolle im Spike-Aufbau (keine Produktarchitektur).

| Fähigkeit | Primäre Ebene | Sekundäre Ebene | Begründung |
|-----------|---------------|-----------------|------------|
| Fensterstil, HWND_BOTTOM, Hit-Plate, Nachrichtenzähler | Host (`spikes/WindowHost`, WPF + CsWin32) | — | Das ist der Prüfling; Win32 nur dort, nie in Core |
| Eingabe auslösen (SendInput Win+D/Win+M/Alt+Tab, Mausfahrt, Klicks, Tasten im Menü) | Driver (`spikes/Driver`, Konsole) | PowerShell-Hüllen | Eigener Prozess: Treiber-CPU verfälscht die Host-Messung nicht; Klicks kommen „von außen“ |
| Orakel „sichtbar“ (Pixelprobe am Bildschirm), Z-Order-Schnappschuss, Vordergrundfenster, Fokus | Driver (`spikes/Common` als verlinkter Quellcode) | Host (Selbstlog) | Unabhängige Sicht von außen; Host-Selbstlog nur ergänzend |
| Frame-Zeit, CPU, GC | Host (Selbstmessung) | Driver (`GetProcessTimes` Host + `dwm.exe`), PresentMon | Drei unabhängige Quellen, Querprüfung nach D-16 |
| Zoom-Mathematik + Test | `src/MehrBar.Core` + `src/MehrBar.Tests` (echt, D-02) | — | Reine Logik, ohne Oberfläche testbar |
| Symbol-Extraktion | `spikes/Icons` (Wegwerf) | später `App/Interop` | Nur Messung; Produktcode kommt in Phase 2 |
| Menü (WPF vs. nativ) | Host (Variantendatei) | Driver (Eingabe/Orakel) | Verhalten hängt am nicht aktivierbaren Fenster |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK | 10.0.401 [VERIFIED: `dotnet --list-sdks` this session: `10.0.401 [C:\Program Files\dotnet\sdk]`] | Build aller Projekte | Von Stephan festgelegt; in `global.json` pinnen |
| Microsoft.Windows.CsWin32 | 0.3.360 [VERIFIED: NuGet flat-container API this session, letzte Versionen `0.3.355, 0.3.358, 0.3.360`] | Win32 für Host/Driver/Icons | Projektentscheidung; Liste unten am Rechner kompiliert |
| xunit.v3.mtp-v2 | 4.0.1 [VERIFIED: NuGet API this session, letzte `4.0.0, 4.0.1`] | Core-Tests | Projektentscheidung D-02 |
| WPF (`net10.0-windows`) | in-box | Host-Fenster | Konzept |
| PresentMon | 2.6.0, `PresentMon-2.6.0-x64.exe`, 980 320 Byte, MIT, veröffentlicht 2026-09-21, SHA-256 `b2a706bc6ad475749e3b7e3409263aa1e6906d45bdcf993f6dbc0f660188f1af` [VERIFIED: GitHub releases API this session] | Unabhängige Present-Messung | D-16; Download nach `spikes/tools/` (gitignored), SHA-256 vor Benutzung vergleichen und in der Notiz festhalten |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Diagnostics.Process` / `GetProcessTimes` | in-box | CPU-Zeit von Host und `dwm.exe` | Jeder Renderlauf |
| `Get-Counter` / `typeperf` (`\GPU Engine(pid_*)\Utilization Percentage`) | in-box | GPU-Anteil Host und DWM | Nur als Beleg zur Notiz |
| WPR/xperf (Windows Performance Toolkit liegt im PATH [VERIFIED: PATH-Ausgabe, `wpr` meldet 10.0.26100]) | in-box | Nur falls PresentMon das Layered-Fenster nicht sieht und eine Ablaufverfolgung nötig wird | Nachrangig |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Driver als C#-Konsole | Reines PowerShell + `Add-Type` | PS 5.1 kompiliert mit altem C#-Compiler, doppelte P/Invoke-Deklarationen, kein CsWin32; Treiber in C# teilt sich Orakel-Code mit dem Host |
| Pixelprobe als Orakel | `PrintWindow`, `IsWindowVisible` | `PrintWindow(PW_RENDERFULLCONTENT)` liefert den Fensterinhalt **auch wenn verdeckt**, `IsWindowVisible` bleibt bei verdecktem Fenster `true`: beide taugen nicht als „sichtbar“ [ASSUMED: aus Win32-Semantik, im Spike an Nicht-Fall gegenprüfen] |
| PresentMon als einzige Quelle | Eigenmessung | PresentMon braucht Admin (ETW) und sieht Layered-Fenster eventuell nicht — Eigenmessung bleibt Primärquelle |

**Installation (Gerüst, D-02):**
```bash
# im Repo-Wurzelordner; SDK 10 erzeugt per Voreinstellung .slnx (siehe Pitfall 9)
dotnet new sln -n MehrBar --format sln
dotnet new classlib -n MehrBar.Core -o src/MehrBar.Core -f net10.0
dotnet new console  -n MehrBar.Tests -o src/MehrBar.Tests -f net10.0   # dann csproj auf OutputType Exe + xunit.v3.mtp-v2, Using Xunit
dotnet sln add src/MehrBar.Core src/MehrBar.Tests
```
Versionen zentral in `Directory.Packages.props` (nur CsWin32 und xunit.v3.mtp-v2; H.NotifyIcon erst in Phase 2). `global.json` genau wie in `STACK.md` § 12: `{ "sdk": { "version": "10.0.401", "rollForward": "latestFeature" }, "test": { "runner": "Microsoft.Testing.Platform" } }`.

**Version verification:** CsWin32 und xunit am NuGet-API-Index bestätigt (oben). `npm view`/`pip` entfallen (kein Node/Python im Produkt).

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| Microsoft.Windows.CsWin32 0.3.360 | NuGet | n. a. (63 Versionen im Index [VERIFIED: NuGet API]) | n. a. (nicht abgefragt) | github.com/microsoft/CsWin32 [ASSUMED] | n. a. (Seam kennt NuGet nicht) | Approved: Projektentscheidung, in `STACK.md` mit Probes belegt; in dieser Sitzung kompiliert |
| xunit.v3.mtp-v2 4.0.1 | NuGet | 10 Versionen im Index [VERIFIED: NuGet API] | n. a. | github.com/xunit/xunit [ASSUMED] | n. a. | Approved: Projektentscheidung D-02 |
| PresentMon-2.6.0-x64.exe | GitHub Release (kein Paket) | veröffentlicht 2026-09-21 [VERIFIED: GitHub API] | n. a. | github.com/GameTechDev/PresentMon, Lizenz MIT [VERIFIED: GitHub API `license.spdx_id`] | n. a. | Approved: D-16; SHA-256 prüfen (siehe oben), **nicht** committen |

`gsd-tools query package-legitimacy check` unterstützt nur `npm|pypi|crates` (Aufruf in dieser Sitzung: „Usage: … --ecosystem <npm|pypi|crates>“); für NuGet gibt es keinen Verdikt. Es werden **keine neuen Pakete** gegenüber der Projektrecherche eingeführt.

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### Spike-Messaufbau (Datenfluss)

```
 Stephan (Haltepunkt: Rechner in Ruhe lassen, Arbeit gespeichert)
        |
        v
 PowerShell-Hülle run-*.ps1 ──(startet, wartet, sammelt)──┐
        |                                                  |
        v                                                  v
 Driver.exe  case <name> --variant ...              Host.exe --variant <A|B|..> --log <csv>
 (SendInput: Win+D, Maus, Tasten; ChangeDisplaySettingsEx;    (WPF, AllowsTransparency, CsWin32;
  taskkill/explorer; LockWorkStation)                           Fenster 1900x250, Zählleisten)
        |   Orakel (von außen, unabhängig)                          |   Selbstlog (Nachrichtenzähler,
        |   - Pixelprobe Bildschirm (Sentinel-Farbe)                |    WinEvents, Frame-Zeit, GC)
        |   - Z-Order-Schnappschuss (EnumWindows, Host-Fenster)     |
        |   - GetForegroundWindow, GetGUIThreadInfo                 |
        |   - GetProcessTimes (Host, dwm.exe)                       |
        v                                                           v
   results/<run>.csv  (ts_ms, case, variant, guard, event, detail)  results/<run>-host.csv
        |
        v   (+ PresentMon-CSV, nur Renderläufe, Admin)
 Auswertung (kleines Skript/C#) -> Zahlen in 01-SPIKE-<name>.md  (D-09)  -> Go/No-go nach Schwelle (D-08)
```

### Empfohlene Struktur

```
spikes/                              # außerhalb der Solution (D-01), committet
├── README.md                        # Zweck, wie man nachmisst, Verweis auf Phasenordner
├── Common/                          # Quellcode, per <Compile Include="..\Common\*.cs" Link=...> in Host+Driver
│   ├── Oracles.cs                   # PixelProbe, ZOrderSnapshot, ForegroundInfo, Stats (Median/p95/p99)
│   └── NativeMethods.txt/.json      # EINE Liste, von beiden Projekten verlinkt
├── WindowHost/                      # net10.0-windows, UseWPF, CsWin32 (Q1-Q4, Q7)
│   ├── Program.cs / App.xaml        # Kommandozeile --variant X --count N --render hw|sw --log ...
│   ├── VariantRegistry.cs           # EINZIGE Datei, die alle Varianten kennt (Plan 1 schreibt sie einmal)
│   ├── Variants/HitPlate.cs         # Q1 (+ SetWindowRgn, WS_EX_TRANSPARENT als Varianten, D-14)
│   ├── Variants/ZOrderGuard.cs      # Q2 (Wächter aus/ein, Strategien)
│   ├── Variants/MenuWpf.cs, MenuNative.cs   # Q3
│   └── Variants/RenderCost.cs       # Q4 (OnRender, DrawingVisual, Kontrolle ohne Transparenz)
├── Driver/                          # net10.0-windows Konsole, CsWin32
│   └── Cases/*.cs                   # je Testfall eine Datei (WinD, WinM, Corner, ExplorerRestart, Lock, Display, Sweep, Click, MenuKeys)
├── Icons/                           # net10.0-windows Konsole (Q6), gleiche Orakel-Idee (Alpha-Prüfung)
├── fixtures/                        # vom Skript erzeugt: Only48.exe, .url, .pdf, Ordner (nichts Fremdes)
├── tools/                           # PresentMon-2.6.0-x64.exe (GITIGNORED), run-*.ps1
└── results/                         # kleine CSV/Logs committet; results/private/ GITIGNORED (Kontaktbögen, Screenshots, Pfade)
```

Wichtig für die Parallelisierung (config `parallelization: true`): Q2, Q3, Q4 fassen **nur ihre eigene Variantendatei** an. `VariantRegistry.cs` und `Program.cs` schreibt der erste Plan einmal fertig (Reflection oder Dictionary mit Platzhaltern), danach kollidiert nichts.

Wegen `Directory.Packages.props` im Repo-Wurzelordner gilt zentrale Paketverwaltung auch für `spikes/*`: Spike-csproj tragen `<PackageReference Include="Microsoft.Windows.CsWin32" PrivateAssets="all" />` **ohne** `Version`, und der Pin steht im Wurzel-`Directory.Packages.props` (sonst Fehler NU1008). [ASSUMED: Verhalten der zentralen Paketverwaltung; beim ersten Build prüfen]

### Pattern 1: Fenster-Host (Q1) — Ablauf ohne Aufblitzen

**What:** Den HWND früh erzeugen, Stile und Ebene setzen, **bevor** der erste nichttransparente Pixel gezeichnet wird.
**When to use:** Immer für den Host; ist zugleich die Vorlage für Phase 2.
```csharp
// Skizze. Konstanten ausschließlich aus CsWin32 (PInvoke.*, WINDOW_EX_STYLE.*, SET_WINDOW_POS_FLAGS.*), keine Literale.
// Window: WindowStyle=None, AllowsTransparency=true, Background=Transparent, ShowInTaskbar=false,
//         ShowActivated=false, Topmost=false, ResizeMode=NoResize
protected override void OnSourceInitialized(EventArgs e)
{
    base.OnSourceInitialized(e);                       // HWND existiert, Fenster noch nicht sichtbar gezeichnet
    var hwnd = (HWND)new WindowInteropHelper(this).Handle;
    var ex = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
    PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
        (int)(ex | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE));
    PInvoke.SetWindowPos(hwnd, HWND.HWND_BOTTOM, x, y, 1900, 250,
        SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE /* | SWP_SHOWWINDOW nur falls getestet */);
    HwndSource.FromHwnd((nint)hwnd).AddHook(WndProc);  // WM_MOUSEACTIVATE -> MA_NOACTIVATE
}
```
`WS_EX_NOACTIVATE` (0x08000000) und `WS_EX_TOOLWINDOW` (0x00000080) sind laut Microsoft Learn „nicht aktiviert beim Klick“ bzw. „nicht in Taskleiste/Alt+Tab“ [CITED: learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles]. Dieselbe Seite sagt: „To activate the window, use the SetActiveWindow or SetForegroundWindow function“ — ein NOACTIVATE-Fenster **kann also programmatisch** in den Vordergrund geholt werden (Grundlage für D-15). `GetWindowLong`/`SetWindowLong` sind in CsWin32 die richtigen Namen (nicht `…Ptr`, siehe Pitfall 8).
Was mit `SWP_SHOWWINDOW`/`Show()` an Aufblitzen passiert, ist **offen** und wird gemessen: Die Fragestellung ist, ob vor dem ersten nichttransparenten Frame schon die Ebene gesetzt ist; das Host-Selbstlog schreibt dafür die Z-Position beim ersten `Rendering` mit Inhalt.

### Pattern 2: Wächter nach Rainmeter-Vorbild (Q2)

**What:** Erkennung „Desktop gezeigt“ = Desktop-Symbol-Host liegt in der Z-Reihenfolge über dem eigenen Fenster; dann `HWND_TOPMOST`, sonst `HWND_BOTTOM`.
**Quelle:** [CITED: raw.githubusercontent.com/rainmeter/rainmeter/master/Library/System.cpp, in dieser Sitzung gelesen; Lizenz GPL v2 — Verfahren nachbauen, Code **nicht** kopieren, das spätere Release soll öffentlich werden]
- Hook: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, …, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS)`; Timer 250 ms (während „gezeigt“ 100 ms) als Rückfall.
- Desktop-Host: das Fenster, das `SHELLDLL_DefView` besitzt; vor 24H2 ein `WorkerW`, ab 24H2 `Progman` (Win+D-Hierarchie geändert); **nie über Explorer-Neustarts hinweg merken**.
- Platzierung: `SetWindowPos(..., SWP_NOMOVE | SWP_NOSIZE | SWP_NOOWNERZORDER | SWP_NOACTIVATE | SWP_NOSENDCHANGING)`; bei „gezeigt“ `HWND_TOPMOST`, danach „nach dem ersten Topmost-Fenster einfügen“, sonst `HWND_BOTTOM`.
- Der Hook-Callback braucht eine Nachrichtenschleife im aufrufenden Thread und einen festgehaltenen Delegaten (`GCHandle`/statisches Feld) [CITED: learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook: „The client thread that calls SetWinEventHook must have a message loop“; „you should use the GCHandle structure“].

### Pattern 3: Orakel „Leiste sichtbar“ (für Q1, Q2, Q3)

Die Leiste malt im Orakelmodus ein **opakes Sentinel-Rechteck** (z. B. `#FF00FF`, 5 Probepunkte); der Driver liest nach jedem Schritt die Bildschirmpixel (`GetDC(NULL)` + `GetPixel`, kein Fensterzugriff) und wertet „sichtbar“ = ≥ 4 von 5 Punkten innerhalb ±8 je Kanal. Parallel ein Z-Order-Schnappschuss (`EnumWindows` von oben nach unten: Position der Leiste, des Desktop-Hosts, aller sichtbaren, nicht gecloakten (`DWMWINDOWATTRIBUTE.DWMWA_CLOAKED`), nicht minimierten Fenster, die die Leistenfläche schneiden). Selbsttest des Orakels (Pflicht, Wave 0): (a) Leiste frei → sichtbar; (b) Notepad über die Leiste geschoben → **nicht** sichtbar; sonst taugt das Orakel nicht [ASSUMED: dass `GetPixel` auf dem Bildschirm-DC das DWM-Ergebnis liefert; genau dieser Selbsttest prüft es].
„Nicht vor andere Fenster gesprungen“ (D-11) = **Deckungstest**: Notepad liegt vorher über der Leistenfläche; nach dem Auslöser und der Wiederherstellung muss an einem Probepunkt der Notepad-Pixel stehen, nicht das Sentinel; die Zeit, in der das Sentinel doch oben liegt, wird mit 10-ms-Abtastung während der Wiederherstellung gemessen.

### Anti-Patterns to Avoid
- **Einen Satz Spike-Code für alles in einer Datei:** Q2/Q3/Q4 blockieren sich im Plan, und das Ergebnis ist nicht nachmessbar.
- **Zoom-Mathematik im Spike-Host nachbauen und später wegwerfen:** Die echte steht in `src/MehrBar.Core` (D-02). Der Renderlauf benutzt eine eigene 5-Zeilen-Kosinusfunktion nur als Arbeitslast.
- **Elevated alles starten:** Nur der PresentMon-Lauf braucht Admin; der Fenstertest muss auf normaler Integrität laufen (UIPI, Fokusregeln).
- **Win32-Konstanten als Zahlenliterale:** CsWin32 liefert sie typisiert (`PInvoke.MA_NOACTIVATE` kompiliert und ergibt 3 [VERIFIED: Build-Lauf dieser Sitzung, Ausgabe `… 3 WS_EX_NOACTIVATE`]).

## Spike-Methoden je Frage

Reihenfolge-Empfehlung und Zeitbudget (Claude's Discretion; D-04 „etwa eine Sitzung je Frage“): **Gerüst+Q7 (1) → Host-Skelett+Q1 (1) → Q2 (1, schwerste) ‖ Q3 (½) ‖ Q4 (1) ‖ Q5 (½–1) ‖ Q6 (½) → Sammelnotiz + Handteil (½)**. Q1 zuerst, weil Q2–Q4 denselben Host brauchen.

### Notizformat (D-09) — maschinenprüfbar machen
Jede `01-SPIKE-*.md` hat genau diese Überschriften: `## Frage`, `## Aufbau`, `## Messwerte` (Tabelle, Rohdaten-Dateinamen), `## Entscheidung` (enthält **eine** Zeile `**Entscheidung:** GO | NO-GO | UNKLAR — <Strategie/Ausweichweg>`), `## Folgen für Phase 2/3`, `## Grenzfälle für Stephan` (leer oder Liste). Ein kleines Prüfskript (`spikes/tools/check-notes.ps1`) prüft Existenz, Überschriften und die eine Entscheidungszeile → das ist der automatisierbare Teil der Phasenprüfung.

### Q1 — Trefferfläche, Klick-Durchlass, erstes Erscheinen, Fokus (`01-SPIKE-window-hit.md`)
- **Aufbau:** Host 1900×250, oben mittig; „Leiste“ = Rechteck R (z. B. 600×56) mit Alpha-1-Füllung (`Color.FromArgb(1,0,0,0)`; WPF braucht zusätzlich eine **nicht-null Pinsel**, sonst trifft auch WPF-Hit-Test nicht — Alpha 1 erfüllt beides). Varianten H1 Alpha-1-Fläche, H2 `SetWindowRgn` (Region = R; Kosten und Wirkung auf Zoom-Überhang messen, D-14), H3 `WS_EX_TRANSPARENT` per 16-ms-Timer-Abfrage von `GetCursorPos` umgeschaltet (Eintritts-Latenz messen). Für H1 gilt laut WPF-Team: Pixel mit Alpha 0 reichen die Maus an das Fenster darunter weiter, Alpha > 0 fängt sie [CITED: Dwayne Need, „Transparent Windows in WPF“, learn.microsoft.com/en-us/archive/blogs/dwayneneed/transparent-windows-in-wpf, über `PITFALLS.md` Pitfall 2; auf Build 26200 zu bestätigen].
- **Messung (Driver, je 100 Klicks, Zufallspunkte, Seed festhalten):** (a) Klick in R → Host-Zähler `WM_LBUTTONDOWN`/`WM_MOUSEMOVE` steigen; (b) Klick **1 px** außerhalb R (rechts, links, oben, unten, und in einem Lücken-Rechteck im künftigen Zoombereich) → Zähler steigen **nicht**; Gegenprobe: Vordergrundfenster nach dem Klick ist Desktop-Host (`Progman`/`WorkerW`) und das Klick-Ereignis wurde am Treiber per `WH_MOUSE_LL` gesehen. Ein Zeuge unter der Leiste ist nicht möglich (Leiste liegt ganz unten), deshalb Nachrichtenzähler + Vordergrundwechsel statt `WindowFromPoint` (dessen Verhalten bei Alpha 0 ist unbelegt [ASSUMED]; nur als Zusatzdatenpunkt aufnehmen).
- **Fokus:** Notepad starten, `SetForegroundWindow` (mit dem bekannten Alt-Tastendruck-Trick, falls Vordergrundsperre greift), Klick in R, Rechtsklick in R, Mausbewegung → `GetForegroundWindow()` und `GetGUIThreadInfo` bleiben Notepad (100/100). Zusätzlich ein eigener „Zeuge“-Prozess als aktivierbares Fenster (deterministischer als das Store-Notepad, dessen Prozess-/Fensterzuordnung über Stubs laufen kann [ASSUMED]).
- **Erstes Erscheinen:** Host 10× starten (Driver), je Lauf Z-Position beim ersten nichttransparenten Frame loggen; „ohne Aufblitzen“ selbst ist **Handteil** (D-06), das Log liefert die objektive Hälfte.
- **Pixel-Basisfall:** Leiste sichtbar ohne bedeckende Fenster. Ist sie es auf Build 26200 **nicht**, ist das der D-12-Fall (zweiter Spike), nicht stillschweigend ein anderer Weg.
- **Kriterium (D-08):** GO für H1, wenn (a) ≥ 99 % gefangen, (b) 0 % durchgelassen-an-Leiste für 1-px-Außenpunkte, Fokus 100/100, Ebene beim ersten Frame bereits unten. Sonst die zweite Variante, Rang nach: Korrektheit > keine Kosten pro Frame > Einfachheit.

### Q2 — Ebene gegen Win+D und Verwandte (`01-SPIKE-zorder-wind.md`)
- **Matrix (D-10):** Auslöser × Wächter{aus, ein} × je 5 Wiederholungen: Win+D (SendInput `VK_LWIN`+`D`, danach zweites Win+D = Wiederherstellen), Ecke „Desktop anzeigen“ (Klick auf den äußersten Taskleistenpunkt; Taskleistenlage aus `Shell_TrayWnd` bestimmen; Option „Ecke“ ist per Voreinstellung nicht gesetzt: `TaskbarSd` fehlt im Registry [VERIFIED: Abfrage dieser Sitzung, leer] → Wert 1 für den Test setzen und danach **zurückstellen**), Win+M (Wiederherstellung Win+Shift+M), Aero Peek (Mausverweilen ≥ 1,5 s auf der Ecke; `EnableAeroPeek = 1` [VERIFIED: Registry-Abfrage]; zusätzlich `Win+,` gehalten), Alt+Tab (Leiste darf **nicht** erscheinen), Explorer-Neustart (`taskkill /f /im explorer.exe`, auf automatische Rückkehr warten, nur bei Ausbleiben nach 5 s `explorer.exe` starten), Auflösungswechsel (siehe Risiko), Sperren/Entsperren (`LockWorkStation`, Stephan entsperrt; Host loggt `WM_WTSSESSION_CHANGE` und Z-Order zu T+0/0,5/1/2 s nach Entsperren).
- **Erfassung je Fall:** (1) Sentinel-Pixel alle 10 ms von Auslöser bis +3 s → „Lücke“ = erste/letzte Zeit ohne Sentinel; (2) Deckungstest mit Notepad über der Leiste → Dauer „Leiste vor Notepad“; (3) Z-Order-Schnappschuss und Rohereignisse (alle `EVENT_SYSTEM_*`-Ereignisse und `EVENT_OBJECT_REORDER` per Hook **nur zum Protokollieren**, um zu sehen, welches Signal Win+D wirklich liefert — Rainmeter nimmt `EVENT_SYSTEM_FOREGROUND` als gegeben, auf 26200 ist das zu bestätigen).
- **Strategien (Variantendatei `ZOrderGuard`):** S0 nur `HWND_BOTTOM` (Erwartung laut Recherche: Leiste verschwindet bei Win+D [CITED: `PITFALLS.md` Pitfall 1, Raymond Chen / Microsoft Q&A / Rainmeter]; hier **messen**, nicht annehmen); S1 Rainmeter-Wächter (Pattern 2); S2 „einfügen direkt über dem Desktop-Host“ ohne Topmost [ASSUMED, nur falls S1 an D-11 scheitert]. Beschränkung D-12: Topmost ist nur ein **zeitweiliges** Werkzeug des Wächters, die Leiste bleibt bei `bottom`.
- **Kriterium (D-11):** je Auslöser: Lücke ≤ 500 ms, Rückkehr von selbst, **keine** Zeit vor Notepad (0 sichtbare Abtastwerte). „Dauer vor Notepad 1–2 Abtastwerte (10–20 ms)“ = Grenzfall → Stephan. Sperren/Entsperren ist Handteil, aber mit Host-Log. Strategie = einfachste Variante, die alle Auslöser besteht.
- **Risiken für Stephan (Plan muss Haltepunkte enthalten):** Win+D/Win+M minimiert **alle** Fenster inkl. der Terminals mit laufenden Sitzungen; `taskkill explorer` schließt Explorer-Fenster/Drags; ein Auflösungswechsel kann Desktop-Symbolanordnungen verschieben (Desktop hat viele Dateien [VERIFIED: Verzeichnisliste]) → vorher Zustimmung, Anordnung sichern oder die weniger invasive Variante „Bildwiederholrate umstellen“ zuerst; **Wiederherstellung im `finally` des Treibers und per Notabbruchtaste** (z. B. Strg+Alt+Q global, Zeitüberschreitung je Fall). Der Rechner hat einen Monitor 1920×1080 bei 60 Hz [VERIFIED: CIM-Abfrage].

### Q3 — Menütechnik am NOACTIVATE-Fenster (`01-SPIKE-menu.md`)
- **Varianten:** M-A WPF-`ContextMenu` am Host; M-B WPF-`ContextMenu` mit **temporärer Aktivierung** (`SetForegroundWindow(Leiste)` vor dem Öffnen, danach Fokus an das vorherige Fenster zurück; D-15); M-C native `CreatePopupMenu`/`TrackPopupMenuEx(TPM_RETURNCMD)` mit `SetForegroundWindow(Besitzer)` davor und `PostMessage(WM_NULL)` danach — laut Microsoft Learn Pflicht, sonst „the menu will not disappear when the user clicks outside of the menu“ und beim zweiten Öffnen „appears and then immediately disappears“ [CITED: learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-trackpopupmenu, Remarks]; M-D native mit **unsichtbarem Hilfsfenster** als Besitzer (Leiste wird nicht angefasst).
- **Warum M-D mitnehmen:** `SetForegroundWindow` auf die Leiste hebt sie (Aktivierung hebt in der Z-Reihenfolge) und holt sie kurz vor alle Fenster (Annahme [ASSUMED], im Spike über Z-Order-Schnappschuss während des Menüs beobachten). Ein Hilfsfenster vermeidet das.
- **Prüfpunkte je Variante (Pflicht aus D-15 fett):** öffnet an der Mausposition; **schließt bei Klick auf Desktop / auf anderes Fenster / auf eine andere Stelle der Leiste**; **schließt mit Escape**; **Pfeil ab/auf + Enter wählen den Eintrag** (Treiber sendet `VK_DOWN`,`VK_DOWN`,`VK_RETURN`, Host loggt welcher Eintrag `Click`/`TPM_RETURNCMD` lieferte); Mausauswahl; **gestartetes Programm (Eintrag startet Notepad/Rechner) kommt nach vorn** (`GetForegroundWindow` Prozess = gestartetes Programm innerhalb 2 s); zweites Öffnen funktioniert; Vordergrundfenster vorher/nachher; Leiste während/nach dem Menü weiter unten (Z-Order); Fokus nach Schließen wieder beim vorherigen Fenster.
- **Automatisierung:** Fast alles per SendInput prüfbar; Treiber-Eingaben sind aber „synthetisch“: Vordergrundrechte und Tastaturweg können gegenüber echter Eingabe abweichen → D-07 einmal von Hand je Variante (Handteil). Optik (WPF-Stil gegenüber nativer Menüoptik unter Windows 11, Dunkelmodus, DPI) als Screenshot beider im `private/`-Ordner für Stephans Auge.
- **Kriterium:** einfachste Variante, die alle fetten Punkte besteht (Reihenfolge der Einfachheit: M-A < M-C < M-B < M-D); schafft keine alle, gilt D-15 (zeitweilige Aktivierung) und das Ergebnis nennt die Variante.

### Q4 — Zeichenkosten (`01-SPIKE-render-cost.md`)
- **Arbeitslast:** 20 und 64 Symbole aus synthetischen 256-px-`Pbgra32`-Bitmaps (eingefroren; Kreis/Verlauf mit weichem Alpha), Größen je Frame aus eigener Kosinusfunktion (Basis 32, Zoom 121, `r = 2,5`) um die per `GetCursorPos` gelesene Mausposition; Fenster 1900×250, `AllowsTransparency=true`. Zeichenvarianten (Claude's Discretion): **R1** ein `FrameworkElement.OnRender` mit `InvalidateVisual()` je Frame; **R2** ein `DrawingVisual` je Symbol, nur Transformationen je Frame ändern (retained); optional **R3** `WriteableBitmap`; **Kontrolle K** gleiche Zeichnung in einem **nicht** transparenten Fenster (trennt Transparenzkosten von Zeichenkosten); Skalierungsmodus (`BitmapScalingMode` HighQuality gegen Linear) als Nebenachse bei SoftwareOnly.
- **Modus:** `RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly` muss **vor dem ersten Fenster** gesetzt werden (App-Konstruktor/`OnStartup`); die Doku nennt es eine „preference“, andere Systemteile können Software erzwingen [CITED: learn.microsoft.com/en-us/dotnet/api/system.windows.media.renderoptions.processrendermode] → der Host muss den **tatsächlich wirksamen** Modus ausgeben (z. B. `RenderCapability.Tier` und Prozessmodus) und die Notiz ihn nennen.
- **Lauf (D-17):** Driver fährt die Maus 30 s lang auf einer festen Sinusbahn (x = Mitte + A·sin(2πt/T), Schritt alle ~8 ms, A so, dass 3–4 Symbole überstrichen werden) per `SendInput` (absolut, virtueller Desktop, 0..65535); 5 s Einlaufzeit verwerfen; 5-s-Ruhelauf am Ende; Matrix 2 Zahlen × 2 Modi × R1/R2 (+K) = 8–10 Läufe ≈ 6 min.
- **Messgrößen (alle drei Quellen):**
  1. *Host-Selbstmessung:* je Frame Stopwatch um Handler + `OnRender` (= Aufbaukosten), Abstand zweier `CompositionTarget.Rendering`-Ereignisse (= Kadenz; `RenderingEventArgs.RenderingTime` verwenden und doppelte Zeitstempel überspringen), GC-Zähler 0/1/2 und allokierte Bytes je Frame.
  2. *Prozess-CPU:* Driver ruft `GetProcessTimes` für Host **und** `dwm.exe` zu Beginn/Ende; CPU% = ΔCPU-Zeit / (Wandzeit × 20 logische Prozessoren) [VERIFIED: i7-12700, 12 Kerne, 20 logische Prozessoren] — das ist die Task-Manager-Spalte (D-19). **Zusätzlich** Kernäquivalente (ΔCPU/Wandzeit) berichten, weil 15 % hier drei Kerne bedeuten.
  3. *PresentMon* (Admin, eigener Aufruf je Lauf: `--process_name`, `--output_file`, `--timed 30`, `--terminate_after_timed` [CITED: GameTechDev/PresentMon README-ConsoleApplication, in dieser Sitzung gelesen]); Spaltennamen der 2.6-Ausgabe zur Laufzeit aus der CSV-Kopfzeile lesen (README nennt `MsBetweenPresents`/`MsBetweenDisplayChange`, 2.x kann umbenannt haben [ASSUMED]). **Vorversuch (Wave 0):** sieht PresentMon überhaupt Zeilen für das transparente WPF-Fenster? WPF rendert Layered-Fenster mit `UpdateLayeredWindow`, kein Swapchain-Present [CITED: Dwayne Need, über `PITFALLS.md`] → womöglich **keine** Zeilen. Dann: `DwmGetCompositionTimingInfo(NULL)`-Differenzen (`cFramesDropped`, `cFramesMissed`) am Anfang/Ende des Laufs (Handle muss seit Windows 8.1 `NULL` sein [CITED: learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmgetcompositiontiminginfo]; Werte gelten global, deshalb nur auf ruhigem Desktop) und Host-Kadenz als Ersatz, PresentMon-Befund in der Notiz „nicht anwendbar“.
- **Auslegung der Schwellen (Klarstellung zu D-18, keine Änderung):** Bei 60 Hz bildet eine perfekte Kadenz 16,67 ms mit Jitter; „p95 ≤ 16,7 ms“ auf *Kadenz* wäre durch Jitter allein gerissen. Empfohlene Auswertung: p95 der **Aufbaukosten** ≤ 16,7 ms; „ausgefallener Frame“ = Kadenz-Abstand > 25 ms (1,5 Perioden) oder PresentMon-Abstand ≥ 2 Perioden; ≤ ~1 % davon; bei PresentMon-Abständen (auf Perioden quantisiert) p95 ≤ 17 ms. Mit Stephan abzustimmen, wenn die Zahl knapp daneben liegt (Grenzfall, D-08).
- **Entscheidung (D-13):** GO für Phase 2, wenn die reale Größe (20 Symbole) in der **besten** Variante besteht. Verfehlen 64 Symbole: kein No-go, sondern „offen vor Phase 3“ mit gemessenem Ausweichweg aus der Leiter in `PITFALLS.md` Pitfall 3 (kleineres Fenster/getrennte Beschriftung, Glass/WindowChrome mit Hit-Test-Folgen, eigenes D2D/DirectComposition-Fenster) und einer Messung der ersten Stufe (Fensterhöhe 250 → 160, Breite 1900 → ~1000) im selben Spike.
- **Idle:** 30-s-Ruhelauf ohne Maus: Rendering-Abonnement getrennt, Zähler 0 Frames, CPU ≈ 0 (Beleg für ZOOM-06 in Phase 3).

### Q5 — Zoom-Verankerung als Core-Test (`01-SPIKE-zoom-anchor.md`)
**Eingangsdaten (Messtabelle, verbatim):** Ruhe 109/698 (Breite 589, Mitte 404); Maus mittig −3/812; Maus links (2. Symbol) 27/812; Maus rechts (Publisher) −3/804; Maus ganz rechts (Excel) −3/733 [VERIFIED: `docs/PROJEKT_START.md:384-390`, Tabelle `| Ruhe | 109 | 698 | 589 | 404 |` …; und CONTEXT.md „Specifics“].
**Aufbau:** Reine Funktion in `src/MehrBar.Core` (achsenneutral: Abstand `u` entlang der Leistenachse), Test in `src/MehrBar.Tests` mit `Assert.InRange`/`Assert.Equal(expected, actual, precision)`.

**Befunde aus einem Nachbau am Rechner (Python, Wegwerf, nicht im Repo) — [VERIFIED: Rechenlauf dieser Sitzung; Rastermaße aus Augenmaß am Screenshot = [ASSUMED]]:**
- Symbole in `rocketdock-zoom-1.png`: 16 Symbole in Gruppen 6/6/3/1, 3 Trennlinien; Breite 589 [VERIFIED: Bild gelesen].
- Modell: Kernel `f(d) = (cos(π·d/r)+1)/2` für `d<r`, `d` = |Cursor − Ruhemitte| / Slotbreite; Zusatzbreite `e = (121−32)·f`; Zusatzbreite des Symbols **unter** dem Cursor wird im Verhältnis der Cursorposition im Symbol auf links/rechts verteilt (nicht pauschal halbiert); alles links wird um die Summe links, alles rechts um die Summe rechts verschoben (= Verankerung `cursor`).
- Ergebnis A (naives Raster: Slot 32, Trennlinie 16, `r = 2,5`, Cursor in Symbolmitte): Maus mittig −111/+111 ✓, aber 2. Symbol −103/+111 (Soll −82/+114), Publisher +75 (Soll +106), Excel +44 (Soll +35) — **trifft die Reihen nicht**.
- Ergebnis B (Slot 34–38, Trennlinie ≈ 2, `r = 2,5`, Cursor frei innerhalb des benannten Symbols): mittig +111 (Soll +114), 2. Symbol −82/+111 (Soll −82/+114), Publisher +111 links/+106 rechts (Soll +106), Excel +35 (Soll +35); Restfehler je Zeile ≤ 3 px. `r = 2,0/2,25/2,75/3,0` verschlechtern die Summe.
- **Folgerungen für den Plan:** (1) Der Test braucht ein **Fixture**: Slotbreite, Trennlinienbreite, Randabstände (aus Zeichen und aus `bg`-Margen 8), Cursorposition je Zustand — aus den Screenshots messen (PNG ohne PIL: Python-`zlib`/kleine C#-Routine oder von Hand im Bildbetrachter); (2) die drei **identischen linken Ränder −3** (mittig, Publisher, Excel) als **Untergrenze** („Rand ≤ −3“) statt Sollwert prüfen, nur die unbeschnittenen Seiten sind Sollwerte (rechts bei mittig/Publisher/Excel, beidseitig beim 2. Symbol), Toleranz ±6 px; (3) `center` als zweiter Parameterwert, Test dort nur „Mitte bleibt fest“; (4) nach dem Test das Fixture und die gefitteten Parameter (Slot, `r`) in der Notiz dokumentieren — Phase 3 kalibriert gegen Vollbild-Neuaufnahmen und die Reihe Zoom-Breite 2/6/8 (offener Punkt in STATE.md).
- Zeitbox: Gelingt kein Fixture, das alle unbeschnittenen Seiten auf ±6 px trifft, endet die Notiz mit „UNKLAR — Restfehler X px, Ursache Y“ (D-04), nicht mit weiterem Fitten.
- TDD ist hier passend: erst Fixture + roter Test, dann `ZoomEngine`-Minimalimplementierung.

### Q6 — Symbole (`01-SPIKE-icons.md`)
**Wiederverwenden (probe-verifiziert, nicht neu prüfen):** `IShellItemImageFactory.GetImage` mit `SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK` bei 256 px; HBITMAP per `GetObject`+`GetDIBits` lesen, **Straight-Alpha** → `Pbgra32` → `Freeze()`; `.lnk` ohne Pfeil-Overlay, Store-App über `shell:AppsFolder\<AUMID>`; Aufruf aus MTA-Threadpool funktioniert; Ohne `ICONONLY` kommen Thumbnails/44 px [CITED: `.planning/research/STACK.md` § 1 und `.claude/CLAUDE.md`, Probes vom 2026-10-09]. **Widerspruch zu klären:** `PITFALLS.md` Pitfall 7 verlangt STA, `STACK.md` fand MTA funktionierend → im Spike beide Threadmodelle über 20 Wiederholungen messen und die Notiz legt fest, welches Phase 2 nutzt.
**Neu zu messen (nicht geprobt):**
| Fall | Fixture (alles vom Skript erzeugt, nichts Fremdes im Repo) | Prüfen |
|------|------|--------|
| .exe nur 48 px | `Only48.exe`: winziges Konsolenprogramm mit `ApplicationIcon` aus einer vom Skript geschriebenen ICO mit nur 48×48 | Rückgabegröße (256 hochskaliert oder 48), Alpha, Unschärfe → Notiz „Quelle begrenzt Schärfe“ |
| .exe 256 px | `Only256.exe` mit 256-px-ICO (PNG-Frame) | 256×256, sauberes Alpha |
| .lnk (Datei) und Store-App | `.lnk` auf notepad.exe; Startmenü-/AppsFolder-Verknüpfung einer Store-App (AUMID via `Get-StartApps` verfügbar [VERIFIED: Abfrage, z. B. `AcrobatReader`]) | kein Pfeil, Größe 256 |
| Ordner | normaler Ordner | 256 |
| .url | `[InternetShortcut] URL=…` mit und ohne `IconFile=` | Rückgabe: Standard-Browser-Symbol? |
| PDF | vom Skript geschriebene Mini-PDF; Standardprogramm Adobe Acrobat (`.pdf=Acrobat.Document.DC` [VERIFIED: `assoc`-Abfrage]) | Symbol statt Thumbnail (`ICONONLY`) |
| Netzpfad | gemappte Laufwerke `L:` und `M:` existieren [VERIFIED: `net use`: `L: \\server-rewe\WINDVSW1`, `M: \\192.168.100.100\Firmendaten\Medien`] — **Firmenfreigaben, nur Symbol einer vorhandenen Datei abfragen, nichts auflisten/schreiben**; Zeitlimit-Fall mit einem **nicht erreichbaren** Ziel (z. B. Dokumentationsadresse `192.0.2.1`) und hartem Zeitlimit je Eintrag im Worker | Kalt-/Warmlatenz, Zeit bis Fehler, Fallback-Symbol nötig? |
- **Orakel „sauberes Alpha“ (automatisch):** Alpha-Kanal nicht überall 255; Ecken Alpha 0 (keine schwarze/weiße Box); Anteil Pixel mit Farbkanal > Alpha (= Straight-Alpha-Erkennung) vor der Umwandlung; nach `Pbgra32` über Weiß **und** Schwarz zusammengesetzt → Randsaum-Kennzahl. **Kontaktbogen** (PNG, Symbole auf hell/dunkel/Schachbrett, bei 32 und 121 px) für Stephans Blick → `spikes/results/private/` (enthält seine Symbole).
- **Messung:** Zeit je Symbol kalt (erster Aufruf nach Start) und warm, STA gegen MTA, parallel 8 Symbole.
- **Kriterium:** GO, wenn jeder Fall ein 48-…256-px-Bitmap mit gültigem Alpha liefert oder eine dokumentierte Ersatzregel hat und Netz-/Timeout-Fall den Worker nicht blockiert (Zeitlimit greift).

### Q7 — Werkzeuge (`01-SPIKE-tooling.md`)
- **CsWin32-Build:** Diese Namensliste kompiliert mit 0.3.360 auf SDK 10.0.401 ohne Fehler und Warnung [VERIFIED: Build-Lauf dieser Sitzung in einem Wegwerfprojekt `net10.0-windows` + `UseWPF` + `AllowUnsafeBlocks`, „0 Fehler, 0 Warnung(en)“, Programm lief]. `NativeMethods.json` = `{ "allowMarshaling": false, "useSafeHandles": false }`. Liste (Auszug, Grundlage für Host/Driver):
  `SetWindowPos GetWindowLong SetWindowLong HWND_BOTTOM HWND_TOPMOST HWND_NOTOPMOST GetForegroundWindow SetForegroundWindow GetWindow EnumWindows GetClassName GetWindowText IsWindowVisible IsIconic GetWindowRect GetWindowThreadProcessId DwmGetWindowAttribute DwmGetCompositionTimingInfo SetWinEventHook UnhookWinEvent SendInput GetCursorPos SetCursorPos WindowFromPoint CreatePopupMenu AppendMenu TrackPopupMenuEx DestroyMenu PostMessage ChangeDisplaySettingsEx EnumDisplaySettings LockWorkStation WTSRegisterSessionNotification WTSUnRegisterSessionNotification GetDC GetPixel ReleaseDC FindWindow FindWindowEx GetProcessTimes RegisterWindowMessage GetSystemMetrics MonitorFromWindow GetMonitorInfo AllowSetForegroundWindow SetWindowRgn CreateRectRgn GetGUIThreadInfo SHCreateItemFromParsingName IShellItemImageFactory GetDIBits GetObject BITMAP DeleteObject MA_NOACTIVATE WM_MOUSEACTIVATE WM_WINDOWPOSCHANGING EVENT_SYSTEM_FOREGROUND WINEVENT_OUTOFCONTEXT WINDOW_EX_STYLE SET_WINDOW_POS_FLAGS VIRTUAL_KEY MOUSE_EVENT_FLAGS DWMWINDOWATTRIBUTE WINDOW_LONG_PTR_INDEX KEYBD_EVENT_FLAGS TRACK_POPUP_MENU_FLAGS MENU_ITEM_FLAGS GET_WINDOW_CMD`
  Gefundene Fallen: `GetWindowLongPtr`/`SetWindowLongPtr` **existieren als Name nicht** (Warnung PInvoke001) → `GetWindowLong`/`SetWindowLong` (CsWin32 wählt die Ptr-Form auf x64 selbst); einzelne Konstanten wie `WS_EX_NOACTIVATE`, `SWP_NOACTIVATE`, `VK_LWIN`, `MOUSEEVENTF_ABSOLUTE`, `DWMWA_CLOAKED` sind **nicht** als Einzelnamen listbar (Warnung PInvoke004: „Use the name of the enum that declares this constant“) → Enum-Namen `WINDOW_EX_STYLE`, `SET_WINDOW_POS_FLAGS`, `VIRTUAL_KEY`, `MOUSE_EVENT_FLAGS`, `DWMWINDOWATTRIBUTE` listen. `INPUT`/`SendInput` für Treiber: Struktur- und Enum-Namen beim ersten Treiber-Build ergänzen (nicht getestet).
- **Testerkennung in VS Code:** `code --version` = `1.138.0`; Erweiterungen `ms-dotnettools.csdevkit` und `ms-dotnettools.csharp` installiert [VERIFIED: `code --list-extensions`]. Die Dokumentation klärt es nicht: Die VS-Code-Seite nennt xUnit/NUnit/MSTest, **kein** MTP/xUnit v3 [VERIFIED: Seite in dieser Sitzung geprüft]; die xUnit-Seite (Stand 2026-05-27) beschreibt nur Visual Studio und `dotnet test` und sagt, dass Drittläufer VSTest weiter brauchen können [CITED: xunit.net/docs/getting-started/v3/microsoft-testing-platform]; ein Gegenbericht (Juni 2026, sekundär) sagt, Dev Kit finde xUnit-v3-Tests mit MTP nicht [CITED: Suchergebnis, libhunt/dev.to-Verweis, LOW]. → **Praktischer Test am echten Projekt** (D-02): Ordner in VS Code öffnen, Testexplorer aktualisieren, prüfen, ob der Test erscheint und läuft. Das ist ein **Handschritt** (kein CLI-Weg für die Dev-Kit-Oberfläche; als Handteil-Punkt H6). Fällt es durch: Rückfall B aus `STACK.md` § 12 (`xunit.v3.mtp-off` 4.0.1 + `xunit.runner.visualstudio` 4.0.0 + `Microsoft.NET.Test.Sdk` 18.10.1, `test`-Block aus `global.json` entfernen).
- **`dotnet test`** im Wurzelordner (CLI, automatisiert) ist die verbindliche Prüfung; CLI-Ergebnis ist laut STACK in beiden Modi gleich.

## Automatisierbar oder Handteil

| Prüfung | Automatisiert (Driver/Skript) | Handteil (D-06/D-07) |
|---------|-------------------------------|----------------------|
| Klick in/neben R, Mausbewegung, Zähler | ja (100 Klicks) | 1× echte Maus gegenprüfen |
| Fokus bleibt bei Notepad | ja | — (in D-07-Gegenprobe) |
| „Ohne Aufblitzen“ | nur Z-Position beim ersten Frame | **ja**, Auge (10× Start, ggf. Handy-Zeitlupe) |
| Win+D, Ecke, Win+M, Peek, Alt+Tab | ja (SendInput + Pixel/Z-Order) | je Auslöser 1× echte Taste/echter Klick, Ergebnis vergleichen |
| Explorer-Neustart | ja | — |
| Auflösungswechsel | ja, **nach Zustimmung** | Anordnung der Desktop-Symbole danach prüfen |
| Sperren/Entsperren | Auslösen + Log; **Entsperren nur Stephan** | **ja** |
| Menü: Öffnen/Klick daneben/Escape/Pfeile/Enter | ja (synthetisch) | **ja** je Variante 1× echte Tastatur; „Programm kommt nach vorn“ sichtbar; Menüoptik |
| Renderläufe, CPU, PresentMon | ja (PresentMon nur elevated) | „fühlt sich flüssig an“ als Randnotiz |
| Zoom-Test, CsWin32-Build, `dotnet test` | ja | — |
| VS-Code-Testexplorer | nein | **ja** |
| Symbol-Kontaktbogen | Metriken ja | **ja**, Auge (sauberer Rand) |

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Per-Frame-Messung unabhängig vom Prozess | eigener ETW-Konsument | PresentMon 2.6.0 (MIT) | D-16; Layered-Fenster-Sichtbarkeit zuerst prüfen |
| Win32-Signaturen | eigene `DllImport`-Deklarationen | CsWin32 (Liste oben) | Projektentscheidung; Liste kompiliert |
| Perzentile | eigene Interpolation | einfache, dokumentierte Nearest-Rank-Funktion (10 Zeilen) in `Common/Stats.cs`, im Spike getestet | Reicht; keine Bibliothek |
| Taskleisten-Ecke/Tray-Position | geratene Koordinaten | `FindWindow("Shell_TrayWnd")` + `GetWindowRect` | Taskleistenlage nicht annehmen |
| ICO-Fixture | ICO-Editor | kleine Schreibfunktion (PNG-Frame im ICO-Container, ~30 Zeilen) im Skript | Reproduzierbar, nichts Fremdes |
| Wächter-Quelltext | Rainmeter-Code übernehmen | Verfahren nachbauen | GPL v2 passt nicht zu einem späteren öffentlichen Release |

**Key insight:** Das teure an diesen Spikes ist nicht der Prüfling, sondern ein vertrauenswürdiges Orakel; deshalb gehört der meiste Aufwand in Pixelprobe, Z-Order-Schnappschuss und deren Selbsttest.

## Common Pitfalls

### Pitfall 1: Das Orakel lügt (verdecktes Fenster gilt als sichtbar)
**What goes wrong:** `IsWindowVisible`/`PrintWindow` melden „sichtbar“, obwohl die Leiste unter dem Desktop liegt.
**How to avoid:** Pixelprobe am Bildschirm als Primärorakel plus Selbsttest mit Notepad-Abdeckung (Pattern 3).
**Warning signs:** Wächter „aus“ zeigt bei Win+D ebenfalls „sichtbar“.

### Pitfall 2: Treiber-CPU und Treiber-Eingabe verfälschen die Renderlauf-Zahlen
**How to avoid:** Zweiprozess-Aufbau, Treiber nur `SendInput` mit `Thread.Sleep`-Takt, CPU nur über `GetProcessTimes` des Host-PID; PresentMon-Lauf elevated, Host aber normal gestartet (oder alles elevated **nur** für den Renderlauf, in der Notiz vermerkt).

### Pitfall 3: Win+D/Win+M erwischen Stephans echte Arbeit
**What goes wrong:** Alle Fenster minimiert, Explorer beendet, Auflösung gewechselt, Symbole des Desktops verschoben; bis zu sechs Sitzungen laufen parallel.
**How to avoid:** `checkpoint:human-action` vor jedem Automatikblock („Arbeit speichern, Maus/Tastatur nicht anfassen, N Minuten“), globale Notabbruchtaste, Zeitüberschreitung je Fall, Wiederherstellung im `finally` (Win+Shift+M, zweites Win+D, Anzeigemodus zurück, `TaskbarSd` zurück), Automatiklauf nie während anderer GSD-Sitzungen mit Fensterzugriff.

### Pitfall 4: Synthetische Eingabe ≠ echte Eingabe
**What goes wrong:** `SendInput`-Tasten/-Klicks verhalten sich bei Vordergrundrechten, Menüs und Win-Kombinationen anders als echte (D-07).
**How to avoid:** Jeder Fall einmal von Hand; bei Abweichung gilt das Handergebnis.

### Pitfall 5: Aufblitzen wird mit „Fenster unten“ verwechselt
**What goes wrong:** `Show()` setzt das Fenster zuerst oben in die Z-Reihenfolge, dann erst nach unten.
**How to avoid:** Ebene und Stile in `OnSourceInitialized` vor dem ersten gezeichneten Inhalt; Inhalt anfangs vollständig transparent (Alpha 0 ist unsichtbar, auch an falscher Z-Position); Z-Position beim ersten Inhalts-Frame loggen. [ASSUMED: Ablauf trägt; genau das misst Q1]

### Pitfall 6: `ProcessRenderMode` zu spät gesetzt
**How to avoid:** vor dem ersten Window; wirksamen Modus ausgeben, weil „preference“ [CITED: Learn].

### Pitfall 7: Delegat des WinEvent-Hooks wird eingesammelt
**How to avoid:** statisches Feld/GCHandle, Hook im UI-Thread mit Nachrichtenschleife [CITED: Learn SetWinEventHook].

### Pitfall 8: CsWin32-Namen
Siehe Q7: `GetWindowLong`/`SetWindowLong`, Enum-Namen statt Einzelkonstanten, mehrdeutige Namen qualifizieren, `EmitCompilerGeneratedFiles` nie ins Projekt (aus `STACK.md`).

### Pitfall 9: `dotnet new sln` erzeugt `.slnx`
**What goes wrong:** SDK 10.0.401 legt per Voreinstellung `MehrBar.slnx` an [VERIFIED: Lauf dieser Sitzung, Ausgabe `T.slnx`; `--format <sln|slnx>` vorhanden]. Ob C# Dev Kit `.slnx` sauber lädt, ist ungeprüft [ASSUMED].
**How to avoid:** Bewusst wählen und in der Tooling-Notiz festhalten; bei Dev-Kit-Problemen `--format sln`.

### Pitfall 10: Zentrale Paketverwaltung greift auf `spikes/` durch
Siehe Struktur: Spike-csproj ohne `Version`, Pin im Wurzelfile. `global.json` mit MTP-Runner wirkt auch auf `dotnet test` in `spikes/` (dort keine Tests — kein Problem).

### Pitfall 11: Frame-Schwelle auf Kadenz statt Kosten
Siehe Q4: Bei 60 Hz reißt Jitter allein ein „p95 ≤ 16,7 ms“ auf Kadenz; Kosten und Ausfälle getrennt auswerten.

### Pitfall 12: Zoom-Fixture ist unterbestimmt
Siehe Q5; linker Rand −3 ist eine Grenze, kein Messwert.

## Code Examples

Nur Skizzen; alle Konstanten aus CsWin32, jede Zeile ist im Spike zu verifizieren.

### Eingabe: Win+D und absolute Mausbewegung (Driver)
```csharp
// Source: Win32 SendInput; INPUT/KEYBDINPUT/MOUSEINPUT-Namen beim ersten Driver-Build in NativeMethods.txt ergänzen.
// Win+D: LWIN down, D down, D up, LWIN up (je ~20 ms Abstand), danach 3 s Orakel-Abtastung.
// Maus: MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, dx/dy = pixel * 65535 / (screen-1)
```

### Orakel: Pixel am Bildschirm
```csharp
var dc = PInvoke.GetDC(HWND.Null);                     // Bildschirm-DC
uint c = PInvoke.GetPixel(dc, x, y);                   // COLORREF 0x00BBGGRR; Sentinel #FF00FF -> 0x00FF00FF
PInvoke.ReleaseDC(HWND.Null, dc);
```

### Stats (Nearest-Rank)
```csharp
static double Percentile(double[] sorted, double p) => sorted[(int)Math.Ceiling(p / 100.0 * sorted.Length) - 1];
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Desktop-Host = `WorkerW` | ab Windows 11 24H2 `Progman` als Symbol-Host | 24H2 (Build 26100); 26200 = 25H2 | Host zur Laufzeit über `SHELLDLL_DefView`-Besitzer finden [CITED: Rainmeter System.cpp] |
| `dotnet new sln` → `.sln` | → `.slnx` | SDK 10 | Dev-Kit-Verträglichkeit prüfen (Pitfall 9) |
| xUnit v2 + VSTest | xUnit v3 + MTP (`global.json`-Runner ab SDK 10) | xUnit 4.0 | Dev-Kit-Testexplorer offen (Q7) |
| `DwmGetCompositionTimingInfo(hwnd)` | `hwnd` muss `NULL` sein | Windows 8.1 | nur globale Zähler [CITED: Learn] |
| PresentMon 1.x CSV-Spalten | 2.x (Umbenennungen möglich) | 2.0+ | Kopfzeile zur Laufzeit lesen |

**Deprecated/outdated:** `GetWindowLongPtr` als CsWin32-Name; Rainmeter-/Qt-Rezepte mit `WorkerW`-Annahme (PITFALLS Pitfall 1).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `GetPixel` auf dem Bildschirm-DC liefert das DWM-zusammengesetzte Bild; Selbsttest mit Notepad-Abdeckung bestätigt | Pattern 3 | Orakel unbrauchbar → Alternative: Desktop Duplication/Graphics.Capture |
| A2 | `WindowFromPoint` verhält sich bei Alpha-0-Pixeln eines Layered-Fensters unbelegt | Q1 | Nur Zusatzdatenpunkt, kein Kriterium |
| A3 | PresentMon sieht das transparente WPF-Fenster möglicherweise nicht | Q4 | Dann nur Eigenmessung + DWM-Zähler; Notiz „nicht anwendbar“ |
| A4 | PresentMon-2.6-CSV-Spaltennamen entsprechen der README (`MsBetweenPresents` …) | Q4 | Auswertung liest Kopfzeile, geringes Risiko |
| A5 | `SetForegroundWindow` auf die NOACTIVATE-Leiste hebt sie in der Z-Reihenfolge nach vorn | Q3 | Hilfsfenster-Variante M-D unnötig oder nötiger |
| A6 | Ablauf `OnSourceInitialized` + transparenter Inhalt vermeidet Aufblitzen | Pitfall 5/Q1 | Alternative: `SWP_SHOWWINDOW` mit `HWND_BOTTOM` als erster Show-Schritt |
| A7 | Zentrale Paketverwaltung verlangt `Version`-lose PackageReferences auch unter `spikes/` | Struktur | Build-Fehler NU1008 am Anfang, leicht behebbar |
| A8 | C# Dev Kit lädt `.slnx` und findet xUnit-v3-MTP-Tests | Q7/Pitfall 9 | Rückfall B (VSTest) bzw. `--format sln` |
| A9 | Die Option „Ecke Desktop anzeigen“ ist ohne Wert `TaskbarSd` aus (Win11-Voreinstellung) | Q2 | Test setzt den Wert ohnehin explizit und stellt zurück |
| A10 | Rastermaß der Original-Leiste: Slot ≈ 34–38 px, Trennlinie klein; aus Augenmaß und Fit, nicht gemessen | Q5 | Fixture falsch → Test misst nichts; deshalb Fixture-Messung als erster Schritt |
| A11 | Kein Wächter nötig, wenn S0 (nur `HWND_BOTTOM`) Win+D auf 26200 besteht | Q2 | Spike entscheidet; nicht vorab annehmen |

## Open Questions

1. **Besteht `HWND_BOTTOM` auf Build 26200 überhaupt den Basisfall (Leiste über dem Desktop sichtbar)?**
   - Known: Recherche rechnet mit Verschwinden bei Win+D, nicht mit Unsichtbarkeit im Ruhezustand.
   - Unclear: nirgends dokumentiert.
   - Recommendation: Basisfall als allererste Messung in Q1; schlägt er fehl → D-12 (zweiter Spike), kein Ausweichen auf eine andere Ebene.
2. **Sieht PresentMon das Layered-Fenster?** — Vorversuch in Wave 0 entscheidet über die Messquellen (siehe A3).
3. **Welche Elevation fährt der Renderlauf?** — Empfehlung: ein erhöhtes Terminal für den kompletten Renderlauf; Stephan bestätigt (UAC). Alternativ Benutzer in „Leistungsprotokollbenutzer“ aufnehmen (Admin einmalig, Neuanmeldung) [ASSUMED].
4. **Auflösungswechsel auf dem echten Desktop?** — Risiko Symbolanordnung; Stephan zustimmen lassen oder zuerst Bildwiederholraten-Wechsel (weniger invasiv).
5. **Grenzfall-Auslegung D-18/D-19** (Kadenz-Jitter, 15 % = 3 Kerne) — mit Stephan abstimmen, falls Messwerte nahe der Schwelle liegen.
6. **Zoom-Fixture:** Reicht `rocketdock-zoom-1.png` für Rastermaße und Cursorpositionen? Falls nicht, braucht es Stephans Vollbild-Neuaufnahme (offener Punkt STATE.md, „vor Phase 3“); der Phase-1-Test darf mit dem Bestand laufen, solange die beschnittenen Ränder als Untergrenze behandelt werden.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | alle | ✓ | 10.0.401 (plus 9.0.318), Runtime 10.0.12 [VERIFIED: `dotnet --list-sdks/--list-runtimes`] | — |
| Windows / Build | alle | ✓ | 10.0.26200 [VERIFIED: CIM] | — |
| Grafik / Anzeige | Q4 | ✓ | Intel UHD Graphics 770, Treiber 32.0.101.7088, 1920×1080 bei 60 Hz, ein Monitor [VERIFIED: CIM] | — |
| CPU | Q4 (D-19) | ✓ | i7-12700, 12 Kerne / 20 logische [VERIFIED: CIM] | Kernäquivalente mitberichten |
| VS Code + C# Dev Kit | Q7 | ✓ | Code 1.138.0; `ms-dotnettools.csdevkit`, `ms-dotnettools.csharp` [VERIFIED: `code --list-extensions`] | VSTest-Rückfall |
| PresentMon | Q4 | ✗ (nicht installiert; `where` leer) | Download 2.6.0 möglich (GitHub, MIT; winget: `Intel.PresentMon.Console` 2.5.1/2.6.0 [VERIFIED: `winget search`]) | Eigenmessung + DWM-Zähler |
| Admin-Rechte (für PresentMon/ETW) | Q4 | ✗ (aktuelle Shell nicht erhöht [VERIFIED: IsInRole = False]) | UAC-Bestätigung durch Stephan | PresentMon entfällt |
| Notepad | Q1–Q3 | ✓ | `C:\WINDOWS\system32\notepad.exe` [VERIFIED: Get-Command] | eigener „Zeuge“-Prozess |
| Netzlaufwerke | Q6 | ✓ | `L:`, `M:` verbunden (Firmenfreigaben, nur lesen) [VERIFIED: `net use`] | Zeitlimit-Fall mit nicht erreichbarer Adresse |
| Adobe Acrobat (PDF-Standardprogramm) | Q6 | ✓ | `.pdf=Acrobat.Document.DC` [VERIFIED: `assoc`] | — |
| Windows Performance Toolkit (`wpr`) | Q4 (nachrangig) | ✓ | 10.0.26100 [VERIFIED] | — |
| Python 3.13 | Fixture-Messung/Wegwerf-Rechnung | ✓ | 3.13.14, **ohne** Pillow [VERIFIED] | PNG per `zlib` selbst lesen oder C# `System.Drawing`/WPF `BitmapDecoder` |
| Referenzbilder | Q5 | ✓ (lokal, gitignored) | `samples/reference/rocketdock-zoom-1..5.png` [VERIFIED: Verzeichnis] | — |
| Skin CrystalXP.net | (Phase 4; hier nur Bezug) | ✓ | `bg.png` 107×104, `sep.png` 1×59 [VERIFIED: PNG-Kopf gelesen] | — |

**Missing dependencies with no fallback:** keine.
**Missing dependencies with fallback:** PresentMon (Download, Admin nötig → sonst Eigenmessung).

## Validation Architecture

> `workflow.nyquist_validation` ist `true` [VERIFIED: `.planning/config.json`]. Die Phase liefert fast nur Messnotizen; „Validierung“ heißt: jede Notiz existiert, ist vollständig, trägt genau eine Entscheidung, und die Messläufe sind wiederholbar. Der einzige echte Unit-Test ist der Zoom-Test.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 `xunit.v3.mtp-v2` 4.0.1 auf `net10.0` (Core-Tests); Spikes: Driver-Läufe + Prüfskript |
| Config file | `global.json` (MTP-Runner), `Directory.Packages.props`; keine weitere — siehe Wave 0 |
| Quick run command | `dotnet test` (Wurzelordner; STACK-Probe: 3/3 in ~1 s) |
| Full suite command | `dotnet test` **plus** `powershell -File spikes/tools/check-notes.ps1` (Notizen-Vollständigkeit) |

### Phase Requirements → Test Map
| Kriterium | Verhalten | Test Type | Automated Command | File Exists? |
|-----------|-----------|-----------|-------------------|-------------|
| SC5 (Zoom) | Cursor-Verankerung trifft die unbeschnittenen Seiten der vier Reihen ±6 px; `center` hält die Mitte | unit | `dotnet test --filter-method "*ZoomAnchor*"` (Syntax der MTP-v2-Filter beim ersten Lauf prüfen; sonst `dotnet test`) | ❌ Wave 0 |
| SC5 (Werkzeug) | `dotnet build` Host mit CsWin32 fehlerfrei; `dotnet test` grün | smoke | `dotnet build spikes/WindowHost -c Release && dotnet test` | ❌ Wave 0 |
| SC5 (VS Code) | Testexplorer zeigt den Test | manual-only (Dev-Kit-Oberfläche, kein CLI) | — Handteil H6 | n/a |
| SC1 | Klick-Fang/-Durchlass, Fokus, Basis-Sichtbarkeit, erster Frame unten | integration (Driver) | `dotnet run --project spikes/Driver -- case hit --variant H1 --clicks 100 --out spikes/results/q1.csv` | ❌ Wave 0 |
| SC1 | „Ohne Aufblitzen“ | manual-only (Auge) | — Handteil H1 | n/a |
| SC2 | Matrix Auslöser × Wächter, Lücke/Deckung | integration (Driver, nach Haltepunkt) | `dotnet run --project spikes/Driver -- case zorder --trigger win-d --guard on --repeat 5` | ❌ Wave 0 |
| SC2 | Sperren/Entsperren, Echt-Taste je Auslöser | manual-only | — Handteil H2/H3 | n/a |
| SC3 | Menüpunkte je Variante (synthetisch) | integration (Driver) | `dotnet run --project spikes/Driver -- case menu --variant M-C` | ❌ Wave 0 |
| SC3 | Echttastatur, Vordergrund sichtbar, Optik | manual-only | — Handteil H4 | n/a |
| SC4 | 8–10 Renderläufe: p95-Kosten, Ausfälle, CPU%, Kernäquivalente | integration (Driver + Host, ggf. PresentMon elevated) | `powershell -File spikes/tools/run-render-matrix.ps1` | ❌ Wave 0 |
| SC5 (Symbole) | Fälle × Größe/Alpha/Latenz, Kontaktbogen | integration | `dotnet run --project spikes/Icons -- --all --out spikes/results` | ❌ Wave 0 |
| SC5 (Symbole) | Augenprüfung Kontaktbogen | manual-only | — Handteil H7 | n/a |
| alle | je Spike eine Notiz mit genau einer Entscheidungszeile | smoke | `powershell -File spikes/tools/check-notes.ps1` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test` (Core) bzw. der betroffene Spike-Lauf in kurzer Form (`--repeat 1`).
- **Per wave merge:** `dotnet test` + `check-notes.ps1` + `dotnet build` aller Spike-Projekte.
- **Phase gate:** alle sieben Notizen vorhanden und vollständig, Sammelnotiz, Handteil (H1–H7) abgehakt, Core-Test grün, bevor `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `global.json`, `Directory.Packages.props`, `MehrBar.sln`(x), `src/MehrBar.Core`, `src/MehrBar.Tests` — Gerüst nach D-02 (+ ein erster trivialer grüner Test, damit Q7 an einem echten Projekt klärbar ist)
- [ ] `src/MehrBar.Tests/ZoomAnchorTests.cs` — rote Tests aus dem Fixture (Q5)
- [ ] `spikes/Common/Oracles.cs` + **Orakel-Selbsttest** (Pixelprobe sichtbar/verdeckt) — ohne ihn sind Q1–Q3 wertlos
- [ ] `spikes/WindowHost`, `spikes/Driver`, `spikes/Icons`-Projektgerüste mit der CsWin32-Liste; `spikes/tools/check-notes.ps1`; PresentMon-Download (Hash prüfen) und Layered-Sichtbarkeits-Vorversuch
- [ ] `.gitignore`-Ergänzungen: `spikes/tools/PresentMon*.exe`, `spikes/results/private/`, `spikes/fixtures/*.exe`
- [ ] Framework-Installation: vorhanden (SDK); Pakete per `dotnet restore`

## Security Domain

> `security_enforcement` ist `true`, ASVS-Stufe 1 [VERIFIED: `.planning/config.json`]. Die Phase liefert Wegwerf-Messcode ohne Netz- oder Benutzerdateneingabe; relevant sind die Werkzeuge selbst.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | nein | — |
| V3 Session Management | nein (Sperren/Entsperren nur ausgelöst, Anmeldung durch Stephan) | — |
| V4 Access Control | ja (gering) | Spike läuft auf Benutzerintegrität (`asInvoker`); erhöht nur PresentMon, mit UAC-Bestätigung durch Stephan |
| V5 Input Validation | ja (gering) | Spikes parsen `.url`/`.lnk`/ICO nicht selbst, sondern über die Shell; Fixtures nur selbst erzeugt |
| V6 Cryptography | nein (SHA-256-Vergleich der PresentMon-Datei, `Get-FileHash`) | — |
| V12 Files / V14 Config | ja | `private/`-Ordner gitignored; Firmenfreigaben nur lesend; keine Zugangsdaten in Logs |

### Known Threat Patterns for diese Spikes
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Heruntergeladene EXE (PresentMon) ausgetauscht | Tampering | SHA-256 gegen den GitHub-Digest (`b2a706bc…f1af`) prüfen, Quelle nur `github.com/GameTechDev/PresentMon/releases`, nicht committen |
| Automatisches `SendInput`/`taskkill`/Auflösungswechsel außer Kontrolle | Denial of Service (eigener Desktop) | Haltepunkt, Notabbruchtaste, `finally`-Wiederherstellung, Zeitüberschreitung je Fall |
| Firmenfreigaben (`L:`/`M:`) werden gelesen oder verändert | Information Disclosure / Tampering | nur Symbol einer vorhandenen Datei abfragen, keine Auflistung/Schreiben; Pfade nur in `private/`-Logs |
| Dritt-Symbole/Screenshots im Repo | (Rechte, Q2/Konzept) | Kontaktbögen und Screenshots in `spikes/results/private/` |
| Erhöhter Spike-Prozess empfängt Drag & Drop/Eingabe nicht | — | nur der PresentMon-Lauf erhöht; Fenster-Tests normal |

## Sources

### Primary (HIGH confidence)
- Lokale Läufe dieser Sitzung: `dotnet --list-sdks`, CIM-Abfragen (GPU/CPU/OS), `dotnet new sln` (→ `.slnx`), CsWin32-0.3.360-Build mit der Namensliste (0 Fehler/0 Warnungen, Programm lief), Python-Nachbau der Zoom-Reihen, Registry-Abfragen, `code --list-extensions`, `net use`, `assoc`
- NuGet flat-container API (CsWin32 0.3.360, xunit.v3.mtp-v2 4.0.1) und GitHub-API (PresentMon v2.6.0, Asset-Größe, MIT, SHA-256-Digest)
- `.planning/phases/01-risiko-spikes/01-CONTEXT.md`, `ROADMAP.md`, `STATE.md`, `config.json`, `docs/PROJEKT_START.md` (§ 6, Q10, Q12 samt Nachtrag), `.planning/research/{SUMMARY,PITFALLS,STACK,FEATURES}.md`, `.claude/CLAUDE.md`
- Microsoft Learn: Extended Window Styles (WS_EX_NOACTIVATE/TOOLWINDOW), TrackPopupMenu (Remarks), SetWinEventHook (Remarks), DwmGetCompositionTimingInfo (HWND = NULL ab 8.1), RenderOptions.ProcessRenderMode
- Rainmeter `Library/System.cpp` (Wächter-Verfahren; GPL v2)

### Secondary (MEDIUM confidence)
- PresentMon README-ConsoleApplication (Argumente `--process_name`, `--output_file`, `--timed`, `--terminate_after_timed`; Spaltennamen), GitHub-Releaseseite
- xunit.net „Microsoft Testing Platform support in xUnit.net v3“ (Stand 2026-05-27), VS-Code-Testseite (kein MTP erwähnt)

### Tertiary (LOW confidence)
- Sekundärbericht (Juni 2026), C# Dev Kit finde xUnit-v3-MTP-Tests nicht — nur Hinweis, Q7 klärt es am Rechner
- Fehlende Belege: PresentMon-Sicht auf Layered-Fenster, `WindowFromPoint` bei Alpha 0 (siehe Assumptions)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Projektrecherche probe-verifiziert, CsWin32-Liste und Versionen in dieser Sitzung geprüft
- Architecture (Host/Driver/Orakel): MEDIUM-HIGH — Aufbau ist Standard, die Orakel-Annahmen (A1) werden im Selbsttest geprüft
- Spike-Methoden: MEDIUM — Verfahren belegt (Rainmeter, Learn), konkrete Win-11-26200-Ergebnisse offen (das ist der Zweck)
- Zoom-Fixture: MEDIUM — Fit am Rechner reproduziert die Reihen, Rastermaße aber noch nicht aus dem Bild gemessen
- Pitfalls: MEDIUM-HIGH

**Research date:** 2026-10-09
**Valid until:** 2026-11-08 (CsWin32 0.3.x erscheint nahezu täglich — Pin gilt trotzdem; PresentMon-Version beim Download neu prüfen)
