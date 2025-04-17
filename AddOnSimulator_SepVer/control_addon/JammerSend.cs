using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using u08 = System.Byte;
using s08 = System.Byte;
using System.Windows.Forms;
using System.Runtime.Remoting.Lifetime;
using System.IO;
using System.Threading;
using AddOnSimulator_SepVer.util;
using System.Runtime.InteropServices.ComTypes;

namespace AddOnSimulator_SepVer
{
	internal class JammerSend
    {
        public static Action<string> DataSendEvent;
		private CancellationTokenSource _cts = new CancellationTokenSource();

        private TcpServer server;
		private Random random = new Random();

        private bool isConnect = false;

        private short pan = 0;
		private short tilt = 0;

		private bool onoff24 = false;
		private bool onoff58 = false;
		private bool onoffl1 = false;
		private bool onoffl2 = false;
		private bool onoff400 = false;
		private bool onoff900 = false;

		private short out24 = 0;
		private short out58 = 0;
		private short outl1 = 0;
		private short outl2 = 0;
		private short out400 = 0;
		private short out900 = 0;

		private u08 step24 = 0;
		private u08 step58 = 0;
		private u08 stepl1 = 0;
		private u08 stepl2 = 0;
		private u08 step400 = 0;
		private u08 step900 = 0;

		private s08 temp24 = 0;
		private s08 temp58 = 0;
		private s08 templ1 = 0;
		private s08 templ2 = 0;
		private s08 temp400 = 0;
		private s08 temp900 = 0;


		//KNeTZ 사용 변수
		private bool isRebooting = false;
		private bool testMode = false;
		private bool PanTiltRunning = false;
		private bool receiveNewAngleReq = false; //기존에 돌던 PanTilt 로직이 있었는지 확인


		//DamsTec 사용 변수
		private bool isRadiation = false;

		private int jammerIndex = 0;
		private int jammerType = 0;


		// AGOS 사용 변수
		private byte[] agosPacket = new byte[60];
		private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);


        private void ShowLog(string message)
        {
            DataSendEvent?.Invoke($"Jammer[{jammerIndex}] - {message}");
        }

        public void SetNetwork(string _serverIP, int _port, int type, int index)
        {
            isConnect = true;
            jammerIndex = index;
            jammerType = type;

            server = new TcpServer();
			server.OpenTCPServer(_serverIP, _port, out string message);
            server.MessageSendEvent += ShowLog;
            server.DataSendEvent += ParcingData;
            ShowLog($"{message}");

			agosPacket[0] = 0x50;
            agosPacket[1] = 0x3C;
			agosPacket[agosPacket.Length - 1] = 0x5A;
        }

		public void DisConnect()
		{
			isConnect = false;
            server.CloseTCPServer();
            ShowLog("Closed");
        }

		private async void ParcingData(byte[] data)
		{
			switch (jammerType)
			{
				case 0: DYParcing(data); break;	// DY
				case 1: KNParcing(data); break;	// KN
				case 2: DNParcing(data); break;	// DN
				case 3: AGOSParcing(data); break;	// AGOS
			}
		}

        /// <summary>
        /// KNeTZ 메서드
        /// </summary>
        public async void KNParcing(byte[] receiveData)
		{
			try
			{
				switch (receiveData[5])
				{
					case 0x32:         //상태 질의
                        await KNSendState(receiveData);
                        break;

					case 0x34:       //각도 제어
						if (!PanTiltRunning)     //PanTilt가 대기중이었다면 바로 탐
						{
							PanTiltRunning = true;
							await Task.Run(() => KNSendAngle(receiveData));
						}
						else                    //PanTilt가 돌고있었다면 기존 로직 정지 
						{
							_cts.Cancel();
							_cts = new CancellationTokenSource();
							receiveNewAngleReq = true;
							Task.Run(() => KNSendAngle(receiveData));
							receiveNewAngleReq = false;
						}
						break;

					case 0x36:       //출력 제어
						await KNSendOutPut(receiveData);
						break;

					case 0x42:       //Reboot
						await KNSendReboot();
						break;

					case 0x44:       //TestMode
						testMode = true;
                        ShowLog($"Test Mode");
						break;

					case 0x45:       //ReleaseMode
						testMode = false;
                        ShowLog($"Release Mode");
						break;
				}
			}
			catch (Exception ex)
			{
				ShowLog($"err : {ex.Message}");
            }
		}

