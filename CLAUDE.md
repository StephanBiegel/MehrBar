# CLAUDE.md — MehrBar

Schlanker Programmstarter für Windows im Stil von RocketDock: mehrere Leisten, Zoom-Effekt,
Gruppen, Drag & Drop, RocketDock-Skins. Das vollständige Konzept steht in
[`docs/PROJEKT_START.md`](docs/PROJEKT_START.md).

## Name

**MehrBar** ist Projekt- und Produktname und gilt überall: Solution, Projekte
(`MehrBar.Core`, `MehrBar.App`, `MehrBar.Tests`), Namespaces, `%APPDATA%\MehrBar\`.
Der Arbeitstitel „LaunchDock“ im Konzept ist überholt (Festlegung Stephan, 2026-10-09).

## GSD

**GSD: nötig.** Sieben Meilensteine mit messbaren Abnahmekriterien und echtem technischem
Risiko (Zoom-Stabilität, Klick-Durchlass, Skin-Parser) — dafür ist der Ablauf
planen → ausführen → prüfen gemacht. GSD ist global unter `~/.claude` installiert.

## Kommunikation

- Mit dem Entwickler auf **Deutsch**.
- Code, Kommentare und Commit-Nachrichten auf **Englisch** (späteres öffentliches Release);
  UI-Texte zunächst Deutsch, aber von Anfang an in Ressourcen-Dateien.

## Technik (Kurzfassung, Details im Konzept)

- C# / **.NET 10** / WPF, Windows 10/11 x64, self-contained single-file Publish.
- Bauen: `dotnet build` · Starten: `dotnet run --project src/MehrBar.App`
- Alles ohne Oberfläche Testbare (Zoom, Layout, Config, Skin-Parser) gehört in
  `MehrBar.Core` und wird mit xUnit getestet; Win32-Aufrufe nur in `App/Interop`.
