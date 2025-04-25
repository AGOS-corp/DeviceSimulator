using AddOnSimulator_SepVer.util;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AddOnSimulator_SepVer
{
    public class AdsbSend
    {
        private static UdpServer udpServer { get; set; } = new UdpServer();
        public static Action<string> DataSendEvent;

        private const int ADSB_LENGTH = 50;  //146 - 휴대용..?   50 - 랙타임..?
        private static int readIndex { get; set; }
        public static int timeOut = 1000;
        public static bool isConnected = false;

        public static void SetNetwork(string _serverIP, int _port)
        {
            Array.Clear(cQueue, 0, cQueue.Length);
            input_index = 0;
            output_index = 0;
            selectPacketLength = 0;

            udpServer.OpenUDPServer(_serverIP, _port);
            ShowLog("Open");
            isConnected = true;
        }

        public static void Disconnect()
        {
            isConnected = false;
            udpServer.CloseUDPServer();
            ShowLog("Closed");
        }

        private static void ShowLog(string message)
        {
            DataSendEvent?.Invoke($"ADSB - {message}");
        }

        static byte[] cQueue = new byte[ADSB_LENGTH * 10];

        static int input_index = 0;
        static int output_index = 0;
        static int selectPacketLength = 0;

        public static async Task<bool> SendADSB(byte[] packets)
        {
            var readIndexCount = 0;
            var result = false;

            Array.Clear(cQueue, input_index, cQueue.Length - input_index);

            while (readIndexCount < packets.Length && isConnected)
            {
                result = false;
                cQueue[input_index++] = packets[readIndexCount++];
                selectPacketLength++;

                if (input_index == cQueue.Length)
                    input_index = 0;

                if (readIndexCount > 2 && packets[readIndexCount - 2] == 0x15)
                {
                    switch (input_index)
                    {
                        case 1:
                            result = IsStx(cQueue[cQueue.Length - 1], cQueue[input_index - 1]);
                            break;

                        case 0:
                            result = IsStx(cQueue[cQueue.Length - 2], cQueue[cQueue.Length - 1]);
                            break;

                        default:
                            result = IsStx(cQueue[input_index - 2], cQueue[input_index - 1]);
                            break;
                    }

                    if (result)
                    {
                        selectPacketLength -= 2;  // 현재 읽어낸 다음 Packet의 STX size 만큼 크기 줄이기
                        byte[] dataToSend = new byte[selectPacketLength];
                        for (int i = 0; i < selectPacketLength; i++)
                        {
                            dataToSend[i] = cQueue[output_index++];
                            if (output_index == cQueue.Length)
                                output_index = 0;
                        }

                        if (await udpServer.SendData(dataToSend))
                            ShowLog("ADSB - Data 송신");

                        /*if (timeOut < 15)
                            SpinWaitMilliseconds(timeOut);

                        else*/
                        await Task.Delay(timeOut);

                        selectPacketLength = 2;     // 현재 읽어낸 다음 Packet의 STX size 저장
                    }
                }
            }
            return false;
        }

        public static void SpinWaitMilliseconds(int milliseconds)
        {
            if (milliseconds <= 0) return;

            Stopwatch stopwatch = Stopwatch.StartNew();
            long targetTicks = stopwatch.ElapsedTicks + (long)(milliseconds * Stopwatch.Frequency / 1000.0);

            while (stopwatch.ElapsedTicks < targetTicks)
            {
                // CPU를 계속 사용하며 대기
                // Thread.Yield() 또는 Thread.SpinWait()를 사용하여 다른 스레드에게 기회를 줄 수도 있지만,
                // 매우 짧은 대기에는 효과가 제한적일 수 있습니다.
            }
        }

        static Stopwatch stopwatch = new Stopwatch();


        private static bool IsStx(byte node1, byte node2)
        {
            return (node1 == 0x15 && node2 == 0x00);
        }
    }
}