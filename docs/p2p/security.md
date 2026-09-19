# Optionale AES-GCM-Ausbaustufe

## Status

AES-GCM ist nicht Teil des ersten P2P-Releases. Die unverschlüsselte Version
wird zuerst implementiert und mit zwei Geräten abgenommen. Diese Datei hält
die Sicherheitsanforderungen für ein separates Folge-Release fest.

## Bedrohungsmodell

Ohne Verschlüsselung kann ein Dritter im Funkbereich:

- Payloads mitlesen,
- Frames mit fremden Absender-IDs erzeugen,
- aufgezeichnete Frames erneut senden,
- Inhalte manipulieren.

LoRa-Payload-CRC schützt nur gegen Übertragungsfehler und bietet keine
Authentifizierung.

## Geplantes Verfahren

- AES-GCM mit 256-Bit-Schlüssel,
- 12-Byte-Nonce,
- 16-Byte-Authentication-Tag,
- Protokollkopf als Additional Authenticated Data,
- Verschlüsselung nur der Anwendungs-Payload,
- kein ACK für Frames mit ungültigem Tag.

Ein verschlüsselter Data-Inhalt besteht aus:

```text
Nonce (12 Byte) | Ciphertext (0–215 Byte) | Tag (16 Byte)
```

Bei unveränderter RFM95-Grenze von 255 Byte und 12 Byte Protokollheader bleiben
maximal 215 Byte Klartext.

## Nonce-Strategie

Die Sequenznummer reicht nicht als Nonce. Vorgesehen ist:

- 4 Byte zufälliges, pro Geräteinstallation persistiertes Salt,
- 8 Byte persistenter monotoner Zähler,
- atomare Reservierung von Zählerbereichen vor ihrer Verwendung.

Ein Prozessabsturz darf zu übersprungenen Nonces führen, niemals zu
Wiederverwendung. Zählerüberlauf blockiert weiteres verschlüsseltes Senden und
verlangt Schlüsselrotation.

## Schlüsselablage

- Schlüssel niemals in `appsettings.json`, `p2p.json`, Logs oder Repository.
- Laden über eine separate Datei oder einen Plattform-Secret-Store.
- Linux-Datei nur für den Dienstbenutzer lesbar, typischerweise Modus `0600`.
- Fehlerhafte Dateirechte erzeugen mindestens eine deutliche Warnung und
  sollen im unbeaufsichtigten Betrieb den Start verhindern.
- Schlüsselmaterial darf in Fehlermeldungen und Diagnosen nicht erscheinen.

## Protokollerweiterung

Flag-Bit 0 kennzeichnet zukünftig eine AES-GCM-Payload. Empfänger ohne
implementierte Verschlüsselung verwerfen gesetzte Flags entsprechend
Protokoll Version 1. Vor Aktivierung muss entschieden werden, ob:

- Version 1 um das Flag erweitert wird, oder
- eine Protokollversion 2 eingeführt wird.

Eine neue Version ist vorzuziehen, wenn sich ACK- oder Error-Authentifizierung
ebenfalls ändert.

## Zusätzliche Tests

- erfolgreicher Encrypt/Decrypt-Roundtrip,
- falscher Schlüssel,
- manipulierte Header, Ciphertexte, Nonces und Tags,
- wiederverwendete Nonce,
- persistenter Zähler nach Neustart,
- simulierte Abstürze während Zählerreservierung,
- maximale verschlüsselte Payload,
- keine ACKs für nicht authentifizierte Frames,
- keine Schlüssel oder Klartexte in Logs.

