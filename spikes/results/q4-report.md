# Q4 Zeichenkosten - Auswertung (render-report.ps1)

Eingabe: spikes/results/q4-elevated-summary.csv, spikes/results/q4-normal-summary.csv
PresentMon-Ordner: (keiner)

Schwellen: D-18 Kosten p95 <= 16,7 ms (PresentMon p95 <= 17 ms), Ausfaelle <= 1 % (nur PresentMon: Intervall >= 2 Perioden bei 60 Hz), D-19 CPU <= 15 % gesamt. Grenzfall = Quellen uneins oder Wert innerhalb 10 % der Schwelle.

## Urteile je Zelle (D-18, D-19)

| Zelle | D-18 p95 | D-18 Ausfaelle | Ersatz, nicht D-18 | D-19 CPU | Grenzfall |
|---|---|---|---|---|---|
| elevated R1/20/hw/1900x250 | OK (Kosten p95 0.01 ms) | UNKLAR (PresentMon fehlt: PresentMon schrieb 0 Zeilen (sieht das Layered-Fenster nicht, oder Flag abgelehnt)) | Host-Kadenz > 25 ms: 5.76 % (p95 33.33 ms); DWM verworfen 0, verpasst 0 | OK (0.40 % gesamt; 0.081 Kernaequiv.; dwm.exe 0.36 %) | - |
| elevated R2/20/hw/1900x250 | OK (Kosten p95 0.03 ms) | UNKLAR (PresentMon fehlt: PresentMon schrieb 0 Zeilen (sieht das Layered-Fenster nicht, oder Flag abgelehnt)) | Host-Kadenz > 25 ms: 14.22 % (p95 33.33 ms); DWM verworfen 0, verpasst 0 | OK (0.37 % gesamt; 0.073 Kernaequiv.; dwm.exe 0.29 %) | - |
| elevated R1/20/sw/1900x250 | OK (Kosten p95 0.02 ms) | UNKLAR (PresentMon fehlt: PresentMon schrieb 0 Zeilen (sieht das Layered-Fenster nicht, oder Flag abgelehnt)) | Host-Kadenz > 25 ms: 16.27 % (p95 33.33 ms); DWM verworfen 0, verpasst 0 | OK (0.31 % gesamt; 0.061 Kernaequiv.; dwm.exe 0.25 %) | - |
| elevated R2/20/sw/1900x250 | OK (Kosten p95 0.04 ms) | UNKLAR (PresentMon fehlt: PresentMon schrieb 0 Zeilen (sieht das Layered-Fenster nicht, oder Flag abgelehnt)) | Host-Kadenz > 25 ms: 21.14 % (p95 33.33 ms); DWM verworfen 0, verpasst 0 | OK (0.29 % gesamt; 0.058 Kernaequiv.; dwm.exe 0.28 %) | - |
| elevated R1/64/hw/1900x250 | OK (Kosten p95 0.03 ms) | UNKLAR (PresentMon fehlt: PresentMon schrieb 0 Zeilen (sieht das Layered-Fenster nicht, oder Flag abgelehnt)) | Host-Kadenz > 25 ms: 3.57 % (p95 16.72 ms); DWM verworfen 0, verpasst 0 | OK (0.53 % gesamt; 0.105 Kernaequiv.; dwm.exe 0.42 %) | - |
| elevated R2/64/hw/1900x250 | OK (Kosten p95 0.10 ms) | UNKLAR (PresentMon fehlt: PresentMon schrieb 0 Zeilen (sieht das Layered-Fenster nicht, oder Flag abgelehnt)) | Host-Kadenz > 25 ms: 6.29 % (p95 33.33 ms); DWM verworfen 0, verpasst 0 | OK (0.71 % gesamt; 0.143 Kernaequiv.; dwm.exe 0.49 %) | - |
| elevated R1/64/sw/1900x250 | OK (Kosten p95 0.08 ms) | UNKLAR (PresentMon fehlt: PresentMon schrieb 0 Zeilen (sieht das Layered-Fenster nicht, oder Flag abgelehnt)) | Host-Kadenz > 25 ms: 1.52 % (p95 16.69 ms); DWM verworfen 0, verpasst 0 | OK (0.86 % gesamt; 0.173 Kernaequiv.; dwm.exe 0.51 %) | - |
| elevated R2/64/sw/1900x250 | OK (Kosten p95 0.17 ms) | UNKLAR (PresentMon fehlt: PresentMon schrieb 0 Zeilen (sieht das Layered-Fenster nicht, oder Flag abgelehnt)) | Host-Kadenz > 25 ms: 6.60 % (p95 33.33 ms); DWM verworfen 0, verpasst 0 | OK (0.61 % gesamt; 0.122 Kernaequiv.; dwm.exe 0.47 %) | - |
| normal R1/20/hw/1900x250 | OK (Kosten p95 0.02 ms) | UNKLAR (PresentMon fehlt: kein PresentMon-Lauf fuer diese Zelle) | Host-Kadenz > 25 ms: 5.15 % (p95 33.33 ms); DWM verworfen 0, verpasst 0 | OK (0.43 % gesamt; 0.085 Kernaequiv.; dwm.exe n/a %) | - |
| normal R2/20/hw/1900x250 | OK (Kosten p95 0.04 ms) | UNKLAR (PresentMon fehlt: kein PresentMon-Lauf fuer diese Zelle) | Host-Kadenz > 25 ms: 11.31 % (p95 33.33 ms); DWM verworfen 0, verpasst 0 | OK (0.38 % gesamt; 0.075 Kernaequiv.; dwm.exe n/a %) | - |
| normal R1/20/sw/1900x250 | OK (Kosten p95 0.02 ms) | UNKLAR (PresentMon fehlt: kein PresentMon-Lauf fuer diese Zelle) | Host-Kadenz > 25 ms: 10.57 % (p95 33.33 ms); DWM verworfen 0, verpasst 0 | OK (0.41 % gesamt; 0.082 Kernaequiv.; dwm.exe n/a %) | - |
| normal R2/20/sw/1900x250 | OK (Kosten p95 0.05 ms) | UNKLAR (PresentMon fehlt: kein PresentMon-Lauf fuer diese Zelle) | Host-Kadenz > 25 ms: 18.80 % (p95 33.34 ms); DWM verworfen 0, verpasst 0 | OK (0.23 % gesamt; 0.047 Kernaequiv.; dwm.exe n/a %) | - |
| normal R1/64/hw/1900x250 | OK (Kosten p95 0.04 ms) | UNKLAR (PresentMon fehlt: kein PresentMon-Lauf fuer diese Zelle) | Host-Kadenz > 25 ms: 1.64 % (p95 16.69 ms); DWM verworfen 0, verpasst 0 | OK (0.60 % gesamt; 0.121 Kernaequiv.; dwm.exe n/a %) | - |
| normal R2/64/hw/1900x250 | OK (Kosten p95 0.09 ms) | UNKLAR (PresentMon fehlt: kein PresentMon-Lauf fuer diese Zelle) | Host-Kadenz > 25 ms: 4.24 % (p95 16.95 ms); DWM verworfen 0, verpasst 0 | OK (0.70 % gesamt; 0.140 Kernaequiv.; dwm.exe n/a %) | - |
| normal R1/64/sw/1900x250 | OK (Kosten p95 0.09 ms) | UNKLAR (PresentMon fehlt: kein PresentMon-Lauf fuer diese Zelle) | Host-Kadenz > 25 ms: 0.11 % (p95 16.67 ms); DWM verworfen 0, verpasst 0 | OK (0.91 % gesamt; 0.181 Kernaequiv.; dwm.exe n/a %) | - |
| normal R2/64/sw/1900x250 | OK (Kosten p95 0.17 ms) | UNKLAR (PresentMon fehlt: kein PresentMon-Lauf fuer diese Zelle) | Host-Kadenz > 25 ms: 8.90 % (p95 33.33 ms); DWM verworfen 0, verpasst 0 | OK (0.53 % gesamt; 0.106 Kernaequiv.; dwm.exe n/a %) | - |
| normal R1/64/hw/1900x250 (K: nicht transparent) | OK (Kosten p95 0.05 ms) | UNKLAR (PresentMon fehlt: kein PresentMon-Lauf fuer diese Zelle) | Host-Kadenz > 25 ms: 11.81 % (p95 33.33 ms); DWM verworfen 0, verpasst 0 | OK (0.39 % gesamt; 0.078 Kernaequiv.; dwm.exe n/a %) | - |
| normal R2/64/hw/1900x250 (K: nicht transparent) | OK (Kosten p95 0.08 ms) | UNKLAR (PresentMon fehlt: kein PresentMon-Lauf fuer diese Zelle) | Host-Kadenz > 25 ms: 16.78 % (p95 33.34 ms); DWM verworfen 0, verpasst 0 | OK (0.35 % gesamt; 0.069 Kernaequiv.; dwm.exe n/a %) | - |

