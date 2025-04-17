using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Remoting.Contexts;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.Net.WebSockets;

namespace AddOnSimulator_SepVer
{
	internal class DroneSimulationSend
	{
		private IPEndPoint serverEndPoint { get; set; }
		private UdpClient udpClient { get; set; }
		private IPAddress serverIP { get; set; }

        public string drone_id { get; set; } = "";
        public string drone_model { get; set; } = "";
        public float drone_lon { get; set; } = 0;
        public float drone_lat { get; set; } = 0;
        public double drone_before_lon { get; set; } = 0;
        public double drone_before_utcTime { get; set; } = 0;
        public double drone_before_lat { get; set; } = 0;
        public float drone_altitude { get; set; } = 0;
        public float operator_lon { get; set; } = 0;
        public float operator_lat { get; set; } = 0;
        public float operator_drone_altitude { get; set; } = 0;
        public float drone_vel { get; set; } = 0;

        // 방향
        public string drone_direction { get; set; } = "";
        public string drone_before_direction { get; set; } = "동";

        // 속도
        public string drone_velocity { get; set; } = "";

        public int iZigZagValue { get; set; } = 0;
        public int iDataMode { get; set; } = 0;

        public CancellationTokenSource _cts = new CancellationTokenSource();

        public static Action<string> DataSendEvent;

        public static int timeOut = 1000;

		private long _sendPacketIdx = 0;
		public double addSpeed = 0;
		public double addDroneLon = 0;
		public double addDroneLat = 0;

		private static int iOddNum = 0;

		private int iChkCnt = 0;
		private int iDataOffSet = 0;
		private Random freqRand = new Random();

        Random r = new Random();

        private bool connect = false;

		private int index = 0;

        private void ShowLog(string message)
        {
            DataSendEvent?.Invoke($"Scanner[{index}] - {message}");
        }

        public void SetNetwork(string _serverIP, int _port, int index)
		{
			serverIP = IPAddress.Parse(_serverIP);

			serverEndPoint = new IPEndPoint(serverIP, _port);
			// UDP 클라이언트를 생성합니다.
			udpClient = new UdpClient();

			connect = true;
			this.index = index;

            Task.Run(() => SimulationStart());
		}

		public void Disconnect()
		{
			connect = false;
			udpClient?.Close();
		}


