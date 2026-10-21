using System;
using System.Device.Gpio;
using System.Device.Spi;
using System.Threading;

namespace MogwaiNanoTft
{
    /// <summary>
    /// Generic command-based SPI TFT driver (ST7789, ST7735, ILI9341 and similar).
    /// Everything chip-specific comes from the TftConfig (init table, rotations, offsets, commands).
    /// SPI pin functions must be set before Initialize (see TftManager.ConfigureSpiPins).
    /// </summary>
    public class SpiTftDriver : IDisplayDriver
    {
        private const int TransferChunk = 2048;

        private readonly TftConfig _cfg;
        private readonly GpioController _gpio;
        private SpiDevice _spi;
        private GpioPin _dc;
        private GpioPin _rst;
        private GpioPin _bl;
        private bool _dcHigh;

        public SpiTftDriver(TftConfig config)
        {
            _cfg = config;
            _gpio = new GpioController();
        }

        public int NativeWidth { get { return _cfg.Width; } }
        public int NativeHeight { get { return _cfg.Height; } }
        public int MaxTransferSize { get { return TransferChunk; } }

        public void Initialize()
        {
            if (_cfg.Backlight >= 0)
            {
                _bl = _gpio.OpenPin(_cfg.Backlight, PinMode.Output);
                _bl.Write(PinValue.Low);
            }

            _dc = _gpio.OpenPin(_cfg.Dc, PinMode.Output);
            _dc.Write(PinValue.Low);
            _dcHigh = false;

            var settings = new SpiConnectionSettings(_cfg.SpiBus, _cfg.Cs)
            {
                ClockFrequency = _cfg.ClockHz,
                Mode = SpiMode.Mode0,
                DataFlow = DataFlow.MsbFirst
            };
            _spi = SpiDevice.Create(settings);

            if (_cfg.Rst >= 0)
            {
                _rst = _gpio.OpenPin(_cfg.Rst, PinMode.Output);
                _rst.Write(PinValue.High);
                Thread.Sleep(10);
                _rst.Write(PinValue.Low);
                Thread.Sleep(10);
                _rst.Write(PinValue.High);
                Thread.Sleep(120);
            }

            RunInitTable(_cfg.InitTable);
        }

        private void RunInitTable(byte[] table)
        {
            int i = 0;
            while (i < table.Length)
            {
                if (i + 1 >= table.Length)
                    throw new ArgumentException("malformed init table");

                byte cmd = table[i];
                int n = table[i + 1];

                if (i + 2 + n >= table.Length)
                    throw new ArgumentException("malformed init table");

                WriteCommand(cmd);

                if (n > 0)
                {
                    byte[] args = new byte[n];
                    Array.Copy(table, i + 2, args, 0, n);
                    WriteData(args);
                }

                int delay = table[i + 2 + n] * 10;
                if (delay > 0)
                    Thread.Sleep(delay);

                i += 3 + n;
            }
        }

        public void SetRotation(int rotation)
        {
            int madctl = _cfg.Rotations[rotation & 3];
            if (_cfg.Bgr)
                madctl |= 0x08;

            WriteCommand((byte)_cfg.CmdMadctl);
            WriteData(new byte[] { (byte)madctl });
        }

        public void SetWindow(int x0, int y0, int x1, int y1)
        {
            x0 += _cfg.XOffset; x1 += _cfg.XOffset;
            y0 += _cfg.YOffset; y1 += _cfg.YOffset;

            WriteCommand((byte)_cfg.CmdCaset);
            WriteData(new byte[] { (byte)(x0 >> 8), (byte)x0, (byte)(x1 >> 8), (byte)x1 });
            WriteCommand((byte)_cfg.CmdRaset);
            WriteData(new byte[] { (byte)(y0 >> 8), (byte)y0, (byte)(y1 >> 8), (byte)y1 });
            WriteCommand((byte)_cfg.CmdRamwr);
        }

        public void WritePixels(byte[] buffer, int count)
        {
            SetDc(true);

            if (count == buffer.Length)
            {
                _spi.Write(buffer);
            }
            else
            {
                byte[] part = new byte[count];
                Array.Copy(buffer, part, count);
                _spi.Write(part);
            }
        }

        public void SetBacklight(bool on)
        {
            if (_bl != null)
                _bl.Write(on ? PinValue.High : PinValue.Low);
        }

        // DC is written only when its state changes (saves ~30 ms per full refresh).
        private void SetDc(bool high)
        {
            if (_dcHigh == high) return;

            _dc.Write(high ? PinValue.High : PinValue.Low);
            _dcHigh = high;
        }

        private void WriteCommand(byte cmd)
        {
            SetDc(false);
            _spi.WriteByte(cmd);
        }

        private void WriteData(byte[] data)
        {
            SetDc(true);
            _spi.Write(data);
        }

        public void Dispose()
        {
            if (_spi != null)
            {
                _spi.Dispose();
                _spi = null;
            }
            if (_dc != null)
            {
                _gpio.ClosePin(_cfg.Dc);
                _dc = null;
            }
            if (_rst != null)
            {
                _gpio.ClosePin(_cfg.Rst);
                _rst = null;
            }
            if (_bl != null)
            {
                _gpio.ClosePin(_cfg.Backlight);
                _bl = null;
            }
        }
    }
}