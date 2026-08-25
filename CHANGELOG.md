# Changelog

All notable changes to MOGWAI NANO will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `makeData` — creates a zero-initialized `MOGData` of a given size directly (e.g. `1024 makeData`), without pushing each byte onto the stack first. Building a large buffer via `repeat { 0 } ->data` allocates one `MOGObject` per byte before conversion, which can exhaust memory on lower-RAM devices even though it works fine on more capable ones; `makeData` avoids that transient peak entirely
- `nano.send` — sends an arbitrary string to the connected device: `"TIME=15:45" nano.send`. The device fires a `STUDIO_DID_SEND` event on the currently running program, with the received string available as `eventData` (a plain `MOGString`), letting a long-running program (`forever do { ... }`) react to free-form commands from Studio without needing to be stopped and relaunched. No built-in message format is imposed — parsing the string (e.g. a `NAME=value` convention) is entirely up to the receiving script
- `->vars`, `->safeVars`, `->params` — ported with 100% functional parity from the desktop MOGWAI engine. `->vars` extracts values from a record or the stack straight into matching local variables, with no type checking beyond raising an error if the stack doesn't have enough elements. `->safeVars` does the same but also validates each value's type against a declared record, and is what `to ... with [...] do` uses automatically for typed function parameters. `->params` validates a named-parameter record (with optional default values) against a declared shape, raising an error if a required parameter is missing or mistyped, and silently ignoring extras
- Bitwise operators on `.number` — `&` (AND), `|` (OR), `^` (XOR), `~` (invert), `<<`/`>>` (shift left/right). Note the context-sensitive parsing of `&`: `&A` right before a name is still the existing reference sigil, while `X Y &` after two numbers already on the stack is the bitwise AND primitive — the two are distinguished by position, not a separate symbol. No `.binary`/`B:` type was introduced for this — these operators work directly on regular numbers, which was enough for the intended use cases (e.g. manipulating individual pixels in a display frame buffer) without the overhead of a whole new type family
- `floor`, `mod` — standard floor and modulo, useful alongside the new bitwise operators for address/offset arithmetic (e.g. computing a byte offset and bit position from pixel coordinates)

### Updated

- `mogwai.info` (device-side) and `nano.info` (Studio-side) records now also include a `skills:` key, listing the same skills queryable via `skills`/`hasSkill` — lets you check a device's capabilities from a single info call, without a separate query
- **RPN stack storage**: replaced the `ArrayList`-backed execution stack with a dedicated `MOGStack` class using a plain `MOGObject[]` array with manual growth (doubling capacity as needed, starting small to keep the per-scope memory footprint low). Since every value on the stack is already a `MOGObject` reference, this removes `ArrayList`'s generic overhead entirely with no boxing trade-off — measured as a very significant speedup on stack-heavy operations (e.g. building a large `MOGData` buffer by pushing hundreds of values with `repeat` before converting with `->data`)
- I2C write primitives confirmed working with large multi-byte buffers in a single transaction (not just single bytes or short sequences) — validated by initializing and clearing a full 128x64 OLED display (SSD1306) frame buffer (1024 bytes) in one `i2c.register.write` call

### Fixed

- If MOGWAI NANO Studio was killed abruptly while a program on the device kept sending console/debug output, a new connection attempt would silently hang for up to 30 seconds (the idle timeout) before succeeding — the failed writes on the old, dead connection were detected but never actually signaled anywhere, leaving the device's TCP accept loop stuck on the stale connection. A shared flag now lets a failed write immediately unblock the read loop, so a fresh reconnection succeeds right away instead of waiting out the timeout

### Known Limitations

- Occasional network disconnections between MOGWAI NANO Studio and a device are expected over WiFi — they can originate on either side (device WiFi hiccups, or a transient network interruption on the PC) and the exact interval is not constant. Reconnection is fast and doesn't require restarting Studio.

## [0.2.0] - 2026-08-21

### Added

- Hexadecimal number literals (`0xFF`)
- `nano.info` — remote equivalent of the device-side `mogwai.info`, returning the same `MOGRecord` (system version, IP, device name, platform, session, free memory, target, MOGWAI NANO version, and OEM build details) without needing a `nano.run` round-trip
- `nano.user.connect` — guided connection shortcut combining discovery, interactive selection, and connection in one call: scans for devices, lists the ones that responded for the user to pick from, and connects to the selected one. Pushes `true` on a successful connection, `false` if nothing responded, no device was selected, or the connection failed — the same boolean convention as `nano.connect`
- I2C support — `i2c.open` (name, bus, address), `i2c.close`, `i2c.write`, `i2c.read`, `i2c.register.write`, `i2c.register.read`, `i2c.scan`. Devices are identified by a user-chosen name rather than repeating the bus/address pair on every call, following the same pattern as named timers. `i2c.open` rejects a name that's already in use with a dedicated error rather than silently overwriting it. Validated against real hardware (a DS3231 RTC module) — write, read, register auto-increment, and bus scanning all confirmed working correctly, BCD-encoded values included
- `->bcd`/`bcd->` — convert a number to/from BCD (binary-coded decimal) encoding, commonly used by I2C devices like RTC modules (e.g. `35 ->bcd` pushes `0x35`; `0x35 bcd->` pushes `35`)
- Skills — reusing the same mechanism as the desktop MOGWAI engine, the device now declares `'GPIO'` and `'I2C'` as available skills, queryable with `skills` (returns the full list as a `MOGList`) and `hasSkill` (tests for a specific one, e.g. `if ('I2C' hasSkill) then { ... }`). `mogwai.assertSkill` is not implemented yet
- Flags — named on/off state markers, reusing the same mechanism as the desktop MOGWAI engine: `flag.set`/`flag.clear` to activate/deactivate a named flag, `flag.isSet`/`flag.isClear` to test its state (e.g. `if ('MY_FLAG' flag.isSet) then { ... }`). Volatile — reset on every new program run, not persisted across reboots. Anticipates the future `.mog` library system, where a flag can act as a simple guard against loading the same library twice within a single run

