using AddOnSimulator_SepVer.util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AddOnSimulator_SepVer
{
    internal class PanTiltSend
    {
        private TcpServer server;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public static Action<string> DataSendEvent;

        private bool isConnect = false;
        private int panTiltIndex = 0;

        private readonly ushort tiltMin = 10000;
        private readonly ushort tiltMax = 20396;
        private readonly ushort panMin = 25913;
        private readonly ushort panMax = 40000;

        private readonly ushort tiltMid = 13305;
        private readonly ushort panMid = 32960;

        private ushort tiltNow = 13305;
        private ushort panNow = 32960;

        private byte[] packet = new byte[11];

        private byte[] tiltArray = new byte[2];
        private byte[] panArray = new byte[2];

        public void SetNetwork(string _serverIP, int _port, int index)
        {
            packet[0] = 0xF0;
            packet[1] = 0x01;
            server = new TcpServer();
            server.OpenTCPServer(_serverIP, _port, out string message);
            server.MessageSendEvent += ShowLog;
            server.DataSendEvent += ParcingData;
            ShowLog($"{message}");
            isConnect = true;
            panTiltIndex = index;
        }

        public void DisConnect()
        {
            server?.CloseTCPServer();
            ShowLog("Closed");
        }

        private void ShowLog(string message)
        {
            DataSendEvent?.Invoke($"PanTilt[{panTiltIndex}] - {message}");
        }

        private async void ParcingData(byte[] receiveData)
        {
            try
            {
                switch (receiveData[3])
                {
                    case 0x7D:      // PanTilt 최소치 호출
                        await SendValue("Min");
                        break;

                    case 0x7B:      // PanTilt 최대치 호출
                        await SendValue("Max");
                        break;

                    case 0x71:      // PanTilt절대값 제어
                        _cts?.Cancel();
                        _cts = new CancellationTokenSource();
                        // ShowLog("각도 제어 취소");
                        Task.Run(() => SetAngle(receiveData));
                        break;

                    case 0x81:
                        await SendStatus();
                        break;
                }
            }
            catch (Exception ex)
            {
                ShowLog("err - " + ex.Message);
            }
        }


        private async Task SendStatus()
        {
            tiltArray = BitConverter.GetBytes((ushort)tiltNow);
            panArray = BitConverter.GetBytes((ushort)panNow);
            Array.Reverse(tiltArray);
            Array.Reverse(panArray);
            Buffer.BlockCopy(tiltArray, 0, packet, 2, tiltArray.Length);
            Buffer.BlockCopy(panArray, 0, packet, 4, panArray.Length);
            await server.SendData(packet);

            ShowLog("PanTilt 상태 응답");
        }

        private async Task SendValue(string target)
        {
            switch (target)
            {
                case "Min":
                    tiltArray = BitConverter.GetBytes(tiltMin);
                    panArray = BitConverter.GetBytes(panMin);
                    break;

                case "Max":
                    tiltArray = BitConverter.GetBytes(tiltMax);
                    panArray = BitConverter.GetBytes(panMax);
                    break;

                default:
                    return;
            }

            Array.Reverse(tiltArray);
            Array.Reverse(panArray);
            Buffer.BlockCopy(tiltArray, 0, packet, 2, tiltArray.Length);
            Buffer.BlockCopy(panArray, 0, packet, 4, panArray.Length);
            await server.SendData(packet);
        }

        private async Task SetAngle(byte[] bytes)
        {
            byte[] tiltReceive = { bytes[5], bytes[4] };
            byte[] panReceive = { bytes[7], bytes[6] };

            var tiltTarget = BitConverter.ToUInt16(tiltReceive, 0);
            var panTarget = BitConverter.ToUInt16(panReceive, 0);

            try
            {
                while (tiltNow != tiltTarget || panNow != panTarget)
                {
                    if (tiltNow != tiltTarget)
                    {
                        if (tiltNow < tiltTarget && tiltTarget - tiltNow > 82 * 2)
                            tiltNow += 82 * 2;
                        else if (tiltNow > tiltTarget && tiltNow - tiltTarget > 82 * 2)
                            tiltNow -= 82 * 2;
                        else
                            tiltNow = tiltTarget;
                    }

                    if (panNow != panTarget)
                    {
                        if (panNow < panTarget && panTarget - panNow > 39 * 6)
                            panNow += 39 * 6;
                        else if (panNow > panTarget && panNow - panTarget > 39 * 6)
                            panNow -= 39 * 6;
                        else
                            panNow = panTarget;
                    }

                    await Task.Delay(100, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    return;

                ShowLog("err - " + ex.Message);
            }
        }

    }
}
