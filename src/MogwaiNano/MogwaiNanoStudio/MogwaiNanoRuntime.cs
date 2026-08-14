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

        public MogwaiNanoRuntime(MogwaiEngine engine)
        {
            _engine = engine;

            _pingTimer = new Timer(_ =>
            {
                if (AppGlobal.NanoClient.IsConnected)
                {
                    Debug.WriteLine("Sending PING to Nano...");
                    AppGlobal.NanoClient.SendMessage(new ServerMessage(SOURCE_NAME, "PING"));
                }
                else
                {
                    Debug.WriteLine("Nano is not connected, skipping PING.");
                }
            
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
            if (true) // _listenMessages)
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
            var responseState = await SendMessageAndWaitResponse(messageState, 1000);

            if (responseState == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            if (responseState.Parameters[0] != "IDLE")
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceBusyError);

            ListenMessages = false;

            IsRunning = false;

            // On passe le code en base64 pour éviter le bug json coté nano avec les \"

            var code64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(code));

            try
            {
                var message = new ServerMessage(SOURCE_NAME, "RUN", code64);
                AppGlobal.NanoClient.SendMessage(message);
            }
            catch
            {
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);
            }

            // On attend que le programme démarre

            var startResponse = await WaitResponse("PROGRAM.DID.START");

            if (startResponse == null)
            {
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);
            }

            return EvalResult.NoError;
        }

        public async Task<EvalResult> ViewAsync()
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var messageState = new ServerMessage(SOURCE_NAME, "STATE.GET");
            var responseState = await SendMessageAndWaitResponse(messageState, 1000);

            if (responseState == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            if (responseState.Parameters[0] != "RUNNING")
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceIsNotRunningError);

            Console.WriteLine();
            Console.WriteLine("──── Start view mode (press ESC to exit) ─────────────");
            Console.WriteLine();

            ListenMessages = true;
            IsRunning = true;

            // On attend que le programme se termine

            if (!await WaitNanoProgramDidEnd())
            {
                ListenMessages = false;
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);
            }

            // On affiche le résultat final du programme

            var messageLastResult = new ServerMessage(SOURCE_NAME, "LAST.RESULT.GET");
            var responseLastResult = await SendMessageAndWaitResponse(messageLastResult, 2000);

            if (responseLastResult == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            Console.WriteLine();
            Console.WriteLine(responseLastResult.Parameters[0]);    

            Console.WriteLine();
            Console.WriteLine("──── Exit view mode ──────────────────────────────────");
            Console.WriteLine();

            return EvalResult.NoError;
        }

        public async Task<EvalResult> SetAutorunAsync(string code)
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            // On passe le code en base64 pour éviter le bug json coté nano avec les \"

            var code64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(code));

            var message = new ServerMessage(SOURCE_NAME, "AUTORUN.SET", code64);
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
            var response = await SendMessageAndWaitResponse(message, 1000);

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
            var response = await SendMessageAndWaitResponse(message, 1000);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            var code = response.Parameters[0] ?? "";

            byte[] decoded = Convert.FromBase64String(code);
            code = Encoding.UTF8.GetString(decoded, 0, decoded.Length);

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
            var response = await SendMessageAndWaitResponse(message, 2000);

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
            var response = await SendMessageAndWaitResponse(message, 2000);

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
            var response = await SendMessageAndWaitResponse(message, 2000);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            var state = response.Parameters[0] ?? "";
            _engine.StackPushString(state);

            return EvalResult.NoError;
        }

        public async Task<EvalResult> StateIsRunning()
        {
            if (!AppGlobal.NanoClient.IsConnected)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceNotConnectedError);

            var message = new ServerMessage(SOURCE_NAME, "STATE.GET");
            var response = await SendMessageAndWaitResponse(message, 1000);

            if (response == null)
                return EvalResult.Failure(_engine, MogwaiNanoErrors.DeviceUnreachableError);

            var state = response.Parameters[0] ?? "";
            _engine.StackPushBoolean(state == "RUNNING");

            return EvalResult.NoError;
        }
        
        private async Task<bool> WaitNanoProgramDidEnd(int timeout = 12000)
        {
            _lastAliveReceived = DateTime.Now;

            while (IsRunning)
            {
                if (_engine.HaltRequested)
                {
                    ListenMessages = false;
                    return true;
                }

                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Escape)
                {
                    IsRunning = false;
                    return true;
                }
                
                await Task.Delay(100);

                var r = await _engine.Yield();

                if (r.IsError)
                    return false;

                var interval = DateTime.Now - _lastAliveReceived;

                if (interval.TotalMilliseconds >= timeout)
                {
                    ListenMessages = false;
                    IsRunning = false;

                    AppGlobal.NanoClient.Disconnect();

                    return false;
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
