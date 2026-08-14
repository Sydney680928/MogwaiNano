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
            Debug.WriteLine("Program did end");
            Debug.WriteLine(result.ToString());
            Debug.WriteLine($"MEM={GC.Run(false)}");

            var msg = new ServerMessage("MogwaiNanoDevice", "PROGRAM.DID.END", result.ToString());
            AppGlobal.TcpServer.EnqueueMessage(msg);
        }

        public void ProgramStart(MogwaiNanoEngine engine, string code)
        {
            Debug.WriteLine("Program did start");

            var msg = new ServerMessage("MogwaiNanoDevice", "PROGRAM.DID.START");
            AppGlobal.TcpServer.EnqueueMessage(msg);

        }

        public EvalResult DebugMessage(MogwaiNanoEngine engine, string message)
        {
            Debug.WriteLine(message);

            if (AppGlobal.TcpServer.IsClientConnected)
            {
                var msg = new ServerMessage("MogwaiNanoDevice", "DEBUG.WRITE", message);
                AppGlobal.TcpServer.EnqueueMessage(msg);
            }

            return EvalResult.NoError;
        }

        public EvalResult ConsolePrint(MogwaiNanoEngine engine, string message)
        {
            if (AppGlobal.TcpServer.IsClientConnected)
            {
                var msg = new ServerMessage("MogwaiNanoDevice", "CONSOLE.PRINT", message);
                AppGlobal.TcpServer.EnqueueMessage(msg);
            }

            return EvalResult.NoError;
        }

        public EvalResult MessageReceivedFromRuntime(MogwaiNanoEngine engine, string message, MOGObject parameter)
        {
            Debug.WriteLine($"Message received from runtime: {message}");

            if (message == "GO!")
            {
                AppGlobal.ComPort.WriteLine("HELLO FROM MOGWAI NANO!");
            }

            return EvalResult.NoError;
        }
    }
}
