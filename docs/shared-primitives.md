# Shared Primitives Reference

MOGWAI NANO's canonical RPN language is a strict subset of the desktop MOGWAI language — every primitive listed here behaves identically whether the code runs on your PC (in MOGWAI or MOGWAI NANO Studio) or on a device via `nano.run`.

This page only covers what's **common** to both. For NANO-specific primitives (`gpio.*`, `i2c.*`, `timer.*`, event handling, `nano.*` host functions in MOGWAI NANO Studio), see the [CHANGELOG](../CHANGELOG.md) and the [Getting Started guide](getting-started.md).

> **Reminder:** the forms below written in ALL CAPS (`IF`, `WHILE`, `FOR`...) are the private, canonical primitives — generated automatically by desugaring. As a developer, you write the sugared form (`if...then...else`, `while...do`, `for...do`...); you never type the canonical form directly. Both are shown here so you can recognize canonical code if you ever inspect it (for example via `nano.autorun.get`).

## Arithmetic

| Primitive | Description |
|---|---|
| `+` | Addition (numbers), concatenation (strings), merge (`MOGData`) |
| `-` | Subtraction |
| `*` | Multiplication |
| `/` | Division |

## Comparisons

| Primitive | Description |
|---|---|
| `==` | Equality |
| `!=` | Inequality |
| `<` | Less than |
| `>` | Greater than |
| `<=` | Less than or equal |
| `>=` | Greater than or equal |
| `isnull` | Tests whether the top of stack is null |

## Boolean logic

| Primitive | Description |
|---|---|
| `and` | Logical AND |
| `or` | Logical OR |
| `xor` | Logical XOR |
| `not` | Logical negation |

## Stack manipulation

| Primitive | Description |
|---|---|
| `dup` | Duplicate the top of stack |
| `swap` | Swap the top two stack items |
| `drop` | Remove the top of stack |
| `clear` | Empty the entire stack |
| `break` | Break out of the current loop |

## Variables and data access

| Primitive | Description |
|---|---|
| `STO` (canonical; write `->`) | Store a value into a variable — write `50 -> 'A'`, not `50 'A' STO` |
| `get` | Read from a `MOGList` (by index) or `MOGRecord` (by key) |
| `set` | Write into a `MOGList` (by index) or `MOGRecord` (by key), creating the key if needed |
| `size` | Length of a `MOGList`, `MOGRecord`, `MOGString` or `MOGData` |
| `->type` | Returns the type name of the top-of-stack object |
| `->data` | Converts the top-of-stack object to `MOGData` (raw bytes) |
| `&` | Reference sigil — pushes a direct reference to a variable's object instead of a copy. Significantly reduces allocations and memory fragmentation in long-running loops |

## Function parameters and local variables

| Primitive | Description |
|---|---|
| `->vars` | Extracts values from a record or from the stack straight into matching local variables. From a record, one local variable is created per key. From the stack, pass a list of names (e.g. `('a' 'b' 'c') ->vars`) and that many values are popped and assigned. No type checking — raises an error if the stack doesn't have enough elements, otherwise a no-op-safe convenience |
| `->safeVars` | Same as `->vars`, but also validates each value's type against a declared record (e.g. `[a: .number b: .list c: .boolean] ->safeVars`) — raises an error immediately if a value doesn't match. This is what `to ... with [...] do` uses automatically to type-check a function's parameters |
| `->params` | Validates a **named-parameter** record against a declared shape (names, types, and optional default values), creating matching local variables when everything checks out — e.g. `[nom: "STEPHANE" age: 55] [nom: .string age: .number] ->params`. Raises an error if a required parameter is missing or mistyped; extra parameters are silently ignored. Default values are declared as `(.number 0)` style pairs inside the shape record |

## Control flow (canonical / sugared)

| Canonical primitive | Sugared form you actually write |
|---|---|
| `IF` | `if (condition) then { ... }` |
| `IFELSE` | `if (condition) then { ... } else { ... }` |
| `WHILE` | `while (condition) do { ... }` |
| `REPEAT` | (see MOGWAI language docs) |
| `FOR` | `for (start end) 'var' do { ... }` |
| `FORSTEP` | `for (start end step) 'var' do { ... }` |
| `FOREVER` | `forever do { ... }` |
| `FOREACH` | `foreach 'item' do { ... }` (iterates a `MOGList`) |
| `EVENT` | `onEvent 'NAME' do { ... }` |
| `AFTER` | `timer 'name' after <ms> do { ... }` |
| `EVERY` | `timer 'name' every <ms> do { ... }` |

## Functions

| Primitive | Description |
|---|---|
| `DEFUNC` | Canonical form of function definition — written as `to 'name' do { ... }` |

User-defined functions get their own local variable scope. Function names live in a namespace separate from variables — attempting to reuse a function's name as a variable (or vice versa) raises a dedicated error (`MW.43`) rather than silently overwriting it.

## Timers

| Primitive | Description |
|---|---|
| `timer.start` | Starts a named timer previously defined with `after`/`every` |
| `timer.stop` | Stops a named timer |
| `timer.purge` | Removes a named timer entirely |

Timer names are plain `MOGName` values, not reserved words — they can be stored in variables and computed dynamically, just like any other data.

## Events

| Primitive | Description |
|---|---|
| `event.fire` | Manually fires a named event |
| `event.purge` | Removes a named event subscription |

