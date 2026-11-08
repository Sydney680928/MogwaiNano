using MogwaiNano.Engine;
using MogwaiNano.Objects;
using System;
using System.Collections;
using System.Device.Spi;

namespace MogwaiNanoSpi
{
    public class SpiManager
    {
        private static Hashtable _spiDevices = new(3);

        public readonly static Error SpiDeviceAlreadyOpenedError = new("SPI.1", "spi device already opened error");
        public readonly static Error SpiDeviceOpenError = new("SPI.2", "spi device open error");
        public readonly static Error SpiUnknownDeviceNameError = new("SPI.3", "spi unknown device name error");
        public readonly static Error SpiWriteError = new("SPI.4", "spi write error");
        public readonly static Error SpiReadError = new("SPI.5", "spi read error");
        public readonly static Error SpiTransferError = new("SPI.6", "spi transfer error");

        public static void Cleanup(MogwaiNanoEngine engine)
        {
            if (_spiDevices.Contains(engine.EngineId))
            {
                var spiDevices = _spiDevices[engine.EngineId] as Hashtable;

                foreach (string key in spiDevices.Keys)
                {
                    var spiDevice = spiDevices[key] as SpiDevice;

                    try
                    {
                        spiDevice.Dispose();
                    }
                    catch
                    {

                    }
                }

                spiDevices.Clear();

                _spiDevices.Remove(engine.EngineId);
            }
        }

        public static EvalResult PrimitiveSpiOpen(MogwaiNanoEngine engine, string name)
        {
            // 'name' bus csPin frequency mode spi.open

            var s = engine.StackSign(5);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGNumber) || s[2] != typeof(MOGNumber) || s[3] != typeof(MOGNumber) || s[4] != typeof(MOGName))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var mode = engine.StackPop() as MOGNumber;
            var frequency = engine.StackPop() as MOGNumber;
            var csPin = engine.StackPop() as MOGNumber;
            var bus = engine.StackPop() as MOGNumber;
            var deviceName = engine.StackPop() as MOGName;

            int modeNumber = (int)mode.Value;

