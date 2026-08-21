# MOGWAI NANO

![MOGWAI NANO](images/img01.png)

## [MOGWAI](https://github.com/Sydney680928/mogwai) NANO - Scripting for Microcontrollers

[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)
[![.NET nanoFramework](https://img.shields.io/badge/.NET-nanoFramework-blue.svg)](https://nanoframework.net/)

**Give your ESP32 or Raspberry Pi Pico W a scripting engine.** MOGWAI NANO brings the [MOGWAI](https://github.com/Sydney680928/mogwai) engine to embedded devices — write comfortable, sugared code on your PC, and run it remotely on real hardware over WiFi.

> If MOGWAI NANO looks useful to you, a ⭐ helps others discover it — thank you!

---

## What is MOGWAI NANO?

MOGWAI NANO is not a port of the desktop MOGWAI engine — it's a **companion runtime**, built from the ground up for the constraints of embedded devices. It works alongside **MOGWAI NANO Studio**, a desktop application that lets you write comfortable, sugared MOGWAI code, discover devices on your network, and run that code remotely on real hardware.

```
# What you write, comfortably, in MOGWAI NANO Studio
{
    if (a 10 >) then { "big" ? } else { "small" ? }
} nano.run
```

Behind the scenes, the code is **desugared** into pure canonical [RPN](https://en.wikipedia.org/wiki/Reverse_Polish_notation) before being sent to the device:

```
{ a 10 > } eval { "big" ? } { "small" ? } IFELSE
```

The device only ever executes this minimal form — no complex parser, no syntactic sugar to maintain twice. Just a tokenizer, a stack, and a primitive dispatcher, running in a few tens of kilobytes of RAM.

> **Note:** you never write the canonical form by hand. MOGWAI NANO Studio runs on the desktop MOGWAI engine, which only exposes sugared constructs (`if...then...else`, `for...do`, `while...do`, `forever do`, `timer...every...do`, `->`, etc.) to the developer — the underlying canonical primitives (`IF`, `IFELSE`, `FOR`, `WHILE`, `FOREVER`, `EVERY`, `AFTER`, `EVENT`, `STO`...) are private and generated automatically during desugaring, not meant to be typed directly.

## Why two runtimes?

- **MOGWAI NANO Studio** (PC) — full syntactic sugar, an integrated editor, network device discovery, and orchestration logic written in regular MOGWAI.
- **MOGWAI NANO** (device) — a minimal, rigorously disciplined RPN interpreter with GPIO, I2C, SPI, PWM, ADC, timers, and event support.

This separation means the device firmware stays small and stable, while the desktop side can evolve freely — including reusing the existing [MOGWAI VS Code extension](https://github.com/Sydney680928/mogwai) with zero modification, since from the editor's point of view, you're just writing MOGWAI.

## Prerequisite: know a bit of MOGWAI first

MOGWAI NANO Studio runs on the desktop MOGWAI engine, and everything you write — including the code sent to a device via `nano.run` — is regular MOGWAI syntax (stack-based RPN, with syntactic sugar for control flow). If you've never used MOGWAI before, spend a few minutes with its documentation and tutorial first:

- [MOGWAI repository](https://github.com/Sydney680928/mogwai) — full documentation, tutorial, language reference
- [Try MOGWAI in your browser](https://github.com/Sydney680928/mogwai) — no install needed, a fast way to get a feel for the RPN style

Once you're comfortable with the basics of the language itself, the [Getting Started guide](docs/getting-started.md) below picks up from there and focuses on what's specific to NANO: connecting to a device, GPIO, timers, events, and the desugaring model.

## Key features

- **Full scripting language** — arithmetic, comparisons, control flow (`if...then...else`, `while...do`, `for...do`, `forever do`), user-defined functions, references (`&`)
- **Hardware support** — GPIO (digital I/O, interrupts) today; I2C, SPI, PWM and ADC packages already validated, primitives coming in upcoming releases
- **Timers** — one-shot and recurring, running independently of your main program
- **Events** — subscribe to hardware events (like GPIO changes) with data delivered through a `MOGRecord`
- **Network protocol** — UDP discovery + reliable TCP communication, with automatic disconnection detection and clean recovery
- **Persistent autorun** — store code to run automatically on every boot, for standalone production deployments
- **Cross-platform** — the exact same compiled binary runs on ESP32 and Raspberry Pi Pico W

## Supported platforms

| Platform | Status |
|---|---|
| ESP32 | ✅ Tested — recommended |
| Raspberry Pi Pico W | ⚠️ Runtime tested and working, but WiFi configuration currently blocked (see Quick Start note) |
| STM32 | 🔜 Should work — nanoFramework supports it, not yet tested by us |
| TI | 🔜 Should work — nanoFramework supports it, not yet tested by us |

## Quick start

### 1. Flash the firmware + application

Download the latest `MogwaiNano.bin` from the [Releases](../../releases) page, then:

```bash
# Install the nanoFramework flashing tool
dotnet tool install -g nanoff

# ESP32 — two separate commands (see note below on why)

# 1. Flash the firmware (--masserase avoids issues from leftover factory partitions on a brand-new board)
nanoff --target ESP32_REV3 --serialport COMx --masserase --update

# 2. Deploy the application
nanoff --target ESP32_REV3 --serialport COMx --deploy --image MogwaiNano.bin --address 0x1E0000
```

> **Why two separate commands, and why `--address` is required:** on `ESP32_REV3`, `nanoff`'s automatic deployment address calculation is unreliable and consistently fails to match the device's actual partition layout — confirmed across three different boards, regardless of firmware version. Always pass `--address 0x1E0000` explicitly when deploying. It also needs to be in its own command, separate from `--update`: combining `--update` and `--deploy --address` in a single invocation causes `--address` to be silently ignored.
>
> **Why `--masserase`:** on a board that was never flashed with nanoFramework before (or previously ran different firmware), leftover factory partition data can cause the deployment to fail — `--masserase` wipes the flash clean first, avoiding this entirely. Recommended on every first flash of a new board.
>
> After deploying, check the device with `nanoff --devicedetails` or Device Explorer and look at the `Assemblies:` section to confirm success — `MogwaiNano` should be listed there with its dependencies. The `Deployment Map` field further down is unrelated to this (it reports on In-Field Update capability, which this target doesn't support) and will read `Empty` even on a fully successful deployment — don't use it to diagnose a failed deploy.

> On ESP32, you may be asked to hold the BOOT/FLASH button on the board during flashing.

> **Raspberry Pi Pico W:** flashing the firmware and deploying the application both work, but WiFi network configuration via `nanoff --networkdeployment` currently hangs on this target — see [Known Limitations](CHANGELOG.md) below. Until this is resolved, ESP32 is the recommended target to follow this guide with.

### 2. Configure WiFi

```bash
nanoff --networkdeployment wifi.json
```

with a `wifi.json` file:

```json
{
    "SerialPort": "COMx",
    "WirelessClient": {
        "Ssid": "YourNetworkName",
        "Password": "YourPassword"
    }
}
```

### 3. Run your first program

From **MOGWAI NANO Studio**:

```
"192.168.1.75" nano.connect
{ 5 gpio.setMode.output forever do { 5 gpio.write.high 500 wait 5 gpio.write.low 500 wait } } nano.run
```

Not sure of your device's IP? Discover it on the network instead:

```
nano.user.select -> 'device'
if (device ->type .record ==) then { device->ip: nano.connect }
```

`nano.user.select` scans the network, lists the responding devices (platform and IP), and lets you pick one interactively — pushing `null` on the stack if you abort or nothing responds.

Your device is now blinking an LED, controlled remotely from your PC. 🎉

## Project structure

```
src/MogwaiNano/
├── MogwaiNano/          # Device runtime (deployed to ESP32 / Pico W)
└── MogwaiNanoStudio/    # Desktop companion app (editor, network client)
```

## Documentation

- [Getting started guide](docs/getting-started.md) — step-by-step NANO tutorial (connect, GPIO, timers, events)
- [Shared primitives reference](docs/shared-primitives.md) — primitives common to both MOGWAI and MOGWAI NANO
- [Network protocol](docs/) *(coming soon)*

## Roadmap

- [ ] `.binary`/`B:` support on NANO — for register-level bit manipulation
- [ ] I2C/SPI helper primitives for common sensors (starting with SSD1306 OLED displays)
- [ ] STM32 and TI validation

## About

MOGWAI NANO was born from an old idea: bringing [MOGWAI](https://github.com/Sydney680928/mogwai) to microcontrollers, using tools I already knew well. Read the full story on the blog: [MOGWAI Comes Down to Earth (and Silicon)](https://coding4phone.com/?p=2778).

Special thanks to [Laurent Ellerbach](https://github.com/Ellerbach), an active nanoFramework contributor, for his invaluable help throughout this project.

## Related projects

- [MOGWAI](https://github.com/Sydney680928/mogwai) — the desktop scripting engine
- [.NET nanoFramework](https://nanoframework.net/) — the .NET CLR for microcontrollers

## License

MOGWAI NANO is licensed under the [Apache License 2.0](LICENSE). See [NOTICE](NOTICE) for third-party attributions.