		private async Task KNSendReboot()
		{
			isRebooting = true;
            ShowLog($"Rebooting");
			await Task.Delay(10000);
			isRebooting = false;
		}

		private async Task KNSendOutPut(byte[] receiveData)
		{
			getOnOffData(receiveData);

			step24 = u08.Parse(receiveData[12].ToString());
			step58 = u08.Parse(receiveData[13].ToString());
			stepl1 = u08.Parse(receiveData[14].ToString());
			stepl2 = u08.Parse(receiveData[15].ToString());
			step400 = u08.Parse(receiveData[16].ToString());
			step900 = u08.Parse(receiveData[17].ToString());

			if (onoff24)
				out24 = (short)(20 - 3 * (int)step24);
			else
				out24 = 0;

			if (onoff58)
				out58 = (short)(20 - 3 * (int)step58);
			else
				out58 = 0;

			if (onoffl1)
				outl1 = (short)(20 - 3 * (int)stepl1);
			else
				outl1 = 0;

			if (onoffl2)
				outl2 = (short)(20 - 3 * (int)stepl2);
			else
				outl2 = 0;

			if (onoff400)
				out400 = (short)(20 - 3 * (int)step400);
			else
				out400 = 0;

			if (onoff900)
				out900 = (short)(20 - 3 * (int)step900);
			else
				out900 = 0;


            byte[] sendData = { 0x50, 0x04, 0x00, 0x25, 0x00, 0x37, 0x5A };
			server.SendData(sendData);
        }

        private async Task KNSendAngle(byte[] receiveData)
		{
            byte[] sendData = { 0x50, 0x08, 0x00, 0x24, 0x00, 0x34, 0x4C, 0xFF, 0xE2, 0xFF, 0x5A };
            server.SendData(sendData);

            var inpan = BitConverter.ToInt16(receiveData, 6);
            var intilt = BitConverter.ToInt16(receiveData, 8);

            var token = _cts.Token;

            while (inpan != pan || tilt != intilt)
            {
                if (pan < inpan && inpan - pan > 10)
                    pan += 10;
                else if (pan > inpan && pan - inpan > 10)
                    pan -= 10;
                else
                    pan = inpan;


                if (tilt < intilt && intilt - tilt > 10)
                    tilt += 10;
                else if (tilt > intilt && tilt - intilt > 10)
                    tilt -= 10;
                else
                    tilt = intilt;

                await Task.Delay(333, token);
                //새로운 각도 제어 요청이 들어오면 기존 로직 무시
                if (token.IsCancellationRequested || receiveNewAngleReq)
                {
                    ShowLog($"각도 제어 중지");
                    break;
                }
            }
            PanTiltRunning = false;
		}

