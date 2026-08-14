using nanoFramework.Json;
using nanoFramework.Runtime.Native;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MogwaiNano.Engine
{
    public class UdpServer
    {
        private Thread _udpThread;
        private UdpClient _udpListener;
        private bool _shuttingDown = false;
        
        public void StartUdpServer()
        {
            _udpThread = new Thread(UdpListenLoop);
            _udpThread.Start();
        }

        public void StopUdpServer()
        {
            Debug.WriteLine("Stopping UDP server...");

            _shuttingDown = true;

            using (var wakeupClient = new UdpClient())
            {
                byte[] dummy = Encoding.UTF8.GetBytes("WAKEUP");
                string localIp = NetworkInterface.GetAllNetworkInterfaces()[0].IPv4Address;
                wakeupClient.Send(dummy, new IPEndPoint(IPAddress.Parse(localIp), AppGlobal.DISCOVERY_PORT));
            }

            _udpThread.Join(2000);

            Debug.WriteLine("UDP server stopped.");
        }

        private void UdpListenLoop()
        {
            try
            {
                _udpListener = new UdpClient(AppGlobal.DISCOVERY_PORT);

                byte[] buffer = new byte[1024];

                while (!_shuttingDown)
                {
                    try
                    {
                        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                        int length = _udpListener.Receive(buffer, ref remoteEndPoint);

                        if (_shuttingDown)
                            break; // le paquet reçu était peut-être notre propre "WAKEUP"

                        string json = Encoding.UTF8.GetString(buffer, 0, length);
                        var message = (ServerMessage)JsonConvert.DeserializeObject(json, typeof(ServerMessage));

                        Debug.WriteLine($"Received UDP message from {remoteEndPoint.Address}:{remoteEndPoint.Port} - Source: {message.Source}, Function: {message.Function}");

                        if (message.Source == AppGlobal.EXPECTED_SOURCE && message.Function == "WHO IS HERE")
                            SendDiscoveryResponse(_udpListener, remoteEndPoint);
                    }
                    catch
                    {
                        if (_shuttingDown)
                            break;

                        // sinon, erreur ponctuelle -> ignorée
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_shuttingDown)
                {
                    Debug.WriteLine($"UDP server fatal error: {ex.Message}");
                }
                else
                {
                   Debug.WriteLine("UDP server shutting down.");
                }
            }
        }

        private void SendDiscoveryResponse(UdpClient udpListener, IPEndPoint remoteEndPoint)
        {
            var response = new ServerMessage(
                AppGlobal.DEVICE_NAME,
                "I AM HERE",
                AppGlobal.MogwaiNanoEngine.Version.ToString(),
                AppGlobal.Session.ToString(),
                SystemInfo.Platform,
                SystemInfo.TargetName,
                SystemInfo.OEMString,
                SystemInfo.Version.ToString()
            );

            string json = JsonSerializer.SerializeObject(response);
            byte[] responseBytes = Encoding.UTF8.GetBytes(json);

            udpListener.Send(responseBytes, remoteEndPoint);
        }
    }
}
