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
using System.Text;

namespace MogwaiNanoStudio
{
    public class MogwaiNanoRuntime
    {
        private const string SOURCE_NAME = "STUDIO_NANO";

        private DateTime _lastAliveReceived;
        private MogwaiEngine _engine;
        private Timer _pingTimer;

        public delegate void NanoProgramDidStartHandler();
        public event NanoProgramDidStartHandler? NanoProgramDidStart;

        public delegate void NanoProgramDidEndHandler(string result);
        public event NanoProgramDidEndHandler? NanoProgramDidEnd;

        public delegate void NanoDebugWriteHandler(string message);
        public event NanoDebugWriteHandler? NanoDebugWrite;

        public delegate void NanoPrintHandler(string message);
        public event NanoPrintHandler? NanoPrint;

        public bool IsRunning { get; private set; } = false;

        public bool ListenMessages { get; set; } = false;

        public bool ViewMode { get; private set; } = false; 

        public bool ExitViewModeRequested { get; set; } = false;    

        public MogwaiNanoRuntime(MogwaiEngine engine)
        {
            _engine = engine;

            _pingTimer = new Timer(_ =>
            {
                if (AppGlobal.NanoClient.IsConnected)
                    AppGlobal.NanoClient.SendMessage(new ServerMessage(SOURCE_NAME, "PING"));
            
            }, null, 10000, 10000);

            AppGlobal.NanoClient.MessageReceived += NanoClient_MessageReceived;

            AppGlobal.NanoClient.Disconnected += NanoClient_Disconnected;
        }

        private void NanoClient_Disconnected(object? sender, EventArgs e)
        {
            IsRunning = false;
        }

        private void NanoClient_MessageReceived(object? sender, ServerMessage message)
        {
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
            else if (message.Function == "CONSOLE.PRINT")
            {
                NanoPrint?.Invoke(message.Parameters[0]);
            }
            else if (message.Function == "ALIVE")
            {
                _lastAliveReceived = DateTime.Now;
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

            ListenMessages = false;

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

            // On attend que le programme démarre

            var startResponse = await WaitResponse("PROGRAM.DID.START");

            if (startResponse == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            return EvalResult.NoError;
        }

        public async Task<EvalResult> ViewAsync()
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var messageState = new ServerMessage(SOURCE_NAME, "STATE.GET");
            var responseState = await SendMessageAndWaitResponse(messageState);

            if (responseState == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            if (responseState.Parameters[0] != "RUNNING")
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceIsNotRunningError);

            Console.WriteLine();
            Console.WriteLine("──── Start view mode (press CTRL-C to exit) ─────────────");
            Console.WriteLine();

            IsRunning = true;   
            ExitViewModeRequested = false;
            ListenMessages = true;
            ViewMode = true;

            // On attend que le programme se termine

            if (! await WaitNanoProgramDidEnd())
            {
                ListenMessages = false;
                ViewMode = false;
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);
            }

            // On affiche le résultat final du programme si on n'est pas simplement sorti du mode view

            if (!ExitViewModeRequested)
            {
                var messageLastResult = new ServerMessage(SOURCE_NAME, "LAST.RESULT.GET");
                var responseLastResult = await SendMessageAndWaitResponse(messageLastResult);

                if (responseLastResult == null)
                    return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

                Console.WriteLine();
                Console.WriteLine(responseLastResult.Parameters[0]);
            }

            Console.WriteLine();
            Console.WriteLine("──── Exit view mode ──────────────────────────────────");
            Console.WriteLine();

            ListenMessages = false;
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

            var message = new ServerMessage(SOURCE_NAME, "STATE.GET");
            var response = await SendMessageAndWaitResponse(message);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            var state = response.Parameters[0] ?? "";
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

            var message = new ServerMessage(SOURCE_NAME, "NAME.GET");
            var response = await SendMessageAndWaitResponse(message);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            if (response.Parameters.Length > 0 && response.Parameters[0] != null)
            {
                _engine.StackPushString(response.Parameters[0]);
                return EvalResult.NoError;
            }

            return EvalResult.Failure(_engine, Error.BadArgumentValueError, "No name value received from device.");
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

            var message = new ServerMessage(SOURCE_NAME, "STATE.GET");
            var response = await SendMessageAndWaitResponse(message);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            var state = response.Parameters[0] ?? "";
            _engine.StackPushBoolean(state == "RUNNING");

            return EvalResult.NoError;
        }

        public async Task<EvalResult> GetInfo()
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var message = new ServerMessage(SOURCE_NAME, "INFO.GET");
            var response = await SendMessageAndWaitResponse(message);
            
            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);
            
            if (response.Parameters == null || response.Parameters.Length < 8)
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

                _engine.StackPush(record);

                return EvalResult.NoError;
            }
            else
            {
                return EvalResult.Failure(_engine, MogwaiNanoErrors.BadDeviceResponse, "Invalid memory value received from device.");
            }        
        }

        public Task<EvalResult> Select()
        {
            var list = AppGlobal.NanoClient.Scan(_engine);

            if (list.Items.Count > 0)
            {
                while (true)
                {
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
                                Console.WriteLine($"{c}: {name.Value.PadRight(20)} - {ip.Value} - {target.Value.PadRight(20)}");
                                c++;
                            }
                        }
                    }

                    if (c == 0)
                    {
                        // Aucun device valide dans la liste, on retourne null

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
                            // ESC

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
                // Aucun device dans la liste, on retourne null

                _engine.StackPushNull();
                return Task.FromResult(EvalResult.NoError);
            }
        }

        private async Task<bool> WaitNanoProgramDidEnd(int timeout = 12000)
        {
            _lastAliveReceived = DateTime.Now;

            while (IsRunning)
            {
                await Task.Delay(100);

                if (ExitViewModeRequested)
                {
                    ListenMessages = false;
                    return true;
                }

                if (_engine.HaltRequested)
                {
                    ListenMessages = false;
                    return true;
                }

                var r = await _engine.Yield();

                if (r.IsError)
                    return false;

                var interval = DateTime.Now - _lastAliveReceived;

                if (interval.TotalMilliseconds >= timeout)
                {
                    //ListenMessages = false;
                    //IsRunning = false;
                    //return false;
                }
            }

            return true;
        }

        private async Task<ServerMessage?> WaitResponse(string function, int timeout = 5000)
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

        private async Task<ServerMessage?> SendMessageAndWaitResponse(ServerMessage sourceMessage, int timeout = 5000)
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