		int tempCount = 10;
		private async Task KNSendState(byte[] receiveData)
		{
			if (tempCount >= 10)
			{
				GetTempPerHz();
				tempCount = 0;
			}
			tempCount++;

			var index = 6;

			byte[] sendData = new byte[44];
			sendData[0] = 0x50;
			sendData[1] = 0x29;
            sendData[2] = 0x00;
            sendData[3] = 0x23;
            sendData[4] = 0x00;
            sendData[5] = 0x33;
			BitConverter.GetBytes(pan).CopyTo(sendData, index);
			index += 2;

			BitConverter.GetBytes(tilt).CopyTo(sendData, index);
			index += 2;

			SetPacket_OnOff(ref sendData, ref index);

			SetPacket_Out(ref sendData, ref index);

			BitConverter.GetBytes(step24).CopyTo(sendData, index);
			index++;
			BitConverter.GetBytes(step58).CopyTo(sendData, index);
			index++;
			BitConverter.GetBytes(stepl1).CopyTo(sendData, index);
			index++;
			BitConverter.GetBytes(stepl2).CopyTo(sendData, index);
			index++;
			BitConverter.GetBytes(step400).CopyTo(sendData, index);
			index++;
			BitConverter.GetBytes(step900).CopyTo(sendData, index);
			index++;


			SetPacket_Temp(ref sendData, ref index);

			BitConverter.GetBytes(isRebooting).CopyTo(sendData, index);
			index++;
			BitConverter.GetBytes(testMode).CopyTo(sendData, index);
			index++;
			BitConverter.GetBytes(PanTiltRunning).CopyTo(sendData, index);
			index++;

            sendData[index] = 0x5A;

			//Task.Delay(10).Wait(); 
			server.SendData(sendData);

            ShowLog($"상태 정보 전송");
		}


		/// <summary>
		/// DSNC 메서드
		/// </summary>

		public async void DNParcing(byte[] receiveData)
		{
			try
			{
                switch (receiveData[5])
                {
                    case 0x32:         //상태 질의
                        await DNSendState(receiveData);
                        break;

                    case 0x36:       //출력 제어
                        await DNSendOutPut(receiveData);
                        break;
                }
            }
			catch (Exception ex)
			{
                ShowLog($"err : {ex.Message}");
            }
		}

		private async Task DNSendState(byte[] receiveData)
		{
			if (tempCount >= 10)
			{
				GetTempPerHz();
				tempCount = 0;
			}
			tempCount++;

			var index = 6;

			byte[] sendData = new byte[31];
            sendData[0] = 0x50;
            sendData[1] = 0x1C;
            sendData[2] = 0x00;
            sendData[3] = 0x23;
            sendData[4] = 0x00;
            sendData[5] = 0x33;

			SetPacket_OnOff(ref sendData, ref index);

			SetPacket_Out(ref sendData, ref index);

			SetPacket_Temp(ref sendData, ref index);

            sendData[index] = 0x5A;
			server.SendData(sendData);

            ShowLog($"상태 정보 전송");
		}

		private async Task DNSendOutPut(byte[] receiveData)
		{
			//DataSendEvent($"{jammerIndex}] - " + "출력 요청 수신");

			getOnOffData(receiveData);

			if (onoff24)
				out24 = (short)20;
			else
				out24 = 0;

			if (onoff58)
				out58 = (short)20;
			else
				out58 = 0;

			if (onoffl1)
				outl1 = (short)20;
			else
				outl1 = 0;

			if (onoffl2)
				outl2 = (short)20;
			else
				outl2 = 0;

			if (onoff400)
				out400 = (short)20;
			else
				out400 = 0;

			if (onoff900)
				out900 = (short)20;
			else
				out900 = 0;


            byte[] sendData = { 0x50, 0x04, 0x00, 0x25, 0x00, 0x37, 0x5A };

            server.SendData(sendData);
        }


		/// <summary>
		/// DymsTec에서 사용하는 메서드
		/// </summary>
		/// <returns></returns>
		private async void DYParcing(byte[] receiveData)
		{
			try
			{
                switch (receiveData[5])
                {
                    case 0x3A:      // 상태값
                        DYSendStatus(receiveData);
                        break;

                    case 0x40:      // 출력 설정
                        await DYSetOutput(receiveData);
                        break;

                    case 0x49:      // 방사 - 중지
                        await DYSetRadiation(receiveData);
                        break;
                }
            }
			catch (Exception ex)
			{
               ShowLog($"err : {ex.Message}");
            }
		}

