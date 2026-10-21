using System;
using System.Device.I2c;

namespace MogwaiNanoTouch
{
    public class TouchState
    {
        public const int MaxPoints = 5;

        public int Count;
        public int[] X = new int[MaxPoints];
        public int[] Y = new int[MaxPoints];
        public int[] Id = new int[MaxPoints];    // identifier of each finger, stable while it stays down
    }

    public abstract class TouchChip : IDisposable
    {
        protected I2cDevice Dev;

        protected static I2cDevice CreateDevice(int bus, int address)
        {
            return I2cDevice.Create(new I2cConnectionSettings(bus, address, I2cBusSpeed.StandardMode));
        }

        /// <summary>Opens the chip. address = -1 means default / auto-detect. Throws if the chip is not found.</summary>
        public abstract void Open(int bus, int address);

        /// <summary>Reads the chip. Returns true if the state was updated, false if there is no new data.</summary>
        public abstract bool Read(TouchState state);

        public virtual void Dispose()
        {
            if (Dev != null)
            {
                Dev.Dispose();
                Dev = null;
            }
        }
    }

    /// <summary>Generic driver for controllers that expose their fingers in one register block (CST816, FT6x36, ...).</summary>
    public class RegisterTouchChip : TouchChip
    {
        private readonly TouchProfile _p;
        private readonly byte[] _reg;
        private readonly byte[] _buf;
        private readonly int _maxRel;

        public RegisterTouchChip(TouchProfile profile)
        {
            _p = profile;

            if (_p.ReadLen <= 0 || _p.CountIndex < 0 || _p.CountIndex >= _p.ReadLen)
                throw new ArgumentException("invalid read layout");

            _maxRel = _p.XHi;
            if (_p.XLo > _maxRel) _maxRel = _p.XLo;
            if (_p.YHi > _maxRel) _maxRel = _p.YHi;
            if (_p.YLo > _maxRel) _maxRel = _p.YLo;
            if (_p.IdByte > _maxRel) _maxRel = _p.IdByte;

            _reg = new byte[] { (byte)_p.ReadReg };
            _buf = new byte[_p.ReadLen];
        }

        public override void Open(int bus, int address)
        {
            if (address < 0)
                address = _p.DefaultAddress;

            Dev = CreateDevice(bus, address);

            var r = Dev.WriteRead(_reg, _buf);

            if (r.Status != I2cTransferStatus.FullTransfer)
                throw new InvalidOperationException("no answer from the touch controller at address " + address);
        }

        public override bool Read(TouchState st)
        {
            var r = Dev.WriteRead(_reg, _buf);

            if (r.Status != I2cTransferStatus.FullTransfer)
            {
                // Some chips do not answer when idle: no answer means no finger.
                st.Count = 0;
                return true;
            }

            int n = _buf[_p.CountIndex] & _p.CountMask;
            if (n > _p.MaxPoints) n = _p.MaxPoints;
            if (n > TouchState.MaxPoints) n = TouchState.MaxPoints;

            int count = 0;

            for (int i = 0; i < n; i++)
            {
                int b = _p.PointBase + i * _p.PointStride;

                if (b + _maxRel >= _buf.Length)
                    break;

                st.X[count] = ((_buf[b + _p.XHi] & _p.HiMask) << 8) | _buf[b + _p.XLo];
                st.Y[count] = ((_buf[b + _p.YHi] & _p.HiMask) << 8) | _buf[b + _p.YLo];

                if (_p.IdByte >= 0)
                    st.Id[count] = (_buf[b + _p.IdByte] >> _p.IdShift) & _p.IdMask;
                else
                    st.Id[count] = i;

                count++;
            }

            st.Count = count;
            return true;
        }
    }

    /// <summary>Goodix GT911 (16 bit register addresses, status register to acknowledge).</summary>
    public class Gt911Chip : TouchChip
    {
        private readonly byte[] _status = new byte[1];
        private readonly byte[] _data = new byte[TouchState.MaxPoints * 8];
        private readonly byte[] _regStatus = new byte[] { 0x81, 0x4E };
        private readonly byte[] _regPoints = new byte[] { 0x81, 0x4F };
        private readonly byte[] _ack = new byte[] { 0x81, 0x4E, 0x00 };

        public override void Open(int bus, int address)
        {
            int[] candidates;

            if (address >= 0)
                candidates = new int[] { address };
            else
                candidates = new int[] { 0x5D, 0x14 };

            byte[] id = new byte[4];

            for (int i = 0; i < candidates.Length; i++)
            {
                I2cDevice dev = CreateDevice(bus, candidates[i]);

                // Product ID register 0x8140: "911" in ASCII
                var r = dev.WriteRead(new byte[] { 0x81, 0x40 }, id);

                if (r.Status == I2cTransferStatus.FullTransfer && id[0] == (byte)'9' && id[1] == (byte)'1' && id[2] == (byte)'1')
                {
                    Dev = dev;
                    return;
                }

                dev.Dispose();
            }

            throw new InvalidOperationException("gt911 not found (tried 0x5D and 0x14)");
        }

        public override bool Read(TouchState st)
        {
            var r = Dev.WriteRead(_regStatus, _status);

            if (r.Status != I2cTransferStatus.FullTransfer)
                return false;

            // Bit 7: new data available. Bits 0-3: number of fingers.
            if ((_status[0] & 0x80) == 0)
                return false;

            int n = _status[0] & 0x0F;
            int count = 0;

            if (n > 0 && n <= TouchState.MaxPoints)
            {
                r = Dev.WriteRead(_regPoints, _data);

                if (r.Status == I2cTransferStatus.FullTransfer)
                {
                    for (int i = 0; i < n; i++)
                    {
                        int b = i * 8;   // [track id][x low][x high][y low][y high][size low][size high][reserved]
                        st.Id[i] = _data[b];
                        st.X[i] = _data[b + 1] | (_data[b + 2] << 8);
                        st.Y[i] = _data[b + 3] | (_data[b + 4] << 8);
                    }
                    count = n;
                }
            }

            st.Count = count;

            // Acknowledge, otherwise the chip does not publish new data.
            Dev.Write(_ack);

            return true;
        }
    }
}