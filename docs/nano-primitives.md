# MOGWAI NANO Runtime — Complete Primitives Reference

This is the complete, exhaustive reference for every primitive available in the **MOGWAI NANO device runtime** — everything you can use in code sent to a device via `nano.run`, or stored via `nano.autorun.set`.

Each primitive is marked with its origin:

- 🔗 **Shared** — identical behavior on the desktop MOGWAI engine and MOGWAI NANO
- ⚙️ **NANO-only** — specific to the embedded runtime, not present on desktop MOGWAI

For Studio-side `nano.*` commands (run from your PC to control a device), see the [Studio Primitives Reference](studio-primitives.md).

> **Reminder:** primitives written in ALL CAPS (`IF`, `WHILE`, `FOR`...) are the private, canonical forms — generated automatically by desugaring. As a developer, you write the sugared form (`if...then...else`, `while...do`, `for...do`...); you never type the canonical form directly. Both are shown here so you can recognize canonical code if you ever inspect it (for example via `nano.autorun.get`).

---

## 1. Language basics

### Arithmetic

> **Reference support:** `+`, `-`, `*`, `/` all accept a `MOGRef` (`&variable`) in place of their main operand — the reference is transparently dereferenced to the variable's actual value before the operation runs, with no difference in the result.

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `+` | 🔗 | `a b +` | Addition (numbers), concatenation (strings), merge (adds an item to a `MOGList`), append a byte (`MOGData`) |
| `-` | 🔗 | `a b -` | Subtraction |
| `*` | 🔗 | `a b *` | Multiplication |
| `/` | 🔗 | `a b /` | Division |
| `floor` | 🔗 | `n floor` | Rounds down to the nearest integer |
| `mod` | 🔗 | `a b mod` → `a mod b` | Modulo |

### Bitwise operators (on `.number`)

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `&` | 🔗 | `a b &` | Bitwise AND. Note the context-sensitive parsing: `&A` right before a name is the reference sigil, while `a b &` after two numbers is bitwise AND — distinguished by position, not a separate symbol |
| `\|` | 🔗 | `a b \|` | Bitwise OR |
| `^` | 🔗 | `a b ^` | Bitwise XOR |
| `~` | 🔗 | `a ~` | Bitwise NOT (invert) |
| `<<` | 🔗 | `value positions <<` | Left shift |
| `>>` | 🔗 | `value positions >>` | Right shift |

### Comparisons and boolean logic

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `==` | 🔗 | `a b ==` | Equality |
| `!=` | 🔗 | `a b !=` | Inequality |
| `<` | 🔗 | `a b <` | Less than |
| `>` | 🔗 | `a b >` | Greater than |
| `<=` | 🔗 | `a b <=` | Less than or equal |
| `>=` | 🔗 | `a b >=` | Greater than or equal |
| `isnull` | 🔗 | `v isnull` | Tests whether the top of stack is null |
| `not` | 🔗 | `b not` | Logical negation |
| `and` | 🔗 | `a b and` | Logical AND |
| `or` | 🔗 | `a b or` | Logical OR |
| `xor` | 🔗 | `a b xor` | Logical XOR |

### Stack manipulation

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `clear` | 🔗 | `clear` | Empties the entire stack |
| `swap` | 🔗 | `swap` | Swaps the top two stack items |
| `dup` | 🔗 | `dup` | Duplicates the top of stack |
| `drop` | 🔗 | `drop` | Removes the top of stack |
| `break` | 🔗 | `break` | Breaks out of the current loop (`for`/`while`/`foreach`/`forever`/`repeat`) |

