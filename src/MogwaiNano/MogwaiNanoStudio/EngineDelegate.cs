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
using System.Net;
using Terminal.Gui;

namespace MogwaiNanoStudio
{
    internal class EngineDelegate : IDelegate
    {
        public delegate void NanoConnectEventHandler(string name, string address);
        public event NanoConnectEventHandler? NanoConnect;

        public delegate void NanoRunEventHandler(string code);
        public event NanoRunEventHandler? NanoRun;

        public delegate void NanoRequestTcpConnectionEventHandler(string host, int port);
        public event NanoRequestTcpConnectionEventHandler? NanoRequestTcpConnection;

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
            "file.edit",
            "file.select",

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

            "nano.autorun.set",
            "nano.autorun.get",
            "nano.autorun.purge",
            
            "nano.user.select",
            "nano.user.view",
            "nano.user.connect",          

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
            else if (word == "file.edit")
            {
                var s = engine.StackSign(1);

                if (s.Count == 0)
                    return EvalResult.Failure(engine, Error.TooFewArgumentsError, word);

                if (s[0] == typeof(MOGString))
                {
                    var @string = engine.StackPopString();
                    var filename = string.Empty;

                    try
                    {
                        filename = Path.GetFullPath(@string.Value);
                        _text = File.ReadAllText(filename);
                        Filename = filename;

                        return EvalResult.NoError;
                    }
                    catch
                    {
                        return EvalResult.Failure(engine, Error.FileOperationError, word, filename);
                    }
                }

                return EvalResult.Failure(engine, Error.BadArgumentTypeError, word);
            }
            else if (word == "file.select")
            {
                Application.Init();

                var openDialog = new OpenDialog("Open", "");
                openDialog.DirectoryPath = _engine.ProgramsDirectory;
                openDialog.AllowsMultipleSelection = false;

                Application.Run(openDialog);

                if (!openDialog.Canceled && openDialog.FilePaths.Count > 0)
                {
                    string filename = openDialog.FilePaths[0];
                    engine.StackPushString(filename);

                    Application.Shutdown();

                    return EvalResult.NoError;
                }

                engine.StackPush(new MOGNull(engine));

                Application.Shutdown();

                return EvalResult.NoError;
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

                return await AppGlobal.NanoRuntime.Select();
            }
            else if (word == "nano.user.connect")
            {
                // nano.user.connect = nano.user.select + nano.connect
                // true if connected, false if not or no device selected

                Console.WriteLine();
                Console.WriteLine("MOGWAI NANO DEVICES ON THE NETWORK");

                var r = await AppGlobal.NanoRuntime.Select();

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
            else if (word == "nano.user.view")
            {
                return await AppGlobal.NanoRuntime.ViewAsync();
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

        public Task ProgramEnd(MogwaiEngine engine, EvalResult result) => Task.CompletedTask;

        public Task<EvalResult> ConsoleClearScreen(MogwaiEngine engine)
        {
            lock (_ConsoleAccessLocker)
                Console.Clear();

            return Task.FromResult(EvalResult.NoError);
        }

        public Task<EvalResult> ConsolePrintLn(MogwaiEngine engine, string message)
        {
            lock (_ConsoleAccessLocker)
                Console.WriteLine(message);

            return Task.FromResult(EvalResult.NoError);
        }

        public Task<EvalResult> ConsolePrint(MogwaiEngine engine, string message)
        {
            lock (_ConsoleAccessLocker)
                Console.Write(message);

            return Task.FromResult(EvalResult.NoError);
        }

        public Task<EvalResult> ConsoleShow(MogwaiEngine engine)
            => Task.FromResult(EvalResult.NoError);

        public Task<EvalResult> ConsoleHide(MogwaiEngine engine)
            => Task.FromResult(EvalResult.NoError);

        public Task<EvalResult> ConsoleLocate(MogwaiEngine engine, int x, int y)
        {
            lock (_ConsoleAccessLocker)
                Console.SetCursorPosition(x, y);

            return Task.FromResult(EvalResult.NoError);
        }

        public Task<(EvalResult result, int x, int y)> ConsoleGetCursorPosition(MogwaiEngine engine)
        {
            var r = Console.GetCursorPosition();
            return Task.FromResult((EvalResult.NoError, r.Left, r.Top));
        }

        public Task<EvalResult> ConsoleSetForegroundColor(MogwaiEngine engine, string color)
        {
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

            return Task.FromResult(EvalResult.NoError);
        }

        public Task<EvalResult> ConsoleSetBackgroundColor(MogwaiEngine engine, string color)
        {
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

            return Task.FromResult(EvalResult.NoError);
        }

        public Task<(EvalResult result, int key)> ConsoleGetInputKey(MogwaiEngine engine)
        {
            int key = -1;

            lock (_ConsoleAccessLocker)
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(true);
                    key = (int)keyInfo.Key;
                }
            }

            return Task.FromResult((EvalResult.NoError, key));
        }

        public Task<(EvalResult result, string? value)> Prompt(MogwaiEngine engine, string message)
        {
            lock (_ConsoleAccessLocker)
            {
                Console.Write(message);
                var r = Console.ReadLine();
                return Task.FromResult((EvalResult.NoError, r));
            }
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
            => Task.FromResult(EvalResult.NoError);

        public Task<EvalResult> DebugClear(MogwaiEngine engine)
            => Task.FromResult(EvalResult.NoError);

        public string[] Skills(MogwaiEngine engine) => ["TERMINAL", "NANO"];

    }
}