        byte[] statusPacket = new byte[38];
        int tempChangeCount = 0;
        private void DYSendStatus(byte[] bytes)
        {
            var index = 0;

            statusPacket[index++] = 0x02;       // stx

            // len
            byte[] len = BitConverter.GetBytes((short)(statusPacket.Length - 4));
            Array.Reverse(len);
            Buffer.BlockCopy(len, 0, statusPacket, index, len.Length);
            index += len.Length;

            statusPacket[index++] = bytes[4];       // src  
            statusPacket[index++] = 0x40;           // dst
            statusPacket[index++] = 0x3A;           // cmd
            statusPacket[index++] = 0x30;           // code

            switch (bytes[4])
            {
                case 0x21:
                    index = SetBandData(ref statusPacket, onoffl2, outl2, index);
                    index = SetBandData(ref statusPacket, onoffl1, outl1, index);
                    break;

                case 0x22:
                    index = SetBandData(ref statusPacket, onoff24, out24, index);
                    index = SetBandData(ref statusPacket, onoff58, out58, index);
                    break;

                case 0x23:
                    index = SetBandData(ref statusPacket, onoff400, out400, index);
                    index = SetBandData(ref statusPacket, onoff900, out900, index);
                    break;
            }
            statusPacket[statusPacket.Length - 1] = 0x03;   // etx

            server.SendData(statusPacket);

			if (bytes[4] == 0x23)
                ShowLog($"상태 정보 전송");
        }

        private int SetBandData(ref byte[] statusPacket, bool onoff, short output, int index)
        {
            // Flag data 1 (1 Byte)
            if (onoff) statusPacket[index++] = 0xFF;
            else statusPacket[index++] = 0x7F;

            // Flag data 2 (1 Byte)
            statusPacket[index++] = 0xF0;

            // 현재 출력값 (2 Byte)
            byte[] tmp = BitConverter.GetBytes(output);
            Array.Reverse(tmp);
            Buffer.BlockCopy(tmp, 0, statusPacket, index, tmp.Length);
            index += tmp.Length;

            // 현재 Atten (2 Byte)
            index += 2;

            // 현재 온도 (1 Byte)
            statusPacket[index++] = (byte)(onoff ? 45 : 0);

            // Lower Limit, Upper Limit, Temp Limit (2 * 3 Byte)
            index += 2;
            index += 2;
            index += 2;

            // 목표 출력 값 (2 Byte)
            Buffer.BlockCopy(tmp, 0, statusPacket, index, tmp.Length);
            index += tmp.Length;

            return index;
        }


        byte[] responseSetting = new byte[10] { 0x02, 0x00, 0x04, 0x00, 0x40, 0x40, 0x30, 0x00, 0x00, 0x03 };
        private async Task DYSetOutput(byte[] bytes)
        {
            switch (bytes[4])
            {
                case 0x01:
                    if ((bytes[7] & 0x01) == 0x01)
                    {
                        byte[] dBm = { bytes[18], bytes[17] };
                        outl2 = BitConverter.ToInt16(dBm, 0);
                    }
                    break;
                case 0x02:
                    if ((bytes[7] & 0x01) == 0x01)
                    {
                        byte[] dBm = { bytes[18], bytes[17] };
                        outl1 = BitConverter.ToInt16(dBm, 0);
                    }
                    break;
                case 0x03:
                    if ((bytes[7] & 0x01) == 0x01)
                    {
                        byte[] dBm = { bytes[18], bytes[17] };
                        out24 = BitConverter.ToInt16(dBm, 0);
                    }
                    break;
                case 0x04:
                    if ((bytes[7] & 0x01) == 0x01)
                    {
                        byte[] dBm = { bytes[18], bytes[17] };
                        out58 = BitConverter.ToInt16(dBm, 0);
                    }
                    break;
                case 0x05:
                    if ((bytes[7] & 0x01) == 0x01)
                    {
                        byte[] dBm = { bytes[18], bytes[17] };
                        out400 = BitConverter.ToInt16(dBm, 0);
                    }
                    break;
                case 0x06:
                    if ((bytes[7] & 0x01) == 0x01)
                    {
                        byte[] dBm = { bytes[18], bytes[17] };
                        out900 = BitConverter.ToInt16(dBm, 0);
                    }
                    break;
            }
            responseSetting[3] = bytes[4];
            server.SendData(responseSetting);
        }

