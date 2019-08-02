using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace udpMicStream
{
    class UdpSender
    {
        UdpClient sender, receiver;
        IPEndPoint des_ip,src_ip;
        public UdpSender()
        {
            sender = new UdpClient();
            receiver = new UdpClient(8000);
            des_ip = new IPEndPoint(IPAddress.Parse("192.168.0.4"),8000);
            src_ip = new IPEndPoint(0, 0);
        }

        public void send(byte[] data)
        {
            try
            {
                sender.Send(data, data.Length, des_ip);
            }
            catch { }
        }

        public byte[] recv()
        {
            byte[] recv_data = null;
            try
            {
                recv_data = receiver.Receive(ref src_ip); ;
            }
            catch { }
            return recv_data;
        }

        public void close()
        {
            try
            {
                sender.Close();
            }
            catch
            { }
            try
            {
                receiver.Close();
            }
            catch { }

        }
    }
}
