using Data_struct;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenStreaming
{
    //개발자가 추가한 Data클래스 객체를 네트워크상에서 수신받을수있도록
    //허용하는 클래스
    sealed class AllowAllAssemblyVersionsDeserializationBinder : SerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            Type typeToDeserialize = null;

            String currentAssembly = Assembly.GetExecutingAssembly().FullName;

            // In this case we are always using the current assembly
            assemblyName = currentAssembly;

            // Get the type using the typeName and assemblyName
            typeToDeserialize = Type.GetType(String.Format("{0}, {1}",
                typeName, assemblyName));

            return typeToDeserialize;
        }
    }
    public partial class Form1 : Form
    {
        //마우스제어 함수 가져오기
        //Dll(소문자)I(대문자)mport
        [DllImport("User32.dll")]
        private static extern void mouse_event
            (uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
        //마우스제어에 사용할 클릭이벤트 값 저장
        //L,R,M : 마우스 버튼, Down - 누르는 동작, up - 버튼을 때는 동작
        const uint L_down = 0x0002;
        const uint L_up = 0x0004;
        const uint M_down = 0x0020;
        const uint M_up = 0x0040;
        const uint R_down = 0x0008;
        const uint R_up = 0x0010;
        public Form1()
        {
            InitializeComponent();
        }

        private void Button1_Click(object sender, EventArgs e)
        {

            Bitmap screen = getScreen();
            this.Invoke((Action)(() =>
            {
                pictureBox1.Image = screen;
            }), null);
        }
        //현재 컴퓨터 화면을 스크린샷한 결과를 반환하는 함수
        Bitmap getScreen()
        {
            //현재 컴퓨터의 해상도를 추출
            Rectangle rect = Screen.PrimaryScreen.Bounds;
            //비트맵 객체 생성 - 컴퓨터의 해상도에 맞춤
            Bitmap scnbit = new Bitmap(rect.Width, rect.Height);
            //현재화면을 비트맵객체 복사
            //Graphics : Bitmap에 도형그리기나 영상처리 연산에 사용하는 클래스
            Graphics g = Graphics.FromImage(scnbit);
            //CopyFromScreen : 모니터 화면 데이터를 비트맵객체에 복사하는 함수
            g.CopyFromScreen(rect.X, rect.Y, 0, 0, 
                scnbit.Size,CopyPixelOperation.SourceCopy);
            g.Dispose();

            return scnbit;
        }
        //Picturebox를 마우스로 클릭했을때 발생하는 이벤트
        private void PictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            label1.Text = string.Format("X : {0}", e.X);
            label2.Text = string.Format("Y : {0}", e.Y);
            switch(e.Button)
            {
                //왼쪽클릭
                case MouseButtons.Left:
                    label3.Text = "Click, left";
                    break;
                case MouseButtons.Right:
                    label3.Text = "Click, Right";
                    break;
                case MouseButtons.Middle:
                    label3.Text = "Click, Middle";
                    break;
            }
            //Cursor : 현재 컴퓨터의 마우스 정보를 저장한 속성
            //Position : 현재 커서위치를 추출하거나 지정할수있는 변수
            /*
            Cursor.Position = new Point
                ((int)(e.X * rate_width),(int)( e.Y * rate_height) );
            mouse_event(L_down, 0, 0, 0, 0);
            mouse_event(L_up, 0, 0, 0, 0);
            */

        }
        //마우스로 더블클릭했을때 발생하는 이벤트
        private void PictureBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            label3.Text = "double click";
        }
        float rate_width, rate_height;

        //폼크기가 바뀌어 picturebox사이즈가 변경됬을때 발생하는 이벤트
        private void PictureBox1_SizeChanged(object sender, EventArgs e)
        {
            //모니터 너비,높이와 picturebox의 너비,높이를 나눠서 비율 저장
            Rectangle rect = Screen.PrimaryScreen.Bounds;
            rate_width = rect.Width / (float)pictureBox1.Width;
            rate_height = rect.Height / (float)pictureBox1.Height;
        }
        TcpListener server;
        TcpClient client;
        NetworkStream stream;
        BinaryFormatter formatter;
        Task recv_task, send_task;
        //원격제어 서버 생성 및 실행
        private void Button2_Click(object sender, EventArgs e)
        {
            server = new TcpListener(8000);
            recv_task = new Task(new Action(recv_thread));
            recv_task.Start();
        }
        //서브스레드1 - 서버 열기 및 클라이언트 접속대기, 연결된클라이언트에게
        //영상데이터를 보내주는 서브스레드2 생성, 마우스입력 명령을 받아 처리
        void recv_thread()
        {
            server.Start();
            client = server.AcceptTcpClient();
            stream = client.GetStream();
            formatter = new BinaryFormatter();
            formatter.Binder = new AllowAllAssemblyVersionsDeserializationBinder();

            //서버 컴퓨터의 모니터 크기를 전송
            Rectangle rect = Screen.PrimaryScreen.Bounds;
            int H = rect.Height;
            int W = rect.Width;
            formatter.Serialize(stream, H);
            formatter.Serialize(stream, W);
            //서버컴퓨터의 화면을 전송하는 서브스레드 동작
            send_task = new Task(new Action(send_thread));
            send_task.Start();
            //무한반복 - 클라이언트의 마우스입력을 수신하고 해당 위치로 이동/클릭
            for (; ; )
            {
                try
                {
                    Data data = (Data)formatter.Deserialize(stream);
                    this.Invoke((Action)(() =>
                    {
                        label1.Text = string.Format("X : {0}", data.X);
                        label2.Text = string.Format("Y : {0}", data.Y);
                        label3.Text = string.Format("mode : {0}", data.mode);
                        //마우스 위치 이동
                        Cursor.Position = new Point(data.X, data.Y);
                        //mode변수에 따른 버튼클릭
                        if (data.mode == 1)//왼쪽클릭
                        {
                            mouse_event(L_down, 0, 0, 0, 0);
                            mouse_event(L_up, 0, 0, 0, 0);
                        }
                        else if (data.mode == 2)//오른쪽클릭
                        {
                            mouse_event(R_down, 0, 0, 0, 0);
                            mouse_event(R_up, 0, 0, 0, 0);
                        }
                        else if (data.mode == 3)//휠클릭
                        {
                            mouse_event(M_down, 0, 0, 0, 0);
                            mouse_event(M_up, 0, 0, 0, 0);
                        }
                        else if (data.mode == 4)//더블클릭
                        {
                            mouse_event(L_down, 0, 0, 0, 0);
                            mouse_event(L_up, 0, 0, 0, 0);
                            Thread.Sleep(50);
                            mouse_event(L_down, 0, 0, 0, 0);
                            mouse_event(L_up, 0, 0, 0, 0);
                        }
                    }), null);
                }
                catch { break; }
            }
            //연결종료 처리
            stream.Close();
            client.Close();
            server.Stop();
            MessageBox.Show("클라이언트가 연결을 끊음");
        }
        void send_thread()
        {
            //무한반복 - 서버컴퓨터의 화면 데이터를 송신, 30FPS
            for (; ; )
            {
                try
                {
                    Bitmap bitmap = getScreen();
                    formatter.Serialize(stream, bitmap);
                }
                catch { break; }
                Thread.Sleep(33);
            }
        }


        //폼이 로드가 완료됬을때 발생하는 이벤트
        private void Form1_Load(object sender, EventArgs e)
        {
            //모니터 너비,높이와 picturebox의 너비,높이를 나눠서 비율 저장
            Rectangle rect = Screen.PrimaryScreen.Bounds;
            rate_width = rect.Width / (float)pictureBox1.Width;
            rate_height = rect.Height / (float)pictureBox1.Height;
        }
    }
}
