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

using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MogwaiNano.Engine
{
    public class TcpServer
    {
        private const int MAX_MESSAGE_SIZE = 16384;
        private const int MAX_QUEUE_SIZE = 100; 

        private Thread _tcpThread;
        private TcpListener _tcpListener;
        private TcpClient _tcpClient;
        private readonly object _sendLock = new();
        private bool _clientConnected = false;
        private bool _shuttingDown = false;
        private Stopwatch _lastMessageStopwatch = new Stopwatch();
        private Queue _outgoingQueue = new();
        private object _outgoingLock = new();
        private Thread _senderThread;
        private bool _pauseSending = false;
        private bool _disconnectRequested = false;
        private bool _connectionBroken = false;

        public delegate void MessageReceivedHandler(ServerMessage message);
        public event MessageReceivedHandler MessageReceived;

        public bool IsClientConnected => _clientConnected;

        public int ActualPort => _tcpListener != null ? ((IPEndPoint)_tcpListener.LocalEndpoint).Port : -1;

        public void StartTcpServer()
        {
            _tcpThread = new Thread(TcpListenLoop);
            _tcpThread.Start();

            _senderThread = new Thread(SenderLoop);
            _senderThread.Start();
        }

        public void StopTcpServer()
        {
            _shuttingDown = true;
            _tcpClient?.Close();
            _tcpListener?.Stop();
            _tcpThread.Join(2000);
        }

        public void EnqueueMessage(ServerMessage message)
        {
            if (!_connectionBroken)
            {
                lock (_outgoingLock)
                {
                    if (_outgoingQueue.Count >= MAX_QUEUE_SIZE)
                        _outgoingQueue.Dequeue();

                    _outgoingQueue.Enqueue(message);
                }
            }
        }

        private void TcpListenLoop()
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, AppGlobal.TCP_PORT);
                _tcpListener.Start(1);

                while (!_shuttingDown)
                {
                    try
                    {
                        using (TcpClient client = _tcpListener.AcceptTcpClient())
                        {
                            _tcpClient = client;
                            _clientConnected = true;

                            HandleClient(client);

                            _clientConnected = false;
                            _tcpClient = null;
                        }
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

        private void SenderLoop()
        {
            while (true)
            {
                if (_pauseSending)
                {
                    Thread.Sleep(50);
                }
                else if (_connectionBroken)
                {
                    lock (_outgoingLock)
                    {
                        _outgoingQueue.Clear();
                    }

                    Thread.Sleep(50);
                }
                else
                {
                    ServerMessage message = null;

                    lock (_outgoingLock)
                    {
                        if (_outgoingQueue.Count > 0)
                            message = (ServerMessage)_outgoingQueue.Dequeue();
                    }

                    if (message != null)
                    {
                        AppGlobal.TcpServer.SendMessage(message);
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();

            var idleStopwatch = Stopwatch.StartNew();

            _disconnectRequested = false;
            _connectionBroken = false;

            while (!_shuttingDown && !_disconnectRequested && !_connectionBroken)
            {
                int available = client.Available;

                if (available > 0)
                {
                    string nano = ReadMessage(stream);

                    if (nano == null)
                        break;

                    idleStopwatch.Restart();

                    ProcessMessage(nano, stream);
                }
                else
                {
                    if (idleStopwatch.ElapsedMilliseconds > 30000)
                        break;

                    Thread.Sleep(100);
                }
            }

            client.Close();

            _pauseSending = false;
            _outgoingQueue.Clear();
        }

        private void ProcessMessage(string nano, NetworkStream stream)
        {
            try
            {
                var message = ServerMessage.FromNanoFormat(nano);

                if (message.Function == "BYE")
                {
                    _pauseSending = true;
                    _disconnectRequested = true;
                }

                MessageReceived?.Invoke(message);
            }
            catch
            {

            }
        }

        public void SendMessage(ServerMessage message)
        {
            var stream = _tcpClient?.GetStream();

            if (stream == null)
                return;

            try
            {
                SendMessage(stream, message);
            }
            catch (Exception ex)
            {
                _connectionBroken = true;
            }
        }

        private void SendMessage(NetworkStream stream, ServerMessage message)
        {
            string nano = message.ToNanoFormat();
            byte[] payloadBytes = Encoding.UTF8.GetBytes(nano);

            int length = payloadBytes.Length;
            byte[] lengthBytes = new byte[4];
            lengthBytes[0] = (byte)(length >> 24);
            lengthBytes[1] = (byte)(length >> 16);
            lengthBytes[2] = (byte)(length >> 8);
            lengthBytes[3] = (byte)length;

            lock (_sendLock)
            {
                stream.Write(lengthBytes, 0, 4);
                stream.Write(payloadBytes, 0, payloadBytes.Length);
            }
        }

        private string ReadMessage(NetworkStream stream)
        {
            byte[] lengthBuffer = new byte[4];

            if (!ReadExactly(stream, lengthBuffer, 4))
                return null;

            int messageLength = (lengthBuffer[0] << 24) | (lengthBuffer[1] << 16) | (lengthBuffer[2] << 8) | lengthBuffer[3];

            if (messageLength <= 0 || messageLength > MAX_MESSAGE_SIZE)
                return null;

            byte[] payloadBuffer = new byte[messageLength];

            if (!ReadExactly(stream, payloadBuffer, messageLength))
                return null;

            return Encoding.UTF8.GetString(payloadBuffer, 0, payloadBuffer.Length);
        }

        private bool ReadExactly(NetworkStream stream, byte[] buffer, int count)
        {
            int totalRead = 0;

            while (totalRead < count)
            {
                int bytesRead = stream.Read(buffer, totalRead, count - totalRead);

                if (bytesRead == 0)
                    return false;

                totalRead += bytesRead;
            }

            return true;
        }
    }
}
