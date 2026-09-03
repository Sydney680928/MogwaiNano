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
using MogwaiNano.Objects;
using nanoFramework.Networking;
using nanoFramework.Runtime.Native;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading;
using GC = nanoFramework.Runtime.Native.GC;

namespace MogwaiNano
{
    public class Program
    {
        public static void Main()
        {
            Power.OnRebootEvent += Power_OnRebootEvent;

            Debug.WriteLine("MOGWAI NANO");
            Debug.WriteLine($"Version {AppGlobal.MogwaiNanoEngine.Version}");
            Debug.WriteLine("(c) 2026 Stéphane Sibué");

            AppGlobal.NanoParameters = NanoParameters.Load(AppGlobal.PARAMETERS_FILE);

            Debug.WriteLine($"Device name: {AppGlobal.NanoParameters.Name}");
            Debug.WriteLine($"Session: {AppGlobal.Session}");

            try
            {
                Directory.CreateDirectory(@"I:\mogwai\units");
                Debug.WriteLine($"Units folder OK.");  
            }
            catch
            {
                Debug.WriteLine($"Unabled to create units folder !");              
            }

            if (ConnectToWifi())
            {
                AppGlobal.TcpServer.MessageReceived += TcpServer_MessageReceived;

                AppGlobal.TcpServer.StartTcpServer();

                AppGlobal.UdpServer.StartUdpServer();
            }

            var @delegate = new MogwaiNanoDelegate(AppGlobal.MogwaiNanoEngine);
            AppGlobal.MogwaiNanoEngine.Delegate = @delegate;

            Debug.WriteLine($"Memory: {GC.Run(true)} bytes free.");

            if (File.Exists(@"I:\autorun.mog"))
            {
                Debug.WriteLine("autorun.mog found, running...");

                string code = File.ReadAllText(@"I:\autorun.mog");
                AppGlobal.MogwaiNanoEngine.RunAsync(code);
            }
            else
            {
                Debug.WriteLine("Ready.");
            }

            Thread.Sleep(Timeout.Infinite);
        }

        private static void Power_OnRebootEvent()
        {
            var message = new ServerMessage(AppGlobal.NanoParameters.Name, "REBOOTING");
            AppGlobal.TcpServer.EnqueueMessage(message);

            Thread.Sleep(1000);
        }

