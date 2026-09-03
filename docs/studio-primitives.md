# MOGWAI NANO Studio Primitives Reference

These are the extended primitives exposed by **MOGWAI NANO Studio** — the desktop companion application. They're regular MOGWAI host functions, available anywhere in the desktop MOGWAI engine when run from Studio, and let you discover, connect to, and orchestrate a MOGWAI NANO device from your PC.

This page only covers Studio's own `nano.*` primitives. For everything that actually runs *on* the device (sent via `nano.run`) — canonical language, hardware primitives (`gpio.*`, `i2c.*`, `ssd1306.*`), timers, events — see the [NANO Primitives Reference](nano-primitives.md).

> **Naming convention:** primitives that interact directly with the console (user input or on-screen display) are prefixed with `user` — `nano.user.select`, `nano.user.connect`, `nano.user.view`. Primitives that only exchange data with the device, with no console interaction of their own, don't carry that prefix (`nano.connect`, `nano.scan`, `nano.run`, etc.).

## Connection management

| Primitive | Signature | Description |
|---|---|---|
| `nano.connect` | `"ip" nano.connect` → `.boolean` | Connects to a device at a known IP address. Pushes `true`/`false` depending on success — a deliberate exception to the pattern used elsewhere in this reference: connecting is expected to sometimes fail, so a boolean fits a straightforward feasibility check |
| `nano.disconnect` | `nano.disconnect` | Disconnects from the currently connected device |
| `nano.isConnected` | `nano.isConnected` → `.boolean` | Tests whether a device is currently connected |
| `nano.user.connect` | `nano.user.connect` → `.boolean` | Guided connection shortcut combining discovery, interactive selection, and connection in one call: scans for devices, lists the ones that responded for the user to pick from, and connects to the selected one. Pushes `true` on a successful connection, `false` if nothing responded, no device was selected, or the connection failed — same boolean convention as `nano.connect` |

## Units (reusable code libraries)

A *unit* is a named piece of MOGWAI NANO code, stored permanently on the device's flash, that gets parsed and executed once on demand to declare functions — a library, in effect (e.g. a set of RTC helper functions). A unit is a `MOGFunction` under the hood, so it goes through the exact same execution machinery as any other code — no separate caching or `frugalMode` behavior to think about. It's meant to declare functions once, not to be run repeatedly as regular logic.

