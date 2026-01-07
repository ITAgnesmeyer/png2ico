# png2ico

CLI-Werkzeug zum Erzeugen von Windows-ICO-Dateien aus einer einzelnen PNG-Quelle. Mehrere quadratische Icon-Groessen werden mit Transparenz in das Ziel-ICO eingebettet, sodass das Ergebnis in modernen Windows-Versionen und Browsern funktioniert.

## Voraussetzungen
- .NET 8 SDK oder neuer (Projektziel: .NET 10)
- Plattform mit Shell-Zugriff (PowerShell, cmd, Bash)

## Build
```bash
dotnet build
```
Das erzeugte Artefakt liegt standardmaessig unter `bin/Debug/net10.0/png2ico.dll`.

## Nutzung
```bash
png2ico --in input.png --out output.ico [--sizes 16,24,32,48,64,128,256]
```

| Option | Beschreibung |
| --- | --- |
| `--in`, `-i` | Pfad zur eingehenden PNG-Datei (erforderlich) |
| `--out`, `-o` | Pfad zur ausgehenden ICO-Datei (erforderlich) |
| `--sizes` | Komma-getrennte Liste von Zielkantenlaengen zwischen 1 und 256 Pixel (Standard: `16,24,32,48,64,128,256`) |

## Beispiele
```bash
png2ico -i logo.png -o favicon.ico
png2ico -i logo.png -o app.ico --sizes 16,32,48,256
```

## Funktionsweise
1. Die Eingabe-PNG wird geladen und fuer jede angeforderte Groesse proportional skaliert.
2. Jedes Bild wird auf eine quadratische Flaeche gelegt, um ICO-Anforderungen zu erfuellen.
3. Jede Variante wird als PNG in den ICO-Container geschrieben, inklusive Alpha-Kanal.

## Lizenz
Kein Lizenztext vorhanden. Bitte bei Bedarf ergaenzen.
