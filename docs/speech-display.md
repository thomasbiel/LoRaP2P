# Speech on the Radio Bonnet Display

`LoRaP2P.SpeechDisplay` is a standalone .NET 10 CLI for Raspberry Pi OS 64-bit.
An external ALSA microphone provides speech, and Vosk converts it to text
locally. The Adafruit Radio Bonnet SSD1306 OLED shows four lines of up to 21
characters each. No radio hardware is used. Network access is required only for
the initial download of a model that is not already present; speech recognition
remains offline.

## Prepare the Raspberry Pi

The Bonnet has no microphone. Connect a USB microphone and enable I2C:

```bash
sudo raspi-config nonint do_i2c 0
sudo apt-get update
sudo apt-get install -y alsa-utils unzip curl libatomic1 libstdc++6
sudo usermod -aG audio,i2c,gpio "$USER"
```

Log in again afterward. Display assignment: I2C1, address `0x3C`, reset BCM
GPIO4, 128 x 32 pixels. The Bonnet buttons are not used by the speech tool.
Do not start it at the same time as `LoRaP2P.Console`: both programs access the
same OLED and reset pin.

## Check the microphone

```bash
arecord -l
arecord -L
arecord --device plughw:1,0 --format S16_LE --rate 16000 --channels 1 \
  --duration 5 /tmp/microphone.wav
aplay /tmp/microphone.wav
```

Replace `plughw:1,0` with the recording device. `plughw` lets ALSA convert the
microphone format to 16 kHz, mono, and 16-bit little-endian PCM. A stable device
name such as `plughw:CARD=USB,DEV=0` prevents problems caused by changing card
numbers. Without `--device`, `default` is used.

## Vosk and speech model

The `Vosk` 0.3.38 NuGet package provides the C# API but contains no Linux ARM64
library. Its host-dependent copy rules are disabled in the project so a Windows
publish does not copy a Windows DLL to the Pi. The official Vosk 0.3.45 AArch64
library is kept unchanged under
`src/LoRaP2P.SpeechDisplay/runtimes/linux-arm64/native` and is copied beside the
CLI automatically for `linux-arm64`. License and provenance information are
published under `licenses`. Other target architectures do not receive this
ARM64 library.

By default, the tool loads `libvosk.so` from its program directory, independent
of the current working directory. `--vosk-library <path>` is an optional
override, for example for other architectures or custom builds.

Without `--model`, the tool automatically downloads
<https://alphacephei.com/vosk/models/vosk-model-small-de-0.15.zip>.
Models are stored in the user directory under `~/LoRaP2P/models/<URL-SHA256>`.
The URL determines the cache key; different model URLs receive separate
directories. Existing complete models are reused without network access.

`--model-url https://example.org/another-vosk-model.zip` selects another model
archive. Only HTTPS URLs without credentials or a fragment are allowed. The ZIP
must contain exactly one Vosk model in the archive root or a direct
subdirectory. It is first downloaded and extracted into a temporary directory,
then checked using the model, configuration, and graph files, and moved
atomically into the cache. The ZIP and temporary files are removed after errors
or interruption as well. The total model-provisioning timeout is 30 minutes.

`--model /path/to/unzipped/model` uses an existing local model instead,
without downloading or changing that directory. This also enables a completely
network-free first start.

The native library requires `libatomic.so.1`, `libstdc++.so.6`, `libm.so.6`,
`libgcc_s.so.1`, and `libc.so.6`. These system libraries must be present on the
Pi; a self-contained .NET publish does not replace them. After transferring the
publish directory, run `ldd ./libvosk.so` and resolve entries marked `not found`.
A speech model is not part of the publish directory, but is downloaded
automatically when needed. The small German model is about 45 MB and, according
to the provider, requires approximately 300 MB RAM at runtime. Use a small
model on the Pi 3B+.

Source and model licenses: <https://alphacephei.com/vosk/models>.

## Build and start

On the development machine:

```powershell
dotnet publish src/LoRaP2P.SpeechDisplay/LoRaP2P.SpeechDisplay.csproj `
  --configuration Release `
  --runtime linux-arm64 `
  --self-contained true `
  --output artifacts/speech-display/linux-arm64
```

Transfer the publish directory to the Pi and start it there:

```bash
chmod +x LoRaP2P.SpeechDisplay
./LoRaP2P.SpeechDisplay \
  --device plughw:1,0 \
  --scroll-ms 1000
```

`--help` shows all options. `--no-display` allows microphone and Vosk tests on
Linux without an attached Bonnet; recognized text appears in the terminal.
`--scroll-ms` accepts 100 to 10000 milliseconds. Ctrl+C stops capture and the
scroll task, releases Vosk/GPIO/I2C, and turns off the OLED.

CLI help, application errors, .NET diagnostics, and ALSA capture errors use
English regardless of the system UI language. This does not translate captured
speech or change the recognition model or numeric culture.

## Display behavior

After a speech pause, Vosk provides a confirmed text segment. It is printed in
the terminal and queued for the display with word wrapping. Vosk partial results
are not shown, so corrected hypotheses do not produce duplicate words. As a
result, text is not displayed immediately for each word; long sentences appear
only after the next speech pause.

One more line is inserted per interval; after four lines, the oldest moves out of
the top of the display. New sentences do not replace an active display. Without
new speech, the last text remains visible. The buffer is limited to 256 pending
lines; under overload, the oldest not-yet-displayed lines are dropped. Umlauts
and sharp S are present in the font; unknown characters appear as `?`.

The application stores neither audio nor transcripts. However, recognized text
is sent to stdout and can be stored through terminal logging. Remove the
microphone test recording at `/tmp/microphone.wav` when needed.

## Validation

Hardware-free NUnit tests cover the scroll buffer, line wrapping, framebuffer,
Vosk JSON, ALSA arguments, fragmented PCM reads, and cancellation. Download
tests use simulated HTTP responses for the cache, local models, custom URLs,
errors, interruption, and incomplete or unsafe ZIPs.

```powershell
dotnet test tests/LoRaP2P.Console.Tests/LoRaP2P.Console.Tests.csproj `
  --configuration Release `
  --filter "FullyQualifiedName~SpeechDisplayTests|FullyQualifiedName~RadioBonnetTextLayoutTests|FullyQualifiedName~VoskModelStoreTests"
```

Verify on real hardware: microphone intelligibility, German text after speech
pauses, word wrapping and upward scrolling beyond four lines, multiple
consecutive sentences, Ctrl+C, and a subsequent restart. A successful ARM64
publish replaces neither testing the native Vosk library on the Pi nor the
microphone/OLED test.