		public async void SimulationStart(bool isMavlink = false)
		{
            int iLength = 185;
            //int iLength = 106;

            //스펙트럼 타입
            byte[] byte4 = new byte[4];
            byte[] byte1 = new byte[1];
            byte[] byte2 = new byte[2];
            byte[] byte34 = new byte[34];
            byte[] byte20 = new byte[20];
            byte[] byte8 = new byte[8];
            byte[] byte14 = new byte[14];
            byte[] byte16 = new byte[16];
            byte[] byte30 = new byte[30];

            byte[] sendData = new byte[iLength];

            while (connect)
			{
				try
				{
					var index = 0;

					//STX
					index = BindSTX(sendData, index);

					//Freq (24, 58)
					index = SelecteGHz(sendData, index);

					//Length
					sendData[index++] = (byte)iLength; // index = 6;

					index = PaddingDefaultData(sendData, index);

                    //드론 ID
                    byte16 = Encoding.UTF8.GetBytes(drone_id);
					if (byte16.Length != 16)
					{
						Array.Resize(ref byte16, 16);
					}
					Array.Copy(byte16, 0, sendData, index, byte16.Length);
                    index += byte16.Length;

                    GetDroneVelocity();

					GetDroneDataMode();
					
					GetDroneDirection(out var iHeading);

                    iHeading += iDataOffSet;


					// 드론 경도
					double droneLon = drone_lon;
					droneLon = drone_before_lon;
					droneLon = ((droneLon * Math.PI) / 0.0000001) / 180;
					byte4 = BitConverter.GetBytes((uint)droneLon);
					Array.Copy(byte4, 0, sendData, index, byte4.Length);
                    index += byte4.Length;

					// 드론 위도
					double droneLat = drone_lat;
					droneLat = droneLat + addDroneLat;
					droneLat = drone_before_lat;
					droneLat = ((droneLat * Math.PI) / 0.0000001) / 180;

					byte4 = BitConverter.GetBytes((uint)droneLat);
					Array.Copy(byte4, 0, sendData, index, byte4.Length);
                    index += byte4.Length;

					// 드론 고도
					byte2 = BitConverter.GetBytes((UInt16)drone_altitude);
					Array.Copy(byte2, 0, sendData, index, byte2.Length);
                    index += byte2.Length;

					// 조종자-드론 고도
					byte2 = BitConverter.GetBytes((Int16)(operator_drone_altitude * 10));
					Array.Copy(byte2, 0, sendData, index, byte2.Length);
                    index += byte2.Length;

					// 속도
					var droneVelocity = drone_vel;
					
					//V_North
					//드론 속도 * cos(iHeading)
					Int16 vNorth = (Int16)(droneVelocity * Math.Cos(iHeading) * 100);
					byte2 = BitConverter.GetBytes(vNorth);
					Array.Copy(byte2, 0, sendData, index, byte2.Length);
                    index += byte2.Length;

					//V_East
					//드론 속도 * sin(iHeading)
					Int16 vEast = (Int16)(droneVelocity * Math.Sin(iHeading) * 100);
					byte2 = BitConverter.GetBytes(vEast);
					Array.Copy(byte2, 0, sendData, index, byte2.Length);
                    index += byte2.Length;

					//V_Up  
					byte2[0] = 0x00;
					byte2[1] = 0x00;
					Array.Copy(byte2, 0, sendData, index, byte2.Length);
                    index += byte2.Length;

					// 코스각  (== 방향..?)
					//byte4 = BitConverter.GetBytes((float)35);
					byte2 = BitConverter.GetBytes((Int16)iHeading);
					Array.Copy(byte2, 0, sendData, index, byte2.Length);
                    index += byte2.Length;

					// 시간
					var timeSpan = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
					byte8 = BitConverter.GetBytes((ulong)timeSpan.TotalSeconds);
					if (byte8.Length != 8)
					{
						Array.Resize(ref byte8, 8);
					}
					Array.Copy(byte8, 0, sendData, index, byte8.Length);
                    index += byte8.Length;

					// 조종자 위도
					double operatorLat = operator_lat;
					byte4 = BitConverter.GetBytes((float)operatorLat);
					Array.Copy(byte4, 0, sendData, index, byte4.Length);
                    index += byte4.Length;

					// 조종자 경도
					double operatorLon = operator_lon;
                    byte4 = BitConverter.GetBytes((float)operatorLon);
					Array.Copy(byte4, 0, sendData, index, byte4.Length);
                    index += byte4.Length;

					//드론 홈 경도
					byte4 = BitConverter.GetBytes((float)operatorLon);
					Array.Copy(byte4, 0, sendData, index, byte4.Length);
                    index += byte4.Length;

					//드론 홈 위도
					byte4 = BitConverter.GetBytes((float)operatorLat);
					Array.Copy(byte4, 0, sendData, index, byte4.Length);
                    index += byte4.Length;

					// 드론 모델
					byte modelData = byte.Parse(drone_model);
					sendData[index++] = modelData;


					double dUtcTime = DateTime.Now.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

					double iDiffTime = dUtcTime - drone_before_utcTime;

					if (drone_before_utcTime < 0.1)
					{
						iDiffTime = 1000;
					}

					addSpeed = addSpeed * (iDiffTime / 1000);

					double[] res = getvertaxList(drone_before_lon, drone_before_lat, iHeading, addSpeed);
					drone_before_lon = res[0];
					drone_before_lat = res[1];

					drone_before_utcTime = dUtcTime;

                    //UUID_Len
                    index += 1;

                    //UUID
                    index += 20;

                    //CRC
                    index += 2;

                    //G.D
                    index += 85;

					//ETX
					byte4[0] = 0x12;
					byte4[1] = 0x34;
					byte4[2] = 0x56;
					byte4[3] = 0x03;
					Array.Copy(byte4, 0, sendData, index, byte4.Length);
                    //iPos = iPos + byte2.Length;

                    //데이터 UDP 송신
                    udpClient.Send(sendData, sendData.Length, serverEndPoint);
                    ShowLog("Data 전송");

                    Task.Delay(timeOut).Wait();

					double tmpTime = DateTime.Now.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
					long tmpTime2 = (long)tmpTime;
				}
				catch (Exception ex)
				{
                    ShowLog("err- 종료");
				}
			}
			ShowLog("Done");
		}

        private void GetDroneDirection(out int iHeading)
        {
            iHeading = 0;
            switch (drone_direction)
            {
                case "구간 반복":
                    drone_direction = drone_before_direction;
                    Task.Run(() => ChangeDirectionUsingTimer());
                    break;

                case "북":  //0도
                    iHeading = 0;
                    addDroneLon = addDroneLon;
                    addDroneLat = addDroneLat + addSpeed;
                    break;

                case "북동":  //45도
                    iHeading = 45;
                    addDroneLon = addDroneLon + addSpeed;
                    addDroneLat = addDroneLat + addSpeed;
                    break;

                case "동":
                    iHeading = 90;
                    addDroneLon = addDroneLon + addSpeed;
                    addDroneLat = addDroneLat;
                    break;

                case "동남":
                    iHeading = 135;
                    addDroneLon = addDroneLon + addSpeed;
                    addDroneLat = addDroneLat - addSpeed;
                    break;

                case "남":
                    iHeading = 180;
                    addDroneLon = addDroneLon;
                    addDroneLat = addDroneLat - addSpeed;
                    break;

                case "남서":
                    iHeading = 225;
                    addDroneLon = addDroneLon - addSpeed;
                    addDroneLat = addDroneLat - addSpeed;
                    break;

                case "서":
                    iHeading = 270;
                    addDroneLon = addDroneLon - addSpeed;
                    addDroneLat = addDroneLat;
                    break;

                case "북서":
                    iHeading = 315;
                    addDroneLon = addDroneLon - addSpeed;
                    addDroneLat = addDroneLat + addSpeed;
                    break;

                default:
                    break;
            }
        }