### Conversions

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `->type` | 🔗 | `v ->type` → `.name` | Returns the type name of the top-of-stack object (e.g. `.number`, `.string`) |
| `->data` | 🔗 | `v1 v2 ... vN N ->data` → `.data` | Pops `N` numbers (each `0`-`255`) and builds a `MOGData` from them, in push order |
| `->bcd` | ⚙️ | `n ->bcd` | Converts a decimal number to BCD encoding (e.g. `35 ->bcd` → `0x35`) |
| `bcd->` | ⚙️ | `n bcd->` | Converts a BCD-encoded number back to decimal (e.g. `0x35 bcd->` → `35`) |
| `->format` | 🔗 | `n "spec" ->format` → `.string` | Converts a number to a string using a .NET **standard** numeric format specifier — nanoFramework only supports `D`/`F`/`G`/`N`/`X` (with an optional precision digit), not custom format strings like `"000"`. E.g. `50 "D3" ->format` → `"050"` |
| `->num` | 🔗 | `"str" ->num` → `.number` | Converts a string to a number; raises an error if it isn't a valid one |
| `sub` | 🔗 | `v start extent sub` | Extracts a part of a `MOGString`, `MOGList`, `MOGData` or `.binary` value by start position and extent. An extent of `0` means "to the end". Also accepts a `MOGRef` (`&variable`) in place of `v`, dereferenced transparently |
| `makeData` | ⚙️ | `size value makeData` → `.data` | Creates a `MOGData` of a given size, filled with a given byte value, without pushing each byte individually — avoids a memory spike compared to building the same buffer with `repeat`/`->data` |

### Variable extraction

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `->vars` | 🔗 | `record ->vars` or `(names) ->vars` | Extracts values from a record (one local variable per key) or from the stack (given a list of names) into matching local variables. No type checking |
| `->safeVars` | 🔗 | `record shape ->safeVars` | Same as `->vars`, but validates each value's type against a declared shape — what `to ... with [...] do` uses automatically for typed parameters |
| `->params` | 🔗 | `values shape ->params` | Validates a named-parameter record against a declared shape, with optional default values — raises an error if a required parameter is missing or mistyped, silently ignores extras |

### Storage

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `STO` (canonical; write `->`) | 🔗 | `value -> 'name'` | Stores a value into a variable |

### Function definition

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `DEFUNC` (canonical; write `to 'name' do { ... }`) | 🔗 | `{ ... } 'name' DEFUNC` | Registers a code block as a named user function. Refuses to redefine an already-existing function name rather than silently overwriting it |

### Data access

> **Reference support:** `get`, `set`, and `size` also accept a `MOGRef` (`&variable`) in place of the collection/value operand, dereferenced transparently before the operation runs.

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `get` | 🔗 | `list index get` / `record key: get` | Reads from a `MOGList` (by index) or `MOGRecord` (by key) |
| `set` | 🔗 | `value list index set` / `value record key: set` | Writes into a `MOGList` (by index) or `MOGRecord` (by key), creating the key if needed |
| `size` | 🔗 | `v size` → `.number` | Length of a `MOGList`, `MOGRecord`, `MOGString` or `MOGData` |

---

## 2. Control flow

| Primitive (canonical) | Sugared form | Signature | Description |
|---|---|---|---|
| `IF` | `if (...) then { ... }` | `condition block IF` | Executes `block` if `condition` is true |
| `IFELSE` | `if (...) then { ... } else { ... }` | `condition thenBlock elseBlock IFELSE` | Executes `thenBlock` if true, `elseBlock` if false |
| `WHILE` | `while (...) do { ... }` | `conditionBlock codeBlock WHILE` | Re-executes `conditionBlock` on every pass; stops once it leaves `false` on the stack |
| `REPEAT` | (no common sugared form) | `n block REPEAT` | Executes `block` exactly `n` times |
| `FOR` | `for (start end) 'var' do { ... }` | `start end 'var' block FOR` | Loops from `start` to `end` inclusive (direction auto-detected). The loop variable object is reused and updated in place on every iteration for performance — a reference to it (`&var`) always reflects its *current* value, even after the loop has moved past that point. To keep a snapshot from a specific iteration, copy it explicitly (`var -> 'snapshot'`) |
| `FORSTEP` | `for (start end step) 'var' do { ... }` | `start end step 'var' block FORSTEP` | Like `FOR`, with an explicit step (always taken as an absolute value — direction comes from `start`→`end`). Same loop variable reuse semantics as `FOR` |
| `FOREVER` | `forever do { ... }` | `block FOREVER` | Loops indefinitely until `break` |
| `FOREACH` | `foreach 'var' do { ... }` | `collection 'var' block FOREACH` | Iterates a `MOGList` (element by element), a `MOGData` (byte by byte, exposed as `.number`), or a `MOGString` (character by character, exposed as a single-character `.string`) |