            if (modeNumber < 0 || modeNumber > 3)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "SPI mode number must be between 0 and 3");

            int csPinNumber = (int)csPin.Value;

            if (csPinNumber < 0)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "SPI csPin number must be a positive value");

            int busNumber = (int)bus.Value;

            if (busNumber < 1 || busNumber > 2)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "SPI bus number must be between 1 and 2");

            int frequencyNumber = (int)frequency.Value;
            var spiInfo = SpiDevice.GetBusInfo(busNumber);

            if (frequencyNumber < spiInfo.MinClockFrequency || frequencyNumber > spiInfo.MaxClockFrequency)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "SPI frequency must be between the minimum and maximum clock frequency of the bus");

            if (GetSpiDevice(engine.EngineId, deviceName.Value) != null)
                return EvalResult.Failure(engine, SpiDeviceAlreadyOpenedError, name, $"SPI device {deviceName} is already opened");

            try
            {
                var settings = new SpiConnectionSettings(busNumber, csPinNumber)
                {
                    ClockFrequency = frequencyNumber,
                    DataBitLength = 8,
                    Mode = (SpiMode)modeNumber
                };

                var spiDevice = SpiDevice.Create(settings); 
                AddSpiDevice(engine.EngineId, deviceName.Value, spiDevice);
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(engine, SpiDeviceOpenError, name, $"failed to open SPI device {deviceName}", ex.Message);
            }

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveSpiClose(MogwaiNanoEngine engine, string name)
        {
            // name spi.close   

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGName))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var deviceName = engine.StackPop() as MOGName;
            
            if (!RemoveSpiDevice(engine.EngineId, deviceName.Value))
               return EvalResult.Failure(engine, SpiUnknownDeviceNameError, name);

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveSpiWrite(MogwaiNanoEngine engine, string name)
        {
            // name data spi.write

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGData) && s[1] == typeof(MOGName))
            {
                var data = engine.StackPop() as MOGData;
                var deviceName = engine.StackPop() as MOGName;
                var spiDevice = GetSpiDevice(engine.EngineId, deviceName.Value);    

                if (spiDevice == null)
                    return EvalResult.Failure(engine, SpiUnknownDeviceNameError, name);

                try
                {
                    spiDevice.Write(data.ToSpanByte());
                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, SpiWriteError, name, $"failed to write to SPI device '{deviceName}'", ex.Message);
                }
            }
            else if (s[0] == typeof(MOGRef) && s[1] == typeof(MOGName))
            {
                var @ref = engine.StackPop() as MOGRef;
                var deviceName = engine.StackPop() as MOGName;

                var value = engine.VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name.ToString());

                engine.StackPush(deviceName);
                engine.StackPush(value);

                var r = PrimitiveSpiWrite(engine, name);

                if (r.IsError)
                    return r;

                engine.StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveSpiTransfer(MogwaiNanoEngine engine, string name)
        {
            // name data spi.transfert

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGData) && s[1] == typeof(MOGName))
            {
                var data = engine.StackPop() as MOGData;
                var deviceName = engine.StackPop() as MOGName;
                var spiDevice = GetSpiDevice(engine.EngineId, deviceName.Value);    

                if (spiDevice == null)
                    return EvalResult.Failure(engine, SpiUnknownDeviceNameError, name);

                SpanByte readBuffer = new byte[data.Items.Length];

                try
                {
                    spiDevice.TransferFullDuplex(data.ToSpanByte(), readBuffer);

                    var reponse = new MOGData(engine, readBuffer.ToArray());
                    engine.StackPush(reponse);

                    return EvalResult.NoError;
                }
                catch (Exception ex)
                {
                    return EvalResult.Failure(engine, SpiTransferError, name, $"failed to transfert to SPI device '{deviceName}'", ex.Message);
                }
            }
            else if (s[0] == typeof(MOGRef) && s[1] == typeof(MOGName))
            {
                var @ref = engine.StackPop() as MOGRef;
                var deviceName = engine.StackPop() as MOGName;

                var value = engine.VarRead(@ref.Value, false);

                if (value == null)
                    return EvalResult.Failure(engine, Error.UnknownNameError, name.ToString());

                engine.StackPush(deviceName);
                engine.StackPush(value);

                var r = PrimitiveSpiTransfer(engine, name);

                if (r.IsError)
                    return r;

                engine.StackDrop();

                return EvalResult.NoError;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveSpiMinClockFrequency(MogwaiNanoEngine engine, string name)
        {
            // bus spi.minClockFrequency

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var bus = engine.StackPop() as MOGNumber;
            var busNumber = (int)bus.Value;

            var busInfo = SpiDevice.GetBusInfo(busNumber);

            engine.StackPush(new MOGNumber(engine, busInfo.MinClockFrequency));

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveSpiMaxClockFrequency(MogwaiNanoEngine engine, string name)
        {
            // bus spi.maxClockFrequency

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var bus = engine.StackPop() as MOGNumber;
            var busNumber = (int)bus.Value;

            var busInfo = SpiDevice.GetBusInfo(busNumber);

            engine.StackPush(new MOGNumber(engine, busInfo.MaxClockFrequency));

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveSpiRead(MogwaiNanoEngine engine, string name)
        {
            // name length spi.read

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] != typeof(MOGNumber) || s[1] != typeof(MOGName))
                return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);

            var length = engine.StackPop() as MOGNumber;
            var deviceName = engine.StackPop() as MOGName;
            var spiDevice = GetSpiDevice(engine.EngineId, deviceName.Value);    

            if (spiDevice == null)
                return EvalResult.Failure(engine, Error.SpiUnknownDeviceNameError, name);

            if (length.Value < 0 || length.Value > 4096)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, "SPI read length must be between 0 and 4096");

            try
            {
                byte[] buffer = new byte[(int)length.Value];
                var span = new SpanByte(buffer);
                spiDevice.Read(span);

                var mogData = new MOGData(engine, buffer);
                engine.StackPush(mogData);

                return EvalResult.NoError;
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(engine, Error.SpiReadError, name, $"failed to read from SPI device '{deviceName.Value}'", ex.Message);
            }
        }

        private static SpiDevice GetSpiDevice(int engineId, string deviceName)
        {
            if (_spiDevices.Contains(engineId))
            {
                var spiDevices = _spiDevices[engineId] as Hashtable;

                if (spiDevices.Contains(deviceName))
                    return spiDevices[deviceName] as SpiDevice;
            }

            return null;
        }

        private static bool RemoveSpiDevice(int engineId, string deviceName)
        {
            if (_spiDevices.Contains(engineId))
            {
                var spiDevices = _spiDevices[engineId] as Hashtable;

                if (spiDevices.Contains(deviceName))
                {
                    spiDevices.Remove(deviceName);
                    return true;
                }
            }

            return false;
        }

        private static void AddSpiDevice(int engineId, string deviceName, SpiDevice spiDevice)
        {
            if (!_spiDevices.Contains(engineId))
                _spiDevices[engineId] = new Hashtable();

            var spiDevices = _spiDevices[engineId] as Hashtable;
            spiDevices[deviceName] = spiDevice;
        }
    }
}
