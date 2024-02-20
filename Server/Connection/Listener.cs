using System.Net;
using System.Net.Sockets;
using System;
using System.Windows.Forms;
using System.Drawing;
using Server.Handle_Packet;
using System.Diagnostics;
using Server.Connection;
using System.Drawing.Text;

namespace Server.Connection
{
    class Listener
    {
        private Socket Server { get; set; }
        private Socket ServerV6 { get; set; }

        public void Connect(object port)
        {
            try
            {
                IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, Convert.ToInt32(port));
                IPEndPoint ipEndPointV6 = new IPEndPoint(IPAddress.IPv6Any, Convert.ToInt32(port));
                Server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    SendBufferSize = 50 * 1024,
                    ReceiveBufferSize = 50 * 1024,
                }; // ipv4
                ServerV6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
                {
                    SendBufferSize = 50 * 1024,
                    ReceiveBufferSize = 50 * 1024,
                }; //ipv6

                //IPV4
                Server.Bind(ipEndPoint);
                Server.Listen(500);
                new HandleLogs().Addmsg($"Listenning to: {port}(ipv4)", Color.Green);
                Server.BeginAccept(EndAccept, null);

                //IPV6
                ServerV6.Bind(ipEndPointV6);
                ServerV6.Listen(500);
                new HandleLogs().Addmsg($"Listenning to: {port}(ipv6)", Color.Green);
                ServerV6.BeginAccept(EndAcceptV6, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Environment.Exit(0);
            }
        }

        private void EndAccept(IAsyncResult ar)
        {
            try
            {
                new Clients(Server.EndAccept(ar));
            }
            catch(Exception error)
            {
                Debug.WriteLine(error.Message);
            }
            finally
            {
                Server.BeginAccept(EndAccept, null);
            }
        }
        private void EndAcceptV6(IAsyncResult ar) 
        {
            try
            {
                new Clients(ServerV6.EndAccept(ar));
            }
            catch (Exception error1)
            {
                Debug.WriteLine(error1.Message);
            }
            finally
            {
                ServerV6.BeginAccept(EndAcceptV6, null);
             
            }
        }
    }
}
