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

using nanoFramework.Runtime.Native;
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
            _shuttingDown = true;

            using (var wakeupClient = new UdpClient())
            {
                byte[] dummy = Encoding.UTF8.GetBytes("WAKEUP");
                string localIp = NetworkInterface.GetAllNetworkInterfaces()[0].IPv4Address;
                wakeupClient.Send(dummy, new IPEndPoint(IPAddress.Parse(localIp), AppGlobal.DISCOVERY_PORT));
            }

            _udpThread.Join(2000);
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
                            break;

                        string nano = Encoding.UTF8.GetString(buffer, 0, length);
                        var message = ServerMessage.FromNanoFormat(nano);

                        if (message.Source == AppGlobal.EXPECTED_SOURCE && message.Function == "WHO IS HERE" && !AppGlobal.TcpServer.IsClientConnected)
                            SendDiscoveryResponse(_udpListener, remoteEndPoint);
                    }
                    catch
                    {
                        if (_shuttingDown)
                            break;
                    }
                }
            }
            catch
            {

            }
        }

        private void SendDiscoveryResponse(UdpClient udpListener, IPEndPoint remoteEndPoint)
        {
            var response = new ServerMessage(
                AppGlobal.NanoParameters.Name,
                "I AM HERE",
                AppGlobal.MogwaiNanoEngine.Version.ToString(),
                AppGlobal.Session.ToString(),
                SystemInfo.Platform,
                SystemInfo.TargetName,
                SystemInfo.OEMString,
                SystemInfo.Version.ToString()
            );

            string nano = response.ToNanoFormat();
            byte[] responseBytes = Encoding.UTF8.GetBytes(nano);
            udpListener.Send(responseBytes, remoteEndPoint);
        }
    }
}
