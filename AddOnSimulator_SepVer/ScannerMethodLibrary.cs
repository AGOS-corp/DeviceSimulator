using IniParser;
using IniParser.Model;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AddOnSimulator_SepVer
{
    public static class ScannerMethodLibrary
    {
        private static string[] timer = { "1", "10", "50", "100", "200", "300", "500", "1000", "2000" };
        private static string[] droneNameData = {"Inspire 1", "Phantom 3 SE", "Phantom 3 Series",
                "Phantom 3 Std", "M100", "ACEONE", "WKM", "NAZA", "A2", "A3", "Phantom 4",
                "MG1", "None", "M600", "Phantom 3 4k", "Mavic Pro", "Inspire 2",
                "Phantom 4 Pro", "None", "N2", "Spark", "None", "M600 Pro", "Mavic Air",
                "M200", "Phantom 4 Series", "Phantom 4 Adv", "M210", "None", "M210RTK"};
        private static string[] directionData = { "북", "북동", "동", "동남", "남", "남서", "서", "북서", "구간 반복" };
        private static string[] droneAngle = { "0", "10", "20", "30", "40", "50" };
        private static string[] droneScenario = { "직선", "계속 ZigZag", "간헐적 ZigZag", "Circle" };
        private static string[] setVel = { "천천히", "보통", "빠르게", "매우 빠르게", "호버링" };


        private static string DefaultDroenID { get; set; } = "VH95129AG"; // 9자. 앞에 5자 추가로 더 붙여야 함 (시뮬 Index (2), D (1), 드론 Index (2))

        private static DroneSimulationSend[] droneSimulProperties = new DroneSimulationSend[3]; // 드론 시뮬레이터 3대까지 지원


        private static FileIniDataParser parser = new FileIniDataParser();
        private static IniData configData = parser.ReadFile("config.ini");

        public static void SetDefaultConfig(Form form)
        {
            for (int i = 0; i < droneSimulProperties.Length; i++)
                droneSimulProperties[i] = new DroneSimulationSend();

            var textBox = form.Controls.Find($"TB_Udp_IP", true).FirstOrDefault() as TextBox;
            textBox.Text = configData["Scanner"]["UDP_IP"].ToString();

            textBox = form.Controls.Find($"TB_SPSheild_Port", true).FirstOrDefault() as TextBox;
            textBox.Text = configData["Scanner"]["SPSheild_Port"].ToString();

            var comboBox = form.Controls.Find($"CB_Timeout_UDP", true).FirstOrDefault() as ComboBox;
            comboBox.Items.Clear();
            comboBox.Items.AddRange(timer);
            comboBox.SelectedIndex = int.Parse(configData["Scanner"]["SendTimeOut"].ToString());
        }

        /// <summary>
        /// Drone Simulation 초기 data Setting 메서드
        /// </summary>
        public static void SetDroneSimuleProperty(Form form)
        {
            for (int index = 1; index <= 3; index++)
            {
                var comboBox = form.Controls.Find($"CB_Drone{index}_Direction", true).FirstOrDefault() as ComboBox;
                comboBox.Items.Clear();
                comboBox.Items.AddRange(directionData);
                comboBox.SelectedIndex = int.Parse(configData[$"DroneSimulator{index}"]["Direction"].ToString());

                comboBox = form.Controls.Find($"CB_Drone{index}_Vel", true).FirstOrDefault() as ComboBox;
                comboBox.Items.Clear();
                comboBox.Items.AddRange(setVel);
                comboBox.SelectedIndex = int.Parse(configData[$"DroneSimulator{index}"]["Speed"].ToString());

                comboBox = form.Controls.Find($"CB_Drone{index}_Scenario", true).FirstOrDefault() as ComboBox;
                comboBox.Items.Clear();
                comboBox.Items.AddRange(droneScenario);
                comboBox.SelectedIndex = int.Parse(configData[$"DroneSimulator{index}"]["Scenario"].ToString());

                comboBox = form.Controls.Find($"CB_Drone{index}_Angle", true).FirstOrDefault() as ComboBox;
                comboBox.Items.Clear();
                comboBox.Items.AddRange(droneAngle);
                comboBox.SelectedIndex = int.Parse(configData[$"DroneSimulator{index}"]["Angle"].ToString());

                var textBox = form.Controls.Find($"TB_SImul{index}_Scanner_Port", true).FirstOrDefault() as TextBox;
                textBox.Text = configData[$"DroneSimulator{index}"]["DstPort"];

                textBox = form.Controls.Find($"TB_Drone{index}_ID", true).FirstOrDefault() as TextBox;
                textBox.Text = configData[$"DroneSimulator{index}"]["Model"];

                textBox = form.Controls.Find($"TB_Drone{index}_Lon", true).FirstOrDefault() as TextBox;
                textBox.Text = configData[$"DroneSimulator{index}"]["Lon"];

                textBox = form.Controls.Find($"TB_Drone{index}_Lat", true).FirstOrDefault() as TextBox;
                textBox.Text = configData[$"DroneSimulator{index}"]["Lat"];

                textBox = form.Controls.Find($"TB_Drone{index}_Alt", true).FirstOrDefault() as TextBox;
                textBox.Text = configData[$"DroneSimulator{index}"]["Alt"];

                var button = form.Controls.Find($"Btn_Simul{index}_Scanner", true).FirstOrDefault() as Button;
                button.Text = configData[$"DroneSimulator{index}"]["Scanner"];
            }
        }

        /// <summary>
        /// Simulation 시작 메서드
        /// </summary>
        public static void PrepareSimulationStart(Form form, int index, string udpIP)
        {
            var scannerButton = form.Controls.Find($"Btn_Simul{index}_Scanner", true).FirstOrDefault() as Button;
            var simulButton = form.Controls.Find($"Btn_Drone{index}_Simul", true).FirstOrDefault() as Button;
            var portText = form.Controls.Find($"TB_SImul{index}_Scanner_Port", true).FirstOrDefault() as TextBox;

            if (scannerButton.Text.Equals("Ocusync"))   //Ocusync로 드론 시뮬레이션을 돌릴 때
            {
                if (simulButton.Text.Equals("Start Simulation"))
                {
                    var result = GetDroneID(form, index, out string droneId, out int number);

                    if (result)
                    {
                        SetSimulationData(form, droneId, index);
                        droneSimulProperties[index - 1].SetNetwork(udpIP, int.Parse(portText.Text), index);

                        simulButton.BackColor = Color.Green;
                        simulButton.Text = "Stop Simulation";
                    }
                    else
                        MessageBox.Show("드론 ID를 숫자로 입력하세요.");
                }
                else
                {
                    droneSimulProperties[index - 1].Disconnect();
                    simulButton.BackColor = Color.White;
                    simulButton.Text = "Start Simulation";
                }
            }
        }

        /// <summary>
        /// 설정해 둔 Simulation data를 바탕으로 실제 Simulator 속성에 바인딩
        /// </summary>
        public static void SetSimulationData(Form form, string droneID, int index)
        {
            droneSimulProperties[index - 1].drone_id = droneID;

            var textBox = form.Controls.Find($"TB_Drone{index}_ID", true).FirstOrDefault() as TextBox;
            droneSimulProperties[index - 1].drone_model = textBox.Text;

            textBox = form.Controls.Find($"TB_Drone{index}_Lon", true).FirstOrDefault() as TextBox;
            droneSimulProperties[index - 1].drone_lon = Convert.ToSingle(textBox.Text);
            droneSimulProperties[index - 1].drone_before_lon = Convert.ToSingle(textBox.Text);
            droneSimulProperties[index - 1].operator_lon = Convert.ToSingle(textBox.Text);

            textBox = form.Controls.Find($"TB_Drone{index}_Lat", true).FirstOrDefault() as TextBox;
            droneSimulProperties[index - 1].drone_lat = Convert.ToSingle(textBox.Text);
            droneSimulProperties[index - 1].drone_before_lat = Convert.ToSingle(textBox.Text);
            droneSimulProperties[index - 1].operator_lat = Convert.ToSingle(textBox.Text);

            textBox = form.Controls.Find($"TB_Drone{index}_Alt", true).FirstOrDefault() as TextBox;
            droneSimulProperties[index - 1].drone_altitude = Convert.ToSingle(textBox.Text);
            droneSimulProperties[index - 1].operator_drone_altitude = Convert.ToSingle(textBox.Text) - 3;

            var comboBox = form.Controls.Find($"CB_Drone{index}_Direction", true).FirstOrDefault() as ComboBox;
            droneSimulProperties[index - 1].drone_direction = Convert.ToString(comboBox.Text);

            comboBox = form.Controls.Find($"CB_Drone{index}_Vel", true).FirstOrDefault() as ComboBox;
            switch (comboBox.Text)
            {
                case "천천히": droneSimulProperties[index - 1].drone_vel = 10; break;
                case "보통": droneSimulProperties[index - 1].drone_vel = 20; break;
                case "빠르게": droneSimulProperties[index - 1].drone_vel = 40; break;
                case "호버링": droneSimulProperties[index - 1].drone_vel = 0.0f; break;
            }
            droneSimulProperties[index - 1].drone_velocity = Convert.ToString(comboBox.Text);

            comboBox = form.Controls.Find($"CB_Drone{index}_Scenario", true).FirstOrDefault() as ComboBox;
            droneSimulProperties[index - 1].iDataMode = comboBox.SelectedIndex;

            comboBox = form.Controls.Find($"CB_Drone{index}_Angle", true).FirstOrDefault() as ComboBox;
            droneSimulProperties[index - 1].iZigZagValue = int.Parse(comboBox.Text);
        }


        /// <summary>
        /// 설정한 Drone ID를 바탕으로 드론 Info을 Label에 바인딩
        /// </summary>
        public static void ChangeDroneInfo(Form form, int index)
        {
            var result = GetDroneID(form, index, out string droneId, out int number);

            if (result)
            {
                var modelName = "";
                if (number >= 0 && number < droneNameData.Length)
                    modelName = droneNameData[number];
                else
                    modelName = "TestDrone";

                var label = form.Controls.Find($"LB_Drone{index}_Info", true).FirstOrDefault() as Label;
                label.Text = $"{droneId} / {modelName}";
            }
        }

        /// <summary>
        /// 입력한 Drone Number를 바탕으로 드론 ID를 생성하는 메서드
        /// </summary>
        public static bool GetDroneID(Form form, int index, out string droneID, out int number)
        {
            var textBox = form.Controls.Find($"TB_Drone{index}_ID", true).FirstOrDefault() as TextBox;
            var num = 0;
            var midddeID = "99";
            var isSuccess = int.TryParse(textBox.Text, out num);
            if (!isSuccess)
            {
                droneID = "";
                number = -999;
                return false;
            }
            if (num >= 0 && num < 30)
                midddeID = num.ToString("00");

            droneID = $"{index}{index}D" + midddeID + DefaultDroenID;
            number = num;
            return true;
        }

        /// <summary>
        /// Simulation Drone 속도 제어 메서드
        /// </summary>
        public static void ChangeDroneVelocity(Form form, int index)
        {
            var comboBox = form.Controls.Find($"CB_Drone{index}_Vel", true).FirstOrDefault() as ComboBox;
            droneSimulProperties[index - 1].drone_velocity = Convert.ToString(comboBox.Text);
        }

        /// <summary>
        /// Simulation Drone 방향 제어 메서드
        /// </summary>
        public static void ChangeDroneDirection(Form form, int index)
        {
            var comboBox = form.Controls.Find($"CB_Drone{index}_Direction", true).FirstOrDefault() as ComboBox;
            droneSimulProperties[index - 1].CancelToken();
            droneSimulProperties[index - 1].drone_before_direction = droneSimulProperties[index - 1].drone_direction;
            droneSimulProperties[index - 1].drone_direction = Convert.ToString(comboBox.Text);
        }

        /// <summary>
        /// Simulation Drone ZigZag 각도 제어 메서드
        /// </summary>
        public static void ChangeDroneAngle(Form form, int index)
        {
            var comboBox = form.Controls.Find($"CB_Drone{index}_Angle", true).FirstOrDefault() as ComboBox;
            droneSimulProperties[index - 1].iZigZagValue = int.Parse(comboBox.Text);
        }

        /// <summary>
        /// Simulation Drone 비행 시나리오 제어 메서드
        /// </summary>
        public static void ChangeDroneScenario(Form form, int index)
        {
            var comboBox = form.Controls.Find($"CB_Drone{index}_Scenario", true).FirstOrDefault() as ComboBox;
            droneSimulProperties[index - 1].iDataMode = comboBox.SelectedIndex;
        }

        /// <summary>
        /// config.ini 파일에 설정된 Scanner data를 저장하는 메서드
        /// </summary>
        /// <param name="form"></param>
        internal static void SaveConfigData(Form form)
        {
            var textBox = form.Controls.Find($"TB_Udp_IP", true).FirstOrDefault() as TextBox;
            configData["Scanner"]["UDP_IP"] = textBox.Text;

            textBox = form.Controls.Find($"TB_SPSheild_Port", true).FirstOrDefault() as TextBox;
            configData["Scanner"]["SPSheild_Port"] = textBox.Text;

            var comboBox = form.Controls.Find($"CB_Timeout_UDP", true).FirstOrDefault() as ComboBox;
            configData["Scanner"]["SendTimeOut"] = comboBox.SelectedIndex.ToString();

            GetSimulationConfigData(form);

            parser.WriteFile("config.ini", configData);
        }

        /// <summary>
        /// Drone Simulator의 data들을 Config 파일에 save하기 위한 load 메서드
        /// </summary>
        /// <param name="form"></param>
        private static void GetSimulationConfigData(Form form)
        {
            for (int index = 1; index <= 3; index++)
            {
                var textBox = form.Controls.Find($"TB_Drone{index}_ID", true).FirstOrDefault() as TextBox;
                configData[$"DroneSimulator{index}"]["Model"] = textBox.Text;

                textBox = form.Controls.Find($"TB_Drone{index}_Lon", true).FirstOrDefault() as TextBox;
                configData[$"DroneSimulator{index}"]["Lon"] = textBox.Text;

                textBox = form.Controls.Find($"TB_Drone{index}_Lat", true).FirstOrDefault() as TextBox;
                configData[$"DroneSimulator{index}"]["Lat"] = textBox.Text;

                textBox = form.Controls.Find($"TB_Drone{index}_Alt", true).FirstOrDefault() as TextBox;
                configData[$"DroneSimulator{index}"]["Alt"] = textBox.Text;

                textBox = form.Controls.Find($"TB_SImul{index}_Scanner_Port", true).FirstOrDefault() as TextBox;
                configData[$"DroneSimulator{index}"]["DstPort"] = textBox.Text;

                var comboBox = form.Controls.Find($"CB_Drone{index}_Direction", true).FirstOrDefault() as ComboBox;
                configData[$"DroneSimulator{index}"]["Direction"] = comboBox.SelectedIndex.ToString();

                comboBox = form.Controls.Find($"CB_Drone{index}_Vel", true).FirstOrDefault() as ComboBox;
                configData[$"DroneSimulator{index}"]["Speed"] = comboBox.SelectedIndex.ToString();

                comboBox = form.Controls.Find($"CB_Drone{index}_Scenario", true).FirstOrDefault() as ComboBox;
                configData[$"DroneSimulator{index}"]["Scenario"] = comboBox.SelectedIndex.ToString();

                comboBox = form.Controls.Find($"CB_Drone{index}_Angle", true).FirstOrDefault() as ComboBox;
                configData[$"DroneSimulator{index}"]["Angle"] = comboBox.SelectedIndex.ToString();

                var button = form.Controls.Find($"Btn_Simul{index}_Scanner", true).FirstOrDefault() as Button;
                configData[$"DroneSimulator{index}"]["Scanner"] = button.Text.ToString();
            }
        }
    }
}
