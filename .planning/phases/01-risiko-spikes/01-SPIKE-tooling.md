# Spike Q7 — Werkzeuge (CsWin32, xUnit v3 mit MTP, Testfilter)

**Stand:** 2026-10-10 · Plan 01-08 · Rohdaten: `spikes/results/q7-tooling.txt` · Gerüst aus Plan 01-01, Listen-Regel aus Plan 01-02

## Frage

Q7: Baut CsWin32 0.3.360 auf SDK 10.0.401 die Spike-Projekte ohne Warnung, wenn die Namenslisten **nach Thema auf mehrere `NativeMethods.txt`-Dateien** verteilt sind? Läuft `dotnet test` mit xUnit v3 über die Microsoft Testing Platform (MTP, Runner in `global.json`), und welche Syntax wählt einen einzelnen Test bzw. listet die Tests? Und findet der VS-Code-Testexplorer diese Tests?

## Aufbau

- **Versionen:** SDK 10.0.401, gepinnt in `global.json` (`rollForward: latestFeature`) mit `test.runner = Microsoft.Testing.Platform`; `xunit.v3.mtp-v2` 4.0.1 im Produkt-`Directory.Packages.props`; CsWin32 0.3.360 exakt gepinnt im **eigenen** `spikes/Directory.Packages.props` (D-01: MSBuild nimmt die nächstgelegene Datei, die Spikes hängen nicht an der Wurzel-Datei).
- **Projektmappe:** klassische `MehrBar.sln` statt des SDK-10-Standards `.slnx` (Plan 01-01). Gründe: das Konzept nennt `MehrBar.sln`, und die `.slnx`-Unterstützung des C# Dev Kit war nicht belegt (Research Pitfall 9). Umkehrbar.
- **CsWin32-Listen:** je Thema eine Datei unter `spikes/Common/native/` (`base`, `host`, `input`, `oracles`); `NativeMethods.json` (`allowMarshaling: false`, `useSafeHandles: false`) liegt einmal in `spikes/Common/`. `WindowHost` und `Driver` binden beides mit je einem Platzhalter-Eintrag ein (`AdditionalFiles Include="..\Common\native\**\NativeMethods.txt"`). So können Plan 01-03 (Host), 01-04 (Eingabe) und 01-05 (Orakel) eigene Listen ergänzen, ohne dieselbe Datei zu ändern.
- **Befehle** (Wurzelordner, Ausgabe deutsch): `dotnet --version`, `dotnet test`, `dotnet test --filter-method "*Core_assembly*"` und die Gegenproben, `dotnet test --list-tests`, `dotnet build spikes/WindowHost -c Release -warnaserror`, `dotnet build spikes/Driver -c Release -warnaserror` jeweils auch mit `--no-incremental`, damit der Quellgenerator sicher läuft. Zusätzlich ein Lauf mit `EmitCompilerGeneratedFiles` **nach `obj/`** (nie in den Projektordner), um zu sehen, welche Namen wirklich erzeugt wurden; der Ordner wurde danach gelöscht.

## Messwerte

Alle Zeilen stammen aus `spikes/results/q7-tooling.txt` (Blocknummern in Klammern).

| Größe | Ergebnis | Block |
|---|---|---|
| SDK | `10.0.401` (aus `global.json`; 9.0.318 ist zusätzlich installiert, wird nicht gewählt) | 1 |
| `dotnet test` (MTP) | 19 Tests, 19 bestanden (`ScaffoldTests` 1, `ZoomAnchorTests` 18), Exitcode 0 | 2 |
| Einzeltest-Filter | `dotnet test --filter-method "*Core_assembly*"` → genau **1** Test; dieselbe Zahl mit `dotnet test -- --filter-method "*Core_assembly*"` | 3a, 3b |
| Klassen-Filter | `dotnet test --filter-class "*ZoomAnchorTests"` → 18 Tests | 3d |
| Filter ohne Treffer | Exitcode **8** („keine Tests ausgeführt“), ein Tippfehler besteht also nie still | 3e |
| VSTest-Form | `--filter "FullyQualifiedName~Core_assembly"` wurde ebenfalls angenommen (1 Test); nicht darauf verlassen, die MTP-Formen sind die dokumentierten | 3c |
| Tests listen | `dotnet test --list-tests` → 19 vollqualifizierte Namen; `dotnet test -- --list-tests` listet **nichts** (Exitcode 0, „keine Tests ausgeführt“) | 4a, 4b |
| `NativeMethods.txt`-Listen | **4** (`base` 69, `host` 10, `input` 23, `oracles` 5 Zeilen) | 5a |
| CsWin32-Version | `0.3.360` laut `project.assets.json` beider Projekte | 5f |
| Build `WindowHost` | `-c Release -warnaserror`, auch `--no-incremental`: **0 Warnungen, 0 Fehler** | 5b, 5d |
| Build `Driver` | dasselbe: **0 Warnungen, 0 Fehler** | 5c, 5e |
| Erzeugte Dateien (`Driver`) | 113 `*.g.cs`; Namen aus `base` (`SetWindowPos`, `GetPixel`), `input` (`SendInput`/`INPUT`, `SetWindowsHookEx`) und `oracles` (`GetAncestor`) sind darunter | 5f |

