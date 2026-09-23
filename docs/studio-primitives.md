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
| `nano.user.connect` | `nano.user.connect` → `.boolean` | Guided connection shortcut combining discovery, interactive selection, and connection in one call: scans for devices, lists the ones that responded (with their name, firmware version, IP, and platform) for the user to pick from, and connects to the selected one. Pushes `true` on a successful connection, `false` if nothing responded, no device was selected, or the connection failed — same boolean convention as `nano.connect` |
| `nano.user.connect?` | `nano.user.connect?` → `.boolean` | Same as `nano.user.connect`, but skips discovery and selection entirely if a device is already connected — pushes `true` immediately in that case, with no scan and no prompt. Otherwise behaves exactly like `nano.user.connect`, including on failure. Lets a script safely lead with this without worrying whether it's already connected: `if (nano.user.connect?) then { { ... } nano.run }` |

## Units (reusable code libraries)

A *unit* is a named piece of MOGWAI NANO code, stored permanently on the device's flash, that gets parsed and executed once on demand to declare functions — a library, in effect (e.g. a set of RTC helper functions). A unit is a `MOGFunction` under the hood, so it goes through the exact same execution machinery as any other code — no separate caching or `frugalMode` behavior to think about. It's meant to declare functions once, not to be run repeatedly as regular logic.

| Primitive | Signature | Description |
|---|---|---|
| `nano.units.install` | `"file" nano.units.install` → `.boolean` | Parses a local `.mog` file and sends its canonical form to the device for permanent storage (under `I:\mogwai\units` on the device's flash) — no limit on how many units can be stored. The unit's name is the source file's name (e.g. `C:\folder\code.mog` installs as unit `code.mog`). Pushes `false` if the file can't be found, can't be opened, or its content fails to parse into a canonical form; `true` otherwise. Useful for a script that installs whatever units a program needs (if not already present) before running it |
| `nano.units.purge` | `'unit' nano.units.purge` → `.boolean` | Removes a stored unit. Pushes `true` on success, `false` otherwise |
| `nano.units` | `nano.units` → `.list` | Returns the names of all units currently stored on the device |

## Usings (dynamically loaded libraries)

**MOGWAI NANO Studio GUI only.** A *using* is a compiled plugin library — unlike a unit (source code, parsed and run on demand), a using ships as pre-compiled `.pe` files, loaded into the running CLR via the device-side `mogwai.using` primitive. Installing a using only places its files on flash; it doesn't load it — a script still needs to call `mogwai.using` for its primitives to become usable, and once loaded, a using can never be unloaded (only a reboot clears it — see `mogwai.using` in the [NANO Primitives Reference](nano-primitives.md)).

| Primitive | Signature | Description |
|---|---|---|
| `nano.usings.install` | `"name" "folder" nano.usings.install` → `.boolean` | Copies every file from a local folder (a `manifest.txt` plus the using's `.pe` files) onto the device's flash, under `I:\mogwai\usings\<name>` — ready for a script to `mogwai.using` it. Pushes `true`/`false` rather than raising an error, so it fits directly into scripting logic (`if (... nano.usings.install) then { ... }`) without a separate failure case to handle |
| `nano.usings.purge` | `'name' nano.usings.purge` → `.boolean` | Removes an installed using from the device's flash. Since a loaded using can never be unloaded from memory, purging one already in use has no effect until the next reboot — it only prevents a *future* `mogwai.using` on that name from succeeding. Same `true`/`false` convention as `nano.usings.install` |
| `nano.usings` | `nano.usings` → `.list` | Returns the names of all usings currently *installed* on the device's flash — not the same as *loaded*: an installed using only becomes active once `mogwai.using` is called on it |

## File & directory management

**MOGWAI NANO Studio GUI only.** General-purpose access to the device's internal flash storage (`I:\`) — used internally by `nano.usings.install`/`nano.usings.purge`, but just as usable directly for any other purpose.

| Primitive | Signature | Description |
|---|---|---|
| `nano.dir.list` | `"path" nano.dir.list` → `.list` | Returns the names of the subdirectories directly inside `path` (e.g. `"I:\mogwai" nano.dir.list` → `("usings" "units")`) |
| `nano.dir.create` | `"path" nano.dir.create` | Creates a directory, including any missing parent directories along the way |
| `nano.dir.purge` | `"path" nano.dir.purge` | Removes a directory |
| `nano.file.list` | `"path" nano.file.list` → `.list` | Returns the names of the files directly inside `path` |
| `nano.file.copy` | `"localPath" "devicePath" nano.file.copy` | Copies a file from the PC's local disk to the connected device's flash storage — the same underlying mechanism `nano.units.install` and `nano.usings.install` build on |
| `nano.file.purge` | `"path" nano.file.purge` | Removes a file |

## Discovery

| Primitive | Signature | Description |
|---|---|---|
| `nano.scan` | `nano.scan` → `.list` | UDP network discovery (fixed 1s duration, with retransmission every 250ms to compensate for broadcast packet loss). Returns a list of records (`name`, `version`, `session`, `ip`, `platform`, `target`, `oem`, firmware version), deduplicated by IP. The `session` field is a random number generated once at boot, letting you detect a silent device reboot between two scans even without any visible error |
| `nano.user.select` | `nano.user.select` → `.record` \| `.null` | Runs its own scan and displays the responding devices (name, firmware version, IP, platform) for interactive console selection. Pushes the selected device's scan record on the stack, or `null` if aborted or nothing responded |

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
| `nano.info` | `nano.info` → `.record` | Remote equivalent of the device-side `mogwai.info`, returning the same record (system version, IP, device name, platform, session, free memory, target, MOGWAI NANO version, OEM build details, a `skills:` list, a `units:` list of stored unit names, a `usings:` list of library names currently installed on flash — not necessarily loaded, see `mogwai.using` — and a `primitives:` list of every primitive name currently usable at that exact instant) without needing a `nano.run` round-trip |
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
