using System.Runtime.InteropServices;

namespace MonitorBrightnessAutoAdjust.Sensors
{
    public class TSL2591
    {
        [DllImport("ch341dll.DLL", CallingConvention = CallingConvention.Winapi)]
        public extern static long CH341OpenDevice(int i);

        [DllImport("ch341dll.DLL", CallingConvention = CallingConvention.Winapi)]
        public extern static void CH341CloseDevice(int iIndex);

        // CH341WriteI2C( ULONG iIndex, UCHAR iDevice, UCHAR iAddr, UCHAR iByte );
        [DllImport("ch341dll.DLL", CallingConvention = CallingConvention.Winapi)]
        public extern static bool CH341WriteI2C(int iIndex, byte i1, byte i2, byte i3);
        // Calling convention: Cdecl geht nicht, PInvokeStackImbalance- Fehler
        //.FastCall :Ungültige nicht verwaltete Aufrufkonvention ("stdcall", "cdecl" oder "thiscall" ist erforderlich). 
        // .stdCall : OK
        // .ThisCall :  PInvokeStackImbalance- Fehler
        // .WinApi : OK

        // CH341StreamI2C(ChipIndex.Value,wlen,@outbytes,rlen,@inbytes); 
        [DllImport("ch341dll.DLL", CallingConvention = CallingConvention.StdCall)]
        public extern static bool CH341StreamI2C(int iIndex, int wlen, ref byte WBuf, int rlen, ref byte RBuf);


        // CH341SetStream() konfiguriert I2C und SPI
        // Bit 1-0: I2C speed 00= low speed /20KHz
        // 01= standard /100KHz
        // 10= fast /400KHz
        // 11= high speed /750KHz           plus  0x60  , das ist wichtig. Also :
        // BOOL	WINAPI	CH341SetStream( ULONG iIndex, ULONG	iMode );*/

        [DllImport("ch341dll.DLL", CallingConvention = CallingConvention.Winapi)]
        public extern static long CH341SetStream(int iIndex, int iMode);

        byte[] WriteBuf = new byte[10];

        // Address Constant
        private const int TSL2591_ADDR = 0x29;
        // Commands
        private const int TSL2591_CMD = 0xA0;

        // Registers
        private const int TSL2591_REG_ENABLE = 0x00;
        private const int TSL2591_REG_CONTROL = 0x01;
        private const int TSL2591_REG_ID = 0x12;
        private const int TSL2591_REG_DATA_0 = 0x14;
        private const int TSL2591_REG_DATA_1 = 0x16;

        //private const int TSL2591_STATUS_REG = 0x13;

        /*
         LOW gain: use in bright light to avoid sensor saturation
         MED: use in low light to boost sensitivity 
         HIGH: use in very low light condition
         */
        public const int GAIN_LOW = 0x00;
        public const int GAIN_MED = 0x10;
        public const int GAIN_HIGH = 0x20;
        public const int GAIN_MAX = 0x30;
        /*
         100ms: fast reading but low resolution
         600ms: slow reading but best accuracy
         */
        public const int INT_TIME_100MS = 0x00;
        public const int INT_TIME_200MS = 0x01;
        public const int INT_TIME_300MS = 0x02;
        public const int INT_TIME_400MS = 0x03;
        public const int INT_TIME_500MS = 0x04;
        public const int INT_TIME_600MS = 0x05;
        // Constants for LUX calculation
        private const double LUX_DF = 408.0;
        private const double LUX_COEFB = 1.64;  // CH0 coefficient
        private const double LUX_COEFC = 0.59;  // CH1 coefficient A
        private const double LUX_COEFD = 0.86;  //CH2 coefficient B

        //default values
        private const int gainDefault = GAIN_MED;
        private const int integrationTimeDefault = INT_TIME_200MS;

        private uint intTimeSet { get; set; } = integrationTimeDefault;
        private uint gainSet { get; set; } = gainDefault;

        // I2C Device
        private int I2C_ADDRESS = TSL2591_ADDR;
        public TSL2591()
        {
            Initialise();
        }

        public static bool IsInitialised { get; private set; } = false;

        private void Initialise()
        {
            if (!IsInitialised)
            {
                EnsureInitializedAsync().Wait();
            }
        }
        private async Task EnsureInitializedAsync()
        {
            if (IsInitialised)
            {
                return;
            }

            Initialize();
        }

