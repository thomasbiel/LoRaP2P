# Bundled Vosk Native Library

- Version: Vosk 0.3.45
- Platform: Linux AArch64, ELF64 little-endian
- Source: <https://github.com/alphacep/vosk-api/releases/download/v0.3.45/vosk-linux-aarch64-0.3.45.zip>
- Archive entry: `vosk-linux-aarch64-0.3.45/libvosk.so`
- License: Apache License 2.0; original upstream `COPYING` is included unchanged.
- License source: <https://raw.githubusercontent.com/alphacep/vosk-api/v0.3.45/COPYING>

SHA256 of `libvosk.so`:

```text
0e9df29f060a93cf3df3263a4d3635e1b75688a5fd84e86ade1599372e3c9597
```

SHA256 of the original release archive:

```text
45e95d37755deb07568e79497d7feba8c03aee5a9e071df29961aa023fd94541
```

The binary is not modified. `linux-arm64` builds and publishes copy it next to
the executable. Other target platforms do not receive this binary. The managed
Vosk API version is independently pinned in the repository's central package
configuration; the application uses the model/recognizer C ABI available in
both versions.

ELF `DT_NEEDED`: `libatomic.so.1`, `libstdc++.so.6`, `libm.so.6`,
`libgcc_s.so.1`, `libc.so.6`. Parsed GLIBC requirement: `GLIBC_2.17`.
These operating-system libraries are not bundled. Raspberry Pi OS 64-bit must
provide them. Verify with `ldd ./libvosk.so` on the target system.
