using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace udpMicStream
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        MicStream stream;
        UdpSender udpSender;
        private void Button1_Click(object sender, EventArgs e)
        {
            udpSender = new UdpSender();
            stream = new MicStream(udpSender);
            stream.stream_play();
            
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            stream.stream_stop();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