All loop forms interrupt cleanly if the executed block returns an error, and support `break` for early exit.

---

## 3. Events and timers

| Primitive (canonical) | Sugared form | Signature | Description |
|---|---|---|---|
| `EVENT` | `onEvent 'name' do { ... }` | `block 'name' EVENT` | Registers a handler for a named event. Refuses to redefine an already-existing event name |
| `event.fire` | — | `data 'name' event.fire` | Manually fires a named event with arbitrary data (or `null`) |
| `event.purge` | — | `'name' event.purge` | Removes a registered event handler |
| `AFTER` | `timer 'name' after <ms> do { ... }` | `block interval 'name' AFTER` | Creates a one-shot timer |
| `EVERY` | `timer 'name' every <ms> do { ... }` | `block interval 'name' EVERY` | Creates a recurring timer |
| `timer.start` | — | `'name' timer.start` | Starts a previously created timer |
| `timer.stop` | — | `'name' timer.stop` | Stops a timer without removing it |
| `timer.purge` | — | `'name' timer.purge` | Removes a timer entirely |

All of the above are 🔗 **Shared** with the desktop engine.

**How event data reaches your handler:** when an event fires, its data is injected as a local variable called `eventData`, automatically available inside the handler block — no special syntax needed to receive it.

**Robustness:** `AFTER`/`EVERY` refuse to create a timer with a name that's already in use (same protection as `EVENT`) rather than silently replacing it — call `timer.purge` first if you need to recreate one under the same name. A negative interval is also rejected.

**Hardware events (NANO-only):** `GPIO_PIN_CHANGED` follows the same event mechanism, firing with a `MOGRecord` containing `pin` (the pin number) and `eventType` (`1` = rising edge, `0` = falling edge).

---

## 4. Skills and flags

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `skills` | 🔗 | `skills` → `.list` | Returns the full list of declared skills as a `MOGList` of names |
| `hasSkill` | 🔗 | `'name' hasSkill` → `.boolean` | Tests whether a named skill is present. Comparison is case-insensitive |
| `flag.set` | 🔗 | `'name' flag.set` | Activates a named flag |
| `flag.clear` | 🔗 | `'name' flag.clear` | Deactivates a named flag |
| `flag.isSet` | 🔗 | `'name' flag.isSet` → `.boolean` | Tests whether a flag is active |
| `flag.isClear` | 🔗 | `'name' flag.isClear` → `.boolean` | Tests whether a flag is inactive |

**NANO-declared skills:** `'GPIO'`, `'I2C'`, `'SSD1306'` — reflecting which hardware subsystems are available on the running firmware.

**Flags are volatile on NANO** — reset on every new program run, not persisted across reboots.

---

## 5. Console and debug output

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `?` / `console.println` | 🔗 | `v ?` | Prints the top of stack, with a newline |
| `??` / `console.print` | 🔗 | `v ??` | Prints the top of stack, no newline |
| `debug.write` | ⚙️ | `v debug.write` | Writes a debug message — on a connected NANO device, streamed back to MOGWAI NANO Studio in real time via `nano.user.view` |

All three accept a `MOGRef` (`&variable`) and dereference it automatically before printing.

---

## 6. System (`mogwai.*`)

All ⚙️ **NANO-only** (though most have a conceptual desktop equivalent).

