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

        public const string PARAMETERS_FILE = @"I:\nano_parameters.json";

        public const string AUTORUN_FILE = @"I:\autorun.mog";

        public static MogwaiNanoEngine MogwaiNanoEngine { get; set; }
        public static Random RandomGenerator { get; set; }
        public static SerialPort ComPort { get; set; }
        public static UdpServer UdpServer { get; set; }
        public static TcpServer TcpServer { get; set; }
        public static int Session { get; set; }
        public static NanoParameters NanoParameters { get; set; }
        public static string IpAddress { get; set; } = "?.?.?.?";

        public static void Initialize()
        {
            RandomGenerator = new Random();
            Session = RandomGenerator.Next(100000);
            UdpServer = new UdpServer();
            TcpServer = new TcpServer();
            NanoParameters = new NanoParameters();
            MogwaiNanoEngine = new MogwaiNanoEngine("RootMogwaiNanoEngine");
        }
    }
}