Die Spalte "Ersatz, nicht D-18" ist ein beschrifteter Ersatz (Host-Kadenz, DWM-Zaehler) und fliesst in kein Urteil ein.

## Einzelwerte je Zelle

| Zelle | Frames | Kosten p50 / p95 / p99 (ms) | Wirksamer Modus | Ruhe (Idle) | GC 0/1/2, Allokation | pm_rows |
|---|---|---|---|---|---|---|
| elevated R1/20/hw/1900x250 | 1702 | 0.01 / 0.01 / 0.02 | hardware (tier 2) | 0 Frames, 0.000 % CPU | 1/0/0, 10412 B/Frame | 0 |
| elevated R2/20/hw/1900x250 | 1569 | 0.02 / 0.03 / 0.07 | hardware (tier 2) | 0 Frames, 0.016 % CPU | 1/0/0, 12626 B/Frame | 0 |
| elevated R1/20/sw/1900x250 | 1544 | 0.01 / 0.02 / 0.05 | software (tier 2) | 0 Frames, 0.000 % CPU | 1/0/0, 10952 B/Frame | 0 |
| elevated R2/20/sw/1900x250 | 1472 | 0.02 / 0.04 / 0.08 | software (tier 2) | 0 Frames, 0.000 % CPU | 1/0/0, 12795 B/Frame | 0 |
| elevated R1/64/hw/1900x250 | 1736 | 0.02 / 0.03 / 0.05 | hardware (tier 2) | 0 Frames, 0.000 % CPU | 2/0/0, 16652 B/Frame | 0 |
| elevated R2/64/hw/1900x250 | 1685 | 0.04 / 0.10 / 0.17 | hardware (tier 2) | 0 Frames, 0.016 % CPU | 3/0/0, 24140 B/Frame | 0 |
| elevated R1/64/sw/1900x250 | 1772 | 0.03 / 0.08 / 0.12 | software (tier 2) | 0 Frames, 0.000 % CPU | 2/0/0, 16464 B/Frame | 0 |
| elevated R2/64/sw/1900x250 | 1683 | 0.05 / 0.17 / 0.22 | software (tier 2) | 0 Frames, 0.000 % CPU | 3/0/0, 23815 B/Frame | 0 |
| normal R1/20/hw/1900x250 | 1710 | 0.01 / 0.02 / 0.03 | hardware (tier 2) | 0 Frames, 0.016 % CPU | 1/0/0, 10415 B/Frame | n/a |
| normal R2/20/hw/1900x250 | 1610 | 0.02 / 0.04 / 0.05 | hardware (tier 2) | 0 Frames, 0.016 % CPU | 1/0/0, 12360 B/Frame | n/a |
| normal R1/20/sw/1900x250 | 1619 | 0.01 / 0.02 / 0.05 | software (tier 2) | 0 Frames, 0.000 % CPU | 1/0/0, 10703 B/Frame | n/a |
| normal R2/20/sw/1900x250 | 1506 | 0.02 / 0.05 / 0.09 | software (tier 2) | 0 Frames, 0.031 % CPU | 1/0/0, 12734 B/Frame | n/a |
| normal R1/64/hw/1900x250 | 1769 | 0.02 / 0.04 / 0.06 | hardware (tier 2) | 0 Frames, 0.031 % CPU | 2/0/0, 16508 B/Frame | n/a |
| normal R2/64/hw/1900x250 | 1723 | 0.05 / 0.09 / 0.15 | hardware (tier 2) | 0 Frames, 0.016 % CPU | 3/0/0, 23947 B/Frame | n/a |
| normal R1/64/sw/1900x250 | 1798 | 0.03 / 0.09 / 0.11 | software (tier 2) | 0 Frames, 0.000 % CPU | 2/0/0, 16435 B/Frame | n/a |
| normal R2/64/sw/1900x250 | 1642 | 0.04 / 0.17 / 0.24 | software (tier 2) | 0 Frames, 0.000 % CPU | 3/0/0, 23988 B/Frame | n/a |
| normal R1/64/hw/1900x250 (K: nicht transparent) | 1601 | 0.02 / 0.05 / 0.08 | hardware (tier 2) | 0 Frames, 0.016 % CPU | 2/0/0, 18694 B/Frame | n/a |
| normal R2/64/hw/1900x250 (K: nicht transparent) | 1527 | 0.03 / 0.08 / 0.11 | hardware (tier 2) | 0 Frames, 0.000 % CPU | 3/0/0, 25700 B/Frame | n/a |

## Grenzfaelle fuer Stephan (aus der Auswertung)

Keine Zelle ist ein Grenzfall nach diesen Regeln.