| Primitive | Signature | Description |
|---|---|---|
| `mogwai.halt` | `mogwai.halt` | Stops the current program immediately, raising `MW.2` (`HaltEncounteredError`) — the mechanism by which a script halts itself voluntarily |
| `mogwai.memory` | `forceCollect mogwai.memory` → `.number` | Returns free RAM in bytes. `true` forces a garbage collection before measuring; `false` returns the current figure without forcing one |
| `mogwai.reset` | `mogwai.reset` | Resets engine state (stack, variables, timers, etc.) |
| `mogwai.reboot` | `mogwai.reboot` | Reboots the device. Runs `MOGWAI.onReboot` first if defined (unlike the Studio-side `nano.reboot`, which bypasses it), waits 1 second, then reboots |
| `mogwai.info` | `mogwai.info` → `.record` | Returns a record with `name`, `mogwai` (NANO runtime version), `ip`, `session`, `platform`, `target`, `oem`, `system`, `memory` (free RAM, non-forcing), `skills` (list), and `frugalMode` (current mode) |
| `mogwai.frugalMode` | `enabled mogwai.frugalMode` | Enables (`true`) or disables (`false`) frugal mode for subsequent execution |
| `mogwai.sendMessage` | `"message" mogwai.sendMessage` | Sends an arbitrary string to Studio (device → Studio direction) — the counterpart to the Studio-side `nano.send` (Studio → device) |

### Lifecycle hooks

Defined as regular functions with reserved names — called automatically by the engine:

| Hook | Triggered by |
|---|---|
| `MOGWAI.onStop` | Any clean program exit |
| `MOGWAI.onError` | An unhandled error |
| `MOGWAI.onReboot` | `mogwai.reboot` called from within a running script (not triggered by the Studio-side `nano.reboot`, which bypasses it entirely) |

Only the matching hook runs for a given program end — never more than one.

---

## 7. GPIO

All ⚙️ **NANO-only.** Every primitive takes a **pin number** (`.number`), not a name.

| Primitive | Signature | Description |
|---|---|---|
| `gpio.setMode.input` | `pin gpio.setMode.input` | Configures a pin as a plain input |
| `gpio.setMode.inputPullDown` | `pin gpio.setMode.inputPullDown` | Input with pull-down resistor |
| `gpio.setMode.inputPullUp` | `pin gpio.setMode.inputPullUp` | Input with pull-up resistor |
| `gpio.setMode.output` | `pin gpio.setMode.output` | Configures a pin as an output |
| `gpio.write.high` | `pin gpio.write.high` | Sets the pin high |
| `gpio.write.low` | `pin gpio.write.low` | Sets the pin low |
| `gpio.read` | `pin gpio.read` → `.number` | Reads the pin's state (`1` = high, `0` = low) |
| `gpio.toggle` | `pin gpio.toggle` | Inverts the pin's current state |
| `gpio.close` | `pin gpio.close` | Closes the pin, unsubscribes its `GPIO_PIN_CHANGED` event, and releases the hardware resource |

**Notes:**
- Calling `gpio.setMode.*` on an already-open pin just changes its mode, without closing/reopening it
- Every opened pin is automatically subscribed to value-change notifications, which is what makes `onEvent 'GPIO_PIN_CHANGED'` work without any extra setup
- **Automatic cleanup**: any pin still open at the end of a program is automatically closed and unsubscribed, regardless of how the program ended (normal completion, error, or halt)

---

## 8. I2C

All ⚙️ **NANO-only.** Devices are identified by a user-chosen name rather than repeating the bus/address pair on every call.

| Primitive | Signature | Description |
|---|---|---|
| `i2c.open` | `'name' bus address i2c.open` | Opens a named I2C device. `bus` must be `1` or `2`, `address` between `0` and `127`. Refuses to reopen an already-used name |
| `i2c.close` | `'name' i2c.close` | Closes the device and releases the resource |
| `i2c.write` | `'name' data i2c.write` (or `&data`) | Writes a `MOGData` buffer to the device. The buffer can be passed by value or by reference (`&data`) — reference support here specifically avoids copying a large, frequently-updated buffer (like a display frame buffer) on every call |
| `i2c.register.write` | `'name' register data i2c.register.write` (or `&data`) | Writes to a specific register (`0`-`255`; the control byte is prefixed automatically). The data buffer can be passed by value or by reference (`&data`), for the same reason as `i2c.write` |
| `i2c.read` | `'name' count i2c.read` → `.data` | Reads `count` raw bytes |
| `i2c.register.read` | `'name' register count i2c.register.read` → `.data` | Reads `count` bytes from a specific register (via a combined write-then-read transaction, respecting the repeated-start requirement many I2C devices rely on) |
| `i2c.scan` | `bus i2c.scan` → `.list` | Probes addresses `0x08`-`0x77` on the given bus, returns the list of ones that responded |

