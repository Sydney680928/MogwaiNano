namespace MogwaiNanoTft
{
    /// <summary>Configuration of a SPI TFT display, filled from the record given to tft.init.</summary>
    public class TftConfig
    {
        public int SpiBus = 1;
        public int Sclk = -1;
        public int Mosi = -1;
        public int Miso = -1;
        public int Dc = -1;
        public int Cs = -1;
        public int Rst = -1;
        public int Backlight = -1;
        public int ClockHz = 40000000;

        public int Width;
        public int Height;
        public int XOffset;
        public int YOffset;

        public bool Invert;
        public bool Bgr;

        /// <summary>Memory access control value (MADCTL) for rotations 0..3.</summary>
        public int[] Rotations;

        /// <summary>Init table: repeated entries of [command][argCount][args...][delay in 10 ms units].</summary>
        public byte[] InitTable;

        public int CmdCaset = 0x2A;
        public int CmdRaset = 0x2B;
        public int CmdRamwr = 0x2C;
        public int CmdMadctl = 0x36;
    }

    /// <summary>Built-in controller profiles.</summary>
    public static class TftProfiles
    {
        public static byte[] St7789Init(bool invert)
        {
            return new byte[]
            {
                0x01, 0, 15,                                   // SWRESET, wait 150 ms
                0x11, 0, 12,                                   // SLPOUT, wait 120 ms
                0x3A, 1, 0x55, 0,                              // COLMOD: RGB565
                invert ? (byte)0x21 : (byte)0x20, 0, 0,        // INVON / INVOFF
                0x13, 0, 1,                                    // NORON, wait 10 ms
                0x29, 0, 10                                    // DISPON, wait 100 ms
            };
        }

        public static int[] St7789Rotations()
        {
            return new int[] { 0x00, 0x60, 0xC0, 0xA0 };
        }
    }
}