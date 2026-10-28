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

> **Reference support:** `+`, `-`, `*`, `/` all accept a `MOGRef` (`&variable`) in place of their main operand. The variable's value is placed on the stack without being copied, the primitive does its work on it, and since the operand was a reference the result is not left on the stack: it is already in the variable. `&counter 8 +` therefore adds 8 to `counter` in place, leaving nothing on the stack.

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
| `==` | 🔗 | `a b ==` | Equality. Also works on two `MOGName` values (e.g. `'foo' 'foo' ==`), comparing them by name text — previously raised a type error |
| `!=` | 🔗 | `a b !=` | Inequality. Same `MOGName` support as `==` |
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
| `->utf8` | 🔗 | `data ->utf8` → `.string` | Converts a `MOGData` buffer to a string, decoding its bytes as UTF-8 |
| `utf8->` | 🔗 | `"str" utf8->` → `.data` | Converts a string to a `MOGData` buffer, encoding it as UTF-8 |
| `->upper` | 🔗 | `"str" ->upper` → `.string` | Converts a string to upper case |
| `->lower` | 🔗 | `"str" ->lower` → `.string` | Converts a string to lower case |
| `->hex` | ⚙️ | `n size ->hex` → `.string` | Converts a number to an upper-case hexadecimal string of `size` digits, padded with leading zeros — equivalent to the `X` format specifier with a precision: `255 4 ->hex` → `"00FF"`. The desktop MOGWAI engine also has a `->hex`, but it takes no size (`255 ->hex` → `"FF"`): the NANO version is the more flexible of the two |
| `sub` | 🔗 | `v start extent sub` | Extracts a part of a `MOGString`, `MOGList`, `MOGData` or `.binary` value by start position and extent. An extent of `0` means "to the end". Also accepts a `MOGRef` (`&variable`) in place of `v`, dereferenced transparently |
| `makeData` | ⚙️ | `size value makeData` → `.data` | Creates a `MOGData` of a given size, filled with a given byte value, without pushing each byte individually — avoids a memory spike compared to building the same buffer with `repeat`/`->data` |

### Evaluation and string interpolation

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `eval` | 🔗 | `v eval` | Evaluates an object, with behavior depending on its type: a `MOGFunction`/`MOGCode` block is executed; a `MOGString` has its `{! ... }` placeholders resolved (see below); a `MOGRecord` or `MOGList` has any dynamic elements it contains replaced by their current value |

**String interpolation:** a `{! ... }` placeholder inside a string is replaced, on `eval`, by the result of evaluating the RPN code inside it — not just a variable name, any expression. `"Le nombre est {! A}" eval` (with `50 -> 'A'` beforehand) produces `"Le nombre est 50"`; `"2x2={! 2 2 *}" eval` produces `"2x2=4"`.

**Auto-eval on records and lists:** a `MOGRecord`/`MOGList` literal prefixed with `!` evaluates its dynamic elements immediately at construction, without a separate `eval` call — `[! x: 10 y: A]` is equivalent to `[x: 10 y: A] eval`, and the same `!` prefix works identically on lists: `(! 1 2 3 A)` is equivalent to `(1 2 3 A) eval`. This is the same `!` marker already used for auto-evaluating code blocks (`{! ... }`), now extended uniformly to all three collection/block types.

**Escape sequences:** on `eval`, a `MOGString` also has its backslash escape sequences resolved: `\r`, `\n`, `\t`, `\b`, `\f`, `\v`, `\a`, `\0`, `\\` and `\"` — `"Bonjour \"MOGWAI\"" eval` produces `Bonjour "MOGWAI"`. This is the one place NANO's `eval` doesn't quite match the desktop engine: the desktop reports a malformed escape sequence as a proper error; nanoFramework has no tuple type to carry that same two-part result, so NANO's `eval` falls back to returning the string exactly as given — backslashes and all — rather than raising an error. This applies to a stray trailing `\` (nothing left to escape) as well as to any unrecognized escape character; either one leaves the *entire* string untouched, not just the offending part.

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
| `keys` | 🔗 | `record keys` → `.list` | Returns the keys of a `MOGRecord` as a `MOGList` — useful for iterating over a record whose keys aren't known in advance |
| `size` | 🔗 | `v size` → `.number` | Length of a `MOGList`, `MOGRecord`, `MOGString` or `MOGData` |

---

## 2. Control flow

| Primitive (canonical) | Sugared form | Signature | Description |
|---|---|---|---|
| `IF` | `if (...) then { ... }` | `condition block IF` | Executes `block` if `condition` is true |
| `IFELSE` | `if (...) then { ... } else { ... }` | `condition thenBlock elseBlock IFELSE` | Executes `thenBlock` if true, `elseBlock` if false |
| `SWITCH` | `switch { (test) then { ... } (test) then { ... } ... }` | `{ testBlock1 codeBlock1 testBlock2 codeBlock2 ... } SWITCH` | A single block alternating test/code pairs. Runs the code block belonging to the *first* test that returns true, and only that one. If none match, nothing happens — a final `(true) then { ... }` is the common way to write a default/catch-all branch |
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
| `POST` | `post { ... }` | `block POST` | Posts a block of code to the execution queue: it runs in the next scheduler cycle, after pending events and timers, without creating an intermediate timer |
| `LATER` | `after <ms> do { ... }` | `block interval LATER` | Runs a block once after a delay in milliseconds, without a timer name |
| `timer.start` | — | `'name' timer.start` | Starts a previously created timer |
| `timer.stop` | — | `'name' timer.stop` | Stops a timer without removing it |
| `timer.purge` | — | `'name' timer.purge` | Removes a timer entirely |

All of the above are 🔗 **Shared** with the desktop engine.

**How event data reaches your handler:** when an event fires, its data is injected as a local variable called `eventData`, automatically available inside the handler block — no special syntax needed to receive it.

**Robustness:** `AFTER`/`EVERY` refuse to create a timer with a name that's already in use (same protection as `EVENT`) rather than silently replacing it — call `timer.purge` first if you need to recreate one under the same name. A negative interval is also rejected.

**`POST` and `LATER`:** `post { ... }` posts a block to the engine's execution queue. The block executes in the next scheduler cycle, after pending events and timers — without creating an intermediate timer. `post { }` with an empty block is valid, and lets the scheduler process pending events without executing any additional code. It is functionally equivalent to `after 0 do { }`, with clearer intent. The main use case is deferred execution from an event handler, so that pending events are processed before a long computation. `after <ms> do { ... }` (canonical `LATER`) runs a block once after a delay in milliseconds, without naming a timer, unlike `timer 'name' after <ms> do { ... }`.

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
| `TASK_DID_ABORT` | the task's name (`.name`) | A task had to be force-killed — see below |

