using AddOnSimulator_SepVer.util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AddOnSimulator_SepVer
{
    internal class FMSSend
    {
        private TcpServer server;

        public static Action<string> DataSendEvent;

        private bool isConnect;
        private int type = 0;   // 0-구 1-신 2-차량
        private int version = 0;    // 0-민군 1-구공
        private int carFmsSendType;     // 0-Scanner FMS   1-Car FMS
        private byte receiveCMD = 0x30;
        private byte relay = 0x00;  // 구-0 : On / 1 - Off
        private byte reset_new = 0xff;  //신 버전 fms
        private byte fan_12v = 0x01;  //1 - ON / 0 - off
        private byte rtk = 0x01;  //1 - ON / 0 - off



        public void SetNetwork(string _serverIP, string _port, int type, int version, int carFmsType)
        {
            server = new TcpServer();
            server.OpenTCPServer(_serverIP, int.Parse(_port), out string message);
            server.MessageSendEvent += ShowLog;
            server.DataSendEvent += ParcingData;
            ShowLog($"{message}");

            this.type = type;
            this.version = version;
            this.carFmsSendType = carFmsType;
            isConnect = true;
        }

        public void DisConnect()
        {
            server?.CloseTCPServer();
            ShowLog("Closed");
        }

        private void ShowLog(string message)
        {
            DataSendEvent?.Invoke($"FMS - {message}");
        }

        private async void ParcingData(byte[] receiveData)
        {
            if (receiveData.Length == 0)
            {
                DisConnect();
                return;
            }

            if (receiveData[10] == receiveCMD)
                await SendFMSState();

            else if (receiveData[10] == 0x31)
            {
                if (type == 0)
                    Task.Run(() => SetRelay(receiveData[15]));
                else
                    Task.Run(() => SetReset(receiveData[13]));

                await SendFMSState();
            }

            else if (type == 2)
            {
                if (carFmsSendType == 1)
                    Task.Run(() => SetCarFms(receiveData[10]));
                else
                    Task.Run(() => SetReset(receiveData[10]));

                await SendFMSState();
            }
        }

        private void SetCarFms(byte v)
        {
            switch (v)
            {
                case 0xB7:
                    rtk = 0x00;
                    DataSendEvent?.Invoke("FMS - RTK Off");
                    break;

                case 0xB8:
                    rtk = 0x01;
                    DataSendEvent?.Invoke("FMS - RTK ON");
                    break;

                case 0xB9:
                    fan_12v = 0x01;
                    DataSendEvent?.Invoke("FMS - 12V Fan 인가");
                    break;

                case 0xBA:
                    fan_12v = 0x00;
                    DataSendEvent?.Invoke("FMS - 12V Fan 인가 해제");
                    break;
            }
        }

        private void SetRelay(byte v)
        {
            if (v == 0x00)
                relay = 0x00;
            else
                relay = 0x01;
        }

        private async Task SetReset(byte v)
        {
            switch (v)
            {
                case 0x01:
                case 0xB2:
                    reset_new = 0xdf;
                    DataSendEvent?.Invoke("FMS - PC & HUB Reset");
                    break;

                case 0x02:
                case 0xB3:
                    reset_new = 0xfe;
                    DataSendEvent?.Invoke("FMS - 방탐수신기 Reset");
                    break;

                case 0xB4:
                    reset_new = 0x3f;
                    DataSendEvent?.Invoke("FMS - Protection 설정");
                    break;

                case 0xB5:
                    reset_new = 0xff;
                    DataSendEvent?.Invoke("FMS - Protection 해제");
                    break;

                default:
                    reset_new = 0x00;
                    DataSendEvent?.Invoke("FMS - 전체 Reset");
                    break;
            }

            await Task.Delay(1000);

            if (v == 0xB4 || v == 0xB5)
                return;

            reset_new = 0xff;
        }


        private async Task SendFMSState()
        {
            byte[] sendData;

            if (type == 0)          // 구
            {
                sendData = new byte[] { 0x16, 0x16, 0x16, 0x16, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0xb0, 0x0b, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x19, 0x03, 0x11, relay, 0x00, 0x00};
            }
            else if (type == 1)     // 신
            {
                sendData = new byte[] { 0x16, 0x16, 0x16, 0x16, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xb0, 0x0b, 0x00, 0x02, 0x05, 0x00, 0x00, 0x00, 0x18, 0x01, 0x08, reset_new, 0x00, 0x00 };
            }
            else                    // 차량
            {
                if (version == 0)    // 민군
                {
                    if (carFmsSendType == 0)   // Scanner FMS
                    {
                        sendData = new byte[] { 0x16, 0x16, 0x16, 0x16, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xb0, 0x0b, 0x00, 0x02, 0x05, 0x00, 0x00, 0x00, 0x18, 0x01, 0x08, reset_new, 0x00, 0x00 };
                    }
                    else                        // Car FMS
                    {
                        sendData = new byte[] {0x16, 0x16, 0x16, 0x16, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xb0, 0x26, 0x00, 0x02, 0x07, 0x00, 0x00, 0x00, 0x09, 0x09, 0x16, 0x00, fan_12v, rtk, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
};
                    }
                }
                else                // 구공
                {
                    if (carFmsSendType == 0)   // Scanner FMS
                    {
                        sendData = new byte[] { 0x16, 0x16, 0x16, 0x16, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xb0, 0x0b, 0x00, 0x02, 0x05, 0x00, 0x00, 0x00, 0x18, 0x01, 0x08, reset_new, 0x00, 0x00 };
                    }
                    else                        // Car FMS
                    {
                        sendData = new byte[] {0x16, 0x16, 0x16, 0x16, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xb0, 0x26, 0x00, 0x02, 0x07, 0x00, 0x00, 0x00, 0x09, 0x09, 0x16, 0x00, fan_12v, rtk, 0x32, 0x36, 0x32, 0x01, 0x30, 0x30, 0x30, 0x32, 0x36, 0x31, 0x01, 0x30, 0x30, 0x31, 0xff, 0x00, 0x3c, 0x00, 0x3c, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
};
                    }
                }
            }
            byte[] packet = new byte[sendData.Length + 2];
            Buffer.BlockCopy(sendData, 0, packet, 0, sendData.Length);
            BitConverter.GetBytes(gencrc_CCITT16(IgnoreHeader(packet))).CopyTo(packet, sendData.Length); //CRC 처리

            await server.SendData(packet);

            DataSendEvent?.Invoke("FMS - 상태값 응답");
        }



        private async void SetCarFmsCmd()
        {
            if (version == 0)
                receiveCMD = 0xC1;
            else
                receiveCMD = 0xD1;
        }

        protected static readonly ushort[] CRC16_TAB =
        {
            0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50a5, 0x60c6, 0x70e7,
            0x8108, 0x9129, 0xa14a, 0xb16b, 0xc18c, 0xd1ad, 0xe1ce, 0xf1ef,
            0x1231, 0x0210, 0x3273, 0x2252, 0x52b5, 0x4294, 0x72f7, 0x62d6,
            0x9339, 0x8318, 0xb37b, 0xa35a, 0xd3bd, 0xc39c, 0xf3ff, 0xe3de,
            0x2462, 0x3443, 0x0420, 0x1401, 0x64e6, 0x74c7, 0x44a4, 0x5485,
            0xa56a, 0xb54b, 0x8528, 0x9509, 0xe5ee, 0xf5cf, 0xc5ac, 0xd58d,
            0x3653, 0x2672, 0x1611, 0x0630, 0x76d7, 0x66f6, 0x5695, 0x46b4,
            0xb75b, 0xa77a, 0x9719, 0x8738, 0xf7df, 0xe7fe, 0xd79d, 0xc7bc,
            0x48c4, 0x58e5, 0x6886, 0x78a7, 0x0840, 0x1861, 0x2802, 0x3823,
            0xc9cc, 0xd9ed, 0xe98e, 0xf9af, 0x8948, 0x9969, 0xa90a, 0xb92b,
            0x5af5, 0x4ad4, 0x7ab7, 0x6a96, 0x1a71, 0x0a50, 0x3a33, 0x2a12,
            0xdbfd, 0xcbdc, 0xfbbf, 0xeb9e, 0x9b79, 0x8b58, 0xbb3b, 0xab1a,
            0x6ca6, 0x7c87, 0x4ce4, 0x5cc5, 0x2c22, 0x3c03, 0x0c60, 0x1c41,
            0xedae, 0xfd8f, 0xcdec, 0xddcd, 0xad2a, 0xbd0b, 0x8d68, 0x9d49,
            0x7e97, 0x6eb6, 0x5ed5, 0x4ef4, 0x3e13, 0x2e32, 0x1e51, 0x0e70,
            0xff9f, 0xefbe, 0xdfdd, 0xcffc, 0xbf1b, 0xaf3a, 0x9f59, 0x8f78,
            0x9188, 0x81a9, 0xb1ca, 0xa1eb, 0xd10c, 0xc12d, 0xf14e, 0xe16f,
            0x1080, 0x00a1, 0x30c2, 0x20e3, 0x5004, 0x4025, 0x7046, 0x6067,
            0x83b9, 0x9398, 0xa3fb, 0xb3da, 0xc33d, 0xd31c, 0xe37f, 0xf35e,
            0x02b1, 0x1290, 0x22f3, 0x32d2, 0x4235, 0x5214, 0x6277, 0x7256,
            0xb5ea, 0xa5cb, 0x95a8, 0x8589, 0xf56e, 0xe54f, 0xd52c, 0xc50d,
            0x34e2, 0x24c3, 0x14a0, 0x0481, 0x7466, 0x6447, 0x5424, 0x4405,
            0xa7db, 0xb7fa, 0x8799, 0x97b8, 0xe75f, 0xf77e, 0xc71d, 0xd73c,
            0x26d3, 0x36f2, 0x0691, 0x16b0, 0x6657, 0x7676, 0x4615, 0x5634,
            0xd94c, 0xc96d, 0xf90e, 0xe92f, 0x99c8, 0x89e9, 0xb98a, 0xa9ab,
            0x5844, 0x4865, 0x7806, 0x6827, 0x18c0, 0x08e1, 0x3882, 0x28a3,
            0xcb7d, 0xdb5c, 0xeb3f, 0xfb1e, 0x8bf9, 0x9bd8, 0xabbb, 0xbb9a,
            0x4a75, 0x5a54, 0x6a37, 0x7a16, 0x0af1, 0x1ad0, 0x2ab3, 0x3a92,
            0xfd2e, 0xed0f, 0xdd6c, 0xcd4d, 0xbdaa, 0xad8b, 0x9de8, 0x8dc9,
            0x7c26, 0x6c07, 0x5c64, 0x4c45, 0x3ca2, 0x2c83, 0x1ce0, 0x0cc1,
            0xef1f, 0xff3e, 0xcf5d, 0xdf7c, 0xaf9b, 0xbfba, 0x8fd9, 0x9ff8,
            0x6e17, 0x7e36, 0x4e55, 0x5e74, 0x2e93, 0x3eb2, 0x0ed1, 0x1ef0
        };

        /// <summary>
        /// CRC16 계산
        /// </summary>
        protected ushort gencrc_CCITT16(byte[] tmpByte)
        {
            ushort crc = 0;
            foreach (byte datum in tmpByte)
                crc = (ushort)(crc << 8 ^ CRC16_TAB[(crc >> 8 ^ datum) & 0x00FF]);

            return crc;
        }

        private byte[] IgnoreHeader(byte[] srcPacket)
        {
            int length = srcPacket.Length - 4 - 2; //crc 자리 부분도 잘라서 주기
            byte[] dstPacket = new byte[length];
            Buffer.BlockCopy(srcPacket, 4, dstPacket, 0, length);
            return dstPacket;
        }
    }
}
