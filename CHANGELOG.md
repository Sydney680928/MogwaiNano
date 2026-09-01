# Changelog

All notable changes to MOGWAI NANO will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `sub`, `->format`, `->num` — ported from the desktop MOGWAI engine. `sub` extracts a part of a `MOGString`, `MOGList`, `MOGData` or `.binary` value by start position and extent (an extent of `0` means "to the end"), useful for parsing fixed-format messages received via `nano.send`. `->format` converts a number to a string using a .NET standard numeric format specifier (e.g. `50 "D3" ->format` → `"050"`) — nanoFramework only supports standard specifiers (`D`/`F`/`G`/`N`/`X`), not the custom format strings (`"000"`, `"000.000"`) available on desktop. `->num` converts a string to a number, raising an error if it isn't a valid one
- `nano.session` — returns the connected device's session identifier directly, as a string, without needing a full `nano.scan`/`nano.info` call
- `nano.lastResult` — returns the full result message from the last program run on the device, however it ended. On a program that fails without producing any output of its own, this is the way to find out why — it reports the underlying error rather than leaving a silent failure
- `device.setPinFunction` — dynamically reassigns a pin's function (e.g. designating I2C clock/data pins on a board where the default I2C bus isn't pre-wired, like `ESP32_S3_OCTAL`): `pin function device.setPinFunction`. `function` is a raw numeric value from the platform's own function enum (e.g. `nanoFramework.Hardware.Esp32`'s `DeviceFunction` — `131328`/`131329` for `I2C1_DATA`/`I2C1_CLOCK`), not a MOGWAI NANO abstraction — look up the right value in the platform package's source for the pin function you need. Detects the running platform at runtime via `SystemInfo.Platform` and only invokes the platform-specific API when running on a matching platform, returning a clean error otherwise — this keeps the `.bin` universal across platforms rather than requiring a separate build per target, since the platform-specific package is referenced but its methods are only ever called after confirming a match. Currently only implemented for ESP32; other platforms return the same clean "unsupported" error until support is added
- Two new documentation references: [NANO primitives reference](docs/nano-primitives.md) — a complete, exhaustive reference for every primitive in the device runtime, marking which are shared with desktop MOGWAI and which are NANO-only — and [Studio primitives reference](docs/studio-primitives.md), covering the full `nano.*` command set exposed by MOGWAI NANO Studio

### Updated

- `sub` handled an out-of-range `start` inconsistently depending on the input type: `MOGList`/`MOGData` validated it upfront with a clean error, while `MOGString` let the calculation run and relied on a `try/catch` around `Substring()` to turn the resulting exception into an error. All three types now validate `start` upfront the same way, for consistent behavior and a cleaner error path

### Fixed