**Ending a program with tasks still running.** When a program ends (whether it reaches its natural end, or is stopped via `mogwai.halt`/`mogwai.exit`/an error), every task it declared is asked to stop cooperatively first (the same mechanism as `task.stop` — setting a flag the task's own code checks between operations), then the engine waits up to **10 seconds** for all of them to actually finish. If any task still hasn't stopped by then — most likely because it's stuck in a long-running native operation that never checks back in — it's forcibly killed, firing `TASK_DID_ABORT` with its name. This is a genuine last resort, not a routine occurrence: force-killing a task can leave whatever hardware operation it was in the middle of (an I2C transaction, a GPIO left mid-toggle) in an inconsistent state, so a task that triggers this is worth investigating rather than treating as normal.

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

## 6. Stopwatch and date/time

### Stopwatch

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

### Date and time

Primitives to read and set the device's date and time. `now`, `date->` and `->date` are 🔗 **Shared** with the desktop MOGWAI engine and work the same way; `setDateTime` is ⚙️ **NANO-only**. A point in time is represented as a **number of ticks** — a plain `.number`, so it can be stored, compared and subtracted like any other.

A `.number` carries both a `float` and a `double` value. Most primitives use the `float` one, which suits the ESP32's hardware FPU; **every time primitive uses the `double` one**, which has the precision a number of ticks requires. Nothing has to be done in a script for this: the choice is made by the primitive being called.

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `now` | 🔗 | `now` → `.number` | Pushes the current date and time, as a number of ticks |
| `date->` | 🔗 | `ticks date->` → `.record` | Converts a number of ticks (such as the value returned by `now`) into a record with the keys `year:`, `month:`, `day:`, `hour:`, `minute:`, `second:`, `dayOfWeek:` and `dayOfYear:` |
| `->date` | 🔗 | `record ->date` → `.number` | The opposite of `date->`: converts a record in that same format into a number of ticks |
| `setDateTime` | ⚙️ | `record setDateTime` | Sets the device's date and time from a record. The keys `year:`, `month:` and `day:` are required; `hour:`, `minute:` and `second:` are optional and are `0` when missing |

```
[ year: 2026 month: 10 day: 27 hour: 14 minute: 30 ] setDateTime

now date-> ?d          # the current date and time, one key per line
now date-> ->date ?    # back to a number of ticks
```

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

**NANO-declared skills:** `'GPIO'`, `'I2C'`, `'SSD1306'`, plus `'BLE-P'`, `'ONBOARD-LED'`, `'TFT'` and `'TOUCH'` on ESP32 — reflecting which hardware subsystems are available on the running firmware.

**Flags are volatile on NANO** — reset on every new program run, not persisted across reboots.

---

## 8. Console and debug output

| Primitive | Origin | Signature | Description |
|---|---|---|---|
| `?` / `console.println` | 🔗 | `v ?` | Prints the top of stack, with a newline |
| `??` / `console.print` | 🔗 | `v ??` | Prints the top of stack, no newline |
| `?d` | 🔗 | `v ?d` | Displays an object in a clear, detailed form that depends on its type. Already available in the desktop MOGWAI engine, where it behaves the same way — see the [Shared Primitives Reference](shared-primitives.md) for the output format |
| `debug.write` | ⚙️ | `v debug.write` | Writes a debug message — on a connected NANO device, streamed back to MOGWAI NANO Studio in real time |
| `debug.vs.write` | ⚙️ | `v debug.vs.write` | Like `debug.write`, but writes through `Debug.WriteLine` — into the debugger's output window (Visual Studio) — instead of over the network channel to a connected Studio. Meant for testing the runtime, or a plugin, in isolation, from a test project with no Studio connected |
| `console.clear` | ⚙️ | `console.clear` | Sends `CONSOLE.CLEAR` to a connected Studio, clearing its Console NANO output |
| `debug.clear` | ⚙️ | `debug.clear` | Sends `DEBUG.CLEAR` to a connected Studio, clearing its Debug NANO output |
| `debug.halt` / `¤` | 🔗 | `debug.halt` | Debug mode only: requests a pause before the next instruction that can be paused on — a breakpoint. `¤` is a shorter way of writing `debug.halt`. No effect outside debug mode. Resuming is done from Studio, or from the script itself with `debug.resume` — see [Debugging](#debugging-a-program) below |
| `debug.resume` | ⚙️ | `debug.resume` | Debug mode only: leaves the paused / step-by-step state and lets the program carry on running normally. The counterpart of `debug.halt`, usable from inside the script — for example placed on the 5th iteration of a loop, to pause at the start, step through the first iterations, then run freely once the user steps onto it. Only leaves the pause: it doesn't change the trace setting (`debug.tron`/`debug.troff`). Ignored outside debug mode. NANO-only: not present in the desktop MOGWAI engine |
| `debug.tron` | 🔗 | `delay debug.tron` | Debug mode only: waits `delay` milliseconds between each instruction and shows the current instruction in the Studio editor, like a trace. No effect outside debug mode |
| `debug.troff` | 🔗 | `debug.troff` | Debug mode only: turns the trace off — equivalent to `0 debug.tron`. No effect outside debug mode |
| `debug.stack` | ⚙️ | `debug.stack` → `.string` | Pushes a string representing the stack as it was when the primitive was called, the top of the stack first (`0:`), one item per line, under a `STACK` heading — see the example below. Works in a normal run as well as in debug mode. Studio's `nano.stack` asks the device for this same representation and displays it |

`?`, `??` and `debug.write` accept a `MOGRef` (`&variable`) and dereference it automatically before printing.

### Inspecting the stack

`debug.stack` pushes onto the stack a string representing the stack, which can then be displayed or sent anywhere. With `50 60 "Hello" (1 2 3) true [x: 50 y: 20]` on the stack, `debug.stack` pushes:

```
STACK
0: [x: 50 y: 20]
1: true
2: (1 2 3)
3: "Hello"
4: 60
5: 50
```

The string describes the stack as it was when the primitive was called — `debug.stack` itself isn't part of it. From MOGWAI NANO Studio, `nano.stack` asks the connected device for the same representation and displays it in the MOGWAI console, which makes it a convenient way to look at a program's stack while it is paused (see the [Studio Primitives Reference](studio-primitives.md)).

### Debugging a program

A program sent with `nano.debug` or `nano.debugall` (see the [Studio Primitives Reference](studio-primitives.md)) runs on the device in *debug mode*. Studio sends it with the position of each instruction in your original source attached to it; the device keeps that information with the instruction while it runs, and a program sent without it simply runs as before.

Before executing an instruction that can be paused on, the device checks what it has been asked to do:

- **A pause was requested** (`debug.halt`, `¤`, `nano.debug.halt` or `nano.debug.step`): it reports the position of the instruction about to run to Studio, which highlights it in the editor, then waits. That instruction has not been executed yet. It stays there until Studio asks it to resume or to execute a single instruction.
- **A trace delay is set** (`debug.tron`): it reports the position the same way, then waits for the delay before going on.

Which instructions can be paused on depends on the command used: with `nano.debug`, primitives, words and auto-evaluated lists and records; with `nano.debugall`, every token, literals included. Blocks never are.

A script can pause itself with `¤` (`debug.halt`) and resume itself with `debug.resume`. For example, to step through the first iterations of a loop and then let the rest run normally:

```
¤
1 10 for 'i' do
{
    i ?
    if (i 5 ==) then { debug.resume }
}
```

The program pauses at the start; the user steps with `nano.debug.step` (or the debug panel's Step button) through the first iterations, and when the program reaches `debug.resume` on the 5th one, the next step executes it and the program carries on running normally.

`debug.step` is **not** a primitive: it only exists as a message sent by Studio to the device, through `nano.debug.step` or the Step button of the debug panel — stepping makes no sense from inside the script itself. `debug.resume` exists both ways: as the primitive above, and as a message sent by Studio through `nano.debug.resume` or the Resume button.

Limitations for now: stepping is *step-into* only (no step-over or step-out); tasks can't be run in debug mode; units are loaded without debug information, so they can't be stepped through. An event handler, on the other hand, can be stepped through like a function of the source code.

---

## 9. System (`mogwai.*`)

All ⚙️ **NANO-only** (though most have a conceptual desktop equivalent).

| Primitive | Signature | Description |
|---|---|---|
| `mogwai.halt` | `mogwai.halt` | Stops the current program immediately, raising `MW.2` (`HaltEncounteredError`) — the mechanism by which a script halts itself voluntarily |
| `mogwai.exit` | `mogwai.exit` | Stops the current program immediately, from anywhere, with no error — a clean exit rather than `mogwai.halt`'s voluntary-halt error |
| `mogwai.memory` | `forceCollect mogwai.memory` → `.number` | Returns free RAM in bytes. `true` forces a garbage collection before measuring; `false` returns the current figure without forcing one |
| `mogwai.reset` | `mogwai.reset` | Fully resets the engine state: stack, global variables, functions, timers, events, tasks, stopwatches, opened pins and I2C/SPI/PWM/ADC/SSD1306 resources, what loaded usings keep for the engine, frugal mode and interrupt state. It does so even in keepAlive mode — see `mogwai.keepAlive` |
| `mogwai.reboot` | `mogwai.reboot` | Reboots the device. Runs `MOGWAI.onReboot` first if defined (unlike the Studio-side `nano.reboot`, which bypasses it), waits 1 second, then reboots |
| `mogwai.info` | `mogwai.info` → `.record` | Returns a record with `name`, `mogwai` (NANO runtime version), `ip`, `session`, `platform`, `target`, `oem`, `system`, `memory` (free RAM, non-forcing), `skills` (list), `units` (list of stored unit names), `usings` (list of library names currently *installed* on flash — not necessarily loaded, see `mogwai.using`), `primitives` (list of every primitive name currently usable, at this exact instant — grows as `mogwai.using` calls succeed, since a loaded library's primitives merge into this same list), `frugalMode` (current mode), and `keepAlive` (`true`/`false` — see `mogwai.keepAlive`) |
| `mogwai.frugalMode` | `enabled mogwai.frugalMode` | Enables (`true`) or disables (`false`) frugal mode for subsequent execution |
| `mogwai.keepAlive` | `enabled mogwai.keepAlive` | Enables (`true`) or disables (`false`) keepAlive mode, a REPL-style mode where state carries over from one run to the next instead of every run starting from a clean slate. **Preserved between runs:** global variables, declared functions, the stack, stopwatches, frugal mode, the interrupt state (`DI`/`EI`), the GPIO pins and the I2C/SPI/PWM/ADC/SSD1306 resources opened by earlier runs (and the TFT screen and touch panel, if open), and whatever loaded usings keep on the engine's behalf (for instance the channels they opened) — the libraries themselves stay loaded in either mode. **Not preserved:** tasks, timers and events. Since the interrupt state is preserved too, a `DI` can deliberately span several runs (handy to test a timing-sensitive sequence step by step) — and one left without its `EI` keeps callbacks from being delivered until an `EI` or a `mogwai.reset`. Off by default — it must be turned on explicitly. It takes effect from the next run, not the current one, and `mogwai.halt`/`nano.halt` don't change how it works: everything that is preserved stays preserved. `mogwai.reset` still performs a full reset in this mode — the way to start over from a clean slate without switching keepAlive off. Studio-side counterpart: `nano.keepAlive` (Studio GUI only) |
| `mogwai.sendMessage` | `"message" mogwai.sendMessage` | Sends an arbitrary string to Studio (device → Studio direction) — the counterpart to the Studio-side `nano.send` (Studio → device) |
| `mogwai.units` | `mogwai.units` → `.list` | Returns the names of all units currently stored on the device, from within a running program |
| `mogwai.units.run` | `'unit' mogwai.units.run` | Executes a stored unit's code — typically used to load the functions it declares (e.g. a RTC helper library) into the current program's context, once, near the start of a script |
| `mogwai.using` | `'name' mogwai.using` | Loads the library (*using*) installed on the device's flash under `I:\mogwai\usings\<name>`: its `.pe` files, in the order listed by its `manifest.txt` (the last one being the plugin itself), then registers the primitives and error codes it exposes — usable from then on, by every program, for the rest of the device's uptime. A library is loaded at most once: calling `mogwai.using` again for a name already loaded is a harmless no-op. Failures (manifest missing or unreadable, a file that fails to load...) raise `MW.80`. A loaded library can never be unloaded — only a reboot clears it. Libraries are installed from the Studio with `nano.usings.install` (Studio GUI only) |
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

## 12. SPI

All ⚙️ **NANO-only.** Channels identified by a user-chosen name, like I2C/PWM/ADC.

| Primitive | Signature | Description |
|---|---|---|
| `spi.open` | `'name' bus csPin frequency mode spi.open` | Opens a named SPI channel. `bus` must be `1` or `2` (like I2C, exactly two SPI buses exist). `csPin` is any GPIO — chip-select toggling is handled automatically by the driver, no manual write needed. `frequency` in Hz. `mode` is `0`-`3` (standard SPI CPOL/CPHA combinations). Refuses to reopen an already-used name. Unlike I2C, **no default pin mapping exists on either bus** — `esp32.setPinFunction` is mandatory for MOSI/MISO/SCK before every `spi.open`, no exceptions |
| `spi.close` | `'name' spi.close` | Closes the channel and releases the resource |
| `spi.write` | `'name' data spi.write` | Sends a `MOGData` buffer of any length in a single transaction |
| `spi.read` | `'name' length spi.read` → `.data` | Reads `length` bytes from the device and pushes them as a `MOGData`. `length` must be between `0` and `4096`, otherwise `MW.22` is raised; `MW.554` is raised if the read itself fails. Like `spi.write`, each call is its own transaction (the driver handles chip-select around it): for a protocol that has to write and read within the same transaction, use `spi.transfer` |
| `spi.transfer` | `'name' data spi.transfer` → `.data` | Full-duplex: sends a buffer and returns a same-length `MOGData` of whatever was clocked back in simultaneously. Needed for chips whose protocol requires reading and writing within the same continuous transaction — for example, reading a register on an MFRC522 RFID reader, where the requested register's value only comes back one byte after it was addressed |
| `spi.minClockFrequency` | `bus spi.minClockFrequency` → `.number` | Queries the minimum clock frequency (Hz) supported by a bus, directly by bus number — no channel needs to be open first |
| `spi.maxClockFrequency` | `bus spi.maxClockFrequency` → `.number` | Same as above, for the maximum supported frequency. No fixed number is documented here since it varies by target — query it at runtime instead |

**Validated against real hardware:** a full MFRC522 RFID reader driver (register read/write, the chip's CRC coprocessor, card detection, UID/SAK retrieval) was written entirely in MOGWAI NANO script on top of `spi.write`/`spi.transfer`, with no additional C# needed — successfully read a real card's UID and SAK.

---

## 13. PWM

All ⚙️ **NANO-only.** Channels are identified by a user-chosen name, following the same pattern as I2C. Requires the pin to already be configured for PWM via `esp32.setPinFunction` (see [ESP32 DeviceFunction values reference](esp32-device-function-values.md)) beforehand.

| Primitive | Signature | Description |
|---|---|---|
| `pwm.open` | `'name' pin frequency dutyCycle pwm.open` | Creates and starts-ready a named PWM channel on `pin`, at `frequency` (Hz, must be greater than `0`) and `dutyCycle` — given as a **percentage** (`0`-`100`), not a fraction. Refuses to reopen an already-used name. Fails cleanly if the pin can't provide a PWM channel |
| `pwm.start` | `'name' pwm.start` | Starts the named channel's output |
| `pwm.stop` | `'name' pwm.stop` | Stops the named channel's output |
| `pwm.close` | `'name' pwm.close` | Stops (if running) and releases the named channel |

**Not yet available:** changing frequency or duty cycle after opening a channel. To change either, close the channel and reopen it with new values. This also sidesteps a platform caveat — some PWM implementations recommend fixing the frequency at creation rather than changing it on a running channel.

**Note on pairs:** PWM channels operate in pairs sharing the same frequency (e.g. `PWM1`/`PWM2`). For two independent frequencies running at once, use channels at least two apart in the `DeviceFunction` numbering — see the [ESP32 DeviceFunction values reference](esp32-device-function-values.md).

---

## 14. ADC

All ⚙️ **NANO-only.** Analog-to-digital conversion. Channels are identified by a user-chosen name, following the same pattern as I2C, SPI and PWM.

| Primitive | Signature | Description |
|---|---|---|
| `adc.open` | `'name' channel adc.open` | Opens ADC channel number `channel` under the given name. Raises `MW.540` if the name is already in use, `MW.541` if the channel can't be opened |
| `adc.read` | `'name' adc.read` → `.number` | Reads the channel's current raw value — to be interpreted against `adc.maxValue` (and `adc.resolutionInBits`). Raises `MW.542` if the name is unknown |
| `adc.close` | `'name' adc.close` | Releases the named channel. Raises `MW.542` if the name is unknown |
| `adc.maxValue` | `adc.maxValue` → `.number` | The highest raw value the ADC can return. Takes no argument |
| `adc.resolutionInBits` | `adc.resolutionInBits` → `.number` | The ADC's resolution, in bits. Takes no argument |

---

## 15. SSD1306 OLED display

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

## 16. Device-level platform access

⚙️ **NANO-only. ESP32 only** — this primitive exists in the `MogwaiNanoEsp32` binary alone; it isn't part of `MogwaiNanoPicoW` at all (not a runtime check, the primitive itself isn't compiled in). Raspberry Pi Pico 2 W has no equivalent yet.

| Primitive | Signature | Description |
|---|---|---|
| `esp32.setPinFunction` | `pin function esp32.setPinFunction` | Dynamically reassigns a pin's function — e.g. designating I2C clock/data pins on a board where the default I2C bus isn't pre-wired (like `ESP32_S3_OCTAL`). `function` is a raw numeric value from `nanoFramework.Hardware.Esp32`'s `DeviceFunction` enum (e.g. `131328`/`131329` for `I2C1_DATA`/`I2C1_CLOCK`), not a MOGWAI NANO abstraction — see the [ESP32 DeviceFunction values reference](esp32-device-function-values.md) for the complete list (SPI, I2C, serial, PWM, ADC, I2S, SDMMC). Named `device.setPinFunction` before the runtime split into per-platform binaries (see the [project structure](../README.md#project-structure)) — renamed once "device" stopped being accurate |

---

## 17. BLE Peripheral

⚙️ **NANO-only. ESP32 only** — lives entirely in its own `MogwaiNanoBlePeripheral` assembly, referenced only by `MogwaiNanoEsp32` (kept out of `MogwaiNanoCore`, which is already close to its own metadata-table size limit). Raspberry Pi Pico 2 W has no equivalent yet. Declares the `'BLE-P'` skill once available — check with `hasSkill` before using.

`ble.peripheral.create` takes the whole peripheral definition as a single record:

```
[
    name: "TEST"
    services:
    (
        [
            name: "Service1"
            uuid: "7EC2D911-9D30-48B9-BC37-963C3775F04C"
            characteristics:
            (
                [
                    name: "FreeValue"
                    uuid: "C9B57B56-7835-4305-B25D-6D783A10227A"
                    properties: "RW"
                    value: D:00
                ]
                [
                    name: "AutoValue"
                    uuid: "BF564D26-ECB0-43A4-B77F-DFB67F594B11"
                    properties: "RN"
                    value: D:00
                ]
            )
        ]
    )
]
```

`uuid:` is a standard dashed GUID string, for both services and characteristics. `properties:` combines any of `R` (Read), `W` (Write), `X` (WriteWithoutResponse), `I` (Indicate), `N` (Notify). `value:` is the characteristic's initial value, as `MOGData`.

**Only the first service listed is ever advertised.** Every service and characteristic described is created regardless, and all are reachable once a client is connected — but BLE advertising/discovery only exposes that first service. Define everything under a single service if it all needs to be discoverable together.

| Primitive | Signature | Description |
|---|---|---|
| `ble.peripheral.create` | `record ble.peripheral.create` | Defines the whole peripheral from a record, matching the shape above — implicitly resets any peripheral already defined first. Creates every GATT service and characteristic described, but doesn't start advertising; call `ble.peripheral.start` next |
| `ble.peripheral.start` | `ble.peripheral.start` | Starts advertising the first defined service, making the peripheral discoverable and connectable. A no-op if already advertising. Fires `BLE_PERIPHERAL_DID_START` |
| `ble.peripheral.stop` | `ble.peripheral.stop` | Stops advertising and tears the peripheral down entirely — a fresh `ble.peripheral.create` is needed before using it again. Fires `BLE_PERIPHERAL_DID_STOP` if it was actually advertising |
| `ble.peripheral.setValue` | `value 'name' ble.peripheral.setValue` | Sets a characteristic's current value locally (`value` as `MOGData`). Doesn't notify connected clients by itself — call `ble.peripheral.notify` separately for that |
| `ble.peripheral.getValue` | `'name' ble.peripheral.getValue` → `.data` | Reads a characteristic's current, locally-held value |
| `ble.peripheral.notify` | `'name' ble.peripheral.notify` | Pushes a characteristic's current value to every subscribed client, if any (requires `N` or `I` in that characteristic's `properties`). A no-op if nobody is subscribed |

**Events**, fired on the engine that created the peripheral:

| Event | `eventData` | Fired when |
|---|---|---|
| `BLE_PERIPHERAL_DID_START` | — | `ble.peripheral.start` actually starts advertising |
| `BLE_PERIPHERAL_DID_STOP` | — | advertising stops, whether via `ble.peripheral.stop` or a fresh `ble.peripheral.create` |
| `BLE_PERIPHERAL_VALUE_DID_CHANGE` | `[name: uuid: value:]` — the characteristic that changed | a connected client writes to a characteristic (`W` or `X`) |
| `BLE_PERIPHERAL_CLIENT_DID_CONNECT` | — (`null`) | a client connects to the peripheral |
| `BLE_PERIPHERAL_CLIENT_DID_DISCONNECT` | — (`null`) | a connected client disconnects from the peripheral |
| `BLE_PERIPHERAL_SESSION_ERROR` | `.string` — the name of the C# `BluetoothError` enumeration value describing the error | the BLE session reports an error (anything other than `Success`) |

`BLE_PERIPHERAL_SESSION_ERROR`'s string is one of: `"RadioNotAvailable"` (the Bluetooth radio is unavailable), `"ResourceInUse"` (the necessary resources are currently in use), `"DeviceNotConnected"` (the remote device is not connected), `"OtherError"` (an unexpected error), `"DisabledByPolicy"`, `"DisabledByUser"`, `"NotSupported"` (not supported by the radio hardware), `"ConsentRequired"` and `"TransportNotSupported"`. `"Success"`, the enumeration's no-error value, is never delivered: when the session reports no error, the status of the session decides instead (`Active` fires `BLE_PERIPHERAL_CLIENT_DID_CONNECT`, `Closed` fires `BLE_PERIPHERAL_CLIENT_DID_DISCONNECT`).

**Example — reacting to a client coming and going:**

```
onEvent 'BLE_PERIPHERAL_CLIENT_DID_CONNECT' do
{
    "Client connected" ?
}

onEvent 'BLE_PERIPHERAL_CLIENT_DID_DISCONNECT' do
{
    "Client disconnected" ?
}

onEvent 'BLE_PERIPHERAL_SESSION_ERROR' do
{
    "BLE session error: " eventData + ?
}
```

Since nothing tears the peripheral down automatically on an abnormal exit, a good practice is calling `ble.peripheral.stop` from both `MOGWAI.onStop` and `MOGWAI.onError`.

---

## 18. Onboard LED

⚙️ **NANO-only. ESP32 only** — lives entirely in its own `MogwaiNanoEsp32LED` assembly, referenced only by `MogwaiNanoEsp32` (kept out of `MogwaiNanoCore`). Raspberry Pi Pico 2 W has no equivalent. Declares the `'ONBOARD-LED'` skill — check with `hasSkill` before using.

Many ESP32-S3 boards carry a built-in RGB LED (a WS2812) wired to a GPIO — GPIO 48 on some boards, but this varies with the board and its revision, and a few need a solder bridge to connect it. The runtime uses it as a status indicator, and a script can take it over. The GPIO is set from Studio with `nano.onboard.led.set` (see the [Studio Primitives Reference](studio-primitives.md)) and takes effect after a reboot. **The LED is disabled by default**: the GPIO is `-1` until it is configured, so it has to be set once before the LED is used.

### Status colours

The runtime shows its state with fixed colours — there is no blinking:

| State | Colour | When |
|---|---|---|
| Initializing | orange | The application is starting: network, services... |
| Idle | green | Initialization is complete and no program is running |
| Running | blue | A program is running. In keepAlive mode, each command turns the LED from green to blue while it runs, then back to green |
| Error | red | Initialization failed, a program failed to start, or a program stopped on an error — `mogwai.halt` included, since it raises `MW.2` |
| Paused | orange | A debug session is paused. Back to blue on step or resume |

Tasks don't change the colour: only the top-level program does.

### Primitives

| Primitive | Signature | Description |
|---|---|---|
| `onboard.led.set` | `r g b onboard.led.set` | Sets the LED to the given colour — each value between `0` and `255`, otherwise `MW.22` is raised. The script takes control of the LED |
| `onboard.led.off` | `onboard.led.off` | Turns the LED off. The script takes control of the LED |
| `onboard.led.brightness` | `value onboard.led.brightness` | Sets the global brightness, `0`-`255` (`30` by default), applied to every colour, status colours included |
| `onboard.led.release` | `onboard.led.release` | Gives the LED back to the runtime, which shows its current state again |
| `onboard.led.red` | `onboard.led.red` | Shortcut for `255 0 0 onboard.led.set` |
| `onboard.led.green` | `onboard.led.green` | Shortcut for `0 255 0 onboard.led.set` |
| `onboard.led.blue` | `onboard.led.blue` | Shortcut for `0 0 255 onboard.led.set` |
| `onboard.led.orange` | `onboard.led.orange` | Shortcut for `255 80 0 onboard.led.set` |
| `onboard.led.purple` | `onboard.led.purple` | Shortcut for `160 0 255 onboard.led.set` |
| `onboard.led.yellow` | `onboard.led.yellow` | Shortcut for `255 200 0 onboard.led.set` |

The values of `orange`, `purple` and `yellow` are tuned for a WS2812, whose green is much brighter than its red and blue: the textbook values (`255 165 0` for orange, for instance) would look yellow.

### Taking control from a script

As soon as a script calls `onboard.led.set`, `onboard.led.off` or one of the colour shortcuts, it owns the LED: the status colours of the runtime are ignored, but remembered. The one exception is the orange of a paused debug session, which always wins so that a pause stays visible; the script's colour comes back when the program resumes. The runtime takes the LED back, and shows its current state, when the program ends, when it is paused in a debug session, or when the script calls `onboard.led.release`.

While the LED is disabled (GPIO `-1`, the default) or unusable (a failed driver, for instance), all these primitives do nothing and raise no error, so the same script runs unchanged on any ESP32 board. A failure of the LED driver while a primitive is writing to it raises `LED.1`.

### Implementation note: the driver and the garbage collector

After a garbage-collection compaction (for instance a forced `GC.Run(true)`), the `Ws28xx` driver for ESP32 (`nanoFramework.Iot.Device.Ws28xx.Esp32`, version 3.0.1) sends wrong data to the LED, which then shows random colours instead of the requested one — probably because the driver keeps a native pointer to its managed image buffer, which the compaction has moved. To stay reliable whenever a collection happens, the runtime re-creates the driver on every colour change instead of keeping a single instance. Nothing is required from scripts.

---

## 19. TFT display

⚙️ **NANO-only. ESP32 only** — lives entirely in its own `MogwaiNanoTft` assembly, referenced only by `MogwaiNanoEsp32` (kept out of `MogwaiNanoCore`, which has reached the limit of the CLR's 16-bit internal identifiers). Raspberry Pi Pico 2 W has no equivalent yet. Declares the `'TFT'` skill — check with `hasSkill` before using.

Drives a colour display wired over SPI. Every drawing primitive writes into a RGB565 canvas held in RAM, and **nothing is visible until `tft.refresh`** sends it to the display (or until `true tft.autoRefresh` makes every drawing call refresh its own area). `tft.refresh` only sends the rectangle modified since the previous refresh: a full-screen refresh of a 240×320 ST7789 at 40 MHz takes about 80 ms, while refreshing a small area is almost instant. Batch several drawing calls before a single `tft.refresh`.

**The SPI pins are set by the runtime**, from the pins given to `tft.init` — `esp32.setPinFunction` isn't needed (SPI bus 1 only). **The canvas takes `width × height × 2` bytes** — 150 KB for a 240×320 display — which makes the library practical on an ESP32-S3 with PSRAM only; `tft.init` raises `TFT.1` if the buffer can't be allocated.

**One screen per device.** The screen is shared by the main program and every task: any of them can call the `tft.*` primitives, and a lock serializes the calls so that two tasks never modify the canvas at the same time. `tft.init` on a screen that is already open raises `TFT.3`, whichever engine opened it; `tft.close` can be called from any engine. At the end of a program the backlight is switched off and the screen is released, so the next program has to call `tft.init` again — unless keepAlive mode is on (see `mogwai.keepAlive`), in which case the screen stays open, and displayed, from one run to the next.

### Primitives

Names and argument order follow the `ssd1306.*` family where they overlap, with a colour in place of the on/off flag. A **colour** is a `.number` in RGB565 (`0`-`65535`), produced by `tft.color`.

| Primitive | Signature | Description |
|---|---|---|
| `tft.init` | `record tft.init` | Opens the screen described by the record (see below), clears it to black and switches the backlight on. Raises `TFT.3` if a screen is already open, `TFT.1` if the initialization fails |
| `tft.close` | `tft.close` | Switches the backlight off and releases the screen. Does nothing if no screen is open |
| `tft.setRotation` | `n tft.setRotation` | Sets the rotation: `0` is the display's native orientation (portrait for a 240×320 panel), `1` to `3` turn it by 90° steps. **Clears the canvas to black** |
| `tft.width` | `tft.width` → `.number` | Width in pixels in the current rotation |
| `tft.height` | `tft.height` → `.number` | Height in pixels in the current rotation |
| `tft.backlight` | `on tft.backlight` | Switches the backlight on (`true`) or off (`false`). Does nothing if the record has no `backlight:` pin |
| `tft.refresh` | `tft.refresh` | Sends the area modified since the previous refresh to the display |
| `tft.autoRefresh` | `on tft.autoRefresh` | When `true`, every drawing call refreshes its own area immediately. Off by default |
| `tft.color` | `r g b tft.color` → `.number` | Builds a RGB565 colour from three values between `0` and `255` |
| `tft.clear` | `color tft.clear` | Fills the whole canvas with a colour |
| `tft.drawPixel` | `x y color tft.drawPixel` | Sets one pixel |
| `tft.getPixel` | `x y tft.getPixel` → `.number` | Reads one pixel of the canvas (`0` outside the canvas) |
| `tft.drawHorizontalLine` | `x y length color tft.drawHorizontalLine` | Draws a horizontal line |
| `tft.drawVerticalLine` | `x y length color tft.drawVerticalLine` | Draws a vertical line |
| `tft.drawLine` | `x1 y1 x2 y2 color tft.drawLine` | Draws a line between two points |
| `tft.drawRectangle` | `x y width height color tft.drawRectangle` | Draws a rectangle outline |
| `tft.drawFilledRectangle` | `x y width height color tft.drawFilledRectangle` | Draws a filled rectangle |
| `tft.drawRoundRectangle` | `x y width height radius color tft.drawRoundRectangle` | Draws a rounded rectangle outline |
| `tft.drawFilledRoundRectangle` | `x y width height radius color tft.drawFilledRoundRectangle` | Draws a filled rounded rectangle |
| `tft.drawCircle` | `x y radius color tft.drawCircle` | Draws a circle outline |
| `tft.drawFilledCircle` | `x y radius color tft.drawFilledCircle` | Draws a filled circle |
| `tft.drawTriangle` | `x1 y1 x2 y2 x3 y3 color tft.drawTriangle` | Draws a triangle outline |
| `tft.drawBitmap` | `x y width height data tft.drawBitmap` | Draws an image from a `MOGData` buffer of RGB565 pixels, two bytes per pixel, high byte first. Raises `TFT.4` if the buffer is smaller than `width × height × 2` |
| `tft.scroll` | `x y width height dx dy color tft.scroll` | Moves the content of a rectangle of the canvas by `dx` pixels to the right and `dy` pixels down — negative values move it left and up. `0 0 240 320 0 -8 0 tft.scroll` scrolls a whole 240×320 screen up by 8 pixels, i.e. one line of text at `size` 1. What leaves the rectangle is lost, and the band that is freed is filled with `color`. The whole rectangle counts as modified, so the next `tft.refresh` sends all of it: about 80 ms for a full 240×320 screen, proportionally less for a smaller rectangle. A displacement larger than the rectangle simply fills it with `color` |
| `tft.drawString` | `x y "text" size foreground background tft.drawString` | Draws a text with the built-in 8×8 font, `size` being an integer scale factor (`1`, `2`, `3`...): each character is `8 × size` pixels wide. A `background` of `-1` leaves the background untouched (transparent); `\n` starts a new line |
| `tft.measureString` | `"text" size tft.measureString` → `.number` | Width in pixels of the longest line of the text — to centre it, for instance |
| `tft.smooth` | `on tft.smooth` | When `true`, text drawn with a `size` of 2 or more is anti-aliased: only the stair steps of diagonal edges are smoothed, while straight edges and corners stay sharp. The smoothing blends with what is already on the canvas, so it also works with a transparent background (`-1`). It has no effect at `size` 1. Off by default, and back to off each time a screen is opened with `tft.init` |

Everything drawn outside the canvas is clipped.

The font covers ASCII and the characters used in French: `é è ê ë à â ä î ï ô ö ù û ü ÿ ç`, their capitals, `œ` `Œ`, `«` `»`, `€`, `°` and a few more (`á í ó ú`, `·`, the typographic apostrophe, the en dash). An accented capital is drawn one pixel lower than the other letters, to leave room for the accent. A character the font doesn't have is drawn as `?`.

### The configuration record

```
[
    controller: "st7789"
    width: 240
    height: 320
    sclk: 39
    mosi: 38
    miso: 40
    dc: 42
    cs: 45
    rst: -1
    backlight: 1
    invert: true
]
```

| Key | Type | Default | Description |
|---|---|---|---|
| `width:`, `height:` | number | — (mandatory) | Size of the display in its native orientation |
| `sclk:`, `mosi:`, `dc:`, `cs:` | number | — (mandatory) | Pins: SPI clock, SPI data out, data/command select and chip select |
| `miso:` | number | `-1` | SPI data in, if the display has it wired |
| `rst:` | number | `-1` | Hardware reset pin. `-1` means none: the initialization table then starts with a software reset |
| `backlight:` | number | `-1` | Backlight pin, driven high when the screen is on. `-1` means none |
| `spiBus:` | number | `1` | SPI bus. Only bus `1` is supported |
| `clock:` | number | `40000000` | SPI clock frequency in Hz |
| `controller:` | string | — | A built-in controller profile. `"st7789"` is the only one for now |
| `init:` | `MOGData` | — | A custom initialization table (see below). Replaces the profile's one when both are given |
| `rotations:` | list | — | Four numbers: the memory-access-control value of rotations `0` to `3`. Replaces the profile's one when both are given |
| `invert:` | boolean | `false` | Colour inversion, **for the built-in profile only** (IPS panels such as the one of the Waveshare ESP32-S3-Touch-LCD-2 need `true`). With a custom `init:`, put the inversion command in the table |
| `bgr:` | boolean | `false` | Red and blue swapped, for the panels that need it. Applies with any initialization |
| `xOffset:`, `yOffset:` | number | `0` | Added to the position of the window sent to the display, for a panel smaller than the controller's memory (such as a 240×240 or 135×240 ST7789). Applied as given in every rotation, which some panels don't want |
| `casetCommand:`, `rasetCommand:`, `ramwrCommand:`, `madctlCommand:` | number | `42`, `43`, `44`, `54` | Command codes for the column address, row address, memory write and memory access control, for a controller that doesn't use the usual ones (`0x2A`, `0x2B`, `0x2C`, `0x36`) |

A missing or invalid key raises `MW.22`, naming the key. Either `controller:`, or both `init:` and `rotations:`, must be given.

### Controllers and custom initialization

`controller: "st7789"` fills in both the initialization table and the four rotation values of an ST7789, so that pins and size are all the record needs. The table it runs is a software reset, sleep out, 16-bit colour (RGB565), colour inversion (`invert:`), normal display mode, then display on. The rotation values are `0`, `96`, `192` and `160`.

For any other controller of the same command-based SPI family (ST7735, ILI9341, GC9A01...) — whose chip-specific part is essentially its initialization sequence — give `init:` and `rotations:` instead of `controller:`. The table is run once, by `tft.init`, after the hardware reset if `rst:` is set. Each entry is:

```
command   number-of-arguments   argument 1 ... argument N   delay
```

with the delay in units of 10 ms (`15` is 150 ms, `0` is no wait). The table of the built-in ST7789 profile, written as a `MOGData`, is:

```
D:01000F11000C3A01550021000013000129000A
```

that is: `01 00 0F` software reset and wait 150 ms, `11 00 0C` sleep out and wait 120 ms, `3A 01 55 00` 16-bit colour, `21 00 00` inversion on, `13 00 01` normal mode and wait 10 ms, `29 00 0A` display on and wait 100 ms. A malformed table raises `TFT.1`.

> Only the ST7789 has been tried on real hardware. A custom `init:` for another controller is written from its datasheet and may need adjusting.

### Example

The whole configuration of the Waveshare ESP32-S3-Touch-LCD-2, then a few drawings:

```
[ controller: "st7789" width: 240 height: 320
  sclk: 39 mosi: 38 miso: 40 dc: 42 cs: 45 rst: -1 backlight: 1
  invert: true ] tft.init

0 0 0 tft.color tft.clear

8 8 "Écran prêt" 2 0 255 255 tft.color -1 tft.drawString
8 40 100 50 255 0 0 tft.color tft.drawRectangle
130 60 100 50 8 0 255 0 tft.color tft.drawFilledRoundRectangle
60 160 30 255 255 0 tft.color tft.drawFilledCircle

tft.refresh
```

A text console that scrolls like an 8-bit home computer, under a fixed title. Each new line is written at the bottom of the screen; once the screen is full, `tft.scroll` moves the area below the title up by one line (8 pixels at `size` 1) first. `&curY 8 +` adds 8 to `curY` in place:

```
# Works unmodified on a Waveshare ESP32-S3-Touch-LCD-2

[ controller: "st7789" width: 240 height: 320
  sclk: 39 mosi: 38 miso: 40 dc: 42 cs: 45 rst: -1 backlight: 1
  invert: true ] tft.init

0 0 0 tft.color tft.clear
255 255 255 tft.color -> 'white'
0 255 0 tft.color -> 'green'
true tft.smooth

# Fixed title (outside the scrolling area)
0 0 "CONSOLE" 2 white 0 tft.drawString

# The text lines (size 1, 8 pixels high) run from y=16 to y=312
16 -> 'curY'

1 80 for 'n' do
{
    # The line no longer fits: scroll the area below the title up by one line
    if (curY 312 >) then
    {
        0 16 240 304 0 -8 0 tft.scroll
        312 -> 'curY'
    }

    # Every fifth line in green
    if (n 5 mod 0 ==) then { green -> 'fg' } else { white -> 'fg' }

    0 curY "Line number " n ->str + 1 fg 0 tft.drawString
    &curY 8 +

    tft.refresh
}

5000 wait
```

### Errors

| Code | Message | Raised when |
|---|---|---|
| `TFT.1` | tft init error | `tft.init` fails: malformed initialization table, SPI or GPIO failure, SPI bus other than `1`, not enough memory for the canvas |
| `TFT.2` | tft already closed error | a `tft.*` primitive that needs the screen is called while it isn't open |
| `TFT.3` | tft already opened error | `tft.init` is called while a screen is already open |
| `TFT.4` | tft operation error | a drawing or refresh call fails (the display or the SPI bus fails, a bitmap buffer is too small...) |

Wrong arguments raise the usual `MW.20` (too few arguments), `MW.21` (wrong type) and `MW.22` (wrong value).

**Tested on** the Waveshare ESP32-S3-Touch-LCD-2 (ST7789, 240×320), in the four rotations. Other ST7789 panels should work given the right size and rotation values. Other controllers, and panels that need `xOffset:`/`yOffset:`, have not been tried.

---

## 20. Touch panel

⚙️ **NANO-only. ESP32 only** — lives entirely in its own `MogwaiNanoTouch` assembly, referenced only by `MogwaiNanoEsp32` (kept out of `MogwaiNanoCore`). Raspberry Pi Pico 2 W has no equivalent yet. Declares the `'TOUCH'` skill — check with `hasSkill` before using.

Reads a capacitive touch panel wired over I2C, either on demand (`touch.read`) or through events fired by a polling thread (`touch.start`). The panel is independent of the TFT display — it can be used without one — but when a TFT display is open, **the coordinates follow its rotation**, so that touching a point of the screen gives that point's coordinates whatever the rotation.

**The I2C pins are set by the runtime**, from the pins given to `touch.init` — `esp32.setPinFunction` isn't needed (I2C bus 1 only).

**One panel per device**, shared by the main program and every task, with the same rules as the TFT screen: `touch.init` on a panel already open raises `TOUCH.3`, and the panel is released at the end of a program unless keepAlive mode is on.

### Primitives

| Primitive | Signature | Description |
|---|---|---|
| `touch.init` | `record touch.init` | Opens the panel described by the record (see below). Raises `TOUCH.3` if a panel is already open, `TOUCH.1` if the controller can't be reached |
| `touch.close` | `touch.close` | Stops the polling thread, if any, and releases the panel. Does nothing if no panel is open |
| `touch.start` | `touch.start` | Starts the polling thread. The `TOUCH_DID_*` events are fired on the engine that called it. A no-op if it is already running |
| `touch.stop` | `touch.stop` | Stops the polling thread |
| `touch.count` | `touch.count` → `.number` | The number of fingers currently on the panel |
| `touch.read` | `touch.read` → `.number .number` | `x y` of the followed finger (see below) — or of the first finger when the polling thread isn't running — in the frame of the screen. `-1 -1` when nobody touches the panel |
| `touch.readPoint` | `n touch.readPoint` → `.number .number` | `x y` of the finger of rank `n` in the controller's list (`0` is the first), in the frame of the screen. `-1 -1` if there is no such finger |

While the polling thread isn't running, `touch.count`, `touch.read` and `touch.readPoint` read the controller each time they are called, so a script can poll the panel itself. While it is running, they use the latest reading of the thread.

### Events

Fired on the engine that called `touch.start`, with the usual `eventData` local variable holding a record.

| Event | `eventData` | Fired when |
|---|---|---|
| `TOUCH_DID_PRESS` | `[x: y: count:]` | The followed finger touches the panel |
| `TOUCH_DID_MOVE` | `[x: y: count:]` | The followed finger has moved |
| `TOUCH_DID_RELEASE` | `[x: y: count:]` | The followed finger is lifted. `x` and `y` are its last position |

`count` is the number of fingers **still down once the event has happened**.

**The events follow one finger.** The followed finger is the one that touched the panel first, identified by the controller's own finger number: it keeps being followed while other fingers come and go, instead of the events jumping from one finger to another. A second finger putting down fires nothing by itself — `count` goes up in the next events — and `touch.readPoint` gives access to it.

**Hand-over.** When the followed finger is lifted while other fingers remain, `TOUCH_DID_RELEASE` is fired at its last position **with a `count` above `0`**, and the finger that remains becomes the followed one, with a new `TOUCH_DID_PRESS` on the next reading (20 ms later by default). A script that draws strokes can therefore tell the real end of a stroke (`count` is `0`) from a hand-over to another finger (`count` is not `0`).

### The configuration record

```
[
    controller: "gt911"
    i2cBus: 1
    sda: 8
    scl: 9
    width: 800
    height: 480
]
```

| Key | Type | Default | Description |
|---|---|---|---|
| `sda:`, `scl:` | number | — (mandatory) | I2C data and clock pins |
| `width:`, `height:` | number | — (mandatory) | Size of the panel in its native orientation (used to flip and to follow the screen's rotation) |
| `controller:` | string | — | `"cst816"`, `"gt911"` or `"ft6x36"`. Without it, the record describes a custom controller (see below) |
| `i2cBus:` | number | `1` | I2C bus. Only bus `1` is supported |
| `address:` | number | the controller's own | I2C address of the controller. `"gt911"` without `address:` is searched at `93` (`0x5D`) then `20` (`0x14`) and recognized by its product identifier |
| `pollMs:` | number | `20` | Polling period of the thread, in milliseconds (`5` at least) |
| `swapXY:`, `flipX:`, `flipY:` | boolean | `false` | For a panel mounted rotated or mirrored relative to the display, applied in that order to the panel's native coordinates |
| `debug:` | boolean | `false` | Writes each event to the debugger output (`Debug.WriteLine`, like `debug.vs.write`) |

A missing or invalid key raises `MW.22`, naming the key.

### Controllers

| `controller:` | Chip | Fingers | Default address | State |
|---|---|---|---|---|
| `"cst816"` | CST816S / CST816D | 1 | `0x15` (`21`) | Tested on the Waveshare ESP32-S3-Touch-LCD-2 |
| `"gt911"` | Goodix GT911 | 5 | `0x5D` or `0x14`, searched | Tested on the Waveshare ESP32-S3-Touch-LCD-7 (found at `0x14`), over its full 800×480 area |
| `"ft6x36"` | FT6206 / FT6336 | 5 | `0x38` (`56`) | **Not tried on real hardware** |
| *(none)* | custom register layout | up to 5 | `address:` (mandatory) | **Not tried on real hardware** |

A controller that exposes its fingers in one register block (as the CST816 and the FT6x36 do) can be described by the record itself, without `controller:` — `address:` is then mandatory, along with the keys marked as such below:

| Key | Default | Description |
|---|---|---|
| `readReg:` | — (mandatory) | Register the block is read from (8 bits) |
| `readLen:` | — (mandatory) | Number of bytes read |
| `countIndex:` | — (mandatory) | Index, in the block, of the byte holding the number of fingers |
| `countMask:` | `15` | Mask applied to that byte |
| `pointBase:` | `0` | Index of the first finger's data |
| `pointStride:` | `0` | Distance between two fingers' data |
| `maxPoints:` | `1` | Number of fingers to read |
| `xHi:`, `xLo:`, `yHi:`, `yLo:` | — (mandatory) | Offsets, inside one finger's data, of the high and low bytes of `x` and `y` |
| `hiMask:` | `15` | Mask applied to the high bytes |
| `idByte:`, `idShift:`, `idMask:` | `-1`, `0`, `15` | Where the finger's own number is, if the chip gives one (used to follow a finger while others come and go). Without `idByte:`, a finger's rank in the list is used instead |

### Example

The touch panel of a Waveshare ESP32-S3-Touch-LCD-7 (GT911), logging the events:

```
[ controller: "gt911" i2cBus: 1 sda: 8 scl: 9 width: 800 height: 480 ] touch.init

onEvent 'TOUCH_DID_PRESS' do { "PRESS" ? eventData ? }
onEvent 'TOUCH_DID_RELEASE' do { "RELEASE" ? eventData ? }

touch.start
```

On the Waveshare ESP32-S3-Touch-LCD-2, together with its display, drawing under the finger:

```
[ controller: "cst816" i2cBus: 1 sda: 48 scl: 47 width: 240 height: 320 ] touch.init

onEvent 'TOUCH_DID_MOVE' do
{
    eventData->x: eventData->y: 3 255 255 255 tft.color tft.drawFilledCircle
    tft.refresh
}

touch.start
```

### Errors

| Code | Message | Raised when |
|---|---|---|
| `TOUCH.1` | touch init error | `touch.init` fails: unknown controller, controller not answering or not found, I2C bus other than `1`, invalid register layout |
| `TOUCH.2` | touch already closed error | a `touch.*` primitive that needs the panel is called while it isn't open |
| `TOUCH.3` | touch already opened error | `touch.init` is called while a panel is already open |
| `TOUCH.4` | touch operation error | reading the controller fails in `touch.count`, `touch.read` or `touch.readPoint` |

Wrong arguments raise the usual `MW.20`, `MW.21` and `MW.22`.

**Tested on** the Waveshare ESP32-S3-Touch-LCD-2 (CST816D, with the four rotations of its TFT display) and the Waveshare ESP32-S3-Touch-LCD-7 (GT911, two fingers, with the hand-over described above). `"ft6x36"` and the custom register layout have not been tried on real hardware.

---

## Error codes

MOGWAI uses structured `MW.xx` error codes.

| Range | Category |
|---|---|
| `MW.0`–`MW.9` | Execution flow (e.g. `MW.2` = halt encountered) |
| `MW.10`–`MW.24` | Argument/stack errors |
| `MW.30`–`MW.32` | Math/conversion |
| `MW.40`–`MW.50` | Name/word resolution |
| `MW.60`–`MW.62` | Tasks |
| `MW.80`–`MW.81` | Libraries loaded with `mogwai.using` |
| `MW.500`–`MW.509` | GPIO |
| `MW.510`–`MW.519` | I2C |
| `MW.520`–`MW.529` | SSD1306 |
| `MW.530`–`MW.539` | PWM |
| `MW.540`–`MW.549` | ADC |
| `MW.550`–`MW.559` | SPI |
| `MW.580`–`MW.581` | Units |
| `MW.590` | Platform not supported |
| `MW.!!!` | Fatal |

BLE Peripheral is an exception: it defines its own `BLE.1` ("unable to create peripheral error"), in its own namespace rather than an `MW.5xx` sub-range — a deliberate choice, since it lives in its own assembly specifically to stay out of `MogwaiNanoCore` (whose own `Error.cs` is where every `MW.5xx` range above is actually defined, and is already close to its own metadata-table limit). The onboard LED, the TFT display and the touch panel are the others, for the same reason — each lives in its own assembly: `LED.1` ("led operation error"), `TFT.1` to `TFT.4` (see [TFT display](#19-tft-display)) and `TOUCH.1` to `TOUCH.4` (see [Touch panel](#20-touch-panel)).