        private async Task DYSetRadiation(byte[] bytes)
        {
            switch (bytes[4])
            {
                case 0x01: onoffl2 = bytes[7] != 0x00 ? true : false; break;
                case 0x02: onoffl1 = bytes[7] != 0x00 ? true : false; break;
                case 0x03: onoff24 = bytes[7] != 0x00 ? true : false; break;
                case 0x04: onoff58 = bytes[7] != 0x00 ? true : false; break;
                case 0x05: onoff400 = bytes[7] != 0x00 ? true : false; break;
                case 0x06: onoff900 = bytes[7] != 0x00 ? true : false; break;
            }

            responseSetting[3] = bytes[4];
            server.SendData(responseSetting);
        }




        /// <summary>
        /// DymsTec에서 사용하는 메서드
        /// </summary>
        /// <returns></returns>
        private async void AGOSParcing(byte[] receiveData)
        {
			try
			{
                switch (receiveData[2])
                {
                    case 0x65:
                        await SendAgosJammerState();
                        break;

                    case 0x64:
                        await AGOSSetJammerState(receiveData);
                        break;

                    case 0x63:
                        await AGOSSendBitState();
                        break;
                }
            }
			catch (Exception ex)
			{
				ShowLog($"err : {ex.Message}");
            }
        }

		int count = 0;
        private async Task SendAgosJammerState()
        {
            await semaphoreSlim.WaitAsync();
            agosPacket[2] = 0x75;
			server.SendData(agosPacket);
            count++;

            if (count >= 100)
            {
                ChangeAGOSJammerRadiateState(ref agosPacket[53]);
                ChangeAGOSJammerRadiateState(ref agosPacket[54]);
                ChangeAGOSJammerRadiateState(ref agosPacket[55]);
                ChangeAGOSJammerRadiateState(ref agosPacket[56]);
                ChangeAGOSJammerRadiateState(ref agosPacket[57]);
                ChangeAGOSJammerRadiateState(ref agosPacket[58]);
                count = 0;
            }
            semaphoreSlim.Release();

            ShowLog($"AGOS Jammer 응답");
        }

        private void ChangeAGOSJammerRadiateState(ref byte state)
        {
            if (state != 0x02)
			{
				var rand = random.Next(0, 10);

				if (rand < 2) state = 0xFF;
				else state = 0x01;
            }
        }

        private async Task AGOSSendBitState()
        {
            byte[] sendData = { 0x50, 0x58, 0x73,  // STX, Length, CMD
                    0x02,	// common connect status
					0x04,	// PLL status
					0x08,	// device status
					0x10,	// 과전압
					0x20,	// 과전류
					0x20,	// 온도, FAN
                    0x03, 0x03, 0x03, 0x03, 0x03, 0x03,		// device status M1~M6
					
					// Temp M1~M6 (mega, gnss, giga, mcu)
                    0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,
                    0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,
                    0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,
                    0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,

					// PSU CH1~CH6 (voltage, current)
                    0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,
                    0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,   0x7B, 0x00,

                    0x7B, 0x00,   // PCU 온도
					0x7B, 0x00,	  // PCU input voltage
                    0x5A	// ETX
				};
			server.SendData(sendData);

            ShowLog($"AGOS Jammer BIT 응답");
        }

        private async Task AGOSSetJammerState(byte[] bytes)
        {
            if (bytes[0] != 0x50 || bytes[bytes.Length - 1] != 0x5A)
                return;

            if (bytes.Length != 52)
                return;

            await semaphoreSlim.WaitAsync();
            Buffer.BlockCopy(bytes, 0, agosPacket, 0, bytes.Length);
			ChangeAGOSJammerState(ref agosPacket[3], ref agosPacket[53]);
			ChangeAGOSJammerState(ref agosPacket[11], ref agosPacket[54]);
			ChangeAGOSJammerState(ref agosPacket[19], ref agosPacket[55]);
			ChangeAGOSJammerState(ref agosPacket[27], ref agosPacket[56]);
			ChangeAGOSJammerState(ref agosPacket[35], ref agosPacket[57]);
			ChangeAGOSJammerState(ref agosPacket[43], ref agosPacket[58]);
            agosPacket[1] = 0x3C;
            semaphoreSlim.Release();
        }

