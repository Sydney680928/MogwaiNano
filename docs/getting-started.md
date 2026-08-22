# Getting Started with MOGWAI NANO

This tutorial assumes you already know the basics of MOGWAI itself — the RPN stack model, syntax like `50 -> 'A'`, and control-flow sugar like `if...then...else`. If any of that sounds unfamiliar, spend a few minutes with the [MOGWAI documentation and tutorial](https://github.com/Sydney680928/mogwai) first.

Here, we focus on what's specific to NANO: flashing a device, connecting to it, and driving real hardware.

## 1. Flash and configure your device

Follow the [Quick Start](../README.md#quick-start) in the main README to flash `MogwaiNano.bin` and configure WiFi. Once done, power-cycle the device — it will boot straight into MOGWAI NANO and start listening for connections.

## 2. Start MOGWAI NANO Studio

Launch `MogwaiNanoStudio.exe`. You get a regular MOGWAI console.

> ### Where does your code actually run?
>
> This is the single most important thing to understand before going further: **everything you type in MOGWAI NANO Studio — at the console prompt, or in the editor — runs on your PC**, using the full desktop MOGWAI engine. Typing `code` doesn't mean "code" on the device. It means "code" on your computer, right here, right now.
>
> The **only** way to get code running on a device is to:
> 1. Connect to it (`nano.connect`, `nano.user.select`, etc.)
> 2. Wrap the code you want to run *on the device* inside a block, and pass it to `nano.run`
>
> ```
> "hello from my PC" ?              # runs on your PC — always
> { "hello from the device" ? } nano.run   # runs on the device — only because of nano.run
> ```
>
> Everything outside a `nano.run` block — variables, loops, `if`/`then`, file I/O, even other `nano.*` primitives like `nano.scan` or `nano.user.select` — is regular MOGWAI running locally on your machine. It's a full scripting language in its own right, and you'll use it to *orchestrate* what happens on the device (deciding which one to connect to, what to send, when, and what to do with the result) — not to run things on the device itself. Only the contents of a `nano.run` block ever leave your PC.

### A more comfortable way to write code: `edit`

Typing multi-line programs directly at the console prompt gets old fast. The `edit` command opens a full-screen editor for a much more comfortable workflow:

```
edit
```

Inside the editor:

| Shortcut | Action |
|---|---|
| `F5` | Run the current code, then return to a clean console showing the result |
| `Ctrl+N` | New file |
| `Ctrl+O` | Open a file |
| `Ctrl+W` | Save |
| `Ctrl+A` | Save as |
| `Ctrl+Q` | Quit the editor |

`F5` is the workflow you'll use constantly: it closes the editor, runs your code (locally, or on a device if it contains a `nano.run` block), shows you the result, and — once you press any key — brings you right back to the editor with your code still there, ready for the next tweak. If you try to quit or open another file with unsaved changes, you'll be prompted to save first.

This is how every example in this guide was actually written and tested — write the block in `edit`, hit `F5`, watch it run, adjust, repeat.

## 3. Find your device

You don't need to know your device's IP address. Ask `nano.user.select` to discover it on the network:

```
nano.user.select -> 'device'
if (device ->type .record ==) then { device->ip: nano.connect ? }
```

`nano.user.select` runs a network scan on its own, then shows you every device that responded — name, platform, and IP — and lets you pick one:

```
0: DEVICE1              - 192.168.1.75 - ESP32_REV3
1: DEVICE2              - 192.168.1.80 - ESP32_REV3
Select device number (enter only = abort): 0
```

If you pick one, its scan record (device name, version, session, IP, platform, target, OEM, firmware version) is pushed onto the stack. If nothing responds, or you just press Enter to abort, `null` is pushed instead.

The example checks the type of what's on top of the stack (`.record`) rather than just testing for null — MOGWAI type names are dot-prefixed literals (`.record`, `.string`, `.number`...) that compare directly against `->type`. This guarantees the code that follows really has a proper record with an `ip:` key to work with, rather than just "not null" — a device record could theoretically be null for other reasons than a failed selection, so checking the exact expected type is the more robust habit to build.

If you already know the IP, you can skip discovery entirely:

```
"192.168.1.75" nano.connect
```

Check the connection at any time with:

```
nano.isConnected ?
```

### Scripted discovery (no user interaction)

`nano.user.select` is built for interactive use — it always prompts for input. If you're writing a script that should decide on its own (for example: "if a specific device is on the network, connect to it automatically"), use `nano.scan` directly instead. It returns the raw list of records without ever asking anything, so you can filter it programmatically:

```
nano.scan foreach 'd' do
{
    if (d name: get "MogwaiNanoDevice" ==) then
    {
        d ip: get nano.connect
        break
    }
}
```

## 4. Run your first program on the device

Everything you want to execute *on the device* goes inside a code block, passed to `nano.run`:

```
{ "Hello from the device!" ? } nano.run
```

Behind the scenes, MOGWAI NANO Studio desugars this block into canonical RPN and sends it over the network. `nano.run` only waits long enough to confirm the program has actually started on the device — it does **not** wait for it to finish, and it does **not** show any output. By default, console output (`?`/`console.print`) and `debug.write` messages coming from the device are silently discarded, whether the program was launched via `nano.run` or is running as a stored autorun program.

To actually watch a device's live output, use `nano.user.view`:

```
{ 1000 wait 1 10 for 'i' do { i ? 100 wait } } nano.run nano.user.view
```

```
──── Start view mode (press CTRL-C to exit) ─────────────
1
2
3
4
5
6
7
8
9
10
MOGWAI NANO
OK
execution time 00:00:02.2900000
──── Exit view mode ──────────────────────────────────
OK
execution time 00:00:04.3724066
```

`nano.user.view` attaches to the currently running program and streams its console/debug output to your screen in real time. Press `Ctrl+C` to detach and return to the prompt — the program keeps running on the device regardless, `nano.user.view` only affects whether you're watching it or not.

Notice the `1000 wait` at the very start of the program: attaching `nano.user.view` right after `nano.run` still takes a brief moment over the network, so a short initial delay gives it time to attach before the program starts printing — otherwise you could miss the first few lines.

## 5. Blink an LED

Wire an LED (with a current-limiting resistor, ~220Ω for a red LED on 3.3V) between a free GPIO pin and GND. We'll use pin 5 here — adjust to whichever pin you wired.

```
{
    5 gpio.setMode.output
    forever do
    {
        5 gpio.write.high
        500 wait
        5 gpio.write.low
        500 wait
    }
} nano.run
```

The LED should now be blinking, entirely controlled by code running on the device. `Ctrl+C` here would only interrupt your local MOGWAI script on the PC — since this example's `nano.run` call has already returned, there's nothing local left running to interrupt. To actually stop the program running *on the device*, use `nano.halt`:

```
nano.halt
```

## 6. React to a button press

Wire a push button between another GPIO pin (say, pin 4) and GND, using the device's internal pull-up resistor — no external resistor needed.

```
{
    4 gpio.setMode.inputPullUp
    5 gpio.setMode.output

    to 'onButtonChange' do
    {
        if (eventData pin: get 4 ==) then
        {
            if (eventData eventType: get 0 ==)
            then { 5 gpio.write.high }
            else { 5 gpio.write.low }
        }
    }

    onEvent 'GPIO_PIN_CHANGED' do { onButtonChange }

    forever do { }
} nano.run
```

Pressing the button now lights the LED; releasing it turns it off. The `eventData` variable is automatically populated by the runtime with the pin number and new value whenever a subscribed GPIO event fires — you never have to poll the pin yourself.

> Note the `forever do { }` at the end: without it, the program would reach its natural end and the event subscription would stop existing. An empty infinite loop is a common and cheap way to keep a program — and its event subscriptions — alive.

## 7. Add a recurring timer

Timers run independently of your main program, on their own schedule:

```
{
    to 'heartbeat' do { "still alive" ? }

    timer 'T1' every 5000 do { heartbeat }
    'T1' timer.start

    forever do { 250 wait }
} nano.run
nano.user.view
```

Every 5 seconds, `"still alive"` is printed — interleaved with whatever else the program is doing — regardless of what the main `forever do` loop is up to. As with any device output, you need `nano.user.view` running to actually see it; `Ctrl+C` to detach whenever you like, the timer keeps firing on the device either way.

## 8. Talk to an I2C device

Wire an I2C device to your board's SDA/SCL pins — a real-time clock module (DS3231) is used here, a cheap and common way to add battery-backed timekeeping to a project.

I2C devices are opened with a name, a bus number, and a 7-bit address — the name is what you'll use afterward, so you never have to repeat the bus/address pair on every call:

```
{
    'RTC' 1 0x68 i2c.open

    'RTC' 0x00 D:00 i2c.register.write
    5000 wait
    'RTC' 0x00 1 i2c.register.read bcd-> ?

    'RTC' i2c.close
} nano.run
nano.user.view
```

This writes `0` to the RTC's seconds register, waits 5 seconds, then reads that same register back. RTC chips store time values in **BCD** (binary-coded decimal) rather than plain binary — `bcd->` converts a BCD-encoded number to a regular one (the opposite direction, `->bcd`, exists too). Without it, you'd see the raw encoded byte rather than a readable number; here, the output is `5`.

Not sure what's out there on the bus? `i2c.scan` probes the standard address range (`0x08` to `0x77`) and returns the ones that responded:

```
{ 1 i2c.scan ? } nano.run
```

The other I2C primitives — `i2c.write`, `i2c.read` — work the same way as their `register` counterparts, but operate on the device directly rather than a specific register; useful for devices that don't follow the register-addressed pattern.

I2C writes aren't limited to single bytes — a `MOGData` of any size can be sent in one call, useful for devices like OLED displays that need a whole frame buffer written at once. `newData` creates a zero-initialized `MOGData` of a given size directly, which is the safer choice for larger buffers on lower-RAM devices (building the same buffer by pushing hundreds of individual values with `repeat` before converting with `->data` works fine on more capable boards, but can run out of memory on smaller ones):

```
{
    'OLED' 1 0x3C i2c.open

    # ... display initialization sequence omitted ...

    1024 newData -> 'buffer'
    'OLED' 0x40 &buffer i2c.register.write   # clears the entire 128x64 frame buffer in one transaction

    'OLED' i2c.close
} nano.run
```

## 9. Make it survive without a PC connected

Once you're happy with a program, you can store it on the device so it runs automatically on every boot — no MOGWAI NANO Studio connection required afterward:

```
{
    5 gpio.setMode.output
    forever do { 5 gpio.write.high 500 wait 5 gpio.write.low 500 wait }
} nano.autorun.set
```

`nano.autorun.set` only **stores** the code — it doesn't start running it. It will run automatically the next time the device boots, but not before. If you want it to start right away rather than waiting for the next power cycle, follow it with a reboot:

```
nano.autorun.set
nano.reboot
```

From then on, the device blinks the LED on its own every time it's powered on — even without WiFi, since the code is already stored on the device.

To check what's currently stored, or remove it:

```
nano.autorun.get      # returns the stored code as a MOGCode block
nano.autorun.purge    # clears it
```

## 10. Naming your device and checking its free memory

Once you have more than one device on your network, telling them apart by IP alone gets tedious. Give a device a persistent name — it survives reboots, and shows up as the `device` field in future `nano.scan`/`nano.user.select` results:

```
nano.name.set "Greenhouse Sensor"
nano.name ?
```

Every device starts out named `"MogwaiNanoDevice"` until you set something else.

You can also check how much RAM is currently free on the device — useful when writing long-running scripts, or just to keep an eye on things:

```
nano.memory ?
```

This is a lightweight, non-blocking query (it doesn't force a garbage collection on the device the way `mogwai.memory` does from within a running program) — safe to call frequently, even in a polling loop.

If your program is running *on the device* itself (typically as a stored autorun program, with no Studio connection to fall back on), `mogwai.info` gives you everything in a single call — a record with the device's system version, IP, name, platform, session, free memory, target, MOGWAI NANO version, OEM build details, and the device's skills:

```
{ 1000 wait mogwai.info ? } nano.run
nano.user.view
```

```
[system: "1.17.0.334" ip: "192.168.1.75" name: "DEVICE1" platform: "ESP32" session: "39122" memory: 49872 target: "ESP32_REV3" mogwai: "0.2.0.0" oem: "MinSizeRel build, chip rev. >= 3, without support for PSRAM" skills: ("GPIO" "I2C")]
```

As with the earlier `nano.user.view` example, the `1000 wait` gives the view mode time to fully attach before the program prints anything — skip it and you risk missing the very first output. Without `nano.user.view` at all, nothing from `?`/`console.print` is displayed, `mogwai.info` included.

## 11. Reboot cleanly

If your program needs to reboot the device itself (for example, after applying a new configuration), call `mogwai.reboot` from within the running script. If you've defined a `MOGWAI.onReboot` function, it runs first, giving you a chance to clean up:

```
{
    to 'MOGWAI.onReboot' do { "Rebooting, bye!" ? }
    mogwai.reboot
} nano.run
```

(as always, `"Rebooting, bye!"` would only be visible if you had `nano.user.view` attached)

If you need to force a reboot or halt from MOGWAI NANO Studio *without* running any device-side code — for example, if a device seems stuck — use `nano.reboot` or `nano.halt` instead. These act immediately and bypass `MOGWAI.onReboot` entirely.

This is particularly useful when connecting to a device that's already busy — for example, one running a stored autorun program from the moment it booted. `nano.state` would report `RUNNING`, and `nano.run` would refuse to start anything new (raising an error) until that program stops. `nano.halt` stops it immediately, bringing the device back to `IDLE` and ready for your next `nano.run`:

```
nano.halt
nano.state ?
```

## 12. Putting it all together

Here's a complete, self-contained script that ties together everything covered so far: it checks whether you're already connected, discovers and connects to a device if not (handling both a failed connection and an aborted selection), then runs a program and watches its output.

```
mogwai.reset
console.clear

if (nano.isConnected not) then
{
    nano.user.select -> 'device'

    if (device ->type .record ==) then
    {
        "" ?
        "Connecting to selected device..." ?

        if (device->ip: nano.connect not) then
        {
            "Connection error !" ?
            mogwai.exit
        }
        else
        {
            "Device connected." ?
            "" ?
        }
    }
    else
    {
        "" ?
        "Connection aborted !" ?
        mogwai.exit
    }
}

{
    1000 wait
    1 10 for 'i' do
    {
        i ?
        500 wait
    }
}

guard
{
    nano.run
    nano.user.view
}
else
{
    "" ?
    "Unable to run or view !" ?
}
```

This is entirely regular MOGWAI code — `if`/`then`/`else`, `mogwai.exit` for early exit on failure, `guard`/`else` to catch a failure in `nano.run`/`nano.user.view` itself (for example, if the device drops off the network right as the script tries to run something) — orchestrating the connection and the discovery UI on your PC, with only the small inner block ever actually running on the device. A good pattern to reuse and adapt as your own scripts grow.

### The shortcut version

Everything in the connection block above — scan, list, select, connect — is exactly what `nano.user.connect` does in a single call:

```
if (nano.isConnected not) then { nano.user.connect }
```

Same guided experience, same `true`/`false` outcome as `nano.connect`, one line instead of the block spelled out above. Now that you've seen what it does under the hood, use whichever fits your script better.

## What's next

- Browse the [shared primitives reference](shared-primitives.md) for everything common to MOGWAI and MOGWAI NANO
- Check the [CHANGELOG](../CHANGELOG.md) for the full list of NANO-specific primitives (`gpio.*`, `timer.*`, `nano.*`, event handling)
- I2C, SPI, PWM and ADC support is on the roadmap — GPIO is fully available today
