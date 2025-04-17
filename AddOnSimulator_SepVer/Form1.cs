using System;
using System.Threading;
using System.Windows.Forms;

namespace AddOnSimulator_SepVer
{
    public partial class Form1 : Form
    {
        private SemaphoreSlim controlSemaphore = new SemaphoreSlim(1, 1);
        private SemaphoreSlim scannerSemaphore = new SemaphoreSlim(1, 1);

        public Form1()
        {
            InitializeComponent();

            ScannerMethodLibrary.SetDefaultConfig(this);
            ScannerMethodLibrary.SetDroneSimuleProperty(this);

            ControlMethodLibrary.SetDefaultConfig(this);

            TB_Drone1_ID_TextChanged(this, EventArgs.Empty); // 드론 1 ID 텍스트박스 초기화
            TB_Drone2_ID_TextChanged(this, EventArgs.Empty); // 드론 2 ID 텍스트박스 초기화
            TB_Drone3_ID_TextChanged(this, EventArgs.Empty); // 드론 3 ID 텍스트박스 초기화

            SpooferSend.DataSendEvent += AppendControlLog;
            LightSend.DataSendEvent += AppendControlLog;
            FMSSend.DataSendEvent += AppendControlLog;
            JammerSend.DataSendEvent += AppendControlLog;
            AisSend.DataSendEvent += AppendControlLog;
            RadarSend.DataSendEvent += AppendControlLog;

            DroneSimulationSend.DataSendEvent += AppendScannerLog;
        }

        private async void AppendScannerLog(string data)
        {
            try
            {
                await scannerSemaphore.WaitAsync();

                RTB_Scanner_Log.Invoke(new MethodInvoker(delegate
                {
                    if (RTB_Scanner_Log.Text.Length > 1000)
                        RTB_Scanner_Log.Clear();

                    RTB_Scanner_Log.AppendText(data + Environment.NewLine);
                    RTB_Scanner_Log.ScrollToCaret();
                }));

                scannerSemaphore.Release();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                if (scannerSemaphore.CurrentCount == 0)
                    scannerSemaphore.Release();
            }
        }

        private async void AppendControlLog(string data)
        {
            try
            {
                await controlSemaphore.WaitAsync();

                RTB_Control_Log.Invoke(new MethodInvoker(delegate
                {
                    if (RTB_Control_Log.Text.Length > 1000)
                        RTB_Control_Log.Clear();

                    RTB_Control_Log.AppendText(data + Environment.NewLine);
                    RTB_Control_Log.ScrollToCaret();
                }));

                controlSemaphore.Release();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                if (controlSemaphore.CurrentCount == 0)
                    controlSemaphore.Release();
            }
        }


        ///////////////////////////////////// 여기서부터 Scanner Blazor 관련 Simulation 코드 //////////////////////////////////////////

        // 메시지 전송 주기 설정 버튼 클릭 이벤트
        private void Btn_SetTimeOut_Click(object sender, EventArgs e)
        {
            DroneSimulationSend.timeOut = int.Parse(CB_Timeout_UDP.Text);
            ScannerMethodLibrary.SaveConfigData(this);
        }


        // Start-Stop Simulation 버튼 클릭 이벤트
        private void Btn_Drone1_Simul_Click(object sender, EventArgs e)
        {
            ScannerMethodLibrary.PrepareSimulationStart(this, 1, TB_Udp_IP.Text);
        }
        private void Btn_Drone2_Simul_Click(object sender, EventArgs e)
        {
            ScannerMethodLibrary.PrepareSimulationStart(this, 2, TB_Udp_IP.Text);
        }
        private void Btn_Drone3_Simul_Click(object sender, EventArgs e)
        {
            ScannerMethodLibrary.PrepareSimulationStart(this, 3, TB_Udp_IP.Text);
        }


        // Drone 1,2,3 ID TextBox 변경 이벤트
        private void TB_Drone1_ID_TextChanged(object sender, EventArgs e)
        {
            ScannerMethodLibrary.ChangeDroneInfo(this, 1);
        }
        private void TB_Drone2_ID_TextChanged(object sender, EventArgs e)
        {
            ScannerMethodLibrary.ChangeDroneInfo(this, 2);
        }
        private void TB_Drone3_ID_TextChanged(object sender, EventArgs e)
        {
            ScannerMethodLibrary.ChangeDroneInfo(this, 3);
        }



        // Drone 1,2,3 속도 제어 ComboBox 변경 이벤트
        private void CB_Drone1_Vel_SelectedIndexChanged(object sender, EventArgs e)
        {
            ScannerMethodLibrary.ChangeDroneVelocity(this, 1);
        }
        private void CB_Drone2_Vel_SelectedIndexChanged(object sender, EventArgs e)
        {
            ScannerMethodLibrary.ChangeDroneVelocity(this, 2);
        }
        private void CB_Drone3_Vel_SelectedIndexChanged(object sender, EventArgs e)
        {
            ScannerMethodLibrary.ChangeDroneVelocity(this, 3);
        }


