# Changelog

All notable changes to MOGWAI NANO will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

 

### Added

**Open Source Release**

- MOGWAI NANO is now open source under Apache 2.0 license
- Available on GitHub at https://github.com/Sydney680928/MogwaiNano

**Core Language**

- Full RPN interpreter — tokenizer, stack, primitive dispatcher
- Arithmetic (`+`, `-`, `*`, `/`), comparisons (`==`, `!=`, `<`, `>`, `<=`, `>=`), boolean operators
- Stack operations (`dup`, `swap`, `drop`, `clear`)
- Control flow: `IF`, `IFELSE`, `WHILE`, `REPEAT`, `FOR`, `FORSTEP`, `FOREVER`, `FOREACH`
- Variable storage (`STO`), local and global (`$`-prefixed) scopes
- User-defined functions (`DEFUNC`) with dedicated local scope, protected against name collisions with primitives and other functions
- Reference sigil (`&`) — direct object reference instead of copy, significantly reducing memory allocation and fragmentation on long-running scripts
- Types: `MOGNumber` (`float`-based, to leverage ESP32 hardware FPU), `MOGString`, `MOGName`, `MOGList`, `MOGRecord`, `MOGKey`, `MOGCode`, `MOGFunction`, `MOGData` (raw byte buffers, `D:` literal syntax)
- `get`/`set` primitives for list (by index) and record (by key) access, plus `size` for collection length
- System primitives — `mogwai.halt`, `mogwai.memory` (free RAM reporting), `mogwai.reset`, `mogwai.sendMessage`
- Lifecycle hooks — `MOGWAI.onStop` (any clean exit), `MOGWAI.onError` (unhandled error), `MOGWAI.onReboot` (pre-reboot cleanup, see below)
- Structured error codes (`MW.xx`), with a dedicated `MW.5xx` range reserved for hardware-related errors (`MW.500-509` GPIO, `MW.510-519` I2C, etc.)

**Hardware Support**

- GPIO — `gpio.setMode.*` (input, inputPullDown, inputPullUp, output), `gpio.write.high`/`gpio.write.low`, `gpio.read`, `gpio.toggle`, `gpio.close`
- Automatic cleanup of open GPIO pins at the end of every program run, regardless of how it ended (normal completion, error, or `STOP`)
- I2C, SPI, PWM and ADC packages are already referenced and validated for memory footprint — primitive implementations are planned for upcoming releases

**Timers & Events**

- `AFTER` (one-shot) and `EVERY` (recurring) timers, named and independently startable/stoppable (`timer.start`, `timer.stop`, `timer.purge`)
- Event subscription system (`EVENT`, sugared as `onEvent...do` on the desktop side) — hardware events (e.g. GPIO value changes) deliver their data through an automatically-injected `eventData` local variable, shaped as a `MOGRecord`
- `event.fire`/`event.purge` primitives for manually firing or clearing registered events
- `DI`/`EI` primitives for critical sections, protecting user code from being interrupted by pending timer/event callbacks
- All pending timers and interrupt state are reset to a clean state at the start of every program run

**Networking**

- UDP-based device discovery (fixed port `1968`) — devices respond with their name, version and platform details
- Reliable TCP protocol (fixed port `9597`) for remote code execution — length-prefixed JSON messages, single active client
- Automatic disconnection detection via periodic `ALIVE` heartbeat during long-running executions
- Clean recovery on device reboot or unexpected disconnection, with no lingering blocked state on either side

**Production Deployment**

- `mogwai.reboot` device-side primitive — called from within a running MOGWAI NANO script, it triggers the optional `MOGWAI.onReboot` hook for pre-reboot cleanup before actually rebooting
- `nano.reboot`/`nano.halt` remote commands from MOGWAI NANO Studio — force an immediate reboot/halt regardless of any program currently running on the device, bypassing `MOGWAI.onReboot` entirely
- Persistent autorun storage — code saved to flash automatically executes on every boot, managed remotely via `nano.autorun.set`/`nano.autorun.get`/`nano.autorun.purge`

**Cross-Platform**

- Validated on ESP32 and Raspberry Pi Pico W — the exact same compiled `.bin` runs unmodified on both, despite very different underlying architectures (Xtensa LX6 vs Cortex-M0+)

**MOGWAI NANO Studio**

- Desktop companion application built on the desktop MOGWAI engine
- Integrated Terminal.Gui-based code editor with F5-to-run workflow
- Extended primitives, exposed as regular MOGWAI host functions:
  - `nano.connect`, `nano.disconnect`, `nano.isConnected` — connection management. `nano.connect` pushes `true`/`false` depending on success, a deliberate exception to the pattern below: connecting is expected to sometimes fail, so a boolean fits a straightforward feasibility check
  - `nano.scan` — UDP network discovery (fixed 1s duration, with retransmission every 250ms to compensate for broadcast packet loss), returns a list of records (device, version, session, IP, platform, target, OEM, firmware version), deduplicated by IP. The `session` field is a random number generated once at boot, letting you detect a silent device reboot between two scans even without any visible error
  - `nano.select` — runs its own scan and displays the responding devices (platform, IP) for interactive console selection; pushes the selected device's scan record on the stack, or `null` if aborted or nothing responded
  - `nano.run` — desugars and sends a code block for remote execution; unlike `nano.connect`, failure raises a distinct `MW.xx` error (device not connected, unreachable, or busy already running something) rather than returning a boolean — running is expected to normally succeed, so a failure is treated as an incident with a diagnosable cause, not a routine outcome
  - `nano.state`, `nano.isRunning` — query the connected device's current execution state
  - `nano.autorun.set`, `nano.autorun.get`, `nano.autorun.purge` — manage code stored on the device for automatic execution on every boot
  - `nano.halt`, `nano.reboot` — force an immediate halt/reboot on the device, bypassing the `MOGWAI.onReboot` hook. `nano.halt` stops whatever is currently running (whether started via `nano.run` or as a stored autorun program) and returns the device's state from `RUNNING` to `IDLE`, ready for a new `nano.run`
  - `nano.view` — attaches to the currently running program on the device and displays its live console output (`?`/`console.print`, `debug.write`) in real time; exit with `Escape`. Without `nano.view` active, output from a `nano.run` or an autorun program is not displayed at all — `nano.run` itself only waits for confirmation that the program has started, it doesn't wait for it to finish or show anything. Like `nano.run`, failure raises an `MW.xx` error rather than returning a boolean
- Zero-modification compatibility with the existing [MOGWAI VS Code extension](https://github.com/Sydney680928/mogwai) — canonical NANO primitives are declared as no-op stubs on the desktop engine purely so the extension can recognize and highlight them; using them outside of a `nano.run` context on the desktop engine simply raises an "unknown word" error, with no other consequence

### Known Limitations

- No step-by-step debugging on the device runtime (yet)
- Network configuration deployment (`nanoff --networkdeployment`) support on Raspberry Pi Pico W is still being confirmed with the nanoFramework team
- `MogwaiNanoRuntime.WaitResponse` correlates a response to a request by `Function` name only, not by a unique request identifier — if two requests of the *same* `Function` were ever in flight concurrently, the wrong response could be matched to the wrong caller. Not an issue with the current sequential REPL-driven usage, but worth revisiting if concurrent `nano.*` calls are ever introduced.

---

## Links

- **Repository**: https://github.com/Sydney680928/MogwaiNano
- **Related project**: https://github.com/Sydney680928/mogwai
- **Issue Tracker**: https://github.com/Sydney680928/MogwaiNano/issues
- **Releases**: https://github.com/Sydney680928/MogwaiNano/releases

---
