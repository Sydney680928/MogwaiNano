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
| `->format` | 🔗 | `n "spec" ->format` → `.string` | Converts a number to a string using a .NET **standard** numeric format specifier — nanoFramework only supports `D`/`F`/`G`/`N`/`X` (with an optional precision digit), not custom format strings like `"000"`. E.g. `50 "D3" ->format` → `"050"`. Since `D` and `X` only apply to integers in .NET while `MOGNumber` is `float`-backed, the number is automatically cast to an integer first when the specifier is `D` or `X` (truncating any decimal part); `F`/`G`/`N` work directly on the float value with no cast |
| `->num` | 🔗 | `"str" ->num` → `.number` | Converts a string to a number; raises an error if it isn't a valid one |
| `->str` | 🔗 | `v ->str` → `.string` | Converts an object to a string |
| `sub` | 🔗 | `v start extent sub` | Extracts a part of a `MOGString`, `MOGList`, `MOGData` or `.binary` value by start position and extent. An extent of `0` means "to the end". Also accepts a `MOGRef` (`&variable`) in place of `v`, dereferenced transparently |
| `makeData` | ⚙️ | `size value makeData` → `.data` | Creates a `MOGData` of a given size, filled with a given byte value, without pushing each byte individually — avoids a memory spike compared to building the same buffer with `repeat`/`->data` |

### Evaluation and string interpolation

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `eval` | 🔗 | `v eval` | Evaluates an object, with behavior depending on its type: a `MOGFunction`/`MOGCode` block is executed; a `MOGString` has its `{! ... }` placeholders resolved (see below); a `MOGRecord` or `MOGList` has any dynamic elements it contains replaced by their current value |

**String interpolation:** a `{! ... }` placeholder inside a string is replaced, on `eval`, by the result of evaluating the RPN code inside it — not just a variable name, any expression. `"Le nombre est {! A}" eval` (with `50 -> 'A'` beforehand) produces `"Le nombre est 50"`; `"2x2={! 2 2 *}" eval` produces `"2x2=4"`.

**Auto-eval on records and lists:** a `MOGRecord`/`MOGList` literal prefixed with `!` evaluates its dynamic elements immediately at construction, without a separate `eval` call — `[! x: 10 y: A]` is equivalent to `[x: 10 y: A] eval`, and the same `!` prefix works identically on lists: `(! 1 2 3 A)` is equivalent to `(1 2 3 A) eval`. This is the same `!` marker already used for auto-evaluating code blocks (`{! ... }`), now extended uniformly to all three collection/block types.

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
| `purge` | 🔗 | see below | Removes an element — behavior depends on what's on the stack: `'name' purge` deletes a variable (e.g. `'$A' purge`); `record key: purge` removes a key from a `MOGRecord` (`[x: 10 y: 20] x: purge` → `[y: 20]`); `list index purge` removes an item from a `MOGList` by index (`(1 2 3 4) 1 purge` → `(1 3 4)`); `data index purge` removes a byte from a `MOGData` by index (`D:FF5634 1 purge` → `D:FF34`) |
| `exists` | 🔗 | `'name' exists` → `.boolean` | Tests whether a variable with the given name exists — unlike `purge`, scoped to variables only, no record/list/data variant |

### Function definition

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `DEFUNC` (canonical; write `to 'name' do { ... }`) | 🔗 | `{ ... } 'name' DEFUNC` | Registers a code block as a named user function. Refuses to redefine an already-existing function name rather than silently overwriting it |

### Data access

> **Reference support:** `get`, `set`, and `size` also accept a `MOGRef` (`&variable`) in place of the collection/value operand, dereferenced transparently before the operation runs.

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `get` | 🔗 | `list index get` / `record key: get` | Reads from a `MOGList` (by index) or `MOGRecord` (by key). Sugared as `record->key:` on the desktop MOGWAI engine — this shorthand is desugared into `record key: get` by MOGWAI NANO Studio's parser before anything is sent to the device; the device runtime only ever receives and understands the canonical form |
| `set` | 🔗 | `value list index set` / `value record key: set` | Writes into a `MOGList` (by index) or `MOGRecord` (by key), creating the key if needed. Sugared as `value record<-key:` on the desktop MOGWAI engine, desugared the same way as `->key:` above before reaching the device |
| `size` | 🔗 | `v size` → `.number` | Length of a `MOGList`, `MOGRecord`, `MOGString` or `MOGData` |

---

## 2. Control flow