        // Drone 1,2,3 방향 제어 ComboBox 변경 이벤트
        private void CB_Drone1_Direction_SelectedIndexChanged(object sender, EventArgs e)
        {
            ScannerMethodLibrary.ChangeDroneDirection(this, 1);
        }
        private void CB_Drone2_Direction_SelectedIndexChanged(object sender, EventArgs e)
        {
            ScannerMethodLibrary.ChangeDroneDirection(this, 2);
        }
        private void CB_Drone3_Direction_SelectedIndexChanged(object sender, EventArgs e)
        {
            ScannerMethodLibrary.ChangeDroneDirection(this, 3);
        }


        // Drone 1,2,3 각도 제어 ComboBox 변경 이벤트
        private void CB_Drone1_Angle_SelectedIndexChanged(object sender, EventArgs e)
        {
            ScannerMethodLibrary.ChangeDroneAngle(this, 1);
        }
        private void CB_Drone2_Angle_SelectedIndexChanged(object sender, EventArgs e)
        {
            ScannerMethodLibrary.ChangeDroneAngle(this, 2);
        }
        private void CB_Drone3_Angle_SelectedIndexChanged(object sender, EventArgs e)
        {
            ScannerMethodLibrary.ChangeDroneAngle(this, 3);
        }


        // Drone 1,2,3 시나리오 ComboBox 변경 이벤트
        private void CB_Drone1_Scenario_SelectedIndexChanged(object sender, EventArgs e)
        {
            ScannerMethodLibrary.ChangeDroneScenario(this, 1);
        }
        private void CB_Drone2_Scenario_SelectedIndexChanged(object sender, EventArgs e)
        {
            ScannerMethodLibrary.ChangeDroneScenario(this, 2);
        }
        private void CB_Drone3_Scenario_SelectedIndexChanged(object sender, EventArgs e)
        {
            ScannerMethodLibrary.ChangeDroneScenario(this, 3);
        }

        private void Btn_Connect_SPSheild_Click(object sender, EventArgs e)
        {

        }





        ///////////////////////////////////// 여기서부터 Control Blazor 관련 Simulation 코드 //////////////////////////////////////////

        private void Btn_SaveSetting_TCP_Click(object sender, EventArgs e)
        {
            ControlMethodLibrary.SaveConfigData(this);
        }

        private void Btn_AIS_Conenct_Click(object sender, EventArgs e)
        {
            ControlMethodLibrary.PrepareAISSimul(this);
        }

        private void Btn_ADSB_Connect_Click(object sender, EventArgs e)
        {
            ControlMethodLibrary.PrepareADSBSimul(this);
        }

        private void CB_FMS_Type_SelectedIndexChanged(object sender, EventArgs e)
        {
            ControlMethodLibrary.ChangeFMSType(this);
        }


        private void Btn_Radar1_Connect_Click(object sender, EventArgs e)
        {
            ControlMethodLibrary.PrepareRadarSimul(this, 1);
        }
        private void Btn_Radar2_Connect_Click(object sender, EventArgs e)
        {
            ControlMethodLibrary.PrepareRadarSimul(this, 2);
        }



        private void Btn_Light1_Connect_Click(object sender, EventArgs e)
        {
            ControlMethodLibrary.PrepareLightSimul(this, 1);
        }
        private void Btn_Light2_Connect_Click(object sender, EventArgs e)
        {
            ControlMethodLibrary.PrepareLightSimul(this, 2);
        }



        private void Btn_Spoofer1_Connect_Click(object sender, EventArgs e)
        {
            ControlMethodLibrary.PrepareSpooferSimul(this, 1);
        }
        private void Btn_Spoofer2_Connect_Click(object sender, EventArgs e)
        {
            ControlMethodLibrary.PrepareSpooferSimul(this, 2);
        }



        private void Btn_Jammer1_Connect_Click(object sender, EventArgs e)
        {
            ControlMethodLibrary.PrepareJammerSimul(this, 1);
        }
        private void Btn_Jammer2_Connect_Click(object sender, EventArgs e)
        {
            ControlMethodLibrary.PrepareJammerSimul(this, 2);
        }

        private void Btn_FMS_Connect_Click(object sender, EventArgs e)
        {
            ControlMethodLibrary.PrepareFMSSimul(this);
        }

        private void CB_Timeout_TCP_SelectedIndexChanged(object sender, EventArgs e)
        {
            ControlMethodLibrary.ChangeTimeout(this);
        }

        private void Btn_Clear_TCP_Click(object sender, EventArgs e)
        {
            RTB_Control_Log.Clear();
        }
    }
}
