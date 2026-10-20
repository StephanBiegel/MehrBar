# Handprüfung 02-03 — Grundstrang der Leiste am Desktop

Vom Desktop aus (nicht aus Hivemo) eine PowerShell im Ordner `C:\Development\MehrBar` öffnen. RocketDock vorher beenden.

1. **H-T0 Rauchtest:** `powershell -NoProfile -ExecutionPolicy Bypass -File tools/phase2-smoke.ps1 -Runs 5` — letzte Zeile muss `SMOKE OK` sein.
2. **H-T1 Erscheinen:** fünfmal `dotnet run --project src/MehrBar.App -- --plate-outline --exit-after-ms 3000`, dabei ein Fenster (z. B. Notepad) über den oberen Bildschirmrand legen. Erwartet: Die Leiste erscheint nie vor dem Fenster. Danach das Fenster wegschieben: leerer Kasten oben mittig, etwa drei Symbole breit, ohne Text, leicht über den Rand geschoben.
3. **H-T2 Klick-Durchlass:** `dotnet run --project src/MehrBar.App` (läuft, bis du es beendest). Rechtsklicks 1 px links, rechts und unterhalb des Kastens und etwa 50 px darunter: Es kommt das Desktop-Menü, nicht die Leiste.
4. **H-T3 Fokus:** Notepad aktivieren, einmal links auf den Kasten klicken: Notepad bleibt aktiv, Tippen landet in Notepad.
5. **H-T4 Drop:** Explorer so legen, dass der Kasten frei bleibt. Nacheinander eine `.exe`, eine Verknüpfung (`.lnk`), einen Ordner und drei Dateien auf einmal auf den Kasten ziehen, einmal mit gehaltener Umschalttaste. Erwartet: Der Mauszeiger zeigt „Verknüpfung“ oder „Kopieren“, nie „Verschieben“; je Datei ein grauer Platzhalter; die Originale bleiben im Explorer, wo sie sind.
6. **H-T5 Start nach vorn:** Notepad aktivieren, dann auf den Platzhalter der `.exe` klicken: Das Programm startet und erscheint vor Notepad.
7. **H-T6 Dateiauswahl:** Rechtsklick auf den Kasten: Die Dateiauswahl erscheint vorn; eine `.lnk` wählen — ein Platzhalter kommt dazu, die Leiste liegt danach wieder hinter den Fenstern.

Beenden: Die Leiste hat noch kein Tray-Symbol; im Terminal mit Strg+C oder im Task-Manager „MehrBar“ beenden. Die Einträge sind in diesem Stand noch nicht gespeichert.

**Rückfälle**, nur wenn ein Punkt scheitert:
- H-T4 scheitert: mit `dotnet run --project src/MehrBar.App -- --plate-alpha 6` wiederholen; klappt es dann, bitte melden („nur mit Alpha 6“).
- H-T5 oder H-T6 scheitert (Programm oder Dialog bleibt hinter Fenstern): bitte melden.

Antwort an die Sitzung: „alles ok“ oder je gescheitertem Punkt eine Zeile, z. B. „H-T4 nur mit Alpha 6“.
