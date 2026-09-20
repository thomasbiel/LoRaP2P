# P2P-Ausbaustufe

Dieses Verzeichnis konkretisiert die Raw-LoRa-Zwei-Wege-Kommunikation
aus [Abschnitt 12 des PRD](../PRD.md#12-raw-lora-zwei-wege-kommunikation).

Protokollkern, Persistenz, Konsolenbefehle und hardwarefreie Tests sind
implementiert. Offen sind die bidirektionale Hardwareabnahme mit zwei Geräten
und die optionale AES-GCM-Ausbaustufe.

## Ziel

Zwei LoRaP2P-Geräte sollen adressierte Raw-LoRa-Nachrichten austauschen,
Empfangsbestätigungen senden, verlorene Übertragungen wiederholen und
Duplikate unterdrücken. Broadcasts werden ohne ACK übertragen.

LoRaWAN, ein Gateway, ein Network Server und Internetzugriff sind dafür nicht
erforderlich.

## Festgelegte Produktentscheidungen

| Thema | Entscheidung |
| --- | --- |
| Geräte-ID | 16-Bit-Zahl von 1 bis 65534 |
| Broadcast-ID | 65535 (`0xFFFF`) |
| Ungültige/unvergebene ID | 0 |
| Byte-Reihenfolge | Network Byte Order, Big Endian |
| ACK-Timeout | 2 Sekunden |
| Wiederholungen | maximal 3 Retries nach dem ersten Versuch |
| Backoff | zufällig 250 bis 1000 ms |
| ACK-Wartezeit des Empfängers | 100 ms |
| Peer-Konfiguration | separate persistente JSON-Datei ohne Geheimnisse |
| Nachrichtenpersistenz | JSONL je lokaler ID und Gegenstelle unter `~/LoRaP2P/sessions` |
| Chat-Export | eigenständiges HTML über `export-chat` |
| `listen` beenden | Strg+C beendet die gesamte Anwendung |
| Verschlüsselung | separate spätere Phase; erstes P2P-Release unverschlüsselt |

## Dokumente

- [Funkkonfiguration](../radio-configuration.md): sämtliche Felder der
  `appsettings.json`, Time-on-Air, Duty Cycle und EU868-Rahmenbedingungen.
- [Protokollspezifikation](protocol-v1.md): binäres Wire-Format,
  Validierung, ACKs, Retries und Duplikate.
- [Zielarchitektur](architecture.md): Komponenten, Abhängigkeiten,
  Zustände und Datenflüsse.
- [Umsetzungsplan](implementation-plan.md): Reihenfolge, Arbeitspakete,
  Abhängigkeiten und Definition of Done.
- [Test- und Betriebsplan](test-and-operations.md): Unit-Tests,
  CLI-Verhalten, Konfiguration und Hardwareabnahme.
- [Sicherheitsausbaustufe](security.md): spätere optionale
  AES-GCM-Integration und Schlüsselverwaltung.

## Abgrenzung des ersten P2P-Releases

Enthalten:

- versionierte Frames,
- adressierte Daten und Broadcasts,
- ACK, Timeout, Retry und zufälliger Backoff,
- Duplikatunterdrückung,
- Peer-Registry und Sitzungsstatistiken,
- sämtliche Befehle aus PRD 12.6,
- JSONL-Sitzungspersistenz und HTML-Chat-Export,
- hardwarefreie NUnit-Tests,
- bidirektionale Abnahme mit zwei RFM9x-Geräten.

Nicht enthalten:

- LoRaWAN,
- Routing über mehrere Hops,
- Gruppenadressen außer Broadcast,
- Fragmentierung von Payloads über mehrere Funkframes,
- persistente Nachrichtenwarteschlangen,
- automatische Peer-Erkennung,
- AES-GCM im ersten P2P-Release.
