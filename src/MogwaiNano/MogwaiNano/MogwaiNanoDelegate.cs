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

using MogwaiNano.Engine;
using MogwaiNano.Interfaces;

namespace MogwaiNano
{
    public class MogwaiNanoDelegate : IDelegate
    {
        MogwaiNanoEngine _engine;

        public MogwaiNanoDelegate(MogwaiNanoEngine engine)
        {
            _engine = engine;
        }

        public void ProgramEnd(MogwaiNanoEngine engine, EvalResult result)
        {
            var msg = new ServerMessage(AppGlobal.NanoParameters.Name, "PROGRAM.DID.END", result.ToString());
            AppGlobal.TcpServer.EnqueueMessage(msg);
        }

        public void ProgramStart(MogwaiNanoEngine engine, string code)
        {
            var msg = new ServerMessage(AppGlobal.NanoParameters.Name, "PROGRAM.DID.START");
            AppGlobal.TcpServer.EnqueueMessage(msg);

        }

        public EvalResult DebugMessage(MogwaiNanoEngine engine, string message)
        {
            if (AppGlobal.TcpServer.IsClientConnected)
            {
                var msg = new ServerMessage(AppGlobal.NanoParameters.Name, "DEBUG.WRITE", message);
                AppGlobal.TcpServer.EnqueueMessage(msg);
            }

            return EvalResult.NoError;
        }

        public EvalResult ConsolePrint(MogwaiNanoEngine engine, string message)
        {
            if (AppGlobal.TcpServer.IsClientConnected)
            {
                var msg = new ServerMessage(AppGlobal.NanoParameters.Name, "CONSOLE.PRINT", message);
                AppGlobal.TcpServer.EnqueueMessage(msg);
            }

            return EvalResult.NoError;
        }
    }
}
