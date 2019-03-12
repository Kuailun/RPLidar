using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace LIDAR
{
    class Program
    {
        struct Config
        {
            public string COM;              //Example: "COM6"
            public int Baudrate;            //Example: 115200
            public double ScanInterval;     //Example: 10       Unit: Seconds
            public double ScanPeriod;       //Example: 2        Unit: Seconds
            public string SaveDir;          //Example: C:/data/
            public double width;            //Example: 10000     Unit: Milimeters
            public double length;           //Example: 10000     Unit: Milimeters
            public int segmentation;        //Example: 16
        }
        struct Datastructure
        {
            public bool newRoundFlag;
            public double angle;
            public double distance;
            public int quality;
        }
        private static int NumDay;
        private static int Day = -1;
        private static int cnt = 0;
        private static SerialPort sp;
        private static Thread th;
        private static bool stopFlag = false;
        private static Datastructure ds=new Datastructure();
        private static byte[] g_buffer = new byte[0];
        private static Config mConfig = new Config();
        static void Main(string[] args)
        {
            bool ret = File.Exists("./config.json");
            if (!ret)
            {
                Console.WriteLine("Please run Init.py first!");
                Console.Read();
                System.Environment.Exit(0);
            }

            //Default value
            mConfig.COM = "COM6";
            mConfig.Baudrate = 115200;
            mConfig.ScanInterval = 2;
            mConfig.ScanPeriod = 1;
            mConfig.SaveDir = System.Environment.CurrentDirectory + "\\Database";
            mConfig.width = 20000;
            mConfig.length = 20000;
            mConfig.segmentation = 64;
            using (StreamReader r = new StreamReader("./config.json"))
            {
                string json = r.ReadToEnd();
                json.Replace("\\","");
                try
                {
                    mConfig = JsonConvert.DeserializeObject<Config>(json);
                }catch(Exception ee)
                {
                    Console.WriteLine("Please check the config");
                    Console.Read();
                    System.Environment.Exit(0);
                }
            }

            //Check the Config
            ret = CheckConfig(mConfig);
            if (!ret)
            {
                Console.Read();
                System.Environment.Exit(0);
            }

            NumDay = (int)(86400 / mConfig.ScanInterval);

            Console.WriteLine("The width is {0},\r\nThe length is {1}, \r\nEvery scan would last {2} seconds.\r\nEvery {3} seconds will scan one time.\r\nThe segmentation is {4}\r\n", mConfig.width, mConfig.length, mConfig.ScanPeriod, mConfig.ScanInterval, mConfig.segmentation);
                       
            //Check and create directory
            ret = InitialDir(mConfig.SaveDir);

            //Initiate the serial port
            ret = SerialInit(mConfig.COM,mConfig.Baudrate);
            if(!ret)
            {
                Console.Read();
                System.Environment.Exit(0);
            }

            //Initiate the thread
            double[] mScan = { mConfig.ScanInterval, mConfig.ScanPeriod };
            ret = InitialThread(mScan);

            Console.WriteLine("Start getting data");
            while(!stopFlag)
            {
                char char_temp=Console.ReadKey().KeyChar;
                if(char_temp=='1')
                {
                    stopFlag = true;
                }
                Console.WriteLine();
                Console.WriteLine("Begin exit sequence\r\nWait for {0} seconds",mConfig.ScanInterval);
            }
            //Wait for other thread exit
            Thread.Sleep((int)(mConfig.ScanInterval * 1000));

            SerialClose();
            Console.WriteLine("Exit the application");
            Console.Read();
        }

        /// <summary>
        /// Check and create folder to hold data
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        private static bool InitialDir(string dir)
        {
            if(Directory.Exists(dir))
            {
                return true;
            }
            else
            {
                Console.WriteLine("No directory detected. Create a new folder");
                Directory.CreateDirectory(dir);
                return true;
            }
        }

        private static bool InitialThread(double[] parameter)
        {
            th = new Thread(sendThread);
            th.Start(parameter);
            return true;
        }

        private static bool CheckConfig(Config p_config)
        {
            if(p_config.Baudrate!=115200)
            {
                Console.WriteLine("This Lidar requires the baudrate to be 115200");
                return false;
            }

            if(p_config.ScanInterval<=p_config.ScanPeriod)
            {
                Console.WriteLine("The ScanInterval should larger than ScanPeriod");
                return false;
            }

            if(p_config.width<2000||p_config.length<2000||p_config.width>24000||p_config.width>24000)
            {
                Console.WriteLine("The length and width should be in [2000,24000]");
                return false;
            }

            if (p_config.segmentation<8||p_config.segmentation>64)
            {
                Console.WriteLine("The segmentation should be in [8,24]");
                return false;
            }

            return true;
        }
        private static void sendThread(object p)
        {
            while(!stopFlag)
            {
                byte[] s = new byte[2];
                s[0] = 0xA5;
                s[1] = 0x20;
                SerialSend(s);

                double mInterval = ((double[])p)[0]*1000;
                double mPeriod = ((double[])p)[1]*1000;

                Thread.Sleep((int)mPeriod);

                s[0] = 0xA5;
                s[1] = 0x25;
                SerialSend(s);

                Parse_Write();
                Thread.Sleep((int)(mInterval-mPeriod));
            }
        }

        /// <summary>
        /// Initiate the serial port
        /// </summary>
        /// <param name="COM"></param>
        /// <param name="Baudrate"></param>
        /// <returns></returns>
        private static bool SerialInit(string COM,int Baudrate)
        {
            sp = new SerialPort();
            try
            {
                sp.PortName = COM;
                sp.BaudRate = Baudrate;
                sp.Open();
                if (sp.IsOpen)
                {
                    sp.DataReceived += new SerialDataReceivedEventHandler(DataReceived);
                    return true;
                }
                else
                {
                    sp = null;
                    return false;
                }
            }catch(Exception e)
            {
                Console.WriteLine("Error to open Serial " + COM);
                return false;
            }
        }

        /// <summary>
        /// Close and free the serial port
        /// </summary>
        /// <returns></returns>
        private static bool SerialClose()
        {
            if(sp==null)
            {
                return true;
            }
            if(!sp.IsOpen)
            {
                return true;
            }
            sp.Close();
            sp = null;
            return true;
        }

        private static bool SerialSend(byte[] bytearray)
        {
            if(sp==null||!sp.IsOpen)
            {
                Console.WriteLine("Serialport not reachable");
                return false;
            }
            sp.Write(bytearray,0,bytearray.Length);

            return true;
        }

        private static void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if(sp.IsOpen)
            {
                byte[] buffer = new byte[sp.BytesToRead];
                sp.Read(buffer, 0, buffer.Length);
                byte[] newbuffer = new byte[g_buffer.Length + buffer.Length];
                Buffer.BlockCopy(g_buffer, 0, newbuffer, 0, g_buffer.Length);
                Buffer.BlockCopy(buffer, 0, newbuffer, g_buffer.Length, buffer.Length);
                g_buffer = newbuffer;
                newbuffer = null;
            }
        }
        private static Datastructure decodeData(byte[] p_array)
        {
            ds.angle = 0;
            ds.newRoundFlag = false;
            ds.angle = 0;
            ds.distance = 0;
            ds.quality = 0;

            if (p_array.Length != 5)
                return ds;

            bool invertFlag = false;
            int b00 = p_array[0] & 0x01;
            int b01 = (p_array[0] & 0x02) / 2;
            int b10 = p_array[1] & 0x01;
            invertFlag = (b00 == 1 - b01) && (b10 == 1);

            if (!invertFlag)
                return ds;

            ds.quality = (p_array[0] & 0xFC) / 4;
            ds.angle = (p_array[2] * Math.Pow(2, 7) + (p_array[1] & 0xFE) / 2);
            ds.distance = (p_array[4] * Math.Pow(2, 8) + p_array[3]);
            ds.newRoundFlag = b00 == 1;

            ds.angle = ds.angle / 64;
            ds.distance = ds.distance / 4;

            //Console.WriteLine("Angle:{0},Distance:{1}", ds.angle, ds.distance);

            return ds;
        }
        private static void Parse_Write()
        {
            List<Datastructure> mD = new List<Datastructure>();
            byte[] b= g_buffer;
            int len = b.Length;
            while(len>=10)
            {
                b = g_buffer;
                len = b.Length;
                if (b[0]==0xA5&&
                    b[1]==0x5A&&
                    b[2]==0x05&& len >= 7)
                {
                    byte[] newbuffer = new byte[len - 7];
                    Buffer.BlockCopy(b, 7, newbuffer, 0, len - 7);
                    g_buffer = newbuffer;
                    newbuffer = null;
                    continue;
                }
                else
                {
                    bool invertFlag = false;
                    int b00 = b[0] & 0x01;
                    int b01 = (b[0] & 0x02) / 2;
                    int b10 = b[1] & 0x01;
                    invertFlag = (b00 == 1 - b01) && (b10 == 1);
                    if(invertFlag)
                    {
                        byte[] newbuffer1 = new byte[len - 5];
                        byte[] newbuffer2 = new byte[5];
                        Buffer.BlockCopy(b, 5, newbuffer1, 0, len - 5);
                        Buffer.BlockCopy(b, 0, newbuffer2, 0, 5);
                        g_buffer = newbuffer1;
                        newbuffer1 = null;

                        mD.Add(decodeData(newbuffer2));
                        continue;
                    }
                }
                byte[] newbuffer3 = new byte[len - 1];
                Buffer.BlockCopy(b, 1, newbuffer3, 0, len - 1);
                g_buffer = newbuffer3;
                newbuffer3 = null;
            }
            int[,] mGrid = new int[mConfig.segmentation,mConfig.segmentation];
            for(int i=0;i<mD.Count;i++)
            {
                double angle = Math.PI * (90 - mD[i].angle) / 180;
                double x = Math.Cos(angle) * mD[i].distance+mConfig.width/2;
                double y = Math.Sin(angle) * mD[i].distance+mConfig.length/2;

                x = Cutoff(x, mConfig.width);
                y = Cutoff(y, mConfig.length);

                mGrid[(int)Math.Floor(x / mConfig.width * mConfig.segmentation),(int)Math.Floor(y / mConfig.width * mConfig.segmentation)] +=1;
            }
            if(mConfig.width%8!=0||mConfig.length%8!=0)
            {
                Console.WriteLine("Please adjust the segmentation of grid");
                return;
            }
            string stringtowrite=CompileBytes(mGrid);
            cnt = cnt + 1;
            if(cnt==NumDay)
            {
                cnt = 0;
                Day = Day + 1;
            }
            string filename = "Day"+Day.ToString();
            WritetoFile(stringtowrite,filename);
        }

        private static String GetTimestamp(DateTime value)
        {
            return value.ToString("yyyyMMddHHmmssffff");
        }
        private static void WritetoFile(string str,string name)
        {
            string timeStamp = GetTimestamp(DateTime.Now);
            str = timeStamp + " " + str;
            InitialDir(mConfig.SaveDir);
            /*if (!File.Exists(mConfig.SaveDir + "\\mData.txt"))
            {
                File.Create(mConfig.SaveDir + "\\mData.txt");
                FileStream fss = new FileStream(mConfig.SaveDir + "\\mData.txt", FileMode.CreateNew);
                fss.Close();
            }*/
            byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(str);
            FileStream fs = new FileStream(mConfig.SaveDir + "\\"+name+".txt", FileMode.Append);
            fs.Write(byteArray, 0, byteArray.Length);
            fs.Close();
        }

        /// <summary>
        /// Compile location data to bytes
        /// </summary>
        /// <param name="p_data"></param>
        /// <returns></returns>
        private static string CompileBytes(int[,] p_data)
        {
            /*string s = "";
            int value = 0;
            int nD1 = mConfig.segmentation / 8;
            int nD2 = mConfig.segmentation;
            for (int i=0;i<nD2;i++)
            {
                for(int j=0;j< nD1; j++)
                {
                    value = p_data[i, j * 8 + 0] * 128
                                                    + p_data[i, j * 8 + 1] * 64
                                                    + p_data[i, j * 8 + 2] * 32
                                                    + p_data[i, j * 8 + 3] * 16
                                                    + p_data[i, j * 8 + 4] * 8
                                                    + p_data[i, j * 8 + 5] * 4
                                                    + p_data[i, j * 8 + 6] * 2
                                                    + p_data[i, j * 8 + 7] * 1;
                    s = s + value.ToString() + " ";
                }
            }
            s = s + "\r\n";
            return s;*/

            string s = "";
            int value = 0;
            int nD1 = mConfig.segmentation;
            int nD2 = mConfig.segmentation;
            for (int i = 0; i < nD2; i++)
            {
                for (int j = 0; j < nD1; j++)
                {
                    value = p_data[i, j];
                    s = s + value.ToString() + " ";
                }
            }
            s = s + "\r\n";
            return s;
        }

        /// <summary>
        /// Restrain the scope of data to [0,p_max]
        /// </summary>
        /// <param name="p_data"></param>
        /// <param name="p_max"></param>
        /// <returns></returns>
        private static double Cutoff(double p_data,double p_max)
        {
            if(p_data<0)
            {
                return 0;
            }
            if(p_data>=p_max)
            {
                return p_max-1;
            }
            return p_data;
        }
    }
}
