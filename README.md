# LoRaP2P Console

.NET 10 Raw LoRa PoC for Raspberry Pi 3B+ and Adafruit RFM95W Radio Bonnet. The project ports basic RFM9x register control to `System.Device.Gpio` and provides a testable foundation for a future LoRaWAN Class A client.

> The current implementation is Raw LoRa, not LoRaWAN. `TxDone` only confirms that the RFM95 has completed transmission.

Project documentation, CLI help, and application diagnostics are in English.
Both CLIs use an English .NET UI culture, and microphone capture uses the
`C` locale for ALSA diagnostics. Numeric culture is unchanged. Received messages
and recognized speech retain their original language; the default Vosk model
continues to recognize German speech.

The complete product concept is in [docs/PRD.md](docs/PRD.md).
The P2P expansion is specified in
[docs/p2p/README.md](docs/p2p/README.md).

## Hardware

| Bonnet signal | Raspberry Pi |
| --- | --- |
| SPI | SPI0 |
| CS | CE1 |
| RST | BCM GPIO25 |
| DIO0 | BCM GPIO22 |
| DIO1 | BCM GPIO23, not used |
| DIO2 | BCM GPIO24, not used |

Connect a suitable 868 MHz antenna before transmitting. The default configuration is intended for Germany/EU868.

## Prepare the Raspberry Pi

Enable SPI:

```bash
sudo raspi-config nonint do_spi 0
sudo usermod -aG spi,gpio "$USER"
```

Log in again or restart, then verify CE1:

```bash
ls -l /dev/spidev0.1
```

## Build and test

Requirement on the development computer: .NET SDK 10.

```powershell
dotnet restore
dotnet build LoRaP2P.slnx --configuration Release
dotnet test LoRaP2P.slnx --configuration Release
```

The tests use NUnit, a fake register transport, and a fake radio. They do not require radio hardware.

NuGet versions are centrally managed in [Directory.Packages.props](Directory.Packages.props).
The tests use NUnit 5.0.0, NUnit3TestAdapter 6.3.1, and
Microsoft.NET.Test.Sdk 18.10.1.

## Publish for Raspberry Pi OS 64-bit

```powershell
dotnet publish src/LoRaP2P.Console/LoRaP2P.Console.csproj `
  --configuration Release `
  --runtime linux-arm64 `
  --self-contained true `
  --output artifacts/linux-arm64
```

Copy the contents of `artifacts/linux-arm64` to the Pi and start it there:

```bash
chmod +x LoRaP2P.Console
./LoRaP2P.Console
```

Alternative configuration:

```bash
./LoRaP2P.Console --config /etc/LoRaP2P/radio.json
```

All fields, valid values, interactions, and duty-cycle and EU868 notes are in the
[radio configuration documentation](docs/radio-configuration.md).

Alternative P2P configuration:

```bash
./LoRaP2P.Console --peer-config /var/lib/lorap2p/p2p.json
```

Check basic wiring, radio hardware, OLED, and buttons:

```bash
./LoRaP2P.Console --check
```

The check runs the reset sequence, verifies SPI/CE1 communication using the
SX1276 version register, applies the radio configuration, and checks standby
status. The OLED then shows `RFM9x: Detected`. The buttons on GPIO5, GPIO6, and
GPIO12 show `Ada`, `Fruit`, and `Radio`, respectively. The test runs until
Ctrl+C. It sends no packet and requires no peer.

## Commands

```text
probe
status
reset
configure frequency 869.525
configure sf 7
configure bandwidth 125
configure power 14
send text ping
send hex DEADBEEF
receive 10
register read 0x42
register dump
peer id 1
peer add 2
listen
send-to 2 text "hello peer"
send-to 2 hex DEADBEEF
broadcast text "hello all"
peers
stats
export-chat /home/pi/LoRaP2P/sessions/session.jsonl
export-chat /home/pi/LoRaP2P/sessions/session.jsonl /home/pi/chat.html
quit
```

With a single Bonnet, SPI, registers, modem configuration, FIFO, and `TxDone`
can be checked. A second compatible RFM9x transceiver with an identical radio
profile is required for a real reception test.

## Raw LoRa P2P

Addressed P2P communication uses versioned binary frames, 16-bit device IDs,
ACKs, at most three retries, and random backoff. The supplied `p2p.json` initially
configures local ID 1 and no peers. `peer id` and `peer add` save changes
atomically to this file.

`send-to` reports delivery only after a matching peer ACK.
`broadcast` expects no ACK. `listen` runs until Ctrl+C and acknowledges valid
directly addressed frames. The current P2P version is neither authenticated nor
encrypted; AES-GCM is planned as a later expansion.

In normal operation, the OLED shows received broadcast text without an
additional status line. Text is wrapped to four lines of up to 21 characters
each. Longer messages scroll upward one line at a time, then leave the last page
displayed. A new broadcast message replaces the current display. Unsupported
characters are shown as `?`.

On the first transmission or reception, the application opens a JSONL session
file for each local ID and peer under `~/LoRaP2P/sessions`. Each line contains
exactly one JSON object with timestamp, direction, IDs, sequence number, text
and hexadecimal representation, and delivery or reception status. The file
remains open until the process ends. Broadcasts are stored in a separate
`peerbroadcast` session.

`export-chat <session.jsonl> [output.html]` creates a standalone HTML file with
chat bubbles. Without an output path, it is placed beside the JSONL file.
Export can also run while the session is still being written.

Wire format, architecture, operation, and tests are documented in
[docs/p2p/README.md](docs/p2p/README.md).

## Speech on the Bonnet Display

The separate `LoRaP2P.SpeechDisplay` CLI tool captures an ALSA microphone,
recognizes speech offline with Vosk, and displays text scrolling upward one
line at a time on the Bonnet OLED. It uses neither SPI nor the radio module.
An external microphone, such as a USB microphone, is required.

```powershell
dotnet publish src/LoRaP2P.SpeechDisplay/LoRaP2P.SpeechDisplay.csproj `
  --configuration Release `
  --runtime linux-arm64 `
  --self-contained true `
  --output artifacts/speech-display/linux-arm64
```

The native ARM64 library `libvosk.so` is published automatically.
Without `--model`, the tool automatically downloads the default German model
to `~/LoRaP2P/models` on first start and uses it from the cache thereafter:

```bash
./LoRaP2P.SpeechDisplay \
  --device plughw:1,0 \
  --scroll-ms 1000
```

Complete setup, microphone checks, and troubleshooting are documented in
[docs/speech-display.md](docs/speech-display.md). `--model-url <https-url>`
selects another model archive; `--model <path>` uses a local model without
downloading. Only initial model provisioning requires Internet. The speech model
and Linux system libraries are not part of the self-contained .NET output.

## Project structure

```text
src/LoRaP2P.Console/Commands    CommandLineParser verbs for the interactive console
src/LoRaP2P.Console             Configuration and application composition
src/LoRaP2P.Display.RadioBonnet Shared SSD1306 driver, font, and text layout
src/LoRaP2P.SpeechDisplay       ALSA microphone, Vosk, and scrolling OLED output
src/LoRaP2P.Protocol             P2P frames, ACKs, retries, peers, and statistics
src/LoRaP2P.Radio.Rfm9x          Hardware transport and SX1276/RFM95 driver
tests/LoRaP2P.Console.Tests     Hardware-free command-processing tests
tests/LoRaP2P.Protocol.Tests    Hardware-free P2P protocol tests
tests/LoRaP2P.Radio.Rfm9x.Tests Hardware-free radio-driver tests
docs/PRD.md                     Product requirements and expansion stages
```
