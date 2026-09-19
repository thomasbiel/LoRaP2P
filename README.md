# LoRaP2P Console

.NET-10-Raw-LoRa-PoC für Raspberry Pi 3B+ und Adafruit RFM95W Radio Bonnet. Das Projekt portiert die grundlegende RFM9x-Registersteuerung auf `System.Device.Gpio` und schafft eine testbare Basis für einen späteren LoRaWAN-Class-A-Client.

> Der aktuelle Stand ist Raw LoRa, nicht LoRaWAN. `TxDone` bestätigt nur, dass der RFM95 den Sendevorgang beendet hat.

Das vollständige Produktkonzept steht in [docs/PRD.md](docs/PRD.md).

## Hardware

| Bonnet-Signal | Raspberry Pi |
| --- | --- |
| SPI | SPI0 |
| CS | CE1 |
| RST | BCM GPIO25 |
| DIO0 | BCM GPIO22 |
| DIO1 | BCM GPIO23, nicht verwendet |
| DIO2 | BCM GPIO24, nicht verwendet |

Vor dem Senden eine passende 868-MHz-Antenne anschließen. Die Standardkonfiguration ist für Deutschland/EU868 vorgesehen.

## Raspberry Pi vorbereiten

SPI aktivieren:

```bash
sudo raspi-config nonint do_spi 0
sudo usermod -aG spi,gpio "$USER"
```

Danach neu anmelden oder neu starten und CE1 prüfen:

```bash
ls -l /dev/spidev0.1
```

## Bauen und testen

Voraussetzung auf dem Entwicklungsrechner: .NET SDK 10.

```powershell
dotnet restore
dotnet build LoRaP2P.slnx --configuration Release
dotnet test LoRaP2P.slnx --configuration Release
```

Die Tests verwenden NUnit, einen Fake-Registertransport und ein Fake-Radio. Sie benötigen keine Funkhardware.

## Für Raspberry Pi OS 64-bit veröffentlichen

```powershell
dotnet publish src/LoRaP2P.Console/LoRaP2P.Console.csproj `
  --configuration Release `
  --runtime linux-arm64 `
  --self-contained true `
  --output artifacts/linux-arm64
```

Den Inhalt von `artifacts/linux-arm64` auf den Pi übertragen und dort starten:

```bash
chmod +x LoRaP2P.Console
./LoRaP2P.Console
```

Alternative Konfiguration:

```bash
./LoRaP2P.Console --config /etc/LoRaP2P/radio.json
```

Grundlegende Verdrahtung, Funkhardware, OLED und Taster prüfen:

```bash
./LoRaP2P.Console --check
```

Der Check führt die Reset-Sequenz aus, prüft die SPI-/CE1-Kommunikation anhand
des SX1276-Versionsregisters, wendet die Funkkonfiguration an und kontrolliert
den Standby-Status. Das OLED zeigt anschließend `RFM9x: Detected`. Die Taster
auf GPIO5, GPIO6 und GPIO12 blenden `Ada`, `Fruit` beziehungsweise `Radio` ein.
Der Test läuft bis Ctrl+C. Es wird kein Paket gesendet und keine Gegenstelle
benötigt.

## Befehle

```text
probe
status
reset
configure frequency 868.1
configure sf 7
configure bandwidth 125
configure power 14
send text ping
send hex DEADBEEF
receive 10
register read 0x42
register dump
quit
```

Mit einem einzelnen Bonnet lassen sich SPI, Register, Modemkonfiguration, FIFO und `TxDone` prüfen. Für einen echten Empfangstest wird ein zweiter kompatibler RFM9x-Transceiver mit identischem Funkprofil benötigt.

## Projektstruktur

```text
src/LoRaP2P.Console/Commands    CommandLineParser-Verben der interaktiven Konsole
src/LoRaP2P.Console             Konfiguration und Anwendungskomposition
src/LoRaP2P.Radio.Rfm9x         Hardwaretransport und SX1276/RFM95-Treiber
tests/LoRaP2P.Console.Tests     Hardwarefreie Tests der Kommandoverarbeitung
tests/LoRaP2P.Radio.Rfm9x.Tests Hardwarefreie Tests des Radiotreibers
docs/PRD.md                     Produktanforderungen und Ausbaustufen
```