When a subscribed event fires, its data is delivered through an automatically-injected local variable called `eventData`, shaped as a `MOGRecord` — the exact keys depend on the event source (for example, GPIO events expose `pin` and `eventType`).

## Critical sections

| Primitive | Description |
|---|---|
| `DI` | Disable interrupts — pending timer/event callbacks are queued but not delivered |
| `EI` | Re-enable interrupts — queued callbacks are delivered |

`DI`/`EI` calls are reentrant (counter-based), so nested critical sections are safe. Every program run starts with interrupts enabled, regardless of the state left by a previous run.

## Skills

A *skill* is a name declared by the host application embedding MOGWAI, identifying a capability available in that specific execution context — for example, MOGWAI NANO declares `'GPIO'` and `'I2C'` once those subsystems are available on a device.

| Primitive | Description |
|---|---|
| `skills` | Returns the full list of declared skills as a `MOGList`, e.g. `skills ?` → `('GPIO' 'I2C')` |
| `hasSkill` | Tests whether a named skill is present, returns a boolean — `if ('I2C' hasSkill) then { ... }` |
| `mogwai.assertSkill` | Checks for a skill and stops execution with an error message (`MW.9`, calling `MOGWAI.onError` if defined) if it's absent — the recommended way to declare a script's prerequisites: `'I2C' "This script requires I2C support." mogwai.assertSkill` |

The current skills are also available via the `skills:` key of the `mogwai.info` (device-side) / `nano.info` (Studio-side) record.

## Flags

Named on/off state markers — a flag has a name and is either activated or deactivated.

| Primitive | Description |
|---|---|
| `flag.set` | Activates a named flag — `'MY_FLAG' flag.set` |
| `flag.clear` | Deactivates a named flag — `'MY_FLAG' flag.clear` |
| `flag.isSet` | Returns `true` if the named flag is activated — `if ('MY_FLAG' flag.isSet) then { ... }` |
| `flag.isClear` | Returns `true` if the named flag is deactivated |

On NANO, flags are volatile — reset on every new program run, not persisted across reboots.

## Console and debug output

| Primitive | Description |
|---|---|
| `?` / `console.print` | Print the top of stack |
| `debug.write` | Write a debug message — on a connected NANO device, this is streamed back to MOGWAI NANO Studio in real time |

## System primitives

| Primitive | Description |
|---|---|
| `mogwai.halt` | Halt the current program |
| `mogwai.reboot` | Reboot the device — triggers `MOGWAI.onReboot` first if defined |
| `mogwai.memory` | Reports free RAM (device-side, most meaningful on NANO) |
| `mogwai.reset` | Resets engine state |
| `mogwai.sendMessage` | Sends a message on the runtime's messaging channel |

## Lifecycle hooks

Defined as regular functions with reserved names — the engine calls them automatically when the corresponding event occurs:

| Hook | Triggered by |
|---|---|
| `MOGWAI.onStop` | Any clean program exit |
| `MOGWAI.onError` | An unhandled error — has access only to `error.last` |
| `MOGWAI.onReboot` | `mogwai.reboot` called from within a running script (not triggered by a remote `nano.reboot`/`nano.halt`, which bypass it entirely) |

Only the matching hook runs for a given program end — never more than one.

## Types

| Type | Type literal | Description |
|---|---|---|
| `MOGNumber` | `.number` | Numeric value. Can be written in decimal or hexadecimal (`0xFF`). On NANO, backed by `float` rather than `double`, to take advantage of the ESP32's hardware FPU |
| `MOGString` | `.string` | Text, delimited with `"..."` |
| `MOGBoolean` | `.boolean` | `true`/`false` |
| `MOGName` | `.name` | An identifier/name value, delimited with `'...'` — used for variable names, function names, timer names |
| `MOGList` | `.list` | Ordered collection, delimited with `(...)` |
| `MOGRecord` | `.record` | Key/value collection, delimited with `[...]` |
| `MOGKey` | `.key` | A record key, written as `name:` |
| `MOGCode` | `.code` | A deferred, unevaluated block, delimited with `{...}` |
| `MOGFunction` | `.function` | A user-defined function created via `DEFUNC` |
| `MOGData` | `.data` | Raw byte buffer, written with the `D:` hex literal syntax (e.g. `D:FF23AB`) — note: this syntax changed from `DATA:...` in MOGWAI v7 and earlier to `D:...` from v8 onward |
| `MOGRef` | `.ref` | A reference to a variable's object rather than a copy, written with the `&` sigil (e.g. `&A`) |
| `MOGNull` | `.null` | Null value |

The `->type` primitive returns one of these dot-prefixed literals, so you can compare a value's type directly: `if (something ->type .record ==) then { ... }`.

> `.objref` (class instance references) is desktop-only and not part of NANO's canonical language. `.binary`/`B:` binary number literals aren't supported yet either, but are planned soon — binary masks and register-level bit manipulation come up often in embedded work.

## Error codes

MOGWAI uses structured `MW.xx` error codes. NANO reuses the same core ranges and adds a dedicated range for hardware-related errors:

| Range | Category |
|---|---|
| `MW.0`–`MW.9` | Execution flow |
| `MW.10`–`MW.24` | Argument/stack errors |
| `MW.30`–`MW.32` | Math/conversion |
| `MW.40`–`MW.50` | Name/word resolution |
| `MW.500`–`MW.509` | GPIO (NANO only) |
| `MW.510`–`MW.519` | I2C (NANO only) |
| `MW.!!!` | Fatal |