Grenze der Messung: Die erzeugten Dateien zeigen, dass Namen aus mehreren Listen ankommen. Ob ein Name aus **jeder** der vier Listen ankam, ist nur für `base`, `input` und `oracles` einzeln belegt; für `host` (Nachrichten-Konstanten) fiel der Stichprobenname `WINDOWPOS` in eine gemeinsame Datei und sagt nichts über die Liste. Die beiden Builds mit `-warnaserror` hätten bei einem nicht aufgelösten Namen einen Fehler (PInvoke001) gemeldet; das ist der eigentliche Beleg.

## Entscheidung

**Entscheidung:** GO — CsWin32 0.3.360 und xunit.v3.mtp-v2 (MTP) über die CLI belegt; VS-Code-Testexplorer folgt im Handteil H6

Begründung: Alle CLI-messbaren Teile sind grün (Build ohne Warnung mit mehreren Listen, 19 von 19 Tests, Filter und Auflistung mit funktionierender Syntax). Der Testexplorer des C# Dev Kit hat keinen CLI-Weg und wird deshalb nicht hier entschieden. Fällt H6 durch, gilt der vorbereitete Rückfall B aus `STACK.md` § 12 (`xunit.v3.mtp-off` 4.0.1 + `xunit.runner.visualstudio` 4.0.0 + `Microsoft.NET.Test.Sdk` 18.10.1, `test`-Block aus `global.json` entfernen); die CLI-Ergebnisse sind laut `STACK.md` in beiden Modi gleich. Die Umstellung gehört dann in den Abschlussplan 01-18.

## Folgen für Phase 2/3

- **Filter-Syntax für alle späteren Prüfbefehle:** `dotnet test --filter-method "*Name*"` bzw. `--filter-class "*Klasse*"`; Auflisten mit `dotnet test --list-tests` (**nicht** `-- --list-tests`). Ein Lauf mit Exitcode 8 bedeutet „Filter hat nichts getroffen“ und ist in Verifikationsbefehlen als Fehler zu behandeln.
- **`MehrBar.sln` bleibt** (klassisches Format). Ein Wechsel auf `.slnx` wäre erst nach einem belegten Dev-Kit-Test sinnvoll; er steht nicht an.
- **CsWin32 im Produkt:** Der Pin 0.3.360 wandert zusammen mit `MehrBar.App` in Phase 2 in das Wurzel-`Directory.Packages.props` (heute dort nur `xunit.v3.mtp-v2`); `PrivateAssets=all`, `AllowUnsafeBlocks`, `NativeMethods.json` mit `allowMarshaling: false`. Die Spike-Pins bleiben unabhängig und können mit `spikes/` eingefroren oder gelöscht werden.
- **Listen nach Thema funktionieren:** `App/Interop` darf wie die Spikes mehrere `NativeMethods.txt` nach Thema führen (Fenster, Eingabe, Symbole, Shell), per Platzhalter-Eintrag eingebunden. Mehrdeutige Namen weiter qualifizieren und `EmitCompilerGeneratedFiles` nie in den Projektordner legen.
- **Lokalisierte Ausgabe:** Die CLI meldet auf diesem Rechner deutsch („erfolgreich“, „Fehler“). Skripte, die Ausgaben auswerten, nehmen den **Exitcode** (0 / 8), nicht den Text.

## Grenzfälle für Stephan

- **H6 — VS-Code-Testexplorer:** Ordner `C:\Development\MehrBar` mit `code .` öffnen. Erwartet: (1) das C# Dev Kit lädt `MehrBar.sln` ohne Fehlermeldung; (2) der Testexplorer zeigt `ScaffoldTests` und `ZoomAnchorTests` (19 Tests); (3) „Alle Tests ausführen“ endet grün. Zeigt er nichts oder bleibt er hängen, ist das der Auslöser für Rückfall B (siehe Entscheidung), kein Grund, Phase 2 anzuhalten. Der Eintrag wandert in `01-HANDTEIL.md` (Plan 01-17).