        private static void TcpServer_MessageReceived(ServerMessage message)
        {
            if (message.Function == "RUN")
            {
                var code = message.Parameters[0];
                AppGlobal.MogwaiNanoEngine.RunAsync(code);
            }
            else if (message.Function == "AUTORUN.SET")
            {
                // On stocke le code dans I:\autorun.mogwai pour qu'il soit exécuté au démarrage

                var code = message.Parameters[0];
                File.WriteAllText(AppGlobal.AUTORUN_FILE, code);

                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "AUTORUN.SET", "OK"));
            }
            else if (message.Function == "AUTORUN.GET")
            {
                if (File.Exists(AppGlobal.AUTORUN_FILE))
                {
                    string code = File.ReadAllText(AppGlobal.AUTORUN_FILE);
                    AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "AUTORUN.GET", code));
                }
                else
                {
                    AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "AUTORUN.GET", ""));
                }
            }
            else if (message.Function == "AUTORUN.PURGE")
            {
                if (File.Exists(AppGlobal.AUTORUN_FILE))
                    File.Delete(AppGlobal.AUTORUN_FILE);

                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "AUTORUN.PURGE", "OK"));
            }
            else if (message.Function == "HALT")
            {
                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "HALT", "REQUESTED"));
                AppGlobal.MogwaiNanoEngine.Halt();
            }
            else if (message.Function == "REBOOT")
            {
                Thread.Sleep(1000);
                Power.RebootDevice(5000, RebootOption.NormalReboot);
            }
            else if (message.Function == "STATE.GET")
            {
                if (AppGlobal.MogwaiNanoEngine.IsRunning)
                {
                    AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "STATE.GET", "RUNNING"));
                }
                else
                {
                    AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "STATE.GET", "IDLE"));
                }
            }
            else if (message.Function == "MEMORY.GET")
            {
                var memory = GC.Run(false);
                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "MEMORY.GET", memory.ToString()));
            }
            else if (message.Function == "NAME.GET")
            {
                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "NAME.GET", AppGlobal.NanoParameters.Name));
            }
            else if (message.Function == "NAME.SET")
            {
                if (message.Parameters.Length > 0 && message.Parameters[0] != null)
                {
                    var newName = message.Parameters[0];
                    AppGlobal.NanoParameters.Name = newName;
                    AppGlobal.NanoParameters.Save(AppGlobal.PARAMETERS_FILE);
                }

                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "NAME.SET", "OK"));
            }
            else if (message.Function == "SEND")
            {
                if (message.Parameters.Length > 0 && message.Parameters[0] != null)
                {
                    var payload = message.Parameters[0];
                    AppGlobal.MogwaiNanoEngine.FireEvent("STUDIO_DID_SEND", new MOGString(AppGlobal.MogwaiNanoEngine, payload));
                }

                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "SEND", "OK"));
            }
            else if (message.Function == "SESSION.GET")
            {
                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "SESSION.GET", AppGlobal.Session.ToString()));
            }
            else if (message.Function == "INFO.GET")
            {
                var memory = GC.Run(false);

                var skillsBuilder = new StringBuilder();

                foreach (var skill in AppGlobal.MogwaiNanoEngine.Skills)
                {
                    if (skillsBuilder.Length > 0)
                        skillsBuilder.Append('\n');

                    skillsBuilder.Append(skill);
                }

                var unitsBuilder = new StringBuilder();

                foreach (var unit in AppGlobal.MogwaiNanoEngine.Units)
                {
                    if (unitsBuilder.Length > 0)
                        unitsBuilder.Append('\n');

                    unitsBuilder.Append(unit);
                }

                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(
                        AppGlobal.NanoParameters.Name,
                        "INFO.GET",
                        AppGlobal.NanoParameters.Name,
                        AppGlobal.MogwaiNanoEngine.Version.ToString(),
                        AppGlobal.IpAddress,
                        AppGlobal.Session.ToString(),
                        SystemInfo.Platform,
                        SystemInfo.TargetName,
                        SystemInfo.OEMString,
                        SystemInfo.Version.ToString(),
                        memory.ToString(),
                        skillsBuilder.ToString(),
                        AppGlobal.MogwaiNanoEngine.FrugalMode.ToString(),
                        unitsBuilder.ToString()
                    ));
            }
            else if (message.Function == "LAST.RESULT.GET")
            {
                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "LAST.RESULT.GET", AppGlobal.MogwaiNanoEngine.LastResult.ToString()));
            }
            else if (message.Function == "PING")
            {
                Debug.WriteLine("PING received / send PONG");
                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "PONG"));
            }
            else if (message.Function == "UNITS.INSTALL")
            {
                // On stocke le code dans I:\unitName

                if (message.Parameters.Length < 2)
                {
                    AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "UNITS.INSTALL", "ERROR", "Missing parameters"));
                    return;
                }

                var unitName = message.Parameters[0];
                var code = message.Parameters[1];
                var filename = $@"I:\mogwai\units\{unitName}";

                try
                {

                    File.WriteAllText(filename, code);
                    AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "UNITS.INSTALL", "OK"));
                }
                catch (Exception ex)
                {
                    AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "UNITS.INSTALL", "ERROR", ex.Message));
                }
            }
            else if (message.Function == "UNITS.LIST")
            {
                try
                {
                    var units = AppGlobal.MogwaiNanoEngine.Units;

                    var sb = new StringBuilder();

                    for (int i = 0; i < units.Length; i++)
                    {
                        if (sb.Length > 0)
                            sb.Append("\n");

                        sb.Append(units[i]);
                    }

                    AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "UNITS.LIST", "OK", sb.ToString()));
                }
                catch (Exception ex)
                {
                    AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "UNITS.LIST", "ERROR", ex.Message));
                }
            }
            else if (message.Function == "UNITS.PURGE")
            {
                if (message.Parameters.Length > 0 && message.Parameters[0] != null)
                {
                    var filename = Path.Combine(@"I:\mogwai\units",  message.Parameters[0]);

                    if (File.Exists(filename))
                    {
                        try
                        {
                            File.Delete(filename);
                            AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "UNITS.PURGE", "OK"));
                        }
                        catch (Exception ex)
                        {
                            AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "UNITS.PURGE", "ERROR", ex.Message));
                            return;
                        }
                    }
                    else
                    {
                        AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "UNITS.PURGE", "ERROR", "Unit not found"));
                    }
                }
                else
                {
                    AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(AppGlobal.NanoParameters.Name, "UNITS.PURGE", "ERROR", "Missing parameters"));
                }       
            }
        }

        private static bool ConnectToWifi()
        {
            Debug.Write("Connecting to WiFi... ");

            CancellationTokenSource cs = new(60000);

            var success = WifiNetworkHelper.Reconnect(requiresDateTime: true, token: cs.Token);

            if (!success)
            {
                Debug.WriteLine();
                Debug.WriteLine("No WiFi configuration found or connection failed.");
                
                return false;
            }
            else
            {
                AppGlobal.IpAddress = NetworkInterface.GetAllNetworkInterfaces()[0].IPv4Address;
                Debug.WriteLine($"OK @ {AppGlobal.IpAddress}");
            }

            return true;
        }
    }
}
