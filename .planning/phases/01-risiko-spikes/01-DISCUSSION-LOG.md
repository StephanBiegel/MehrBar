# Phase 1: Risiko-Spikes - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-10-09
**Phase:** 01-risiko-spikes
**Areas discussed:** Spike-Code: wohin, wie lange · Handtests und wer prüft · Rückfallgrenzen bei No-go · Messung der Zeichenkosten

---

## Spike-Code: wohin, wie lange

| Frage | Optionen | Gewählt |
|-------|----------|---------|
| Ort des Spike-Codes | spikes/ im Repo · nur lokal · Branch spike/* | spikes/ im Repo ✓ |
| Zoom-Core-Test | gleich ins echte Core · auch wegwerfen | echtes Core ✓ |
| Zeitlimit | pro Spike · kein Limit · Gesamtbudget | pro Spike ✓ |
| Schnitt | ein Fenster-Spike, Rest getrennt · je Frage eine App · Claude entscheidet | ein Fenster-Spike ✓ |

**Notes:** Phase 1 legt dadurch das Minimalgerüst (Solution, global.json, Core, Tests) an.

---

## Handtests und wer prüft

| Frage | Optionen | Gewählt |
|-------|----------|---------|
| Ablauf der Handtests | App protokolliert, Stephan führt aus · nur Checkliste · möglichst automatisiert | möglichst automatisiert ✓ |
| Go/No-go | Claude schlägt vor, Stephan bestätigt · klare Kriterien entscheiden selbst · jede einzeln | klare Kriterien ✓ |
| Notizformat | Datei je Spike · Sammeldatei | Datei je Spike ✓ |
| Testmatrix | Roadmap + Recherche · nur Roadmap · plus Energiesparen/RDP | Roadmap + Recherche ✓ |
| Nicht automatisierbarer Rest | kurzer Handteil am Ende · Bildschirmaufnahme | kurzer Handteil ✓ |
| Automatik vs. echter Tastendruck | echter Weg gewinnt · Automatik reicht | echter Weg gewinnt ✓ |

---

## Rückfallgrenzen bei No-go

| Frage | Optionen | Gewählt |
|-------|----------|---------|
| Win+D trotz Wächter | kurz weg, von selbst zurück · durchgehend sichtbar · weg bis Fensterwechsel | kurz weg (≤ ~0,5 s) ✓ |
| „Ganz unten“ geht nicht | weiterbohren · Ebene normal · Autohide | weiterbohren ✓ |
| 64 Symbole verfehlen Ziel | flüssig bei realer Zahl · 64 hart | reale Zahl ✓ |
| Alpha-1-Fläche scheitert | Ausweichwege gleich mitmessen · nur Alpha 1 | mitmessen ✓ |
| Menüs scheitern | kurz aktivieren fürs Menü · Tastatur verzichtbar · alles Pflicht ohne Aktivierung | kurz aktivieren ✓ |

**Notes:** Claude wies darauf hin, dass ZOOM-06 (60+ Symbole) für Phase 3 bestehen bleibt; 64 Symbole werden daher zum offenen Punkt vor Phase 3, nicht zum No-go für Phase 2.

---

## Messung der Zeichenkosten

| Frage | Optionen | Gewählt |
|-------|----------|---------|
| Werkzeug | eigene Messung + PresentMon · nur eigene · WPR | eigene + PresentMon ✓ |
| Ablauf | Skript fährt Maus, feste Dauer · von Hand | Skript ✓ |
| Schwelle Frame-Zeit | P95 ≤ 16,7 ms · Mittelwert · jeder Frame | P95 ✓ |
| CPU | Task-Manager-Anteil + dwm.exe · ein Kern = 100 % | Task-Manager + dwm ✓ |

**Notes:** Rechner hat nur Intel UHD Graphics 770, keinen Akku; PresentMon nicht installiert.

---

## Claude's Discretion

- Budget je Spike im Detail, Reihenfolge, Dateinamen, Umschaltmechanik im Fenster-Spike, konkrete Zeichenvarianten, Umfang des Symbol-Spikes jenseits der Roadmap-Liste

## Deferred Ideas

Keine.