| Primitive | Signature | Description |
|---|---|---|
| `nano.units.install` | `"file" nano.units.install` → `.boolean` | Parses a local `.mog` file and sends its canonical form to the device for permanent storage (under `I:\mogwai\units` on the device's flash) — no limit on how many units can be stored. The unit's name is the source file's name (e.g. `C:\folder\code.mog` installs as unit `code.mog`). Pushes `false` if the file can't be found, can't be opened, or its content fails to parse into a canonical form; `true` otherwise. Useful for a script that installs whatever units a program needs (if not already present) before running it |
| `nano.units.purge` | `'unit' nano.units.purge` → `.boolean` | Removes a stored unit. Pushes `true` on success, `false` otherwise |
| `nano.units` | `nano.units` → `.list` | Returns the names of all units currently stored on the device |

## Discovery

| Primitive | Signature | Description |
|---|---|---|
| `nano.scan` | `nano.scan` → `.list` | UDP network discovery (fixed 1s duration, with retransmission every 250ms to compensate for broadcast packet loss). Returns a list of records (`name`, `version`, `session`, `ip`, `platform`, `target`, `oem`, firmware version), deduplicated by IP. The `session` field is a random number generated once at boot, letting you detect a silent device reboot between two scans even without any visible error |
| `nano.user.select` | `nano.user.select` → `.record` \| `.null` | Runs its own scan and displays the responding devices (name, platform, IP) for interactive console selection. Pushes the selected device's scan record on the stack, or `null` if aborted or nothing responded |

## Running code

| Primitive | Signature | Description |
|---|---|---|
| `nano.run` | `{ ... } nano.run` | Desugars and sends a code block for remote execution on the connected device. Unlike `nano.connect`, failure raises a distinct `MW.xx` error (device not connected, unreachable, or busy running something else) rather than returning a boolean — running is expected to normally succeed, so a failure is treated as a diagnosable incident, not a routine outcome. `nano.run` only waits for confirmation that the program has *started* — it doesn't wait for it to finish or show any output |
| `nano.user.view` | `nano.user.view` | Attaches to the program currently running on the device and streams its live console output (`?`/`console.print`, `debug.write`) in real time. Exit with `Ctrl+C`. Without `nano.user.view` active, output from `nano.run` or an autorun program isn't displayed anywhere. Like `nano.run`, failure raises an `MW.xx` error rather than returning a boolean. Give it a moment to fully attach before the program starts printing — a short `wait` at the top of the sent code avoids missing the very first lines |
| `nano.halt` | `nano.halt` | Forces an immediate halt on the device, bypassing the `MOGWAI.onReboot` hook. Stops whatever is currently running (started via `nano.run` or as a stored autorun program) and returns the device's state from `RUNNING` to `IDLE`, ready for a new `nano.run` |
| `nano.reboot` | `nano.reboot` | Forces an immediate reboot on the device, bypassing `MOGWAI.onReboot` entirely — unlike the device-side `mogwai.reboot`, which runs that hook first |
| `nano.send` | `"string" nano.send` | Sends an arbitrary string to the connected device: `"TIME=15:45" nano.send`. The device fires a `STUDIO_DID_SEND` event on the currently running program, with the received string available as `eventData` (a plain `MOGString`), letting a long-running program (`forever do { ... }`) react to free-form commands from Studio without needing to be stopped and relaunched. No built-in message format is imposed — parsing the string (e.g. a `NAME=value` convention) is entirely up to the receiving script |

## Device state and info

| Primitive | Signature | Description |
|---|---|---|
| `nano.state` | `nano.state` → `.name` | Queries the connected device's current execution state (`IDLE`/`RUNNING`) |
| `nano.isRunning` | `nano.isRunning` → `.boolean` | Tests whether a program is currently running on the device |
| `nano.memory` | `nano.memory` → `.number` | Free RAM on the device, in bytes (`GC.Run(false)` result — non-blocking, doesn't force a collection) |
| `nano.info` | `nano.info` → `.record` | Remote equivalent of the device-side `mogwai.info`, returning the same record (system version, IP, device name, platform, session, free memory, target, MOGWAI NANO version, OEM build details, a `skills:` list, and a `units:` list of stored unit names) without needing a `nano.run` round-trip |
| `nano.lastResult` | `nano.lastResult` → `.string` | Returns the full result message from the last program run on the device — whichever way it ended, successfully or not. On a program that fails without producing any output of its own, this is the way to find out why: it reports the underlying error rather than leaving you with a silent failure |
| `nano.session` | `nano.session` → `.string` | Returns the connected device's session identifier directly, as a string — the same value found in the `session` field of `nano.scan`/`nano.info` results, without needing a full scan or info call |
| `nano.name` | `nano.name` → `.string` | Reads the connected device's name |
| `nano.name.set` | `"name" nano.name.set` | Sets the connected device's name, persisted on the device and reported as the `name` field in future `nano.scan`/`nano.user.select` results. Defaults to `"MogwaiNanoDevice"` — useful for telling multiple devices apart on the same network |

## Autorun management

| Primitive | Signature | Description |
|---|---|---|
| `nano.autorun.set` | `{ ... } nano.autorun.set` | Stores code on the device's flash to run automatically on every boot. Only stores it — the code doesn't start running immediately, use `nano.reboot` for that |
| `nano.autorun.get` | `nano.autorun.get` → `.code` | Returns the code currently stored for autorun, as a `MOGCode` block |
| `nano.autorun.purge` | `nano.autorun.purge` | Clears the stored autorun code |

## Notes for scripting against these primitives

- **Zero-modification VS Code compatibility.** Canonical NANO primitives (`gpio.*`, `i2c.*`, `ssd1306.*`, etc.) are declared as no-op stubs on the desktop MOGWAI engine, purely so the [MOGWAI VS Code extension](https://github.com/Sydney680928/mogwai) recognizes and syntax-highlights them. Using them outside of a `nano.run` context on the desktop engine simply raises an "unknown word" error, with no other consequence — they only do something real once sent to and executed on an actual device.
- **`WaitResponse` correlates by function name, not request ID.** If two requests of the *same* function were ever in flight concurrently, the wrong response could be matched to the wrong caller. Not an issue with the current sequential, one-command-at-a-time usage pattern, but worth keeping in mind if concurrent `nano.*` calls are ever introduced into a script.
