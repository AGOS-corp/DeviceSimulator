using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AddOnSimulator_SepVer.util;
using System.Diagnostics;

namespace AddOnSimulator_SepVer
{
    public class AisSend
    {
		private Random rnd = new Random();
        private TcpServer server;
        private SerialPort serialPort;

        private int what = 3;

        public static Action<string> DataSendEvent;

        public static int timeOut = 1000;
		private int sumCount = 0;
		private int currentCount = 0;
		private int fileLength = 0;
		private bool runAis = false;

        public void SetNetwork(string _serverIP, string _port, int typeIndex)
        {
            var fileEntries = Directory.GetFiles(@"./AIS_Packets", "*.bin");
            fileLength = fileEntries.Length;
			runAis = true;
            if (typeIndex == 0)
			{
				try
				{
                    server = new TcpServer();
					server.OpenTCPServer(_serverIP, int.Parse(_port), out string message, true);
                    server.MessageSendEvent += ShowLog;
                    ShowLog($"{message}");

                    what = typeIndex;
				}
				catch { } //TODO. 연결 취소 버튼 눌릴 때 발생 에러만 잡을 것
			}
            else
			{
				if(! _port.Contains("."))
				{
					serialPort = new SerialPort(_port, 4800);
					serialPort.Open();
					what = typeIndex;
				}
			}
		}

		public void DisConnect()
        {
			runAis = false;
            server?.CloseTCPServer();
            serialPort?.Dispose();
            ShowLog("Closed");
        }

        private void ShowLog(string message)
        {
            DataSendEvent?.Invoke($"AIS - {message}");
        }


        public async Task<bool> SendAIS(byte[] packets)
        {
			var count = 0;
			var read = 0;
			var index = 0;
			byte[] sendData = new byte[0];
            var rand = new Random();
            while (count < packets.Length - 1 && runAis)
			{
				read = rnd.Next(25, 30);

				for (int n = 1; n <= read; n++)
				{
					if (count + n >= packets.Length)
					{
						index = n - 1;
						break;
					}

					if (packets[count + n] == 0x00)
					{
						index = n;
						break;
					}
					index = n;
				}

				sendData = new byte[index];

				Buffer.BlockCopy(packets, count, sendData, 0, index);
				count += index;

				List<byte> byteList = sendData.ToList();
				byteList.RemoveAll(b => b == 0x00); // 0x00(NULL) 문자 제거
				byte[] filteredBytes = byteList.ToArray();

				if (filteredBytes.Length < 20)
					continue;

				filteredBytes[18] += (byte)sumCount;


                if (what == 0)
				{
                    if (await server.SendData(filteredBytes))
                    {
                        DataSendEvent?.Invoke("AIS - Data 송신");
                    }
                }
				else
				{
					if (serialPort != null)
					{
						serialPort.Write(filteredBytes, 0, filteredBytes.Length);
                        DataSendEvent?.Invoke("AIS - Data 송신");
					}
				}

                await Task.Delay(timeOut);
            }
            currentCount++;

			if (currentCount >= fileLength)
            {
				sumCount++;

				if(sumCount >= 5)
					sumCount = 0;

				currentCount = 0;
            }

            return false;
		}
    }
}