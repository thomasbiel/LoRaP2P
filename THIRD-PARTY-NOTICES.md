# Third-Party Notices

## RFM9XLoRa-Net

Parts of the RFM95/SX1276 register model and hardware-control approach were derived from or informed by:

- Project: `KiwiBryn/RFM9XLoRa-Net`
- Repository: <https://github.com/KiwiBryn/RFM9XLoRa-Net>
- Copyright: devMobile Software contributors
- License: Apache License 2.0
- License text: <https://www.apache.org/licenses/LICENSE-2.0>

The original project targets Windows 10 IoT Core/UWP. This repository replaces its UWP SPI/GPIO implementation and control flow with a new .NET 10 implementation based on `System.Device.Gpio`.

## System.Device.Gpio

- Project: .NET IoT Libraries
- Repository: <https://github.com/dotnet/iot>
- License: MIT

## Vosk

- Project: Vosk offline speech recognition API
- Repository: <https://github.com/alphacep/vosk-api>
- License: Apache License 2.0
- License text: <https://www.apache.org/licenses/LICENSE-2.0>

`LoRaP2P.SpeechDisplay` uses the Vosk C# package and redistributes the unmodified
official Vosk 0.3.45 Linux AArch64 `libvosk.so`. Original upstream `COPYING`
and binary provenance are included under
`src/LoRaP2P.SpeechDisplay/runtimes/linux-arm64/native` and copied to the
`licenses` directory in Linux ARM64 publish output.

Speech models are installed separately. The recommended German model
`vosk-model-small-de-0.15` is listed under Apache License 2.0; other models may
have different licenses. See <https://alphacephei.com/vosk/models>.

## CommandLineParser

- Project: Command Line Parser Library for CLR and NetStandard
- Repository: <https://github.com/commandlineparser/commandline>
- Copyright: 2005-2015 Giacomo Stelluti Scala and contributors
- License: MIT