        private void ChangeAGOSJammerState(ref byte radiate, ref byte state)
        {
			if (state != 0xFF) state = (byte)(radiate == 0x00 ? 1 : 2);
			else radiate = 0x00;
        }



        /// <summary>
        /// KN, DN에서 공통으로 사용하는 메서드 _ Packet내 온도 값 바인딩
        /// </summary>
        private void SetPacket_Temp(ref byte[] sendData2, ref int index)
		{
			BitConverter.GetBytes(temp24).CopyTo(sendData2, index);
			index++;
			BitConverter.GetBytes(temp58).CopyTo(sendData2, index);
			index++;
			BitConverter.GetBytes(templ1).CopyTo(sendData2, index);
			index++;
			BitConverter.GetBytes(templ2).CopyTo(sendData2, index);
			index++;
			BitConverter.GetBytes(temp400).CopyTo(sendData2, index);
			index++;
			BitConverter.GetBytes(temp900).CopyTo(sendData2, index);
			index++;
		}

		/// <summary>
		/// KN, DN에서 공통으로 사용하는 메서드 _ Packet내 출력 값 바인딩
		/// </summary>
		private void SetPacket_Out(ref byte[] sendData2, ref int index)
		{
			BitConverter.GetBytes(out24).CopyTo(sendData2, index);
			index += 2;
			BitConverter.GetBytes(out58).CopyTo(sendData2, index);
			index += 2;
			BitConverter.GetBytes(outl1).CopyTo(sendData2, index);
			index += 2;
			BitConverter.GetBytes(outl2).CopyTo(sendData2, index);
			index += 2;
			BitConverter.GetBytes(out400).CopyTo(sendData2, index);
			index += 2;
			BitConverter.GetBytes(out900).CopyTo(sendData2, index);
			index += 2;
		}


		/// <summary>
		/// 공통으로 사용하는 메서드 _ Packet내 OnOff 값 바인딩
		/// </summary>
		private void SetPacket_OnOff(ref byte[] sendData2, ref int index)
		{
			BitConverter.GetBytes(onoff24).CopyTo(sendData2, index);
			index++;
			BitConverter.GetBytes(onoff58).CopyTo(sendData2, index);
			index++;
			BitConverter.GetBytes(onoffl1).CopyTo(sendData2, index);
			index++;
			BitConverter.GetBytes(onoffl2).CopyTo(sendData2, index);
			index++;
			BitConverter.GetBytes(onoff400).CopyTo(sendData2, index);
			index++;
			BitConverter.GetBytes(onoff900).CopyTo(sendData2, index);
			index++;
		}

		/// <summary>
		/// 공통으로 사용하는 대역별 온도 측정 메서드
		/// </summary>
		private void GetTempPerHz()
		{
			temp24 = (u08)(random.Next(25, 36));
			temp58 = (u08)(random.Next(25, 36));
			templ1 = (u08)(random.Next(25, 36));
			templ2 = (u08)(random.Next(25, 36));
			temp400 = (u08)(random.Next(25, 36));
			temp900 = (u08)(random.Next(25, 36));
		}

		/// <summary>
		/// 공통으로 사용하는 onoff 확인 메서드
		/// </summary>
		private void getOnOffData(byte[] receiveData)
		{
			onoff24 = receiveData[6] == 1 ? true : false;
			onoff58 = receiveData[7] == 1 ? true : false;
			onoffl1 = receiveData[8] == 1 ? true : false;
			onoffl2 = receiveData[9] == 1 ? true : false;
			onoff400 = receiveData[10] == 1 ? true : false;
			onoff900 = receiveData[11] == 1 ? true : false;
		}
	}
}
