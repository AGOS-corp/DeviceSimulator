using AddOnSimulator_SepVer;
using IniParser;
using IniParser.Model;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace AddOnSimulator_SepVer
{
    public static class ControlMethodLibrary
    {
        private static string[] timer = { "1", "10", "50", "100", "200", "300", "500", "1000", "2000" };
        private static string[] jammerType = { "DY", "KN", "DN", "AGOS" };
        private static string[] aisType = { "sLAN", "Serial" };
        private static string[] fmsType = { "구 FMS", "신 FMS", "차량형" };
        private static string[] fmsSite = { "Min", "Gu"};
        private static string[] fmsMode = { "Scanner", "Car"};

        private static FileIniDataParser parser = new FileIniDataParser();
        private static IniData configData = parser.ReadFile("Config.ini");

        private static BinaryReader adsbReader;
        private static BinaryReader aisReader;
        private static BinaryReader loopRadarReader;
        private static BinaryReader rfcoreRadarReader;

        private static Thread AdsbThread;
        private static Thread AisThread;
        private static Thread loopRadarThread;
        private static Thread rfcoreRadarThread;

        private static AisSend aisSend = new AisSend();
        private static LightSend lightSend1 = new LightSend();
        private static LightSend lightSend2 = new LightSend();
        private static SpooferSend spooferSend1 = new SpooferSend();
        private static SpooferSend spooferSend2 = new SpooferSend();
        private static JammerSend jammerSend1 = new JammerSend();
        private static JammerSend jammerSend2 = new JammerSend();
        private static FMSSend fmsSend = new FMSSend();

        public static void SetDefaultConfig(Form form)
        {
            var textBox = form.Controls.Find($"TB_Tcp_IP", true).FirstOrDefault() as TextBox;
            textBox.Text = configData["Control"]["TCP_IP"];

            textBox = form.Controls.Find($"TB_AIS_Port", true).FirstOrDefault() as TextBox;
            textBox.Text = configData["AIS"]["Port_AIS"];

            textBox = form.Controls.Find($"TB_ADSB_Port", true).FirstOrDefault() as TextBox;
            textBox.Text = configData["ADSB"]["Port_ADSB"];

            SetBinaryDeviceInfo(form);
            SetMultiDeviceInfo(form);
            SetFMSInfo(form);

            var comboBox = form.Controls.Find($"CB_Timeout_TCP", true).FirstOrDefault() as ComboBox;
            comboBox.Items.Clear();
            comboBox.Items.AddRange(timer);
            comboBox.SelectedIndex = int.Parse(configData["Control"]["SendTimeOut"]);

            SetReaderPath();
        }

        private static void SetBinaryDeviceInfo(Form form)
        {
            var comboBox = form.Controls.Find($"CB_AIS_Type", true).FirstOrDefault() as ComboBox;
            comboBox.Items.Clear();
            comboBox.Items.AddRange(aisType);
            comboBox.SelectedIndex = int.Parse(configData["AIS"]["TypeIndex"]);
        }

        private static void SetReaderPath()
        {
            adsbReader = new BinaryReader(@"./ADSB_Packets");
            aisReader = new BinaryReader(@"./AIS_Packets");
            loopRadarReader = new BinaryReader(@"./Loop_Packets");
            rfcoreRadarReader = new BinaryReader(@"./RFCore_Packets");
        }

        private static void SetFMSInfo(Form form)
        {
            var comboBox = form.Controls.Find($"CB_FMS_Type", true).FirstOrDefault() as ComboBox;
            comboBox.Items.Clear();
            comboBox.Items.AddRange(fmsType);
            var selectedIndex = int.Parse(configData["FMS"]["TypeIndex"]);
            comboBox.SelectedIndex = selectedIndex;

            comboBox = form.Controls.Find($"CB_FMS_Site", true).FirstOrDefault() as ComboBox;
            comboBox.Items.Clear();
            comboBox.Items.AddRange(fmsSite);
            comboBox.SelectedIndex = int.Parse(configData["FMS"]["SiteIndex"]);
            if (selectedIndex != 2) comboBox.Enabled = false;

            comboBox = form.Controls.Find($"CB_FMS_Mode", true).FirstOrDefault() as ComboBox;
            comboBox.Items.Clear();
            comboBox.Items.AddRange(fmsMode);
            comboBox.SelectedIndex = int.Parse(configData["FMS"]["ModeIndex"]);
            if (selectedIndex != 2) comboBox.Enabled = false;
        }

        private static void SetMultiDeviceInfo(Form form)
        {
            for(int i = 1; i <= 2; i++)
            {
                var radarTextBox = form.Controls.Find($"TB_Radar{i}_Port", true).FirstOrDefault() as TextBox;
                var lightTextBox = form.Controls.Find($"TB_Light{i}_Port", true).FirstOrDefault() as TextBox;
                var spooferTextBox = form.Controls.Find($"TB_Spoofer{i}_Port", true).FirstOrDefault() as TextBox;
                var jammerTextBox = form.Controls.Find($"TB_Jammer{i}_Port", true).FirstOrDefault() as TextBox;
                var jammerComboBox = form.Controls.Find($"CB_Jammer{i}_Type", true).FirstOrDefault() as ComboBox;


                if (i==0)
                    radarTextBox.Text = configData["Radar"]["Port_Loop"];
                else
                    radarTextBox.Text = configData["Radar"]["Port_RFCore"];

                lightTextBox.Text = configData["Light"][$"Port_{i}"];
                spooferTextBox.Text = configData["Spoofer"][$"Port_{i}"];


                jammerTextBox.Text = configData["Jammer"][$"Port_{i}"];
                jammerComboBox.Items.Clear();
                jammerComboBox.Items.AddRange(jammerType);
                jammerComboBox.SelectedIndex = int.Parse(configData["Jammer"][$"TypeIndex_{i}"]);
            }
        }


        public static void PrepareAISSimul(Form form)
        {
            var button = form.Controls.Find($"Btn_AIS_Conenct", true).FirstOrDefault() as Button;

            if (button.Text.Equals("Connect"))
            {
                var textBox = form.Controls.Find($"TB_Tcp_IP", true).FirstOrDefault() as TextBox;
                var serverIp = textBox.Text;

                textBox = form.Controls.Find($"TB_AIS_Port", true).FirstOrDefault() as TextBox;
                var aisPort = textBox.Text;

                var comboBox = form.Controls.Find($"CB_AIS_Type", true).FirstOrDefault() as ComboBox;
                var selectedIndex = comboBox.SelectedIndex;

                aisSend.SetNetwork(serverIp, aisPort, selectedIndex);
                AisThread = new Thread(() => aisReader.ReadFile(aisSend.SendAIS));
                AisThread.Start();

                button.Text = "Stop";
                button.BackColor = Color.Green;
            }
            else
            {
                aisSend.DisConnect();
                AisThread?.Abort();

                button.Text = "Connect";
                button.BackColor = Color.White;
            }
        }

        public static void PrepareADSBSimul(Form form)
        {
            var button = form.Controls.Find($"Btn_ADSB_Connect", true).FirstOrDefault() as Button;
            if (button.Text.Equals("Connect"))
            {
                var textBox = form.Controls.Find($"TB_ADSB_IP", true).FirstOrDefault() as TextBox;
                var dstIp = textBox.Text;

                textBox = form.Controls.Find($"TB_ADSB_Port", true).FirstOrDefault() as TextBox;
                var adsbPort = textBox.Text;

                AdsbSend.SetNetwork(dstIp, int.Parse(adsbPort));
                AdsbThread = new Thread(() => adsbReader.ReadFile(AdsbSend.SendADSB));
                // richTextBox1.AppendText($"[ADSB - {_UdpIP}] Start\r\n");
                AdsbThread.Start();

                button.Text = "Stop";
                button.BackColor = Color.Green;
            }
            else
            {
                AdsbThread?.Abort();
                // richTextBox1.AppendText($"[ADSB] Stop\r\n");

                button.Text = "Connect";
                button.BackColor = Color.White;
            }
        }


        public static void PrepareRadarSimul(Form form, int index)
        {
            var button = form.Controls.Find($"Btn_Radar{index}_Connect", true).FirstOrDefault() as Button;
            if (button.Text.Equals("Connect"))
            {
                var textBox = form.Controls.Find($"TB_Tcp_IP", true).FirstOrDefault() as TextBox;
                var serverIp = textBox.Text;

                textBox = form.Controls.Find($"TB_Radar{index}_Port", true).FirstOrDefault() as TextBox;
                var port = textBox.Text;

                if (index == 1) //Loop 
                {
                    RadarSend.SetLoopRadar(serverIp, int.Parse(port)); 
                    loopRadarThread = new Thread(() => loopRadarReader.ReadFile(RadarSend.SendData));
                    loopRadarThread.Start();
                }
                else            //RFCore
                {
                    RadarSend.SetRFCoreRadar(serverIp, int.Parse(port));
                    rfcoreRadarThread = new Thread(() => rfcoreRadarReader.ReadFile(RadarSend.SendData));
                    rfcoreRadarThread.Start();
                }

                // richTextBox1.AppendText($"[Radar] Start\r\n");
                
                button.BackColor = Color.Green;
                button.Text = "Stop";
            }
            else
            {
                if (index == 1) //Loop 
                {
                    loopRadarThread.Abort();
                    RadarSend.StopLoopRadar();
                }
                else            //RFCore
                {
                    rfcoreRadarThread.Abort();
                    RadarSend.StopRFCoreRadar();
                }

                // richTextBox1.AppendText($"[Radar] Stop\r\n");
                button.BackColor = Color.White;
                button.Text = "Connect";
            }
        }


        public static void PrepareLightSimul(Form form, int index)
        {
            var button = form.Controls.Find($"Btn_Light{index}_Connect", true).FirstOrDefault() as Button;
            if (button.Text.Equals("Connect"))
            {
                var textBox = form.Controls.Find($"TB_Tcp_IP", true).FirstOrDefault() as TextBox;
                var serverip = textBox.Text;

                textBox = form.Controls.Find($"TB_Light{index}_Port", true).FirstOrDefault() as TextBox;
                var port = textBox.Text;

                if (index==1)
                    lightSend1.SetNetwork(serverip, int.Parse(port), index);
                else
                    lightSend2.SetNetwork(serverip, int.Parse(port), index);

                // richTextBox1.AppendText($"[Light] Start\r\n");

                button.BackColor = Color.Green;
                button.Text = "Stop";
            }
            else
            {
                if (index == 1)
                    lightSend1.DisConnect();
                else
                    lightSend2.DisConnect();

                // richTextBox1.AppendText($"[Light] Stop\r\n");

                button.BackColor = Color.White;
                button.Text = "Connect";
            }
        }


        public static void PrepareSpooferSimul(Form form, int index)
        {
            var button = form.Controls.Find($"Btn_Spoofer{index}_Connect", true).FirstOrDefault() as Button;
            if (button.Text.Equals("Connect"))
            {
                var textBox = form.Controls.Find($"TB_Tcp_IP", true).FirstOrDefault() as TextBox;
                var serverIp = textBox.Text;
                textBox = form.Controls.Find($"TB_Spoofer{index}_Port", true).FirstOrDefault() as TextBox;
                var port = textBox.Text;

                if (index == 1)
                    spooferSend1.SetNetwork(serverIp, int.Parse(port), index);
                else
                    spooferSend2.SetNetwork(serverIp, int.Parse(port), index);

                // richTextBox1.AppendText($"[Spoofer] Start\r\n");
                button.BackColor = Color.Green;
                button.Text = "Stop";
            }
            else
            {
                if (index == 1)
                    spooferSend1.DisConnect();
                else
                    spooferSend2.DisConnect();

                // richTextBox1.AppendText($"[Spoofer] Stop\r\n");
                button.BackColor = Color.White;
                button.Text = "Connect";
            }
        }

        public static void PrepareJammerSimul(Form form, int index)
        {
            var button = form.Controls.Find($"Btn_Jammer{index}_Connect", true).FirstOrDefault() as Button;
            if (button.Text.Equals("Connect"))
            {
                var textBox = form.Controls.Find($"TB_Tcp_IP", true).FirstOrDefault() as TextBox;
                var serverIp = textBox.Text;

                textBox = form.Controls.Find($"TB_Jammer{index}_Port", true).FirstOrDefault() as TextBox;
                var port = textBox.Text;

                var comboBox = form.Controls.Find($"CB_Jammer{index}_Type", true).FirstOrDefault() as ComboBox;
                var selectedIndex = comboBox.SelectedIndex;

                switch(index)
                {
                    case 1: jammerSend1.SetNetwork(serverIp, int.Parse(port), selectedIndex, index); break;
                    case 2: jammerSend2.SetNetwork(serverIp, int.Parse(port), selectedIndex, index); break;
                }

                // richTextBox1.AppendText($"[Jammer] Start\r\n");
                button.BackColor = Color.Green;
                button.Text = "Stop";
            }
            else
            {
                switch (index)
                {
                    case 1: jammerSend1.DisConnect(); break;
                    case 2: jammerSend2.DisConnect(); break;
                }

                // richTextBox1.AppendText($"[Jammer] Stop\r\n");
                button.BackColor = Color.White;
                button.Text = "Connect";
            }
        }


        public static void PrepareFMSSimul(Form form)
        {
            var button = form.Controls.Find($"Btn_FMS_Connect", true).FirstOrDefault() as Button;
            if (button.Text.Equals("Connect"))
            {
                var textBox = form.Controls.Find($"TB_Tcp_IP", true).FirstOrDefault() as TextBox;
                var serverIp = textBox.Text;

                textBox = form.Controls.Find($"TB_FMS_Port", true).FirstOrDefault() as TextBox;
                var port = textBox.Text;

                var comboBox = form.Controls.Find($"CB_FMS_Type", true).FirstOrDefault() as ComboBox;
                var typeIndex = comboBox.SelectedIndex;

                comboBox = form.Controls.Find($"CB_FMS_Site", true).FirstOrDefault() as ComboBox;
                var siteIndex = comboBox.SelectedIndex;

                comboBox = form.Controls.Find($"CB_FMS_Mode", true).FirstOrDefault() as ComboBox;
                var modeIndex = comboBox.SelectedIndex;


                fmsSend.SetNetwork(serverIp, port, typeIndex, siteIndex, modeIndex);

                // richTextBox1.AppendText($"[FMS] Start\r\n");
                button.BackColor = Color.Green;
                button.Text = "Stop";
            }
            else
            {
                fmsSend.DisConnect();
                // richTextBox1.AppendText($"[FMS] Stop\r\n");
                button.BackColor = Color.White;
                button.Text = "Connect";
            }
        }


        public static void SaveConfigData(Form form)
        {
            var textBox = form.Controls.Find($"TB_Tcp_IP", true).FirstOrDefault() as TextBox;
            configData["Control"]["TCP_IP"] = textBox.Text;

            var comboBox = form.Controls.Find($"CB_Timeout_TCP", true).FirstOrDefault() as ComboBox;
            configData["Control"]["SendTimeOut"] = comboBox.SelectedIndex.ToString();

            GetMultiDeviceConfigData(form);
            GetFMSConfigData(form);

            parser.WriteFile("config.ini", configData);
        }

        private static void GetMultiDeviceConfigData(Form form)
        {
            for (int i = 1; i <= 2; i++)
            {
                var radarTextBox = form.Controls.Find($"TB_Radar{i}_Port", true).FirstOrDefault() as TextBox;
                var lightTextBox = form.Controls.Find($"TB_Light{i}_Port", true).FirstOrDefault() as TextBox;
                var spooferTextBox = form.Controls.Find($"TB_Spoofer{i}_Port", true).FirstOrDefault() as TextBox;
                var jammerTextBox = form.Controls.Find($"TB_Jammer{i}_Port", true).FirstOrDefault() as TextBox;
                var jammerComboBox = form.Controls.Find($"CB_Jammer{i}_Type", true).FirstOrDefault() as ComboBox;


                if (i == 0)
                    configData["Radar"]["Port_Loop"] = radarTextBox.Text;
                else
                    configData["Radar"]["Port_RFCore"] = radarTextBox.Text;

                configData["Light"][$"Port_{i}"] = lightTextBox.Text;
                configData["Spoofer"][$"Port_{i}"] = spooferTextBox.Text;
                configData["Jammer"][$"Port_{i}"] = jammerTextBox.Text;
                configData["Jammer"][$"TypeIndex_{i}"] = jammerComboBox.SelectedIndex.ToString();
            }
        }

        private static void GetFMSConfigData(Form form)
        {
            var comboBox = form.Controls.Find($"CB_FMS_Type", true).FirstOrDefault() as ComboBox;
            configData["FMS"]["TypeIndex"] = comboBox.SelectedIndex.ToString();

            comboBox = form.Controls.Find($"CB_FMS_Site", true).FirstOrDefault() as ComboBox;
            configData["FMS"]["SiteIndex"] = comboBox.SelectedIndex.ToString();

            comboBox = form.Controls.Find($"CB_FMS_Mode", true).FirstOrDefault() as ComboBox;
            configData["FMS"]["ModeIndex"] = comboBox.SelectedIndex.ToString();
        }

        public static void ChangeFMSType(Form form)
        {
            var comboBox = form.Controls.Find($"CB_FMS_Type", true).FirstOrDefault() as ComboBox;
            var selectedIndex = comboBox.SelectedIndex;

            var siteComboBox = form.Controls.Find($"CB_FMS_Site", true).FirstOrDefault() as ComboBox;
            var modeComboBox = form.Controls.Find($"CB_FMS_Mode", true).FirstOrDefault() as ComboBox;

            if (selectedIndex == 2)
            {
                siteComboBox.Enabled = true;
                modeComboBox.Enabled = true;
            }
            else
            {
                siteComboBox.Enabled = false;
                modeComboBox.Enabled = false;
            }
        }

        internal static void ChangeTimeout(Form form)
        {
            var comboBox = form.Controls.Find($"CB_Timeout_TCP", true).FirstOrDefault() as ComboBox;
            var timeOut = int.Parse(comboBox.Text);
            AisSend.timeOut = timeOut;
            AdsbSend.timeOut = timeOut;
            RadarSend.timeOut = timeOut;
        }
    }
}
