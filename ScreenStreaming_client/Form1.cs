using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenStreaming_client
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        TcpClient client;
        private void Button1_Click(object sender, EventArgs e)
        {
            try
            {
                client = new TcpClient(textBox1.Text,(int)numericUpDown1.Value);
                Form2 form = new Form2(client);
                //ShowDialog() : 기존의 화면은 제어를 하지못하고
                //열려있는 폼만 동작시킬수있는 함수.
                form.ShowDialog();
            }
            catch
            {
                MessageBox.Show("서버 연결 실패");
            }
        }
    }
}
