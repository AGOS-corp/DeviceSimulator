using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


namespace AddOnSimulator_SepVer
{
    public class BinaryReader
    {
        public delegate void DataGetEventHandler(string data);
        public Action<string> DataSendEvent;
        
        private string folderPath { get; set; }

        public Action Reconnect;

        public bool isRun = false;

        public BinaryReader(string _folderPath)
        {
            folderPath = _folderPath;
		}

        public void ChangePath (string newPath)
        {
			folderPath = newPath;
		}

        public void ChangeRun(bool run)
        {
            isRun = run;
        }

        private byte[] ReadBinaryFile(string filePath)
        {
            // 파일을 바이너리 형태로 읽기
            byte[] fileContent = File.ReadAllBytes(filePath);
            return fileContent;
        }
        
       public async Task ReadFile(Func<byte[], Task<bool>> sendMethods)
       {
            // 해당 폴더의 모든 .bin 파일을 읽어옴
            string[] fileEntries = Directory.GetFiles(folderPath, "*.bin");
            while (isRun)
            {
                foreach (string fileName in fileEntries)
                {
                    if (!isRun) break; // isRun이 false일 경우 루프 종료

                    // 각 파일을 처리 (예: 파일 내용 읽기)
                    var packets = ReadBinaryFile(fileName);
                    while (isRun)
                    {
                        try
                        {
                            bool r = await sendMethods(packets);
                            DataSendEvent?.Invoke(" - " + "데이터 송신.");
                            if (!r) break;
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            //중지 후 재시작할 때 발생하는 에러. 무시처리
                            break;
                        }
                        catch (Exception e)
                        {
                            DataSendEvent?.Invoke(" - err : " + e.Message);
                            break;
                        }
                    }
                    //Console.WriteLine(fileName + "읽기 완료");
                }
            }
       }
	}
}