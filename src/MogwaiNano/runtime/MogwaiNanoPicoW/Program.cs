using System;
using System.Diagnostics;
using System.Threading;

namespace MogwaiNanoPicoW
{
    public class Program
    {
        public static void Main()
        {
            MogwaiNanoCore.MogwaiNanoSystem.Start();

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
