using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

// Don't forget to add this
using vJoyInterfaceWrap;

namespace vJoyFeeder
{
    class IMUVector
    {
        public float x;
        public float y;
        public float z;
    }

    class IMUData
    {
        public IMUVector accel;
    }

    class Program
    {
        const string IP_ADDRESS = ""; // YOUR PCs IP ADDRESS
        const int PORT = 13000; // PORT (I USED 13000)

        // Declaring one joystick (Device id 1) and a position structure. 
        static public vJoy joystick;
        static public vJoy.JoystickState iReport;
        static public uint id = 1;

        static void Main(string[] args)
        {
            // Create one joystick object and a position structure.
            joystick = new vJoy();
            iReport = new vJoy.JoystickState();

            // Device ID can only be in the range 1-16
            if (args.Length > 0 && !String.IsNullOrEmpty(args[0]))
                id = Convert.ToUInt32(args[0]);
            if (id <= 0 || id > 16)
            {
                Console.WriteLine("Illegal device ID {0}\nExit!", id);
                return;
            }

            // Get the driver attributes (Vendor ID, Product ID, Version Number)
            if (!joystick.vJoyEnabled())
            {
                Console.WriteLine("vJoy driver not enabled: Failed Getting vJoy attributes.");
                return;
            }
            else
                Console.WriteLine("Vendor: {0}\nProduct :{1}\nVersion Number:{2}", joystick.GetvJoyManufacturerString(), joystick.GetvJoyProductString(), joystick.GetvJoySerialNumberString());

            // Get the state of the requested device
            VjdStat status = joystick.GetVJDStatus(id);
            switch (status)
            {
                case VjdStat.VJD_STAT_OWN:
                    Console.WriteLine("vJoy Device {0} is already owned by this feeder", id);
                    break;
                case VjdStat.VJD_STAT_FREE:
                    Console.WriteLine("vJoy Device {0} is free", id);
                    break;
                case VjdStat.VJD_STAT_BUSY:
                    Console.WriteLine("vJoy Device {0} is already owned by another feeder\nCannot continue", id);
                    return;
                case VjdStat.VJD_STAT_MISS:
                    Console.WriteLine("vJoy Device {0} is not installed or disabled\nCannot continue", id);
                    return;
                default:
                    Console.WriteLine("vJoy Device {0} general error\nCannot continue", id);
                    return;
            };

            // Check which axes are supported
            bool AxisX = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_X);
            bool AxisY = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_Y);
            bool AxisZ = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_Z);
            bool AxisRX = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_RX);
            bool AxisRZ = joystick.GetVJDAxisExist(id, HID_USAGES.HID_USAGE_RZ);
            // Get the number of buttons and POV Hat switchessupported by this vJoy device
            int nButtons = joystick.GetVJDButtonNumber(id);
            int ContPovNumber = joystick.GetVJDContPovNumber(id);
            int DiscPovNumber = joystick.GetVJDDiscPovNumber(id);

            // Print results
            Console.WriteLine("\nvJoy Device {0} capabilities:", id);
            Console.WriteLine("Numner of buttons\t\t{0}", nButtons);
            Console.WriteLine("Numner of Continuous POVs\t{0}", ContPovNumber);
            Console.WriteLine("Numner of Descrete POVs\t\t{0}", DiscPovNumber);
            Console.WriteLine("Axis X\t\t{0}", AxisX ? "Yes" : "No");
            Console.WriteLine("Axis Y\t\t{0}", AxisX ? "Yes" : "No");
            Console.WriteLine("Axis Z\t\t{0}", AxisX ? "Yes" : "No");
            Console.WriteLine("Axis Rx\t\t{0}", AxisRX ? "Yes" : "No");
            Console.WriteLine("Axis Rz\t\t{0}", AxisRZ ? "Yes" : "No");

            // Test if DLL matches the driver
            UInt32 DllVer = 0, DrvVer = 0;
            bool match = joystick.DriverMatch(ref DllVer, ref DrvVer);
            if (match)
                Console.WriteLine("Version of Driver Matches DLL Version ({0:X})", DllVer);
            else
                Console.WriteLine("Version of Driver ({0:X}) does NOT match DLL Version ({1:X})", DrvVer, DllVer);


            // Acquire the target
            if ((status == VjdStat.VJD_STAT_OWN) || ((status == VjdStat.VJD_STAT_FREE) && (!joystick.AcquireVJD(id))))
            {
                Console.WriteLine("Failed to acquire vJoy device number {0}.", id);
                return;
            }
            else
                Console.WriteLine("Acquired: vJoy device number {0}.", id);

            long maxval = 0;

            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_X, ref maxval);

            // Reset this device to default values
            joystick.ResetVJD(id);

            TcpListener server = new TcpListener(IPAddress.Parse(IP_ADDRESS), PORT);

            server.Start();

            // Feed the device in endless loop
            while (true)
            {
                TcpClient client = server.AcceptTcpClient();  // If a connection exists, the server will accept it

                NetworkStream ns = client.GetStream(); // Networkstream is used to send/receive messages

                while (client.Connected)  // While the client is connected, we look for incoming messages
                {
                    try
                    {
                        byte[] msg = new byte[1024];     // The messages arrive as byte array
                        ns.Read(msg, 0, msg.Length);   // The same networkstream reads the message sent by the client

                        string dataString = Encoding.Default.GetString(msg);

                        IMUData data = JsonConvert.DeserializeObject<IMUData>(dataString);

                        IMUVector accel = data.accel;

                        int X = (int)((accel.y + 1) * maxval / 2.0f);
                        int Y = (int)((accel.x + 1) * maxval / 2.0f);

                        Console.WriteLine(string.Format("{0}, {1}", X, Y));

                        joystick.SetAxis(X, id, HID_USAGES.HID_USAGE_X);
                        joystick.SetAxis(Y, id, HID_USAGES.HID_USAGE_Y);

                        byte[] response = new byte[64];
                        response = Encoding.Default.GetBytes("DATA ACCEPTED");
                        ns.Write(response, 0, response.Length);
                    }
                    catch
                    {
                        return;
                    }
                }
            }
        } // Main
    } // class Program
} // namespace FeederDemoCS