### Updated

- **Naming convention**: primitives that interact directly with the console (user input or display) are now prefixed with `user` for clarity — `nano.select` is renamed `nano.user.select`, and `nano.view` is renamed `nano.user.view`. Primitives that only exchange data with the device, with no console interaction of their own, keep their existing names (`nano.connect`, `nano.scan`, `nano.run`, etc.)
- **Network protocol**: replaced the JSON + Base64 message format with a lightweight delimiter-based one — fields (source, function, parameters) are now joined with a single ASCII Record Separator character (`0x1E`) rather than serialized to JSON and Base64-encoded. This eliminates the allocation overhead of JSON serialization/deserialization plus Base64 encoding/decoding on every network message, which was found to cause heap fragmentation and, under sustained high-frequency traffic, an outright `OutOfMemoryException`. `nanoFramework.Json` remains a device dependency — it's still used to persist local configuration (currently just the device name) to flash — but it's no longer involved in the network protocol itself. **This is a breaking wire protocol change** — a device and MOGWAI NANO Studio must be on matching versions; an old Studio cannot talk to a new device's firmware or vice versa.

### Fixed

- `EvalResult.ToString()` threw a `NullReferenceException` when `Informations` was `null` on certain error paths, which could crash the device's execution dispatcher instead of reporting the original error cleanly
- The outgoing message queue (`TcpServer.EnqueueMessage`/`SenderLoop`) had no upper bound — a program producing console/debug output faster than the network could send it (e.g. a tight `forever` loop with `console.print`) could grow the queue indefinitely and exhaust available memory. The queue is now capped, dropping the oldest pending message to make room for new ones once full

## [0.1.0] - 2026-08-17

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
- System primitives — `mogwai.halt`, `mogwai.memory` (free RAM reporting), `mogwai.reset`, `mogwai.sendMessage`, `mogwai.info` (a `MOGRecord` with system version, IP, device name, platform, session, free memory, target, MOGWAI NANO version, and OEM build details — everything in one call, useful from within an autorun program that has no active Studio connection to query)
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
- Reliable TCP protocol (fixed port `9597`) for remote code execution — length-prefixed, Base64-encoded JSON messages, single active client
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
  - `nano.name`, `nano.name.set` — read or set the connected device's name, persisted on the device and reported as the `name` field in `nano.scan`/`nano.select` results. Defaults to `"MogwaiNanoDevice"`; useful to tell multiple devices apart on the same network
  - `nano.scan` — UDP network discovery (fixed 1s duration, with retransmission every 250ms to compensate for broadcast packet loss), returns a list of records (name, version, session, IP, platform, target, OEM, firmware version), deduplicated by IP. The `session` field is a random number generated once at boot, letting you detect a silent device reboot between two scans even without any visible error
  - `nano.select` — runs its own scan and displays the responding devices (platform, IP) for interactive console selection; pushes the selected device's scan record on the stack, or `null` if aborted or nothing responded
  - `nano.run` — desugars and sends a code block for remote execution; unlike `nano.connect`, failure raises a distinct `MW.xx` error (device not connected, unreachable, or busy already running something) rather than returning a boolean — running is expected to normally succeed, so a failure is treated as an incident with a diagnosable cause, not a routine outcome
  - `nano.state`, `nano.isRunning`, `nano.memory` — query the connected device's current execution state and free RAM (`GC.Run(false)` result, non-blocking)
  - `nano.autorun.set`, `nano.autorun.get`, `nano.autorun.purge` — manage code stored on the device for automatic execution on every boot
  - `nano.halt`, `nano.reboot` — force an immediate halt/reboot on the device, bypassing the `MOGWAI.onReboot` hook. `nano.halt` stops whatever is currently running (whether started via `nano.run` or as a stored autorun program) and returns the device's state from `RUNNING` to `IDLE`, ready for a new `nano.run`
  - `nano.view` — attaches to the currently running program on the device and displays its live console output (`?`/`console.print`, `debug.write`) in real time; exit with `Ctrl+C`. Without `nano.view` active, output from a `nano.run` or an autorun program is not displayed at all — `nano.run` itself only waits for confirmation that the program has started, it doesn't wait for it to finish or show anything. Like `nano.run`, failure raises an `MW.xx` error rather than returning a boolean
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

[Unreleased]: https://github.com/Sydney680928/MogwaiNano/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/Sydney680928/MogwaiNano/releases/tag/v0.2.0
[0.1.0]: https://github.com/Sydney680928/MogwaiNano/releases/tag/v0.1.0
