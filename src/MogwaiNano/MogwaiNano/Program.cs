using MogwaiNano.Engine;
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
        private const int TCP_PORT = 5200;
        private const string DEVICE_NAME = "MogwaiNanoDevice";

        public static void Main()
        {
            Power.OnRebootEvent += Power_OnRebootEvent;

            Debug.WriteLine("MOGWAI NANO");
            Debug.WriteLine($"Version {AppGlobal.MogwaiNanoEngine.Version}");
            Debug.WriteLine("(c) 2026 Stéphane Sibué");
            Debug.WriteLine($"Session {AppGlobal.Session}");

            if (!ConnectToWifi())
            {
                Debug.WriteLine("Impossible de se connecter au WiFi. Arrêt du programme.");
                return;
            }

            AppGlobal.TcpServer.MessageReceived += TcpServer_MessageReceived;

            AppGlobal.TcpServer.StartTcpServer();

            AppGlobal.UdpServer.StartUdpServer();

            var @delegate = new MogwaiNanoDelegate(AppGlobal.MogwaiNanoEngine);
            AppGlobal.MogwaiNanoEngine.Delegate = @delegate;

            Debug.WriteLine($"MEMORY={GC.Run(true)}");

            if (File.Exists(@"I:\autorun.mog"))
            {
                Debug.WriteLine("autorun.mog found. Executing...");

                string code = File.ReadAllText(@"I:\autorun.mog");
                AppGlobal.MogwaiNanoEngine.RunAsync(code);
            }

            Thread.Sleep(Timeout.Infinite);
        }

        private static void Power_OnRebootEvent()
        {
            var message = new ServerMessage(DEVICE_NAME, "REBOOTING");
            AppGlobal.TcpServer.EnqueueMessage(message);

            Thread.Sleep(1000);
        }

        private static void TcpServer_MessageReceived(ServerMessage message)
        {
            if (message.Function == "RUN")
            {
                var code = message.Parameters[0];

                Debug.WriteLine("RUN command received");
                Debug.WriteLine(code);
                Debug.WriteLine("Executing code...");

                AppGlobal.MogwaiNanoEngine.RunAsync(code);
            }
            else if (message.Function == "AUTORUN.SET")
            {
                // On stocke le code dans I:\autorun.mogwai pour qu'il soit exécuté au démarrage
                // Le code est encodé en base64 pour éviter les problèmes d'encodage

                var code = message.Parameters[0];

                File.WriteAllText(@"I:\autorun.mog", code);
                Debug.WriteLine($"autorun.mog written.");

                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(DEVICE_NAME, "AUTORUN.SET", "OK"));
            }
            else if (message.Function == "AUTORUN.GET")
            {
                if (File.Exists(@"I:\autorun.mog"))
                {
                    string code = File.ReadAllText(@"I:\autorun.mog");
                    AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(DEVICE_NAME, "AUTORUN.GET", code));
                }
                else
                {
                    AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(DEVICE_NAME, "AUTORUN.GET", ""));
                }
            }
            else if (message.Function == "AUTORUN.PURGE")
            {
                if (File.Exists(@"I:\autorun.mog"))
                {
                    File.Delete(@"I:\autorun.mog");
                    Debug.WriteLine($"autorun.mog deleted.");
                }

                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(DEVICE_NAME, "AUTORUN.PURGE", "OK"));
            }
            else if (message.Function == "HALT")
            {
                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(DEVICE_NAME, "HALT", "REQUESTED"));
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
                    AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(DEVICE_NAME, "STATE.GET", "RUNNING"));
                }
                else
                {
                    AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(DEVICE_NAME, "STATE.GET", "IDLE"));
                }
            }
            else if (message.Function == "SESSION.GET")
            {
                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(DEVICE_NAME, "SESSION.GET", AppGlobal.Session.ToString()));
            }
            else if (message.Function == "LAST.RESULT.GET")
            {
                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(DEVICE_NAME, "LAST.RESULT.GET", AppGlobal.MogwaiNanoEngine.LastResult.ToString()));
            }
            else if (message.Function == "PING")
            {
                AppGlobal.TcpServer.EnqueueMessage(new ServerMessage(DEVICE_NAME, "PONG"));
            }
        }

        private static bool ConnectToWifi()
        {
            Debug.Write("Connexion au WiFi... ");

            CancellationTokenSource cs = new(60000);

            var success = WifiNetworkHelper.Reconnect(requiresDateTime: true, token: cs.Token);

            if (!success)
            {
                Debug.WriteLine();
                Debug.WriteLine("Aucune configuration WiFi trouvée ou échec de connexion.");
                
                return false;
            }
            else
            {
                Debug.WriteLine($"OK - {NetworkInterface.GetAllNetworkInterfaces()[0].IPv4Address}");
            }

            return true;
        }
    }
}
