# Umsetzungsplan für die P2P-Ausbaustufe

## Übersicht

Die Phasen bauen aufeinander auf. Eine Phase gilt erst als abgeschlossen,
wenn ihre Tests und Dokumentationsänderungen enthalten sind.

| Phase | Ergebnis | Abhängigkeit |
| ---: | --- | --- |
| 0 | Bidirektionaler Raw-LoRa-Basistest | Version 1 |
| 1 | Projektgerüst und Protokollmodelle | Phase 0 |
| 2 | Frame-Codec | Phase 1 |
| 3 | Konfiguration, Peers und Statistiken | Phase 1 |
| 4 | Empfang, ACK und Duplikate | Phasen 2–3 |
| 5 | Zuverlässiges Senden und Broadcast | Phase 4 |
| 6 | Konsolenbefehle aus PRD 12.6 | Phase 5 |
| 7 | Systemtest, Dokumentation und Release | Phase 6 |
| 8 | Optionale AES-GCM-Erweiterung | separates Folge-Release |

## Phase 0: Hardwarebasis bestätigen

Aufgaben:

- zwei Geräte auf identisches Funkprofil konfigurieren,
- Text und Binärdaten in beide Richtungen übertragen,
- RSSI und SNR plausibilisieren,
- Timeout bei falscher Frequenz prüfen,
- Timeout bei falschem Syncword prüfen,
- Abbruch und Timeout mit anschließendem Standby bestätigen.

Abschlusskriterium:

- Alle hardwarebezogenen Akzeptanzkriterien aus PRD 10 sind protokolliert.

## Phase 1: Projekt und öffentliche Modelle

Aufgaben:

- `LoRaP2P.Protocol` und `LoRaP2P.Protocol.Tests` anlegen,
- beide Projekte in `LoRaP2P.slnx` aufnehmen,
- `P2pNodeId` mit Wertebereich 1–65534 definieren,
- Nachrichtentypen und unveränderliches `P2pFrame` definieren,
- Optionen für ACK, Retry, Backoff und Duplikatfenster modellieren,
- Nullable und Warnings-as-Errors beibehalten.

Abschlusskriterium:

- Lösung baut; Modelle lehnen ungültige IDs und Optionen ab.

## Phase 2: Frame-Codec

Aufgaben:

- Encoding gemäß [Protokollspezifikation](protocol-v1.md) implementieren,
- Decoding ohne teilweise Ergebnisse implementieren,
- Big-Endian-Konvertierung zentralisieren,
- maximale Payloadgrößen erzwingen,
- typabhängige Payloadregeln validieren,
- unbekannte Versionen, Flags und Typen ablehnen.

Abschlusskriterium:

- Alle gültigen Framearten bestehen Roundtrip-Tests.
- Jede Validierungsregel besitzt mindestens einen Negativtest.

## Phase 3: Konfiguration, Peers und Statistiken

Aufgaben:

- `P2pConfiguration` und Validierung implementieren,
- separate JSON-Datei und `--peer-config` ergänzen,
- atomaren Configuration Store implementieren,
- lokale ID und Peer-IDs persistent verwalten,
- sitzungsbezogene Peer-Metadaten modellieren,
- thread-sichere Statistikzähler implementieren,
- Sequenznummerngenerator mit zufälligem Startwert implementieren,
- begrenzten Duplikatcache pro Peer implementieren.

Abschlusskriterium:

- Konfigurationsänderungen überstehen einen Reload.
- Fehlerhafte Dateien erzeugen verständliche Fehler statt Defaults.
- Statistiken und Duplikatcache sind deterministisch getestet.

## Phase 4: Empfang, ACK und Duplikate

Aufgaben:

- `P2pNode.ListenAsync` implementieren,
- CRC-Fehler vor dem Frame-Decoder verwerfen,
- lokale und Broadcast-Adressierung behandeln,
- neue Daten ausliefern,
- Duplikate erneut bestätigen, aber nicht erneut ausliefern,
- ACK nach 100 ms senden,
- fremde und ungültige Frames ohne ACK verwerfen,
- Peer-Metadaten und Statistiken aktualisieren,
- Cancellation bis zum Radio-Standby durchreichen.

Abschlusskriterium:

- Empfangs-, ACK- und Duplikatfälle bestehen ohne Hardware.

## Phase 5: Zuverlässiges Senden und Broadcast

Aufgaben:

- adressiertes `SendAsync` implementieren,
- ACK anhand Peer-ID und Sequenznummer zuordnen,
- festen Gesamt-ACK-Timeout von 2 Sekunden pro Versuch einhalten,
- bis zu drei Retries implementieren,
- Backoff von 250 bis 1000 ms injizierbar zufällig wählen,
- während der ACK-Wartezeit andere gültige Frames verarbeiten,
- Zustellfehler nach ausgeschöpften Versuchen melden,
- Broadcast ohne ACK und Retry implementieren,
- Duty-Cycle-Verhalten des vorhandenen Radios unverändert nutzen.

Abschlusskriterium:

- verlorenes erstes ACK führt zu genau einem Retry und einer Zustellung.
- vier erfolglose Versuche führen zu einem expliziten Zustellfehler.

## Phase 6: Konsolenbefehle

Aufgaben:

- `CommandContext` um P2P-Dienste erweitern,
- neue Verben implementieren und registrieren:
  - `peer id <id>`,
  - `peer add <id>`,
  - `listen`,
  - `send-to <id> text <value>`,
  - `send-to <id> hex <bytes>`,
  - `broadcast text <value>`,
  - `peers`,
  - `stats`,
- CommandLineParser-Hilfe aus Attributen erzeugen,
- Fehlertexte zwischen Parsing, Zustellung und Funkfehler unterscheiden,
- unverschlüsselten Betrieb beim Start von `listen`, `send-to` und
  `broadcast` sichtbar kennzeichnen,
- README um vollständige Beispiele ergänzen.

Abschlusskriterium:

- Jeder neue Befehl besitzt mindestens einen Erfolgs- und einen Fehlerfall.
- Bestehende Version-1-Befehle verhalten sich unverändert.

## Phase 7: Systemtest und Release

Aufgaben:

- vollständigen Release-Build und alle Tests ausführen,
- self-contained `linux-arm64` veröffentlichen,
- Zwei-Geräte-Testmatrix ausführen,
- absichtlich erstes ACK verwerfen,
- gleichzeitige Sendeversuche auslösen,
- Duty-Cycle-Wartezeiten dokumentieren,
- PRD-Status und Architekturdiagramm aktualisieren,
- bekannte Einschränkungen dokumentieren.

Abschlusskriterium:

- Sämtliche Kriterien aus PRD 12.7 sind erfüllt oder explizit als
  hardwarebedingt offen dokumentiert.

## Phase 8: AES-GCM als Folge-Release

Diese Phase beginnt erst nach Abnahme des unverschlüsselten P2P-Protokolls.
Details stehen in [security.md](security.md).

## Definition of Done

Ein Arbeitspaket ist fertig, wenn:

- Produktionscode und relevante Tests enthalten sind,
- Build und Tests ohne Warnungen erfolgreich sind,
- keine echte Hardware für Unit-Tests benötigt wird,
- Fehler explizit gemeldet werden,
- README, PRD und P2P-Dokumente konsistent sind,
- bei Plattformänderungen das `linux-arm64`-Publish erfolgreich ist,
- Hardwaretests mit Datum, Geräten und Funkprofil protokolliert sind.

