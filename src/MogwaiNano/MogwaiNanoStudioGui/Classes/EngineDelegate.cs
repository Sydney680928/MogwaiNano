// Copyright 2026 Stéphane Sibué
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using MOGWAI.Engine;
using MOGWAI.Interfaces;
using MOGWAI.Objects;
using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace MogwaiNanoStudioGui.Classes
{
    internal class EngineDelegate : IDelegate
    {
        public delegate void NanoConnectEventHandler(string name, string address);
        public event NanoConnectEventHandler? NanoConnect;

        public delegate void NanoRunEventHandler(string code);
        public event NanoRunEventHandler? NanoRun;

        public delegate void NanoRequestTcpConnectionEventHandler(string host, int port);
        public event NanoRequestTcpConnectionEventHandler? NanoRequestTcpConnection;

        // Console output of the desktop MOGWAI engine itself (the Studio-side
        // orchestration script, not the NANO device) — see
        // ConsolePrintLn/ConsolePrint below, which raise these events.
        public event Action<string>? ConsoleLineReceived;
        public event Action<string>? ConsoleTextReceived;
        public event Action? ConsoleClearRequested;

        // "Debug" output of the desktop MOGWAI engine itself (see DebugMessage
        // below) — the counterpart of NanoDebugWrite on the device side.
        public event Action<string>? DebugMessageReceived;
        public event Action? DebugClearRequested;

        // console.show — a standard primitive that already calls into
        // ConsoleShow(engine) below; switches to the Console MOGWAI tab.
        public event Action? ConsoleShowRequested;

        // nano.console.show / nano.debug.show / nano.console.clear /
        // nano.debug.clear — extended primitives (not part of the standard
        // IDelegate interface), acting on the device-side Console/Debug
        // NANO tabs rather than the desktop engine's own MOGWAI ones.
        public event Action? NanoConsoleShowRequested;
        public event Action? NanoDebugShowRequested;
        public event Action? NanoConsoleClearRequested;
        public event Action? NanoDebugClearRequested;

        // Owner window for modal dialogs (Prompt) — set once by MainWindow
        // at its own construction.
        public Window? OwnerWindow { get; set; }

        // Fired when the engine signals the actual end of a program's
        // execution — used as the point to display the result rather than
        // relying on RunAsync's Task returning, whose ordering relative to
        // the program's last internal ConsolePrintLn/ConsolePrint calls isn't guaranteed.
        public event Action<EvalResult>? ProgramEnded;

        private MogwaiEngine _engine;

        private string _text = string.Empty;
        private string _filename = string.Empty;

        private string Filename
        {
            get => _filename;
            set
            {
                _filename = value ?? string.Empty;
            }
        }

        public EngineDelegate(MogwaiEngine engine)
        {
            _engine = engine;
        }

        public string[] HostFunctions(MogwaiEngine engine) => [
            "?s",
            "run",

            "nano.run",
            "nano.connect",
            "nano.disconnect",
            "nano.isConnected",
            "nano.scan",           
            "nano.state",
            "nano.isRunning",
            "nano.halt",
            "nano.session",
            "nano.lastResult",
            "nano.memory",
            "nano.name",
            "nano.name.set",
            "nano.info",
            "nano.reboot",
            "nano.send",

            "nano.console.show",
            "nano.debug.show",
            "nano.console.clear",
            "nano.debug.clear",

            "nano.autorun.set",
            "nano.autorun.get",
            "nano.autorun.purge",
            
            "nano.user.select",
            "nano.user.connect",  
            "nano.user.connect?",

            "nano.units.install",
            "nano.units",
            "nano.units.purge",

            "mogwai.memory",
            "mogwai.reboot",
            "mogwai.frugalMode",
            "mogwai.units",
            "mogwai.units.run",

            "bcd->",
            "->bcd",
            "makeData",

            "gpio.setMode.input",
            "gpio.setMode.inputPullDown",
            "gpio.setMode.inputPullUp",
            "gpio.setMode.output",
            "gpio.read",
            "gpio.write.high",
            "gpio.write.low",
            "gpio.toggle",
            "gpio.close",

            "i2c.open",
            "i2c.close",
            "i2c.write",
            "i2c.register.write",
            "i2c.read",
            "i2c.register.read",
            "i2c.scan",

            "ssd1306.init",
            "ssd1306.close",
            "ssd1306.clear",
            "ssd1306.refresh",
            "ssd1306.printString",
            "ssd1306.drawString",
            "ssd1306.drawPixel",
            "ssd1306.drawHorizontalLine",
            "ssd1306.drawVerticalLine",
            "ssd1306.drawRectangle",    
            "ssd1306.drawFilledRectangle",
            "ssd1306.drawBitmap",

            "pwm.open",
            "pwm.close",
            "pwm.start",
            "pwm.stop",

            "adc.open",
            "adc.close",
            "adc.read",
            "adc.resolutionInBits",
            "adc.maxValue",

            "spi.open",
            "spi.close",
            "spi.write",
            "spi.read",
            "spi.transfer",
            "spi.minClockFrequency",
            "spi.maxClockFrequency",

            "stopwatch.create",
            "stopwatch.start",
            "stopwatch.stop",
            "stopwatch.reset",
            "stopwatch.elapsed",
            "stopwatch.reset",
            "stopwatch.isRunning",
            "stopwatch.purge",

            "device.setPinFunction"

            ];

        public async Task<EvalResult> ExecuteHostFunction(MogwaiEngine engine, string word)
        {
            if (word == "run")
            {
                var s = engine.StackSign(1);

                if (s.Count == 0)
                    return EvalResult.Failure(engine, Error.TooFewArgumentsError, word);

                if (s[0] == typeof(MOGString))
                {
                    var codeFile = engine.StackPop() as MOGString;

                    try
                    {
                        var bytes = File.ReadAllBytes(codeFile!.Value);
                        var result = engine.GetCodeFormBytes(bytes);

                        if (result.code != null)
                        {
                            return await engine.RunAsync(result.code, false);
                        }
                        else
                        {
                            return EvalResult.Failure(engine, Error.ParseError, word);
                        }
                    }
                    catch
                    {
                        return EvalResult.Failure(engine, Error.FileOperationError, word);
                    }
                }

                return EvalResult.Failure(engine, Error.BadArgumentTypeError, word);
            }
            else if (word == "nano.run")
            {
                // { ... } nano.run
                // "filename" nano.run

                var s = engine.StackSign(1);

                if (s.Count == 0)
                    return EvalResult.Failure(engine, Error.TooFewArgumentsError, word);

                if (s[0] == typeof(MOGString))
                {
                    // run filename

                    var filename = engine.StackPopString();

                    try
                    {
                        var code = File.ReadAllText(filename.Value);

                        if (!string.IsNullOrEmpty(code))
                        {
                            var function = new MOGFunction(AppGlobal.MogwaiEngine, code, 0, null);
                            return await AppGlobal.NanoRuntime.RunAsync(function.ToStringCode());
                        }
                        else
                        {
                            return EvalResult.Failure(engine, Error.BadArgumentValueError, word, "empty code provided");
                        }
                    }
                    catch (Exception ex)
                    {
                        return EvalResult.Failure(engine, Error.ParseError, word, ex.Message);
                    }
                }
                else if (s[0] == typeof(MOGCode))
                {
                    // run { ... }

                    var code = engine.StackPopCode();
                    return await AppGlobal.NanoRuntime.RunAsync(code.ToStringCode());
                }

                return EvalResult.Failure(engine, Error.BadArgumentTypeError, word);
            }
            else if (word == "nano.connect")
            {
                // "IP" nano.connect
                // "192.168.1.75" nano.connect

                var s = engine.StackSign(1);

                if (s.Count == 0)
                    return EvalResult.Failure(engine, Error.TooFewArgumentsError, word);

                if (s[0] == typeof(MOGString))
                {
                    var ip = engine.StackPopString();

                    try
                    {
                        AppGlobal.NanoClient.Connect(ip.Value, AppGlobal.TCP_PORT);

                        var name = await AppGlobal.NanoRuntime.GetNameValue();

                        NanoConnect?.Invoke(name ?? "unknown name", ip.Value);

                        engine.StackPushBoolean(true);
                    }
                    catch
                    {
                        engine.StackPushBoolean(false);
                    }

                    return EvalResult.NoError;
                }

                return EvalResult.Failure(engine, Error.BadArgumentTypeError, word);
            }
            else if (word == "nano.disconnect")
            {
                AppGlobal.NanoClient.Disconnect();
                return EvalResult.NoError;
            }
            else if (word == "nano.isConnected")
            {
                _engine.StackPushBoolean(AppGlobal.NanoClient.IsConnected);
                return EvalResult.NoError;
            }
            else if (word == "nano.scan")
            {
                var list = AppGlobal.NanoClient.Scan(_engine);
                _engine.StackPush(list);

                return EvalResult.NoError;
            }
            else if (word == "nano.user.select")
            {
                // nano.user.select

                return await SelectDeviceViaDialog();
            }
            else if (word == "nano.user.connect")
            {
                // nano.user.connect = nano.user.select + nano.connect
                // true if connected, false if not or no device selected

                //Console.WriteLine();
                //Console.WriteLine("MOGWAI NANO DEVICES ON THE NETWORK");

                var r = await SelectDeviceViaDialog();

                if (r != EvalResult.NoError)
                    return r;

                var s = _engine.StackSign(1);

                if (s.Count == 0)
                    return EvalResult.Failure(_engine, Error.TooFewArgumentsError, word);

                if (s[0] == typeof(MOGNull))
                {
                    // no device or no device selected or user canceled selection

                    _engine.StackPushBoolean(false);
                    return EvalResult.NoError;
                }
                else if (s[0] == typeof(MOGRecord))
                {
                    var record = _engine.StackPopRecord();

                    // ip: key is mandatory, value is the IP address of the device

                    var ip = record.GetItem("ip") as MOGString;

                    if (ip == null)
                        return EvalResult.Failure(_engine, Error.BadArgumentValueError, word, "ip: key is mandatory");

                    try
                    {
                        AppGlobal.NanoClient.Connect(ip.Value, AppGlobal.TCP_PORT);

                        var name = await AppGlobal.NanoRuntime.GetNameValue();

                        NanoConnect?.Invoke(name ?? "unknown name", ip.Value);

                        engine.StackPushBoolean(true);
                    }
                    catch
                    {
                        _engine.StackPushBoolean(false);
                    }

                    return EvalResult.NoError;
                }
            }
            else if (word == "nano.user.connect?")
            {
                // nano.user.connect? = nano.user.select + nano.connect si aucun device déjà connecté                
                // true if connected, false if not or no device selected

                if (AppGlobal.NanoClient.IsConnected)
                {
                    _engine.StackPushBoolean(true);
                    return EvalResult.NoError;
                }

                var r = await SelectDeviceViaDialog();

                if (r != EvalResult.NoError)
                    return r;

                var s = _engine.StackSign(1);

                if (s.Count == 0)
                    return EvalResult.Failure(_engine, Error.TooFewArgumentsError, word);

                if (s[0] == typeof(MOGNull))
                {
                    // no device or no device selected or user canceled selection

                    _engine.StackPushBoolean(false);
                    return EvalResult.NoError;
                }
                else if (s[0] == typeof(MOGRecord))
                {
                    var record = _engine.StackPopRecord();

                    // ip: key is mandatory, value is the IP address of the device

                    var ip = record.GetItem("ip") as MOGString;

                    if (ip == null)
                        return EvalResult.Failure(_engine, Error.BadArgumentValueError, word, "ip: key is mandatory");

                    try
                    {
                        AppGlobal.NanoClient.Connect(ip.Value, AppGlobal.TCP_PORT);

                        var name = await AppGlobal.NanoRuntime.GetNameValue();

                        NanoConnect?.Invoke(name ?? "unknown name", ip.Value);

                        engine.StackPushBoolean(true);
                    }
                    catch
                    {
                        _engine.StackPushBoolean(false);
                    }

                    return EvalResult.NoError;
                }
            }
            else if (word == "nano.state")
            {
                return await AppGlobal.NanoRuntime.GetState();
            }
            else if (word == "nano.memory")
            {
                return await AppGlobal.NanoRuntime.GetMemory();
            }
            else if (word == "nano.info")
            {
                return await AppGlobal.NanoRuntime.GetInfo();
            }
            else if (word == "nano.name")
            {
                return await AppGlobal.NanoRuntime.GetName();
            }
            else if (word == "nano.name.set")
            {
                var s = engine.StackSign(1);

                if (s.Count == 0)
                    return EvalResult.Failure(engine, Error.TooFewArgumentsError, word);

                if (s[0] == typeof(MOGString))
                {
                    var name = engine.StackPopString();
                    return await AppGlobal.NanoRuntime.SetName(name.Value);
                }

                return EvalResult.Failure(engine, Error.BadArgumentTypeError, word);
            }
            else if (word == "nano.session")
            {
                return await AppGlobal.NanoRuntime.GetSession();
            }
            else if (word == "nano.lastResult")
            {
                return await AppGlobal.NanoRuntime.GetLastResult();
            }
            else if (word == "nano.isRunning")
            {
                return await AppGlobal.NanoRuntime.StateIsRunning();
            }
            else if (word == "nano.autorun.get")
            {
                return await AppGlobal.NanoRuntime.GetAutorunAsync();
            }
            else if (word == "nano.autorun.set")
            {
                // { ... } nano.setAutorun
                // "filename" nano.setAutorun

                var s = engine.StackSign(1);

                if (s.Count == 0)
                    return EvalResult.Failure(engine, Error.TooFewArgumentsError, word);

                if (s[0] == typeof(MOGString))
                {
                    // filename

                    var filename = engine.StackPopString();

                    try
                    {
                        var code = File.ReadAllText(filename.Value);

                        if (!string.IsNullOrEmpty(code))
                        {
                            var function = new MOGFunction(AppGlobal.MogwaiEngine, code, 0, null);
                            return await AppGlobal.NanoRuntime.SetAutorunAsync(function.ToStringCode());
                        }
                        else
                        {
                            return EvalResult.Failure(engine, Error.BadArgumentValueError, word, "empty code provided");
                        }
                    }
                    catch (Exception ex)
                    {
                        return EvalResult.Failure(engine, Error.FatalError, word, ex.Message);
                    }
                }
                else if (s[0] == typeof(MOGCode))
                {
                    // { ... }

                    var code = engine.StackPopCode();
                    return await AppGlobal.NanoRuntime.SetAutorunAsync(code.ToStringCode());
                }

                return EvalResult.Failure(engine, Error.BadArgumentTypeError, word);
            }
            else if (word == "nano.autorun.purge")
            {
                return await AppGlobal.NanoRuntime.PurgeAutorunAsync();
            }
            else if (word == "nano.halt")
            {
                try
                {
                    var message = new ServerMessage(AppGlobal.SOURCE_NAME, "HALT");
                    AppGlobal.NanoClient.SendMessage(message);
                    return EvalResult.NoError;
                }
                catch
                {
                    return EvalResult.Failure(engine, MogwaiNanoErrors.DeviceUnreachableError);
                }
            }
            else if (word == "nano.reboot")
            {
                try
                {
                    var message = new ServerMessage(AppGlobal.SOURCE_NAME, "REBOOT");
                    AppGlobal.NanoClient.SendMessage(message);
                    return EvalResult.NoError;
                }
                catch
                {
                    return EvalResult.Failure(engine, MogwaiNanoErrors.DeviceUnreachableError);
                }
            }
            else if (word == "nano.send")
            {
                var s = engine.StackSign(1);

                if (s.Count == 0)
                    return EvalResult.Failure(engine, Error.TooFewArgumentsError, word);

                if (s[0] == typeof(MOGString))
                {
                    var payload = engine.StackPopString();
                    return await AppGlobal.NanoRuntime.Send(payload.Value);
                }

                return EvalResult.Failure(engine, Error.BadArgumentTypeError, word);
            }
            else if (word == "nano.console.show")
            {
                NanoConsoleShowRequested?.Invoke();
                return EvalResult.NoError;
            }
            else if (word == "nano.debug.show")
            {
                NanoDebugShowRequested?.Invoke();
                return EvalResult.NoError;
            }
            else if (word == "nano.console.clear")
            {
                NanoConsoleClearRequested?.Invoke();
                return EvalResult.NoError;
            }
            else if (word == "nano.debug.clear")
            {
                NanoDebugClearRequested?.Invoke();
                return EvalResult.NoError;
            }
            else if (word == "nano.units.install")
            {
                // "filename" nano.units.install

                var s = engine.StackSign(1);

                if (s.Count == 0)
                    return EvalResult.Failure(engine, Error.TooFewArgumentsError, word);

                if (s[0] == typeof(MOGString))
                {
                    // filename

                    var filename = engine.StackPopString();
                    string code;

                    try
                    {
                        code = File.ReadAllText(filename.Value);
                    }
                    catch (Exception ex)
                    {
                        return EvalResult.Failure(engine, Error.FileOperationError, word, ex.Message);
                    }

                    if (!string.IsNullOrEmpty(code))
                    {
                        MOGFunction function;

                        var unitName = Path.GetFileName(filename.Value);

                        try
                        {
                            function = new MOGFunction(AppGlobal.MogwaiEngine, code, 0, null);
                        }
                        catch (Exception ex)
                        {
                            return EvalResult.Failure(engine, Error.ParseError, word, ex.Message);
                        }

                        return await AppGlobal.NanoRuntime.InstallUnitAsync(unitName, function.ToStringCode());
                    }
                    else
                    {
                        return EvalResult.Failure(engine, Error.BadArgumentValueError, word, "empty code provided");
                    }
                }

                return EvalResult.Failure(engine, Error.BadArgumentTypeError, word);
            }
            else if (word == "nano.units")
            {
                return await AppGlobal.NanoRuntime.GetUnits();
            }
            else if (word == "nano.units.purge")
            {
                // 'unit' nano.units.purge

                var s = engine.StackSign(1);

                if (s.Count == 0)
                    return EvalResult.Failure(engine, Error.TooFewArgumentsError, word);

                if (s[0] == typeof(MOGName))
                {
                    var unitName = engine.StackPopName();
                    return await AppGlobal.NanoRuntime.PurgeUnitAsync(unitName.Value);
                }

                return EvalResult.Failure(engine, Error.BadArgumentTypeError, word);
            }

            return EvalResult.NoExternalFunction;
        }

        // ─── Console ─────────────────────────────────────────────────────────

        private object _ConsoleAccessLocker = new();

        public Task ProgramStart(MogwaiEngine engine, string code) => Task.CompletedTask;

        public Task ProgramEnd(MogwaiEngine engine, EvalResult result)
        {
            ProgramEnded?.Invoke(result);
            return Task.CompletedTask;
        }

        public Task<EvalResult> ConsoleClearScreen(MogwaiEngine engine)
        {
            lock (_ConsoleAccessLocker)
                ConsoleClearRequested?.Invoke();

            return Task.FromResult(EvalResult.NoError);
        }

        public Task<EvalResult> ConsolePrintLn(MogwaiEngine engine, string message)
        {
            lock (_ConsoleAccessLocker)
                ConsoleLineReceived?.Invoke(message);

            return Task.FromResult(EvalResult.NoError);
        }

        public Task<EvalResult> ConsolePrint(MogwaiEngine engine, string message)
        {
            lock (_ConsoleAccessLocker)
                ConsoleTextReceived?.Invoke(message);

            return Task.FromResult(EvalResult.NoError);
        }

        public Task<EvalResult> ConsoleShow(MogwaiEngine engine)
        {
            lock (_ConsoleAccessLocker)
                ConsoleShowRequested?.Invoke();

            return Task.FromResult(EvalResult.NoError);
        }

        public Task<EvalResult> ConsoleHide(MogwaiEngine engine)
            => Task.FromResult(EvalResult.NoError);

        public Task<EvalResult> ConsoleLocate(MogwaiEngine engine, int x, int y)
        {
            /*
            lock (_ConsoleAccessLocker)
                Console.SetCursorPosition(x, y);
            */

            return Task.FromResult(EvalResult.NoError);
        }

        public Task<(EvalResult result, int x, int y)> ConsoleGetCursorPosition(MogwaiEngine engine)
        {
            /*
            var r = Console.GetCursorPosition();
            return Task.FromResult((EvalResult.NoError, r.Left, r.Top));
            */
            
            return Task.FromResult((EvalResult.NoError, 0, 0));
        }

        public Task<EvalResult> ConsoleSetForegroundColor(MogwaiEngine engine, string color)
        {
            /*
            lock (_ConsoleAccessLocker)
                switch (color.ToLower())
                {
                    case "black": Console.ForegroundColor = ConsoleColor.Black; break;
                    case "blue": Console.ForegroundColor = ConsoleColor.Blue; break;
                    case "cyan": Console.ForegroundColor = ConsoleColor.Cyan; break;
                    case "gray": Console.ForegroundColor = ConsoleColor.Gray; break;
                    case "green": Console.ForegroundColor = ConsoleColor.Green; break;
                    case "magenta": Console.ForegroundColor = ConsoleColor.Magenta; break;
                    case "red": Console.ForegroundColor = ConsoleColor.Red; break;
                    case "white": Console.ForegroundColor = ConsoleColor.White; break;
                    case "yellow": Console.ForegroundColor = ConsoleColor.Yellow; break;
                    default: break;
                }
            */
            
            return Task.FromResult(EvalResult.NoError);
        }

        public Task<EvalResult> ConsoleSetBackgroundColor(MogwaiEngine engine, string color)
        {
            /*
            lock (_ConsoleAccessLocker)
                switch (color.ToLower())
                {
                    case "black": Console.BackgroundColor = ConsoleColor.Black; break;
                    case "blue": Console.BackgroundColor = ConsoleColor.Blue; break;
                    case "cyan": Console.BackgroundColor = ConsoleColor.Cyan; break;
                    case "gray": Console.BackgroundColor = ConsoleColor.Gray; break;
                    case "green": Console.BackgroundColor = ConsoleColor.Green; break;
                    case "magenta": Console.BackgroundColor = ConsoleColor.Magenta; break;
                    case "red": Console.BackgroundColor = ConsoleColor.Red; break;
                    case "white": Console.BackgroundColor = ConsoleColor.White; break;
                    case "yellow": Console.BackgroundColor = ConsoleColor.Yellow; break;
                    default: break;
                }
            */

            return Task.FromResult(EvalResult.NoError);
        }

        public Task<(EvalResult result, int key)> ConsoleGetInputKey(MogwaiEngine engine)
        {
            int key = -1;

            /*

            lock (_ConsoleAccessLocker)
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(true);
                    key = (int)keyInfo.Key;
                }
            }

            */

            return Task.FromResult((EvalResult.NoError, key));
        }

        // Used by console.input / console.prompt — opens a modal single-line
        // dialog. Neither closing it, nor Cancel, nor an empty submission
        // ever return null: always a string (empty if nothing was entered).
        public async Task<(EvalResult result, string? value)> Prompt(MogwaiEngine engine, string message)
        {
            string value = string.Empty;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new InputDialog(message);
                var result = await dialog.ShowDialog<string?>(OwnerWindow);
                value = result ?? string.Empty;
            });

            return (EvalResult.NoError, value);
        }

        // Replaces MogwaiNanoRuntime.Select() (console-based, makes no sense
        // at all in a graphical app) with the same scan/selection dialog as
        // MainWindow's "Connect..." button. Reproduces Select()'s exact
        // contract: pushes MOGNull (no device found, or selection
        // canceled) or a full MOGRecord — same keys as Scan().
        private async Task<EvalResult> SelectDeviceViaDialog()
        {
            ScanDevice? selected = null;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new ScanDevicesWindow();
                await dialog.ShowDialog(OwnerWindow);
                selected = dialog.SelectedDevice;
            });

            if (selected is null)
            {
                _engine.StackPushNull();
                return EvalResult.NoError;
            }

            var record = new MOGRecord(_engine);
            record.SetString("name", selected.Name);
            record.SetString("version", selected.Version);
            record.SetString("session", selected.Session);
            record.SetString("ip", selected.IpAddress);
            record.SetString("platform", selected.GenericPlatform);
            record.SetString("target", selected.Platform);
            record.SetString("OEM", selected.Oem);
            record.SetString("system", selected.System);

            _engine.StackPush(record);
            return EvalResult.NoError;
        }

        public Task<EvalResult> MessageReceivedFromRuntime(MogwaiEngine engine, string message, MOGObject parameter)
            => Task.FromResult(EvalResult.NoError);

        public Task<EvalResult> EngineDidPause(MogwaiEngine engine)
            => Task.FromResult(EvalResult.NoError);

        public Task<EvalResult> EngineDidResume(MogwaiEngine engine)
            => Task.FromResult(EvalResult.NoError);

        public Task<EvalResult> StudioDidConnect(MogwaiEngine engine)
            => Task.FromResult(EvalResult.NoError);

        public Task<EvalResult> StudioDidDisconnect(MogwaiEngine engine)
            => Task.FromResult(EvalResult.NoError);

        public Task<EvalResult> SocketServerDidStart(MogwaiEngine engine, IPAddress address, int port)
            => Task.FromResult(EvalResult.NoError);

        public Task<EvalResult> SocketServerDidStop(MogwaiEngine engine)
            => Task.FromResult(EvalResult.NoError);

        public Task<EvalResult> DebugMessage(MogwaiEngine engine, string message)
        {
            lock (_ConsoleAccessLocker)
                DebugMessageReceived?.Invoke(message);

            return Task.FromResult(EvalResult.NoError);
        }

        public Task<EvalResult> DebugClear(MogwaiEngine engine)
        {
            lock (_ConsoleAccessLocker)
                DebugClearRequested?.Invoke();

            return Task.FromResult(EvalResult.NoError);
        }

        public string[] Skills(MogwaiEngine engine) => ["NANO"];

    }
}
