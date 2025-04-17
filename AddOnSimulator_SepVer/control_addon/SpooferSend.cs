using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using AddOnSimulator_SepVer.util;
using System.Threading;

namespace AddOnSimulator_SepVer
{
    public class SPData
    {
        public string MessageID = "";
        public Parameters Parameters = new Parameters();
    }

    public class Parameters
    {
        public List<string> constellation = new List<string>();

        // Configure 메시지 속성
        public bool GPS_L1CA = false;
        public bool GLONASS_G1 = false;
        public bool GALILEO_E1 = false;
        public bool BEIDOU_B1 = false;
        public bool GPS_L2C = false;
        public bool QZSS_L1CA = false;

        // ConfigureDome 메시지 속성
        public double[] guidance_location = { 0.0, 0.0, 0.0 };
        public double circle_radius = 0;       // 반경 1 ~ 2000
        public double radius = 0;       // 반경 1 ~ 2000
        public double circle_omega = 0.01;  // 각속도 0.01 ~ 0.1
        public double power = 0;               // Amp Power -10 ~ 30 dBm

        // ConfigureGuidance 메시지 속성
        public double[] dome_location = { 0.0, 0.0, 0.0 };

        // StartEngine, StopEngine 메시지 속성
        public string engine_state = "Stopped";

        // SetOperationMode 메시지 속성
        public string op_mode = "Dome";

        // SetSpooferSignal 메시지 속성
        public string signal = "Off";

        // GnssSyncStatus 메시지 속성
        public string gnss_sync_state = "Updated";

        // LockTarget, UnlockTarget 메시지 속성
        public string target_id = "-1";
        public bool rf_ground_flag = false;
    }
    internal class SpooferSend
    {
        private SPData sp;
        private TcpServer server;
        public static Action<string> DataSendEvent;

        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        private string text = "";
        private int GNSScount = 0;
        private int spooferIndex = 0;
        private int count = 0;

        public string gnss_receiver = "True";
        public string gnss_antenna = "True";

        public void SetNetwork(string _serverIP, int _port, int index)
        {
            spooferIndex = index;
            sp = new SPData();

            server = new TcpServer();
            server.OpenTCPServer(_serverIP, _port, out string message);
            server.MessageSendEvent += ShowLog;
            server.DataSendEvent += ParcingData;
            ShowLog($"{message}");
        }

        public void DisConnect()
        {
            server.CloseTCPServer();
            ShowLog("Closed");
        }


        private void ShowLog(string message)
        {
            DataSendEvent?.Invoke($"Spoofer[{spooferIndex}] - {message}");
        }


