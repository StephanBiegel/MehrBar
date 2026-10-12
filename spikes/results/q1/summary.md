# Q1 Gesamtauswertung – Messung

Seed 4711, Varianten H1,H2,H3, erzeugt von spikes/tools/run-q1.ps1 am 2026-10-12 14:08. Einzelwerte je Schritt: die CSV-Dateien daneben.

## 1. Orakel-Selbsttest und Basislinie (selftest)

| Messung | Wert |
|---|---|
| Leiste unverdeckt, Sentinels sichtbar (D-12-Basislinie, a) | 5/5 |
| Prüffenster darüber, Deckfarbe gelesen (Orakel A1, b) | 5/5 |

## 2. Erstes Bild ganz unten (firstframe)

| Variante | Start unten (Host) | gemessen | von außen unten bei READY |
|---|---|---|---|
| H1 | 10/10 | 10 | 10/10 |
| H2 | 10/10 | 10 | 10/10 |
| H3 | 10/10 | 10 | 10/10 |

## 3. Trefferfläche und Durchlass (hit)

### Q1 Trefferfläche – Messung

Klicks je Gruppe: 100 (innen) und 100 (außen), Seed 4711.

| Variante | innen geklickt | gefangen | außen geklickt | durchgelassen | Bewegung erkannt | Fokus gehalten | Bemerkung |
|---|---|---|---|---|---|---|---|
| H1 | 100 | 100/100 | 100 | 100/100 | innen 24, außen 0 | 100/100 | Durchlass durch Vordergrundwechsel bestätigt 100/100 |
| H2 | 100 | 100/100 | 100 | 100/100 | innen 24, außen 0 | 100/100 | Fensterregion SIMPLEREGION 650,8-1250,64 = Platte; Durchlass durch Vordergrundwechsel bestätigt 100/100; Region-Churn 5 s: 158 Aufrufe, Median 706 µs, p95 2100 µs, max 2946 µs |
| H3 | 100 | 95/100 | 100 | 100/100 | innen 22, außen 0 | 95/100 | Durchlass durch Vordergrundwechsel bestätigt 100/100; Eintrittslatenz 20/20, Median 17.1 ms, p95 33.9 ms, max 34.3 ms, verpasst 0 |

### Außen je Kategorie (durchgelassen / geklickt)

| Variante | links | rechts | oben | unten | Überhang |
|---|---|---|---|---|---|
| H1 | 20/20 | 20/20 | 20/20 | 20/20 | 20/20 |
| H2 | 20/20 | 20/20 | 20/20 | 20/20 | 20/20 |
| H3 | 20/20 | 20/20 | 20/20 | 20/20 | 20/20 |

## 4. Fokus, aktives Prüffenster (focus-witness)

### Q1 Fokus – Messung

| Variante | Ziel | Fokus gehalten | Bemerkung |
|---|---|---|---|
| H1 | witness | 300/300 | – |
| H2 | witness | 300/300 | – |
| H3 | witness | 300/300 | – |

## 5. Fokus, Notepad (focus-notepad)

### Q1 Fokus – Messung

| Variante | Ziel | Fokus gehalten | Bemerkung |
|---|---|---|---|
| H1 | notepad | 60/60 | – |
| H2 | notepad | 60/60 | – |
| H3 | notepad | 60/60 | – |

