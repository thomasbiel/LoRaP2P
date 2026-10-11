# P2P Extension

This directory specifies the Raw LoRa two-way communication from [section 12 of the PRD](../PRD.md#12-raw-lora-two-way-communication).

Protocol core, persistence, console commands, and hardware-free tests are
implemented. Open items are two-device bidirectional hardware acceptance and
the optional AES-GCM extension.

## Goal

Two LoRaP2P devices shall exchange addressed Raw LoRa messages, send
acknowledgements, retry lost transmissions, and suppress duplicates. Broadcasts
are transmitted without ACKs.

LoRaWAN, a gateway, a network server, and Internet access are not required.

## Fixed Product Decisions

| Topic | Decision |
| --- | --- |
| Device ID | 16-bit number from 1 to 65534 |
| Broadcast ID | 65535 (`0xFFFF`) |
| Invalid/unassigned ID | 0 |
| Byte order | Network Byte Order, Big Endian |
| ACK timeout | 2 seconds |
| Retries | maximum 3 retries after the first attempt |
| Backoff | random 250 to 1000 ms |
| Receiver ACK wait | 100 ms |
| Peer configuration | separate persistent JSON file without secrets |
| Message persistence | JSONL per local ID and peer under `~/LoRaP2P/sessions` |
| Chat export | standalone HTML via `export-chat` |
| Stop `listen` | Ctrl+C terminates the entire application |
| Encryption | separate later phase; first P2P release unencrypted |

## Documents

- [Radio configuration](../radio-configuration.md): all fields of
  `appsettings.json`, time-on-air, duty cycle, and EU868 constraints.
- [Protocol specification](protocol-v1.md): binary wire format,
  validation, ACKs, retries, and duplicates.
- [Target architecture](architecture.md): components, dependencies,
  states, and data flows.
- [Implementation plan](implementation-plan.md): order, work packages,
  dependencies, and Definition of Done.
- [Test and operations plan](test-and-operations.md): unit tests,
  CLI behavior, configuration, and hardware acceptance.
- [Security extension](security.md): later optional AES-GCM integration
  and key management.

## Scope of the First P2P Release

Included:

- versioned frames,
- addressed data and broadcasts,
- ACK, timeout, retry, and random backoff,
- duplicate suppression,
- peer registry and session statistics,
- all commands from PRD 12.6,
- JSONL session persistence and HTML chat export,
- hardware-free NUnit tests,
- bidirectional acceptance with two RFM9x devices.

Not included:

- LoRaWAN,
- routing over multiple hops,
- group addresses other than broadcast,
- fragmentation of payloads across multiple radio frames,
- persistent message queues,
- automatic peer discovery,
- AES-GCM in the first P2P release.