        private async void ParcingData(byte[] receiveData)
        {
            try
            {

                var data = Encoding.UTF8.GetString(receiveData);

                await semaphore.WaitAsync();
                var splitData = data.Split(new[] { "\r\n" }, StringSplitOptions.None);

                foreach (var jsonData in splitData)
                {
                    if (jsonData.Contains("MessageID"))
                    {
                        using (JsonDocument document = JsonDocument.Parse(jsonData))
                        {
                            JsonElement root = document.RootElement;
                            sp.MessageID = root.GetProperty("MessageID").ToString();
                            // ShowLog(sp.MessageID);
                        }

                        switch (sp.MessageID)
                        {
                            case "RequestBIT":
                                await SendBIT();
                                break;

                            case "Poll":         //상태 질의
                                await Task.Run(() => SendState());

                                count++;
                                if (count >= 60)
                                {
                                    //GNSS Missing 로직 호출
                                    if (sp.Parameters.gnss_sync_state.Equals("Updated"))
                                        await SetGnssMissing();
                                    count = 0;
                                }
                                break;

                            case "Configure":
                                var tmpModel = JsonConvert.DeserializeObject<SPData>(data);

                                sp.Parameters.GPS_L1CA = false;
                                sp.Parameters.GLONASS_G1 = false;
                                sp.Parameters.GALILEO_E1 = false;
                                sp.Parameters.BEIDOU_B1 = false;
                                sp.Parameters.GPS_L2C = false;

                                for (int j = 0; j < tmpModel.Parameters.constellation.Count; j++)
                                {
                                    switch (tmpModel.Parameters.constellation[j])
                                    {
                                        case "GPS L1CA":
                                            sp.Parameters.GPS_L1CA = true;
                                            break;

                                        case "GLONASS G1":
                                            sp.Parameters.GLONASS_G1 = true;
                                            break;

                                        case "GALILEO E1":
                                            sp.Parameters.GALILEO_E1 = true;
                                            break;

                                        case "BEIDOU B1":
                                            sp.Parameters.BEIDOU_B1 = true;
                                            break;

                                        case "QZSS L1CA":
                                            sp.Parameters.QZSS_L1CA = true;
                                            break;

                                    }
                                }

                                await SetConfigure();
                                break;

                            case "ConfigureDome":
                                tmpModel = JsonConvert.DeserializeObject<SPData>(data);

                                sp.Parameters.dome_location[0] = tmpModel.Parameters.dome_location[0];
                                sp.Parameters.dome_location[1] = tmpModel.Parameters.dome_location[1];
                                sp.Parameters.dome_location[2] = tmpModel.Parameters.dome_location[2];

                                sp.Parameters.circle_radius = tmpModel.Parameters.circle_radius;
                                sp.Parameters.circle_omega = tmpModel.Parameters.circle_omega;
                                sp.Parameters.power = tmpModel.Parameters.power;

                                await SetDome();
                                break;

                            case "ConfigureDomeByRadius":
                                tmpModel = JsonConvert.DeserializeObject<SPData>(data);

                                sp.Parameters.dome_location[0] = tmpModel.Parameters.dome_location[0];
                                sp.Parameters.dome_location[1] = tmpModel.Parameters.dome_location[1];
                                sp.Parameters.dome_location[2] = tmpModel.Parameters.dome_location[2];

                                var radius = tmpModel.Parameters.radius;

                                sp.Parameters.circle_radius = radius;
                                sp.Parameters.circle_omega = (radius / 250) / 10.0 + 0.01;
                                sp.Parameters.power = radius / 100;

                                await SetDome();
                                break;

                            case "ConfigureGuidance":
                                tmpModel = JsonConvert.DeserializeObject<SPData>(data);

                                sp.Parameters.guidance_location[0] = tmpModel.Parameters.guidance_location[0];
                                sp.Parameters.guidance_location[1] = tmpModel.Parameters.guidance_location[1];
                                sp.Parameters.guidance_location[2] = tmpModel.Parameters.guidance_location[2];

                                await SetGuidance();
                                break;

                            case "StartEngine":
                                sp.Parameters.engine_state = "Starting";
                                await SetEngine();

                                Task.Run(async () =>
                                {
                                    await Task.Delay(1000);
                                    sp.Parameters.engine_state = "Started";
                                });

                                break;

                            case "StopEngine":
                                sp.Parameters.engine_state = "Stopping";
                                await SetEngine();

                                await Task.Run(async () =>
                                {
                                    await Task.Delay(1000);
                                    sp.Parameters.engine_state = "Stopped";
                                });

                                break;

                            case "SetOperationMode":
                                tmpModel = JsonConvert.DeserializeObject<SPData>(data);
                                sp.Parameters.op_mode = tmpModel.Parameters.op_mode;

                                await SetOPMode();
                                break;

                            case "SetSpoofSignal":
                                tmpModel = JsonConvert.DeserializeObject<SPData>(data);
                                sp.Parameters.signal = tmpModel.Parameters.signal;

                                await SetSignal();
                                break;

                            case "LockTarget":
                                tmpModel = JsonConvert.DeserializeObject<SPData>(data);
                                sp.Parameters.target_id = tmpModel.Parameters.target_id;

                                await SetTarget();
                                break;

                            case "UnlockTarget":
                                tmpModel = JsonConvert.DeserializeObject<SPData>(data);
                                if (sp.Parameters.target_id.Equals(tmpModel.Parameters.target_id))
                                {
                                    sp.Parameters.target_id = "-1";

                                    await SetTarget();
                                }
                                break;

                            case "RecoverGnssSync":
                                await Task.Run(() => GnssLogic());
                                break;


                            case "Shutdown":
                                ShowLog("ShutDown 수신) 연결 종료");
                                server.CloseTCPServer();
                                break;

                            case "EnableRFGround":
                                tmpModel = JsonConvert.DeserializeObject<SPData>(data);
                                sp.Parameters.rf_ground_flag = tmpModel.Parameters.rf_ground_flag;

                                await SendRFGround();
                                break;

                            case "UpdateDomeLocation":
                                tmpModel = JsonConvert.DeserializeObject<SPData>(data);

                                sp.Parameters.dome_location[0] = tmpModel.Parameters.dome_location[0];
                                sp.Parameters.dome_location[1] = tmpModel.Parameters.dome_location[1];
                                sp.Parameters.dome_location[2] = tmpModel.Parameters.dome_location[2];

                                await SetDome();
                                break;
                        }
                    }
                }
                semaphore.Release();
            }
            catch (Exception e)
            {
                ShowLog($"err : {e.Message}");
                if (semaphore.CurrentCount == 0)
                    semaphore.Release();
            }
        }


        private async Task SendRFGround()
        {
            text = "{ \"MessageID\": \"RFGround\",  \"Status\": { \"rf_ground_flag\": " + sp.Parameters.rf_ground_flag + "} } \r\n";

            await SendText(text, "RFGround");
        }

        private async Task SendBIT()
        {
            text = "{ \r\n  \"MessageID\": \"BIT\", \r\n  \"Status\": { \r\n    \"amp_gain_control\": True, \r\n    \"amp_power_control\": True, \r\n    \"gnss_receiver\":" + gnss_receiver + ", \r\n    \"gnss_antenna\": " + gnss_antenna + ", \r\n    \"clock\": True, \r\n    \"simulator\": True,\r\n    \"radar\": True \r\n  } \r\n}\r\n";
            await SendText(text, "BIT");
        }