| Primitive (canonical) | Sugared form | Signature | Description |
|---|---|---|---|
| `IF` | `if (...) then { ... }` | `condition block IF` | Executes `block` if `condition` is true |
| `IFELSE` | `if (...) then { ... } else { ... }` | `condition thenBlock elseBlock IFELSE` | Executes `thenBlock` if true, `elseBlock` if false |
| `WHILE` | `while (...) do { ... }` | `conditionBlock codeBlock WHILE` | Re-executes `conditionBlock` on every pass; stops once it leaves `false` on the stack |
| `REPEAT` | `n repeat { ... }` | `n block REPEAT` | Executes `block` exactly `n` times |
| `FOR` | `start end for 'var' do { ... }` | `start end 'var' block FOR` | Loops from `start` to `end` inclusive (direction auto-detected). The loop variable object is reused and updated in place on every iteration for performance — a reference to it (`&var`) always reflects its *current* value, even after the loop has moved past that point. To keep a snapshot from a specific iteration, copy it explicitly (`var -> 'snapshot'`) |
| `FORSTEP` | `start end for 'var' step N do { ... }` | `start end step 'var' block FORSTEP` | Like `FOR`, with an explicit step (always taken as an absolute value — direction comes from `start`→`end`). Same loop variable reuse semantics as `FOR` |
| `FOREVER` | `forever do { ... }` | `block FOREVER` | Loops indefinitely until `break` |
| `DURING` | `during <ms> do { ... }` | `duration block DURING` | Repeats `block` for the given `duration` (milliseconds), then stops — a time-bounded `FOREVER` rather than a count- or condition-bounded loop. Also supports `break` for early exit |
| `FOREACH` | `foreach 'var' do { ... }` | `collection 'var' block FOREACH` | Iterates a `MOGList` (element by element), a `MOGData` (byte by byte, exposed as `.number`), or a `MOGString` (character by character, exposed as a single-character `.string`) |
| `TRAP` | `trap { ... }` | `block TRAP` | Runs `block`; if an error occurs partway through, execution of the block stops there and continues right after `TRAP` — the stack is automatically restored to its state from before `TRAP` ran, so a failed protected block never leaves stray values behind |
| `GUARD` | `guard { ... } else { ... }` | `tryBlock catchBlock GUARD` | Like `TRAP`, but runs `catchBlock` if `tryBlock` fails, with the same stack restoration guarantee |

All loop forms interrupt cleanly if the executed block returns an error, and support `break` for early exit.

---

## 3. Error handling

| Primitive | Signature | Description |
|---|---|---|
| `error.last` | `error.last` → `.string` | Returns the code of the last error raised (e.g. `"MW.40"`), most useful inside a `GUARD`'s catch block or right after a `TRAP` |
| `error.reset` | `error.reset` | Resets the last-error code back to `"MW.0"` (no error). This doesn't happen automatically — reset it once you're done handling an error |
| `error.throw` | `"MW.xx" error.throw` | Artificially raises the given error code, as if the engine itself had encountered it |

`TRAP`/`GUARD` (above) are the primitives you'll actually reach for to keep a program running past a failure; `error.*` is what you use to find out what went wrong and react accordingly.

---

## 4. Events and timers

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

## 5. Tasks

