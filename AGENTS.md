# Agent Guide

## Sources of Truth

- Read [README.md](README.md) for setup, build, test, publish, hardware, and CLI usage.
- Read [docs/PRD.md](docs/PRD.md) before changing behavior, radio constraints, or architecture.
- This project currently implements Raw LoRa, not LoRaP2P. Do not describe `TxDone` as peer reception, and do not treat an RFM95 as a LoRaP2P gateway.

## Architecture

- `src/LoRaP2P.Console` owns configuration, command parsing, and application composition.
- `src/LoRaP2P.Radio.Rfm9x` owns the hardware-independent radio API and RFM9x behavior.
- Keep `ILoraRadio` free of console, transport, and LoRaP2P-specific concerns.
- Keep the radio state machine dependent on `IRfm9xRegisterTransport`; isolate GPIO/SPI access in `SystemDeviceRfm9xTransport`.
- Use `FakeRfm9xRegisterTransport` for tests. Development and unit tests must not require Raspberry Pi or radio hardware.

## Implementation Constraints

- Target .NET 10 with nullable reference types and implicit usings enabled. Warnings are errors.
- Follow existing C# and NUnit patterns; make focused changes and avoid speculative abstractions.

## Validation

- Run the restore, Release build, and hardware-free NUnit commands documented in [README.md](README.md#bauen-und-testen).
- For publish or platform changes, also run the documented self-contained `linux-arm64` publish command.
- Hardware validation is separate from unit tests; follow [docs/PRD.md](docs/PRD.md#10-akzeptanzkriterien) and report when Pi/RFM95 hardware was unavailable.
