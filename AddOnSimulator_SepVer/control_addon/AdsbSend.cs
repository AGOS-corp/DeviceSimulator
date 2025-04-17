using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace AddOnSimulator_SepVer
{
    public class AdsbSend
    {
        private static IPEndPoint serverEndPoint { get; set; }
        private static UdpClient udpClient { get; set; }

        private const int ADSB_LENGTH = 50;  //146 - 휴대용..?   50 - 랙타임..?
        private static IPAddress serverIP { get; set; }
        private static int readIndex { get; set; }
        public static int timeOut = 1000;

        public static void SetNetwork(string _serverIP, int _port)
        {
            Array.Clear(cQueue, 0, cQueue.Length);
            input_index = 0;
            output_index = 0;
            selectPacketLength = 0;

            if (serverIP == null || _serverIP != serverIP.ToString())
            {
                serverIP = IPAddress.Parse(_serverIP);

                serverEndPoint = new IPEndPoint(serverIP, _port);
                // UDP 클라이언트를 생성합니다.
                udpClient = new UdpClient();
            }
        }


        static byte[] cQueue = new byte[ADSB_LENGTH * 10];

        static int input_index = 0;
        static int output_index = 0;
        static int selectPacketLength = 0;

        public static bool SendADSB(byte[] packets)
        {
            var readIndexCount = 0;
            var result = false;

            Array.Clear(cQueue, input_index, cQueue.Length - input_index);

            while (readIndexCount < packets.Length)
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

                        
                        udpClient.Send(dataToSend, dataToSend.Length, serverEndPoint);

                        Thread.Sleep(timeOut);
                        selectPacketLength = 2;     // 현재 읽어낸 다음 Packet의 STX size 저장
                    }
                }
            }
            return false;
        }

        private static bool IsStx(byte node1, byte node2)
        {
            return (node1 == 0x15 && node2 == 0x00);
        }
    }
}