        public void Initialize()
        {
            try
            {
                long open = CH341OpenDevice(0);

                if (open != 4294967295) // if chip not connected open is Hex 00000000FFFFFFFF
                {
                    Console.WriteLine("Chip is open with adress " + open.ToString("X16"));
                }
                else
                {
                    Console.WriteLine("No Chip could be opened");
                }

                PowerUp();
                SetGain();
                IsInitialised = true;
            }
            catch (Exception ex)
            {
                throw new Exception("I2C Initialization Failed", ex);
            }
        }

        // Sensor Power up
        private void PowerUp()
        {
            write8(TSL2591_REG_ENABLE, 0x03);
        }

        // Sensor Power down
        private void PowerDown()
        {
            write8(TSL2591_REG_ENABLE, 0x00);
        }

        // Retrieve sensor ID
        public byte GetId()
        {
            return I2CRead8(TSL2591_REG_ID);
        }

        public void SetGain(byte gain = gainDefault, byte int_time = integrationTimeDefault)
        {
            intTimeSet = int_time;
            gainSet = gain;
            write8(TSL2591_REG_CONTROL, (byte)(gain + int_time));
        }

        // Calculate Lux
        public double GetLux()
        {
            uint gain = gainSet;
            uint itime = intTimeSet;

            uint CH0 = I2CRead16(TSL2591_REG_DATA_0);
            uint CH1 = I2CRead16(TSL2591_REG_DATA_1);

            double d0, d1;

            // Determine if either sensor saturated (0xFFFF)
            if (CH0 == 0xFFFF || CH1 == 0xFFFF)
            {
                var lux = 0.0;
                return lux;
            }

            // Convert from unsigned integer to floating point
            d0 = CH0; d1 = CH1;

            int atime = (int)(itime + 1) * 100;
            double again;
            switch (gain)
            {
                case 0x00: again = 1; break;
                case 0x10: again = 25; break;
                case 0x20: again = 428; break;
                case 0x30: again = 9876; break;
                default: again = 1; break;
            }
            double cpl = atime * again / LUX_DF;
            double lux1 = (d0 - LUX_COEFB * d1) / cpl;
            double lux2 = (LUX_COEFC * d0 - LUX_COEFD * d1) / cpl;
            return Math.Round(Math.Max(lux1, lux2), 4);
        }

        // WriteBuf strucure:
        // [0]: device Address
        // [1]: register Address
        // [2]: data


        // Write byte
        private void write8(byte registerAddress, byte cmd)
        {
            // 7-bit
            WriteBuf[0] = Convert.ToByte(I2C_ADDRESS << 1);

            byte[] Command = new byte[] { (byte)(registerAddress | TSL2591_CMD), cmd };

            // I2C.Write(Command);
            byte[] buf = new byte[1];

            Command.CopyTo(WriteBuf, 1);
            var bufLength = registerAddress + Command.Length;

            CH341StreamI2C(0, bufLength + 1, ref WriteBuf[0], bufLength, ref buf[0]);
        }

        // Read byte
        private byte I2CRead8(byte registerAddress)
        {
            WriteBuf[0] = Convert.ToByte(I2C_ADDRESS << 1);

            byte[] aaddr = new byte[] { (byte)(registerAddress | TSL2591_CMD) };
            aaddr.CopyTo(WriteBuf, 1);

            // I2C.WriteRead(aaddr, data);
            byte[] data = new byte[1];

            CH341StreamI2C(0, 2, ref WriteBuf[0], data.Length, ref data[0]);

            return data[0];
        }

        // Read integer
        private ushort I2CRead16(byte registerAddress)
        {
            WriteBuf[0] = Convert.ToByte(I2C_ADDRESS << 1);

            byte[] aaddr = new byte[] { (byte)(registerAddress | TSL2591_CMD) };
            aaddr.CopyTo(WriteBuf, 1);

            // I2C.WriteRead(aaddr, data);
            byte[] data = new byte[2];

            CH341StreamI2C(0, 2, ref WriteBuf[0], data.Length, ref data[0]);

            return (ushort)(data[1] << 8 | data[0]);
        }
    }
}
