using System;
using System.Threading.Tasks;
using AddOnSimulator_SepVer.util;

namespace AddOnSimulator_SepVer
{
	internal class LightSend
	{
        private TcpServer server;
        private bool isConnect = false;
		public static Action<string> DataSendEvent;

		private byte[] sendData = new byte[10];

		private byte redLight = 0x00;
		private byte yellowLight = 0x00;
		private byte greenLight = 0x00;
		private byte blueLight = 0x00;
		private byte whiteLight = 0x00;
		private byte soundState = 0x00;

		private int lightIndex = 0;

		public void SetNetwork(string _serverIP, int _port, int index)
		{
            server = new TcpServer();
            server.OpenTCPServer(_serverIP, _port, out string message);
            server.MessageSendEvent += ShowLog;
            server.DataSendEvent += ParcingData;
            ShowLog($"{message}");

            isConnect = true;
			lightIndex = index;
		}

		public void DisConnect()
        {
            server.CloseTCPServer();
            ShowLog("Closed");
        }

        private void ShowLog(string message)
        {
            DataSendEvent?.Invoke($"Light[{lightIndex}] - {message}");
        }


        public async void ParcingData(byte[] receiveData)
		{
			try
			{
                switch (receiveData[0])
                {
                    case 0x52:         //상태 질의
                        await SendState(receiveData);
                        break;

                    case 0x57:       //출력 제어
                        await SetLightState(receiveData);
                        break;
                }
            }
			catch (Exception ex)
			{
				ShowLog("err - " + ex.Message);
            }
		}

		private async Task SendState(byte[] receiveData)
		{
			sendData[0] = 0x41;
			sendData[1] = 0x00;	//sound는 built in 일 경우 0x00
			sendData[2] = redLight;
			sendData[3] = yellowLight;
			sendData[4] = greenLight;
			sendData[5] = blueLight;
			sendData[6] = whiteLight;
			sendData[7] = soundState;

			server.SendData(sendData);

            ShowLog("상태 정보 전송");
		}

		private async Task SetLightState(byte[] receiveData)
		{
			redLight = receiveData[2];
			yellowLight = receiveData[3];
			greenLight = receiveData[4];
			blueLight = receiveData[5];
			whiteLight = receiveData[6];
			soundState = receiveData[7];

            server.SendData(sendData);

            ShowLog("설정 완료");
		}
	}
}