---

## 9. SSD1306 OLED display

All ⚙️ **NANO-only** — a native, non-RPN primitive family wrapping the `nanoFramework.Iot.Device.Ssd13xx` binding, built after dense per-pixel drawing in pure RPN proved impractically slow. Fixed to 128x64 resolution over I2C Fast Mode; only one display instance is supported at a time (no naming).

| Primitive | Signature | Description |
|---|---|---|
| `ssd1306.init` | `bus address ssd1306.init` | Initializes the display. `bus` must be `1` or `2`, `address` between `0` and `127`. Refuses to initialize twice; clears the screen automatically |
| `ssd1306.close` | `ssd1306.close` | Releases the display |
| `ssd1306.clear` | `ssd1306.clear` | Clears the screen |
| `ssd1306.printString` | `x y "text" size center ssd1306.printString` | Writes text using **character-grid coordinates** (like a text console — `x=0 y=1` means the start of the second text line) |
| `ssd1306.drawString` | `x y "text" size center ssd1306.drawString` | Writes text using **pixel coordinates** for precise, free-form placement |
| `ssd1306.refresh` | `ssd1306.refresh` | Pushes the in-memory frame buffer to the physical screen — nothing drawn is visible until this is called |
| `ssd1306.drawPixel` | `x y on ssd1306.drawPixel` | Sets or clears a single pixel |
| `ssd1306.drawHorizontalLine` | `x y length on ssd1306.drawHorizontalLine` | Draws a horizontal line |
| `ssd1306.drawVerticalLine` | `x y length on ssd1306.drawVerticalLine` | Draws a vertical line |
| `ssd1306.drawRectangle` | `x y width height on ssd1306.drawRectangle` | Draws a rectangle outline (hand-composed from four line calls — no dedicated method in the underlying binding) |
| `ssd1306.drawFilledRectangle` | `x y width height on ssd1306.drawFilledRectangle` | Draws a filled rectangle |
| `ssd1306.drawBitmap` | `x y width height data size ssd1306.drawBitmap` | Draws a raw `MOGData` buffer as a 1-bit-per-pixel image |

`printString`/`drawString`/`drawPixel`/lines/rectangles/`drawBitmap` all only update the in-memory buffer — call `ssd1306.refresh` to actually update the physical display, so several drawing calls can be batched before paying for one screen update.

---

## 10. Device-level platform access

⚙️ **NANO-only.**

| Primitive | Signature | Description |
|---|---|---|
| `device.setPinFunction` | `pin function device.setPinFunction` | Dynamically reassigns a pin's function — e.g. designating I2C clock/data pins on a board where the default I2C bus isn't pre-wired (like `ESP32_S3_OCTAL`). `function` is a raw numeric value from the platform's own function enum (e.g. `nanoFramework.Hardware.Esp32`'s `DeviceFunction` — `131328`/`131329` for `I2C1_DATA`/`I2C1_CLOCK`), not a MOGWAI NANO abstraction — see the [ESP32 DeviceFunction values reference](esp32-device-function-values.md) for the complete list (SPI, I2C, serial, PWM, ADC, I2S, SDMMC). Detects the running platform at runtime and only invokes the platform-specific API when it matches, returning a clean error otherwise — this keeps the `.bin` universal across platforms rather than requiring a separate build per target. Currently implemented for ESP32 only; other platforms return a clean "unsupported" error |

---

## Error codes

MOGWAI uses structured `MW.xx` error codes.

| Range | Category |
|---|---|
| `MW.0`–`MW.9` | Execution flow (e.g. `MW.2` = halt encountered) |
| `MW.10`–`MW.24` | Argument/stack errors |
| `MW.30`–`MW.32` | Math/conversion |
| `MW.40`–`MW.50` | Name/word resolution |
| `MW.500`–`MW.509` | GPIO |
| `MW.510`–`MW.519` | I2C |
| `MW.520`–`MW.529` | SSD1306 |
| `MW.!!!` | Fatal |
