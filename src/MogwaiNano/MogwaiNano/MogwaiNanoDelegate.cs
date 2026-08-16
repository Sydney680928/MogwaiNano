using MogwaiNano.Engine;
using MogwaiNano.Interfaces;
using MogwaiNano.Objects;
using System.Diagnostics;
using GC = nanoFramework.Runtime.Native.GC;

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
            var msg = new ServerMessage(AppGlobal.DEVICE_NAME, "PROGRAM.DID.END", result.ToString());
            AppGlobal.TcpServer.EnqueueMessage(msg);
        }

        public void ProgramStart(MogwaiNanoEngine engine, string code)
        {
            var msg = new ServerMessage(AppGlobal.DEVICE_NAME, "PROGRAM.DID.START");
            AppGlobal.TcpServer.EnqueueMessage(msg);

        }

        public EvalResult DebugMessage(MogwaiNanoEngine engine, string message)
        {
            if (AppGlobal.TcpServer.IsClientConnected)
            {
                var msg = new ServerMessage(AppGlobal.DEVICE_NAME, "DEBUG.WRITE", message);
                AppGlobal.TcpServer.EnqueueMessage(msg);
            }

            return EvalResult.NoError;
        }

        public EvalResult ConsolePrint(MogwaiNanoEngine engine, string message)
        {
            if (AppGlobal.TcpServer.IsClientConnected)
            {
                var msg = new ServerMessage(AppGlobal.DEVICE_NAME, "CONSOLE.PRINT", message);
                AppGlobal.TcpServer.EnqueueMessage(msg);
            }

            return EvalResult.NoError;
        }
    }
}