- **Memory fragmentation on plain ESP32 boards (~40KB free RAM) is a real, measured constraint** — long-running programs combining several subsystems (a display, I2C sensors, sustained network activity) can hit allocation failures well before total free memory looks exhausted, even after forcing a garbage collection. This isn't a bug to fix — it's confirmed to be a fundamental characteristic of this platform tier. Side-by-side testing confirmed an ESP32-S3 board with PSRAM (several MB instead of tens of KB) runs the exact same composite workload for hours with no instability, no script changes required. See the [README's Memory considerations section](../README.md#memory-considerations) for the full picture and hardware recommendation.
- `->data` still accessed the execution stack's old internal `ArrayList` field directly to validate each value's type before popping — a leftover from before the stack was migrated to the `MOGStack` class (see 0.3.0's stack storage change), which no longer exposes that field. It now uses the same `StackSign` mechanism as every other primitive, and validates each value's `0`-`255` range as it pops rather than only checking its type upfront

## [0.3.0] - 2026-08-28

### Added

- `makeData` — creates a `MOGData` of a given size, filled with a given byte value (e.g. `1024 0 makeData` for 1024 zero bytes, `1023 0xFF makeData` for 1023 bytes all set to `0xFF`), without pushing each byte onto the stack first. Building a large buffer via `repeat { 0 } ->data` allocates one `MOGObject` per byte before conversion, which can exhaust memory on lower-RAM devices even though it works fine on more capable ones; `makeData` avoids that transient peak entirely
- `nano.send` — sends an arbitrary string to the connected device: `"TIME=15:45" nano.send`. The device fires a `STUDIO_DID_SEND` event on the currently running program, with the received string available as `eventData` (a plain `MOGString`), letting a long-running program (`forever do { ... }`) react to free-form commands from Studio without needing to be stopped and relaunched. No built-in message format is imposed — parsing the string (e.g. a `NAME=value` convention) is entirely up to the receiving script
- `->vars`, `->safeVars`, `->params` — ported with 100% functional parity from the desktop MOGWAI engine. `->vars` extracts values from a record or the stack straight into matching local variables, with no type checking beyond raising an error if the stack doesn't have enough elements. `->safeVars` does the same but also validates each value's type against a declared record, and is what `to ... with [...] do` uses automatically for typed function parameters. `->params` validates a named-parameter record (with optional default values) against a declared shape, raising an error if a required parameter is missing or mistyped, and silently ignoring extras
- Bitwise operators on `.number` — `&` (AND), `|` (OR), `^` (XOR), `~` (invert), `<<`/`>>` (shift left/right). Note the context-sensitive parsing of `&`: `&A` right before a name is still the existing reference sigil, while `X Y &` after two numbers already on the stack is the bitwise AND primitive — the two are distinguished by position, not a separate symbol. No `.binary`/`B:` type was introduced for this — these operators work directly on regular numbers, which was enough for the intended use cases (e.g. manipulating individual pixels in a display frame buffer) without the overhead of a whole new type family
- `floor`, `mod` — standard floor and modulo, useful alongside the new bitwise operators for address/offset arithmetic (e.g. computing a byte offset and bit position from pixel coordinates)
- `Engine.Idle()` — a cooperative yield point called from `MOGCode.Execute()` on every executed item (both inside and outside loops), calling `Thread.Sleep(0)` every 10 iterations. The nanoCLR schedules threads cooperatively rather than preemptively (see the [nanoFramework thread execution docs](https://docs.nanoframework.net/content/architecture/thread-execution.html)) — a tight loop that never yields can starve other threads of CPU time, including the device's own network thread. This was observed concretely: two empty nested loops running for more than ~10 seconds would prevent the TCP thread from ever getting a chance to run, eventually causing MOGWAI NANO Studio to lose the connection even though the device itself was working correctly the whole time
- **SSD1306 OLED display support** — a dedicated, native (non-RPN) primitive family wrapping the `nanoFramework.Iot.Device.Ssd13xx` binding, added after measuring that dense per-pixel drawing in pure RPN (deep-cloning/re-parsing overhead per function call) was multiple orders of magnitude too slow for practical use. Fixed to 128x64 resolution over I2C Fast Mode for now — MOGWAI NANO officially supports this specific display type rather than exposing a generic OLED abstraction. Devices are managed as a single global instance (no name-based multi-display support yet), covering: `ssd1306.init` (bus, address), `ssd1306.close`, `ssd1306.clear`, `ssd1306.printString`/`ssd1306.drawString` (x, y, text, size, center) — `printString` uses character-grid coordinates (like a text console, `x=0 y=1` meaning the start of the second text line), while `drawString` uses pixel coordinates for precise, free-form placement, `ssd1306.refresh`, `ssd1306.drawPixel`, `ssd1306.drawHorizontalLine`/`ssd1306.drawVerticalLine`, `ssd1306.drawRectangle` (outline, hand-composed from four calls to the horizontal/vertical line primitives since the underlying binding has no dedicated rectangle-outline method), `ssd1306.drawFilledRectangle`, and `ssd1306.drawBitmap` (drawing a raw `MOGData` buffer as a 1-bit-per-pixel image). A dedicated `MW.52x` error range covers display-specific failures (already open, not open, initialization failure, general operation failure)
- The device now declares `'SSD1306'` as an additional skill alongside `'GPIO'` and `'I2C'`, queryable the same way (`'SSD1306' hasSkill`)
- The new `ssd1306.*` primitives are recognized and syntax-highlighted by the [MOGWAI VS Code extension](https://github.com/Sydney680928/mogwai), following the same zero-modification stub pattern as the other NANO-specific primitives
- **Lazy parsing / `frugalMode`** — a major memory management addition, refined over the course of extensive testing into a clean, unified mechanism. Previously, running a script parsed its *entire* source into a full object tree upfront, and every executed block was deep-cloned (recursively) before evaluation — necessary to protect against in-place mutation (e.g. a list built with `+`/`AddItem` inside a loop must start fresh on every iteration, or `3 { (1 2 3) 4 + ? } REPEAT` would print a list that keeps growing across iterations instead of the same `(1 2 3 4)` three times). On a device with only ~54KB of RAM, this meant a script with several function definitions could consume tens of kilobytes just to parse and clone, well before running any real logic — a script of only ~3KB of source text was measured consuming over 23KB during parsing alone.

  `MOGCode`/`MOGFunction` now support two modes, toggled with `true`/`false` `mogwai.frugalMode`, switching takes effect on the very next execution of any block regardless of which mode it was originally parsed under:
  - **cool** (the default) — parse a block once on first use, keep the resulting object tree cached, and protect against mutation between repeated executions (loop iterations, repeated calls) via deep-cloning. Fast, but memory cost is proportional to total program size and never recovered until the block itself is discarded.
  - **frugal** — parse a block lazily on first use, discard the parsed objects immediately after each execution, and protect against mutation by simply *re-parsing from source* on the next call rather than cloning — the untouched source text guarantees a fresh, uncorrupted object tree every time. Flat, stable memory footprint regardless of program size or loop iteration count, at the cost of repeating the parsing work on every single call.

  Measured on a real 128x64 OLED display test (nested loops drawing 121 pixels, each via a function call): **11s in cool mode vs 23s in frugal mode, in a Release build** — roughly a 2x speed trade-off for a flat memory profile. (Note: this gap nearly disappears in a Debug build, where cloning and re-parsing end up costing about the same — always benchmark this trade-off in Release.) Cool mode combined with lazy parsing (parsing only what's actually invoked, rather than upfront) already recovers most of the original memory problem for typical scripts; frugal mode remains the right choice for very long-running programs with many repeated calls on memory-constrained devices.

### Updated

- `mogwai.info` (device-side) and `nano.info` (Studio-side) records now also include a `skills:` key, listing the same skills queryable via `skills`/`hasSkill` — lets you check a device's capabilities from a single info call, without a separate query
- **RPN stack storage**: replaced the `ArrayList`-backed execution stack with a dedicated `MOGStack` class using a plain `MOGObject[]` array with manual growth (doubling capacity as needed, starting small to keep the per-scope memory footprint low). Since every value on the stack is already a `MOGObject` reference, this removes `ArrayList`'s generic overhead entirely with no boxing trade-off — measured as a very significant speedup on stack-heavy operations (e.g. building a large `MOGData` buffer by pushing hundreds of values with `repeat` before converting with `->data`)
- I2C write primitives confirmed working with large multi-byte buffers in a single transaction (not just single bytes or short sequences) — validated by initializing and clearing a full 128x64 OLED display (SSD1306) frame buffer (1024 bytes) in one `i2c.register.write` call
- I2C write primitives now accept a `MOGData` buffer by reference (`&myBuffer`) rather than only by value, avoiding an unnecessary copy of the buffer on every call — most useful for a large, frequently-updated buffer like a display frame buffer
- `FOR`/`FORSTEP` no longer allocate a new `MOGNumber` for the loop counter on every iteration — the same object is now reused and its value updated in place. **This changes loop variable semantics slightly**: a reference to the loop variable (`&i`) always reflects its current value, even after the loop has moved on — code that needs to preserve a snapshot of the value from a specific iteration (e.g. collecting values into a list) must explicitly copy it (`i -> 'snapshot'`) rather than storing a reference to the loop variable itself
- The outgoing message queue is now split into a priority lane (`PROGRAM.DID.START`, `PROGRAM.DID.STOP`, `STATE.GET`, `PONG`, and other control messages) and the regular capped lane (`console.print`/`debug.write` output). The priority lane is never capped and is always drained first, so a program producing a lot of console output can no longer delay or push out a control message that MOGWAI NANO Studio is actively waiting on

### Fixed

- If MOGWAI NANO Studio was killed abruptly while a program on the device kept sending console/debug output, a new connection attempt would silently hang for up to 30 seconds (the idle timeout) before succeeding — the failed writes on the old, dead connection were detected but never actually signaled anywhere, leaving the device's TCP accept loop stuck on the stale connection. A shared flag now lets a failed write immediately unblock the read loop, so a fresh reconnection succeeds right away instead of waiting out the timeout
- Three more occurrences of the static-initialization-order issue already described for `EvalResult.NoError`/the `Error` class (see below) were found and fixed: `EvalResult.Error` itself (via a C# 9 `init` accessor combined with a field initializer — replaced with a plain constructor-assigned property), and `MogwaiNanoEngine.LastResult`/`LastError` (both `{ get; set; } = ...` property initializers referencing another class's static member — replaced with the same lazy-initialization pattern). Any of these being `null` at the wrong moment could crash the device's execution dispatcher with a hard-to-diagnose `CLR_E_WRONG_TYPE`/`NullReferenceException` pair
- `Error.FatalError` was declared but never actually registered in `Error`'s lazy initialization — a missing line meant it silently stayed `null` forever. Since it's used by `MOGCode`'s catch-all safety net for unexpected exceptions during script execution, this masked the real underlying error message every time that safety net triggered, itself crashing on the same `null`-related symptom instead of reporting what actually went wrong
- Fixed the operand order in `mod`: `y 8 mod` was computing `8 mod y` instead of `y mod 8`, which happened to go unnoticed on small test values before producing an out-of-range result on a real calculation (`30 mod 8` returning `8`, an impossible modulo-8 result)
- `MOGData.Clone()` didn't actually copy its underlying byte array — it shared the same array reference with the original, so mutating one through `set` would silently corrupt the other. Now copies the array on clone
- I2C register writes were briefly split into two separate `WriteByte`/`Write` calls (as a memory optimization, to avoid allocating a combined buffer on every call) — this broke the repeated-start requirement some I2C devices rely on to treat the register address and the following data as a single logical write, causing an OLED display (SSD1306) to silently accept every command over I2C without ever actually updating its output. Reverted to a single combined write; lazy parsing/`frugalMode` (see above) turned out to be the right way to address the original memory concern instead
- `FOR` was missing a `return` on its early argument-count failure path — if the stack didn't have enough elements, it constructed an error result but then fell straight through to the type-check line anyway, indexing into an already-empty array and risking a lower-level crash instead of cleanly reporting the error
- MOGWAI NANO Studio was treating an ordinary TCP read timeout (used to periodically re-check the connection, no message received in the configured window) as a hard disconnection — reported as `Unable to read data from the transport connection`. A program that stays quiet for a while (heavy computation with no console output in between) would trip this every time the timeout elapsed, even though the connection and the device were both fine. A plain read timeout is no longer treated as a disconnect

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

[Unreleased]: https://github.com/Sydney680928/MogwaiNano/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/Sydney680928/MogwaiNano/releases/tag/v0.3.0
[0.2.0]: https://github.com/Sydney680928/MogwaiNano/releases/tag/v0.2.0
[0.1.0]: https://github.com/Sydney680928/MogwaiNano/releases/tag/v0.1.0
