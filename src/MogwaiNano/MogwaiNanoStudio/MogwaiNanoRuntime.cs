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
using MOGWAI.Objects;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace MogwaiNanoStudio
{
    public class MogwaiNanoRuntime
    {
        private const string SOURCE_NAME = "STUDIO_NANO";

        private MogwaiEngine _engine;
        private Timer _pingTimer;

        public delegate void NanoProgramDidStartHandler();
        public event NanoProgramDidStartHandler? NanoProgramDidStart;

        public delegate void NanoProgramDidEndHandler(string result);
        public event NanoProgramDidEndHandler? NanoProgramDidEnd;

        public delegate void NanoDebugWriteHandler(string message);
        public event NanoDebugWriteHandler? NanoDebugWrite;

        public delegate void NanoPrintHandler(string message);
        public event NanoPrintHandler? NanoPrintLn;
        public event NanoPrintHandler? NanoPrint;

        public delegate void NanoSendMessageHandler(string message);
        public event NanoSendMessageHandler? NanoSendMessage;


        public bool IsRunning { get; private set; } = false;

        public bool DisplayMessages { get; set; } = false;

        public bool ViewMode { get; private set; } = false; 

        public bool ExitViewModeRequested { get; set; } = false;    

        public MogwaiNanoRuntime(MogwaiEngine engine)
        {
            _engine = engine;

            _pingTimer = new Timer(_ =>
            {
                try
                {
                    if (AppGlobal.NanoClient.IsConnected)
                        AppGlobal.NanoClient.SendMessage(new ServerMessage(SOURCE_NAME, "PING"));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"PingTimer exception: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                }

            }, null, 10000, 10000);


            AppGlobal.NanoClient.MessageReceived += NanoClient_MessageReceived;

            AppGlobal.NanoClient.Disconnected += NanoClient_Disconnected;
        }

        private void NanoClient_Disconnected(object? sender, EventArgs e)
        {
            // IsRunning = false;

            NanoPrintLn?.Invoke("\ndevice disconnected !\n");
        }

        private void NanoClient_MessageReceived(object? sender, ServerMessage message)
        {
            Debug.WriteLine($"\nMessage received {message}");

            if (message.Function == "PROGRAM.DID.START")
            {
                IsRunning = true;
                NanoProgramDidStart?.Invoke();
            }
            else if (message.Function == "PROGRAM.DID.END")
            {
                IsRunning = false;
                NanoProgramDidEnd?.Invoke(message.Parameters[0]);
            }
            else if (message.Function == "DEBUG.WRITE")
            {
                NanoDebugWrite?.Invoke(message.Parameters[0]);
            }
            else if (message.Function == "CONSOLE.PRINTLN")
            {
                NanoPrintLn?.Invoke(message.Parameters[0]);
            }
            else if (message.Function == "CONSOLE.PRINT")
            {
                NanoPrint?.Invoke(message.Parameters[0]);
            }
            else if (message.Function == "SEND.MESSAGE")
            {
                NanoSendMessage?.Invoke(message.Parameters[0]);
            }
        }

        public bool Halt()
        {
            try
            {
                AppGlobal.NanoClient.SendMessage(new ServerMessage(SOURCE_NAME, "HALT"));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<EvalResult> RunAsync(string code)
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var messageState = new ServerMessage(SOURCE_NAME, "STATE.GET");
            var responseState = await SendMessageAndWaitResponse(messageState);

            if (responseState == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            if (responseState.Parameters[0] != "IDLE")
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceBusyError);

            DisplayMessages = false;

            IsRunning = false;

            try
            {
                var message = new ServerMessage(SOURCE_NAME, "RUN", code);
                AppGlobal.NanoClient.SendMessage(message);
            }
            catch
            {
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);
            }

            var startResponse = await WaitResponse("PROGRAM.DID.START");

            if (startResponse == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            return EvalResult.NoError;
        }

        public async Task<EvalResult> ViewAsync()
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);
                
            Console.WriteLine();
            Console.WriteLine("──── Start view mode (press CTRL-C to exit) ─────────────");
            Console.WriteLine();

            ExitViewModeRequested = false;

            if (IsRunning)
            {                
                DisplayMessages = true;
                ViewMode = true;

                await WaitNanoProgramDidEnd();
            }

            DisplayMessages = false;

            if (!ExitViewModeRequested)
            {
                Console.WriteLine();
                Console.WriteLine();
                
                var messageLastResult = new ServerMessage(SOURCE_NAME, "LAST.RESULT.GET");
                var responseLastResult = await SendMessageAndWaitResponse(messageLastResult);

                if (responseLastResult == null)
                {
                    Console.WriteLine("unabled to get program result !");
                }
                else
                {
                    Console.WriteLine(responseLastResult.Parameters[0]);
                }
            }

            Console.WriteLine();
            Console.WriteLine("──── Exit view mode ──────────────────────────────────");
            Console.WriteLine();

            DisplayMessages = false;
            ViewMode = false;
            ExitViewModeRequested = false;

            return EvalResult.NoError;
        }

        public async Task<EvalResult> SetAutorunAsync(string code)
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var message = new ServerMessage(SOURCE_NAME, "AUTORUN.SET", code);
            var response = await SendMessageAndWaitResponse(message);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            var result = response.Parameters[0] ?? "!";
            _engine.StackPushBoolean(result == "OK");

            return EvalResult.NoError;
        }

        public async Task<EvalResult> PurgeAutorunAsync()
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var message = new ServerMessage(SOURCE_NAME, "AUTORUN.PURGE");
            var response = await SendMessageAndWaitResponse(message);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            var result = response.Parameters[0] ?? "!";
            _engine.StackPushBoolean(result == "OK");

            return EvalResult.NoError;
        }

        public async Task<EvalResult> GetAutorunAsync()
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var message = new ServerMessage(SOURCE_NAME, "AUTORUN.GET");
            var response = await SendMessageAndWaitResponse(message);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            var code = response.Parameters[0] ?? "";

            var allowPrivatePrimitives = _engine.AllowPrivatePrimitives;
            _engine.AllowPrivatePrimitives = true;

            try
            {
                var mogCode = new MOGCode(_engine, code, 0, null);
                _engine.StackPush(mogCode);
                return EvalResult.NoError;
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(_engine, Error.ParseError, ex.Message);
            }
            finally
            {
                _engine.AllowPrivatePrimitives = allowPrivatePrimitives;
            }
        }

        public async Task<EvalResult> GetState()
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var state = await GetStateValue();

            if (state == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            _engine.StackPushString(state);

            return EvalResult.NoError;
        }

        public async Task<EvalResult> GetSession()
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var message = new ServerMessage(SOURCE_NAME, "SESSION.GET");
            var response = await SendMessageAndWaitResponse(message);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            var state = response.Parameters[0] ?? "";
            _engine.StackPushString(state);

            return EvalResult.NoError;
        }

        public async Task<EvalResult> GetLastResult()
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var message = new ServerMessage(SOURCE_NAME, "LAST.RESULT.GET");
            var response = await SendMessageAndWaitResponse(message);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            var state = response.Parameters[0] ?? "";
            _engine.StackPushString(state);

            return EvalResult.NoError;
        }

        public async Task<EvalResult> GetMemory()
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var message = new ServerMessage(SOURCE_NAME, "MEMORY.GET");
            var response = await SendMessageAndWaitResponse(message);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            if (double.TryParse(response.Parameters[0], out double memory))
            {
                _engine.StackPushNumber(memory);
                return EvalResult.NoError;
            }
            else
            {
                return EvalResult.Failure(_engine, Error.BadArgumentValueError, "Invalid memory value received from device.");
            }
        }

        public async Task<EvalResult> GetName()
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var name = await GetNameValue();

            if (name == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            _engine.StackPushString(name);
            return EvalResult.NoError;
        }

        public async Task<string?> GetNameValue()
        {
            var message = new ServerMessage(SOURCE_NAME, "NAME.GET");
            var response = await SendMessageAndWaitResponse(message);

            if (response != null && response.Parameters != null && response.Parameters[0] != null)
            {
                return response.Parameters[0];
            }
            else
            {
                return null;
            }
        }

        public async Task<EvalResult> SetName(string name)
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var message = new ServerMessage(SOURCE_NAME, "NAME.SET", name);
            var response = await SendMessageAndWaitResponse(message);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            if (response.Parameters.Length > 0 && response.Parameters[0] != null && response.Parameters[0] == "OK")
                return EvalResult.NoError;

            return EvalResult.Failure(_engine, Error.BadArgumentValueError);
        }

        public async Task<EvalResult> StateIsRunning()
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var state = await GetStateValue();

            if (state == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            _engine.StackPushBoolean(state == "RUNNING");

            return EvalResult.NoError;
        }

        public async Task<string?> GetStateValue()
        {
            var message = new ServerMessage(SOURCE_NAME, "STATE.GET");
            var response = await SendMessageAndWaitResponse(message);

            if (response != null && response.Parameters != null && response.Parameters[0] != null)
            {
                return response.Parameters[0];
            }
            else
            {
                return null;
            }
        }

        public async Task<EvalResult> GetInfo()
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var message = new ServerMessage(SOURCE_NAME, "INFO.GET");
            var response = await SendMessageAndWaitResponse(message);
            
            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);
            
            if (response.Parameters == null || response.Parameters.Length < 10)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.BadDeviceResponse, "Invalid info response from device.");

            if (int.TryParse(response.Parameters[8], out int memory))
            {
                var record = new MOGRecord(_engine);

                record.SetString("name", response.Parameters[0]);
                record.SetString("mogwai", response.Parameters[1]);
                record.SetString("ip", response.Parameters[2]);
                record.SetString("session", response.Parameters[3]);

                record.SetString("platform", response.Parameters[4]);
                record.SetString("target", response.Parameters[5]);
                record.SetString("oem", response.Parameters[6]);
                record.SetString("system", response.Parameters[7]);

                record.SetNumber("memory", memory);

                var list = new MOGList(_engine);

                if (!string.IsNullOrEmpty(response.Parameters[9]))
                {
                    var sks = response.Parameters[9].Split('\n');

                    foreach (var sk in sks)
                        list.AddName(sk);
                }

                record.SetItem("skills", list);

                record.SetBoolean("frugalMode", response.Parameters[10] == "True");

                _engine.StackPush(record);

                return EvalResult.NoError;
            }
            else
            {
                return EvalResult.Failure(_engine, MogwaiNanoErrors.BadDeviceResponse, "Invalid memory value received from device.");
            }        
        }

        public async Task<EvalResult> InstallUnitAsync(string unitName, string code)
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var message = new ServerMessage(SOURCE_NAME, "UNITS.INSTALL", unitName, code);
            var response = await SendMessageAndWaitResponse(message);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            var result = response.Parameters[0] ?? "!";
            _engine.StackPushBoolean(result == "OK");

            return EvalResult.NoError;
        }
        
        public async Task<EvalResult> GetUnitsList()
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var message = new ServerMessage(SOURCE_NAME, "UNITS.LIST");
            var response = await SendMessageAndWaitResponse(message);

            if (response != null && response.Parameters != null && response.Parameters[0] != null)
            {
                if (response.Parameters[0] == "OK")
                {
                    var unitsList = response.Parameters[1].Split('\n');
                    var mogList = new MOGList(_engine);
                    
                    foreach (var unit in unitsList)
                    {
                        if (!string.IsNullOrWhiteSpace(unit))
                            mogList.AddName(unit);
                    }
                    
                    _engine.StackPush(mogList);
                    
                    return EvalResult.NoError;
                }
                else
                {
                    return EvalResult.Failure(_engine, MogwaiNanoErrors.BadDeviceResponse, "Failed to retrieve units list from device.");
                }
            }
            else
            {
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);
            }
        }

        public async Task<EvalResult> PurgeUnitAsync(string unitName)
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);
            
            var message = new ServerMessage(SOURCE_NAME, "UNITS.PURGE", unitName);
            var response = await SendMessageAndWaitResponse(message);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            var result = response.Parameters[0] ?? "!";
            _engine.StackPushBoolean(result == "OK");

            return EvalResult.NoError;
        }

        public Task<EvalResult> Select()
        {
            var list = AppGlobal.NanoClient.Scan(_engine);

            if (list.Items.Count > 0)
            {
                while (true)
                {
                    Console.WriteLine();

                    var c = 0;

                    foreach (var item in list.Items)
                    {
                        if (item is MOGRecord record && record.Items.ContainsKey("target") && record.Items.ContainsKey("ip"))
                        {
                            var target = record.GetItem("target") as MOGString;
                            var ip = record.GetItem("ip") as MOGString;
                            var name = record.GetItem("name") as MOGString;

                            if (target != null && ip != null && name != null)
                            {
                                var n = name.Value.Length > 30 ? name.Value.Substring(0, 30) : name.Value;
                                Console.WriteLine($"{c}: {n.PadRight(30)} - {ip.Value.PadRight(15)} - {target.Value}");
                                c++;
                            }
                        }
                    }

                    if (c == 0)
                    {
                        _engine.StackPushNull();
                        return Task.FromResult(EvalResult.NoError);
                    }

                    Console.WriteLine("");

                    var ly = Console.CursorTop;

                    while (true)
                    {
                        Console.SetCursorPosition(0, ly);
                        Console.Write(new string(' ', Console.WindowWidth));

                        Console.SetCursorPosition(0, ly);
                        Console.Write("Select device number (enter only = abort): ");

                        var input = Console.ReadLine();

                        if (string.IsNullOrEmpty(input))
                        {
                            _engine.StackPushNull();
                            return Task.FromResult(EvalResult.NoError);
                        }
                        else if (int.TryParse(input, out int index) && index >= 0 && index < c)
                        {
                            _engine.StackPush(list.Items[index]);
                            return Task.FromResult(EvalResult.NoError);
                        }
                    }

                }
            }
            else
            {
                _engine.StackPushNull();
                return Task.FromResult(EvalResult.NoError);
            }
        }

        public async Task<EvalResult> Send(string payload)
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var state = await GetStateValue();

            if (state == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            if (state != "RUNNING")
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceIsNotRunningError);

            var message = new ServerMessage(SOURCE_NAME, "SEND", payload);
            var response = await SendMessageAndWaitResponse(message);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            if (response.Parameters.Length > 0 && response.Parameters[0] != null && response.Parameters[0] == "OK")
                return EvalResult.NoError;

            return EvalResult.Failure(_engine, Error.BadArgumentValueError);
        }

        private async Task WaitNanoProgramDidEnd()
        {
            while (IsRunning)
            {
                await Task.Delay(100);

                if (ExitViewModeRequested || _engine.HaltRequested)
                {
                    DisplayMessages = false;
                    return;
                }

                var r = await _engine.Yield();

                if (r.IsError)
                {
                    DisplayMessages = false;
                    return;
                }
            }
        }

        private async Task<ServerMessage?> WaitResponse(string function, int timeout = 15000)
        {
            var deadline = DateTime.Now.AddMilliseconds(timeout);
            ServerMessage? messageReceived = null;

            void Handler(object? sender, ServerMessage message)
            {
                if (message.Function == function)
                    messageReceived = message;
            }

            AppGlobal.NanoClient.MessageReceived += Handler;

            while (DateTime.Now < deadline && messageReceived == null)
                await Task.Delay(50);

            AppGlobal.NanoClient.MessageReceived -= Handler;

            return messageReceived;
        }

        private async Task<ServerMessage?> SendMessageAndWaitResponse(ServerMessage sourceMessage, int timeout = 15000)
        {
            try
            {
                AppGlobal.NanoClient.SendMessage(sourceMessage);
                return await WaitResponse(sourceMessage.Function, timeout);
            }
            catch
            {
                return null;
            }
        }
    }
}