        private void GetDroneDataMode()
        {
			//iDataOffSet = r.Next(-30, 30);
			//iDataMode 0 : 일반  1 : ZigZag  2: 10번에 2번씩 지그재그  3: 원으로 
            switch (iDataMode)
            {
                case 0:
                    iDataOffSet = 0;
                    break;
                case 1:
                    if (iChkCnt > 20)
                    {
                        iZigZagValue *= -1;     // 간헐적 지그재그 이후 지그재그로 돌아올 시 원래 각도로 돌아오기 위한 로직
                        iDataOffSet = iZigZagValue;
                        iChkCnt = 0;
                    }
                    iChkCnt += 1;
                    break;

                case 2:
                    if (iChkCnt > 20)
                    {
                        iDataOffSet = r.Next(-iZigZagValue, iZigZagValue);
                        iChkCnt = 0;
                    }
                    iChkCnt += 1;
                    break;

                case 3:
                    iDataOffSet += 1;
                    if (iDataOffSet >= 360)
                        iDataOffSet = 0;

                    break;
            }
        }

        private void GetDroneVelocity()
        {
            switch (drone_velocity)
            {
                case "천천히":  //m/s

                    addSpeed = 0.010;
                    drone_vel = 10;
                    break;

                case "보통":
                    addSpeed = 0.020;
                    drone_vel = 20;
                    break;

                case "빠르게":
                    drone_vel = 40;
                    addSpeed = 0.040;
                    break;

                case "매우 빠르게":
                    drone_vel = 80;
                    addSpeed = 0.080;
                    break;

                case "호버링":
                    drone_vel = 0;
                    addSpeed = 0.0;
                    break;

                default:

                    break;
            }
        }

        private int PaddingDefaultData(byte[] sendData, int index)
        {
			//UNK
            sendData[index++] = 0x00;

            //Version
            sendData[index++] = 0x00;

            //Seq_Num
            sendData[index++] = 0x00;
            sendData[index++] = 0x00;

            //State_Info
            sendData[index++] = 0x00;
            sendData[index++] = 0x00;

            return index;
        }

        private int SelecteGHz(byte[] sendData, int index)
        {
			// 시작 index = 4;
            var randInt = freqRand.Next(0, 10);
            if (randInt <= 8)
                sendData[index++] = 0x24;
            else
                sendData[index++] = 0x58;

			return index;
        }

        private int BindSTX(byte[] sendData, int index)
        {
			// 시작 index = 0
            sendData[index++] = 0xAB;
            sendData[index++] = 0xCD;
            sendData[index++] = 0xEF;
            sendData[index++] = 0x02;
			return index;
        }

        public void CancelToken()
		{
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

		public void ChangeDirection(string direction)
		{
			drone_direction = direction;
			drone_before_direction = direction;
		}

        private async void ChangeDirectionUsingTimer()
        {
			try
			{
				while(true)
				{
					switch (drone_direction)
					{
                        case "북": drone_direction = "남"; break;
                        case "북동": drone_direction = "남서"; break;
                        case "동": drone_direction = "서"; break;
                        case "동남": drone_direction = "북서"; break;
                        case "남": drone_direction = "북"; break;
                        case "남서": drone_direction = "북동"; break;
                        case "서": drone_direction = "동"; break;
                        case "북서": drone_direction = "동남"; break;
                        default: break;
                    }
                    await Task.Delay(10000, _cts.Token);
                }
            }
			catch (Exception ex)
			{

			}
        }

        public static double[] getvertaxList(double defX, double defY, double defA, double dist)
		{
			double[] res = new double[2];

			if (defA == 360)
			{
				defA = 0;
			}

			double rad = defA * Math.PI / 180;

			double x1 = dist * Math.Sin(rad);
			double y1 = dist * Math.Cos(rad);

			double thetax = x1 * 360 / 32000;
			double thetay = y1 * 360 / 40000;

			res[0] = defX + thetax;
			res[1] = defY + thetay;

			return res;
		}
	}
}
