using Data_struct;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenStreaming_client
{
    public partial class Form2 : Form
    {
        TcpClient client;
        NetworkStream stream;
        BinaryFormatter formatter;
        Task recv_task;
        public Form2(TcpClient client)
        {
            InitializeComponent();
            this.client = client;
            stream = this.client.GetStream();
            formatter = new BinaryFormatter();
        }

        //서버가 주는 영상을 화면에 갱신하는 스레드 동작처리
        private void Form2_Load(object sender, EventArgs e)
        {
            //넘겨주는 화면의 너비, 높이를 저장
            server_height = (int)formatter.Deserialize(stream);
            server_width = (int)formatter.Deserialize(stream);
            //picturebox와 서버의 너비,높이로 비율 연산
            set_rate();
            recv_task = new Task(new Action(recv_screen));
            recv_task.Start();
        }
        //영상데이터 수신 및 화면갱신
        void recv_screen()
        {
            for(; ; )
            {
                try
                {
                    Bitmap bitmap = (Bitmap)formatter.Deserialize(stream);
                    this.Invoke((Action)(() =>
                    {
                        pictureBox1.Image = (Bitmap) bitmap.Clone();
                    }), null);
                }
                catch { break; }
            }
            stream.Close();
            client.Close();
        }

        void set_rate()
        {
            rate_width = server_width / (float)pictureBox1.Width;
            rate_height = server_height / (float)pictureBox1.Height;
        }
        int server_width, server_height;
        float rate_width, rate_height;

        private void PictureBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {

        }

        private void PictureBox1_SizeChanged(object sender, EventArgs e)
        {
            set_rate();
        }

        private void PictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            Data data = new Data();
            data.X = (int)(e.X * rate_width);
            data.Y = (int)(e.Y * rate_height);
            if(e.Button == MouseButtons.Left)
            {
                data.mode = 1;
            }
            else if (e.Button == MouseButtons.Right)
            {
                data.mode = 2;
            }//114.70.60.88
            else if (e.Button == MouseButtons.Middle)
            {
                data.mode = 3;
            }
            try
            {
                formatter.Serialize(stream, data);
            }
            catch
            {  }
        }

        //서버와 연결을 종료
        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                stream.Close();
            }
            catch { }
            try
            {
                client.Close();
            }
            catch { }

        }
    }
}