🔗 **Shared** with the desktop engine, ported with full parity. A *task* runs a block of code on its own real native thread (`System.Threading.Thread` — nanoFramework has no Task Parallel Library, so unlike desktop MOGWAI's `Task.Run`-based implementation, NANO tasks are backed directly by threads), with its own fully isolated `MogwaiNanoEngine` instance (own stack, own variable scope, own everything) — never shared state with the parent or with other tasks. All communication between a task and its parent happens exclusively through the existing event mechanism (above) — there's no other way to share data between them, by design.

Only two states exist, matching the desktop engine exactly: a task is either `Running`, or `Waiting` — and `Waiting` covers both "never started yet" and "finished running", with no distinction between the two. `task.join`/`task.wait` don't error if you wait on a task you forgot to start; they simply return immediately, since an unstarted task already looks "done" from their point of view.

| Primitive (canonical) | Sugared form | Signature | Description |
|---|---|---|---|
| `TASK.DEF` | `task 'name' do { ... }` | `block 'name' TASK.DEF` | Declares a named task. Creates its dedicated engine instance immediately (reused across restarts), but doesn't run anything yet |
| `task.list` | — | `task.list` → `.list` | Returns the names of all declared tasks |
| `task.start` | `'name' task.start` | `'name' task.start` | Starts a task with no launch parameter. Fails if the task is already running |
| `TASK.START` | `task 'name' start with param` | `param 'name' TASK.START` | Starts a task, passing a launch parameter — parsed and pushed onto the task's own stack before its code runs |
| `task.isRunning` | — | `'name' task.isRunning` → `.boolean` | Tests whether a task is currently running |
| `task.stop` | — | `'name' task.stop` | Requests a running task to halt (sets its `HaltRequested` flag — cooperative, same mechanism as `mogwai.halt`, not an immediate kill) |
| `task.purge` | — | `'name' task.purge` | Removes a declared task |
| `task.publish` | — | `message task.publish` | Called from *inside* a running task: sends `message` to the parent, firing `TASK_DID_PUBLISH` there |
| `task.send` | — | `message 'name' task.send` | Called from the parent: sends `message` to a named task, firing `TASK_DID_RECEIVE` inside it |
| `task.setResult` | — | `object task.setResult` | Called from inside a task: records its final result (safely copied across the thread/instance boundary by serializing to string and re-parsing with the parent's own engine, rather than sharing a live object reference) |
| `task.result` | — | `'name' task.result` | Returns a task's recorded result (set via `task.setResult`) |
| `task.name` | — | `task.name` → `.name` | Returns the current task's own name, called from inside it |
| `task.wait` | — | `'name' task.wait` | Blocks until a single named task finishes, pumping the parent's own pending events while waiting (so `onEvent` handlers still fire live during the wait, not just after) |
| `task.join` | — | `(names) task.join` | Blocks until *all* the named tasks finish, same event-pumping behavior as `task.wait` |

**Events fired around a task's lifecycle**, delivered to the parent via the same `onEvent`/`eventData` mechanism as everything else:

| Event | `eventData` | Fired when |
|---|---|---|
| `TASK_DID_START` | the task's name (`.name`) | A task starts running |
| `TASK_DID_END` | a record with `task:` and `result:` | A task finishes successfully |
| `TASK_DID_FAIL` | a record with `task:`, `error:`, `message:` | A task finishes with an error, or fails to even parse |
| `TASK_DID_PUBLISH` | a record with `task:` and the published message | A running task calls `task.publish` |
| `TASK_DID_RECEIVE` | the message sent | Fired *inside* the task itself when the parent calls `task.send` |

**Memory cost, measured:** roughly 7KB for the task's own `MogwaiNanoEngine` instance plus roughly 3KB for its native thread — about 10KB per task. This is why tasks are only practical on an ESP32-S3 with PSRAM (see [Memory considerations](../README.md#memory-considerations)) — on a plain ESP32's ~40KB budget, even a couple of tasks would eat most of the available headroom.

**Example — two LEDs blinking at independent rates, with every lifecycle event logged:**

```
onEvent 'TASK_DID_START' do 
{ 
    "EVENT TASK DID START" ?
    eventData ?
}

onEvent 'TASK_DID_END' do 
{ 
    "EVENT TASK DID END" ?
    eventData ?
}

onEvent 'TASK_DID_PUBLISH' do 
{ 
    "EVENT TASK DID PUBLISH" ?
    eventData ?
}

onEvent 'TASK_DID_FAIL' do
{
    "EVENT TASK DID FAIL" ?
    eventData ?
}

task 'TSK1' do
{
    4 gpio.setMode.output
    4 gpio.write.low

    10 repeat
    {
        4 gpio.toggle
        1000 wait
    }

    4 gpio.write.low
}

task 'TSK2' do
{
    5 gpio.setMode.output
    5 gpio.write.low

    50 repeat
    {
        5 gpio.toggle
        200 wait
    }

    5 gpio.write.low
}

'TSK1' task.start
'TSK2' task.start

('TSK1' 'TSK2') task.join

"PROGRAM ENDED" ?
```

`TSK1` toggles GPIO 4 every second (10 times), `TSK2` toggles GPIO 5 every 200ms (50 times) — both run genuinely in parallel, each on its own thread. `task.join` blocks until both finish, while every `onEvent` handler above still fires live as each task starts, ends, or publishes — not just after `join` returns.

## 6. Stopwatch

⚙️ **NANO-only.** Named timers for measuring elapsed time, following the same by-name management pattern as I2C/PWM/ADC.

| Primitive | Signature | Description |
|---|---|---|
| `stopwatch.create` | `'name' stopwatch.create` | Creates a named stopwatch, initially stopped. Refuses to create one under an already-used name (`MW.41`) |
| `stopwatch.purge` | `'name' stopwatch.purge` | Removes a stopwatch entirely |
| `stopwatch.start` | `'name' stopwatch.start` | Starts (or resumes) timing |
| `stopwatch.stop` | `'name' stopwatch.stop` | Pauses timing, keeping the elapsed time so far |
| `stopwatch.reset` | `'name' stopwatch.reset` | Stops and zeroes the elapsed time |
| `stopwatch.restart` | `'name' stopwatch.restart` | Equivalent to `reset` followed immediately by `start` |
| `stopwatch.isRunning` | `'name' stopwatch.isRunning` → `.boolean` | Tests whether the stopwatch is currently running |
| `stopwatch.elapsed` | `'name' stopwatch.elapsed` → `.number` | Elapsed time in milliseconds |

---

## 7. Skills and flags

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

## 8. Console and debug output

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `?` / `console.println` | 🔗 | `v ?` | Prints the top of stack, with a newline |
| `??` / `console.print` | 🔗 | `v ??` | Prints the top of stack, no newline |
| `debug.write` | ⚙️ | `v debug.write` | Writes a debug message — on a connected NANO device, streamed back to MOGWAI NANO Studio in real time via `nano.user.view` |

All three accept a `MOGRef` (`&variable`) and dereference it automatically before printing.

---

## 9. System (`mogwai.*`)

All ⚙️ **NANO-only** (though most have a conceptual desktop equivalent).

| Primitive | Signature | Description |
|---|---|---|
| `mogwai.halt` | `mogwai.halt` | Stops the current program immediately, raising `MW.2` (`HaltEncounteredError`) — the mechanism by which a script halts itself voluntarily |
| `mogwai.memory` | `forceCollect mogwai.memory` → `.number` | Returns free RAM in bytes. `true` forces a garbage collection before measuring; `false` returns the current figure without forcing one |
| `mogwai.reset` | `mogwai.reset` | Resets engine state (stack, variables, timers, etc.) |
| `mogwai.reboot` | `mogwai.reboot` | Reboots the device. Runs `MOGWAI.onReboot` first if defined (unlike the Studio-side `nano.reboot`, which bypasses it), waits 1 second, then reboots |
| `mogwai.info` | `mogwai.info` → `.record` | Returns a record with `name`, `mogwai` (NANO runtime version), `ip`, `session`, `platform`, `target`, `oem`, `system`, `memory` (free RAM, non-forcing), `skills` (list), `units` (list of stored unit names), and `frugalMode` (current mode) |
| `mogwai.frugalMode` | `enabled mogwai.frugalMode` | Enables (`true`) or disables (`false`) frugal mode for subsequent execution |
| `mogwai.sendMessage` | `"message" mogwai.sendMessage` | Sends an arbitrary string to Studio (device → Studio direction) — the counterpart to the Studio-side `nano.send` (Studio → device) |
| `mogwai.units` | `mogwai.units` → `.list` | Returns the names of all units currently stored on the device, from within a running program |
| `mogwai.units.run` | `'unit' mogwai.units.run` | Executes a stored unit's code — typically used to load the functions it declares (e.g. a RTC helper library) into the current program's context, once, near the start of a script |
| `mogwai.isTask` | `mogwai.isTask` → `.boolean` | Tests whether the currently running program is a task's own engine (started via `task.start`/`TASK.START`) rather than the top-level program |

### Lifecycle hooks

Defined as regular functions with reserved names — called automatically by the engine:

| Hook | Triggered by |
|---|---|
| `MOGWAI.onStop` | Any clean program exit |
| `MOGWAI.onError` | An unhandled error |
| `MOGWAI.onReboot` | `mogwai.reboot` called from within a running script (not triggered by the Studio-side `nano.reboot`, which bypasses it entirely) |

Only the matching hook runs for a given program end — never more than one.

---

## 10. GPIO

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

## 11. I2C

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

## 12. PWM

All ⚙️ **NANO-only.** Channels are identified by a user-chosen name, following the same pattern as I2C. Requires the pin to already be configured for PWM via `device.setPinFunction` (see [ESP32 DeviceFunction values reference](esp32-device-function-values.md)) beforehand.

| Primitive | Signature | Description |
|---|---|---|
| `pwm.open` | `'name' pin frequency dutyCycle pwm.open` | Creates and starts-ready a named PWM channel on `pin`, at `frequency` (Hz, must be greater than `0`) and `dutyCycle` — given as a **percentage** (`0`-`100`), not a fraction. Refuses to reopen an already-used name. Fails cleanly if the pin can't provide a PWM channel |
| `pwm.start` | `'name' pwm.start` | Starts the named channel's output |
| `pwm.stop` | `'name' pwm.stop` | Stops the named channel's output |
| `pwm.close` | `'name' pwm.close` | Stops (if running) and releases the named channel |

**Not yet available:** changing frequency or duty cycle after opening a channel. To change either, close the channel and reopen it with new values. This also sidesteps a platform caveat — some PWM implementations recommend fixing the frequency at creation rather than changing it on a running channel.

**Note on pairs:** PWM channels operate in pairs sharing the same frequency (e.g. `PWM1`/`PWM2`). For two independent frequencies running at once, use channels at least two apart in the `DeviceFunction` numbering — see the [ESP32 DeviceFunction values reference](esp32-device-function-values.md).

---

## 13. SSD1306 OLED display

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

## 14. Device-level platform access

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
| `MW.530`–`MW.539` | PWM |
| `MW.!!!` | Fatal |
