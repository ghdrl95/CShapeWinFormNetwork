using NAudio.Codecs;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace udpMicStream
{
    class MicStream
    {


        /// <summary>Lock for the sender queue.</summary>
        static Mutex Lock = new Mutex();

        WaveFormat CommonFormat;

        /// <summary>"Kick" semaphore for the sender queue.</summary>
        static Semaphore SenderKick = new Semaphore(0, int.MaxValue);
        /// <summary>Queue of byte buffers from the DataAvailable event.</summary>
        static LinkedList<byte[]> SenderQueue = new LinkedList<byte[]>();

        //Semaphore ReceiverKick = new Semaphore(0, int.MaxValue);
        //static LinkedList<byte[]> ReceiverQueue = new LinkedList<byte[]>();

        /// <summary>WaveProvider for the output.</summary>
        BufferedWaveProvider OutProvider;


        delegate byte EncoderMethod(short _raw);
        delegate short DecoderMethod(byte _encoded);

        // Change these to their ALaw equivalent if you want.
        //16비트 샘플을 MuLaw로 바꾸는 작업
        /*- 북미에서 일반적으로 사용되는 컴팬딩 기법. U-law는 ITU-T G.711에서 64Kbps CODEC로 표준화돼 있다.
      - 16비트 PCM값을 8bit G.711데이터 값으로 압축한다. Sign bit와 마지막 2bit를 값 무시. 나머지 13bit값을 8bit로 변환한다.*/
        static EncoderMethod Encoder = MuLawEncoder.LinearToMuLawSample;
        //MuLaw에서 16비트 샘플로 바꾸는 작업
        static DecoderMethod Decoder = MuLawDecoder.MuLawToLinearSample;


        Task sender, receiver;
        IWaveIn wavein;
        WaveOut waveout;

        UdpSender udpSender;

        CancellationTokenSource source;
        public MicStream(UdpSender udpSender)
        {
            this.udpSender = udpSender;
        }

        public void stream_play()
        {
            source = new CancellationTokenSource();
            // Fire off our Sender thread.
            sender = new Task(new Action(Sender),source.Token);
            sender.Start();

            // And receiver...
            receiver = new Task(new Action(Receiver), source.Token);
            receiver.Start();

            // We're going to try for 16-bit PCM, 8KHz sampling, 1 channel.
            // This should align nicely with u-law
            CommonFormat = new WaveFormat(16000, 16, 1);

            // Prep the input.
            wavein = new WaveInEvent();
            wavein.WaveFormat = CommonFormat;
            //마이크 입력이 들어왔을 때의 처리
            wavein.DataAvailable += new EventHandler<WaveInEventArgs>(wavein_DataAvailable);
            wavein.StartRecording();

            // Prep the output.  The Provider gets the same formatting.
            waveout = new WaveOut();
            //출력을 하기위해 출력데이터를 넣을 버퍼
            OutProvider = new BufferedWaveProvider(CommonFormat);
            waveout.Init(OutProvider);
            waveout.Play();
        }

        public void stream_stop()
        {
            wavein.StopRecording();
            try { wavein.Dispose(); } catch { }
            SenderKick.Release();

            source.Cancel();
            udpSender.close();

            // Wait for both threads to exit.
            sender.Wait();
            receiver.Wait();

            // And close down the output.
            waveout.Stop();
            try { waveout.Dispose(); } catch { }
        }



        //마이크 데이터가 발생했을 때 호출되는 이벤트
        //마이크 이벤트는 계속해서 데이터가 발생하기때문에 데이터가 하나씩들어갈수있도록 스레드 제어
        /// <summary>
        /// Grabs the mic data and just queues it up for the Sender.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void wavein_DataAvailable(object sender, WaveInEventArgs e)
        {
            // Create a local copy buffer.
            byte[] buffer = new byte[e.BytesRecorded];
            //바이트 데이터를 배열에 복사
            System.Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);

            // Drop it into the queue.  We'll need to lock for this.
            //하나의 스레드만 접근할수있도록 처리
            Lock.WaitOne();

            SenderQueue.AddLast(buffer);
            Lock.ReleaseMutex();

            // and kick the thread.
            //접근하는 스레드 수를 줄이는 작업
            SenderKick.Release();
        }


        void Sender()
        {
            // Holds the data from the DataAvailable event.
            byte[] qbuffer = null;

            for (; !source.IsCancellationRequested; )
            {
                // Wait for a 'kick'...
                SenderKick.WaitOne();

                // Lock...
                //Lock : SenderQueue에 데이터를 삽입/추출하는 작업을 한쓰레드만 동작하도록 수행
                Lock.WaitOne();
                bool dataavailable = (SenderQueue.Count != 0);
                //데이터가 있는지 여부확인 및 데이터 추출
                if (dataavailable)
                {
                    qbuffer = SenderQueue.First.Value;
                    SenderQueue.RemoveFirst();
                }
                Lock.ReleaseMutex();

                // If the queue was empty on a kick, then that's our signal to
                // exit.
                if (!dataavailable) break;

                //
                // Convert each 16-bit PCM sample to its 1-byte u-law equivalent.
                int numsamples = qbuffer.Length / sizeof(short);
                byte[] g711buff = new byte[numsamples];

                // I like unsafe for this kind of stuff!
                unsafe
                {
                    fixed (byte* inbytes = &qbuffer[0])
                    fixed (byte* outbytes = &g711buff[0])
                    {
                        // Recast input buffer to short[]
                        short* buff = (short*)inbytes;

                        // And loop over the samples.  Since both input and
                        // output are 16-bit, we can use the same index.
                        for (int index = 0; index < numsamples; ++index)
                        {
                            outbytes[index] = Encoder(buff[index]);
                        }
                    }
                }


                // outbytes를 인터넷으로 송신
                udpSender.send(g711buff);

                // This gets passed off to the reciver.  We'll queue it for now.
                //인코딩된 바이트배열을 실행하기 위한 큐에 넣어줌
                /*
                Lock.WaitOne();
                ReceiverQueue.AddLast(g711buff);
                Lock.ReleaseMutex();
                ReceiverKick.Release();
                */
            }

            // Log it.  We'll also kick the receiver (with no queue addition)
            // to force it to exit.
            Console.WriteLine("Sender: Exiting.");
            //ReceiverKick.Release();
        }

        void Receiver()
        {
            byte[] qbuffer = null;
            for (; !source.IsCancellationRequested; )
            {
                

                qbuffer = udpSender.recv();
                if (qbuffer == null) break;
                /*
                // Wait for a 'kick'...
                ReceiverKick.WaitOne();
                // Lock...
                Lock.WaitOne();
                bool dataavailable = (ReceiverQueue.Count != 0);
                if (dataavailable)
                {
                    qbuffer = ReceiverQueue.First.Value;
                    ReceiverQueue.RemoveFirst();
                }
                Lock.ReleaseMutex();

                // Exit on kick with no data.
                if (!dataavailable) break;
                */
                // As above, but we convert in reverse, from 1-byte u-law
                // samples to 2-byte PCM samples.
                int numsamples = qbuffer.Length;
                byte[] outbuff = new byte[qbuffer.Length * 2];
                unsafe
                {
                    fixed (byte* inbytes = &qbuffer[0])
                    fixed (byte* outbytes = &outbuff[0])
                    {
                        // Recast the output to short[]
                        short* outpcm = (short*)outbytes;

                        // And loop over the u-las samples.
                        for (int index = 0; index < numsamples; ++index)
                        {
                            outpcm[index] = Decoder(inbytes[index]);
                        }
                    }
                }

                // And write the output buffer to the Provider buffer for the
                // WaveOut devices.
                //변환된 16bit를 프로바이더에 쌓아놓음
                OutProvider.AddSamples(outbuff, 0, outbuff.Length);
            }

            Console.Write("Receiver: Exiting.");
        }





    }

}
