using MogwaiNano.Engine;
using System;
using System.IO.Ports;
using System.Reflection;

namespace MogwaiNano
{
    public static class AppGlobal
    {
        public const int TCP_PORT = 9597;

        public const int DISCOVERY_PORT = 1968;

        public const string EXPECTED_SOURCE = "STUDIO_NANO";

        public const string DEVICE_NAME = "MogwaiNanoDevice";

        public static MogwaiNanoEngine MogwaiNanoEngine { get; } = new MogwaiNanoEngine();

        public static Random RandomGenerator { get; } = new();

        public static SerialPort ComPort { get; set; }

        public static UdpServer UdpServer { get; } = new();

        public static TcpServer TcpServer { get; } = new();

        public static int Session { get; } = RandomGenerator.Next(100000);
    }
}
