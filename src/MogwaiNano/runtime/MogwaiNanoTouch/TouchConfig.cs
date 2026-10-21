namespace MogwaiNanoTouch
{
    /// <summary>
    /// Describes how to read a register-based I2C touch controller:
    /// one block read, a finger count, and the X/Y position of each finger inside the block.
    /// </summary>
    public class TouchProfile
    {
        public int DefaultAddress;
        public int ReadReg;          // register to read from (8 bit)
        public int ReadLen;          // number of bytes to read
        public int CountIndex;       // index of the finger count byte
        public int CountMask = 0x0F;
        public int PointBase;        // index of the first finger data
        public int PointStride;      // distance between two fingers data
        public int MaxPoints = 1;
        public int XHi, XLo, YHi, YLo;   // offsets inside one finger data
        public int HiMask = 0x0F;        // mask applied to the high bytes

        // Optional finger identifier, used to follow a finger when others come and go.
        // IdByte = -1: no identifier, the position in the list is used instead.
        public int IdByte = -1;      // offset inside one finger data
        public int IdShift = 0;
        public int IdMask = 0x0F;
    }

    public class TouchConfig
    {
        public string Controller;
        public int I2cBus = 1;
        public int Sda = -1;
        public int Scl = -1;
        public int Address = -1;     // -1: default / auto-detect
        public int Width;            // native width of the touch panel
        public int Height;           // native height of the touch panel
        public int PollMs = 20;
        public bool SwapXY;
        public bool FlipX;
        public bool FlipY;
        public bool Debug;           // writes events to the debug output
        public TouchProfile Custom;  // used when no controller name is given
    }

    public static class TouchProfiles
    {
        public static TouchProfile Cst816()
        {
            var p = new TouchProfile();
            p.DefaultAddress = 0x15;
            p.ReadReg = 0x01;
            p.ReadLen = 6;
            p.CountIndex = 1;
            p.PointBase = 2;
            p.PointStride = 0;
            p.MaxPoints = 1;
            p.XHi = 0; p.XLo = 1; p.YHi = 2; p.YLo = 3;
            return p;
        }

        // Not verified on real hardware.
        public static TouchProfile Ft6x36()
        {
            var p = new TouchProfile();
            p.DefaultAddress = 0x38;
            p.ReadReg = 0x02;
            p.ReadLen = 31;
            p.CountIndex = 0;
            p.PointBase = 1;
            p.PointStride = 6;
            p.MaxPoints = 5;
            p.XHi = 0; p.XLo = 1; p.YHi = 2; p.YLo = 3;
            p.IdByte = 2;        // touch id is in the upper 4 bits of the Y high byte
            p.IdShift = 4;
            return p;
        }
    }
}