        private async Task SetDome()
        {
            text = "{ \"MessageID\": \"DomeConfiguration\", \"Status\": { \"dome_location\": [" + sp.Parameters.dome_location[0] + "," + sp.Parameters.dome_location[1] + "," + sp.Parameters.dome_location[2] + "]," +
                    "\"circle_radius\" :" + sp.Parameters.circle_radius + ", \"circle_omega\":" + sp.Parameters.circle_omega + ", \"power\" :" + sp.Parameters.power + "}}\r\n";
            await SendText(text, "DomeConfiguration");
        }

        private async Task SetGuidance()
        {
            text = "{ \"MessageID\": \"GuidanceConfiguration\", \"Status\": { \"guidance_location\": [" + sp.Parameters.guidance_location[0] + "," + sp.Parameters.guidance_location[1] + "," + sp.Parameters.guidance_location[2] + "]}}\r\n";
            await SendText(text, "GuidanceConfiguration");
        }

        private async Task GnssLogic(bool TF = true)
        {
            text = "{ \"MessageID\": \"GnssSyncStatus\", \"Status\": { \"gnss_sync_state\": \"" + sp.Parameters.gnss_sync_state + "\" }}\r\n";
            await SendText(text, "GnssSyncStatus");

            if (TF)
            {
                await Task.Delay(10000);

                text = "{ \"MessageID\": \"GnssSyncStatus\", \"Status\": { \"gnss_sync_state\": \"" + sp.Parameters.gnss_sync_state + "\" }}\r\n";
                await SendText(text, "GnssSyncStatus");
            }
        }

        private async Task SetGnssMissing()
        {
            if (sp.Parameters.signal.Equals("Off"))
            {
                Task.Run(() => GnssLogic());
            }
            else
            {
                sp.Parameters.gnss_sync_state = "Missing";
                text = "{ \"MessageID\": \"GnssSyncStatus\", \"Status\": { \"gnss_sync_state\": \"" + sp.Parameters.gnss_sync_state + "\" }}\r\n";
                await SendText(text, "GnssSyncStatus");
            }
        }

        private async Task SetTarget()
        {
            text = "{ \"MessageID\": \"TargetLockedStatus\", \"Status\": { \"target_id\": \"" + sp.Parameters.target_id + "\" }}\r\n";
            await SendText(text, "TargetLockStatus");
        }

        private async Task SetOPMode()
        {
            text = "{ \"MessageID\": \"OperationMode\", \"Status\": { \"op_mode\": \"" + sp.Parameters.op_mode + "\" }}\r\n";
            await SendText(text, "OperationMode");
        }

        private async Task SetEngine()
        {
            text = "{ \"MessageID\": \"EngineStatus\", \"Status\": { \"engine_state\": \"" + sp.Parameters.engine_state + "\" }}\r\n";
            await SendText(text, "EngineStatus");
        }

        private async Task SetSignal()
        {
            text = "{ \"MessageID\": \"SpoofSignalStatus\", \"Status\": { \"signal\": \"" + sp.Parameters.signal + "\" }}\r\n";
            await SendText(text, "SpoofSignalStatus");
        }

        double[] gui = new double[3];
        private async Task SetConfigure()
        {
            text = "{ \"MessageID\": \"Configuration\", \"Status\": { \"constellation\": " + GetConstellation() + "}}\r\n";
            await SendText(text, "Configuration");
        }

        private async Task SendText(string text, string MessageID)
        {
            byte[] sendData = Encoding.UTF8.GetBytes(text);
            server.SendData(sendData);
        }

        private string GetConstellation()
        {
            var count = 0;
            var text = "[";

            if (sp.Parameters.GPS_L1CA)
            {
                if (count > 0)
                    text += ",";
                text += "\"GPS L1CA\"";
                count++;
            }
            if (sp.Parameters.GLONASS_G1)
            {
                if (count > 0)
                    text += ",";
                text += "\"GLONASS G1\"";
                count++;
            }
            if (sp.Parameters.GALILEO_E1)
            {
                if (count > 0)
                    text += ",";
                text += "\"GALILEO E1\"";
                count++;
            }
            if (sp.Parameters.BEIDOU_B1)
            {
                if (count > 0)
                    text += ",";
                text += "\"BEIDOU B1\"";
                count++;
            }
            if (sp.Parameters.GPS_L2C)
            {
                if (count > 0)
                    text += ",";
                text += "\"GPS L2C\"";
                count++;
            }
            if (sp.Parameters.QZSS_L1CA)
            {
                if (count > 0)
                    text += ",";
                text += "\"QZSS L1CA\"";
                count++;
            }
            text += "]";
            return text;
        }

        private async Task SendState()
        {
            try
            {
                await SetConfigure();
                await SetDome();
                await SetGuidance();
                await SetEngine();
                await SetOPMode();
                await SetSignal();
                await GnssLogic(false);
                await SetTarget();
                await SendRFGround();

                ShowLog("상태값 응답");
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
        }

    }
}
