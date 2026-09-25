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

using MOGWAI.Engine;
using MOGWAI.Objects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace MogwaiNanoStudioGui.Classes
{
    public class MogwaiNanoClient : IDisposable
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private Thread? _receiveThread;
        private volatile bool _running;

        public event EventHandler<ServerMessage>? MessageReceived;
        public event EventHandler<Exception>? ConnectionError;
        public event EventHandler? Disconnected;

        public bool IsConnected => _running;

        public void Connect(string host, int port)
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect(host, port);

            _stream = _tcpClient.GetStream();
            _stream.ReadTimeout = 15000;

            _running = true;

            _receiveThread = new Thread(ReceiveLoop);
            _receiveThread.IsBackground = true;
            _receiveThread.Start();
        }

        public void Disconnect()
        {
            if (_running)
            {
                try
                {
                    SendMessage(new ServerMessage(AppGlobal.SOURCE_NAME, "BYE"));
                    Thread.Sleep(1000);
                }
                catch
                {

                }
            }

            _running = false;
            _stream?.Close();
            _tcpClient?.Close();
            _receiveThread?.Join(1000);
        }

        public void SendMessage(ServerMessage message)
        {
            var stream = _stream;

            if (!IsConnected || stream == null)
                throw new InvalidOperationException("Not connected.");

            try
            {
                string nano = message.ToNanoFormat();
                byte[] payloadBytes = Encoding.UTF8.GetBytes(nano);

                byte[] lengthBytes = new byte[4];
                int length = payloadBytes.Length;
                lengthBytes[0] = (byte)(length >> 24);
                lengthBytes[1] = (byte)(length >> 16);
                lengthBytes[2] = (byte)(length >> 8);
                lengthBytes[3] = (byte)length;

                lock (stream)
                {
                    stream.Write(lengthBytes, 0, 4);
                    stream.Write(payloadBytes, 0, payloadBytes.Length);
                }
            }
            catch (Exception ex)
            {
                _running = false;
                ConnectionError?.Invoke(this, ex);
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ReceiveLoop()
        {
            try
            {
                while (_running)
                {
                    string? nano = ReadMessage();

                    if (nano == null)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    try
                    {
                        var message = ServerMessage.FromNanoFormat(nano);

                        if (message != null)
                            MessageReceived?.Invoke(this, message);
                    }
                    catch (Exception ex)
                    {
                        ConnectionError?.Invoke(this, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_running)
                    ConnectionError?.Invoke(this, ex);
            }
            finally
            {
                _running = false;
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        private string? ReadMessage()
        {
            try
            {
                byte[] lengthBuffer = new byte[4];

                if (!ReadExactly(lengthBuffer, 4))
                    return null;

                int messageLength = (lengthBuffer[0] << 24) | (lengthBuffer[1] << 16) | (lengthBuffer[2] << 8) | lengthBuffer[3];

                byte[] payloadBuffer = new byte[messageLength];

                if (!ReadExactly(payloadBuffer, messageLength))
                    return null;

                return Encoding.UTF8.GetString(payloadBuffer, 0, payloadBuffer.Length);
            }
            catch (Exception ex)
            {
                ConnectionError?.Invoke(this, ex);
                return null;
            }
        }

        private bool ReadExactly(byte[] buffer, int count)
        {
            if (_stream == null)
                return false;

            try
            {
                int totalRead = 0;

                while (totalRead < count)
                {
                    int bytesRead = _stream.Read(buffer, totalRead, count - totalRead);

                    if (bytesRead == 0)
                        return false;

                    totalRead += bytesRead;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
        }

        public void Dispose() => Disconnect();

        // Raw data for a device detected by the UDP scan, before it's
        // projected into whatever shape each caller needs (MOGRecord for
        // RPN, ScanDevice for the UI) — avoids duplicating the network loop.
        private readonly record struct RawScanResult(
            string Name, string Version, string Session, string Ip,
            string Platform, string Target, string Oem, string System);

        private List<RawScanResult> PerformScan()
        {
            using var udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;

            var request = new ServerMessage(AppGlobal.SOURCE_NAME, "WHO IS HERE");
            string nano = request.ToNanoFormat();
            byte[] data = Encoding.UTF8.GetBytes(nano);

            var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, AppGlobal.DISCOVERY_PORT);

            var results = new List<RawScanResult>();
            var seenAddresses = new HashSet<string>();

            udpClient.Client.ReceiveTimeout = 1000;

            var deadline = DateTime.Now.AddMilliseconds(2000);
            var nextSendTime = DateTime.Now;

            while (DateTime.Now < deadline)
            {
                if (DateTime.Now >= nextSendTime)
                {
                    udpClient.Send(data, data.Length, broadcastEndpoint);
                    nextSendTime = DateTime.Now.AddMilliseconds(250);
                }

                try
                {
                    IPEndPoint? remoteEndPoint = null;
                    byte[] received = udpClient.Receive(ref remoteEndPoint);
                    string responseNano = Encoding.UTF8.GetString(received);

                    var response = ServerMessage.FromNanoFormat(responseNano);

                    if (response != null && response.Function == "I AM HERE" && response.Parameters != null && response.Parameters.Length >= 6)
                    {
                        string ip = remoteEndPoint!.Address.ToString();

                        if (seenAddresses.Add(ip)) // Add() returns false if already present -> no duplicate
                        {
                            results.Add(new RawScanResult(
                                Name: response.Source,
                                Version: response.Parameters[0],
                                Session: response.Parameters[1],
                                Ip: ip,
                                Platform: response.Parameters[2],
                                Target: response.Parameters[3],
                                Oem: response.Parameters[4],
                                System: response.Parameters[5]));
                        }
                    }
                }
                catch (SocketException)
                {
                    // receive timeout, keep going until the deadline
                }
            }

            return results;
        }

        public MOGList Scan(MogwaiEngine engine)
        {
            // nano.scan

            var raw = PerformScan();
            var list = new MOGList(engine);

            foreach (var r in raw)
            {
                var record = new MOGRecord(engine);

                record.SetString("name", r.Name);
                record.SetString("version", r.Version);
                record.SetString("session", r.Session);
                record.SetString("ip", r.Ip);
                record.SetString("platform", r.Platform);
                record.SetString("target", r.Target);
                record.SetString("OEM", r.Oem);
                record.SetString("system", r.System);

                list.AddItem(record);
            }

            return list;
        }

        // "GUI-friendly" version of the scan, for a selection dialog —
        // detects the same devices as Scan(), as simple objects directly
        // usable as a list/grid's ItemsSource.
        // Also carries the fields not shown in the UI (session/OEM/system),
        // to be able to rebuild a full MOGRecord if needed (nano.user.select).
        public List<ScanDevice> ScanDevices()
        {
            return PerformScan()
                .Select(r => new ScanDevice
                {
                    Name = r.Name,
                    Version = r.Version,
                    IpAddress = r.Ip,
                    Platform = r.Target, // the 4th displayed column (e.g. "ESP32_REV3") maps to the "target" field
                    Session = r.Session,
                    GenericPlatform = r.Platform,
                    Oem = r.Oem,
                    System = r.System
                })
                .ToList();
        }
    }
}
